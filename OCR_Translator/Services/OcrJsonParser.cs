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
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var result = new List<OcrDisplayItem>();
            CollectNdlocrItems(doc.RootElement, result);
            return result;
        }

        private static void CollectNdlocrItems(JsonElement element, List<OcrDisplayItem> result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                bool isTextline = false;
                if (element.TryGetProperty("isTextline", out JsonElement tl))
                {
                    isTextline = tl.ValueKind == JsonValueKind.True ||
                                 (tl.ValueKind == JsonValueKind.String &&
                                  string.Equals(tl.GetString(), "true", StringComparison.OrdinalIgnoreCase));
                }

                if (isTextline && TryParseNdlocrItem(element, out OcrDisplayItem? item))
                {
                    result.Add(item!);
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
            if (!obj.TryGetProperty("text", out JsonElement textElement) || textElement.ValueKind != JsonValueKind.String)
                return false;

            string text = textElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(text)) return false;

            int x = 0, y = 0, width = 0, height = 0;
            if (obj.TryGetProperty("boundingBox", out JsonElement box) && box.ValueKind == JsonValueKind.Array)
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

            result.Add(region);
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
