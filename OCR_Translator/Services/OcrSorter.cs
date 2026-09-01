using System;
using System.Collections.Generic;
using System.Linq;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class OcrSorter
    {
        public static List<OcrDisplayItem> SortBodyReadingOrder(List<OcrDisplayItem> items)
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

            int verticalCount = uniqueItems.Count(item => item.IsVertical);
            bool isVertical = verticalCount * 2 >= uniqueItems.Count;

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
