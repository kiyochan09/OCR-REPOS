import os
import sys
import time
import threading
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from PIL import Image

from .settings_manager import SettingsManager
from .services import PdfService, OcrRunnerService, UserDictService, TcyRegistryService
from .canvas_viewer import CanvasViewer
from .table_view import TableView
from .figure_gallery import FigureGallery
from .batch_search_dialog import BatchSearchDialog
from .user_dict_dialog import UserDictDialog
from .option_dialog import OptionDialog
from .docx_export import DocxExporter

class MainWindow(ttk.Frame):
    def __init__(self, root):
        super().__init__(root)
        self.root = root
        self.root.title("OCR Translator - 高精度PDF縦書き・横書きテキスト抽出システム")
        
        self.settings_mgr = SettingsManager()
        self.pdf_service = PdfService()
        self.ocr_runner = OcrRunnerService()
        self.dict_service = UserDictService()
        self.tcy_service = TcyRegistryService()
        self.docx_exporter = DocxExporter()

        self.current_page = 1
        self.total_pages = 0
        self.batch_start_page = 1
        self.batch_end_page = 20
        self.pages_cache = {}  # {page_num: {body_text, headings, tables, annotations, figures, line_boxes, pil_img}}

        self.is_ocr_running = False

        self._apply_theme()
        self._create_widgets()
        self._bind_shortcuts()
        self._apply_font_settings()

    def _apply_theme(self):
        style = ttk.Style()
        try:
            style.theme_use("clam")
        except Exception:
            pass
        style.configure("TButton", font=("Meiryo UI", 9), padding=3)
        style.configure("TLabel", font=("Meiryo UI", 9))
        style.configure("TNotebook.Tab", font=("Meiryo UI", 9, "bold"), padding=[10, 4])
        style.configure("Status.TLabel", font=("Meiryo UI", 9), padding=2)

    def _create_widgets(self):
        # 1. Top Toolbar
        self.toolbar = ttk.Frame(self.root, padding=4)
        self.toolbar.pack(side=tk.TOP, fill=tk.X)
        self._build_toolbar()

        # 2. Status Bar at Bottom
        self.statusbar = ttk.Frame(self.root, relief="sunken", padding=3)
        self.statusbar.pack(side=tk.BOTTOM, fill=tk.X)
        self._build_statusbar()

        # 3. Main PanedWindow (Canvas Left, Tabbed Editor Right)
        self.paned = ttk.PanedWindow(self.root, orient=tk.HORIZONTAL)
        self.paned.pack(fill=tk.BOTH, expand=True, padx=4, pady=4)

        # Left: Canvas Viewer
        self.canvas_frame = ttk.Frame(self.paned)
        self.viewer = CanvasViewer(self.canvas_frame, on_box_clicked=self._on_canvas_box_clicked)
        self.viewer.pack(fill=tk.BOTH, expand=True)
        self.paned.add(self.canvas_frame, weight=3)

        # Right: Tabbed OCR Editor
        self.editor_frame = ttk.Frame(self.paned)
        self._build_editor_tabs(self.editor_frame)
        self.paned.add(self.editor_frame, weight=2)

    # -------------------------------------------------------------
    # 1. TOOLBAR
    # -------------------------------------------------------------
    def _build_toolbar(self):
        tb = self.toolbar

        # File Group
        ttk.Button(tb, text="📂 PDFを開く", command=self.open_pdf_dialog).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="📕 PDFを閉じる", command=self.close_pdf).pack(side=tk.LEFT, padx=2)
        
        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)

        # Navigation Group
        ttk.Button(tb, text="⏮ 先頭", command=self.go_first_page, width=6).pack(side=tk.LEFT, padx=1)
        ttk.Button(tb, text="◀ 前頁", command=self.go_prev_page, width=6).pack(side=tk.LEFT, padx=1)
        
        self.page_var = tk.IntVar(value=1)
        self.page_spin = ttk.Spinbox(tb, from_=1, to=9999, textvariable=self.page_var, width=5, command=self._on_page_spin)
        self.page_spin.pack(side=tk.LEFT, padx=2)
        self.page_spin.bind("<Return>", lambda e: self._on_page_spin())

        self.total_page_lbl = ttk.Label(tb, text="/ 0 頁")
        self.total_page_lbl.pack(side=tk.LEFT, padx=2)

        ttk.Button(tb, text="▶ 次頁", command=self.go_next_page, width=6).pack(side=tk.LEFT, padx=1)
        ttk.Button(tb, text="⏭ 末尾", command=self.go_last_page, width=6).pack(side=tk.LEFT, padx=1)
        ttk.Button(tb, text="⏭ 20P次バッチ", command=self.go_next_batch).pack(side=tk.LEFT, padx=2)

        ttk.Label(tb, text=" 範囲:").pack(side=tk.LEFT, padx=2)
        self.batch_start_var = tk.IntVar(value=1)
        self.batch_start_spin = ttk.Spinbox(tb, from_=1, to=9999, textvariable=self.batch_start_var, width=4)
        self.batch_start_spin.pack(side=tk.LEFT)
        ttk.Label(tb, text="〜").pack(side=tk.LEFT)
        self.batch_end_var = tk.IntVar(value=20)
        self.batch_end_spin = ttk.Spinbox(tb, from_=1, to=9999, textvariable=self.batch_end_var, width=4)
        self.batch_end_spin.pack(side=tk.LEFT)

        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)

        # Zoom Group
        ttk.Button(tb, text="➖", command=lambda: self.viewer.zoom_out(), width=3).pack(side=tk.LEFT, padx=1)
        self.zoom_var = tk.StringVar(value="Fit")
        self.zoom_combo = ttk.Combobox(tb, textvariable=self.zoom_var, values=["Fit", "50%", "75%", "100%", "125%", "150%", "200%"], width=6, state="readonly")
        self.zoom_combo.pack(side=tk.LEFT, padx=1)
        self.zoom_combo.bind("<<ComboboxSelected>>", lambda e: self.viewer.set_zoom(self.zoom_var.get()))
        ttk.Button(tb, text="➕", command=lambda: self.viewer.zoom_in(), width=3).pack(side=tk.LEFT, padx=1)
        ttk.Button(tb, text="全体", command=lambda: self.viewer.zoom_fit(), width=4).pack(side=tk.LEFT, padx=1)

        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)

        # OCR & Export Group
        self.ocr_btn = ttk.Button(tb, text="🔍 OCR実行", command=self.start_ocr_menu)
        self.ocr_btn.pack(side=tk.LEFT, padx=2)

        self.docx_btn = ttk.Button(tb, text="💾 Word出力", command=self.start_docx_menu)
        self.docx_btn.pack(side=tk.LEFT, padx=2)

        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)

        # Search, Dictionary & Settings
        ttk.Button(tb, text="🔍 バッチ検索", command=self.open_batch_search).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="📖 補正辞書・10〜99登録", command=self.open_user_dict_modal).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="⚙️ オプション設定", command=self.open_options_modal).pack(side=tk.LEFT, padx=2)

    # -------------------------------------------------------------
    # 2. STATUSBAR
    # -------------------------------------------------------------
    def _build_statusbar(self):
        sb = self.statusbar
        self.status_msg_lbl = ttk.Label(sb, text="準備完了", style="Status.TLabel")
        self.status_msg_lbl.pack(side=tk.LEFT, padx=6)

        self.progress_bar = ttk.Progressbar(sb, orient=tk.HORIZONTAL, length=180, mode="determinate")
        self.progress_bar.pack(side=tk.LEFT, padx=8)

        self.file_info_lbl = ttk.Label(sb, text="PDF未読込", style="Status.TLabel")
        self.file_info_lbl.pack(side=tk.RIGHT, padx=8)

    # -------------------------------------------------------------
    # 3. RIGHT EDITOR TABS
    # -------------------------------------------------------------
    def _build_editor_tabs(self, parent):
        self.notebook = ttk.Notebook(parent)
        self.notebook.pack(fill=tk.BOTH, expand=True)

        # Tab 1: Main Text (本文)
        self.body_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.body_frame, text="  📄 本文  ")
        self._build_body_tab(self.body_frame)

        # Tab 2: Table (表)
        self.table_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.table_frame, text="  📊 表  ")
        self.table_view = TableView(self.table_frame)
        self.table_view.pack(fill=tk.BOTH, expand=True)

        # Tab 3: Headings (見出し)
        self.heading_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.heading_frame, text="  🏷️ 見出し  ")
        self._build_heading_tab(self.heading_frame)

        # Tab 4: Annotations (注釈文)
        self.anno_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.anno_frame, text="  📌 注釈文  ")
        self._build_anno_tab(self.anno_frame)

        # Tab 5: Figures (図)
        self.fig_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.fig_frame, text="  🖼️ 図  ")
        self.figure_gallery = FigureGallery(self.fig_frame)
        self.figure_gallery.pack(fill=tk.BOTH, expand=True)

        # Tab 6: Logs (ログ)
        self.log_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.log_frame, text="  📜 ログ  ")
        self._build_log_tab(self.log_frame)

    def _build_body_tab(self, parent):
        # Body text mini toolbar
        tb = ttk.Frame(parent, padding=3)
        tb.pack(fill=tk.X)

        ttk.Button(tb, text="📋 全文コピー", command=self.copy_body_text).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="🔄 段落整形・再適用", command=self.format_body_paragraphs).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="🗑️ クリア", command=self.clear_body_text).pack(side=tk.RIGHT, padx=2)

        # ScrolledText
        t_frame = ttk.Frame(parent)
        t_frame.pack(fill=tk.BOTH, expand=True)

        self.body_text = tk.Text(t_frame, wrap=tk.WORD, undo=True, font=("Meiryo UI", 12))
        self.body_scroll = ttk.Scrollbar(t_frame, orient=tk.VERTICAL, command=self.body_text.yview)
        self.body_text.configure(yscrollcommand=self.body_scroll.set)

        self.body_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.body_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

    def _build_heading_tab(self, parent):
        tb = ttk.Frame(parent, padding=3)
        tb.pack(fill=tk.X)
        ttk.Button(tb, text="📋 見出しコピー", command=lambda: self._copy_text_from(self.heading_text)).pack(side=tk.LEFT, padx=2)

        t_frame = ttk.Frame(parent)
        t_frame.pack(fill=tk.BOTH, expand=True)
        self.heading_text = tk.Text(t_frame, wrap=tk.WORD, undo=True, font=("Meiryo UI", 11))
        scroll = ttk.Scrollbar(t_frame, orient=tk.VERTICAL, command=self.heading_text.yview)
        self.heading_text.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.heading_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

    def _build_anno_tab(self, parent):
        tb = ttk.Frame(parent, padding=3)
        tb.pack(fill=tk.X)
        ttk.Button(tb, text="📋 注釈コピー", command=lambda: self._copy_text_from(self.anno_text)).pack(side=tk.LEFT, padx=2)

        t_frame = ttk.Frame(parent)
        t_frame.pack(fill=tk.BOTH, expand=True)
        self.anno_text = tk.Text(t_frame, wrap=tk.WORD, undo=True, font=("Meiryo UI", 11))
        scroll = ttk.Scrollbar(t_frame, orient=tk.VERTICAL, command=self.anno_text.yview)
        self.anno_text.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.anno_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

    def _build_log_tab(self, parent):
        tb = ttk.Frame(parent, padding=3)
        tb.pack(fill=tk.X)
        ttk.Button(tb, text="🗑️ ログクリア", command=self.clear_logs).pack(side=tk.RIGHT, padx=2)

        t_frame = ttk.Frame(parent)
        t_frame.pack(fill=tk.BOTH, expand=True)
        self.log_text = tk.Text(t_frame, wrap=tk.WORD, bg="#1E1E1E", fg="#D4D4D4", font=("Consolas", 10))
        scroll = ttk.Scrollbar(t_frame, orient=tk.VERTICAL, command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=scroll.set)
        scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.log_text.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

    def append_log(self, message: str):
        timestamp = time.strftime("[%H:%M:%S] ")
        self.log_text.insert(tk.END, f"{timestamp}{message}\n")
        self.log_text.see(tk.END)

    def clear_logs(self):
        self.log_text.delete("1.0", tk.END)

    def _copy_text_from(self, text_widget):
        content = text_widget.get("1.0", tk.END).strip()
        if content:
            self.root.clipboard_clear()
            self.root.clipboard_append(content)
            self.status_msg_lbl.config(text="クリップボードにコピーしました。")

    def copy_body_text(self):
        self._copy_text_from(self.body_text)

    def clear_body_text(self):
        self.body_text.delete("1.0", tk.END)

    def format_body_paragraphs(self):
        raw = self.body_text.get("1.0", tk.END).strip()
        if not raw:
            return
        # Apply user dict replacements and paragraph clean up
        cleaned = self.dict_service.apply(raw)
        self.body_text.delete("1.0", tk.END)
        self.body_text.insert("1.0", cleaned)
        self.status_msg_lbl.config(text="辞書置換および段落整形を適用しました。")

    # -------------------------------------------------------------
    # 4. KEYBOARD SHORTCUTS & FONTS
    # -------------------------------------------------------------
    def _bind_shortcuts(self):
        self.root.bind("<Control-o>", lambda e: self.open_pdf_dialog())
        self.root.bind("<Prior>", lambda e: self.go_prev_page())  # Page Up
        self.root.bind("<Next>", lambda e: self.go_next_page())   # Page Down
        self.root.bind("<Control-f>", lambda e: self.open_batch_search())

    def _apply_font_settings(self):
        fn = self.settings_mgr.get("font_family", "Meiryo UI")
        sz = self.settings_mgr.get("font_size", 12)
        weight = "bold" if self.settings_mgr.get("font_bold", False) else "normal"
        custom_font = (fn, sz, weight)

        self.body_text.config(font=custom_font)
        self.heading_text.config(font=custom_font)
        self.anno_text.config(font=custom_font)

    # -------------------------------------------------------------
    # 5. PDF LOADING & NAVIGATION
    # -------------------------------------------------------------
    def open_pdf_dialog(self):
        path = filedialog.askopenfilename(
            title="PDFファイルを選択",
            filetypes=[("PDF Documents", "*.pdf")]
        )
        if path:
            self.load_pdf(path)

    def load_pdf(self, path: str):
        try:
            self.status_msg_lbl.config(text=f"PDFを読み込み中: {os.path.basename(path)}...")
            self.total_pages = self.pdf_service.open(path)
            self.current_page = 1
            self.pages_cache.clear()

            self.total_page_lbl.config(text=f"/ {self.total_pages} 頁")
            self.page_spin.config(to=self.total_pages)
            self.batch_start_spin.config(to=self.total_pages)
            self.batch_end_spin.config(to=self.total_pages)

            self.batch_start_var.set(1)
            self.batch_end_var.set(min(20, self.total_pages))

            self.file_info_lbl.config(text=f"{os.path.basename(path)} | 総頁数: {self.total_pages}")
            self.append_log(f"PDFファイルを開きました: {path} (全 {self.total_pages} ページ)")
            
            self.settings_mgr.set("last_pdf_path", path)
            self.display_page(1)
        except Exception as e:
            messagebox.showerror("PDF読込エラー", f"PDFファイルの読み込みに失敗しました:\n{e}")
            self.append_log(f"PDF読込エラー: {e}")

    def close_pdf(self):
        self.pdf_service.close()
        self.pages_cache.clear()
        self.total_pages = 0
        self.current_page = 1
        self.viewer.set_image(None)
        self.total_page_lbl.config(text="/ 0 頁")
        self.file_info_lbl.config(text="PDF未読込")
        self.body_text.delete("1.0", tk.END)
        self.heading_text.delete("1.0", tk.END)
        self.anno_text.delete("1.0", tk.END)
        self.table_view.clear_table()
        self.figure_gallery.set_figures([])
        self.status_msg_lbl.config(text="PDFを閉じました。")

    def display_page(self, page_num: int):
        if self.total_pages == 0:
            return
        self.current_page = max(1, min(self.total_pages, page_num))
        self.page_var.set(self.current_page)

        # Render PDF page image
        dpi = self.settings_mgr.get("render_dpi", 300)
        img = self.pdf_service.render_page(self.current_page - 1, dpi=dpi)
        self.viewer.set_image(img, reset_zoom=False)

        # Restore from cache if already scanned
        if self.current_page in self.pages_cache:
            p_data = self.pages_cache[self.current_page]
            self.body_text.delete("1.0", tk.END)
            self.body_text.insert("1.0", p_data.get("body_text", ""))
            
            self.heading_text.delete("1.0", tk.END)
            self.heading_text.insert("1.0", "\n".join(p_data.get("headings", [])))

            self.anno_text.delete("1.0", tk.END)
            self.anno_text.insert("1.0", "\n".join(p_data.get("annotations", [])))

            self.table_view.set_data(p_data.get("tables", []))
            self.figure_gallery.set_figures(p_data.get("figures", []))
            self.viewer.set_boxes(p_data.get("line_boxes", []))
            self.status_msg_lbl.config(text=f"P.{self.current_page} を表示中（OCR結果キャッシュ済み）")
        else:
            self.body_text.delete("1.0", tk.END)
            self.heading_text.delete("1.0", tk.END)
            self.anno_text.delete("1.0", tk.END)
            self.table_view.clear_table()
            self.figure_gallery.set_figures([])
            self.viewer.set_boxes([])
            self.status_msg_lbl.config(text=f"P.{self.current_page} を表示中（未OCR）")

    def _on_page_spin(self):
        val = self.page_var.get()
        self.display_page(val)

    def go_first_page(self):
        self.display_page(1)

    def go_prev_page(self):
        if self.current_page > 1:
            self.display_page(self.current_page - 1)

    def go_next_page(self):
        if self.current_page < self.total_pages:
            self.display_page(self.current_page + 1)

    def go_last_page(self):
        self.display_page(self.total_pages)

    def go_next_batch(self):
        cur_end = self.batch_end_var.get()
        new_start = cur_end + 1
        new_end = min(self.total_pages, new_start + 19)
        if new_start <= self.total_pages:
            self.batch_start_var.set(new_start)
            self.batch_end_var.set(new_end)
            self.display_page(new_start)

    def _on_canvas_box_clicked(self, idx, box_data):
        text = box_data.get("text", "").strip()
        if not text:
            return
        # Find in body text and highlight
        body_content = self.body_text.get("1.0", tk.END)
        pos = body_content.find(text)
        if pos != -1:
            self.notebook.select(0)
            self.body_text.tag_remove("highlight", "1.0", tk.END)
            start_idx = f"1.0 + {pos} chars"
            end_idx = f"1.0 + {pos + len(text)} chars"
            self.body_text.tag_add("highlight", start_idx, end_idx)
            self.body_text.tag_config("highlight", background="#FFF275", foreground="#000000")
            self.body_text.see(start_idx)

    # -------------------------------------------------------------
    # 6. OCR EXECUTION
    # -------------------------------------------------------------
    def start_ocr_menu(self):
        if self.total_pages == 0:
            messagebox.showinfo("情報", "PDFファイルを開いてください。")
            return
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label=f"現在のページ (P.{self.current_page}) をOCR実行", command=lambda: self.run_ocr_pages([self.current_page]))
        b_start = self.batch_start_var.get()
        b_end = self.batch_end_var.get()
        menu.add_command(label=f"指定バッチ範囲 (P.{b_start}〜P.{b_end}) をOCR実行", command=lambda: self.run_ocr_pages(list(range(b_start, b_end + 1))))
        menu.add_command(label=f"全ページ (P.1〜P.{self.total_pages}) をOCR実行", command=lambda: self.run_ocr_pages(list(range(1, self.total_pages + 1))))
        
        # Display menu below button
        x = self.ocr_btn.winfo_rootx()
        y = self.ocr_btn.winfo_rooty() + self.ocr_btn.winfo_height()
        menu.tk_popup(x, y)

    def run_ocr_pages(self, pages):
        if self.is_ocr_running:
            messagebox.showwarning("警告", "現在別のOCR処理が実行中です。")
            return
        
        self.is_ocr_running = True
        self.ocr_btn.config(state="disabled")
        self.progress_bar.config(value=0, maximum=len(pages))
        
        output_dir = os.path.join(os.path.dirname(self.pdf_service.pdf_path), "ocr_results")
        os.makedirs(output_dir, exist_ok=True)

        threading.Thread(target=self._ocr_worker, args=(pages, output_dir), daemon=True).start()

    def _ocr_worker(self, pages, output_dir):
        total = len(pages)
        dpi = self.settings_mgr.get("render_dpi", 300)
        options = {
            "enable_tcy": self.settings_mgr.get("enable_tcy", True),
            "render_dpi": dpi,
            "column_deck": self.settings_mgr.get("column_deck", "auto")
        }

        for idx, p_num in enumerate(pages):
            def log_cb(msg):
                self.root.after(0, lambda m=msg: self._on_ocr_progress(p_num, m))

            self.root.after(0, lambda i=idx, p=p_num: self._update_ocr_status(f"P.{p} OCR処理中... ({i+1}/{total})", i))

            page_img = self.pdf_service.render_page(p_num - 1, dpi=dpi)
            parsed_result = self.ocr_runner.run_page_ocr(page_img, p_num, output_dir, options, progress_cb=log_cb)

            # Store in cache
            self.pages_cache[p_num] = parsed_result
            
            # If current page, refresh immediately
            if p_num == self.current_page:
                self.root.after(0, lambda p=p_num: self.display_page(p))

        self.root.after(0, lambda: self._on_ocr_completed(total))

    def _on_ocr_progress(self, page_num, msg):
        self.append_log(f"[P.{page_num}] {msg}")

    def _update_ocr_status(self, msg, progress_val):
        self.status_msg_lbl.config(text=msg)
        self.progress_bar.config(value=progress_val)

    def _on_ocr_completed(self, total):
        self.is_ocr_running = False
        self.ocr_btn.config(state="normal")
        self.progress_bar.config(value=self.progress_bar["maximum"])
        self.status_msg_lbl.config(text=f"OCR処理完了: 全 {total} ページを処理しました。")
        self.append_log(f"OCR処理完了: 全 {total} ページ")
        messagebox.showinfo("OCR完了", f"全 {total} ページのOCR処理が完了しました。")

    # -------------------------------------------------------------
    # 7. WORD (.DOCX) EXPORT
    # -------------------------------------------------------------
    def start_docx_menu(self):
        if not self.pages_cache:
            messagebox.showinfo("情報", "出力対象のOCR結果がありません。先にOCRを実行してください。")
            return
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label=f"現在のページ (P.{self.current_page}) をWord出力", command=lambda: self.export_docx_pages([self.current_page]))
        b_start = self.batch_start_var.get()
        b_end = self.batch_end_var.get()
        batch_pages = [p for p in range(b_start, b_end + 1) if p in self.pages_cache]
        menu.add_command(label=f"指定バッチ範囲 (P.{b_start}〜P.{b_end}) をWord出力 ({len(batch_pages)}頁)", command=lambda: self.export_docx_pages(batch_pages))
        all_pages = sorted(self.pages_cache.keys())
        menu.add_command(label=f"全OCR完了ページ ({len(all_pages)}頁) をWord出力", command=lambda: self.export_docx_pages(all_pages))

        x = self.docx_btn.winfo_rootx()
        y = self.docx_btn.winfo_rooty() + self.docx_btn.winfo_height()
        menu.tk_popup(x, y)

    def export_docx_pages(self, pages):
        if not pages:
            messagebox.showinfo("情報", "選択された範囲にOCR結果がありません。")
            return

        default_dir = self.settings_mgr.get("default_export_dir", "")
        if not default_dir or not os.path.exists(default_dir):
            default_dir = os.path.dirname(self.pdf_service.pdf_path) if self.pdf_service.pdf_path else ""

        default_name = f"OCR_Output_{pages[0]}-{pages[-1]}.docx" if len(pages) > 1 else f"OCR_Output_P{pages[0]}.docx"
        initial_file = os.path.join(default_dir, default_name)

        path = filedialog.asksaveasfilename(
            initialfile=default_name,
            initialdir=default_dir,
            defaultextension=".docx",
            filetypes=[("Word Documents", "*.docx")]
        )
        if not path:
            return

        # Prepare export data
        export_list = []
        for p in pages:
            if p in self.pages_cache:
                export_list.append(self.pages_cache[p])

        options = {
            "insert_page_break": self.settings_mgr.get("insert_page_break", True),
            "annotation_numbering": self.settings_mgr.get("annotation_numbering", "通し番号")
        }

        try:
            out_file = self.docx_exporter.export(export_list, path, options)
            self.status_msg_lbl.config(text=f"Word文書を保存しました: {os.path.basename(out_file)}")
            self.append_log(f"Word出力完了: {out_file}")
            messagebox.showinfo("出力完了", f"Word文書を正常に作成しました:\n{out_file}")
        except Exception as e:
            messagebox.showerror("エラー", f"Word出力に失敗しました:\n{e}")
            self.append_log(f"Word出力エラー: {e}")

    # -------------------------------------------------------------
    # 8. DIALOGS (BATCH SEARCH, USER DICT, OPTIONS)
    # -------------------------------------------------------------
    def open_batch_search(self):
        dlg = BatchSearchDialog(self.root, self.pages_cache, self.current_page, on_jump=self._on_search_jump)

    def _on_search_jump(self, page_num, keyword):
        self.display_page(page_num)
        self.notebook.select(0)
        # Highlight in body
        body_content = self.body_text.get("1.0", tk.END)
        pos = body_content.lower().find(keyword.lower())
        if pos != -1:
            self.body_text.tag_remove("search_highlight", "1.0", tk.END)
            start_idx = f"1.0 + {pos} chars"
            end_idx = f"1.0 + {pos + len(keyword)} chars"
            self.body_text.tag_add("search_highlight", start_idx, end_idx)
            self.body_text.tag_config("search_highlight", background="#FF851B", foreground="#FFFFFF")
            self.body_text.see(start_idx)

    def open_user_dict_modal(self, default_tab=0):
        dlg = UserDictDialog(self.root, default_tab=default_tab, on_saved=self._on_dict_updated)

    def _on_dict_updated(self):
        self.dict_service.load()
        self.tcy_service.load()
        self.status_msg_lbl.config(text="辞書・縦中横レジストリを更新しました。")

    def open_options_modal(self):
        dlg = OptionDialog(self.root, self.settings_mgr, on_apply=self._on_options_applied)

    def _on_options_applied(self):
        self._apply_font_settings()
        zoom_val = self.settings_mgr.get("ocr_zoom", "Fit")
        self.zoom_var.set(zoom_val)
        self.viewer.set_zoom(zoom_val)
        self.status_msg_lbl.config(text="設定を適用しました。")
