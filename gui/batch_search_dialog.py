import tkinter as tk
from tkinter import ttk, messagebox
import re

class BatchSearchDialog(tk.Toplevel):
    def __init__(self, parent, pages_cache, current_page=1, on_jump=None):
        super().__init__(parent)
        self.title("🔍 バッチ全体・全文検索")
        self.geometry("720x480")
        self.minsize(560, 360)
        self.pages_cache = pages_cache or {}
        self.current_page = current_page
        self.on_jump = on_jump
        self.transient(parent)
        self.grab_set()

        self._create_widgets()
        self.keyword_entry.focus_set()

    def _create_widgets(self):
        # Search input frame
        top_frame = ttk.LabelFrame(self, text="検索条件", padding=10)
        top_frame.pack(fill=tk.X, padx=10, pady=8)

        lbl = ttk.Label(top_frame, text="検索文字列:")
        lbl.grid(row=0, column=0, padx=4, pady=4, sticky="w")

        self.keyword_var = tk.StringVar()
        self.keyword_entry = ttk.Entry(top_frame, textvariable=self.keyword_var, width=32)
        self.keyword_entry.grid(row=0, column=1, padx=4, pady=4, sticky="ew")
        self.keyword_entry.bind("<Return>", lambda e: self.do_search())

        self.case_var = tk.BooleanVar(value=False)
        ttk.Checkbutton(top_frame, text="大文字・小文字を区別", variable=self.case_var).grid(row=0, column=2, padx=8, pady=4)

        self.regex_var = tk.BooleanVar(value=False)
        ttk.Checkbutton(top_frame, text="正規表現を使用", variable=self.regex_var).grid(row=0, column=3, padx=8, pady=4)

        search_btn = ttk.Button(top_frame, text="🔍 検索実行", command=self.do_search)
        search_btn.grid(row=0, column=4, padx=6, pady=4)

        top_frame.columnconfigure(1, weight=1)

        # Results frame
        res_frame = ttk.LabelFrame(self, text="検索結果", padding=6)
        res_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=4)

        cols = ("page", "type", "snippet")
        self.tree = ttk.Treeview(res_frame, columns=cols, show="headings", selectmode="browse")
        self.tree.heading("page", text="ページ")
        self.tree.heading("type", text="種別")
        self.tree.heading("snippet", text="該当テキスト")

        self.tree.column("page", width=70, anchor="center")
        self.tree.column("type", width=80, anchor="center")
        self.tree.column("snippet", width=480, anchor="w")

        scrollbar = ttk.Scrollbar(res_frame, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.tree.bind("<Double-1>", lambda e: self.jump_selected())

        # Bottom action bar
        bottom_frame = ttk.Frame(self)
        bottom_frame.pack(fill=tk.X, padx=10, pady=8)

        self.status_lbl = ttk.Label(bottom_frame, text="検索キーワードを入力して「検索実行」を押してください。")
        self.status_lbl.pack(side=tk.LEFT, padx=4)

        ttk.Button(bottom_frame, text="閉じる", command=self.destroy, width=10).pack(side=tk.RIGHT, padx=4)
        ttk.Button(bottom_frame, text="🎯 該当ページへジャンプ", command=self.jump_selected).pack(side=tk.RIGHT, padx=4)

    def do_search(self):
        kw = self.keyword_var.get().strip()
        if not kw:
            messagebox.showwarning("警告", "検索キーワードを入力してください。")
            return

        for item in self.tree.get_children():
            self.tree.delete(item)

        is_regex = self.regex_var.get()
        case_sensitive = self.case_var.get()
        flags = 0 if case_sensitive else re.IGNORECASE

        match_count = 0
        sorted_pages = sorted(self.pages_cache.keys())

        for p_num in sorted_pages:
            p_data = self.pages_cache[p_num]
            
            # Headings
            for h in p_data.get("headings", []):
                if self._check_match(kw, h, is_regex, flags):
                    self.tree.insert("", "end", values=(f"P.{p_num}", "見出し", h.strip()), tags=(str(p_num),))
                    match_count += 1

            # Body lines
            body = p_data.get("body_text", "")
            for line in body.split("\n"):
                if self._check_match(kw, line, is_regex, flags):
                    self.tree.insert("", "end", values=(f"P.{p_num}", "本文", line.strip()), tags=(str(p_num),))
                    match_count += 1

            # Annotations
            for anno in p_data.get("annotations", []):
                if self._check_match(kw, anno, is_regex, flags):
                    self.tree.insert("", "end", values=(f"P.{p_num}", "注釈", anno.strip()), tags=(str(p_num),))
                    match_count += 1

        self.status_lbl.config(text=f"検索結果: {match_count} 件の一致が見つかりました。")

    def _check_match(self, kw, text, is_regex, flags):
        if not text:
            return False
        if is_regex:
            try:
                return re.search(kw, text, flags=flags) is not None
            except Exception:
                return False
        else:
            if flags == re.IGNORECASE:
                return kw.lower() in text.lower()
            return kw in text

    def jump_selected(self):
        sel = self.tree.selection()
        if not sel:
            messagebox.showinfo("情報", "ジャンプ先の検索結果を選択してください。")
            return
        item = self.tree.item(sel[0])
        tags = item.get("tags", ())
        if tags and self.on_jump:
            try:
                page_num = int(tags[0])
                kw = self.keyword_var.get().strip()
                self.on_jump(page_num, kw)
                self.destroy()
            except Exception as e:
                print(f"[BatchSearchDialog] Jump error: {e}")
