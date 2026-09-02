using System;
using System.Collections.Generic;
using OCR_Translator.Services;

namespace OCR_Translator.Models
{
    /// <summary>
    /// 1ページあたりのOCR抽出データ（見出し、本文段落、構造化表、図画像、注釈）
    /// Word出力時にページ順・自然な文書フローで配置するために使用します。
    /// </summary>
    public class OcrPageData
    {
        public int PageNumber { get; set; }
        public List<string> Headings { get; set; } = new();
        public List<string> BodyParagraphs { get; set; } = new();
        public List<StructuredTable> Tables { get; set; } = new();
        public List<FigureItem> Figures { get; set; } = new();
        public List<string> Footnotes { get; set; } = new();
    }
}
