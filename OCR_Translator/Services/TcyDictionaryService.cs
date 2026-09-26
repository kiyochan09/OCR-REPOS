using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class TcyDictionaryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public static string GetTcyTemplatesDirectory()
        {
            string engineDir = OcrProcessor.FindOcrEngineDirectory();
            string dir = Path.Combine(engineDir, "config", "tcy_templates");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetTcyRegistryPath()
        {
            return Path.Combine(GetTcyTemplatesDirectory(), "tcy_registry.json");
        }

        public static List<TcyTemplateEntry> LoadEntries()
        {
            var list = new List<TcyTemplateEntry>();
            string dir = GetTcyTemplatesDirectory();
            string registryPath = GetTcyRegistryPath();

            if (File.Exists(registryPath))
            {
                try
                {
                    string json = File.ReadAllText(registryPath);
                    var reg = JsonSerializer.Deserialize<TcyRegistryStructure>(json, JsonOptions);
                    if (reg != null && reg.Templates != null)
                    {
                        foreach (var kv in reg.Templates)
                        {
                            var entry = kv.Value;
                            if (string.IsNullOrEmpty(entry.Value))
                                entry.Value = kv.Key;

                            string imgPath = Path.Combine(dir, entry.PrimaryImage ?? $"{entry.Value}.png");
                            if (File.Exists(imgPath))
                            {
                                try
                                {
                                    using var src = Image.FromFile(imgPath);
                                    entry.Thumbnail = CreateThumbnail(src, 32);
                                }
                                catch { }
                            }

                            list.Add(entry);
                        }
                    }
                }
                catch { }
            }

            // 辞書が空の場合は 0〜9, 10〜99 を自動構築
            if (list.Count == 0)
            {
                list = RebuildAllTemplates();
            }

            // 数値順にソート（0..9, 10..99）
            return list.OrderBy(e => int.TryParse(e.Value, out int n) ? n : 999).ThenBy(e => e.Value).ToList();
        }

        public static void SaveEntries(IEnumerable<TcyTemplateEntry> entries)
        {
            try
            {
                string registryPath = GetTcyRegistryPath();
                var reg = new TcyRegistryStructure();
                foreach (var e in entries)
                {
                    reg.Templates[e.Value] = new TcyTemplateEntry
                    {
                        Value = e.Value,
                        Enabled = e.Enabled,
                        PrimaryImage = e.PrimaryImage,
                        Images = e.Images ?? new List<string> { e.PrimaryImage },
                        Description = e.Description
                    };
                }

                string json = JsonSerializer.Serialize(reg, JsonOptions);
                File.WriteAllText(registryPath, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"縦中横辞書の保存に失敗しました: {ex.Message}", ex);
            }
        }

        public static List<TcyTemplateEntry> RebuildAllTemplates()
        {
            string dir = GetTcyTemplatesDirectory();
            var list = new List<TcyTemplateEntry>();

            // 0〜9, 10〜99
            var numList = new List<string>();
            for (int i = 0; i <= 9; i++) numList.Add(i.ToString());
            for (int i = 10; i <= 99; i++) numList.Add(i.ToString());

            string[] fonts = { "MS Mincho", "MS Gothic", "Arial" };

            foreach (var numStr in numList)
            {
                var entry = new TcyTemplateEntry
                {
                    Value = numStr,
                    Enabled = true,
                    PrimaryImage = $"{numStr}.png",
                    Images = new List<string>(),
                    Description = $"{numStr} (縦中横)"
                };

                // 各フォントで画像を生成
                for (int fIdx = 0; fIdx < fonts.Length; fIdx++)
                {
                    string fontName = fonts[fIdx];
                    string suffix = fIdx == 0 ? "" : (fIdx == 1 ? "_gothic" : "_arial");
                    string fileName = $"{numStr}{suffix}.png";
                    string filePath = Path.Combine(dir, fileName);

                    try
                    {
                        using var bmp = RenderNumberImage(numStr, fontName, 32);
                        bmp.Save(filePath, ImageFormat.Png);
                        entry.Images.Add(fileName);

                        if (fIdx == 0)
                        {
                            entry.PrimaryImage = fileName;
                            entry.Thumbnail = new Bitmap(bmp);
                        }
                    }
                    catch { }
                }

                list.Add(entry);
            }

            SaveEntries(list);
            return list;
        }

        public static void RegisterCustomImage(TcyTemplateEntry entry, string sourceImagePath)
        {
            if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath))
                return;

            string dir = GetTcyTemplatesDirectory();
            string customFileName = $"{entry.Value}_custom.png";
            string targetPath = Path.Combine(dir, customFileName);

            using (var src = Image.FromFile(sourceImagePath))
            {
                using var bmp = new Bitmap(src);
                bmp.Save(targetPath, ImageFormat.Png);
                entry.Thumbnail?.Dispose();
                entry.Thumbnail = CreateThumbnail(src, 32);
            }

            entry.PrimaryImage = customFileName;
            if (!entry.Images.Contains(customFileName))
            {
                entry.Images.Insert(0, customFileName);
            }
        }

        public static Bitmap RenderNumberImage(string text, string fontName, int fontSize)
        {
            using var font = new Font(fontName, fontSize, FontStyle.Regular);
            using var tempBmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(tempBmp))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var size = g.MeasureString(text, font);
                int x = Math.Max(0, (int)((64 - size.Width) / 2));
                int y = Math.Max(0, (int)((64 - size.Height) / 2));
                using var brush = new SolidBrush(Color.Black);
                g.DrawString(text, font, brush, x, y);
            }

            // トリミング（余白除去）
            int minX = tempBmp.Width, maxX = 0, minY = tempBmp.Height, maxY = 0;
            bool hasInk = false;

            for (int y = 0; y < tempBmp.Height; y++)
            {
                for (int x = 0; x < tempBmp.Width; x++)
                {
                    Color c = tempBmp.GetPixel(x, y);
                    if (c.R < 200 || c.G < 200 || c.B < 200)
                    {
                        hasInk = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!hasInk || maxX < minX || maxY < minY)
            {
                return new Bitmap(tempBmp);
            }

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;
            var cropped = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var gCrop = Graphics.FromImage(cropped))
            {
                gCrop.DrawImage(tempBmp, new Rectangle(0, 0, w, h), new Rectangle(minX, minY, w, h), GraphicsUnit.Pixel);
            }
            return cropped;
        }

        public static Bitmap CreateThumbnail(Image src, int size = 32)
        {
            var thumb = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(thumb);
            g.Clear(Color.White);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float scale = Math.Min((float)(size - 4) / Math.Max(1, src.Width), (float)(size - 4) / Math.Max(1, src.Height));
            int tw = Math.Max(1, (int)(src.Width * scale));
            int th = Math.Max(1, (int)(src.Height * scale));
            int x = (size - tw) / 2;
            int y = (size - th) / 2;
            g.DrawImage(src, x, y, tw, th);
            return thumb;
        }
    }
}
