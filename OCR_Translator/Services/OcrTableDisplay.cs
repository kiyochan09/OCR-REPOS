using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OCR_Translator.Models;

namespace OCR_Translator.Services
{
    public static class OcrTableDisplay
    {
        public static void DisplayOcrTable(DataGridView? dgvOcrTable, List<OcrDisplayItem> tableItems)
        {
            if (dgvOcrTable == null) return;

            dgvOcrTable.Columns.Clear();
            dgvOcrTable.Rows.Clear();

            if (tableItems.Count == 0) return;

            dgvOcrTable.Columns.Add("Index", "No.");
            dgvOcrTable.Columns.Add("Text", "OCR結果");

            for (int i = 0; i < tableItems.Count; i++)
                dgvOcrTable.Rows.Add(i + 1, tableItems[i].Text);

            dgvOcrTable.Columns["Index"]!.FillWeight = 15;
            dgvOcrTable.Columns["Text"]!.FillWeight = 85;
        }

        public static void DisplayDetectedTables(TabPage? tabOcrTable, List<AutoLayoutRegion> autoRegions)
        {
            if (tabOcrTable == null) return;

            tabOcrTable.Controls.Clear();
            TabControl tableTabs = new TabControl { Dock = DockStyle.Fill };

            List<AutoLayoutRegion> tables = autoRegions
                .Where(r => string.Equals(r.Type, "table", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (tables.Count == 0)
            {
                tabOcrTable.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "検出された表はありません。",
                    TextAlign = ContentAlignment.MiddleCenter
                });
                return;
            }

            for (int i = 0; i < tables.Count; i++)
            {
                AutoLayoutRegion table = tables[i];
                TabPage page = new TabPage(
                    string.IsNullOrWhiteSpace(table.Name) ? $"表{i + 1}" : table.Name);
                page.Controls.Add(CreateTableGrid(table));
                tableTabs.TabPages.Add(page);
            }

            tabOcrTable.Controls.Add(tableTabs);
        }

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

            if (rows <= 0 || columns <= 0) return grid;

            for (int col = 1; col <= columns; col++)
                grid.Columns.Add($"Column{col}", $"列{col}");

            grid.Rows.Add(rows);

            foreach (AutoLayoutCell cell in table.Cells)
            {
                int rowIndex = cell.Row - 1;
                int colIndex = cell.Column - 1;
                if (rowIndex >= 0 && rowIndex < grid.Rows.Count &&
                    colIndex >= 0 && colIndex < grid.Columns.Count)
                {
                    grid.Rows[rowIndex].Cells[colIndex].Value = cell.Text;
                }
            }

            return grid;
        }
    }
}
