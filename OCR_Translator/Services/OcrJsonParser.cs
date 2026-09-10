using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class OcrJsonParser
    {
        public static List<OcrDisplayItem> LoadNdlocrPageJson(string path)
        {
            if (!File.Exists(path)) return new List<OcrDisplayItem>();

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<OcrDisplayItem>();

            JsonElement root = doc.RootElement;

            // 1. auto_regions.json 形式 (root has "results": [ { x, y, width, height, text, isVertical } ])
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out JsonElement resultsElem) && resultsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in resultsElem.EnumerateArray())
                {
                    if (TryParseNdlocrItem(item, out OcrDisplayItem? displayItem) && displayItem != null)
                    {
                        result.Add(displayItem);
                    }
                }
                if (result.Count > 0)
                {
                    return result
                        .GroupBy(item => new { item.Text, item.X, item.Y, item.Width, item.Height, item.IsVertical })
                        .Select(g => g.First())
                        .ToList();
                }
            }

            // 2. NDLOCR-Lite raw / page.json 形式
            CollectNdlocrItems(root, result);

            // 重複排除：同じテキスト＋同じ座標のものを1つにまとめる
            return result
                .GroupBy(item => new { item.Text, item.X, item.Y, item.Width, item.Height, item.IsVertical })
                .Select(g => g.First())
                .ToList();
        }

        private static void CollectNdlocrItems(JsonElement element, List<OcrDisplayItem> result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("results", out JsonElement resArr) && resArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement elem in resArr.EnumerateArray())
                    {
                        if (TryParseNdlocrItem(elem, out OcrDisplayItem? parsed) && parsed != null)
                            result.Add(parsed);
                    }
                    if (result.Count > 0) return;
                }

                bool isTextline = false;
                if (element.TryGetProperty("isTextline", out JsonElement tl))
                {
                    isTextline = tl.ValueKind == JsonValueKind.True ||
                                 (tl.ValueKind == JsonValueKind.String &&
                                  string.Equals(tl.GetString(), "true", StringComparison.OrdinalIgnoreCase));
                }

                if ((isTextline || element.TryGetProperty("text", out _)) && TryParseNdlocrItem(element, out OcrDisplayItem? parsedItem))
                {
                    result.Add(parsedItem!);
                    return;
                }

                foreach (JsonProperty property in element.EnumerateObject())
                    CollectNdlocrItems(property.Value, result);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                    CollectNdlocrItems(child, result);
            }
        }

        private static bool TryParseNdlocrItem(JsonElement obj, out OcrDisplayItem? result)
        {
            result = null;
            if (obj.ValueKind != JsonValueKind.Object) return false;

            string text = ReadJsonString(obj, "text", "Text");
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 信頼度0かつ極小ノイズ（記号1〜3文字のみ等の検出ゴミ）を除外
            // ※有意な文字列が含まれる行（ブロック補完行やConfidence未設定エンジン）は正当な行として保護
            if (obj.TryGetProperty("confidence", out JsonElement confProp) || obj.TryGetProperty("Confidence", out confProp))
            {
                if (confProp.ValueKind == JsonValueKind.Number && confProp.TryGetDouble(out double confVal))
                {
                    string trimmed = text.Trim();
                    if (confVal <= 0.01 && (trimmed.Length <= 1 || (trimmed.Length <= 3 && trimmed.All(c => char.IsPunctuation(c) || char.IsWhiteSpace(c) || "._,-~|'`\"^*:;・+=/\\()[]{}<>ー".Contains(c)))))
                    {
                        return false;
                    }
                }
            }

            int x = 0, y = 0, width = 0, height = 0;

            // 1. Direct x, y, width, height (from auto_regions.json)
            if (obj.TryGetProperty("x", out _) || obj.TryGetProperty("X", out _))
            {
                x = ReadJsonInt(obj, "x", "X");
                y = ReadJsonInt(obj, "y", "Y");
                width = ReadJsonInt(obj, "width", "Width");
                height = ReadJsonInt(obj, "height", "Height");
            }
            // 2. BoundingBox array [[x1,y1],[x2,y2],...]
            else if (obj.TryGetProperty("boundingBox", out JsonElement box) && box.ValueKind == JsonValueKind.Array)
            {
                var points = new List<(int X, int Y)>();
                foreach (JsonElement point in box.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Array) continue;
                    var values = new List<int>();
                    foreach (JsonElement value in point.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                            values.Add(n);
                    }
                    if (values.Count >= 2) points.Add((values[0], values[1]));
                }

                if (points.Count > 0)
                {
                    x = points.Min(p => p.X);
                    y = points.Min(p => p.Y);
                    width = Math.Max(0, points.Max(p => p.X) - x);
                    height = Math.Max(0, points.Max(p => p.Y) - y);
                }
            }
            // 3. Flat bbox array [x, y, w, h] or [x1, y1, x2, y2]
            else if (obj.TryGetProperty("bbox", out JsonElement bbox) && bbox.ValueKind == JsonValueKind.Array)
            {
                var values = new List<int>();
                foreach (JsonElement value in bbox.EnumerateArray())
                {
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int n))
                        values.Add(n);
                }
                if (values.Count >= 4)
                {
                    x = values[0];
                    y = values[1];
                    width = values[2] >= values[0] ? values[2] - values[0] : values[2];
                    height = values[3] >= values[1] ? values[3] - values[1] : values[3];
                }
            }

            bool isVertical = false;
            if (obj.TryGetProperty("isVertical", out JsonElement vertical))
            {
                isVertical = vertical.ValueKind == JsonValueKind.True ||
                             (vertical.ValueKind == JsonValueKind.String &&
                              string.Equals(vertical.GetString(), "true", StringComparison.OrdinalIgnoreCase));
            }

            result = new OcrDisplayItem
            {
                X = x, Y = y, Width = width, Height = height,
                IsVertical = isVertical, Text = text
            };
            return true;
        }

        public static List<AutoLayoutRegion> LoadAutoLayoutJson(string path)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<AutoLayoutRegion>();
            JsonElement root = doc.RootElement;
            JsonElement regionsElement;

            if (root.TryGetProperty("regions", out regionsElement) && regionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in regionsElement.EnumerateArray()) AddAutoLayoutRegion(item, result);
                return result;
            }

            if (root.TryGetProperty("Regions", out regionsElement) && regionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in regionsElement.EnumerateArray()) AddAutoLayoutRegion(item, result);
            }
            return result;
        }

        private static void AddAutoLayoutRegion(JsonElement item, List<AutoLayoutRegion> result)
        {
            if (item.ValueKind != JsonValueKind.Object) return;

            AutoLayoutRegion region = new AutoLayoutRegion
            {
                Name = ReadJsonString(item, "name", "Name"),
                Type = ReadJsonString(item, "type", "Type"),
                X = ReadJsonInt(item, "x", "X"),
                Y = ReadJsonInt(item, "y", "Y"),
                Width = ReadJsonInt(item, "width", "Width"),
                Height = ReadJsonInt(item, "height", "Height"),
                Rows = ReadJsonInt(item, "rows", "Rows"),
                Columns = ReadJsonInt(item, "columns", "Columns")
            };

            if (item.TryGetProperty("cells", out JsonElement cellsElement) &&
                cellsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement cellElement in cellsElement.EnumerateArray())
                {
                    if (cellElement.ValueKind != JsonValueKind.Object) continue;

                    region.Cells.Add(new AutoLayoutCell
                    {
                        Row = ReadJsonInt(cellElement, "row", "Row"),
                        Column = ReadJsonInt(cellElement, "column", "Column"),
                        X = ReadJsonInt(cellElement, "x", "X"),
                        Y = ReadJsonInt(cellElement, "y", "Y"),
                        Width = ReadJsonInt(cellElement, "width", "Width"),
                        Height = ReadJsonInt(cellElement, "height", "Height"),
                        Text = ReadJsonString(cellElement, "text", "Text"),
                        OcrCount = ReadJsonInt(cellElement, "ocr_count", "OcrCount")
                    });
                }
            }

            if (item.TryGetProperty("horizontal_lines", out JsonElement hLinesElement) && hLinesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement h in hLinesElement.EnumerateArray())
                {
                    if (h.ValueKind == JsonValueKind.Number && h.TryGetInt32(out int y))
                        region.HorizontalLines.Add(y);
                }
            }

            if (item.TryGetProperty("vertical_lines", out JsonElement vLinesElement) && vLinesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement v in vLinesElement.EnumerateArray())
                {
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int x))
                        region.VerticalLines.Add(x);
                }
            }

            if (region.Cells.Count > 0)
            {
                if (region.HorizontalLines.Count == 0)
                {
                    var hSet = new HashSet<int>();
                    foreach (var c in region.Cells)
                    {
                        if (c.Y > region.Y + 2 && c.Y < region.Y + region.Height - 2)
                            hSet.Add(c.Y);
                        if (c.Y + c.Height > region.Y + 2 && c.Y + c.Height < region.Y + region.Height - 2)
                            hSet.Add(c.Y + c.Height);
                    }
                    region.HorizontalLines = ClusterLines(hSet);
                }

                if (region.VerticalLines.Count == 0)
                {
                    var vSet = new HashSet<int>();
                    foreach (var c in region.Cells)
                    {
                        if (c.X > region.X + 2 && c.X < region.X + region.Width - 2)
                            vSet.Add(c.X);
                        if (c.X + c.Width > region.X + 2 && c.X + c.Width < region.X + region.Width - 2)
                            vSet.Add(c.X + c.Width);
                    }
                    region.VerticalLines = ClusterLines(vSet);
                }
            }

            foreach (int y in region.HorizontalLines)
            {
                if (region.RuleLines.All(l => l.IsVertical || l.Pos != y))
                    region.RuleLines.Add(new TableRuleLine(false, y, region.X, region.X + region.Width));
            }

            foreach (int x in region.VerticalLines)
            {
                if (region.RuleLines.All(l => !l.IsVertical || l.Pos != x))
                    region.RuleLines.Add(new TableRuleLine(true, x, region.Y, region.Y + region.Height));
            }

            result.Add(region);
        }

        private static List<int> ClusterLines(IEnumerable<int> lines, int tolerance = 4)
        {
            var sorted = lines.OrderBy(x => x).ToList();
            var clusters = new List<List<int>>();

            foreach (int val in sorted)
            {
                if (clusters.Count == 0 || Math.Abs(val - clusters.Last().Average()) > tolerance)
                {
                    clusters.Add(new List<int> { val });
                }
                else
                {
                    clusters.Last().Add(val);
                }
            }

            return clusters.Select(c => (int)Math.Round(c.Average())).ToList();
        }

        private static string ReadJsonString(JsonElement obj, string lower, string upper)
        {
            if (obj.TryGetProperty(lower, out JsonElement a) && a.ValueKind == JsonValueKind.String)
                return a.GetString() ?? "";
            if (obj.TryGetProperty(upper, out JsonElement b) && b.ValueKind == JsonValueKind.String)
                return b.GetString() ?? "";
            return "";
        }

        private static int ReadJsonInt(JsonElement obj, string lower, string upper)
        {
            JsonElement element;
            if (!obj.TryGetProperty(lower, out element) && !obj.TryGetProperty(upper, out element)) return 0;
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number)) return number;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out int parsed)) return parsed;
            return 0;
        }
    }
}
