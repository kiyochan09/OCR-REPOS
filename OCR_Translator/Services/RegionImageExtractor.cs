using System;
using System.Drawing;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class RegionImageExtractor
    {
        public static Bitmap Crop(Bitmap source, OcrRegion region)
        {
            int x = Math.Max(0, region.X);
            int y = Math.Max(0, region.Y);
            int right = Math.Min(source.Width, region.X + region.Width);
            int bottom = Math.Min(source.Height, region.Y + region.Height);
            int width = right - x;
            int height = bottom - y;

            if (width <= 0 || height <= 0)
                throw new ArgumentException("OCR領域が画像の範囲外です。");

            return source.Clone(new Rectangle(x, y, width, height), source.PixelFormat);
        }
    }
}
