using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// NDLOCR-Lite の実行（単ページ・全ページバッチ）を行う。
    /// UI 更新は IProgress / Action コールバックで委譲する。
    /// </summary>
    public static class OcrProcessor
    {
        public sealed class ProcessResult
        {
            public int ExitCode { get; init; }
            public string Stdout { get; init; } = "";
            public string Stderr { get; init; } = "";
        }

        // =========================================================
        // Python プロセス実行
        // =========================================================

        public static async Task<ProcessResult> RunAutoRegionProcessAsync(
            string pythonExe,
            string autoRegionScript,
            string projectDir,
            string imagePath,
            string pageDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.Environment["PYTHONUTF8"] = "1";
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            psi.ArgumentList.Add(autoRegionScript);
            psi.ArgumentList.Add(imagePath);
            psi.ArgumentList.Add(pageDir);

            using var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stdout.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    stderr.AppendLine(e.Data);
            };

            process.Exited += (_, _) =>
                completion.TrySetResult(process.ExitCode);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int exitCode = await completion.Task;

            return new ProcessResult
            {
                ExitCode = exitCode,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            };
        }

        // =========================================================
        // エンジン探索
        // =========================================================

        public static string FindOcrEngineDirectory()
        {
            string? dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                string candidate = Path.Combine(dir, "ocr_engine");
                if (File.Exists(Path.Combine(candidate, "ndlocr_auto_region.py")))
                    return candidate;
                DirectoryInfo? parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            string fallback = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "ocr_engine"));
            return fallback;
        }

        // =========================================================
        // 領域変換・分類
        // =========================================================

        public static OcrRegion ConvertAutoLayoutRegion(AutoLayoutRegion source)
        {
            string type = NormalizeRegionType(source.Type);

            return new OcrRegion
            {
                Name = GetRegionDisplayName(type),
                Type = type,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
        }

        public static string GetRegionDisplayName(string type)
        {
            return type switch
            {
                "body" => "本文",
                "heading" => "見出し",
                "footnote" => "注釈文",
                "table" => "表",
                "image" => "図",
                _ => "未分類"
            };
        }

        public static string FindUserRegionType(OcrDisplayItem item, List<OcrRegion> userRegions)
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

        public static string FindAutoLayoutRegionType(OcrDisplayItem item, List<AutoLayoutRegion> regions)
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
            return type switch
            {
                "header" or "footer" or "map" or "ignore" => "body",
                _ => type
            };
        }
    }
}
