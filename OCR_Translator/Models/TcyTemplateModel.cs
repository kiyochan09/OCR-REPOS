using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;

namespace OCR_Translator.Models
{
    public class TcyTemplateEntry
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("primary_image")]
        public string PrimaryImage { get; set; } = "";

        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = new();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonIgnore]
        public Image? Thumbnail { get; set; }

        public TcyTemplateEntry Clone()
        {
            return new TcyTemplateEntry
            {
                Value = this.Value,
                Enabled = this.Enabled,
                PrimaryImage = this.PrimaryImage,
                Images = new List<string>(this.Images),
                Description = this.Description,
                Thumbnail = this.Thumbnail
            };
        }
    }

    public class TcyRegistryStructure
    {
        [JsonPropertyName("_comment")]
        public string? Comment { get; set; } = "縦中横（10〜99）数字・画像登録辞書。各数値に対応する基準画像ファイルを登録できます。";

        [JsonPropertyName("templates")]
        public Dictionary<string, TcyTemplateEntry> Templates { get; set; } = new();
    }
}
