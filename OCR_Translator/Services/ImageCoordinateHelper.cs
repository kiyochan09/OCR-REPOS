using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class ImageCoordinateHelper
    {
        public const int ResizeHandleSize = 8;

        public enum ResizeMode
        {
            None, Left, Right, Top, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight
        }

        public static Rectangle ScreenToImage(Rectangle screenRect, PictureBox pictureBox)
        {
            if (pictureBox.Image == null) return Rectangle.Empty;

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
            if (pictureBox.Image == null) return Rectangle.Empty;

            float scaleX = (float)pictureBox.ClientSize.Width / pictureBox.Image.Width;
            float scaleY = (float)pictureBox.ClientSize.Height / pictureBox.Image.Height;
            float scale = Math.Min(scaleX, scaleY);

            float displayWidth = pictureBox.Image.Width * scale;
            float displayHeight = pictureBox.Image.Height * scale;
            float offsetX = (pictureBox.ClientSize.Width - displayWidth) / 2;
            float offsetY = (pictureBox.ClientSize.Height - displayHeight) / 2;

            return new Rectangle(
                (int)(offsetX + imageRect.X * scale),
                (int)(offsetY + imageRect.Y * scale),
                (int)(imageRect.Width * scale),
                (int)(imageRect.Height * scale));
        }

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

                Rectangle expanded = new Rectangle(
                    screenRect.X - tolerance, screenRect.Y - tolerance,
                    screenRect.Width + tolerance * 2, screenRect.Height + tolerance * 2);

                if (!expanded.Contains(point)) continue;

                float cx = screenRect.Left + screenRect.Width / 2f;
                float cy = screenRect.Top + screenRect.Height / 2f;
                double distance = Math.Sqrt(Math.Pow(point.X - cx, 2) + Math.Pow(point.Y - cy, 2));

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }
            return nearestIndex;
        }

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

            Rectangle left = new Rectangle(rect.Left - edge / 2, rect.Top + edge, edge, Math.Max(0, rect.Height - edge * 2));
            Rectangle right = new Rectangle(rect.Right - edge / 2, rect.Top + edge, edge, Math.Max(0, rect.Height - edge * 2));
            Rectangle top = new Rectangle(rect.Left + edge, rect.Top - edge / 2, Math.Max(0, rect.Width - edge * 2), edge);
            Rectangle bottom = new Rectangle(rect.Left + edge, rect.Bottom - edge / 2, Math.Max(0, rect.Width - edge * 2), edge);

            if (left.Contains(point)) return ResizeMode.Left;
            if (right.Contains(point)) return ResizeMode.Right;
            if (top.Contains(point)) return ResizeMode.Top;
            if (bottom.Contains(point)) return ResizeMode.Bottom;

            return ResizeMode.None;
        }

        public enum RuleLineType
        {
            None,
            Horizontal,
            Vertical
        }

        public enum RuleLineHitPart
        {
            None,
            StartHandle,
            EndHandle,
            LineBody
        }

        public static Point ScreenToImagePoint(Point screenPoint, PictureBox pictureBox)
        {
            if (pictureBox.Image == null) return Point.Empty;

            float scaleX = (float)pictureBox.ClientSize.Width / pictureBox.Image.Width;
            float scaleY = (float)pictureBox.ClientSize.Height / pictureBox.Image.Height;
            float scale = Math.Min(scaleX, scaleY);

            float displayWidth = pictureBox.Image.Width * scale;
            float displayHeight = pictureBox.Image.Height * scale;
            float offsetX = (pictureBox.ClientSize.Width - displayWidth) / 2;
            float offsetY = (pictureBox.ClientSize.Height - displayHeight) / 2;

            int x = (int)((screenPoint.X - offsetX) / scale);
            int y = (int)((screenPoint.Y - offsetY) / scale);

            x = Math.Max(0, Math.Min(x, pictureBox.Image.Width));
            y = Math.Max(0, Math.Min(y, pictureBox.Image.Height));

            return new Point(x, y);
        }

        public static Point ImageToScreenPoint(Point imagePoint, PictureBox pictureBox)
        {
            if (pictureBox.Image == null) return Point.Empty;

            float scaleX = (float)pictureBox.ClientSize.Width / pictureBox.Image.Width;
            float scaleY = (float)pictureBox.ClientSize.Height / pictureBox.Image.Height;
            float scale = Math.Min(scaleX, scaleY);

            float displayWidth = pictureBox.Image.Width * scale;
            float displayHeight = pictureBox.Image.Height * scale;
            float offsetX = (pictureBox.ClientSize.Width - displayWidth) / 2;
            float offsetY = (pictureBox.ClientSize.Height - displayHeight) / 2;

            return new Point(
                (int)(offsetX + imagePoint.X * scale),
                (int)(offsetY + imagePoint.Y * scale));
        }

        public static RuleLineHitPart HitTestLinePart(
            Point screenPoint,
            int handleTolerance,
            int bodyTolerance,
            TableRuleLine line,
            PictureBox pictureBox)
        {
            if (pictureBox.Image == null || line == null) return RuleLineHitPart.None;

            Point p1Img = line.IsVertical ? new Point(line.Pos, line.Start) : new Point(line.Start, line.Pos);
            Point p2Img = line.IsVertical ? new Point(line.Pos, line.End) : new Point(line.End, line.Pos);

            Point sp1 = ImageToScreenPoint(p1Img, pictureBox);
            Point sp2 = ImageToScreenPoint(p2Img, pictureBox);

            // 1. 端点1 (StartHandle)
            if (Math.Abs(screenPoint.X - sp1.X) <= handleTolerance &&
                Math.Abs(screenPoint.Y - sp1.Y) <= handleTolerance)
            {
                return RuleLineHitPart.StartHandle;
            }

            // 2. 端点2 (EndHandle)
            if (Math.Abs(screenPoint.X - sp2.X) <= handleTolerance &&
                Math.Abs(screenPoint.Y - sp2.Y) <= handleTolerance)
            {
                return RuleLineHitPart.EndHandle;
            }

            // 3. 罫線本体 (LineBody)
            if (!line.IsVertical)
            {
                int minX = Math.Min(sp1.X, sp2.X) - bodyTolerance;
                int maxX = Math.Max(sp1.X, sp2.X) + bodyTolerance;
                if (screenPoint.X >= minX && screenPoint.X <= maxX &&
                    Math.Abs(screenPoint.Y - sp1.Y) <= bodyTolerance)
                {
                    return RuleLineHitPart.LineBody;
                }
            }
            else
            {
                int minY = Math.Min(sp1.Y, sp2.Y) - bodyTolerance;
                int maxY = Math.Max(sp1.Y, sp2.Y) + bodyTolerance;
                if (screenPoint.Y >= minY && screenPoint.Y <= maxY &&
                    Math.Abs(screenPoint.X - sp1.X) <= bodyTolerance)
                {
                    return RuleLineHitPart.LineBody;
                }
            }

            return RuleLineHitPart.None;
        }

        public static bool HitTestTableRuleLines(
            Point screenPoint,
            int handleTolerance,
            int bodyTolerance,
            OcrRegion tableRegion,
            IEnumerable<int>? selectedLineIndices,
            PictureBox pictureBox,
            out int hitLineIndex,
            out RuleLineHitPart hitPart)
        {
            hitLineIndex = -1;
            hitPart = RuleLineHitPart.None;

            if (pictureBox.Image == null || tableRegion == null) return false;
            tableRegion.EnsureRuleLines();

            var selectedSet = selectedLineIndices != null ? new HashSet<int>(selectedLineIndices) : new HashSet<int>();

            // 優先度1: 選択中罫線の端点ハンドル判定
            foreach (int idx in selectedSet)
            {
                if (idx >= 0 && idx < tableRegion.RuleLines.Count)
                {
                    var selLine = tableRegion.RuleLines[idx];
                    var part = HitTestLinePart(screenPoint, handleTolerance, bodyTolerance, selLine, pictureBox);
                    if (part == RuleLineHitPart.StartHandle || part == RuleLineHitPart.EndHandle)
                    {
                        hitLineIndex = idx;
                        hitPart = part;
                        return true;
                    }
                }
            }

            // 優先度2: 他のすべての罫線の端点ハンドル判定
            for (int i = 0; i < tableRegion.RuleLines.Count; i++)
            {
                if (selectedSet.Contains(i)) continue;
                var part = HitTestLinePart(screenPoint, handleTolerance, bodyTolerance, tableRegion.RuleLines[i], pictureBox);
                if (part == RuleLineHitPart.StartHandle || part == RuleLineHitPart.EndHandle)
                {
                    hitLineIndex = i;
                    hitPart = part;
                    return true;
                }
            }

            // 優先度3: 選択中罫線の本体判定
            foreach (int idx in selectedSet)
            {
                if (idx >= 0 && idx < tableRegion.RuleLines.Count)
                {
                    var selLine = tableRegion.RuleLines[idx];
                    var part = HitTestLinePart(screenPoint, handleTolerance, bodyTolerance, selLine, pictureBox);
                    if (part == RuleLineHitPart.LineBody)
                    {
                        hitLineIndex = idx;
                        hitPart = part;
                        return true;
                    }
                }
            }

            // 優先度4: 他のすべての罫線の本体判定
            for (int i = 0; i < tableRegion.RuleLines.Count; i++)
            {
                if (selectedSet.Contains(i)) continue;
                var part = HitTestLinePart(screenPoint, handleTolerance, bodyTolerance, tableRegion.RuleLines[i], pictureBox);
                if (part == RuleLineHitPart.LineBody)
                {
                    hitLineIndex = i;
                    hitPart = part;
                    return true;
                }
            }

            return false;
        }

        public static bool HitTestTableRuleLines(
            Point screenPoint,
            int handleTolerance,
            int bodyTolerance,
            OcrRegion tableRegion,
            int selectedLineIndex,
            PictureBox pictureBox,
            out int hitLineIndex,
            out RuleLineHitPart hitPart)
        {
            var selList = selectedLineIndex >= 0 ? new[] { selectedLineIndex } : Array.Empty<int>();
            return HitTestTableRuleLines(
                screenPoint, handleTolerance, bodyTolerance, tableRegion, selList, pictureBox,
                out hitLineIndex, out hitPart);
        }
    }
}
