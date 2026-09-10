using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class OcrProcessor
    {
        public sealed class ProcessResult
        {
            public int ExitCode { get; init; }
            public string Stdout { get; init; } = "";
            public string Stderr { get; init; } = "";
        }

        public static async Task<ProcessResult> RunAutoRegionProcessAsync(
            string pythonExe,
            string autoRegionScript,
            string projectDir,
            string imagePath,
            string pageDir,
            string orientation = "auto",
            string docType = "japanese")
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

            if (!string.IsNullOrEmpty(orientation))
            {
                psi.ArgumentList.Add("--orientation");
                psi.ArgumentList.Add(orientation);
            }

            if (!string.IsNullOrEmpty(docType))
            {
                psi.ArgumentList.Add("--doc-type");
                psi.ArgumentList.Add(docType);
            }

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
                if (e.Data != null) stdout.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Exited += (_, _) => completion.TrySetResult(process.ExitCode);

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

        public static OcrRegion ConvertAutoLayoutRegion(AutoLayoutRegion source)
        {
            string type = NormalizeRegionType(source.Type);
            var r = new OcrRegion
            {
                Name = !string.IsNullOrEmpty(source.Name) ? source.Name : GetRegionDisplayName(type),
                Type = type,
                Orientation = !string.IsNullOrEmpty(source.Orientation) ? source.Orientation : "auto",
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                RuleLines = source.RuleLines.Select(l => l.Clone()).ToList()
            };
            r.EnsureRuleLines();
            return r;
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
            if (userRegions == null || userRegions.Count == 0) return "";

            int centerX = item.X + item.Width / 2;
            int centerY = item.Y + item.Height / 2;
            Rectangle itemRect = new Rectangle(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height));

            foreach (OcrRegion region in userRegions)
            {
                Rectangle regionRect = new Rectangle(region.X, region.Y, region.Width, region.Height);

                // 1. 中心点が領域内に含まれるか
                if (regionRect.Contains(centerX, centerY))
                {
                    string norm = NormalizeRegionType(region.Type);
                    if (string.IsNullOrEmpty(norm) || norm == "unclassified")
                        norm = NormalizeRegionType(region.Name);
                    return norm;
                }

                // 2. バウンディングボックスが交差しているか
                Rectangle inter = Rectangle.Intersect(regionRect, itemRect);
                if (!inter.IsEmpty && inter.Width > 0 && inter.Height > 0)
                {
                    long interArea = (long)inter.Width * inter.Height;
                    long itemArea = (long)itemRect.Width * itemRect.Height;
                    if (itemArea > 0 && (interArea * 2 >= itemArea || interArea >= 100))
                    {
                        string norm = NormalizeRegionType(region.Type);
                        if (string.IsNullOrEmpty(norm) || norm == "unclassified")
                            norm = NormalizeRegionType(region.Name);
                        return norm;
                    }
                }
            }
            return "";
        }

        public static string FindAutoLayoutRegionType(OcrDisplayItem item, List<AutoLayoutRegion> regions)
        {
            if (regions == null || regions.Count == 0) return "";

            int centerX = item.X + item.Width / 2;
            int centerY = item.Y + item.Height / 2;
            Rectangle itemRect = new Rectangle(item.X, item.Y, Math.Max(1, item.Width), Math.Max(1, item.Height));

            foreach (AutoLayoutRegion region in regions)
            {
                Rectangle regionRect = new Rectangle(region.X, region.Y, Math.Max(1, region.Width), Math.Max(1, region.Height));

                // 1. 中心点が領域内に含まれるか
                if (regionRect.Contains(centerX, centerY))
                    return NormalizeRegionType(region.Type);

                // 2. バウンディングボックスが交差しているか
                Rectangle inter = Rectangle.Intersect(regionRect, itemRect);
                if (!inter.IsEmpty && inter.Width > 0 && inter.Height > 0)
                {
                    long interArea = (long)inter.Width * inter.Height;
                    long itemArea = (long)itemRect.Width * itemRect.Height;
                    if (itemArea > 0 && (interArea * 2 >= itemArea || interArea >= 100))
                    {
                        return NormalizeRegionType(region.Type);
                    }
                }
            }
            return "";
        }

        public static string NormalizeRegionType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return "";
            string lower = type.ToLowerInvariant().Trim();
            return lower switch
            {
                "body" or "本文" or "text" => "body",
                "heading" or "見出し" or "title" or "header" => "heading",
                "footnote" or "注釈" or "注釈文" => "footnote",
                "table" or "表" or "tabular" => "table",
                "image" or "figure" or "図" or "画像" or "イラスト" or "写真" or "図版" => "image",
                _ => lower
            };
        }
    }
}
