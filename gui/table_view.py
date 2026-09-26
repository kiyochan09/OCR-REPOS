import tkinter as tk
from tkinter import ttk, messagebox, filedialog
import csv
import io

class TableView(ttk.Frame):
    def __init__(self, parent, *args, **kwargs):
        super().__init__(parent, *args, **kwargs)
        self.data = [["", "", ""], ["", "", ""], ["", "", ""]]
        self.entry_grid = []
        self._create_widgets()

    def _create_widgets(self):
        # Toolbar
        tb = ttk.Frame(self)
        tb.pack(fill=tk.X, padx=4, pady=4)

        ttk.Button(tb, text="➕ 行追加", command=self.add_row, width=9).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="➖ 行削除", command=self.delete_row, width=9).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="➕ 列追加", command=self.add_col, width=9).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="➖ 列削除", command=self.delete_col, width=9).pack(side=tk.LEFT, padx=2)
        
        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)
        
        ttk.Button(tb, text="📋 TSVコピー (Excel貼付用)", command=self.copy_tsv).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="💾 CSV保存...", command=self.export_csv).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="🗑️ クリア", command=self.clear_table, width=8).pack(side=tk.RIGHT, padx=2)

        # Scrollable grid container
        self.canvas = tk.Canvas(self, bg="#FFFFFF", highlightthickness=0)
        self.v_scrollbar = ttk.Scrollbar(self, orient=tk.VERTICAL, command=self.canvas.yview)
        self.h_scrollbar = ttk.Scrollbar(self, orient=tk.HORIZONTAL, command=self.canvas.xview)
        self.canvas.configure(xscrollcommand=self.h_scrollbar.set, yscrollcommand=self.v_scrollbar.set)

        self.grid_frame = ttk.Frame(self.canvas)
        self.grid_frame.bind("<Configure>", lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.canvas_win = self.canvas.create_window((0, 0), window=self.grid_frame, anchor="nw")

        self.v_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.h_scrollbar.pack(side=tk.BOTTOM, fill=tk.X)
        self.canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.refresh_grid()

    def set_data(self, data):
        if not data:
            self.data = [["", "", ""], ["", "", ""], ["", "", ""]]
        else:
            self.data = [[str(cell) for cell in row] for row in data]
        self.refresh_grid()

    def get_data(self):
        # Sync values from entry grid
        result = []
        for row_entries in self.entry_grid:
            row_vals = [e.get() for e in row_entries]
            result.append(row_vals)
        return result

    def refresh_grid(self):
        for widget in self.grid_frame.winfo_children():
            widget.destroy()
        self.entry_grid = []

        if not self.data:
            self.data = [[""]]

        num_rows = len(self.data)
        num_cols = max(len(row) for row in self.data) if num_rows > 0 else 1

        for r in range(num_rows):
            row_entries = []
            # Row header
            lbl = ttk.Label(self.grid_frame, text=f"{r+1}", width=4, anchor="center", background="#EAEAEA")
            lbl.grid(row=r+1, column=0, padx=1, pady=1, sticky="nsew")
            
            for c in range(num_cols):
                val = self.data[r][c] if c < len(self.data[r]) else ""
                entry = ttk.Entry(self.grid_frame, width=16)
                entry.insert(0, val)
                entry.grid(row=r+1, column=c+1, padx=1, pady=1, sticky="nsew")
                row_entries.append(entry)
            self.entry_grid.append(row_entries)

        # Col headers
        for c in range(num_cols):
            col_letter = chr(65 + c) if c < 26 else f"Col{c+1}"
            clbl = ttk.Label(self.grid_frame, text=col_letter, anchor="center", background="#EAEAEA")
            clbl.grid(row=0, column=c+1, padx=1, pady=1, sticky="nsew")

    def add_row(self):
        data = self.get_data()
        cols = len(data[0]) if data else 3
        data.append([""] * cols)
        self.data = data
        self.refresh_grid()

    def delete_row(self):
        data = self.get_data()
        if len(data) > 1:
            data.pop()
            self.data = data
            self.refresh_grid()

    def add_col(self):
        data = self.get_data()
        for row in data:
            row.append("")
        self.data = data
        self.refresh_grid()

    def delete_col(self):
        data = self.get_data()
        if data and len(data[0]) > 1:
            for row in data:
                row.pop()
            self.data = data
            self.refresh_grid()

    def clear_table(self):
        self.set_data([["", "", ""], ["", "", ""], ["", "", ""]])

    def copy_tsv(self):
        data = self.get_data()
        lines = []
        for row in data:
            lines.append("\t".join(row))
        tsv_text = "\n".join(lines)
        self.clipboard_clear()
        self.clipboard_append(tsv_text)
        messagebox.showinfo("コピー完了", "表データをTSV形式（Excel/Word貼り付け用）でクリップボードにコピーしました。")

    def export_csv(self):
        data = self.get_data()
        path = filedialog.asksaveasfilename(defaultextension=".csv", filetypes=[("CSV Files", "*.csv")])
        if path:
            try:
                with open(path, "w", newline="", encoding="utf-8-sig") as f:
                    writer = csv.writer(f)
                    writer.writerows(data)
                messagebox.showinfo("保存完了", f"CSVファイルを保存しました:\n{path}")
            except Exception as e:
                messagebox.showerror("エラー", f"CSV保存に失敗しました:\n{e}")
