using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OCR_Translator.Models
{
    public class UserDictionaryEntry
    {
        public string Original { get; set; } = "";
        public string Replacement { get; set; } = "";
        public bool IsRegex { get; set; } = false;
        public string Description { get; set; } = "";

        public UserDictionaryEntry Clone()
        {
            return new UserDictionaryEntry
            {
                Original = this.Original,
                Replacement = this.Replacement,
                IsRegex = this.IsRegex,
                Description = this.Description
            };
        }
    }

    public class UserDictionaryJsonStructure
    {
        [JsonPropertyName("_comment")]
        public string? Comment { get; set; } = "ユーザー定義OCR補正辞書。ここに登録した用語・正規表現はOCR認識結果に自動適用されます。";

        [JsonPropertyName("exact_replacements")]
        public Dictionary<string, string> ExactReplacements { get; set; } = new();

        [JsonPropertyName("regex_replacements")]
        public List<UserDictionaryRegexEntry> RegexReplacements { get; set; } = new();
    }

    public class UserDictionaryRegexEntry
    {
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = "";

        [JsonPropertyName("replacement")]
        public string Replacement { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
