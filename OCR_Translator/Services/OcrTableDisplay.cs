using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    /// <summary>
    /// OCR結果の表表示（DataGridView構築）を行う。
    /// </summary>
    public static class OcrTableDisplay
    {
        // =========================================================
        // OCR結果一覧（簡易表）
        // =========================================================

        public static void DisplayOcrTable(DataGridView? dgvOcrTable, List<OcrDisplayItem> tableItems)
        {
            if (dgvOcrTable == null)
                return;

            dgvOcrTable.Columns.Clear();
            dgvOcrTable.Rows.Clear();

            if (tableItems.Count == 0)
                return;

            dgvOcrTable.Columns.Add("Index", "No.");
            dgvOcrTable.Columns.Add("Text", "OCR結果");

            for (int i = 0; i < tableItems.Count; i++)
            {
                dgvOcrTable.Rows.Add(i + 1, tableItems[i].Text);
            }

            dgvOcrTable.Columns["Index"]!.FillWeight = 15;
            dgvOcrTable.Columns["Text"]!.FillWeight = 85;
        }

        // =========================================================
        // 自動検出された表をタブ化して表示
        // =========================================================

        public static void DisplayDetectedTables(TabPage? tabOcrTable, List<AutoLayoutRegion> autoRegions)
        {
            if (tabOcrTable == null)
                return;

            tabOcrTable.Controls.Clear();

            TabControl tableTabs = new TabControl { Dock = DockStyle.Fill };

            List<AutoLayoutRegion> tables = autoRegions
                .Where(r => string.Equals(r.Type, "table", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tables.Count == 0)
            {
                Label label = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "検出された表はありません。",
                    TextAlign = ContentAlignment.MiddleCenter
                };
                tabOcrTable.Controls.Add(label);
                return;
            }

            for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                AutoLayoutRegion table = tables[tableIndex];
                TabPage page = new TabPage(
                    string.IsNullOrWhiteSpace(table.Name) ? $"表{tableIndex + 1}" : table.Name);

                DataGridView grid = CreateTableGrid(table);
                page.Controls.Add(grid);
                tableTabs.TabPages.Add(page);
            }

            tabOcrTable.Controls.Add(tableTabs);
        }

        // =========================================================
        // DataGridView 構築
        // =========================================================

        public static DataGridView CreateTableGrid(AutoLayoutRegion table)
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };

            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            int rows = table.Rows;
            int columns = table.Columns;

            if (rows <= 0 || columns <= 0)
            {
                if (table.Cells.Count > 0)
                {
                    rows = table.Cells.Max(c => c.Row);
                    columns = table.Cells.Max(c => c.Column);
                }
            }

            if (rows <= 0 || columns <= 0)
                return grid;

            for (int column = 1; column <= columns; column++)
            {
                grid.Columns.Add($"Column{column}", $"列{column}");
            }

            grid.Rows.Add(rows);

            foreach (AutoLayoutCell cell in table.Cells)
            {
                int rowIndex = cell.Row - 1;
                int columnIndex = cell.Column - 1;

                if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
                    continue;
                if (columnIndex < 0 || columnIndex >= grid.Columns.Count)
                    continue;

                grid.Rows[rowIndex].Cells[columnIndex].Value = cell.Text;
            }

            return grid;
        }
    }
}
