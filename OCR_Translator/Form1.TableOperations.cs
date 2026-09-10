using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Forms;
using OCR_Translator.Models;
using OCR_Translator.Services;

namespace OCR_Translator
{
    public partial class Form1
    {
        /// <summary>
        /// 表の連結ダイアログを開き、複数領域・複数ページまたぎの表を1つに統合します。
        /// </summary>
        public void OpenTableConcatenateDialog()
        {
            if (dgvOcrTable == null || dgvOcrTable.RowCount == 0)
            {
                MessageBox.Show("連結可能な表データがありません。\n先にOCRを実行して表を読み込んでください。", "表の連結", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // dgvOcrTable から全テーブルを走査
            var allTables = TableCellMerger.ScanTablesFromDataGridView(dgvOcrTable);
            if (allTables.Count < 2)
            {
                MessageBox.Show("連結には2つ以上の表が必要です。\n現在、表は1つのみ（または0件）検出されています。", "表の連結", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 現在選択されているセルから初期選択テーブルを特定
            var preSelectedTables = new List<TableGridItemInfo>();
            if (dgvOcrTable.SelectedCells.Count > 0)
            {
                var selectedRowIndices = new HashSet<int>();
                foreach (DataGridViewCell cell in dgvOcrTable.SelectedCells)
                {
                    selectedRowIndices.Add(cell.RowIndex);
                }

                foreach (var tbl in allTables)
                {
                    int tblEnd = tbl.StartRowIndex + tbl.RowCount - 1;
                    if (selectedRowIndices.Any(r => r >= tbl.StartRowIndex && r <= tblEnd))
                    {
                        preSelectedTables.Add(tbl);
                    }
                }
            }

            using var form = new TableConcatenateForm(allTables, preSelectedTables);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                bool success = TableCellMerger.ConcatenateTables(
                    dgvOcrTable,
                    tableMergeSpans,
                    form.SelectedTables,
                    form.IsVertical,
                    form.SkipDuplicateHeader,
                    form.NewTableName);

                if (success)
                {
                    SaveCurrentPageData();
                    txtLog.AppendText($"【表連結】{form.SelectedTables.Count}個の表を「{form.NewTableName}」として連結しました。" + Environment.NewLine);
                    MessageBox.Show(
                        $"表を「{form.NewTableName}」として正常に連結しました。\n「表」タブおよびWord出力へ即座に反映されます。",
                        "表の連結完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// 現在のバッチ範囲の保存済み page_data.json をディスクから直接再読み込みし、
        /// メモリ上のキャッシュおよび表グリッド（dgvOcrTable）を最新状態に更新します。
        /// </summary>
        public void ReloadCurrentPageTableFromDisk()
        {
            if (string.IsNullOrEmpty(currentPdfPath)) return;
            string pdfName = System.IO.Path.GetFileNameWithoutExtension(currentPdfPath);
            string projectDir = OcrProcessor.FindOcrEngineDirectory();
            string ocrResultsDir = System.IO.Path.Combine(projectDir, "ocr_results", pdfName);

            int startP = (int)numPageStart.Value;
            int endP = (int)numPageEnd.Value;

            int reloadedPages = 0;
            int tableCount = 0;
            for (int p = startP; p <= endP; p++)
            {
                string pageDir = System.IO.Path.Combine(ocrResultsDir, $"page_{p:0000}");
                var freshData = OcrPageDataService.LoadPageData(pageDir);
                if (freshData != null)
                {
                    freshData.PageNumber = p;
                    ocrPageDataList.RemoveAll(x => x.PageNumber == p);
                    ocrPageDataList.Add(freshData);
                    reloadedPages++;
                    tableCount += freshData.Tables?.Count ?? 0;
                }
            }

            RefreshTableDataGridFromCurrentBatch();
            txtLog.AppendText($"【表データ再読込】ディスクから P.{startP}〜{endP} の表データを再読み込みしました（{tableCount}個の表）。" + Environment.NewLine);
            MessageBox.Show(
                $"ディスクの page_data.json から最新の表データを再読み込みしました。\n（対象: P.{startP}〜{endP}、計{tableCount}個の表）",
                "表データ再読込", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
