using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// ページ領域設定の保存・読込・クローン・比較
    /// </summary>
    public class LayoutStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        // =========================================================
        // Clone
        // =========================================================

        public OcrRegion CloneRegion(OcrRegion source)
        {
            return new OcrRegion
            {
                Name = source.Name,
                Type = source.Type,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height
            };
        }

        public List<OcrRegion> CloneRegions(IEnumerable<OcrRegion> source)
        {
            return source.Select(CloneRegion).ToList();
        }

        // =========================================================
        // 比較
        // =========================================================

        public bool AreRegionsEqual(
            List<OcrRegion> regions1,
            List<OcrRegion> regions2)
        {
            if (regions1.Count != regions2.Count)
                return false;

            for (int i = 0; i < regions1.Count; i++)
            {
                OcrRegion a = regions1[i];
                OcrRegion b = regions2[i];

                if (a.X != b.X ||
                    a.Y != b.Y ||
                    a.Width != b.Width ||
                    a.Height != b.Height ||
                    a.Type != b.Type)
                {
                    return false;
                }
            }

            return true;
        }

        // =========================================================
        // ページ単位の保存判定（旧 SaveCurrentPageRegions のデータ部分）
        // =========================================================

        /// <summary>
        /// 現在ページの regions を pageRegions に保存するか判定し、必要なら保存する。
        /// </summary>
        /// <returns>保存した場合 true</returns>
        public bool TrySaveCurrentPageRegions(
            int currentPage,
            List<OcrRegion> regions,
            Dictionary<int, List<OcrRegion>> pageRegions,
            Dictionary<int, List<OcrRegion>> autoPageRegions)
        {
            // すでにユーザー補正済みなら上書き保存
            if (pageRegions.ContainsKey(currentPage))
            {
                pageRegions[currentPage] = CloneRegions(regions);
                return true;
            }

            // 自動判定と同じなら「未補正」とみなし保存しない
            if (autoPageRegions.TryGetValue(
                    currentPage,
                    out List<OcrRegion>? autoRegions))
            {
                if (AreRegionsEqual(regions, autoRegions))
                {
                    return false;
                }
            }

            // 自動判定と違う → ユーザー補正として保存
            pageRegions[currentPage] = CloneRegions(regions);
            return true;
        }

        /// <summary>
        /// 強制保存（削除・更新ボタンなど、明示操作時）
        /// </summary>
        public void ForceSavePageRegions(
            int currentPage,
            List<OcrRegion> regions,
            Dictionary<int, List<OcrRegion>> pageRegions)
        {
            pageRegions[currentPage] = CloneRegions(regions);
        }

        // =========================================================
        // ページ読込（旧 LoadCurrentPageRegions のデータ部分）
        // =========================================================

        /// <summary>
        /// ユーザー設定 → 自動判定の優先順で領域リストを返す
        /// </summary>
        public List<OcrRegion> LoadPageRegions(
            int currentPage,
            Dictionary<int, List<OcrRegion>> pageRegions,
            Dictionary<int, List<OcrRegion>> autoPageRegions)
        {
            if (pageRegions.TryGetValue(
                    currentPage,
                    out List<OcrRegion>? savedRegions))
            {
                return CloneRegions(savedRegions);
            }

            if (autoPageRegions.TryGetValue(
                    currentPage,
                    out List<OcrRegion>? autoRegions))
            {
                return CloneRegions(autoRegions);
            }

            return new List<OcrRegion>();
        }

        // =========================================================
        // JSON 保存 / 読込
        // =========================================================

        public PageLayout BuildPageLayout(
            Dictionary<int, List<OcrRegion>> pageRegions,
            string templateName = "縦書き本文")
        {
            var layout = new PageLayout();
            layout.Template.Name = templateName;
            layout.Template.Regions = new List<OcrRegion>();

            foreach (KeyValuePair<int, List<OcrRegion>> pair in pageRegions)
            {
                // 既存仕様: キーは 1 始まりの文字列
                string pageKey = (pair.Key + 1).ToString();

                layout.Pages[pageKey] = new PageSettings
                {
                    UseTemplate = false,
                    Regions = CloneRegions(pair.Value)
                };
            }

            return layout;
        }

        public void SaveToJsonFile(PageLayout layout, string path)
        {
            string json = JsonSerializer.Serialize(layout, JsonOptions);
            File.WriteAllText(path, json);
        }

        public PageLayout? LoadFromJsonFile(string path)
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PageLayout>(json, JsonOptions);
        }

        /// <summary>
        /// JSONの PageLayout を pageRegions（0始まりキー）に展開
        /// </summary>
        public Dictionary<int, List<OcrRegion>> ToPageRegionsDictionary(
            PageLayout layout)
        {
            var result = new Dictionary<int, List<OcrRegion>>();

            foreach (KeyValuePair<string, PageSettings> pair in layout.Pages)
            {
                if (!int.TryParse(pair.Key, out int pageNumber1Based))
                    continue;

                int pageIndex0Based = pageNumber1Based - 1;
                result[pageIndex0Based] = CloneRegions(pair.Value.Regions);
            }

            return result;
        }
    }
}