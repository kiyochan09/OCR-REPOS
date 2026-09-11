using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// 各ページのOCR抽出結果（OcrPageData）のJSON保存・読込・全ページ収集サービス
    /// </summary>
    public static class OcrPageDataService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 1ページ分のOcrPageDataを page_data.json として保存します。
        /// </summary>
        public static void SavePageData(string pageDir, OcrPageData data)
        {
            if (string.IsNullOrEmpty(pageDir) || data == null) return;

            try
            {
                Directory.CreateDirectory(pageDir);
                string jsonPath = Path.Combine(pageDir, "page_data.json");
                string jsonString = JsonSerializer.Serialize(data, JsonOptions);
                File.WriteAllText(jsonPath, jsonString, Encoding.UTF8);

                if (data.BodyParagraphs != null && data.BodyParagraphs.Count > 0)
                {
                    string bodyReadingOrderPath = Path.Combine(pageDir, "body_reading_order.txt");
                    File.WriteAllText(bodyReadingOrderPath, string.Join(Environment.NewLine + Environment.NewLine, data.BodyParagraphs), Encoding.UTF8);
                }
            }
            catch
            {
                // 保存失敗時は例外を握りつぶし処理を継続
            }
        }

        /// <summary>
        /// 指定されたページディレクトリから page_data.json を読み込みます。
        /// page_data.json が存在しない場合や BodyParagraphs が空の場合は、
        /// 同ディレクトリ内の body_reading_order.txt から自動復元（自己修復）します。
        /// </summary>
        public static OcrPageData? LoadPageData(string pageDir)
        {
            if (string.IsNullOrEmpty(pageDir) || !Directory.Exists(pageDir)) return null;

            string jsonPath = Path.Combine(pageDir, "page_data.json");
            OcrPageData? data = null;

            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(jsonPath, Encoding.UTF8);
                    data = JsonSerializer.Deserialize<OcrPageData>(jsonString, JsonOptions);
                }
                catch { }
            }

            if (data == null)
            {
                data = new OcrPageData();
                string dirName = Path.GetFileName(pageDir);
                if (dirName.StartsWith("page_") && int.TryParse(dirName.Substring(5), out int pNum))
                {
                    data.PageNumber = pNum;
                }
            }

            // 自己修復 / フォールバック: BodyParagraphs が空の場合、body_reading_order.txt から本文段落を復元
            if (data.BodyParagraphs == null || data.BodyParagraphs.Count == 0)
            {
                string bodyReadingOrderPath = Path.Combine(pageDir, "body_reading_order.txt");
                if (File.Exists(bodyReadingOrderPath))
                {
                    try
                    {
                        string bodyTxt = File.ReadAllText(bodyReadingOrderPath, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(bodyTxt))
                        {
                            data.BodyParagraphs = bodyTxt.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .Where(p => !string.IsNullOrEmpty(p))
                                .ToList();

                            // 復元した完全なデータを page_data.json へ再保存してディスクを自己修復
                            SavePageData(pageDir, data);
                        }
                    }
                    catch { }
                }
            }

            // 過去の自動判定や同期により Headings に誤混入した長文段落（80文字超や句点「。」含む文）および解除済み見出しを自動除去
            if (data.Headings != null && data.Headings.Count > 0)
            {
                var removedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (data.RemovedHeadings != null)
                {
                    foreach (var rh in data.RemovedHeadings)
                    {
                        if (!string.IsNullOrWhiteSpace(rh))
                        {
                            removedSet.Add(DocxExporter.RemovePagePrefix(rh.Trim()));
                        }
                    }
                }

                data.Headings = data.Headings
                    .Select(h => DocxExporter.RemovePagePrefix(h.Trim()))
                    .Where(h => !string.IsNullOrWhiteSpace(h) && h.Length <= 80 && !h.Contains("。") && !System.Text.RegularExpressions.Regex.IsMatch(h, @"^【(?:注釈文|注)\d+】"))
                    .Where(h => !removedSet.Contains(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // 1. 同一ページ内の前方一致フラグメント除去（例: 「第2章 クリミ」と「第2章 クリミア自治共和国の形成」）
                data.Headings.RemoveAll(h1 => data.Headings.Any(h2 => h2.Length > h1.Length && h2.StartsWith(h1, StringComparison.OrdinalIgnoreCase)));

                // 2. Headings に誤混入した小見出し（例: (2)沿ドニエストル 等）を Subheadings へ移動
                var misclassified = data.Headings.Where(h => OcrSorter.IsSubheadingText(h) && !OcrSorter.IsMajorHeading(h)).ToList();
                if (misclassified.Count > 0)
                {
                    if (data.Subheadings == null) data.Subheadings = new List<string>();
                    foreach (var msh in misclassified)
                    {
                        if (!data.Subheadings.Contains(msh, StringComparer.OrdinalIgnoreCase))
                            data.Subheadings.Add(msh);
                        data.Headings.Remove(msh);
                    }
                }
            }

            if (data.Subheadings != null && data.Subheadings.Count > 0)
            {
                data.Subheadings = data.Subheadings
                    .Select(sh => DocxExporter.RemovePagePrefix(sh.Trim()))
                    .Where(sh => !string.IsNullOrWhiteSpace(sh) && sh.Length <= 80 && !sh.Contains("。") && !System.Text.RegularExpressions.Regex.IsMatch(sh, @"^【(?:注釈文|注)\d+】"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                data.Subheadings.RemoveAll(s1 => data.Subheadings.Any(s2 => s2.Length > s1.Length && s2.StartsWith(s1, StringComparison.OrdinalIgnoreCase)));
            }

            // 段落内が1行ごとに改行されているレガシーデータを段落単位へ自動正規化
            if (data.BodyParagraphs != null && data.BodyParagraphs.Count > 0)
            {
                var normalized = new List<string>();
                foreach (var p in data.BodyParagraphs)
                {
                    string norm = NormalizeParagraphInternalBreaks(p);
                    if (!string.IsNullOrWhiteSpace(norm))
                    {
                        foreach (var subP in norm.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string cleanP = subP.Trim();
                            if (!string.IsNullOrEmpty(cleanP))
                                normalized.Add(cleanP);
                        }
                    }
                }
                data.BodyParagraphs = normalized;
            }

            if (data.Figures != null)
            {
                // 図の画像バイトが空の場合、同フォルダ内の figure_XX.png から復元
                foreach (var fig in data.Figures)
                {
                    if (fig.ImageBytes == null || fig.ImageBytes.Length == 0)
                    {
                        var figFiles = Directory.GetFiles(pageDir, "figure_*.*");
                        if (figFiles.Length > 0 && File.Exists(figFiles[0]))
                        {
                            fig.ImageBytes = File.ReadAllBytes(figFiles[0]);
                        }
                    }
                }
            }

            // 本文も図も表も見出しも注釈も一切ない場合は null を返す
            if ((data.BodyParagraphs == null || data.BodyParagraphs.Count == 0) &&
                (data.Headings == null || data.Headings.Count == 0) &&
                (data.Tables == null || data.Tables.Count == 0) &&
                (data.Figures == null || data.Figures.Count == 0) &&
                (data.Footnotes == null || data.Footnotes.Count == 0))
            {
                return null;
            }

            return data;
        }

        /// <summary>
        /// プロジェクトディレクトリ配下のすべての page_XXXX フォルダから保存済み OcrPageData をページ順に収集します。
        /// </summary>
        public static List<OcrPageData> LoadAllProjectPageData(string projectDir, string pdfName, int totalPages)
        {
            var result = new List<OcrPageData>();
            string ocrResultsDir = Path.Combine(projectDir, "ocr_results", pdfName);
            if (!Directory.Exists(ocrResultsDir)) return result;

            int maxP = Math.Max(totalPages, 1);
            try
            {
                var pageDirs = Directory.GetDirectories(ocrResultsDir, "page_*");
                foreach (var dir in pageDirs)
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith("page_") && int.TryParse(dirName.Substring(5), out int pNum))
                    {
                        maxP = Math.Max(maxP, pNum);
                    }
                }
            }
            catch { }

            for (int p = 1; p <= maxP; p++)
            {
                string pageDir = Path.Combine(ocrResultsDir, $"page_{p:0000}");
                var pageData = LoadPageData(pageDir);
                if (pageData != null)
                {
                    pageData.PageNumber = p;
                    result.Add(pageData);
                }
            }

            return result;
        }

        /// <summary>
        /// 段落内が1行ごと（または文字数制限等）で物理改行されている場合に、
        /// 小見出し行を保護しつつ本文行を自然に結合して1つの連続した段落（段落単位）へ正規化します。
        /// </summary>
        public static string NormalizeParagraphInternalBreaks(string para)
        {
            if (string.IsNullOrWhiteSpace(para)) return "";
            string[] rawLines = para.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (rawLines.Length <= 1) return para.Trim();

            var resultBlocks = new List<string>();
            var currentBody = new List<string>();

            void FlushBody()
            {
                if (currentBody.Count == 0) return;
                bool isWest = OcrSorter.IsMainlyWesternText(string.Join(" ", currentBody));
                if (isWest)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var l in currentBody)
                    {
                        string trimmed = l.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;
                        if (sb.Length == 0)
                        {
                            sb.Append(trimmed);
                        }
                        else
                        {
                            if (sb.ToString().EndsWith("-"))
                            {
                                sb.Length--;
                                sb.Append(trimmed);
                            }
                            else
                            {
                                sb.Append(" ");
                                sb.Append(trimmed);
                            }
                        }
                    }
                    resultBlocks.Add(sb.ToString());
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var l in currentBody)
                    {
                        string trimmed = l.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;
                        if (sb.Length == 0)
                        {
                            sb.Append(trimmed);
                        }
                        else
                        {
                            char lastChar = sb[sb.Length - 1];
                            char firstChar = trimmed[0];
                            bool lastIsAlnum = (lastChar >= 'a' && lastChar <= 'z') || (lastChar >= 'A' && lastChar <= 'Z') || (lastChar >= '0' && lastChar <= '9');
                            bool firstIsAlnum = (firstChar >= 'a' && firstChar <= 'z') || (firstChar >= 'A' && firstChar <= 'Z') || (firstChar >= '0' && firstChar <= '9');
                            if (lastIsAlnum && firstIsAlnum)
                            {
                                sb.Append(" ");
                            }
                            sb.Append(trimmed);
                        }
                    }
                    resultBlocks.Add(sb.ToString());
                }
                currentBody.Clear();
            }

            foreach (var rawLine in rawLines)
            {
                string trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (OcrSorter.IsSubheadingText(trimmed))
                {
                    FlushBody();
                    resultBlocks.Add(trimmed);
                }
                else
                {
                    currentBody.Add(rawLine);
                }
            }
            FlushBody();

            return string.Join(Environment.NewLine + Environment.NewLine, resultBlocks);
        }

        /// <summary>
        /// 複数行にわたる注釈文（NDLOCRの物理行やUI行）を、注釈番号（1), 【注釈文1】, ① など）を基準にして
        /// 各注釈文の1つの完全な段落（1注釈1行）へ自然に結合・正規化します。
        /// </summary>
        public static List<string> MergeFootnoteLines(IEnumerable<string> rawLines, int fallbackStartNum = 1)
        {
            var result = new List<string>();
            if (rawLines == null) return result;

            string? currentTag = null;
            var currentSb = new System.Text.StringBuilder();

            void FlushCurrent()
            {
                if (currentSb.Length == 0) return;
                string text = currentSb.ToString().Trim();
                if (string.IsNullOrEmpty(text)) return;

                string finalItem;
                if (!string.IsNullOrEmpty(currentTag))
                {
                    string cleanText = Regex.Replace(text, @"^【(?:注釈文|注)(?:\d+|__NEW__)】\s*", "").Trim();
                    finalItem = $"{currentTag} {cleanText}";
                }
                else
                {
                    var match = Regex.Match(text, @"^【(?:注釈文|注)(\d+|__NEW__)】");
                    if (match.Success)
                    {
                        finalItem = text;
                    }
                    else
                    {
                        finalItem = $"【注釈文{fallbackStartNum++}】 {text}";
                    }
                }
                result.Add(finalItem);
                currentSb.Clear();
                currentTag = null;
            }

            var linesList = rawLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (linesList.Count == 0) return result;

            bool hasAnyPrintedNumber = linesList.Any(l =>
            {
                string clean = Regex.Replace(l, @"^\[P\d+(?:-\d+)?\]\s*", "").Trim();
                clean = Regex.Replace(clean, @"^【(?:注釈文|注)(?:\d+|__NEW__)】\s*", "").Trim();
                return Regex.IsMatch(clean, @"^(?:(\d{1,3})[\)）\.\:]|\[(?:注)?(\d{1,3})\]|[\(（](\d{1,3})[\)）]|([①-⑳㉑-㉟]))");
            });

            foreach (var rawLine in linesList)
            {
                string line = rawLine?.Trim() ?? "";
                if (string.IsNullOrEmpty(line)) continue;

                // ページプレフィックス [P1] や [P1-01] を除去
                line = Regex.Replace(line, @"^\[P\d+(?:-\d+)?\]\s*", "").Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // 注釈番号パターンの判定
                // 1) 既存の 【注釈文N】 または 【注N】
                // 2) 印刷された注釈番号: 1), 1., 1:, [1], (1), ①〜⑳
                var tagMatch = Regex.Match(line, @"^【(?:注釈文|注)(\d+|__NEW__)】\s*(.*)$");
                string lineBody = tagMatch.Success ? tagMatch.Groups[2].Value.Trim() : line;

                var numMatch = Regex.Match(lineBody, @"^(?:(\d{1,3})[\)）\.\:]|\[(?:注)?(\d{1,3})\]|[\(（](\d{1,3})[\)）]|([①-⑳㉑-㉟]))\s*(.*)$");

                bool isNewNote = false;
                string detectedTag = "";

                if (numMatch.Success)
                {
                    // 本文中に印刷された注釈番号が存在する場合（最優先）
                    isNewNote = true;
                    int noteNum;
                    if (numMatch.Groups[1].Success) noteNum = int.Parse(numMatch.Groups[1].Value);
                    else if (numMatch.Groups[2].Success) noteNum = int.Parse(numMatch.Groups[2].Value);
                    else if (numMatch.Groups[3].Success) noteNum = int.Parse(numMatch.Groups[3].Value);
                    else
                    {
                        char c = numMatch.Groups[4].Value[0];
                        noteNum = (c >= '①' && c <= '⑳') ? (c - '①' + 1) : (c - '㉑' + 21);
                    }
                    detectedTag = $"【注釈文{noteNum}】";
                }
                else if (tagMatch.Success && tagMatch.Groups[1].Value == "__NEW__")
                {
                    // ユーザーが手動で設定した新規注釈
                    isNewNote = true;
                    detectedTag = "【注釈文__NEW__】";
                }
                else if (tagMatch.Success)
                {
                    isNewNote = true;
                    detectedTag = $"【注釈文{tagMatch.Groups[1].Value}】";
                }
                else if (currentSb.Length == 0)
                {
                    // ページの先頭行で番号がない場合（前のページからの継続注釈など）
                    isNewNote = true;
                    detectedTag = $"【注釈文{fallbackStartNum}】";
                }

                if (isNewNote)
                {
                    FlushCurrent();
                    currentTag = detectedTag;
                    currentSb.Append(lineBody);
                }
                else
                {
                    // 継続行を自然に結合
                    AppendMergedFootnoteLine(currentSb, lineBody);
                }
            }

            FlushCurrent();
            return result;
        }

        private static void AppendMergedFootnoteLine(System.Text.StringBuilder sb, string nextLine)
        {
            string trimmed = nextLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
            if (sb.Length == 0)
            {
                sb.Append(trimmed);
                return;
            }

            bool isWestern = OcrSorter.IsMainlyWesternText(sb.ToString() + " " + trimmed);
            if (isWestern)
            {
                if (sb.ToString().EndsWith("-"))
                {
                    sb.Length--;
                    sb.Append(trimmed);
                }
                else
                {
                    sb.Append(" ");
                    sb.Append(trimmed);
                }
            }
            else
            {
                char last = sb[^1];
                char first = trimmed[0];
                bool lastAlnum = (last >= 'a' && last <= 'z') || (last >= 'A' && last <= 'Z') || (last >= '0' && last <= '9');
                bool firstAlnum = (first >= 'a' && first <= 'z') || (first >= 'A' && first <= 'Z') || (first >= '0' && first <= '9');
                if (lastAlnum && firstAlnum)
                {
                    sb.Append(" ");
                }
                sb.Append(trimmed);
            }
        }

        /// <summary>
        /// UIの注釈文テキストボックスの内容をページ番号（[P1], [P2]...）ごとに分割し、
        /// 各ページの注釈文を段落単位（複数行の継続行を結合）でリスト化して返します。
        /// </summary>
        public static Dictionary<int, List<string>> ParseFootnotesByPages(string fullText)
        {
            var result = new Dictionary<int, List<string>>();
            if (string.IsNullOrWhiteSpace(fullText)) return result;

            var lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int curPage = 1;
            var pageLines = new Dictionary<int, List<string>>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var match = Regex.Match(trimmed, @"^\[P(\d+)(?:-\d+)?\]\s*(.*)$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int pageNum))
                {
                    curPage = pageNum;
                    string content = match.Groups[2].Value.Trim();
                    if (!pageLines.ContainsKey(curPage)) pageLines[curPage] = new List<string>();
                    if (!string.IsNullOrEmpty(content)) pageLines[curPage].Add(content);
                }
                else
                {
                    if (!pageLines.ContainsKey(curPage)) pageLines[curPage] = new List<string>();
                    pageLines[curPage].Add(trimmed);
                }
            }

            int lastNoteNum = 1;
            foreach (var p in pageLines.Keys.OrderBy(k => k))
            {
                var merged = MergeFootnoteLines(pageLines[p], fallbackStartNum: lastNoteNum);
                result[p] = merged;
                if (merged.Count > 0)
                {
                    var lastMatch = Regex.Match(merged[^1], @"^【(?:注釈文|注)(\d+)】");
                    if (lastMatch.Success && int.TryParse(lastMatch.Groups[1].Value, out int ln))
                    {
                        lastNoteNum = ln;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 注釈文テキストから印刷された注釈番号またはタグ番号を抽出してソート用キーを返します。
        /// </summary>
        public static int GetFootnoteSortKey(string item, int fallbackIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(item)) return fallbackIndex;
            // 1) 印刷された注釈番号: 22) や [22] 等
            var clean = Regex.Replace(item, @"^\[P\d+(?:-\d+)?\]\s*", "").Trim();
            clean = Regex.Replace(clean, @"^【(?:注釈文|注)(?:\d+|__NEW__)】\s*", "").Trim();
            var pm = Regex.Match(clean, @"^(?:(\d{1,3})[\)）\.\:]|\[(?:注)?(\d{1,3})\]|[\(（](\d{1,3})[\)）]|([①-⑳㉑-㉟]))");
            if (pm.Success)
            {
                if (pm.Groups[1].Success && int.TryParse(pm.Groups[1].Value, out int n1)) return n1;
                if (pm.Groups[2].Success && int.TryParse(pm.Groups[2].Value, out int n2)) return n2;
                if (pm.Groups[3].Success && int.TryParse(pm.Groups[3].Value, out int n3)) return n3;
                if (pm.Groups[4].Success)
                {
                    char c = pm.Groups[4].Value[0];
                    return (c >= '①' && c <= '⑳') ? (c - '①' + 1) : (c - '㉑' + 21);
                }
            }

            // 2) タグ 【注釈文N】
            var tm = Regex.Match(item, @"^【(?:注釈文|注)(\d+)】");
            if (tm.Success && int.TryParse(tm.Groups[1].Value, out int tn))
            {
                return tn;
            }

            return fallbackIndex;
        }

        /// <summary>
        /// 全ページの本文段落および注釈文リスト内の注釈番号（【注N】、【注釈文N】）を、
        /// 設定（文書通し番号 または 大見出し単位）に従って先頭から昇順に整列・再採番します。
        /// 変更があった場合は true を返します。
        /// </summary>
        public static bool RenumberAllFootnotes(List<OcrPageData> pages, AppSettings? settings = null)
        {
            if (pages == null || pages.Count == 0) return false;

            bool isByMajorHeading = settings?.FootnoteNumberingScope == "majorHeading";
            int bodyCounter = 1;
            int footnoteCounter = 1;
            int lastNoteNum = 1;
            bool anyChanged = false;
            string? currentChapterKey = null;

            foreach (var page in pages.OrderBy(p => p.PageNumber))
            {
                // 大見出し単位の採番設定時: ページ内の見出しに新しい大見出し（章）が現れた場合のみリセット
                if (isByMajorHeading && page.Headings != null && page.Headings.Count > 0)
                {
                    string? pageChapterKey = null;
                    foreach (var h in page.Headings)
                    {
                        var key = OcrSorter.ExtractMajorHeadingKey(h);
                        if (key != null)
                        {
                            pageChapterKey = key;
                            break;
                        }
                    }

                    if (pageChapterKey != null && pageChapterKey != currentChapterKey)
                    {
                        currentChapterKey = pageChapterKey;
                        bodyCounter = 1;
                        footnoteCounter = 1;
                        lastNoteNum = 1;
                    }
                }

                // 注釈文リスト内の複数物理行・継続行をマージ正規化
                if (page.Footnotes != null && page.Footnotes.Count > 0)
                {
                    // 印刷番号または既存タグ番号に基づく安定昇順ソート（誤順序の自己修復）
                    page.Footnotes = page.Footnotes
                        .Select((fn, idx) => new { Text = fn, SortKey = GetFootnoteSortKey(fn, idx) })
                        .OrderBy(x => x.SortKey)
                        .Select(x => x.Text)
                        .ToList();

                    var merged = MergeFootnoteLines(page.Footnotes, fallbackStartNum: lastNoteNum);
                    if (merged.Count != page.Footnotes.Count || !merged.SequenceEqual(page.Footnotes))
                    {
                        page.Footnotes = merged;
                        anyChanged = true;
                    }

                    if (merged.Count > 0)
                    {
                        var lastMatch = Regex.Match(merged[^1], @"^【(?:注釈文|注)(\d+)】");
                        if (lastMatch.Success && int.TryParse(lastMatch.Groups[1].Value, out int ln))
                        {
                            lastNoteNum = ln;
                        }
                    }
                }

                // 1. 本文段落内の注釈番号を登場順に1から連番で再採番
                if (page.BodyParagraphs != null)
                {
                    for (int i = 0; i < page.BodyParagraphs.Count; i++)
                    {
                        string original = page.BodyParagraphs[i];
                        string updated = Regex.Replace(original, @"【(?:注釈文|注)(\d+|__NEW__)】", m =>
                        {
                            return $"【注{bodyCounter++}】";
                        });

                        if (original != updated)
                        {
                            page.BodyParagraphs[i] = updated;
                            anyChanged = true;
                        }
                    }
                }

                // 2. 注釈文リスト内の注釈番号を登場順に1から連番で再採番
                if (page.Footnotes != null)
                {
                    for (int i = 0; i < page.Footnotes.Count; i++)
                    {
                        string original = page.Footnotes[i];
                        string updated = Regex.Replace(original, @"【(?:注釈文|注)(\d+|__NEW__)】", m =>
                        {
                            return $"【注釈文{footnoteCounter++}】";
                        });

                        if (original != updated)
                        {
                            page.Footnotes[i] = updated;
                            anyChanged = true;
                        }
                    }
                }
            }

            return anyChanged;
        }

        /// <summary>
        /// プロジェクト内のすべての保存済みページデータを読み込み、注釈番号を全ページ連番（または大見出し単位）で再採番してディスクに保存します。
        /// </summary>
        public static List<OcrPageData> NormalizeAndRenumberProjectFootnotes(string projectDir, string pdfName, int totalPages, AppSettings? settings = null)
        {
            var pages = LoadAllProjectPageData(projectDir, pdfName, totalPages);
            if (pages.Count == 0) return pages;

            bool changed = RenumberAllFootnotes(pages, settings);
            if (changed)
            {
                string ocrResultsDir = Path.Combine(projectDir, "ocr_results", pdfName);
                foreach (var page in pages)
                {
                    string pageDir = Path.Combine(ocrResultsDir, $"page_{page.PageNumber:0000}");
                    SavePageData(pageDir, page);
                }
            }

            return pages;
        }
    }
}
