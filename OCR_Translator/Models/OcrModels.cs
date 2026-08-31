using System.Collections.Generic;

namespace OCR_Translator.Models
{
    public class OcrRegion
    {
        public string Name { get; set; } = "本文";
        public string Type { get; set; } = "body";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class PageLayout
    {
        public TemplateSettings Template { get; set; } = new TemplateSettings();
        public Dictionary<string, PageSettings> Pages { get; set; } = new();
    }

    public class TemplateSettings
    {
        public string Name { get; set; } = "縦書き本文";
        public List<OcrRegion> Regions { get; set; } = new();
    }

    public class PageSettings
    {
        public bool UseTemplate { get; set; } = true;
        public List<OcrRegion> Regions { get; set; } = new();
    }

    public sealed class OcrDisplayItem
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsVertical { get; set; }
        public string Text { get; set; } = "";
    }

    public sealed class AutoLayoutRegion
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public List<AutoLayoutCell> Cells { get; set; } = new();
    }

    public sealed class AutoLayoutCell
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Text { get; set; } = "";
        public int OcrCount { get; set; }
    }
}
