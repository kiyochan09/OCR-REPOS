using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCR_Translator
{
    public class AutoLayout
    {
        [JsonPropertyName("image")]
        public string Image { get; set; } = "";

        [JsonPropertyName("image_width")]
        public int ImageWidth { get; set; }

        [JsonPropertyName("image_height")]
        public int ImageHeight { get; set; }

        [JsonPropertyName("regions")]
        public List<AutoLayoutRegion> Regions { get; set; } = new();
    }

    public class AutoLayoutRegion
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("orientation")]
        public string Orientation { get; set; } = "";

        [JsonPropertyName("ocr_count")]
        public int OcrCount { get; set; }
    }
}