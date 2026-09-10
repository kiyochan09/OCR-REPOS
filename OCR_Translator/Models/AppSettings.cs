using System;
using System.Drawing;

namespace OCR_Translator.Models
{
    public class AppSettings
    {
        // 1. フォント設定
        public string FontFamilyName { get; set; } = "Yu Gothic UI";
        public float FontSize { get; set; } = 11.0f;
        public bool FontBold { get; set; } = false;

        // 2. 組方向設定 ("auto": 自動, "vertical": 縦書き, "horizontal": 横書き)
        public string TextOrientation { get; set; } = "auto";

        // 3. 書籍種別 ("japanese": 和書, "western": 洋書・英欧文)
        public string DocumentType { get; set; } = "japanese";

        // 4. バッチ処理設定（1バッチあたりのページ数）
        public int BatchPageSize { get; set; } = 20;

        // 5. 段組設定 (0: 自動判定, 1: 1段組, 2: 2段組, 3: 3段組)
        public int DeckCount { get; set; } = 0;

        // 6. 本文1行文字数設定（初期値: 0 = 段落単位で改行・強制改行なし）
        public int LineCharCount { get; set; } = 0;

        // 7. Word出力設定
        public bool InsertPageBreakOnWordExport { get; set; } = false; // 改ページ挿入（初期値: false = なし）
        public bool MergeCrossPageParagraphs { get; set; } = true;     // ページ跨ぎ文章の自動結合（初期値: true = 結合）
        public string FootnoteNumberingScope { get; set; } = "continuous"; // 注釈番号の採番方式 ("continuous": 文書通し番号 [初期値], "majorHeading": 大見出し単位)

        // 8. 「最初/最後」ボタンの移動範囲設定 ("batch": 作業バッチ内, "file": ファイル全体)
        public string FirstLastNavScope { get; set; } = "batch";

        // 9. OCRレンダリング解像度（DPI: 300 [標準], 400 [超高精細], 600 [最高精細]）
        public int RenderDpi { get; set; } = 300;

        // 10. OCR終了後の表示倍率設定 ("Fit": 全体表示, "50%": 50%, "75%": 75% [初期値], "85%": 85%, "100%": 100%)
        public string PostOcrZoomRatio { get; set; } = "75%";

        // 11. 小見出しの自動認識設定 (false: しない [初期値], true: する)
        public bool AutoDetectSubheadings { get; set; } = false;

        // フォントオブジェクト取得ヘルパー
        public Font CreateFont()
        {
            try
            {
                FontStyle style = FontBold ? FontStyle.Bold : FontStyle.Regular;
                return new Font(FontFamilyName, FontSize, style);
            }
            catch
            {
                return new Font("Yu Gothic UI", 11.0f, FontStyle.Regular);
            }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                FontFamilyName = this.FontFamilyName,
                FontSize = this.FontSize,
                FontBold = this.FontBold,
                TextOrientation = this.TextOrientation,
                DocumentType = this.DocumentType,
                BatchPageSize = this.BatchPageSize,
                DeckCount = this.DeckCount,
                LineCharCount = this.LineCharCount,
                InsertPageBreakOnWordExport = this.InsertPageBreakOnWordExport,
                MergeCrossPageParagraphs = this.MergeCrossPageParagraphs,
                FootnoteNumberingScope = this.FootnoteNumberingScope,
                FirstLastNavScope = this.FirstLastNavScope,
                RenderDpi = this.RenderDpi,
                PostOcrZoomRatio = this.PostOcrZoomRatio,
                AutoDetectSubheadings = this.AutoDetectSubheadings
            };
        }
    }
}
