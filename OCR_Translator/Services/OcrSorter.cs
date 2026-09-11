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
            string docType = "japanese",
            int deckCount = 0,
            List<OcrRegion>? userRegions = null)
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            // 重複排除（同じテキスト・重複座標）および暴走繰り返し文字列のクリーンアップ
            var uniqueItems = items
                .Select(i => new OcrDisplayItem
                {
                    Text = CleanRunawayRepetition(i.Text),
                    X = i.X,
                    Y = i.Y,
                    Width = i.Width,
                    Height = i.Height,
                    IsVertical = i.IsVertical
                })
                .Where(i => !string.IsNullOrWhiteSpace(i.Text))
                .GroupBy(i => new { i.Text, i.X, i.Y, i.Width, i.Height, i.IsVertical })
                .Select(g => g.First())
                .ToList();

            if (uniqueItems.Count <= 1)
                return uniqueItems;

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

            // 1. ユーザー指定の領域（読み順 ①, ②...）がある場合（手動設定領域のみを対象とする）
            if (userRegions != null && userRegions.Count > 0)
            {
                var result = new List<OcrDisplayItem>();
                var remaining = new List<OcrDisplayItem>(uniqueItems);

                foreach (var region in userRegions)
                {
                    var regionRect = new System.Drawing.Rectangle(region.X, region.Y, region.Width, region.Height);
                    var inRegion = remaining.Where(item =>
                    {
                        double cx = item.X + item.Width / 2.0;
                        double cy = item.Y + item.Height / 2.0;
                        return regionRect.Contains((int)cx, (int)cy);
                    }).ToList();

                    if (inRegion.Count > 0)
                    {
                        result.AddRange(SortSingleTier(inRegion, isVertical, isWestern));
                        foreach (var item in inRegion)
                            remaining.Remove(item);
                    }
                }

                return result;
            }

            // 2. 段組指定（2段組・3段組）がある場合
            if (deckCount == 2 || deckCount == 3)
            {
                return SortMultiDeckItems(uniqueItems, deckCount, isVertical, isWestern);
            }

            // 3. 自動段組判定（縦書きで上下に明確なギャップがある場合）
            if (deckCount == 0 && isVertical && uniqueItems.Count >= 6)
            {
                int detectedDecks = DetectDeckCount(uniqueItems);
                if (detectedDecks >= 2)
                {
                    return SortMultiDeckItems(uniqueItems, detectedDecks, isVertical, isWestern);
                }
            }

            // 4. 単段（1段組）ソート
            return SortSingleTier(uniqueItems, isVertical, isWestern);
        }

        private static List<OcrDisplayItem> SortMultiDeckItems(
            List<OcrDisplayItem> items,
            int deckCount,
            bool isVertical,
            bool isWestern)
        {
            if (items.Count <= 1 || deckCount <= 1)
                return SortSingleTier(items, isVertical, isWestern);

            double minX = items.Min(i => i.X);
            double maxX = items.Max(i => i.X + i.Width);
            double minY = items.Min(i => i.Y);
            double maxY = items.Max(i => i.Y + i.Height);
            double totalW = maxX - minX;
            double totalH = maxY - minY;

            if (totalH <= 10)
                return SortSingleTier(items, isVertical, isWestern);

            // 見開き判定（横幅が広く左右に分かれている場合）
            if (totalW > 250 && totalW > totalH * 0.85)
            {
                double midX = minX + totalW / 2.0;
                var rightItems = items.Where(i => i.X + i.Width / 2.0 >= midX).ToList();
                var leftItems = items.Where(i => i.X + i.Width / 2.0 < midX).ToList();

                if (rightItems.Count > 0 && leftItems.Count > 0)
                {
                    var res = new List<OcrDisplayItem>();
                    if (isWestern || !isVertical)
                    {
                        // 洋書・横書き見開き: 左ページ (上段->下段) -> 右ページ (上段->下段)
                        res.AddRange(SortMultiDeckSinglePage(leftItems, deckCount, isVertical, isWestern));
                        res.AddRange(SortMultiDeckSinglePage(rightItems, deckCount, isVertical, isWestern));
                    }
                    else
                    {
                        // 日本語縦書き見開き: 右ページ (上段->下段) -> 左ページ (上段->下段)
                        res.AddRange(SortMultiDeckSinglePage(rightItems, deckCount, isVertical, isWestern));
                        res.AddRange(SortMultiDeckSinglePage(leftItems, deckCount, isVertical, isWestern));
                    }
                    return res;
                }
            }

            return SortMultiDeckSinglePage(items, deckCount, isVertical, isWestern);
        }

        private static List<OcrDisplayItem> SortMultiDeckSinglePage(
            List<OcrDisplayItem> items,
            int deckCount,
            bool isVertical,
            bool isWestern)
        {
            if (items.Count <= 1 || deckCount <= 1)
                return SortSingleTier(items, isVertical, isWestern);

            if (!isVertical)
            {
                // 横書き段組（左右分割：左段 -> 右段）
                double minX = items.Min(i => i.X);
                double maxX = items.Max(i => i.X + i.Width);
                double totalW = maxX - minX;
                if (totalW <= 10) return SortSingleTier(items, isVertical, isWestern);

                var cols = new List<List<OcrDisplayItem>>();
                for (int d = 0; d < deckCount; d++)
                    cols.Add(new List<OcrDisplayItem>());

                double colW = totalW / deckCount;
                foreach (var item in items)
                {
                    double cx = item.X + item.Width / 2.0;
                    int colIdx = (int)((cx - minX) / colW);
                    if (colIdx < 0) colIdx = 0;
                    if (colIdx >= deckCount) colIdx = deckCount - 1;
                    cols[colIdx].Add(item);
                }

                var result = new List<OcrDisplayItem>();
                foreach (var col in cols)
                {
                    if (col.Count > 0)
                    {
                        result.AddRange(SortSingleTier(col, isVertical, isWestern));
                    }
                }
                return result;
            }
            else
            {
                // 縦書き段組（上下分割：上段 -> 下段）
                double minY = items.Min(i => i.Y);
                double maxY = items.Max(i => i.Y + i.Height);
                double totalH = maxY - minY;
                if (totalH <= 10)
                    return SortSingleTier(items, isVertical, isWestern);

                var tiers = new List<List<OcrDisplayItem>>();
                for (int d = 0; d < deckCount; d++)
                    tiers.Add(new List<OcrDisplayItem>());

                double deckH = totalH / deckCount;

                foreach (var item in items)
                {
                    double cy = item.Y + item.Height / 2.0;
                    int tierIdx = (int)((cy - minY) / deckH);
                    if (tierIdx < 0) tierIdx = 0;
                    if (tierIdx >= deckCount) tierIdx = deckCount - 1;
                    tiers[tierIdx].Add(item);
                }

                var result = new List<OcrDisplayItem>();
                foreach (var tier in tiers)
                {
                    if (tier.Count > 0)
                    {
                        result.AddRange(SortSingleTier(tier, isVertical, isWestern));
                    }
                }

                return result;
            }
        }

        private static int DetectDeckCount(List<OcrDisplayItem> items)
        {
            if (items.Count < 6) return 1;

            double minY = items.Min(i => i.Y);
            double maxY = items.Max(i => i.Y + i.Height);
            double totalH = maxY - minY;
            if (totalH < 100) return 1;

            double midY = minY + totalH / 2.0;
            double quarterH = totalH / 8.0;

            // 中央付近に文字の中心が存在しないギャップ（段間）があるか検証
            bool hasMiddleGap = !items.Any(i =>
            {
                double cy = i.Y + i.Height / 2.0;
                return Math.Abs(cy - midY) < quarterH;
            });

            int topCount = items.Count(i => i.Y + i.Height / 2.0 < midY);
            int botCount = items.Count(i => i.Y + i.Height / 2.0 >= midY);

            if (hasMiddleGap && topCount >= 2 && botCount >= 2)
            {
                return 2;
            }

            return 1;
        }

        private static List<OcrDisplayItem> SortSingleTier(
            List<OcrDisplayItem> items,
            bool isVertical,
            bool isWestern)
        {
            if (items.Count <= 1)
                return new List<OcrDisplayItem>(items);

            if (!isVertical)
            {
                // 横書き見開き・2段組の左右分離判定（横幅が広く左右に分かれている場合）
                double minX = items.Min(i => i.X);
                double maxX = items.Max(i => i.X + i.Width);
                double minY = items.Min(i => i.Y);
                double maxY = items.Max(i => i.Y + i.Height);
                double totalW = maxX - minX;
                double totalH = maxY - minY;

                if (totalW > 250 && totalW > totalH * 0.85)
                {
                    double midX = minX + totalW / 2.0;
                    var leftItems = items.Where(i => i.X + i.Width / 2.0 < midX).ToList();
                    var rightItems = items.Where(i => i.X + i.Width / 2.0 >= midX).ToList();

                    if (leftItems.Count > 0 && rightItems.Count > 0)
                    {
                        var res = new List<OcrDisplayItem>();
                        res.AddRange(SortSingleTierPageHorizontal(leftItems));
                        res.AddRange(SortSingleTierPageHorizontal(rightItems));
                        return res;
                    }
                }

                return SortSingleTierPageHorizontal(items);
            }

            // 縦書き：列グループ化（右から左、同じ列内は上から下）
            var columns = new List<List<OcrDisplayItem>>();

            foreach (OcrDisplayItem item in items.OrderByDescending(item => item.X + item.Width / 2.0))
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
                return bX.CompareTo(aX); // 右列（X大）から左列（X小）
            });

            return columns.SelectMany(column => column).ToList();
        }

        private static List<OcrDisplayItem> SortSingleTierPageHorizontal(List<OcrDisplayItem> pageItems)
        {
            if (pageItems.Count <= 1)
                return new List<OcrDisplayItem>(pageItems);

            var rows = new List<List<OcrDisplayItem>>();
            foreach (OcrDisplayItem item in pageItems.OrderBy(i => i.Y + i.Height / 2.0))
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

            return rows.SelectMany(row => row).ToList();
        }

        public static string FormatBodyParagraphs(
            List<OcrDisplayItem> items,
            string orientationMode = "auto",
            string docType = "japanese",
            int deckCount = 0,
            List<OcrRegion>? userRegions = null,
            int lineCharCount = 0,
            bool autoDetectSubheadings = false)
        {
            if (items == null || items.Count == 0)
                return "";

            var uniqueItems = items
                .Select(i => new OcrDisplayItem
                {
                    Text = CleanRunawayRepetition(i.Text),
                    X = i.X,
                    Y = i.Y,
                    Width = i.Width,
                    Height = i.Height,
                    IsVertical = i.IsVertical
                })
                .Where(i => !string.IsNullOrWhiteSpace(i.Text))
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

            string rawResult;

            // 1. ユーザー指定の領域（読み順 ①, ②...）がある場合（手動設定領域のみを対象とする）
            if (userRegions != null && userRegions.Count > 0)
            {
                var paraList = new List<string>();
                var remaining = new List<OcrDisplayItem>(uniqueItems);

                foreach (var region in userRegions)
                {
                    var regionRect = new System.Drawing.Rectangle(region.X, region.Y, region.Width, region.Height);
                    var inRegion = remaining.Where(item =>
                    {
                        double cx = item.X + item.Width / 2.0;
                        double cy = item.Y + item.Height / 2.0;
                        return regionRect.Contains((int)cx, (int)cy);
                    }).ToList();

                    if (inRegion.Count > 0)
                    {
                        string p = FormatSingleTierParagraphs(inRegion, isVertical, isWestern, autoDetectSubheadings);
                        if (!string.IsNullOrWhiteSpace(p))
                            paraList.Add(p);

                        foreach (var item in inRegion)
                            remaining.Remove(item);
                    }
                }

                var mergedParas = MergeFlowingParagraphs(paraList, isWestern);
                rawResult = string.Join(Environment.NewLine + Environment.NewLine, mergedParas);
            }
            // 2. 段組指定（2段組・3段組）がある場合
            else if (deckCount == 2 || deckCount == 3)
            {
                rawResult = FormatMultiDeckParagraphs(uniqueItems, deckCount, isVertical, isWestern, autoDetectSubheadings);
            }
            // 3. 自動段組判定
            else if (deckCount == 0 && isVertical && uniqueItems.Count >= 6)
            {
                int detectedDecks = DetectDeckCount(uniqueItems);
                if (detectedDecks >= 2)
                {
                    rawResult = FormatMultiDeckParagraphs(uniqueItems, detectedDecks, isVertical, isWestern, autoDetectSubheadings);
                }
                else
                {
                    rawResult = FormatSingleTierParagraphs(uniqueItems, isVertical, isWestern, autoDetectSubheadings);
                }
            }
            // 4. 単段（1段組）
            else
            {
                rawResult = FormatSingleTierParagraphs(uniqueItems, isVertical, isWestern, autoDetectSubheadings);
            }

            if (isWestern || lineCharCount <= 0)
            {
                return rawResult; // 段落単位で改行（Wordやエディタの自動折り返しに任せ、段落内での強制改行を行わない）
            }

            return WrapParagraphsByCharCount(rawResult, lineCharCount);
        }

        public static string WrapParagraphsByCharCount(string text, int lineCharCount)
        {
            if (lineCharCount <= 0 || string.IsNullOrEmpty(text))
                return text;

            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
            var wrappedParagraphs = new List<string>();

            foreach (var para in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(para))
                {
                    wrappedParagraphs.Add(para);
                    continue;
                }

                string normalizedPara = para.Replace("\r\n", "").Replace("\n", "").Trim();

                // 小見出しは強制文字数折り返しの対象外としてそのまま1行で保持
                if (IsSubheadingText(normalizedPara) || normalizedPara.Length <= lineCharCount)
                {
                    wrappedParagraphs.Add(normalizedPara);
                    continue;
                }

                var lines = new List<string>();
                int idx = 0;
                while (idx < normalizedPara.Length)
                {
                    int remaining = normalizedPara.Length - idx;
                    int take = Math.Min(lineCharCount, remaining);

                    // 禁則処理: 次の文字が行頭禁則文字の場合、1文字巻き込む
                    if (idx + take < normalizedPara.Length)
                    {
                        char nextChar = normalizedPara[idx + take];
                        if ("、。，．）」』】！？!?：；ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮ".IndexOf(nextChar) >= 0)
                        {
                            take++;
                        }
                    }

                    lines.Add(normalizedPara.Substring(idx, Math.Min(take, normalizedPara.Length - idx)));
                    idx += take;
                }

                wrappedParagraphs.Add(string.Join(Environment.NewLine, lines));
            }

            return string.Join(Environment.NewLine + Environment.NewLine, wrappedParagraphs);
        }

        /// <summary>
        /// OCRデコーダーの暴走による同一文字・単語の無限ループや末尾の誤認識を除去します。
        /// </summary>
        public static string CleanRunawayRepetition(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // 5回以上連続する同一文字（00000, 11111, ......, ------等）以降の暴走出力を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"([^\s])\1{4,}.*$", "").Trim();
            // 繰り返し単語・パターンの暴走（the the the the や ( ) ( ) 等）を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\b\w+\b\s+)\1{3,}.*$", "").Trim();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"((?:\([^\)]*\)|\S+)\s*)\1{3,}.*$", "").Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[\s\(\)\[\]\{\}\.,・:：1l|]+$")) return "";
            // 年号直後の括弧誤認識 (例: 2005al. -> 2005a]., 1997l. -> 1997].)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\b\d{4}[a-z]?)l\.", "$1].");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(\b\d{4}[a-z]?)1\.", "$1].");
            return text;
        }

        /// <summary>
        /// テキストが主に欧文（ラテン文字）であるかを判定します。
        /// </summary>
        public static bool IsMainlyWesternText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            int latinCount = text.Count(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            int cjkCount = text.Count(c => (c >= 0x3000 && c <= 0x9FFF) || (c >= 0x3040 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF));
            return latinCount > cjkCount;
        }

        /// <summary>
        /// 直前のテキストの末尾が文末約物（「。」または「.」、感嘆符・疑問符、それらに続く閉じ引用符・括弧・注釈引用）で終わっているかを判定します。
        /// 句点またはピリオドがなければ文が途中で継続しているとみなし、段落を分割せず同一段落内に組み込みます。
        /// </summary>
        public static bool EndsWithSentencePunctuation(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[。\.！？!?](?:\[[^\]]*\]|\([^\)]*\)|[」』）\)\""\'\]])*$");
        }

        /// <summary>
        /// 直前の段落の末尾に「。」あるいは「.」がない場合、段落を分割せず前の段落の中に組み込みます。
        /// </summary>
        private static List<string> MergeFlowingParagraphs(List<string> rawParagraphs, bool isWestern)
        {
            if (rawParagraphs.Count <= 1) return rawParagraphs;
            var merged = new List<string>();
            foreach (var para in rawParagraphs)
            {
                if (string.IsNullOrWhiteSpace(para)) continue;
                if (merged.Count == 0)
                {
                    merged.Add(para.Trim());
                    continue;
                }

                string last = merged[^1];
                bool lastEndsSentence = EndsWithSentencePunctuation(last);
                string firstLine = para.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
                bool isNextSubheading = IsSubheadingText(firstLine);

                if (!lastEndsSentence && !isNextSubheading)
                {
                    if (isWestern)
                    {
                        merged[^1] = last.EndsWith("-") ? last[..^1] + para.Trim() : last + " " + para.Trim();
                    }
                    else
                    {
                        merged[^1] = last + para.Trim();
                    }
                }
                else
                {
                    merged.Add(para.Trim());
                }
            }
            return merged;
        }

        /// <summary>
        /// 指定されたテキストが大見出し（章・編・部・序章・終章・Chapter・Part等）であるかを判定します。
        /// </summary>
        public static bool IsMajorHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            trimmed = DocxExporter.RemovePagePrefix(trimmed);

            // 句点・読点・感嘆符・疑問符等で終わる通常の文章は除外
            if (trimmed.EndsWith("。") || trimmed.EndsWith("、") || trimmed.EndsWith(",") ||
                trimmed.EndsWith(";") || trimmed.EndsWith("；") ||
                trimmed.EndsWith("！") || trimmed.EndsWith("!") || trimmed.EndsWith("？") || trimmed.EndsWith("?"))
            {
                return false;
            }

            // 1. 和文: 章・編・部（例: 「第1章」「第2編」「第３部」「第一章」）
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^第[0-9０-９一二三四五六七八九十百千万]+[章編部](?:[\s　・:：].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            // 2. 和文: 序章・終章・補章・間章・プロローグ・エピローグ（例: 「序章 民族自決運動の比較政治史 3」）
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:序章|終章|補章|間章|プロローグ|エピローグ)(?:[\s　・:：].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            // 3. 欧文: Chapter, Part, Book, Volume（例: "Chapter 1", "Chapter 2: War...", "Part I"）
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Chapter|Part|Book|Volume)\s+[0-9IVXLCDMivxlcdm]+(?:\s*[:–—-].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            // 4. 数字＋大見出しタイトル（例: "1 Introduction", "2 War in Eastern Ukraine...", "1 はじめに"）
            // ※枝番（1.1, 1.2.3等）や括弧（(1), [1]等）、節・項・条は除外
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:\d+|[０-９]+|[一二三四五六七八九十]+)(?:[\.．、\s　]+)[^\r\n]{2,80}$") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)+") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(\[【]") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]"))
            {
                string[] jpEndings = { "である", "でした", "ました", "ません", "における", "について" };
                if (!jpEndings.Any(e => trimmed.Contains(e)))
                {
                    return true;
                }
            }

            // 5. 欧文標準大セクション（例: "Introduction", "Conclusion", "Conclusions", "References", "Bibliography"）
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Introduction|Conclusion|Conclusions|References|Bibliography|Abstract)(?:\s*[:–—-].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// 指定された見出しテキストが大見出しである場合、その章・編・部を識別する正規化キー（例: "第1章", "序章", "chapter_1"）を返します。
        /// 大見出しでない場合は null を返します。
        /// </summary>
        public static string? ExtractMajorHeadingKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string trimmed = DocxExporter.RemovePagePrefix(text.Trim()).Trim();

            // 句点・読点・感嘆符・疑問符等で終わる通常の文章は除外
            if (trimmed.EndsWith("。") || trimmed.EndsWith("、") || trimmed.EndsWith(",") ||
                trimmed.EndsWith(";") || trimmed.EndsWith("；") ||
                trimmed.EndsWith("！") || trimmed.EndsWith("!") || trimmed.EndsWith("？") || trimmed.EndsWith("?"))
            {
                return null;
            }

            // 1. 和文: 章・編・部（例: 「第1章」「第2編」「第３部」「第一章」）
            var m1 = System.Text.RegularExpressions.Regex.Match(trimmed, @"^第([0-9０-９一二三四五六七八九十百千万]+)([章編部])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                string normNum = NormalizeNumberString(m1.Groups[1].Value);
                return $"第{normNum}{m1.Groups[2].Value}";
            }

            // 2. 和文: 序章・終章・補章・間章・プロローグ・エピローグ
            var m2 = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(序章|終章|補章|間章|プロローグ|エピローグ)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                return m2.Groups[1].Value;
            }

            // 3. 欧文: Chapter, Part, Book, Volume
            var m3 = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?:Chapter|Part|Book|Volume)\s+([0-9IVXLCDMivxlcdm]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m3.Success)
            {
                return $"chapter_{m3.Groups[1].Value.ToLowerInvariant()}";
            }

            // 4. 数字＋大見出しタイトル（例: "1 Introduction", "1 はじめに"）
            // ※枝番（1.1, 1.2.3等）や括弧（(1), [1]等）、節・項・条は除外
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:\d+|[０-９]+|[一二三四五六七八九十]+)(?:[\.．、\s　]+)[^\r\n]{2,80}$") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)+") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(\[【]") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]"))
            {
                string[] jpEndings = { "である", "でした", "ました", "ません", "における", "について" };
                if (!jpEndings.Any(e => trimmed.Contains(e)))
                {
                    var numMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(?:\d+|[０-９]+|[一二三四五六七八九十]+)");
                    string normNum = NormalizeNumberString(numMatch.Value);
                    return $"heading_num_{normNum}";
                }
            }

            // 5. 節・項・条・回・Section等の小見出し、括弧付き見出し、枝番見出しは除外（大見出しキーではない）
            if (!IsMajorHeading(trimmed) && (
                IsSubheadingText(trimmed) ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^第[0-9０-９一二三四五六七八九十百千万]+[節項条回]") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Section|Sec\.)\s+[0-9IVXLCDMivxlcdm]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(\[【①-⑳❶-❿]") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)+")))
            {
                return null;
            }

            // 6. 洋書・一般見出し領域（ユーザーがキャンバス上で「見出し」領域として設定した任意のテキスト）
            // 「章」の文字に限定せず、見出し領域として設定された単位で注釈番号を振り直す（例: "this is first" 等）
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length <= 80)
            {
                return DocxExporter.NormalizeForComparison(trimmed);
            }

            return null;
        }

        public static string NormalizeNumberString(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            if (raw == "十") return "10";
            var kmap = new System.Collections.Generic.Dictionary<char, char>
            {
                {'０','0'}, {'１','1'}, {'２','2'}, {'３','3'}, {'４','4'},
                {'５','5'}, {'６','6'}, {'７','7'}, {'８','8'}, {'９','9'},
                {'一','1'}, {'二','2'}, {'三','3'}, {'四','4'}, {'五','5'},
                {'六','6'}, {'七','7'}, {'八','8'}, {'九','9'}
            };
            var sb = new System.Text.StringBuilder();
            foreach (char c in raw)
            {
                if (kmap.TryGetValue(c, out char d)) sb.Append(d);
                else sb.Append(c);
            }
            return sb.ToString();
        }


        /// <summary>
        /// 指定されたテキストが小見出し（章・節・項・数字箇条書き・括弧見出し・記号見出し・欧文Stage/Phase/TitleCase見出し等）であるかを判定します。
        /// </summary>
        public static bool IsSubheadingText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 段落冒頭の字下げ（全角スペース、半角スペース、タブ）がある行は段落本文行であり、小見出しではない
            if (text.StartsWith("　") || text.StartsWith(" ") || text.StartsWith("\t"))
            {
                return false;
            }

            string trimmed = text.Trim();

            // 注釈文ラベルやページ区切り線は除外
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^【(?:注釈文|注)\d+】") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\[(?:注釈文|注)?\d+\]") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^---\s*ページ\s*\d+\s*---$"))
            {
                return false;
            }

            // 句点「。」を含む行は段落本文（文の途切れ）であり、小見出しではない
            if (trimmed.Contains("。"))
            {
                return false;
            }

            // 記号連続や接続詞文頭などのOCRノイズを除外
            if (DocxExporter.IsGarbageOrNoiseHeading(trimmed))
            {
                return false;
            }

            // 読点・セミコロン・感嘆符・疑問符等「、」「,」「;」「；」「！」「!」「？」「?」で終わる通常の文章は小見出しではない
            if (trimmed.EndsWith("、") || trimmed.EndsWith(",") ||
                trimmed.EndsWith(";") || trimmed.EndsWith("；") ||
                trimmed.EndsWith("！") || trimmed.EndsWith("!") || trimmed.EndsWith("？") || trimmed.EndsWith("?") ||
                trimmed.EndsWith(".\"") || trimmed.EndsWith("!'") || trimmed.EndsWith("?'"))
            {
                return false;
            }

            // ピリオド「.」で終わる場合: 単独の章節番号プレフィックス（例: "1.", "A.", "Section 1.", "Chapter 2."）以外は通常の文末とみなし除外
            if (trimmed.EndsWith("."))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:(?:\d+|[A-Za-z]|[IVXLCDMivxlcdm]+)|(?:Chapter|Section|Part|Appendix)\s+(?:\d+|[IVXLCDMivxlcdm]+))\.$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }

            // 閉じ括弧で終わる場合: 括弧付き見出し（【...】や（1）等）以外で文末に括弧がある文章は除外
            // また、(Medvedev 2020, 218) や [Horowitz 2000] などの文献・注記引用も除外
            if ((trimmed.StartsWith("(") && trimmed.EndsWith(")")) || (trimmed.StartsWith("（") && trimmed.EndsWith("）")) ||
                (trimmed.StartsWith("[") && trimmed.EndsWith("]")) || (trimmed.StartsWith("［") && trimmed.EndsWith("］")))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(\[［][0-9０-９一二三四五六七八九十a-zA-Z]+[）\)\]］]$"))
                {
                    return false;
                }
            }

            if ((trimmed.EndsWith("」") || trimmed.EndsWith("』") || trimmed.EndsWith("）") || trimmed.EndsWith(")") || trimmed.EndsWith("]") || trimmed.EndsWith("］")) &&
                !trimmed.StartsWith("【") && !trimmed.StartsWith("［") && !trimmed.StartsWith("[") &&
                !trimmed.StartsWith("〔") && !trimmed.StartsWith("〈") && !trimmed.StartsWith("《") &&
                !trimmed.StartsWith("『") && !trimmed.StartsWith("「") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(\[［][0-9０-９一二三四五六七八九十a-zA-Z]+[）\)\]］]$"))
            {
                return false;
            }

            bool isWestern = IsMainlyWesternText(trimmed);
            int maxAllowedLength = isWestern ? 120 : 60;
            if (trimmed.Length > maxAllowedLength)
            {
                return false;
            }

            // 疑問形見出し（例: "Why Has Ukraine Been So Important to Russia and Putin's Regime?"）
            bool isQuestionHeading = trimmed.EndsWith("?") && System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Why|How|What|Where|Who|When)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 和文述語・文末表現を含む継続文は小見出しではない
            if (!isWestern)
            {
                string[] jpSentenceEndings = new[]
                {
                    "における", "について", "により", "によって", "として", "に対する", "に対して", "に関する",
                    "としての", "についての", "からの", "なので", "ですが", "であり", "である",
                    "でした", "ました", "ません", "などの", "といった", "ような", "ながら",
                    "ている", "ていた", "となった", "となる", "を行った", "を行う", "にあった", "にある",
                    "に伴い", "に基づき", "に関して", "をはじめ", "を中心に", "を踏まえ", "とともに"
                };
                foreach (var conn in jpSentenceEndings)
                {
                    if (trimmed.Contains(conn))
                        return false;
                }
            }
            else
            {
                if (!isQuestionHeading)
                {
                    // 英文叙述文（小文字で助動詞や述語動詞を含む文）の除外
                    // ※"since", "with", "of", "in", "to", "for" などの前置詞・接続詞は見出しに含まれるため除外対象としない
                    string[] enPredicateVerbs = new[]
                    {
                        " was ", " were ", " has been ", " have been ", " had been ",
                        " is being ", " are being ", " was being ", " were being ",
                        " can be ", " could be ", " will be ", " would be ", " should be ", " may be ", " must be ",
                        " underwent ", " argued that ", " stated that ", " demonstrated that ", " showed that ",
                        " observed that ", " according to "
                    };
                    string lowerSpaced = " " + trimmed.ToLowerInvariant() + " ";
                    foreach (var verb in enPredicateVerbs)
                    {
                        if (lowerSpaced.Contains(verb))
                            return false;
                    }
                }
            }

            // 1. 和文: 章・節・項・条・編・部・回・巻・段階 見出しパターン
            // 例: 「第1章」「第一節　研究概要」「第３条（定義）」「第２回」「第2段階」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^第[0-9０-９一二三四五六七八九十百千万]+[章節項条部編回話巻段階](?:[\s　・:：].*)?$"))
                return true;

            // 2. 和文/共通: 漢数字・算用数字の箇条書き・見出しパターン
            // 例: 「１、はじめに」「1. 緒言」「一　研究背景」「３．結論」「1.1 背景」「1.2.3 実装」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:\d+|[０-９]+|[一二三四五六七八九十百]+)(?:[、．\.\s　]+[^\r\n]{1,50})$") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[一二三四五六七八九十百]+[\s　]+[^\s　]{6,}") &&
                !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+[\s　]*(?:年|月|日|ページ|頁|人|割|％|%|個|件|回|番|つ|段階で|において|年代)"))
                return true;

            // 3. 単独の数字・漢数字（章・節番号）
            // 例: 「１」「2」「一」「二」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:\d+|[０-９]+|[一二三四五六七八九十百]+)$"))
                return true;

            // 4. 括弧付き番号・記号
            // 例: 「(1) 概要」「（一） 対象者」「(a) 特徴」「[1] 実験」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[（\(][0-9０-９一二三四五六七八九十a-zA-Z]+[）\)](?:[\s　]*[^\r\n]{0,40})?$"))
                return true;

            // 5. 丸数字・特殊数字
            // 例: 「① 概要」「❷ 結果」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[①-⑳❶-❿⓫-⓴㊀-㊉](?:[\s　]*[^\r\n]{0,40})?$"))
                return true;

            // 6. 階層・枝番見出し
            // 例: 「1.1」「1.2.3」「一の二」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)+(?:[\s　]+[^\r\n]{0,40})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[一二三四五六七八九十]+の[一二三四五六七八九十]+(?:[\s　]+[^\r\n]{0,40})?$"))
                return true;

            // 7. 括弧付き小見出し
            // 例: 「【小見出し】」「［補足］」「〈調査結果〉」「『作品について』」「「はじめに」」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^【[^】\r\n]{1,35}】(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^［[^］\r\n]{1,35}］(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\[[^\]\r\n]{1,35}\](?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^〔[^〕\r\n]{1,35}〕(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^〈[^〉\r\n]{1,35}〉(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^《[^》\r\n]{1,35}》(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^『[^』\r\n]{1,35}』(?:[\s　]*[^\r\n]{0,35})?$") ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^「[^」\r\n]{1,35}」(?:[\s　]*[^\r\n]{0,35})?$"))
                return true;

            // 8. 記号プレフィックス付き見出し
            // 例: 「■ 結論」「◆ 考察」「● 留意点」「★ ポイント」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[■●▲◆▼★☆・▶▷◇○□][\s　]*(?:[^\r\n]{1,40})$"))
                return true;

            // 9. 和文標準キーワード小見出し
            // 例: 「はじめに」「おわりに」「参考文献」「まとめ」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:はじめに|おわりに|目次|要約|概要|研究背景|関連研究|提案手法|実験結果|考察|結論|まとめ|謝辞|参考文献|付録)$"))
                return true;

            // 10. 欧文 Stage / Phase / Part / Chapter / Section パターン
            // 例: 「Second Stage – Gradual Loss of Autonomy since August 2014」, 「First Stage - Mobilization」, 「Phase 1: Initial Deployment」, 「Chapter 1: Overview」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:(?:First|Second|Third|Fourth|Fifth|Sixth|Seventh|Eighth|Ninth|Tenth|Final|Initial)\s+(?:Stage|Phase|Part|Step|Section|Chapter|Level)|(?:Stage|Phase|Part|Step|Section|Chapter|Level)\s+(?:[0-9IVXLCDMivxlcdm]+|[A-Za-z]+))(?:\s*[:–—-]\s*.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Chapter|Section|Part|Appendix|Book|Volume)\s+[0-9IVXLCDMivxlcdm]+(?:\.[0-9]+)*(?::\s*[A-Za-z0-9\s,-]{0,50})?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;

            // 11. 欧文標準セクションキーワード
            // 例: 「Introduction」「Background」「Methods」「Results」「Discussion」「Conclusion」
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:Introduction|Background|Methods|Methodology|Results|Discussion|Conclusion|Conclusions|Abstract|Summary|References|Bibliography|Acknowledgments|Overview|Objectives|Analysis)(?::\s*[A-Za-z0-9\s,-]{0,50})?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Z]\.\s+(?:Introduction|Background|Methods|Methodology|Results|Discussion|Conclusion|Overview|Summary|[A-Z][a-z]{1,20}(?:\s+[A-Z][a-z]{1,20}){0,4})$"))
                return true;

            // 12. 欧文 Title Case 見出し判定（各主要単語が大文字で始まり、文末約物のない短い行）
            // 例: 「Secret Services」, 「Russian Armed Forces」, 「Political and Economic Landscape」, 「Loss of Autonomy」
            if (isWestern && trimmed.Length <= 80 && !trimmed.StartsWith("[") && !trimmed.StartsWith("(") && !trimmed.Contains(",") && !trimmed.Contains(":") && !trimmed.Contains(";"))
            {
                string normText = System.Text.RegularExpressions.Regex.Replace(trimmed, @"['’]s\b", "");
                var words = normText.Split(new[] { ' ', '\t', '–', '—', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2 && words.Length <= 10)
                {
                    var minorWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "a", "an", "the", "and", "but", "or", "nor", "for", "on", "at", "to", "from",
                        "by", "with", "in", "of", "since", "as", "into", "onto", "via", "&", "over",
                        "under", "between", "through", "during", "without", "against", "around",
                        "towards", "toward", "until", "about", "within", "after", "before", "among",
                        "their", "his", "her", "its", "our", "your", "per"
                    };

                    int capCount = 0;
                    int totalMajor = 0;
                    for (int w = 0; w < words.Length; w++)
                    {
                        string word = words[w].Trim();
                        string cleanW = System.Text.RegularExpressions.Regex.Replace(word, @"[^A-Za-z0-9]", "");
                        if (string.IsNullOrEmpty(cleanW) || cleanW.All(char.IsDigit) || cleanW.Length <= 1) continue;

                        if (w == 0 || !minorWords.Contains(cleanW))
                        {
                            totalMajor++;
                            if (char.IsUpper(cleanW[0]) || cleanW.StartsWith("anti", StringComparison.OrdinalIgnoreCase) || cleanW.StartsWith("connected", StringComparison.OrdinalIgnoreCase))
                            {
                                capCount++;
                            }
                        }
                    }

                    if (totalMajor >= 1 && capCount >= totalMajor)
                    {
                        return true;
                    }
                }
            }

            if (isQuestionHeading)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 幾何情報（前の段落との行間が通常より広い・行長が80%より短い・字下げインデントなし・文末約物なし等）も含めて総合的に小見出し行であるかを判定します。
        /// </summary>
        public static bool IsSubheadingLine(
            string text,
            double lineGap = 0,
            double medianLineGap = 0,
            double lineLength = 0,
            double maxLineLength = 0,
            double topOrLeftGap = 0,
            double botOrRightGap = 0,
            double avgCharSize = 0,
            bool isVertical = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 段落冒頭の字下げ（全角スペース、半角スペース、タブ）がある行は小見出しではない
            if (text.StartsWith("　") || text.StartsWith(" ") || text.StartsWith("\t"))
            {
                return false;
            }

            string trimmed = text.Trim();

            // 【条件0: 横書き字下げ（インデント）された通常行の小見出し完全除外】
            // 横書き（特に洋書）では、本文段落の冒頭行は1〜4文字分（洋書では通常3文字前後）の字下げが存在する。
            // 括弧付き見出し（【...】等）以外の行で、左端から字下げ（topOrLeftGap >= avgCharSize * 0.75）がある行は、
            // 見出しではなく段落冒頭行であるため小見出しから除外する。
            if (!isVertical && avgCharSize > 0 && topOrLeftGap >= avgCharSize * 0.75)
            {
                if (!trimmed.StartsWith("【") && !trimmed.StartsWith("［") && !trimmed.StartsWith("["))
                {
                    return false;
                }
            }

            // 3. テキストパターンマッチ判定（章・節・数字見出し・括弧見出し・記号見出し・Stage/Phase・Title Case見出し等）
            return IsSubheadingText(trimmed);
        }

        private static string FormatMultiDeckParagraphs(
            List<OcrDisplayItem> items,
            int deckCount,
            bool isVertical,
            bool isWestern,
            bool autoDetectSubheadings = false)
        {
            if (items.Count == 0) return "";
            if (deckCount <= 1) return FormatSingleTierParagraphs(items, isVertical, isWestern, autoDetectSubheadings);

            if (!isVertical)
            {
                // 横書き段組（左右分割：左段 -> 右段）
                double minX = items.Min(i => i.X);
                double maxX = items.Max(i => i.X + i.Width);
                double totalW = maxX - minX;
                if (totalW <= 10) return FormatSingleTierParagraphs(items, isVertical, isWestern, autoDetectSubheadings);

                var cols = new List<List<OcrDisplayItem>>();
                for (int d = 0; d < deckCount; d++)
                    cols.Add(new List<OcrDisplayItem>());

                double colW = totalW / deckCount;
                foreach (var item in items)
                {
                    double cx = item.X + item.Width / 2.0;
                    int colIdx = (int)((cx - minX) / colW);
                    if (colIdx < 0) colIdx = 0;
                    if (colIdx >= deckCount) colIdx = deckCount - 1;
                    cols[colIdx].Add(item);
                }

                var paraList = new List<string>();
                foreach (var col in cols)
                {
                    if (col.Count > 0)
                    {
                        string p = FormatSingleTierParagraphs(col, isVertical, isWestern, autoDetectSubheadings);
                        if (!string.IsNullOrWhiteSpace(p))
                            paraList.Add(p);
                    }
                }

                var mergedParas = MergeFlowingParagraphs(paraList, isWestern);
                return string.Join(Environment.NewLine + Environment.NewLine, mergedParas);
            }
            else
            {
                // 縦書き段組（上下分割：上段 -> 下段）
                double minY = items.Min(i => i.Y);
                double maxY = items.Max(i => i.Y + i.Height);
                double totalH = maxY - minY;
                if (totalH <= 10) return FormatSingleTierParagraphs(items, isVertical, isWestern, autoDetectSubheadings);

                var tiers = new List<List<OcrDisplayItem>>();
                for (int d = 0; d < deckCount; d++)
                    tiers.Add(new List<OcrDisplayItem>());

                double deckH = totalH / deckCount;

                foreach (var item in items)
                {
                    double cy = item.Y + item.Height / 2.0;
                    int tierIdx = (int)((cy - minY) / deckH);
                    if (tierIdx < 0) tierIdx = 0;
                    if (tierIdx >= deckCount) tierIdx = deckCount - 1;
                    tiers[tierIdx].Add(item);
                }

                var paraList = new List<string>();
                foreach (var tier in tiers)
                {
                    if (tier.Count > 0)
                    {
                        string p = FormatSingleTierParagraphs(tier, isVertical, isWestern, autoDetectSubheadings);
                        if (!string.IsNullOrWhiteSpace(p))
                            paraList.Add(p);
                    }
                }

                var mergedParas = MergeFlowingParagraphs(paraList, isWestern);
                return string.Join(Environment.NewLine + Environment.NewLine, mergedParas);
            }
        }

        public static string FormatSingleTierParagraphs(
            List<OcrDisplayItem> uniqueItems,
            bool isVertical,
            bool isWestern,
            bool autoDetectSubheadings = false)
        {
            if (uniqueItems.Count == 0)
                return "";

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
                    LeftX = col.Min(c => c.X),
                    RightX = col.Max(c => c.X + c.Width),
                    Width = col.Max(c => c.Width)
                }).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

                if (lines.Count == 0)
                    return "";

                double minTopY = lines.Min(l => l.TopY);
                double maxBotY = lines.Max(l => l.BotY);
                double maxLineHeight = lines.Max(l => l.BotY - l.TopY);
                double avgCharSize = lines.Average(l => l.Width);

                var colGaps = new List<double>();
                for (int k = 1; k < lines.Count; k++)
                {
                    double g = lines[k - 1].LeftX - lines[k].RightX;
                    if (g > 0) colGaps.Add(g);
                }
                double medianColGap = colGaps.Count > 0
                    ? colGaps.OrderBy(g => g).ElementAt(colGaps.Count / 2)
                    : (avgCharSize * 0.4);

                var paragraphs = new List<string>();
                var currentPara = new List<string> { lines[0].Text };

                for (int i = 1; i < lines.Count; i++)
                {
                    var prev = lines[i - 1];
                    var curr = lines[i];

                    double prevTopGap = prev.TopY - minTopY;
                    double prevBotGap = maxBotY - prev.BotY;
                    double currTopGap = curr.TopY - minTopY;
                    double currBotGap = maxBotY - curr.BotY;
                    double colGap = prev.LeftX - curr.RightX;
                    double currLineHeight = curr.BotY - curr.TopY;
                    double prevLineHeight = prev.BotY - prev.TopY;
                    double prevColGap = (i >= 2) ? (lines[i - 2].LeftX - prev.RightX) : (medianColGap * 1.5);

                    bool isCurrSubheading = autoDetectSubheadings && IsSubheadingLine(curr.Text, colGap, medianColGap, currLineHeight, maxLineHeight, currTopGap, currBotGap, avgCharSize, true);
                    bool isPrevSubheading = autoDetectSubheadings && IsSubheadingLine(prev.Text, prevColGap, medianColGap, prevLineHeight, maxLineHeight, prevTopGap, prevBotGap, avgCharSize, true);

                    // 1. 列間（行間）が通常より広く空いている
                    bool isExtraColGap = colGap >= Math.Max(avgCharSize * 0.70, medianColGap * 1.30);

                    // 2. 天からの字下げ・インデント
                    bool startsWithSpace = curr.Text.StartsWith("　") || curr.Text.StartsWith(" ") || curr.Text.StartsWith("\t");
                    bool isIndentFromPrev = curr.TopY >= prev.TopY + avgCharSize * 0.65;
                    bool isAlignedWithPrev = Math.Abs(curr.TopY - prev.TopY) < avgCharSize * 0.35;
                    bool isPrevBlockquote = (i >= 2) && (lines[i - 2].TopY - minTopY >= avgCharSize * 1.0);
                    bool isOutdentAfterBlockquote = isPrevBlockquote && prevTopGap >= avgCharSize * 1.2 && currTopGap <= prevTopGap - avgCharSize * 0.8;
                    bool isCurrIndented = startsWithSpace || isIndentFromPrev || (currTopGap >= avgCharSize * 0.65 && !isAlignedWithPrev);

                    // 3. 列間拡張 ＋ インデント（複合：引用ブロック・補足段落等）
                    bool isGapAndIndent = isExtraColGap || (colGap >= Math.Min(avgCharSize * 0.45, medianColGap * 1.15) && (isIndentFromPrev || startsWithSpace || (currTopGap >= avgCharSize * 0.45 && !isAlignedWithPrev)));

                    // 4. 前列が短列かつ文末約物
                    bool isPrevShort = prevBotGap > avgCharSize * 2.0;
                    bool prevEndsSentence = EndsWithSentencePunctuation(prev.Text);

                    // 直前の行の行末に「。」あるいは「.」（または文末約物）がなければ、見出し行の開始を除き段落の中に組み込む
                    bool shouldBreak = isCurrSubheading ||
                        (prevEndsSentence && (isPrevSubheading || isExtraColGap || isCurrIndented || isGapAndIndent || isOutdentAfterBlockquote || isPrevShort || (currTopGap > avgCharSize * 3.0)));

                    if (shouldBreak)
                    {
                        if (currentPara.Count > 0)
                        {
                            paragraphs.Add(string.Join("", currentPara));
                        }
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

                        // 波打ち・湾曲耐性: 行高さの55%までの上下ブレを同一行として許容
                        if (distance < minHeight * 0.55 && distance < bestDistance)
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
                    TopY = row.Min(r => r.Y),
                    BotY = row.Max(r => r.Y + r.Height),
                    Height = row.Max(r => r.Height)
                }).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();

                if (lines.Count == 0)
                    return "";

                double minLeftX = lines.Min(l => l.LeftX);
                double maxRightX = lines.Max(l => l.RightX);
                double maxLineWidth = lines.Max(l => l.RightX - l.LeftX);
                double avgCharSize = lines.Average(l => l.Height);

                var lineGaps = new List<double>();
                for (int k = 1; k < lines.Count; k++)
                {
                    double g = lines[k].TopY - lines[k - 1].BotY;
                    if (g > 0) lineGaps.Add(g);
                }
                double medianLineGap = lineGaps.Count > 0
                    ? lineGaps.OrderBy(g => g).ElementAt(lineGaps.Count / 2)
                    : (avgCharSize * 0.4);

                var paragraphs = new List<string>();
                var currentPara = new List<string> { lines[0].Text };

                for (int i = 1; i < lines.Count; i++)
                {
                    var prev = lines[i - 1];
                    var curr = lines[i];

                    double prevLeftGap = prev.LeftX - minLeftX;
                    double prevRightGap = maxRightX - prev.RightX;
                    double currLeftGap = curr.LeftX - minLeftX;
                    double currRightGap = maxRightX - curr.RightX;
                    double lineGap = curr.TopY - prev.BotY;
                    double currLineWidth = curr.RightX - curr.LeftX;
                    double prevLineWidth = prev.RightX - prev.LeftX;
                    double prevLineGap = (i >= 2) ? (prev.TopY - lines[i - 2].BotY) : (medianLineGap * 1.5);

                    bool isCurrSubheading = autoDetectSubheadings && IsSubheadingLine(curr.Text, lineGap, medianLineGap, currLineWidth, maxLineWidth, currLeftGap, currRightGap, avgCharSize, false);
                    bool isPrevSubheading = autoDetectSubheadings && IsSubheadingLine(prev.Text, prevLineGap, medianLineGap, prevLineWidth, maxLineWidth, prevLeftGap, prevRightGap, avgCharSize, false);

                    // 1. 行間が通常より広く空いている
                    bool isExtraLineGap = lineGap >= Math.Max(avgCharSize * 0.70, medianLineGap * 1.30);

                    // 2. 字下げインデント
                    bool startsWithSpace = curr.Text.StartsWith("　") || curr.Text.StartsWith(" ") || curr.Text.StartsWith("\t");
                    bool isIndentFromPrev = curr.LeftX >= prev.LeftX + avgCharSize * 0.65;
                    bool isAlignedWithPrev = Math.Abs(curr.LeftX - prev.LeftX) < avgCharSize * 0.35;
                    bool isPrevBlockquote = (i >= 2) && (lines[i - 2].LeftX - minLeftX >= avgCharSize * 1.0);
                    bool isOutdentAfterBlockquote = isPrevBlockquote && prevLeftGap >= avgCharSize * 1.2 && currLeftGap <= prevLeftGap - avgCharSize * 0.8;
                    bool isCurrIndented = startsWithSpace || isIndentFromPrev || (currLeftGap >= avgCharSize * 0.75 && !isAlignedWithPrev);

                    // 3. 行間拡張 ＋ インデント（複合：引用ブロック・補足文章・独立段落等）
                    bool isGapAndIndent = isExtraLineGap || (lineGap >= Math.Min(avgCharSize * 0.45, medianLineGap * 1.15) && (isIndentFromPrev || startsWithSpace || (currLeftGap >= avgCharSize * 0.45 && !isAlignedWithPrev)));

                    // 4. 引用符・括弧の開始
                    bool isQuoteStart = curr.Text.StartsWith("「") || curr.Text.StartsWith("『") ||
                                        curr.Text.StartsWith("“") || curr.Text.StartsWith("\"") ||
                                        curr.Text.StartsWith("（") || curr.Text.StartsWith("(");

                    // 5. 前行の行末短縮 ＋ 文末約物
                    bool isPrevShort = prevRightGap > avgCharSize * 2.5;
                    bool prevEndsSentence = EndsWithSentencePunctuation(prev.Text);

                    bool isQuoteParagraph = isQuoteStart && (isExtraLineGap || isPrevShort || prevEndsSentence || isIndentFromPrev || startsWithSpace);

                    // 直前の行の行末に「。」あるいは「.」（または文末約物）がなければ、見出し行の開始を除き段落の中に組み込む
                    bool shouldBreak = isCurrSubheading ||
                        (prevEndsSentence && (isPrevSubheading || isExtraLineGap || isCurrIndented || isGapAndIndent || isOutdentAfterBlockquote || isQuoteParagraph || isPrevShort));

                    if (shouldBreak)
                    {
                        if (currentPara.Count > 0)
                        {
                            paragraphs.Add(JoinParagraphLines(currentPara, isWestern));
                        }
                        currentPara = new List<string> { curr.Text };
                    }
                    else
                    {
                        currentPara.Add(curr.Text);
                    }
                }

                if (currentPara.Count > 0)
                {
                    paragraphs.Add(JoinParagraphLines(currentPara, isWestern));
                }

                return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
            }
        }

        private static string JoinParagraphLines(List<string> lines, bool isWestern)
        {
            if (lines.Count == 0) return "";
            if (!isWestern)
            {
                return string.Join("", lines);
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (sb.Length == 0)
                {
                    sb.Append(line);
                }
                else
                {
                    if (sb.ToString().EndsWith("-"))
                    {
                        sb.Length--; // 行末ハイフン削除して単語結合 (e.g. sustain- + able -> sustainable)
                        sb.Append(line);
                    }
                    else
                    {
                        sb.Append(" ");
                        sb.Append(line);
                    }
                }
            }
            return sb.ToString();
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
