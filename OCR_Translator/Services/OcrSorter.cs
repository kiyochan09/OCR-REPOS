using System;
using System.Collections.Generic;
using System.Linq;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class OcrSorter
    {
        public static List<OcrDisplayItem> SortBodyReadingOrder(
            List<OcrDisplayItem> items,
            string orientationMode = "auto",
            string docType = "japanese")
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            // 重複排除（同じテキスト・重複座標）
            var uniqueItems = items
                .GroupBy(i => new { i.Text, i.X, i.Y, i.Width, i.Height, i.IsVertical })
                .Select(g => g.First())
                .ToList();

            if (uniqueItems.Count <= 1)
                return uniqueItems;

            bool isVertical;
            if (docType == "western" || orientationMode == "horizontal")
            {
                isVertical = false;
            }
            else if (orientationMode == "vertical")
            {
                isVertical = true;
            }
            else
            {
                int verticalCount = uniqueItems.Count(item => item.IsVertical);
                isVertical = verticalCount * 2 >= uniqueItems.Count;
            }

            if (!isVertical)
            {
                // 横書き：行グループ化（上から下、同じ行内は左から右）
                var rows = new List<List<OcrDisplayItem>>();
                foreach (OcrDisplayItem item in uniqueItems.OrderBy(i => i.Y + i.Height / 2.0))
                {
                    double centerY = item.Y + item.Height / 2.0;
                    List<OcrDisplayItem>? targetRow = null;
                    double bestDistance = double.MaxValue;

                    foreach (List<OcrDisplayItem> row in rows)
                    {
                        double rowCenterY = row.Average(x => x.Y + x.Height / 2.0);
                        double distance = Math.Abs(centerY - rowCenterY);
                        double minHeight = Math.Min(item.Height, row.Min(r => r.Height));

                        // 同一行判定：Y中心差が文字高さの35%以内で、X方向に重なりがないこと
                        if (distance < minHeight * 0.35 && distance < bestDistance)
                        {
                            bool xOverlap = row.Any(r => Math.Max(item.X, r.X) < Math.Min(item.X + item.Width, r.X + r.Width));
                            if (!xOverlap)
                            {
                                targetRow = row;
                                bestDistance = distance;
                            }
                        }
                    }

                    if (targetRow == null)
                    {
                        targetRow = new List<OcrDisplayItem>();
                        rows.Add(targetRow);
                    }
                    targetRow.Add(item);
                }

                foreach (List<OcrDisplayItem> row in rows)
                {
                    row.Sort((a, b) => a.X.CompareTo(b.X));
                }

                rows.Sort((a, b) =>
                {
                    double aY = a.Average(x => x.Y + x.Height / 2.0);
                    double bY = b.Average(x => x.Y + x.Height / 2.0);
                    return aY.CompareTo(bY);
                });

                return rows.SelectMany(row => row).ToList();
            }

            // 縦書き：列グループ化（右から左、同じ列内は上から下）
            var columns = new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in uniqueItems.OrderByDescending(item => item.X + item.Width / 2.0))
            {
                double centerX = item.X + item.Width / 2.0;
                List<OcrDisplayItem>? targetColumn = null;
                double bestDistance = double.MaxValue;

                foreach (List<OcrDisplayItem> column in columns)
                {
                    double columnCenterX = column.Average(x => x.X + x.Width / 2.0);
                    double distance = Math.Abs(centerX - columnCenterX);
                    double minWidth = Math.Min(item.Width, column.Min(c => c.Width));

                    // 同一列判定：X中心差が文字幅の35%以内で、Y方向に重なりがないこと
                    if (distance < minWidth * 0.35 && distance < bestDistance)
                    {
                        bool yOverlap = column.Any(c => Math.Max(item.Y, c.Y) < Math.Min(item.Y + item.Height, c.Y + c.Height));
                        if (!yOverlap)
                        {
                            targetColumn = column;
                            bestDistance = distance;
                        }
                    }
                }

                if (targetColumn == null)
                {
                    targetColumn = new List<OcrDisplayItem>();
                    columns.Add(targetColumn);
                }
                targetColumn.Add(item);
            }

            foreach (List<OcrDisplayItem> column in columns)
            {
                column.Sort((a, b) => a.Y.CompareTo(b.Y));
            }

            columns.Sort((a, b) =>
            {
                double aX = a.Average(x => x.X + x.Width / 2.0);
                double bX = b.Average(x => x.X + x.Width / 2.0);
                return bX.CompareTo(aX); // 右列（X大）から左列（X小）
            });

            return columns.SelectMany(column => column).ToList();
        }

        public static string FormatBodyParagraphs(
            List<OcrDisplayItem> items,
            string orientationMode = "auto",
            string docType = "japanese")
        {
            if (items == null || items.Count == 0)
                return "";

            var uniqueItems = items
                .GroupBy(i => new { i.Text, i.X, i.Y, i.Width, i.Height, i.IsVertical })
                .Select(g => g.First())
                .ToList();

            if (uniqueItems.Count == 0)
                return "";

            bool isWestern = (docType == "western");
            bool isVertical;
            if (isWestern || orientationMode == "horizontal")
            {
                isVertical = false;
            }
            else if (orientationMode == "vertical")
            {
                isVertical = true;
            }
            else
            {
                int verticalCount = uniqueItems.Count(item => item.IsVertical);
                isVertical = verticalCount * 2 >= uniqueItems.Count;
            }

            if (isVertical)
            {
                // 縦書き：列ごとにグループ化（右列から左列）
                var columns = new List<List<OcrDisplayItem>>();
                foreach (OcrDisplayItem item in uniqueItems.OrderByDescending(item => item.X + item.Width / 2.0))
                {
                    double centerX = item.X + item.Width / 2.0;
                    List<OcrDisplayItem>? targetColumn = null;
                    double bestDistance = double.MaxValue;

                    foreach (List<OcrDisplayItem> column in columns)
                    {
                        double columnCenterX = column.Average(x => x.X + x.Width / 2.0);
                        double distance = Math.Abs(centerX - columnCenterX);
                        double minWidth = Math.Min(item.Width, column.Min(c => c.Width));

                        if (distance < minWidth * 0.35 && distance < bestDistance)
                        {
                            bool yOverlap = column.Any(c => Math.Max(item.Y, c.Y) < Math.Min(item.Y + item.Height, c.Y + c.Height));
                            if (!yOverlap)
                            {
                                targetColumn = column;
                                bestDistance = distance;
                            }
                        }
                    }

                    if (targetColumn == null)
                    {
                        targetColumn = new List<OcrDisplayItem>();
                        columns.Add(targetColumn);
                    }
                    targetColumn.Add(item);
                }

                foreach (List<OcrDisplayItem> column in columns)
                {
                    column.Sort((a, b) => a.Y.CompareTo(b.Y));
                }

                columns.Sort((a, b) =>
                {
                    double aX = a.Average(x => x.X + x.Width / 2.0);
                    double bX = b.Average(x => x.X + x.Width / 2.0);
                    return bX.CompareTo(aX);
                });

                var lines = columns.Select(col => new
                {
                    Text = string.Join("", col.Select(c => c.Text)),
                    TopY = col.Min(c => c.Y),
                    BotY = col.Max(c => c.Y + c.Height),
                    Width = col.Max(c => c.Width)
                }).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

                if (lines.Count == 0)
                    return "";

                double minTopY = lines.Min(l => l.TopY);
                double maxBotY = lines.Max(l => l.BotY);
                double avgCharSize = lines.Average(l => l.Width);

                var paragraphs = new List<string>();
                var currentPara = new List<string> { lines[0].Text };

                for (int i = 1; i < lines.Count; i++)
                {
                    var prev = lines[i - 1];
                    var curr = lines[i];

                    double prevBotGap = maxBotY - prev.BotY;
                    double currTopGap = curr.TopY - minTopY;

                    bool isPrevShort = prevBotGap > avgCharSize * 2.0;
                    bool isCurrIndented = currTopGap > avgCharSize * 0.6;
                    bool prevEndsSentence = prev.Text.EndsWith("。") || prev.Text.EndsWith("」") ||
                                            prev.Text.EndsWith("』") || prev.Text.EndsWith("）") ||
                                            prev.Text.EndsWith(")") || prev.Text.EndsWith("！") ||
                                            prev.Text.EndsWith("？");

                    if (isCurrIndented || (isPrevShort && prevEndsSentence) || (currTopGap > avgCharSize * 3.0))
                    {
                        paragraphs.Add(string.Join("", currentPara));
                        currentPara = new List<string> { curr.Text };
                    }
                    else
                    {
                        currentPara.Add(curr.Text);
                    }
                }

                if (currentPara.Count > 0)
                {
                    paragraphs.Add(string.Join("", currentPara));
                }

                return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
            }
            else
            {
                // 横書き：行ごとにグループ化（上行から下行）
                var rows = new List<List<OcrDisplayItem>>();
                foreach (OcrDisplayItem item in uniqueItems.OrderBy(i => i.Y + i.Height / 2.0))
                {
                    double centerY = item.Y + item.Height / 2.0;
                    List<OcrDisplayItem>? targetRow = null;
                    double bestDistance = double.MaxValue;

                    foreach (List<OcrDisplayItem> row in rows)
                    {
                        double rowCenterY = row.Average(x => x.Y + x.Height / 2.0);
                        double distance = Math.Abs(centerY - rowCenterY);
                        double minHeight = Math.Min(item.Height, row.Min(r => r.Height));

                        if (distance < minHeight * 0.35 && distance < bestDistance)
                        {
                            bool xOverlap = row.Any(r => Math.Max(item.X, r.X) < Math.Min(item.X + item.Width, r.X + r.Width));
                            if (!xOverlap)
                            {
                                targetRow = row;
                                bestDistance = distance;
                            }
                        }
                    }

                    if (targetRow == null)
                    {
                        targetRow = new List<OcrDisplayItem>();
                        rows.Add(targetRow);
                    }
                    targetRow.Add(item);
                }

                foreach (List<OcrDisplayItem> row in rows)
                {
                    row.Sort((a, b) => a.X.CompareTo(b.X));
                }

                rows.Sort((a, b) =>
                {
                    double aY = a.Average(x => x.Y + x.Height / 2.0);
                    double bY = b.Average(x => x.Y + x.Height / 2.0);
                    return aY.CompareTo(bY);
                });

                string tokenSeparator = isWestern ? " " : "";

                var lines = rows.Select(row => new
                {
                    Text = string.Join(tokenSeparator, row.Select(r => r.Text)),
                    LeftX = row.Min(r => r.X),
                    RightX = row.Max(r => r.X + r.Width),
                    Height = row.Max(r => r.Height)
                }).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

                if (lines.Count == 0)
                    return "";

                double minLeftX = lines.Min(l => l.LeftX);
                double maxRightX = lines.Max(l => l.RightX);
                double avgCharSize = lines.Average(l => l.Height);

                var paragraphs = new List<string>();
                var currentPara = new List<string> { lines[0].Text };

                for (int i = 1; i < lines.Count; i++)
                {
                    var prev = lines[i - 1];
                    var curr = lines[i];

                    double prevRightGap = maxRightX - prev.RightX;
                    double currLeftGap = curr.LeftX - minLeftX;

                    bool isPrevShort = prevRightGap > avgCharSize * 2.0;
                    bool isCurrIndented = currLeftGap > avgCharSize * 0.6;
                    bool prevEndsSentence = prev.Text.EndsWith("。") || prev.Text.EndsWith(".") ||
                                            prev.Text.EndsWith("」") || prev.Text.EndsWith("）") ||
                                            prev.Text.EndsWith(")") || prev.Text.EndsWith("!") ||
                                            prev.Text.EndsWith("?");

                    if (isCurrIndented || (isPrevShort && prevEndsSentence))
                    {
                        paragraphs.Add(string.Join(tokenSeparator, currentPara));
                        currentPara = new List<string> { curr.Text };
                    }
                    else
                    {
                        currentPara.Add(curr.Text);
                    }
                }

                if (currentPara.Count > 0)
                {
                    paragraphs.Add(string.Join(tokenSeparator, currentPara));
                }

                return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
            }
        }

        public static List<OcrDisplayItem> SortTableItemsForDisplay(
            List<OcrDisplayItem> items,
            List<OcrRegion> userRegions,
            bool useUserRegions,
            List<AutoLayoutRegion> autoRegions)
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            string GetTypeForItem(OcrDisplayItem item)
            {
                return useUserRegions
                    ? FindUserRegionType(item, userRegions)
                    : FindAutoLayoutRegionType(item, autoRegions);
            }

            List<OcrDisplayItem> tableItems = items
                .Where(item => GetTypeForItem(item) == "table")
                .ToList();

            if (tableItems.Count <= 1)
                return new List<OcrDisplayItem>(items);

            var columns = new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in tableItems
                .OrderBy(item => item.X + item.Width / 2.0)
                .ThenBy(item => item.Y))
            {
                double itemCenterX = item.X + item.Width / 2.0;
                List<OcrDisplayItem>? targetColumn = null;
                double bestDistance = double.MaxValue;

                foreach (List<OcrDisplayItem> column in columns)
                {
                    double columnCenterX = column.Average(x => x.X + x.Width / 2.0);
                    double averageWidth = column.Count == 0
                        ? Math.Max(1, item.Width)
                        : column.Average(x => Math.Max(1, x.Width));

                    double tolerance = Math.Max(4.0, averageWidth * 0.8);
                    double distance = Math.Abs(itemCenterX - columnCenterX);

                    if (distance <= tolerance && distance < bestDistance)
                    {
                        targetColumn = column;
                        bestDistance = distance;
                    }
                }

                if (targetColumn == null)
                {
                    targetColumn = new List<OcrDisplayItem>();
                    columns.Add(targetColumn);
                }
                targetColumn.Add(item);
            }

            foreach (List<OcrDisplayItem> column in columns)
            {
                column.Sort((a, b) =>
                {
                    int result = a.Y.CompareTo(b.Y);
                    return result != 0 ? result : a.X.CompareTo(b.X);
                });
            }

            columns.Sort((a, b) =>
            {
                double ax = a.Average(x => x.X + x.Width / 2.0);
                double bx = b.Average(x => x.X + x.Width / 2.0);
                return ax.CompareTo(bx);
            });

            List<OcrDisplayItem> sortedTableItems = columns.SelectMany(column => column).ToList();

            List<OcrDisplayItem> result = new List<OcrDisplayItem>(items);
            int tableIndex = 0;

            for (int i = 0; i < result.Count; i++)
            {
                if (GetTypeForItem(result[i]) == "table")
                {
                    result[i] = sortedTableItems[tableIndex];
                    tableIndex++;
                }
            }

            return result;
        }

        private static string FindUserRegionType(OcrDisplayItem item, List<OcrRegion> userRegions)
        {
            int centerX = item.X + item.Width / 2;
            int centerY = item.Y + item.Height / 2;

            foreach (OcrRegion region in userRegions)
            {
                if (centerX >= region.X && centerX <= region.X + region.Width &&
                    centerY >= region.Y && centerY <= region.Y + region.Height)
                {
                    return NormalizeRegionType(region.Type);
                }
            }
            return "";
        }

        private static string FindAutoLayoutRegionType(OcrDisplayItem item, List<AutoLayoutRegion> regions)
        {
            int centerX = item.X + item.Width / 2;
            int centerY = item.Y + item.Height / 2;

            foreach (AutoLayoutRegion region in regions)
            {
                if (centerX >= region.X && centerX <= region.X + region.Width &&
                    centerY >= region.Y && centerY <= region.Y + region.Height)
                    return NormalizeRegionType(region.Type);
            }
            return "";
        }

        private static string NormalizeRegionType(string type)
        {
            return OcrProcessor.NormalizeRegionType(type);
        }
    }
}
