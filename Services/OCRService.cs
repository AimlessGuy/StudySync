using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if ANDROID
using Android.Graphics;
using Android.Media;
using Android.Gms.Extensions;
using Android.Runtime;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text.Latin;
#endif

namespace StudySync.Services
{
    public class OcrProgressUpdate
    {
        public int Percentage { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class OCRService
    {
#if ANDROID
        // ── Singleton recognizer: initialized once, reused on every call ──────────
        private static readonly Lazy<ITextRecognizer> _recognizer = new(() =>
        {
            var options = new TextRecognizerOptions.Builder().Build();
            return TextRecognition.GetClient(options);
        });

        // ── Simple path-based result cache ────────────────────────────────────────
        private static readonly Dictionary<string, string> _cache = new();
#endif

        // ─────────────────────────────────────────────────────────────────────────
        // Public warm-up: call this when your OCR page loads so the
        // first real scan doesn't pay the cold-start cost.
        // ─────────────────────────────────────────────────────────────────────────
        public static void WarmUp()
        {
#if ANDROID
            _ = _recognizer.Value; // Forces Lazy<T> to initialize the ML model
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Main entry point
        //   progress  – bind this to your ProgressBar / status label in the VM
        //   ct        – pass a CancellationToken so the user can cancel mid-scan
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<string> RecognizeTextAsync(
            string imagePath,
            IProgress<OcrProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
#if ANDROID
            try
            {
                // ── Step 1 – Cache check ─────────────────────────────────────────
                Report(progress, 5, "Checking cache…");
                if (_cache.TryGetValue(imagePath, out var cached))
                {
                    Report(progress, 100, "Done!");
                    return cached;
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 2 – Load bitmap with calculated sample size ─────────────
                Report(progress, 15, "Loading image…");
                var bitmap = await LoadBitmapAsync(imagePath);
                if (bitmap == null)
                    return "Could not load image.";

                ct.ThrowIfCancellationRequested();

                // ── Step 3 – Fix EXIF rotation ───────────────────────────────────
                Report(progress, 30, "Fixing orientation…");
                int degrees = GetExifRotation(imagePath);
                var rotated = RotateBitmap(bitmap, degrees);
                if (!ReferenceEquals(rotated, bitmap))
                {
                    bitmap.Recycle();
                    bitmap.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 4 – Grayscale ───────────────────────────────────────────
                Report(progress, 45, "Enhancing image…");
                var gray = ToGrayscale(rotated);
                rotated.Recycle();
                rotated.Dispose();

                ct.ThrowIfCancellationRequested();

                // ── Step 5 – Contrast boost ──────────────────────────────────────
                var enhanced = IncreaseContrast(gray, contrast: 1.5f);
                gray.Recycle();
                gray.Dispose();

                ct.ThrowIfCancellationRequested();

                try
                {
                    // ── Step 6 – Build InputImage ────────────────────────────────
                    Report(progress, 60, "Preparing for OCR…");
                    // Rotation is already baked in from RotateBitmap, so pass 0 here
                    var inputImage = InputImage.FromBitmap(enhanced, 0);

                    ct.ThrowIfCancellationRequested();

                    // ── Step 7 – Run OCR ─────────────────────────────────────────
                    Report(progress, 75, "Recognizing text…");
                    var result = await _recognizer.Value
                        .Process(inputImage)
                        .AsAsync<Java.Lang.Object>();

                    ct.ThrowIfCancellationRequested();

                    // ── Step 8 – Extract text ────────────────────────────────────
                    Report(progress, 90, "Extracting results…");
                    string extractedText = string.Empty;

                    if (result is IText textResult)
                        extractedText = textResult.Text;
                    else
                        extractedText = result?.ToString() ?? string.Empty;

                    string finalText = string.IsNullOrWhiteSpace(extractedText)
                        ? "No text could be detected in this image."
                        : extractedText.Trim();

                    // ── Step 9 – Cache & return ──────────────────────────────────
                    _cache[imagePath] = finalText;
                    Report(progress, 100, "Done!");
                    return finalText;
                }
                finally
                {
                    if (!enhanced.IsRecycled)
                    {
                        enhanced.Recycle();
                        enhanced.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Report(progress, 0, "Cancelled.");
                return "OCR was cancelled.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                Report(progress, 0, "Error.");
                return $"Error processing image: {ex.Message}";
            }
#else
            Report(progress, 50, "Simulating OCR…");
            await Task.Delay(1000, ct);
            Report(progress, 100, "Done!");
            return "OCR is only available on Android.\n\nThis is placeholder text for other platforms.";
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Clears the in-memory cache (e.g. call after user deletes a scan)
        // ─────────────────────────────────────────────────────────────────────────
        public static void ClearCache()
        {
#if ANDROID
            _cache.Clear();
#endif
        }

        public static void RemoveFromCache(string imagePath)
        {
#if ANDROID
            _cache.Remove(imagePath);
#endif
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static void Report(IProgress<OcrProgressUpdate>? progress, int pct, string msg) =>
            progress?.Report(new OcrProgressUpdate { Percentage = pct, StatusMessage = msg });

#if ANDROID
        /// <summary>
        /// Decodes the file with a dynamically calculated InSampleSize
        /// so we never over- or under-sample the image.
        /// </summary>
        private static async Task<Bitmap?> LoadBitmapAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // First pass – read dimensions only (very cheap, no pixel data)
                    var boundsOpts = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(imagePath, boundsOpts);

                    // Target ~1024px on the longest side for a good OCR/memory balance
                    const int targetSize = 1024;
                    int sampleSize = 1;
                    int h = boundsOpts.OutHeight;
                    int w = boundsOpts.OutWidth;
                    while (h / sampleSize > targetSize || w / sampleSize > targetSize)
                        sampleSize *= 2;

                    // Second pass – actually decode
                    var decodeOpts = new BitmapFactory.Options
                    {
                        InPreferredConfig = Bitmap.Config.Argb8888,
                        InSampleSize = sampleSize
                    };

                    var bmp = BitmapFactory.DecodeFile(imagePath, decodeOpts);
                    if (bmp == null)
                        System.Diagnostics.Debug.WriteLine($"Failed to decode: {imagePath}");

                    return bmp;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Bitmap load error: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Reads EXIF orientation from the image file and returns degrees.
        /// </summary>
        private static int GetExifRotation(string imagePath)
        {
            try
            {
                var exif = new ExifInterface(imagePath);
                int orientation = exif.GetAttributeInt(
                    ExifInterface.TagOrientation,
                    (int)Android.Media.Orientation.Normal);

                return orientation switch
                {
                    (int)Android.Media.Orientation.Rotate90  => 90,
                    (int)Android.Media.Orientation.Rotate180 => 180,
                    (int)Android.Media.Orientation.Rotate270 => 270,
                    _ => 0
                };
            }
            catch
            {
                return 0; // If EXIF can't be read, assume upright
            }
        }

        /// <summary>
        /// Returns a rotation-corrected bitmap.
        /// Returns the original reference unchanged when degrees == 0.
        /// </summary>
        private static Bitmap RotateBitmap(Bitmap source, int degrees)
        {
            if (degrees == 0) return source;
            var matrix = new Android.Graphics.Matrix();
            matrix.PostRotate(degrees);
            return Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
        }

        /// <summary>
        /// Strips colour so ML Kit doesn't waste time on irrelevant channel data.
        /// </summary>
        private static Bitmap ToGrayscale(Bitmap source)
        {
            var result = Bitmap.CreateBitmap(source.Width, source.Height, Bitmap.Config.Argb8888)!;
            var canvas = new Canvas(result);
            var cm = new ColorMatrix();
            cm.SetSaturation(0);
            var paint = new Android.Graphics.Paint();
            paint.SetColorFilter(new ColorMatrixColorFilter(cm));
            canvas.DrawBitmap(source, 0, 0, paint);
            return result;
        }

        /// <summary>
        /// Boosts contrast to help ML Kit distinguish text from background.
        /// contrast: 1.0 = unchanged, 1.5 = 50% boost (good default).
        /// </summary>
        private static Bitmap IncreaseContrast(Bitmap source, float contrast = 1.5f)
        {
            float t = (1f - contrast) / 2f * 255f;
            var cm = new ColorMatrix(new float[]
            {
                contrast, 0,        0,        0, t,
                0,        contrast, 0,        0, t,
                0,        0,        contrast, 0, t,
                0,        0,        0,        1, 0
            });
            var paint = new Android.Graphics.Paint();
            paint.SetColorFilter(new ColorMatrixColorFilter(cm));
            var result = Bitmap.CreateBitmap(source.Width, source.Height, Bitmap.Config.Argb8888)!;
            new Canvas(result).DrawBitmap(source, 0, 0, paint);
            return result;
        }
#endif
    }
}