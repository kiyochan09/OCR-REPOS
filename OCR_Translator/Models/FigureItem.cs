using System;
using System.Drawing;

namespace OCR_Translator.Models
{
    /// <summary>
    /// PDFから切り出された図（画像）アイテム
    /// </summary>
    public class FigureItem
    {
        public int PageNumber { get; set; } = 1;
        public string Name { get; set; } = "図";
        public Rectangle Bounds { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = "image/png";
        public double FileSizeKb => ImageBytes.Length / 1024.0;
        public Image? Image { get; set; }
    }
}
