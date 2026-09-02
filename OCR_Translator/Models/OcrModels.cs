using System;
using System.Collections.Generic;
using System.Linq;

namespace OCR_Translator.Models
{
    public class TableRuleLine
    {
        public bool IsVertical { get; set; }
        public int Pos { get; set; }
        public int Start { get; set; }
        public int End { get; set; }

        public TableRuleLine() { }

        public TableRuleLine(bool isVertical, int pos, int start, int end)
        {
            IsVertical = isVertical;
            Pos = pos;
            Start = Math.Min(start, end);
            End = Math.Max(start, end);
        }

        public TableRuleLine Clone() => new TableRuleLine
        {
            IsVertical = IsVertical,
            Pos = Pos,
            Start = Start,
            End = End
        };

        public bool EqualsLine(TableRuleLine other)
        {
            if (other == null) return false;
            return IsVertical == other.IsVertical &&
                   Pos == other.Pos &&
                   Start == other.Start &&
                   End == other.End;
        }
    }

    public class OcrRegion
    {
        public string Name { get; set; } = "本文";
        public string Type { get; set; } = "body";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public List<TableRuleLine> RuleLines { get; set; } = new();

        public List<int> HorizontalLines
        {
            get => RuleLines.Where(l => !l.IsVertical).Select(l => l.Pos).ToList();
            set
            {
                if (value != null)
                {
                    RuleLines.RemoveAll(l => !l.IsVertical);
                    foreach (int y in value)
                        RuleLines.Add(new TableRuleLine(false, y, X, X + Width));
                }
            }
        }

        public List<int> VerticalLines
        {
            get => RuleLines.Where(l => l.IsVertical).Select(l => l.Pos).ToList();
            set
            {
                if (value != null)
                {
                    RuleLines.RemoveAll(l => l.IsVertical);
                    foreach (int x in value)
                        RuleLines.Add(new TableRuleLine(true, x, Y, Y + Height));
                }
            }
        }

        public void EnsureRuleLines()
        {
            if (RuleLines == null)
                RuleLines = new List<TableRuleLine>();

            foreach (var line in RuleLines)
            {
                if (line.Start == 0 && line.End == 0)
                {
                    if (line.IsVertical)
                    {
                        line.Start = Y;
                        line.End = Y + Height;
                    }
                    else
                    {
                        line.Start = X;
                        line.End = X + Width;
                    }
                }
                else
                {
                    int min = Math.Min(line.Start, line.End);
                    int max = Math.Max(line.Start, line.End);
                    line.Start = min;
                    line.End = max;
                }
            }
        }
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
        public List<TableRuleLine> RuleLines { get; set; } = new();
        public List<int> HorizontalLines { get; set; } = new();
        public List<int> VerticalLines { get; set; } = new();
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
