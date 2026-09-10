using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace OCR_Translator.Services
{
    public static class ColorIconHelper
    {
        public static Bitmap CreateColorIcon(string iconName, int size = 32)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                float s = size / 32f; // scale factor relative to 32px base

                switch (iconName)
                {
                    case "open_pdf":
                        DrawOpenPdf(g, s);
                        break;
                    case "close_pdf":
                        DrawClosePdf(g, s);
                        break;
                    case "first_page":
                        DrawFirstPage(g, s);
                        break;
                    case "prev_page":
                        DrawPrevPage(g, s);
                        break;
                    case "next_page":
                        DrawNextPage(g, s);
                        break;
                    case "last_page":
                        DrawLastPage(g, s);
                        break;
                    case "next_batch":
                        DrawNextBatch(g, s);
                        break;
                    case "zoom_in":
                        DrawZoomIn(g, s);
                        break;
                    case "zoom_out":
                        DrawZoomOut(g, s);
                        break;
                    case "region_settings":
                        DrawSettingsGear(g, s, Color.FromArgb(79, 70, 229));
                        break;
                    case "reorder_mode":
                        DrawReorder(g, s);
                        break;
                    case "auto_layout":
                        DrawRobot(g, s);
                        break;
                    case "start_ocr":
                        DrawStartOcr(g, s);
                        break;
                    case "add_heading":
                        DrawHeading(g, s);
                        break;
                    case "add_footnote":
                        DrawFootnote(g, s);
                        break;
                    case "add_annotation":
                        DrawAnnotationTag(g, s);
                        break;
                    case "export_word":
                        DrawExportWord(g, s);
                        break;
                    case "search":
                        DrawSearch(g, s);
                        break;
                    case "options":
                        DrawSettingsGear(g, s, Color.FromArgb(59, 130, 246));
                        break;
                    default:
                        DrawDefault(g, s);
                        break;
                }
            }
            return bmp;
        }

        private static void DrawOpenPdf(Graphics g, float s)
        {
            // Back folder tab (Dark Amber)
            using (var bTab = new SolidBrush(Color.FromArgb(217, 119, 6)))
            {
                g.FillPath(bTab, RoundedRect(3 * s, 6 * s, 11 * s, 8 * s, 2 * s));
            }
            // PDF Document inside folder (White with red badge)
            using (var bDoc = new SolidBrush(Color.White))
            using (var pDoc = new Pen(Color.FromArgb(203, 213, 225), 1 * s))
            {
                var docRect = new RectangleF(10 * s, 4 * s, 14 * s, 18 * s);
                g.FillRectangle(bDoc, docRect);
                g.DrawRectangle(pDoc, docRect.X, docRect.Y, docRect.Width, docRect.Height);
            }
            // PDF Red accent strip on document
            using (var bPdf = new SolidBrush(Color.FromArgb(239, 68, 68)))
            {
                g.FillRectangle(bPdf, 12 * s, 7 * s, 10 * s, 3 * s);
            }
            // Front folder flap (Golden Amber with gradient)
            using (var bFlap = new LinearGradientBrush(
                new PointF(2 * s, 11 * s), new PointF(2 * s, 27 * s),
                Color.FromArgb(251, 191, 36), Color.FromArgb(245, 158, 11)))
            {
                g.FillPath(bFlap, RoundedRect(2 * s, 11 * s, 28 * s, 16 * s, 3 * s));
            }
        }

        private static void DrawClosePdf(Graphics g, float s)
        {
            // Red Book cover
            using (var bBook = new LinearGradientBrush(
                new PointF(5 * s, 4 * s), new PointF(27 * s, 28 * s),
                Color.FromArgb(239, 68, 68), Color.FromArgb(185, 28, 28)))
            {
                g.FillPath(bBook, RoundedRect(5 * s, 4 * s, 22 * s, 24 * s, 3 * s));
            }
            // Book spine & pages
            using (var bPages = new SolidBrush(Color.FromArgb(254, 242, 242)))
            {
                g.FillRectangle(bPages, 23 * s, 6 * s, 3 * s, 20 * s);
            }
            // White Close Cross 'X'
            using (var pCross = new Pen(Color.White, 2.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pCross, 11 * s, 11 * s, 19 * s, 21 * s);
                g.DrawLine(pCross, 19 * s, 11 * s, 11 * s, 21 * s);
            }
        }

        private static void DrawFirstPage(Graphics g, float s)
        {
            using var bBlue = new SolidBrush(Color.FromArgb(37, 99, 235));
            // Left vertical bar
            g.FillPath(bBlue, RoundedRect(5 * s, 6 * s, 4 * s, 20 * s, 1.5f * s));
            // Double left arrows
            PointF[] arrow1 = { new PointF(18 * s, 6 * s), new PointF(10 * s, 16 * s), new PointF(18 * s, 26 * s) };
            PointF[] arrow2 = { new PointF(26 * s, 6 * s), new PointF(18 * s, 16 * s), new PointF(26 * s, 26 * s) };
            g.FillPolygon(bBlue, arrow1);
            g.FillPolygon(bBlue, arrow2);
        }

        private static void DrawPrevPage(Graphics g, float s)
        {
            using var bBlue = new SolidBrush(Color.FromArgb(37, 99, 235));
            PointF[] arrow = { new PointF(23 * s, 5 * s), new PointF(9 * s, 16 * s), new PointF(23 * s, 27 * s) };
            g.FillPolygon(bBlue, arrow);
        }

        private static void DrawNextPage(Graphics g, float s)
        {
            using var bBlue = new SolidBrush(Color.FromArgb(37, 99, 235));
            PointF[] arrow = { new PointF(9 * s, 5 * s), new PointF(23 * s, 16 * s), new PointF(9 * s, 27 * s) };
            g.FillPolygon(bBlue, arrow);
        }

        private static void DrawLastPage(Graphics g, float s)
        {
            using var bBlue = new SolidBrush(Color.FromArgb(37, 99, 235));
            // Double right arrows
            PointF[] arrow1 = { new PointF(6 * s, 6 * s), new PointF(14 * s, 16 * s), new PointF(6 * s, 26 * s) };
            PointF[] arrow2 = { new PointF(14 * s, 6 * s), new PointF(22 * s, 16 * s), new PointF(14 * s, 26 * s) };
            g.FillPolygon(bBlue, arrow1);
            g.FillPolygon(bBlue, arrow2);
            // Right vertical bar
            g.FillPath(bBlue, RoundedRect(23 * s, 6 * s, 4 * s, 20 * s, 1.5f * s));
        }

        private static void DrawNextBatch(Graphics g, float s)
        {
            // Stack of 3 colorful books (Bottom: Blue, Middle: Green, Top: Orange/Gold)
            // Book 1 (Blue)
            using (var b1 = new SolidBrush(Color.FromArgb(37, 99, 235)))
            {
                g.FillPath(b1, RoundedRect(3 * s, 20 * s, 26 * s, 7 * s, 2 * s));
            }
            using (var bPages1 = new SolidBrush(Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(bPages1, 24 * s, 21.5f * s, 3.5f * s, 4 * s);
            }

            // Book 2 (Emerald Green)
            using (var b2 = new SolidBrush(Color.FromArgb(16, 185, 129)))
            {
                g.FillPath(b2, RoundedRect(5 * s, 12 * s, 24 * s, 7 * s, 2 * s));
            }
            using (var bPages2 = new SolidBrush(Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(bPages2, 24 * s, 13.5f * s, 3.5f * s, 4 * s);
            }

            // Book 3 (Amber Orange / Red)
            using (var b3 = new SolidBrush(Color.FromArgb(245, 158, 11)))
            {
                g.FillPath(b3, RoundedRect(4 * s, 4 * s, 25 * s, 7 * s, 2 * s));
            }
            using (var bPages3 = new SolidBrush(Color.FromArgb(241, 245, 249)))
            {
                g.FillRectangle(bPages3, 24 * s, 5.5f * s, 3.5f * s, 4 * s);
            }

            // White bookmark ribbon on top book
            using (var bRibbon = new SolidBrush(Color.FromArgb(239, 68, 68)))
            {
                g.FillRectangle(bRibbon, 9 * s, 4 * s, 3 * s, 5 * s);
            }
        }

        private static void DrawZoomIn(Graphics g, float s)
        {
            // Vibrant Blue circle
            using (var bCircle = new SolidBrush(Color.FromArgb(59, 130, 246)))
            {
                g.FillEllipse(bCircle, 3 * s, 3 * s, 26 * s, 26 * s);
            }
            // Crisp White '+'
            using (var pPlus = new Pen(Color.White, 3f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pPlus, 8 * s, 16 * s, 24 * s, 16 * s);
                g.DrawLine(pPlus, 16 * s, 8 * s, 16 * s, 24 * s);
            }
        }

        private static void DrawZoomOut(Graphics g, float s)
        {
            // Vibrant Slate/Blue circle
            using (var bCircle = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                g.FillEllipse(bCircle, 3 * s, 3 * s, 26 * s, 26 * s);
            }
            // Crisp White '-'
            using (var pMinus = new Pen(Color.White, 3f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pMinus, 8 * s, 16 * s, 24 * s, 16 * s);
            }
        }

        private static void DrawSettingsGear(Graphics g, float s, Color gearColor)
        {
            // Gear body
            using (var bGear = new SolidBrush(gearColor))
            {
                // Central circle
                g.FillEllipse(bGear, 6 * s, 6 * s, 20 * s, 20 * s);

                // 8 cogs
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    float cx = 16 * s + (float)(Math.Cos(angle) * 11 * s);
                    float cy = 16 * s + (float)(Math.Sin(angle) * 11 * s);
                    g.FillEllipse(bGear, cx - 3 * s, cy - 3 * s, 6 * s, 6 * s);
                }
            }
            // Center cutout
            using (var bHole = new SolidBrush(Color.White))
            {
                g.FillEllipse(bHole, 11 * s, 11 * s, 10 * s, 10 * s);
            }
        }

        private static void DrawReorder(Graphics g, float s)
        {
            // Indigo badge background
            using (var bBg = new SolidBrush(Color.FromArgb(99, 102, 241)))
            {
                g.FillPath(bBg, RoundedRect(3 * s, 4 * s, 26 * s, 24 * s, 4 * s));
            }
            // "1 2 3" text
            using (var f = new Font("Segoe UI", 8.5f * s, FontStyle.Bold))
            using (var bText = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("123", f, bText, new RectangleF(3 * s, 4 * s, 26 * s, 24 * s), sf);
            }
        }

        private static void DrawRobot(Graphics g, float s)
        {
            // Cyan Robot Head
            using (var bHead = new LinearGradientBrush(
                new PointF(5 * s, 8 * s), new PointF(27 * s, 26 * s),
                Color.FromArgb(6, 182, 212), Color.FromArgb(14, 116, 144)))
            {
                g.FillPath(bHead, RoundedRect(5 * s, 8 * s, 22 * s, 18 * s, 4 * s));
            }
            // Antenna
            using (var pAntenna = new Pen(Color.FromArgb(6, 182, 212), 2 * s))
            {
                g.DrawLine(pAntenna, 16 * s, 8 * s, 16 * s, 4 * s);
            }
            using (var bTip = new SolidBrush(Color.FromArgb(239, 68, 68)))
            {
                g.FillEllipse(bTip, 14 * s, 2 * s, 4 * s, 4 * s);
            }
            // Eyes (Glowing Yellow/White)
            using (var bEye = new SolidBrush(Color.FromArgb(254, 240, 138)))
            {
                g.FillEllipse(bEye, 9 * s, 13 * s, 4 * s, 4 * s);
                g.FillEllipse(bEye, 19 * s, 13 * s, 4 * s, 4 * s);
            }
            // Mouth
            using (var pMouth = new Pen(Color.White, 1.5f * s))
            {
                g.DrawLine(pMouth, 10 * s, 21 * s, 22 * s, 21 * s);
            }
        }

        private static void DrawStartOcr(Graphics g, float s)
        {
            // Emerald Green Circle
            using (var bCircle = new SolidBrush(Color.FromArgb(16, 185, 129)))
            {
                g.FillEllipse(bCircle, 3 * s, 3 * s, 26 * s, 26 * s);
            }
            // White Play Triangle
            using (var bPlay = new SolidBrush(Color.White))
            {
                PointF[] tri = { new PointF(12 * s, 9 * s), new PointF(23 * s, 16 * s), new PointF(12 * s, 23 * s) };
                g.FillPolygon(bPlay, tri);
            }
        }

        private static void DrawHeading(Graphics g, float s)
        {
            // Purple Document
            using (var bDoc = new SolidBrush(Color.White))
            using (var pDoc = new Pen(Color.FromArgb(139, 92, 246), 2 * s))
            {
                g.FillPath(bDoc, RoundedRect(5 * s, 4 * s, 22 * s, 24 * s, 3 * s));
                g.DrawPath(pDoc, RoundedRect(5 * s, 4 * s, 22 * s, 24 * s, 3 * s));
            }
            // Purple Heading Bar (Large)
            using (var bH = new SolidBrush(Color.FromArgb(139, 92, 246)))
            {
                g.FillRectangle(bH, 8 * s, 8 * s, 16 * s, 4 * s);
            }
            // Sub-lines
            using (var bLine = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                g.FillRectangle(bLine, 8 * s, 15 * s, 16 * s, 2 * s);
                g.FillRectangle(bLine, 8 * s, 20 * s, 11 * s, 2 * s);
            }
        }

        private static void DrawFootnote(Graphics g, float s)
        {
            // Teal Document
            using (var bDoc = new SolidBrush(Color.White))
            using (var pDoc = new Pen(Color.FromArgb(20, 184, 166), 2 * s))
            {
                g.FillPath(bDoc, RoundedRect(5 * s, 4 * s, 22 * s, 24 * s, 3 * s));
                g.DrawPath(pDoc, RoundedRect(5 * s, 4 * s, 22 * s, 24 * s, 3 * s));
            }
            // Asterisk / Note mark (*)
            using (var f = new Font("Segoe UI", 9f * s, FontStyle.Bold))
            using (var bMark = new SolidBrush(Color.FromArgb(20, 184, 166)))
            {
                g.DrawString("*", f, bMark, 8 * s, 6 * s);
            }
            // Footnote line
            using (var bLine = new SolidBrush(Color.FromArgb(20, 184, 166)))
            {
                g.FillRectangle(bLine, 8 * s, 18 * s, 16 * s, 2 * s);
                g.FillRectangle(bLine, 8 * s, 22 * s, 12 * s, 2 * s);
            }
        }

        private static void DrawAnnotationTag(Graphics g, float s)
        {
            // Amber Orange Tag Shape
            using (var bTag = new SolidBrush(Color.FromArgb(245, 158, 11)))
            {
                GraphicsPath tagPath = new GraphicsPath();
                tagPath.AddPolygon(new PointF[]
                {
                    new PointF(4 * s, 14 * s),
                    new PointF(14 * s, 4 * s),
                    new PointF(27 * s, 4 * s),
                    new PointF(27 * s, 27 * s),
                    new PointF(14 * s, 27 * s),
                });
                g.FillPath(bTag, tagPath);
            }
            // Tag hole
            using (var bHole = new SolidBrush(Color.White))
            {
                g.FillEllipse(bHole, 8 * s, 13 * s, 4 * s, 4 * s);
            }
            // '#' or '1' symbol
            using (var f = new Font("Segoe UI", 8f * s, FontStyle.Bold))
            using (var bText = new SolidBrush(Color.White))
            {
                g.DrawString("#", f, bText, 17 * s, 8 * s);
            }
        }

        private static void DrawExportWord(Graphics g, float s)
        {
            // Microsoft Word Blue Badge (#185ABD)
            using (var bWord = new SolidBrush(Color.FromArgb(24, 90, 189)))
            {
                g.FillPath(bWord, RoundedRect(3 * s, 4 * s, 26 * s, 24 * s, 4 * s));
            }
            // Clean Crisp 'W'
            using (var f = new Font("Segoe UI", 12f * s, FontStyle.Bold))
            using (var bText = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("W", f, bText, new RectangleF(3 * s, 4 * s, 26 * s, 24 * s), sf);
            }
        }

        private static void DrawSearch(Graphics g, float s)
        {
            // Lens circle parameters
            float cx = 13 * s;
            float cy = 13 * s;
            float radius = 8.5f * s;
            var lensRect = new RectangleF(cx - radius, cy - radius, radius * 2, radius * 2);

            // Lens Glass Fill (Sky blue gradient)
            using (var bLens = new LinearGradientBrush(
                new PointF(lensRect.Left, lensRect.Top),
                new PointF(lensRect.Right, lensRect.Bottom),
                Color.FromArgb(224, 242, 254),
                Color.FromArgb(186, 230, 253)))
            {
                g.FillEllipse(bLens, lensRect);
            }

            // Inner horizontal text hint lines inside lens
            using (var pHint = new Pen(Color.FromArgb(147, 197, 253), 1.2f * s))
            {
                g.DrawLine(pHint, cx - 4.5f * s, cy - 2.5f * s, cx + 4.5f * s, cy - 2.5f * s);
                g.DrawLine(pHint, cx - 4.5f * s, cy + 0.5f * s, cx + 4.5f * s, cy + 0.5f * s);
                g.DrawLine(pHint, cx - 3.5f * s, cy + 3.5f * s, cx + 2.5f * s, cy + 3.5f * s);
            }

            // Lens Outer Ring (Indigo / Deep Blue)
            using (var pRing = new Pen(Color.FromArgb(37, 99, 235), 2.8f * s))
            {
                g.DrawEllipse(pRing, lensRect);
            }

            // Glass Glare highlight
            using (var pGlare = new Pen(Color.White, 1.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pGlare, lensRect.X + 2 * s, lensRect.Y + 2 * s, (radius - 2 * s) * 2, (radius - 2 * s) * 2, 190, 70);
            }

            // Handle connector (Amber / Gold accent ring)
            float handleStartX = cx + radius * 0.7071f;
            float handleStartY = cy + radius * 0.7071f;
            float handleEndX = 27 * s;
            float handleEndY = 27 * s;

            // Handle (Dark Slate with rounded cap)
            using (var pHandle = new Pen(Color.FromArgb(30, 41, 59), 3.8f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pHandle, handleStartX, handleStartY, handleEndX, handleEndY);
            }

            // Handle inner grip highlight
            using (var pGrip = new Pen(Color.FromArgb(148, 163, 184), 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pGrip, handleStartX + 2.5f * s, handleStartY + 2.5f * s, handleEndX - 1.5f * s, handleEndY - 1.5f * s);
            }
        }

        private static void DrawDefault(Graphics g, float s)
        {
            using var b = new SolidBrush(Color.FromArgb(59, 130, 246));
            g.FillEllipse(b, 4 * s, 4 * s, 24 * s, 24 * s);
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath path = new GraphicsPath();
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
