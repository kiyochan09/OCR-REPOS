import os
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from .settings_manager import SettingsManager
from .services import UserDictService, TcyRegistryService
from .user_dict_dialog import UserDictDialog

class OptionDialog(tk.Toplevel):
    def __init__(self, parent, settings_mgr: SettingsManager, on_apply=None):
        super().__init__(parent)
        self.title("⚙️ オプション設定")
        self.geometry("640x520")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        self.settings_mgr = settings_mgr
        self.on_apply = on_apply
        self.dict_service = UserDictService()
        self.tcy_service = TcyRegistryService()

        self._create_widgets()

    def _create_widgets(self):
        self.notebook = ttk.Notebook(self)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # Tab 1: Font & Display
        self.tab_font = ttk.Frame(self.notebook, padding=12)
        self.notebook.add(self.tab_font, text="  🔤 フォント・表示  ")
        self._build_font_tab()

        # Tab 2: OCR & Recognition
        self.tab_ocr = ttk.Frame(self.notebook, padding=12)
        self.notebook.add(self.tab_ocr, text="  🔍 OCR・認識  ")
        self._build_ocr_tab()

        # Tab 3: Output & Format
        self.tab_output = ttk.Frame(self.notebook, padding=12)
        self.notebook.add(self.tab_output, text="  📄 出力・書式  ")
        self._build_output_tab()

        # Tab 4: Dictionary & Replacements
        self.tab_dict = ttk.Frame(self.notebook, padding=12)
        self.notebook.add(self.tab_dict, text="  📖 補正辞書・置換  ")
        self._build_dict_tab()

        # Bottom buttons
        btn_bar = ttk.Frame(self, padding=8)
        btn_bar.pack(fill=tk.X)

        ttk.Button(btn_bar, text="初期設定に戻す", command=self.reset_defaults).pack(side=tk.LEFT, padx=4)
        ttk.Button(btn_bar, text="キャンセル", command=self.destroy, width=10).pack(side=tk.RIGHT, padx=4)
        ttk.Button(btn_bar, text="OK", command=self.save_and_close, width=10).pack(side=tk.RIGHT, padx=4)

    # -------------------------------------------------------------
    # TAB 1: Font & Display
    # -------------------------------------------------------------
    def _build_font_tab(self):
        f = self.tab_font

        # Font Family
        ttk.Label(f, text="エディタ表示フォント:").grid(row=0, column=0, sticky="w", pady=6)
        fonts = ["Meiryo UI", "MS UI Gothic", "MS Gothic", "MS Mincho", "Yu Gothic UI", "Segoe UI", "Arial"]
        self.font_family_var = tk.StringVar(value=self.settings_mgr.get("font_family", "Meiryo UI"))
        self.font_family_combo = ttk.Combobox(f, textvariable=self.font_family_var, values=fonts, state="readonly", width=24)
        self.font_family_combo.grid(row=0, column=1, sticky="w", pady=6, padx=8)

        # Font Size
        ttk.Label(f, text="フォントサイズ (pt):").grid(row=1, column=0, sticky="w", pady=6)
        self.font_size_var = tk.IntVar(value=self.settings_mgr.get("font_size", 12))
        self.font_size_spin = ttk.Spinbox(f, from_=8, to=36, textvariable=self.font_size_var, width=8)
        self.font_size_spin.grid(row=1, column=1, sticky="w", pady=6, padx=8)

        # Bold
        self.font_bold_var = tk.BooleanVar(value=self.settings_mgr.get("font_bold", False))
        ttk.Checkbutton(f, text="太字 (Bold) で表示", variable=self.font_bold_var).grid(row=2, column=1, sticky="w", pady=6, padx=8)

        # OCR Zoom
        ttk.Label(f, text="OCR完了時の自動倍率:").grid(row=3, column=0, sticky="w", pady=6)
        zooms = ["Fit", "50%", "75%", "100%", "125%", "150%", "200%"]
        self.ocr_zoom_var = tk.StringVar(value=self.settings_mgr.get("ocr_zoom", "Fit"))
        self.ocr_zoom_combo = ttk.Combobox(f, textvariable=self.ocr_zoom_var, values=zooms, state="readonly", width=12)
        self.ocr_zoom_combo.grid(row=3, column=1, sticky="w", pady=6, padx=8)

        # Font Preview Frame
        p_frame = ttk.LabelFrame(f, text="フォントプレビュー", padding=8)
        p_frame.grid(row=4, column=0, columnspan=2, sticky="nsew", pady=14)
        self.preview_lbl = tk.Label(p_frame, text="2019年2月27日 サプライチェーン再構築の見通し\n吾輩は猫である。名前はまだ無い。13世紀の歴史。", bg="#FFFFFF", relief="sunken", bd=1, height=3)
        self.preview_lbl.pack(fill=tk.BOTH, expand=True)

        self.font_family_combo.bind("<<ComboboxSelected>>", lambda e: self._update_font_preview())
        self.font_size_spin.bind("<KeyRelease>", lambda e: self._update_font_preview())
        self.font_size_spin.bind("<<Increment>>", lambda e: self._update_font_preview())
        self.font_size_spin.bind("<<Decrement>>", lambda e: self._update_font_preview())
        self._update_font_preview()

    def _update_font_preview(self):
        try:
            fn = self.font_family_var.get()
            sz = int(self.font_size_var.get())
            weight = "bold" if self.font_bold_var.get() else "normal"
            self.preview_lbl.config(font=(fn, sz, weight))
        except Exception:
            pass

    # -------------------------------------------------------------
    # TAB 2: OCR & Recognition
    # -------------------------------------------------------------
    def _build_ocr_tab(self):
        f = self.tab_ocr

        # Orientation
        ttk.Label(f, text="組方向優先度:").grid(row=0, column=0, sticky="w", pady=6)
        orients = [("自動判定", "auto"), ("縦書き優先 (和書・新書)", "vertical"), ("横書き優先 (洋書・論文)", "horizontal")]
        self.orient_var = tk.StringVar(value=self.settings_mgr.get("ocr_orientation", "auto"))
        o_frame = ttk.Frame(f)
        o_frame.grid(row=0, column=1, sticky="w", pady=6)
        for label, val in orients:
            ttk.Radiobutton(o_frame, text=label, variable=self.orient_var, value=val).pack(anchor="w")

        # Book Type
        ttk.Label(f, text="書籍種別:").grid(row=1, column=0, sticky="w", pady=6)
        self.book_type_var = tk.StringVar(value=self.settings_mgr.get("book_type", "和書"))
        b_combo = ttk.Combobox(f, textvariable=self.book_type_var, values=["和書", "洋書"], state="readonly", width=12)
        b_combo.grid(row=1, column=1, sticky="w", pady=6, padx=4)

        # PDF Render DPI
        ttk.Label(f, text="PDFレンダリングDPI:").grid(row=2, column=0, sticky="w", pady=6)
        self.dpi_var = tk.IntVar(value=self.settings_mgr.get("render_dpi", 300))
        dpi_combo = ttk.Combobox(f, textvariable=self.dpi_var, values=[300, 400, 600], state="readonly", width=12)
        dpi_combo.grid(row=2, column=1, sticky="w", pady=6, padx=4)

        # Column deck
        ttk.Label(f, text="段組設定:").grid(row=3, column=0, sticky="w", pady=6)
        self.deck_var = tk.StringVar(value=self.settings_mgr.get("column_deck", "auto"))
        d_combo = ttk.Combobox(f, textvariable=self.deck_var, values=["auto", "1段", "2段", "3段"], state="readonly", width=12)
        d_combo.grid(row=3, column=1, sticky="w", pady=6, padx=4)

        # TCY Detection checkbox
        self.tcy_enable_var = tk.BooleanVar(value=self.settings_mgr.get("enable_tcy", True))
        ttk.Checkbutton(f, text="縦中横（10〜99）数字認識・テンプレート自動補正を有効化", variable=self.tcy_enable_var).grid(row=4, column=0, columnspan=2, sticky="w", pady=10)

    # -------------------------------------------------------------
    # TAB 3: Output & Format
    # -------------------------------------------------------------
    def _build_output_tab(self):
        f = self.tab_output

        # Chars per line
        ttk.Label(f, text="1行文字数制限 (0で制限なし):").grid(row=0, column=0, sticky="w", pady=6)
        self.chars_line_var = tk.IntVar(value=self.settings_mgr.get("chars_per_line", 0))
        ttk.Spinbox(f, from_=0, to=120, textvariable=self.chars_line_var, width=8).grid(row=0, column=1, sticky="w", pady=6, padx=4)

        # Annotation numbering
        ttk.Label(f, text="注釈採番方式:").grid(row=1, column=0, sticky="w", pady=6)
        self.anno_num_var = tk.StringVar(value=self.settings_mgr.get("annotation_numbering", "通し番号"))
        ttk.Combobox(f, textvariable=self.anno_num_var, values=["通し番号", "見出し毎"], state="readonly", width=14).grid(row=1, column=1, sticky="w", pady=6, padx=4)

        # Page break in Word
        self.page_break_var = tk.BooleanVar(value=self.settings_mgr.get("insert_page_break", True))
        ttk.Checkbutton(f, text="Word出力時にページ毎の改ページを挿入する", variable=self.page_break_var).grid(row=2, column=0, columnspan=2, sticky="w", pady=6)

        # Default export directory
        ttk.Label(f, text="デフォルト出力先フォルダ:").grid(row=3, column=0, sticky="w", pady=6)
        d_frame = ttk.Frame(f)
        d_frame.grid(row=3, column=1, sticky="ew", pady=6)
        self.export_dir_var = tk.StringVar(value=self.settings_mgr.get("default_export_dir", ""))
        self.export_dir_entry = ttk.Entry(d_frame, textvariable=self.export_dir_var, width=28)
        self.export_dir_entry.pack(side=tk.LEFT, fill=tk.X, expand=True)
        ttk.Button(d_frame, text="参照...", command=self.browse_export_dir, width=8).pack(side=tk.LEFT, padx=4)

        f.columnconfigure(1, weight=1)

    def browse_export_dir(self):
        d = filedialog.askdirectory(title="デフォルトWord出力先フォルダを選択")
        if d:
            self.export_dir_var.set(d)

    # -------------------------------------------------------------
    # TAB 4: Dictionary & Replacements
    # -------------------------------------------------------------
    def _build_dict_tab(self):
        f = self.tab_dict

        # Statistics Summary Frame
        stat_frame = ttk.LabelFrame(f, text="辞書登録状況", padding=10)
        stat_frame.pack(fill=tk.X, pady=6)

        tcy_cnt = len(self.tcy_service.templates)
        exact_cnt = len(self.dict_service.exact_replacements)
        regex_cnt = len(self.dict_service.regex_replacements)

        ttk.Label(stat_frame, text=f"• 縦中横（10〜99）テンプレート登録数: {tcy_cnt} 件", font=("Meiryo UI", 9, "bold")).pack(anchor="w", pady=2)
        ttk.Label(stat_frame, text=f"• ユーザー補正辞書（完全一致ルール）: {exact_cnt} 件", font=("Meiryo UI", 9)).pack(anchor="w", pady=2)
        ttk.Label(stat_frame, text=f"• ユーザー補正辞書（正規表現ルール）: {regex_cnt} 件", font=("Meiryo UI", 9)).pack(anchor="w", pady=2)

        # Direct Modal Launcher Buttons
        action_frame = ttk.LabelFrame(f, text="登録・編集モーダルを開く", padding=10)
        action_frame.pack(fill=tk.X, pady=8)

        ttk.Button(
            action_frame,
            text="🔢 縦中横（10〜99）画像・数値登録モーダルを開く...",
            command=lambda: self.open_user_dict_modal(0)
        ).pack(fill=tk.X, pady=4)

        ttk.Button(
            action_frame,
            text="📝 語句・誤読修正ルール編集モーダルを開く...",
            command=lambda: self.open_user_dict_modal(1)
        ).pack(fill=tk.X, pady=4)

        # Quick test box
        test_box = ttk.LabelFrame(f, text="簡易変換テスト", padding=8)
        test_box.pack(fill=tk.BOTH, expand=True, pady=6)

        t_grid = ttk.Frame(test_box)
        t_grid.pack(fill=tk.X)
        ttk.Label(t_grid, text="テスト入力:").grid(row=0, column=0, sticky="w")
        self.quick_in_var = tk.StringVar(value="サプライチエーンの再構築")
        self.quick_in_entry = ttk.Entry(t_grid, textvariable=self.quick_in_var)
        self.quick_in_entry.grid(row=0, column=1, sticky="ew", padx=4, pady=2)
        self.quick_in_entry.bind("<KeyRelease>", lambda e: self.update_quick_test())

        ttk.Label(t_grid, text="変換後:").grid(row=1, column=0, sticky="w")
        self.quick_out_var = tk.StringVar()
        self.quick_out_entry = ttk.Entry(t_grid, textvariable=self.quick_out_var, state="readonly")
        self.quick_out_entry.grid(row=1, column=1, sticky="ew", padx=4, pady=2)
        t_grid.columnconfigure(1, weight=1)

        self.update_quick_test()

    def update_quick_test(self):
        out = self.dict_service.apply(self.quick_in_var.get())
        self.quick_out_var.set(out)

    def open_user_dict_modal(self, default_tab=0):
        dlg = UserDictDialog(self, default_tab=default_tab, on_saved=self._on_dict_saved)
        self.wait_window(dlg)

    def _on_dict_saved(self):
        self.dict_service.load()
        self.tcy_service.load()
        self.update_quick_test()

    # -------------------------------------------------------------
    # Save & Reset Actions
    # -------------------------------------------------------------
    def save_and_close(self):
        self.settings_mgr.set("font_family", self.font_family_var.get())
        self.settings_mgr.set("font_size", self.font_size_var.get())
        self.settings_mgr.set("font_bold", self.font_bold_var.get())
        self.settings_mgr.set("ocr_zoom", self.ocr_zoom_var.get())
        self.settings_mgr.set("ocr_orientation", self.orient_var.get())
        self.settings_mgr.set("book_type", self.book_type_var.get())
        self.settings_mgr.set("render_dpi", self.dpi_var.get())
        self.settings_mgr.set("column_deck", self.deck_var.get())
        self.settings_mgr.set("enable_tcy", self.tcy_enable_var.get())
        self.settings_mgr.set("chars_per_line", self.chars_line_var.get())
        self.settings_mgr.set("annotation_numbering", self.anno_num_var.get())
        self.settings_mgr.set("insert_page_break", self.page_break_var.get())
        self.settings_mgr.set("default_export_dir", self.export_dir_var.get().strip())

        if self.on_apply:
            self.on_apply()

        self.destroy()

    def reset_defaults(self):
        if messagebox.askyesno("確認", "全ての設定を初期値に戻しますか？", parent=self):
            self.settings_mgr.reset_to_defaults()
            if self.on_apply:
                self.on_apply()
            self.destroy()
