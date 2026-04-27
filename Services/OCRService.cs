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
        public string WarningMessage { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; }
        public string ConfidenceLabel { get; set; } = string.Empty;
        public string ConfidenceSummary { get; set; } = string.Empty;
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

        public async Task<string> AnalyzeImageQualityAsync(
            string imagePath,
            CancellationToken ct = default)
        {
#if ANDROID
            try
            {
                ct.ThrowIfCancellationRequested();
                var bitmap = await LoadBitmapAsync(imagePath);
                if (bitmap == null)
                    return string.Empty;

                try
                {
                    ct.ThrowIfCancellationRequested();
                    return BuildImageQualityWarning(bitmap);
                }
                finally
                {
                    if (!bitmap.IsRecycled)
                    {
                        bitmap.Recycle();
                    }

                    bitmap.Dispose();
                }
            }
            catch
            {
                return string.Empty;
            }
#else
            await Task.CompletedTask;
            return string.Empty;
#endif
        }

        public async Task<string> RecognizeTextAsync(
            string imagePath,
            IProgress<OcrProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
#if ANDROID
        System.Diagnostics.Debug.WriteLine("✅ REAL ML KIT OCR IS RUNNING");

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
                var imageWarning = BuildImageQualityWarning(bitmap);

                // ── Step 3 – Fix EXIF rotation ───────────────────────────────────
                Report(progress, 30, "Fixing orientation...", imageWarning);
                int degrees = GetExifRotation(imagePath);
                var rotated = RotateBitmap(bitmap, degrees);
                if (!ReferenceEquals(rotated, bitmap))
                {
                    bitmap.Recycle();
                    bitmap.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 4 – Grayscale ───────────────────────────────────────────
                Report(progress, 40, "Enhancing image...", imageWarning);
                var gray = ToGrayscale(rotated);
                rotated.Recycle();
                rotated.Dispose();

                ct.ThrowIfCancellationRequested();

                Report(progress, 46, "Straightening page...", imageWarning);
                var deskewed = DeskewBitmap(gray);
                if (!ReferenceEquals(deskewed, gray))
                {
                    gray.Recycle();
                    gray.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                Report(progress, 52, "Cropping document...", imageWarning);
                var cropped = CropToDocumentBounds(deskewed);
                if (!ReferenceEquals(cropped, deskewed))
                {
                    deskewed.Recycle();
                    deskewed.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 5 – Adaptive contrast based on image brightness ──────────
                // Measures average brightness first, then picks the right boost.
                // Dark images get a stronger lift; bright/washed-out images get less.
                Report(progress, 58, "Adjusting contrast...", imageWarning);
                float avgBrightness = GetAverageBrightness(cropped);
                float contrastAmount;
                if (avgBrightness < 80f)
                    contrastAmount = 3.5f;
                else if (avgBrightness < 150f)
                    contrastAmount = 2.8f;
                else
                    contrastAmount = 2.0f;

                var contrast = IncreaseContrast(cropped, contrastAmount);
                cropped.Recycle();
                cropped.Dispose();

                ct.ThrowIfCancellationRequested();

                // ── Step 6 – Sharpen to make text edges crisper ──────────────────
                Report(progress, 66, "Denoising...", imageWarning);
                var denoised = Denoise(contrast);
                if (!ReferenceEquals(denoised, contrast))
                {
                    contrast.Recycle();
                    contrast.Dispose();
                }

                Report(progress, 72, "Sharpening...", imageWarning);
                var sharpened = Sharpen(denoised);
                if (!ReferenceEquals(sharpened, denoised))
                {
                    denoised.Recycle();
                    denoised.Dispose();
                }

                ct.ThrowIfCancellationRequested();

                try
                {
                    Report(progress, 78, "Preparing for OCR...", imageWarning);
                    var inputImage = InputImage.FromBitmap(sharpened, 0);

                    ct.ThrowIfCancellationRequested();

                    Report(progress, 86, "Recognizing text...", imageWarning);
                    var result = await _recognizer.Value
                        .Process(inputImage)
                        .AsAsync<Java.Lang.Object>();

                    ct.ThrowIfCancellationRequested();

                    Report(progress, 94, "Cleaning up text...", imageWarning);
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

                    extractedText = NormalizeRecognizedText(extractedText);
                    var confidence = EstimateConfidence(extractedText, imageWarning);

                    string finalText = string.IsNullOrWhiteSpace(extractedText)
                        ? "No text could be detected in this image."
                        : extractedText;

                    _cache[imagePath] = finalText;
                    Report(progress, 100, "Done!", imageWarning, confidence.score, confidence.label, confidence.summary);
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

        private static void Report(
            IProgress<OcrProgressUpdate>? progress,
            int pct,
            string msg,
            string warning = "",
            double confidenceScore = 0,
            string confidenceLabel = "",
            string confidenceSummary = "") =>
            progress?.Report(new OcrProgressUpdate
            {
                Percentage = pct,
                StatusMessage = msg,
                WarningMessage = warning,
                ConfidenceScore = confidenceScore,
                ConfidenceLabel = confidenceLabel,
                ConfidenceSummary = confidenceSummary
            });

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
                    const int targetSize = 3072;
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

        private static Bitmap RotateBitmap(Bitmap source, float degrees)
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

        private static Bitmap DeskewBitmap(Bitmap source)
        {
            float angle = EstimateDeskewAngle(source);
            if (Math.Abs(angle) < 0.4f)
                return source;

            try
            {
                return RotateBitmap(source, angle);
            }
            catch
            {
                return source;
            }
        }

        private static float EstimateDeskewAngle(Bitmap source)
        {
            Bitmap sample = source;
            Bitmap? resized = null;

            try
            {
                int maxDimension = Math.Max(source.Width, source.Height);
                if (maxDimension > 600)
                {
                    float scale = 600f / maxDimension;
                    resized = Bitmap.CreateScaledBitmap(
                        source,
                        Math.Max(1, (int)(source.Width * scale)),
                        Math.Max(1, (int)(source.Height * scale)),
                        true);
                    sample = resized;
                }

                float bestAngle = 0f;
                double bestScore = double.MinValue;

                for (float angle = -6f; angle <= 6f; angle += 1f)
                {
                    using var rotated = RotateForAnalysis(sample, angle);
                    double score = ScoreHorizontalAlignment(rotated);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAngle = angle;
                    }
                }

                return bestAngle;
            }
            catch
            {
                return 0f;
            }
            finally
            {
                if (resized != null && !resized.IsRecycled)
                {
                    resized.Recycle();
                    resized.Dispose();
                }
            }
        }

        private static Bitmap RotateForAnalysis(Bitmap source, float angle)
        {
            if (Math.Abs(angle) < 0.01f)
                return source.Copy(source.GetConfig() ?? Bitmap.Config.Argb8888, false)!;

            return RotateBitmap(source, angle);
        }

        private static double ScoreHorizontalAlignment(Bitmap source)
        {
            float avgBrightness = GetAverageBrightness(source);
            int threshold = (int)Math.Clamp(avgBrightness - 25f, 60f, 200f);
            int stepY = Math.Max(1, source.Height / 160);
            int stepX = Math.Max(1, source.Width / 160);

            var rowCounts = new List<int>();
            for (int y = 0; y < source.Height; y += stepY)
            {
                int count = 0;
                for (int x = 0; x < source.Width; x += stepX)
                {
                    var pixel = source.GetPixel(x, y);
                    int brightness = Android.Graphics.Color.GetRedComponent(pixel);
                    if (brightness < threshold)
                        count++;
                }

                rowCounts.Add(count);
            }

            if (rowCounts.Count == 0)
                return 0;

            double mean = rowCounts.Average();
            double variance = 0;
            foreach (var count in rowCounts)
            {
                variance += Math.Pow(count - mean, 2);
            }

            return variance / rowCounts.Count;
        }

        private static Bitmap CropToDocumentBounds(Bitmap source)
        {
            try
            {
                float avgBrightness = GetAverageBrightness(source);
                int threshold = (int)Math.Clamp(avgBrightness - 20f, 70f, 220f);
                int step = Math.Max(2, Math.Min(source.Width, source.Height) / 300);

                int minX = source.Width;
                int minY = source.Height;
                int maxX = -1;
                int maxY = -1;

                for (int y = 0; y < source.Height; y += step)
                {
                    for (int x = 0; x < source.Width; x += step)
                    {
                        var pixel = source.GetPixel(x, y);
                        int brightness = Android.Graphics.Color.GetRedComponent(pixel);
                        if (brightness >= threshold)
                            continue;

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }

                if (maxX <= minX || maxY <= minY)
                    return source;

                int contentWidth = maxX - minX;
                int contentHeight = maxY - minY;
                if (contentWidth < source.Width * 0.2f || contentHeight < source.Height * 0.2f)
                    return source;

                int margin = Math.Max(12, step * 8);
                int left = Math.Max(0, minX - margin);
                int top = Math.Max(0, minY - margin);
                int right = Math.Min(source.Width, maxX + margin);
                int bottom = Math.Min(source.Height, maxY + margin);

                int width = right - left;
                int height = bottom - top;
                if (width <= 0 || height <= 0)
                    return source;

                return Bitmap.CreateBitmap(source, left, top, width, height);
            }
            catch
            {
                return source;
            }
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

        private static string BuildImageQualityWarning(Bitmap source)
        {
            var avgBrightness = GetAverageBrightness(source);
            var edgeStrength = GetEdgeStrength(source);

            var warnings = new List<string>();
            if (avgBrightness < 65f)
                warnings.Add("The photo looks dark");
            else if (avgBrightness > 225f)
                warnings.Add("The photo looks overexposed");

            if (edgeStrength < 18f)
                warnings.Add("The photo may be blurry");

            return warnings.Count == 0
                ? string.Empty
                : string.Join(". ", warnings) + ". OCR may miss some text.";
        }

        private static (double score, string label, string summary) EstimateConfidence(string text, string imageWarning)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "No text could be detected in this image.")
            {
                return (0.18, "Low confidence", "Very little readable text was detected. Retaking the photo will probably help.");
            }

            double score = 0.82;
            var lines = text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            int charCount = text.Count(char.IsLetterOrDigit);
            int shortLineCount = lines.Count(line => line.Length <= 3);
            int symbolCount = text.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
            double symbolRatio = text.Length == 0 ? 0 : (double)symbolCount / text.Length;

            if (!string.IsNullOrWhiteSpace(imageWarning))
                score -= 0.22;

            if (charCount < 40)
                score -= 0.18;
            else if (charCount < 120)
                score -= 0.08;

            if (lines.Length > 0)
            {
                double shortLineRatio = (double)shortLineCount / lines.Length;
                if (shortLineRatio > 0.35)
                    score -= 0.16;
                else if (shortLineRatio > 0.2)
                    score -= 0.08;
            }

            if (symbolRatio > 0.24)
                score -= 0.14;
            else if (symbolRatio > 0.16)
                score -= 0.07;

            score = Math.Clamp(score, 0.1, 0.98);

            if (score >= 0.75)
                return (score, "High confidence", "The scan looks strong. You should only need light edits, if any.");

            if (score >= 0.5)
                return (score, "Medium confidence", "The scan is usable, but review the text for missing lines or formatting issues.");

            return (score, "Low confidence", "This scan is likely unreliable. A clearer retake may give much better text.");
        }

        private static float GetEdgeStrength(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            int step = Math.Max(4, Math.Min(width, height) / 120);
            double total = 0;
            int count = 0;

            for (int y = step; y < height - step; y += step)
            {
                for (int x = step; x < width - step; x += step)
                {
                    var current = source.GetPixel(x, y);
                    var right = source.GetPixel(x + step, y);
                    var down = source.GetPixel(x, y + step);

                    int c = Android.Graphics.Color.GetRedComponent(current);
                    int r = Android.Graphics.Color.GetRedComponent(right);
                    int d = Android.Graphics.Color.GetRedComponent(down);

                    total += Math.Abs(c - r) + Math.Abs(c - d);
                    count += 2;
                }
            }

            return count == 0 ? 0f : (float)(total / count);
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

        private static Bitmap Denoise(Bitmap source)
        {
            // Mild box blur to smooth out noise before sharpening.
            // Kernel weights sum to 1 so brightness is preserved.
            var kernel = new float[]
            {
                1 / 9f, 1 / 9f, 1 / 9f,
                1 / 9f, 1 / 9f, 1 / 9f,
                1 / 9f, 1 / 9f, 1 / 9f
            };

            Android.Renderscripts.RenderScript? rs = null;
            try
            {
                rs = Android.Renderscripts.RenderScript.Create(Android.App.Application.Context);
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
                return source;
            }
            finally
            {
                rs?.Destroy();
            }
        }

        private static string NormalizeRecognizedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var lines = text
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "))
                .Where(line => !LooksLikeNoise(line))
                .ToList();

            var mergedLines = new List<string>();
            foreach (var line in lines)
            {
                if (mergedLines.Count == 0)
                {
                    mergedLines.Add(line);
                    continue;
                }

                var previous = mergedLines[^1];
                if (previous.EndsWith("-") && IsContinuation(line))
                {
                    mergedLines[^1] = previous[..^1] + line;
                    continue;
                }

                if (ShouldMergeIntoPrevious(previous, line))
                {
                    mergedLines[^1] = previous + " " + line;
                    continue;
                }

                mergedLines.Add(line);
            }

            return string.Join(Environment.NewLine, mergedLines)
                .Replace(" ,", ",")
                .Replace(" .", ".")
                .Replace(" :", ":")
                .Replace(" ;", ";")
                .Trim();
        }

        private static bool LooksLikeNoise(string line)
        {
            int letterOrDigitCount = line.Count(char.IsLetterOrDigit);
            return letterOrDigitCount == 0 && line.Length < 4;
        }

        private static bool IsContinuation(string line) =>
            !string.IsNullOrWhiteSpace(line) && char.IsLower(line[0]);

        private static bool ShouldMergeIntoPrevious(string previous, string current)
        {
            if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
                return false;

            char last = previous[^1];
            if (last is '.' or ':' or '!' or '?')
                return false;

            if (current.Length < 4)
                return true;

            return char.IsLower(current[0]);
        }
#endif
    }
}
