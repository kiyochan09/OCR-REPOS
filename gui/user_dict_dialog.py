import os
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from PIL import Image, ImageTk
from pathlib import Path
from .services import UserDictService, TcyRegistryService

class RuleEditDialog(tk.Toplevel):
    def __init__(self, parent, rule_data=None):
        super().__init__(parent)
        self.title("ルール追加・編集" if rule_data else "新規ルール追加")
        self.geometry("480x280")
        self.resizable(False, False)
        self.transient(parent)
        self.grab_set()

        self.result = None
        rule_data = rule_data or {}

        # Type
        self.type_var = tk.StringVar(value=rule_data.get("type", "exact"))
        type_frame = ttk.LabelFrame(self, text="ルール種別", padding=6)
        type_frame.pack(fill=tk.X, padx=12, pady=6)
        ttk.Radiobutton(type_frame, text="完全一致 (単語置換)", variable=self.type_var, value="exact").pack(side=tk.LEFT, padx=12)
        ttk.Radiobutton(type_frame, text="正規表現置換", variable=self.type_var, value="regex").pack(side=tk.LEFT, padx=12)

        # Fields
        f_frame = ttk.Frame(self, padding=6)
        f_frame.pack(fill=tk.X, padx=12, pady=4)

        ttk.Label(f_frame, text="置換前 (誤読パターン):").grid(row=0, column=0, sticky="w", pady=4)
        self.before_var = tk.StringVar(value=rule_data.get("before", ""))
        self.before_entry = ttk.Entry(f_frame, textvariable=self.before_var, width=36)
        self.before_entry.grid(row=0, column=1, sticky="ew", pady=4, padx=4)

        ttk.Label(f_frame, text="置換後 (正しい文字列):").grid(row=1, column=0, sticky="w", pady=4)
        self.after_var = tk.StringVar(value=rule_data.get("after", ""))
        self.after_entry = ttk.Entry(f_frame, textvariable=self.after_var, width=36)
        self.after_entry.grid(row=1, column=1, sticky="ew", pady=4, padx=4)

        ttk.Label(f_frame, text="説明 / 備考:").grid(row=2, column=0, sticky="w", pady=4)
        self.desc_var = tk.StringVar(value=rule_data.get("description", ""))
        self.desc_entry = ttk.Entry(f_frame, textvariable=self.desc_var, width=36)
        self.desc_entry.grid(row=2, column=1, sticky="ew", pady=4, padx=4)

        self.enabled_var = tk.BooleanVar(value=rule_data.get("enabled", True))
        ttk.Checkbutton(f_frame, text="このルールを有効にする", variable=self.enabled_var).grid(row=3, column=1, sticky="w", pady=4)

        f_frame.columnconfigure(1, weight=1)

        # Buttons
        btn_frame = ttk.Frame(self, padding=6)
        btn_frame.pack(fill=tk.X, padx=12, pady=10)
        ttk.Button(btn_frame, text="キャンセル", command=self.destroy, width=10).pack(side=tk.RIGHT, padx=4)
        ttk.Button(btn_frame, text="OK", command=self.on_ok, width=10).pack(side=tk.RIGHT, padx=4)

        self.before_entry.focus_set()

    def on_ok(self):
        before = self.before_var.get().strip()
        after = self.after_var.get()
        if not before:
            messagebox.showwarning("入力エラー", "置換前文字列を入力してください。", parent=self)
            return
        self.result = {
            "type": self.type_var.get(),
            "before": before,
            "after": after,
            "description": self.desc_var.get().strip(),
            "enabled": self.enabled_var.get()
        }
        self.destroy()


