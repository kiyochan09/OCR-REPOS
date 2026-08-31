using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// PictureBox上のスクリーン座標と画像座標の相互変換、
    /// 領域ヒットテスト、リサイズモード判定を行う。
    /// </summary>
    public static class ImageCoordinateHelper
    {
        public const int ResizeHandleSize = 8;

        public enum ResizeMode
        {
            None,
            Left,
            Right,
            Top,
            Bottom,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        // =========================================================
        // 座標変換
        // =========================================================

        public static Rectangle ScreenToImage(Rectangle screenRect, PictureBox pictureBox)
        {
            if (pictureBox.Image == null)
                return Rectangle.Empty;

            float scaleX = (float)pictureBox.ClientSize.Width / pictureBox.Image.Width;
            float scaleY = (float)pictureBox.ClientSize.Height / pictureBox.Image.Height;
            float scale = Math.Min(scaleX, scaleY);

            float displayWidth = pictureBox.Image.Width * scale;
            float displayHeight = pictureBox.Image.Height * scale;

            float offsetX = (pictureBox.ClientSize.Width - displayWidth) / 2;
            float offsetY = (pictureBox.ClientSize.Height - displayHeight) / 2;

            int x = (int)((screenRect.X - offsetX) / scale);
            int y = (int)((screenRect.Y - offsetY) / scale);

            int width = (int)(screenRect.Width / scale);
            int height = (int)(screenRect.Height / scale);

            x = Math.Max(0, x);
            y = Math.Max(0, y);

            width = Math.Min(width, pictureBox.Image.Width - x);
            height = Math.Min(height, pictureBox.Image.Height - y);

            return new Rectangle(x, y, width, height);
        }

        public static Rectangle ImageToScreen(Rectangle imageRect, PictureBox pictureBox)
        {
            if (pictureBox.Image == null)
                return Rectangle.Empty;

            float scaleX = (float)pictureBox.ClientSize.Width / pictureBox.Image.Width;
            float scaleY = (float)pictureBox.ClientSize.Height / pictureBox.Image.Height;
            float scale = Math.Min(scaleX, scaleY);

            float displayWidth = pictureBox.Image.Width * scale;
            float displayHeight = pictureBox.Image.Height * scale;

            float offsetX = (pictureBox.ClientSize.Width - displayWidth) / 2;
            float offsetY = (pictureBox.ClientSize.Height - displayHeight) / 2;

            int x = (int)(offsetX + imageRect.X * scale);
            int y = (int)(offsetY + imageRect.Y * scale);
            int width = (int)(imageRect.Width * scale);
            int height = (int)(imageRect.Height * scale);

            return new Rectangle(x, y, width, height);
        }

        // =========================================================
        // ヒットテスト
        // =========================================================

        public static int HitTestRegionNear(Point point, int tolerance, List<OcrRegion> regions, PictureBox pictureBox)
        {
            int nearestIndex = -1;
            double nearestDistance = double.MaxValue;

            for (int i = 0; i < regions.Count; i++)
            {
                OcrRegion region = regions[i];
                Rectangle screenRect = ImageToScreen(
                    new Rectangle(region.X, region.Y, region.Width, region.Height),
                    pictureBox);

                Rectangle expandedRect = new Rectangle(
                    screenRect.X - tolerance,
                    screenRect.Y - tolerance,
                    screenRect.Width + tolerance * 2,
                    screenRect.Height + tolerance * 2);

                if (!expandedRect.Contains(point))
                    continue;

                float centerX = screenRect.Left + screenRect.Width / 2f;
                float centerY = screenRect.Top + screenRect.Height / 2f;

                double distance = Math.Sqrt(
                    Math.Pow(point.X - centerX, 2) +
                    Math.Pow(point.Y - centerY, 2));

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        // =========================================================
        // リサイズモード判定
        // =========================================================

        public static ResizeMode GetResizeMode(Point point, Rectangle rect)
        {
            int h = ResizeHandleSize;
            int edge = 20;

            Rectangle topLeft = new Rectangle(rect.Left - h / 2, rect.Top - h / 2, h, h);
            Rectangle topRight = new Rectangle(rect.Right - h / 2, rect.Top - h / 2, h, h);
            Rectangle bottomLeft = new Rectangle(rect.Left - h / 2, rect.Bottom - h / 2, h, h);
            Rectangle bottomRight = new Rectangle(rect.Right - h / 2, rect.Bottom - h / 2, h, h);

            if (topLeft.Contains(point)) return ResizeMode.TopLeft;
            if (topRight.Contains(point)) return ResizeMode.TopRight;
            if (bottomLeft.Contains(point)) return ResizeMode.BottomLeft;
            if (bottomRight.Contains(point)) return ResizeMode.BottomRight;

            Rectangle left = new Rectangle(
                rect.Left - edge / 2, rect.Top + edge, edge,
                Math.Max(0, rect.Height - edge * 2));
            Rectangle right = new Rectangle(
                rect.Right - edge / 2, rect.Top + edge, edge,
                Math.Max(0, rect.Height - edge * 2));
            Rectangle top = new Rectangle(
                rect.Left + edge, rect.Top - edge / 2,
                Math.Max(0, rect.Width - edge * 2), edge);
            Rectangle bottom = new Rectangle(
                rect.Left + edge, rect.Bottom - edge / 2,
                Math.Max(0, rect.Width - edge * 2), edge);

            if (left.Contains(point)) return ResizeMode.Left;
            if (right.Contains(point)) return ResizeMode.Right;
            if (top.Contains(point)) return ResizeMode.Top;
            if (bottom.Contains(point)) return ResizeMode.Bottom;

            return ResizeMode.None;
        }
    }
}
