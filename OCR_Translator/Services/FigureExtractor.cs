using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// 図（画像）領域のクロップおよび500KB以下への自動圧縮を行うサービス
    /// </summary>
    public static class FigureExtractor
    {
        public const int DefaultMaxBytes = 500 * 1024; // 500 KB

        /// <summary>
        /// ページ画像から指定領域を図として切り出し、500KB以下に圧縮して返します。
        /// </summary>
        public static FigureItem? CropAndCompressFigure(
            Bitmap sourceBitmap,
            OcrRegion region,
            int pageNumber,
            int maxBytes = DefaultMaxBytes)
        {
            if (sourceBitmap == null) return null;

            int x = Math.Max(0, region.X);
            int y = Math.Max(0, region.Y);
            int right = Math.Min(sourceBitmap.Width, region.X + region.Width);
            int bottom = Math.Min(sourceBitmap.Height, region.Y + region.Height);
            int width = right - x;
            int height = bottom - y;

            if (width < 5 || height < 5) return null;

            Rectangle cropRect = new Rectangle(x, y, width, height);
            using Bitmap cropped = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(sourceBitmap, new Rectangle(0, 0, width, height), cropRect, GraphicsUnit.Pixel);
            }

            var (imageBytes, mimeType) = CompressBitmapUnderLimit(cropped, maxBytes);
            if (imageBytes == null || imageBytes.Length == 0) return null;

            // UI描画用のBitmapを生成
            MemoryStream ms = new MemoryStream(imageBytes);
            Image finalImage = Image.FromStream(ms);

            return new FigureItem
            {
                PageNumber = pageNumber,
                Name = string.IsNullOrWhiteSpace(region.Name) || region.Name == "本文" ? "図" : region.Name,
                Bounds = cropRect,
                ImageBytes = imageBytes,
                MimeType = mimeType,
                Image = finalImage
            };
        }

        /// <summary>
        /// 画像を指定バイト数（デフォルト500KB）以下に最適化・圧縮します。
        /// </summary>
        public static (byte[] bytes, string mimeType) CompressBitmapUnderLimit(Bitmap bmp, int maxBytes)
        {
            // 1. まずPNGで試行（無劣化）
            using (MemoryStream msPng = new MemoryStream())
            {
                bmp.Save(msPng, ImageFormat.Png);
                if (msPng.Length <= maxBytes)
                {
                    return (msPng.ToArray(), "image/png");
                }
            }

            // 2. PNGで超える場合はJPEG品質調整
            ImageCodecInfo? jpegCodec = GetEncoder(ImageFormat.Jpeg);
            if (jpegCodec == null)
            {
                // フォールバック: 標準PNG
                using MemoryStream fallbackMs = new MemoryStream();
                bmp.Save(fallbackMs, ImageFormat.Jpeg);
                return (fallbackMs.ToArray(), "image/jpeg");
            }

            int[] qualityLevels = { 92, 85, 78, 70, 60, 50, 40 };
            foreach (int q in qualityLevels)
            {
                using EncoderParameters encParams = new EncoderParameters(1);
                encParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);

                using MemoryStream msJpeg = new MemoryStream();
                bmp.Save(msJpeg, jpegCodec, encParams);
                if (msJpeg.Length <= maxBytes)
                {
                    return (msJpeg.ToArray(), "image/jpeg");
                }
            }

            // 3. それでも超える場合は段階的に解像度を縮小（0.85x, 0.7x, 0.55x）
            double[] scaleFactors = { 0.85, 0.70, 0.55, 0.40, 0.30 };
            foreach (double scale in scaleFactors)
            {
                int newW = Math.Max(10, (int)(bmp.Width * scale));
                int newH = Math.Max(10, (int)(bmp.Height * scale));

                using Bitmap scaled = new Bitmap(newW, newH, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(bmp, 0, 0, newW, newH);
                }

                foreach (int q in new[] { 85, 75, 60, 45 })
                {
                    using EncoderParameters encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)q);

                    using MemoryStream msScaled = new MemoryStream();
                    scaled.Save(msScaled, jpegCodec, encParams);
                    if (msScaled.Length <= maxBytes)
                    {
                        return (msScaled.ToArray(), "image/jpeg");
                    }
                }
            }

            // 最終手段: 最小圧縮結果を返す
            using MemoryStream finalMs = new MemoryStream();
            using (EncoderParameters encParams = new EncoderParameters(1))
            {
                encParams.Param[0] = new EncoderParameter(Encoder.Quality, 35L);
                bmp.Save(finalMs, jpegCodec, encParams);
            }
            return (finalMs.ToArray(), "image/jpeg");
        }

        private static ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
        }
    }
}