class UserDictDialog(tk.Toplevel):
    def __init__(self, parent, default_tab=0, on_saved=None):
        super().__init__(parent)
        self.title("📖 OCR補正辞書 & 縦中横（10〜99）画像・数値登録")
        self.geometry("960x680")
        self.minsize(820, 560)
        self.transient(parent)
        self.grab_set()

        self.on_saved = on_saved
        self.dict_service = UserDictService()
        self.tcy_service = TcyRegistryService()
        self._thumbnail_cache = {}

        self._create_widgets(default_tab)

    def _create_widgets(self, default_tab):
        self.notebook = ttk.Notebook(self)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        # Tab 1: TCY Image & Value Registration
        self.tcy_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.tcy_frame, text="  🔢 縦中横（10〜99）画像・数値登録  ")
        self._build_tcy_tab()

        # Tab 2: Word & Regex Replacement Rules
        self.rules_frame = ttk.Frame(self.notebook)
        self.notebook.add(self.rules_frame, text="  📝 語句・誤読修正ルール  ")
        self._build_rules_tab()

        if default_tab in (0, 1):
            self.notebook.select(default_tab)

        # Bottom global action bar
        bot_bar = ttk.Frame(self, padding=8)
        bot_bar.pack(fill=tk.X)

        self.global_status_lbl = ttk.Label(bot_bar, text="※ 変更は「保存して適用」を押すと即座にOCRエンジンに反映されます。")
        self.global_status_lbl.pack(side=tk.LEFT, padx=6)

        ttk.Button(bot_bar, text="閉じる", command=self.destroy, width=10).pack(side=tk.RIGHT, padx=4)
        ttk.Button(bot_bar, text="💾 保存して適用", command=self.save_all, width=14).pack(side=tk.RIGHT, padx=4)

    # -------------------------------------------------------------
    # TAB 1: TCY Registration Tab
    # -------------------------------------------------------------
    def _build_tcy_tab(self):
        # Top toolbar
        tb = ttk.Frame(self.tcy_frame, padding=6)
        tb.pack(fill=tk.X)

        ttk.Label(tb, text="検索:").pack(side=tk.LEFT, padx=2)
        self.tcy_search_var = tk.StringVar()
        self.tcy_search_entry = ttk.Entry(tb, textvariable=self.tcy_search_var, width=8)
        self.tcy_search_entry.pack(side=tk.LEFT, padx=2)
        self.tcy_search_entry.bind("<KeyRelease>", lambda e: self.filter_tcy_cards())

        # Range filter buttons
        for r_label, r_range in [("全0〜99", (0, 99)), ("10〜19", (10, 19)), ("20〜29", (20, 29)), ("30〜49", (30, 49)), ("50〜99", (50, 99)), ("1桁 0〜9", (0, 9))]:
            ttk.Button(tb, text=r_label, command=lambda rng=r_range: self.set_tcy_range(rng), width=8).pack(side=tk.LEFT, padx=2)

        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)

        ttk.Button(tb, text="🔄 0〜99標準画像を一括再生成", command=self.rebuild_all_tcy_templates).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="✅ 全て有効", command=lambda: self.toggle_all_tcy(True), width=9).pack(side=tk.RIGHT, padx=2)
        ttk.Button(tb, text="❌ 全て無効", command=lambda: self.toggle_all_tcy(False), width=9).pack(side=tk.RIGHT, padx=2)

        # Scrollable Cards Grid Container
        self.tcy_canvas = tk.Canvas(self.tcy_frame, bg="#F5F5F7", highlightthickness=0)
        self.tcy_vscroll = ttk.Scrollbar(self.tcy_frame, orient=tk.VERTICAL, command=self.tcy_canvas.yview)
        self.tcy_canvas.configure(yscrollcommand=self.tcy_vscroll.set)

        self.tcy_grid_frame = ttk.Frame(self.tcy_canvas)
        self.tcy_grid_frame.bind("<Configure>", lambda e: self.tcy_canvas.configure(scrollregion=self.tcy_canvas.bbox("all")))
        self.tcy_win = self.tcy_canvas.create_window((0, 0), window=self.tcy_grid_frame, anchor="nw")

        self.tcy_vscroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.tcy_canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.current_tcy_range = (0, 99)
        self.current_tcy_filter = ""
        self.render_tcy_cards()

    def set_tcy_range(self, rng):
        self.current_tcy_range = rng
        self.tcy_search_var.set("")
        self.current_tcy_filter = ""
        self.render_tcy_cards()

    def filter_tcy_cards(self):
        self.current_tcy_filter = self.tcy_search_var.get().strip()
        self.render_tcy_cards()

    def toggle_all_tcy(self, state: bool):
        for k in self.tcy_service.templates:
            self.tcy_service.templates[k]["enabled"] = state
        self.render_tcy_cards()

    def render_tcy_cards(self):
        for widget in self.tcy_grid_frame.winfo_children():
            widget.destroy()
        self._thumbnail_cache.clear()

        keys = sorted(self.tcy_service.templates.keys(), key=lambda x: int(x) if x.isdigit() else 999)
        
        # Filter keys
        filtered_keys = []
        for k in keys:
            if not k.isdigit():
                continue
            val_int = int(k)
            if self.current_tcy_filter:
                if self.current_tcy_filter not in k:
                    continue
            else:
                if val_int < self.current_tcy_range[0] or val_int > self.current_tcy_range[1]:
                    continue
            filtered_keys.append(k)

        cols_per_row = 4
        for idx, k in enumerate(filtered_keys):
            item = self.tcy_service.templates[k]
            row = idx // cols_per_row
            col = idx % cols_per_row
            self._create_tcy_card(self.tcy_grid_frame, k, item, row, col)

    def _create_tcy_card(self, parent, key, item, row, col):
        card = ttk.Frame(parent, relief="groove", padding=6)
        card.grid(row=row, column=col, padx=6, pady=6, sticky="nsew")

        # Top row: Checkbox + Number
        top_row = ttk.Frame(card)
        top_row.pack(fill=tk.X)

        enabled_var = tk.BooleanVar(value=item.get("enabled", True))
        def on_toggle():
            self.tcy_service.templates[key]["enabled"] = enabled_var.get()
        cb = ttk.Checkbutton(top_row, variable=enabled_var, command=on_toggle)
        cb.pack(side=tk.LEFT)

        num_lbl = ttk.Label(top_row, text=f"【 {key} 】", font=("Meiryo UI", 12, "bold"), foreground="#1A5276")
        num_lbl.pack(side=tk.LEFT, padx=4)

        # Image preview
        prim_img_name = item.get("primary_image", f"{key}.png")
        img_path = self.tcy_service.get_template_path(prim_img_name)

        preview_frame = tk.Frame(card, bg="#FFFFFF", width=64, height=64, relief="sunken", bd=1)
        preview_frame.pack_propagate(False)
        preview_frame.pack(pady=4)

        if img_path.exists():
            try:
                pil_im = Image.open(str(img_path))
                pil_im.thumbnail((56, 56), Image.Resampling.LANCZOS)
                photo = ImageTk.PhotoImage(pil_im)
                self._thumbnail_cache[key] = photo
                img_lbl = tk.Label(preview_frame, image=photo, bg="#FFFFFF")
                img_lbl.pack(expand=True)
            except Exception:
                tk.Label(preview_frame, text="No Img", bg="#FFFFFF", fg="#999999").pack(expand=True)
        else:
            tk.Label(preview_frame, text="未登録", bg="#FFFFFF", fg="#999999").pack(expand=True)

        # Action Buttons
        btn_f = ttk.Frame(card)
        btn_f.pack(fill=tk.X, pady=2)

        ttk.Button(btn_f, text="📷 画像変更...", command=lambda k=key: self.choose_tcy_image(k), width=10).pack(side=tk.LEFT, padx=1)
        ttk.Button(btn_f, text="🔤 再生成", command=lambda k=key: self.regen_tcy_image(k), width=7).pack(side=tk.RIGHT, padx=1)

    def choose_tcy_image(self, key):
        path = filedialog.askopenfilename(
            title=f"数字「{key}」の基準テンプレート画像を選択",
            filetypes=[("画像ファイル", "*.png;*.jpg;*.jpeg;*.bmp")]
        )
        if path:
            ok = self.tcy_service.register_custom_image(key, path)
            if ok:
                self.render_tcy_cards()
                self.global_status_lbl.config(text=f"数字「{key}」の基準画像を変更しました。")
            else:
                messagebox.showerror("エラー", f"画像の登録に失敗しました:\n{path}")

    def regen_tcy_image(self, key):
        ok = self.tcy_service.generate_font_image(key, "msmincho.ttc", 32)
        if ok:
            self.render_tcy_cards()
            self.global_status_lbl.config(text=f"数字「{key}」の画像を標準フォントから再生成しました。")

    def rebuild_all_tcy_templates(self):
        if messagebox.askyesno("確認", "0〜99の全数字テンプレート画像を標準フォントから一括再生成しますか？"):
            for i in range(100):
                self.tcy_service.generate_font_image(str(i), "msmincho.ttc", 32)
            self.tcy_service.rebuild_default_registry()
            self.render_tcy_cards()
            messagebox.showinfo("完了", "0〜99の全数字テンプレート画像を一括再生成しました。")

    # -------------------------------------------------------------
    # TAB 2: Rules Tab
    # -------------------------------------------------------------
    def _build_rules_tab(self):
        # Top toolbar
        tb = ttk.Frame(self.rules_frame, padding=6)
        tb.pack(fill=tk.X)

        ttk.Button(tb, text="➕ 新規ルール追加", command=self.add_rule).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="✏️ 編集", command=self.edit_rule).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="🗑️ 削除", command=self.delete_rule).pack(side=tk.LEFT, padx=2)
        
        ttk.Separator(tb, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=6)
        
        ttk.Button(tb, text="▲ 上へ", command=lambda: self.move_rule(-1), width=6).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="▼ 下へ", command=lambda: self.move_rule(1), width=6).pack(side=tk.LEFT, padx=2)
        ttk.Button(tb, text="✅ 有効/無効切替", command=self.toggle_rule_status).pack(side=tk.LEFT, padx=2)

        # Rules Treeview
        tree_frame = ttk.Frame(self.rules_frame, padding=4)
        tree_frame.pack(fill=tk.BOTH, expand=True)

        cols = ("idx", "type", "before", "after", "status", "desc")
        self.rules_tree = ttk.Treeview(tree_frame, columns=cols, show="headings", selectmode="browse")
        self.rules_tree.heading("idx", text="#")
        self.rules_tree.heading("type", text="種別")
        self.rules_tree.heading("before", text="置換前 (誤読パターン)")
        self.rules_tree.heading("after", text="置換後 (正しい文字列)")
        self.rules_tree.heading("status", text="状態")
        self.rules_tree.heading("desc", text="説明/備考")

        self.rules_tree.column("idx", width=40, anchor="center")
        self.rules_tree.column("type", width=90, anchor="center")
        self.rules_tree.column("before", width=220, anchor="w")
        self.rules_tree.column("after", width=220, anchor="w")
        self.rules_tree.column("status", width=60, anchor="center")
        self.rules_tree.column("desc", width=180, anchor="w")

        vscroll = ttk.Scrollbar(tree_frame, orient=tk.VERTICAL, command=self.rules_tree.yview)
        self.rules_tree.configure(yscrollcommand=vscroll.set)
        vscroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.rules_tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        self.rules_tree.bind("<Double-1>", lambda e: self.edit_rule())

        # Live Conversion Test Panel
        test_panel = ttk.LabelFrame(self.rules_frame, text="🧪 リアルタイム変換テスト", padding=8)
        test_panel.pack(fill=tk.X, padx=6, pady=6)

        t_grid = ttk.Frame(test_panel)
        t_grid.pack(fill=tk.X)

        ttk.Label(t_grid, text="変換前テスト文字列:").grid(row=0, column=0, sticky="w")
        self.test_input_var = tk.StringVar(value="サプライチエーンの再構築と、2019年2月27日の動向")
        self.test_input_entry = ttk.Entry(t_grid, textvariable=self.test_input_var, width=50)
        self.test_input_entry.grid(row=0, column=1, sticky="ew", padx=6, pady=2)
        self.test_input_entry.bind("<KeyRelease>", lambda e: self.update_live_test())

        ttk.Label(t_grid, text="変換後プレビュー:").grid(row=1, column=0, sticky="w")
        self.test_output_var = tk.StringVar()
        self.test_output_entry = ttk.Entry(t_grid, textvariable=self.test_output_var, width=50, state="readonly")
        self.test_output_entry.grid(row=1, column=1, sticky="ew", padx=6, pady=2)

        t_grid.columnconfigure(1, weight=1)

        self.render_rules_tree()
        self.update_live_test()

    def render_rules_tree(self):
        for item in self.rules_tree.get_children():
            self.rules_tree.delete(item)

        idx = 1
        # 1. Exact replacements
        for src, tgt in self.dict_service.exact_replacements.items():
            self.rules_tree.insert("", "end", values=(idx, "完全一致", src, tgt, "有効", "単語個別置換"), tags=("exact", src))
            idx += 1

        # 2. Regex replacements
        for r_idx, r in enumerate(self.dict_service.regex_replacements):
            st = "有効" if r.get("enabled", True) else "無効"
            self.rules_tree.insert("", "end", values=(idx, "正規表現", r.get("pattern", ""), r.get("replacement", ""), st, r.get("description", "")), tags=("regex", str(r_idx)))
            idx += 1

    def update_live_test(self):
        inp = self.test_input_var.get()
        out = self.dict_service.apply(inp)
        self.test_output_var.set(out)

    def add_rule(self):
        dlg = RuleEditDialog(self)
        self.wait_window(dlg)
        if dlg.result:
            r = dlg.result
            if r["type"] == "exact":
                self.dict_service.exact_replacements[r["before"]] = r["after"]
            else:
                self.dict_service.regex_replacements.append({
                    "pattern": r["before"],
                    "replacement": r["after"],
                    "enabled": r["enabled"],
                    "description": r["description"]
                })
            self.render_rules_tree()
            self.update_live_test()

    def edit_rule(self):
        sel = self.rules_tree.selection()
        if not sel:
            messagebox.showinfo("情報", "編集するルールを選択してください。", parent=self)
            return
        item = self.rules_tree.item(sel[0])
        tags = item.get("tags", ())
        if not tags:
            return
        
        r_type = tags[0]
        if r_type == "exact":
            src = tags[1]
            tgt = self.dict_service.exact_replacements.get(src, "")
            dlg = RuleEditDialog(self, {"type": "exact", "before": src, "after": tgt, "enabled": True})
            self.wait_window(dlg)
            if dlg.result:
                r = dlg.result
                if r["before"] != src and src in self.dict_service.exact_replacements:
                    del self.dict_service.exact_replacements[src]
                if r["type"] == "exact":
                    self.dict_service.exact_replacements[r["before"]] = r["after"]
                else:
                    self.dict_service.regex_replacements.append({
                        "pattern": r["before"],
                        "replacement": r["after"],
                        "enabled": r["enabled"],
                        "description": r["description"]
                    })
        else:
            r_idx = int(tags[1])
            if 0 <= r_idx < len(self.dict_service.regex_replacements):
                curr = self.dict_service.regex_replacements[r_idx]
                dlg = RuleEditDialog(self, {
                    "type": "regex",
                    "before": curr.get("pattern", ""),
                    "after": curr.get("replacement", ""),
                    "description": curr.get("description", ""),
                    "enabled": curr.get("enabled", True)
                })
                self.wait_window(dlg)
                if dlg.result:
                    r = dlg.result
                    self.dict_service.regex_replacements[r_idx] = {
                        "pattern": r["before"],
                        "replacement": r["after"],
                        "enabled": r["enabled"],
                        "description": r["description"]
                    }
        self.render_rules_tree()
        self.update_live_test()

    def delete_rule(self):
        sel = self.rules_tree.selection()
        if not sel:
            messagebox.showinfo("情報", "削除するルールを選択してください。", parent=self)
            return
        if not messagebox.askyesno("確認", "選択したルールを削除しますか？", parent=self):
            return
        item = self.rules_tree.item(sel[0])
        tags = item.get("tags", ())
        if not tags:
            return
        
        r_type = tags[0]
        if r_type == "exact":
            src = tags[1]
            if src in self.dict_service.exact_replacements:
                del self.dict_service.exact_replacements[src]
        else:
            r_idx = int(tags[1])
            if 0 <= r_idx < len(self.dict_service.regex_replacements):
                self.dict_service.regex_replacements.pop(r_idx)
        self.render_rules_tree()
        self.update_live_test()

    def move_rule(self, direction):
        sel = self.rules_tree.selection()
        if not sel:
            return
        item = self.rules_tree.item(sel[0])
        tags = item.get("tags", ())
        if not tags or tags[0] != "regex":
            return
        r_idx = int(tags[1])
        new_idx = r_idx + direction
        if 0 <= new_idx < len(self.dict_service.regex_replacements):
            self.dict_service.regex_replacements[r_idx], self.dict_service.regex_replacements[new_idx] = (
                self.dict_service.regex_replacements[new_idx], self.dict_service.regex_replacements[r_idx]
            )
            self.render_rules_tree()
            self.update_live_test()

    def toggle_rule_status(self):
        sel = self.rules_tree.selection()
        if not sel:
            return
        item = self.rules_tree.item(sel[0])
        tags = item.get("tags", ())
        if not tags or tags[0] != "regex":
            return
        r_idx = int(tags[1])
        if 0 <= r_idx < len(self.dict_service.regex_replacements):
            curr_state = self.dict_service.regex_replacements[r_idx].get("enabled", True)
            self.dict_service.regex_replacements[r_idx]["enabled"] = not curr_state
            self.render_rules_tree()
            self.update_live_test()

    def save_all(self):
        ok1 = self.dict_service.save()
        ok2 = self.tcy_service.save()
        if ok1 and ok2:
            self.global_status_lbl.config(text="✅ 補正辞書および縦中横レジストリを正常に保存・適用しました。")
            if self.on_saved:
                self.on_saved()
            messagebox.showinfo("保存完了", "OCR補正辞書および縦中横登録設定を保存しました。\n今後のOCR認識に自動適用されます。", parent=self)
        else:
            messagebox.showerror("エラー", "保存中にエラーが発生しました。", parent=self)
