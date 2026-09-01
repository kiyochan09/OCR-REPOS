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

            int verticalCount = items.Count(item => item.IsVertical);
            bool isVertical = verticalCount * 2 >= items.Count;

            if (!isVertical)
                return items.OrderBy(item => item.Y).ThenBy(item => item.X).ToList();

            var columns = new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in items.OrderByDescending(item => item.X + item.Width / 2))
            {
                double centerX = item.X + item.Width / 2.0;
                List<OcrDisplayItem>? targetColumn = null;
                double bestDistance = double.MaxValue;

                foreach (List<OcrDisplayItem> column in columns)
                {
                    double columnCenterX = column.Average(x => x.X + x.Width / 2.0);
                    double distance = Math.Abs(centerX - columnCenterX);
                    double averageWidth = column.Average(x => x.Width);
                    double tolerance = Math.Max(8.0, Math.Max(item.Width, averageWidth) * 1.5);

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
                double aX = a.Average(x => x.X + x.Width / 2.0);
                double bX = b.Average(x => x.X + x.Width / 2.0);
                return bX.CompareTo(aX);
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
            if (string.IsNullOrWhiteSpace(type))
                return "";
            string lower = type.ToLowerInvariant().Trim();
            return lower switch
            {
                "header" or "footer" or "map" or "ignore" => "body",
                _ => lower
            };
        }
    }
}
