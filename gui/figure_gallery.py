import os
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from PIL import Image, ImageTk
import io
import shutil

class FigureGallery(ttk.Frame):
    def __init__(self, parent, *args, **kwargs):
        super().__init__(parent, *args, **kwargs)
        self.figure_paths = []
        self._photo_images = []
        self._create_widgets()

    def _create_widgets(self):
        # Toolbar
        tb = ttk.Frame(self)
        tb.pack(fill=tk.X, padx=4, pady=4)

        ttk.Button(tb, text="💾 すべての図を一括保存...", command=self.export_all).pack(side=tk.LEFT, padx=2)
        self.info_lbl = ttk.Label(tb, text="抽出された図: 0件")
        self.info_lbl.pack(side=tk.RIGHT, padx=4)

        # Scrollable area
        self.canvas = tk.Canvas(self, bg="#F7F7F7", highlightthickness=0)
        self.scrollbar = ttk.Scrollbar(self, orient=tk.VERTICAL, command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=self.scrollbar.set)

        self.container = ttk.Frame(self.canvas)
        self.container.bind("<Configure>", lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.canvas.create_window((0, 0), window=self.container, anchor="nw")

        self.scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.refresh()

    def set_figures(self, figure_paths):
        self.figure_paths = figure_paths or []
        self.refresh()

    def refresh(self):
        for widget in self.container.winfo_children():
            widget.destroy()
        self._photo_images.clear()

        count = len(self.figure_paths)
        self.info_lbl.config(text=f"抽出された図: {count}件")

        if not self.figure_paths:
            lbl = ttk.Label(self.container, text="抽出された図（写真・挿絵・グラフ）はありません。", font=("Meiryo UI", 10), foreground="#888888")
            lbl.pack(padx=20, pady=30)
            return

        for idx, fig_path in enumerate(self.figure_paths):
            if not os.path.exists(fig_path):
                continue
            card = ttk.LabelFrame(self.container, text=f"図 {idx+1}: {os.path.basename(fig_path)}", padding=8)
            card.pack(fill=tk.X, padx=10, pady=6)

            # Load thumbnail
            try:
                im = Image.open(fig_path)
                orig_w, orig_h = im.size
                im.thumbnail((400, 300), Image.Resampling.LANCZOS)
                photo = ImageTk.PhotoImage(im)
                self._photo_images.append(photo)

                img_lbl = ttk.Label(card, image=photo)
                img_lbl.pack(side=tk.LEFT, padx=6, pady=4)

                # Info & Action frame
                action_frame = ttk.Frame(card)
                action_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=8)

                ttk.Label(action_frame, text=f"サイズ: {orig_w} × {orig_h} px", font=("Meiryo UI", 9)).pack(anchor="w", pady=2)
                ttk.Button(action_frame, text="💾 画像を保存...", command=lambda p=fig_path: self.save_fig(p)).pack(anchor="w", pady=3)
            except Exception as e:
                ttk.Label(card, text=f"画像読み込みエラー: {e}").pack()

    def save_fig(self, path):
        ext = os.path.splitext(path)[1].lower() or ".png"
        dest = filedialog.asksaveasfilename(defaultextension=ext, filetypes=[("Images", "*.png;*.jpg;*.jpeg")])
        if dest:
            try:
                shutil.copyfile(path, dest)
                messagebox.showinfo("保存完了", f"画像を保存しました:\n{dest}")
            except Exception as e:
                messagebox.showerror("エラー", f"保存に失敗しました:\n{e}")

    def export_all(self):
        if not self.figure_paths:
            messagebox.showinfo("情報", "保存対象の図がありません。")
            return
        dest_dir = filedialog.askdirectory(title="保存先フォルダを選択")
        if dest_dir:
            saved = 0
            for idx, p in enumerate(self.figure_paths):
                if os.path.exists(p):
                    fname = f"fig_{idx+1}_{os.path.basename(p)}"
                    shutil.copyfile(p, os.path.join(dest_dir, fname))
                    saved += 1
            messagebox.showinfo("保存完了", f"{saved} 件の画像を保存しました:\n{dest_dir}")
