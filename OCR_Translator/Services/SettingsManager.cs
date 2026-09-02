using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class SettingsManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string GetSettingsFilePath()
        {
            return Path.Combine(Application.StartupPath, "app_settings.json");
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // エラー時はデフォルト設定を使用
            }

            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                string path = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
