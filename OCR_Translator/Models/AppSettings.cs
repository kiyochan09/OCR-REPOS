using System;
using System.Drawing;

namespace OCR_Translator.Models
{
    public class AppSettings
    {
        // 1. フォント設定
        public string FontFamilyName { get; set; } = "Yu Gothic UI";
        public float FontSize { get; set; } = 11.0f;
        public bool FontBold { get; set; } = false;

        // 2. 組方向設定 ("auto": 自動, "vertical": 縦書き, "horizontal": 横書き)
        public string TextOrientation { get; set; } = "auto";

        // 3. 書籍種別 ("japanese": 和書, "western": 洋書・英欧文)
        public string DocumentType { get; set; } = "japanese";

        // フォントオブジェクト取得ヘルパー
        public Font CreateFont()
        {
            try
            {
                FontStyle style = FontBold ? FontStyle.Bold : FontStyle.Regular;
                return new Font(FontFamilyName, FontSize, style);
            }
            catch
            {
                return new Font("Yu Gothic UI", 11.0f, FontStyle.Regular);
            }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                FontFamilyName = this.FontFamilyName,
                FontSize = this.FontSize,
                FontBold = this.FontBold,
                TextOrientation = this.TextOrientation,
                DocumentType = this.DocumentType
            };
        }
    }
}
