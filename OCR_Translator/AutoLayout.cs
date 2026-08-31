using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using OCR_Translator.Models;

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
}
