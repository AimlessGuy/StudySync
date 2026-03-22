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
        private static readonly Lazy<ITextRecognizer> _recognizer = new(() =>
        {
            var options = new TextRecognizerOptions.Builder().Build();
            return TextRecognition.GetClient(options);
        });

        private static readonly Dictionary<string, string> _cache = new();
#endif

        public static void WarmUp()
        {
#if ANDROID
            _ = _recognizer.Value;
#endif
        }

        public async Task<string> RecognizeTextAsync(
            string imagePath,
            IProgress<OcrProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
#if ANDROID
            try
            {
                Report(progress, 5, "Checking cache...");
                if (_cache.TryGetValue(imagePath, out var cached))
                {
                    Report(progress, 100, "Done!");
                    return cached;
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 2 – Load at higher resolution for better accuracy ────────
                Report(progress, 15, "Loading image...");
                var bitmap = await LoadBitmapAsync(imagePath);
                if (bitmap == null)
                    return "Could not load image.";

                ct.ThrowIfCancellationRequested();

                // ── Step 3 – Fix EXIF rotation ───────────────────────────────────
                Report(progress, 30, "Fixing orientation...");
                int degrees = GetExifRotation(imagePath);
                var rotated = RotateBitmap(bitmap, degrees);
                if (!ReferenceEquals(rotated, bitmap))
                {
                    bitmap.Recycle();
                    bitmap.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 4 – Grayscale ───────────────────────────────────────────
                Report(progress, 40, "Enhancing image...");
                var gray = ToGrayscale(rotated);
                rotated.Recycle();
                rotated.Dispose();

                ct.ThrowIfCancellationRequested();

                // ── Step 5 – Adaptive contrast based on image brightness ──────────
                // Measures average brightness first, then picks the right boost.
                // Dark images get a stronger lift; bright/washed-out images get less.
                Report(progress, 50, "Adjusting contrast...");
                float avgBrightness = GetAverageBrightness(gray);
                float contrastAmount;
                if (avgBrightness < 80f)
                    contrastAmount = 2.0f;      // Dark image — strong boost
                else if (avgBrightness < 150f)
                    contrastAmount = 1.6f;      // Normal image — moderate boost
                else
                    contrastAmount = 1.2f;      // Already bright — gentle boost

                var contrast = IncreaseContrast(gray, contrastAmount);
                gray.Recycle();
                gray.Dispose();

                ct.ThrowIfCancellationRequested();

                // ── Step 6 – Sharpen to make text edges crisper ──────────────────
                Report(progress, 60, "Sharpening...");
                var sharpened = Sharpen(contrast);
                contrast.Recycle();
                contrast.Dispose();

                ct.ThrowIfCancellationRequested();

                try
                {
                    Report(progress, 70, "Preparing for OCR...");
                    var inputImage = InputImage.FromBitmap(sharpened, 0);

                    ct.ThrowIfCancellationRequested();

                    Report(progress, 80, "Recognizing text...");
                    var result = await _recognizer.Value
                        .Process(inputImage)
                        .AsAsync<Java.Lang.Object>();

                    ct.ThrowIfCancellationRequested();

                    Report(progress, 90, "Extracting results...");
                    string extractedText = string.Empty;

                    var textObj = result?.JavaCast<Xamarin.Google.MLKit.Vision.Text.Text>();
                    if (textObj?.TextBlocks != null && textObj.TextBlocks.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var block in textObj.TextBlocks)
                        {
                            if (!string.IsNullOrWhiteSpace(block.Text))
                                sb.AppendLine(block.Text);
                        }
                        extractedText = sb.ToString().Trim();
                    }

                    string finalText = string.IsNullOrWhiteSpace(extractedText)
                        ? "No text could be detected in this image."
                        : extractedText;

                    _cache[imagePath] = finalText;
                    Report(progress, 100, "Done!");
                    return finalText;
                }
                finally
                {
                    if (!sharpened.IsRecycled)
                    {
                        sharpened.Recycle();
                        sharpened.Dispose();
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
            Report(progress, 50, "Simulating OCR...");
            await Task.Delay(1000, ct);
            Report(progress, 100, "Done!");
            return "OCR is only available on Android.\n\nThis is placeholder text for other platforms.";
#endif
        }

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

        private static void Report(IProgress<OcrProgressUpdate>? progress, int pct, string msg) =>
            progress?.Report(new OcrProgressUpdate { Percentage = pct, StatusMessage = msg });

#if ANDROID
        private static async Task<Bitmap?> LoadBitmapAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var boundsOpts = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(imagePath, boundsOpts);

                    // FIX 1: Increased from 1024 → 2048 for better accuracy on
                    // handwritten notes. Higher res = more detail for ML Kit to work with.
                    const int targetSize = 2048;
                    int sampleSize = 1;
                    int h = boundsOpts.OutHeight;
                    int w = boundsOpts.OutWidth;
                    while (h / sampleSize > targetSize || w / sampleSize > targetSize)
                        sampleSize *= 2;

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
                    (int)Android.Media.Orientation.Rotate90 => 90,
                    (int)Android.Media.Orientation.Rotate180 => 180,
                    (int)Android.Media.Orientation.Rotate270 => 270,
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }

        private static Bitmap RotateBitmap(Bitmap source, int degrees)
        {
            if (degrees == 0) return source;
            var matrix = new Android.Graphics.Matrix();
            matrix.PostRotate(degrees);
            return Bitmap.CreateBitmap(source, 0, 0, source.Width, source.Height, matrix, true);
        }

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

        // FIX 2: Measures average pixel brightness so we know how much
        // contrast to apply. Samples every 10th pixel for speed.
        private static float GetAverageBrightness(Bitmap source)
        {
            return Task.Run(() =>
            {
                int w = source.Width;
                int h = source.Height;
                long total = 0;
                int count = 0;
                int step = 10; // sample every 10th pixel

                for (int y = 0; y < h; y += step)
                {
                    for (int x = 0; x < w; x += step)
                    {
                        var pixel = source.GetPixel(x, y);
                        int r = Android.Graphics.Color.GetRedComponent(pixel);
                        total += r; // grayscale so R=G=B
                        count++;
                    }
                }

                return count > 0 ? (float)total / count : 128f;
            }).Result;
        }

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

        // FIX 3: Sharpening pass using a convolution kernel.
        // Makes text edges crisper so ML Kit can distinguish letters better,
        // especially for messy handwriting or slightly blurry photos.
        private static Bitmap Sharpen(Bitmap source)
        {
            // Standard unsharp/sharpen kernel
            var kernel = new float[]
            {
                 0f, -1f,  0f,
                -1f,  5f, -1f,
                 0f, -1f,  0f
            };

            var paint = new Android.Graphics.Paint();
            paint.SetColorFilter(null);

            var rs = Android.Renderscripts.RenderScript.Create(
                Android.App.Application.Context);
            try
            {
                var alloc = Android.Renderscripts.Allocation.CreateFromBitmap(rs, source)!;
                var outAlloc = Android.Renderscripts.Allocation.CreateTyped(rs, alloc.Type)!;

                var script = Android.Renderscripts.ScriptIntrinsicConvolve3x3
                    .Create(rs, Android.Renderscripts.Element.U8_4(rs))!;

                script.SetInput(alloc);
                script.SetCoefficients(kernel);
                script.ForEach(outAlloc);

                var result = Bitmap.CreateBitmap(source.Width, source.Height, Bitmap.Config.Argb8888)!;
                outAlloc.CopyTo(result);

                alloc.Destroy();
                outAlloc.Destroy();
                script.Destroy();

                return result;
            }
            catch
            {
                // If RenderScript fails (older devices), return original unchanged
                rs?.Destroy();
                return source;
            }
            finally
            {
                rs?.Destroy();
            }
        }
#endif
    }
}