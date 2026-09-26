using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class UserDictionaryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public static string GetUserDictionaryPath()
        {
            string engineDir = OcrProcessor.FindOcrEngineDirectory();
            string configPath = Path.Combine(engineDir, "config", "user_dictionary.json");
            if (File.Exists(configPath))
                return configPath;

            string directPath = Path.Combine(engineDir, "user_dictionary.json");
            if (File.Exists(directPath))
                return directPath;

            string configDir = Path.Combine(engineDir, "config");
            if (Directory.Exists(configDir))
                return configPath;

            return directPath;
        }

        public static List<UserDictionaryEntry> LoadEntries()
        {
            var list = new List<UserDictionaryEntry>();
            try
            {
                string path = GetUserDictionaryPath();
                if (!File.Exists(path))
                    return list;

                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // 1. exact_replacements または terms
                    if (root.TryGetProperty("exact_replacements", out var exactProp) && exactProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in exactProp.EnumerateObject())
                        {
                            list.Add(new UserDictionaryEntry
                            {
                                Original = prop.Name,
                                Replacement = prop.Value.GetString() ?? "",
                                IsRegex = false,
                                Description = ""
                            });
                        }
                    }
                    else if (root.TryGetProperty("terms", out var termsProp) && termsProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in termsProp.EnumerateObject())
                        {
                            list.Add(new UserDictionaryEntry
                            {
                                Original = prop.Name,
                                Replacement = prop.Value.GetString() ?? "",
                                IsRegex = false,
                                Description = ""
                            });
                        }
                    }
                    else if (!root.TryGetProperty("regex_replacements", out _))
                    {
                        // フラット形式 {"誤読": "正読"}
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (!prop.Name.StartsWith("_comment"))
                            {
                                list.Add(new UserDictionaryEntry
                                {
                                    Original = prop.Name,
                                    Replacement = prop.Value.GetString() ?? "",
                                    IsRegex = false,
                                    Description = ""
                                });
                            }
                        }
                    }

                    // 2. regex_replacements
                    if (root.TryGetProperty("regex_replacements", out var regexProp) && regexProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in regexProp.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                string pat = item.TryGetProperty("pattern", out var p) ? (p.GetString() ?? "") : "";
                                string rep = item.TryGetProperty("replacement", out var r) ? (r.GetString() ?? "") : "";
                                string desc = item.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
                                if (!string.IsNullOrEmpty(pat))
                                {
                                    list.Add(new UserDictionaryEntry
                                    {
                                        Original = pat,
                                        Replacement = rep,
                                        IsRegex = true,
                                        Description = desc
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load user dictionary: {ex.Message}");
            }

            return list;
        }

        public static void SaveEntries(List<UserDictionaryEntry> entries)
        {
            try
            {
                string path = GetUserDictionaryPath();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var model = new UserDictionaryJsonStructure();
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Original))
                        continue;

                    if (entry.IsRegex)
                    {
                        model.RegexReplacements.Add(new UserDictionaryRegexEntry
                        {
                            Pattern = entry.Original,
                            Replacement = entry.Replacement ?? "",
                            Description = string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description
                        });
                    }
                    else
                    {
                        model.ExactReplacements[entry.Original] = entry.Replacement ?? "";
                    }
                }

                string json = JsonSerializer.Serialize(model, JsonOptions);
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ユーザー辞書の保存に失敗しました: {ex.Message}", ex);
            }
        }

        public static string ApplyEntries(string text, IEnumerable<UserDictionaryEntry> entries)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text;

            // 1. 完全一致置換（文字数の長い順）
            var exacts = entries.Where(e => !e.IsRegex && !string.IsNullOrEmpty(e.Original))
                                .OrderByDescending(e => e.Original.Length);
            foreach (var e in exacts)
            {
                result = result.Replace(e.Original, e.Replacement);
            }

            // 2. 正規表現置換
            var regexes = entries.Where(e => e.IsRegex && !string.IsNullOrEmpty(e.Original));
            foreach (var e in regexes)
            {
                try
                {
                    result = Regex.Replace(result, e.Original, e.Replacement ?? "");
                }
                catch
                {
                    // 不正な正規表現は無視
                }
            }

            return result;
        }
    }
}
