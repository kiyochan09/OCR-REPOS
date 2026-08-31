using System.Collections.Generic;

namespace OCR_Translator.Models
{
    /// <summary>
    /// OCR対象の矩形領域
    /// </summary>
    public class OcrRegion
    {
        public string Name { get; set; } = "本文";

        /// <summary>
        /// body / heading / header / footer / footnote / table / image / map / ignore
        /// </summary>
        public string Type { get; set; } = "body";

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// テンプレート全体の設定（JSON保存用のルート）
    /// </summary>
    public class PageLayout
    {
        public TemplateSettings Template { get; set; } = new TemplateSettings();

        /// <summary>
        /// キーはページ番号（1始まりの文字列）
        /// </summary>
        public Dictionary<string, PageSettings> Pages { get; set; }
            = new Dictionary<string, PageSettings>();
    }

    /// <summary>
    /// 共通テンプレート
    /// </summary>
    public class TemplateSettings
    {
        public string Name { get; set; } = "縦書き本文";

        public List<OcrRegion> Regions { get; set; }
            = new List<OcrRegion>();
    }

    /// <summary>
    /// ページ単位の領域設定
    /// </summary>
    public class PageSettings
    {
        public bool UseTemplate { get; set; } = true;

        public List<OcrRegion> Regions { get; set; }
            = new List<OcrRegion>();
    }
}