import tkinter as tk
from tkinter import ttk
from PIL import Image, ImageTk
import math

CLASS_COLORS = {
    0: ("#8E44AD", "図/写真"),      # Purple
    1: ("#2980B9", "本文"),         # Blue
    2: ("#27AE60", "見出し"),       # Green
    3: ("#D35400", "注釈/キャプション"), # Orange
    4: ("#C0392B", "表"),           # Red
}

class CanvasViewer(ttk.Frame):
    def __init__(self, parent, on_box_clicked=None, *args, **kwargs):
        super().__init__(parent, *args, **kwargs)
        self.on_box_clicked = on_box_clicked

        self.orig_image = None
        self.display_image = None
        self.photo_image = None
        
        self.scale = 1.0
        self.zoom_mode = "Fit"  # "Fit" or float scale
        self.pan_start_x = 0
        self.pan_start_y = 0
        
        self.boxes = []
        self.selected_box_idx = -1
        self.hovered_box_idx = -1

        self._create_widgets()

    def _create_widgets(self):
        # Canvas with scrollbars
        self.canvas = tk.Canvas(self, bg="#2C3E50", highlightthickness=0, cursor="hand2")
        self.v_scroll = ttk.Scrollbar(self, orient=tk.VERTICAL, command=self.canvas.yview)
        self.h_scroll = ttk.Scrollbar(self, orient=tk.HORIZONTAL, command=self.canvas.xview)
        self.canvas.configure(xscrollcommand=self.h_scroll.set, yscrollcommand=self.v_scroll.set)

        self.v_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.h_scroll.pack(side=tk.BOTTOM, fill=tk.X)
        self.canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)

        # Bind events
        self.canvas.bind("<Configure>", self._on_resize)
        self.canvas.bind("<ButtonPress-1>", self._on_left_press)
        self.canvas.bind("<B1-Motion>", self._on_drag)
        self.canvas.bind("<ButtonPress-2>", self._on_pan_start)
        self.canvas.bind("<B2-Motion>", self._on_drag)
        self.canvas.bind("<ButtonPress-3>", self._on_pan_start)
        self.canvas.bind("<B3-Motion>", self._on_drag)
        self.canvas.bind("<MouseWheel>", self._on_mouse_wheel)
        self.canvas.bind("<Motion>", self._on_mouse_motion)

    def set_image(self, pil_image, reset_zoom=True):
        self.orig_image = pil_image
        self.boxes = []
        self.selected_box_idx = -1
        self.hovered_box_idx = -1
        if reset_zoom and self.zoom_mode == "Fit":
            self.scale = self._calc_fit_scale()
        self.redraw()

    def set_boxes(self, boxes):
        self.boxes = boxes or []
        self.selected_box_idx = -1
        self.hovered_box_idx = -1
        self.redraw()

    def select_box(self, idx):
        self.selected_box_idx = idx
        self.redraw()

    def _calc_fit_scale(self):
        if not self.orig_image:
            return 1.0
        cw = max(10, self.canvas.winfo_width())
        ch = max(10, self.canvas.winfo_height())
        iw, ih = self.orig_image.size
        sx = (cw - 20) / iw
        sy = (ch - 20) / ih
        return max(0.1, min(sx, sy))

    def zoom_fit(self):
        self.zoom_mode = "Fit"
        self.scale = self._calc_fit_scale()
        self.redraw(center=True)

    def set_zoom(self, zoom_val):
        if isinstance(zoom_val, str) and zoom_val.endswith("%"):
            try:
                pct = float(zoom_val.rstrip("%"))
                self.zoom_mode = "Custom"
                self.scale = max(0.1, min(5.0, pct / 100.0))
            except Exception:
                self.zoom_fit()
        elif zoom_val == "Fit":
            self.zoom_fit()
        elif isinstance(zoom_val, (int, float)):
            self.zoom_mode = "Custom"
            self.scale = max(0.1, min(5.0, float(zoom_val)))
        self.redraw()

    def zoom_in(self):
        self.zoom_mode = "Custom"
        self.scale = min(5.0, self.scale * 1.25)
        self.redraw()

    def zoom_out(self):
        self.zoom_mode = "Custom"
        self.scale = max(0.1, self.scale / 1.25)
        self.redraw()

    def _on_resize(self, event):
        if self.zoom_mode == "Fit" and self.orig_image:
            self.scale = self._calc_fit_scale()
            self.redraw(center=True)

    def _on_mouse_wheel(self, event):
        if not self.orig_image:
            return
        # Windows: event.delta is typically 120 or -120
        factor = 1.15 if event.delta > 0 else (1.0 / 1.15)
        self.zoom_mode = "Custom"
        self.scale = max(0.1, min(5.0, self.scale * factor))
        self.redraw()

    def _on_pan_start(self, event):
        self.canvas.scan_mark(event.x, event.y)

    def _on_left_press(self, event):
        # Check if clicked on a bounding box
        cx = self.canvas.canvasx(event.x)
        cy = self.canvas.canvasy(event.y)
        
        clicked_idx = self._find_box_at(cx, cy)
        if clicked_idx != -1:
            self.selected_box_idx = clicked_idx
            self.redraw()
            if self.on_box_clicked and 0 <= clicked_idx < len(self.boxes):
                self.on_box_clicked(clicked_idx, self.boxes[clicked_idx])
        else:
            self.selected_box_idx = -1
            self.redraw()
            self._on_pan_start(event)

    def _on_drag(self, event):
        self.canvas.scan_dragto(event.x, event.y, gain=1)

    def _on_mouse_motion(self, event):
        cx = self.canvas.canvasx(event.x)
        cy = self.canvas.canvasy(event.y)
        idx = self._find_box_at(cx, cy)
        if idx != self.hovered_box_idx:
            self.hovered_box_idx = idx
            self.redraw_boxes_only()

    def _find_box_at(self, cx, cy):
        if not self.orig_image or not self.boxes:
            return -1
        # Convert canvas coords to orig image coords
        # Image is placed at (offset_x, offset_y)
        # Note: image top-left is at offset_x, offset_y
        for idx, item in enumerate(self.boxes):
            bbox = item.get("box", [])
            if len(bbox) >= 4:
                xs = [p[0] * self.scale + self.img_offset_x for p in bbox]
                ys = [p[1] * self.scale + self.img_offset_y for p in bbox]
                min_x, max_x = min(xs), max(xs)
                min_y, max_y = min(ys), max(ys)
                if min_x <= cx <= max_x and min_y <= cy <= max_y:
                    return idx
        return -1

    def redraw(self, center=False):
        self.canvas.delete("all")
        if not self.orig_image:
            self.canvas.create_text(
                self.canvas.winfo_width() // 2,
                self.canvas.winfo_height() // 2,
                text="PDFファイルを開いてください（上部ツールバーの「PDFを開く」）",
                fill="#BDC3C7",
                font=("Meiryo UI", 12)
            )
            return

        iw, ih = self.orig_image.size
        nw = max(1, int(iw * self.scale))
        nh = max(1, int(ih * self.scale))

        cw = max(10, self.canvas.winfo_width())
        ch = max(10, self.canvas.winfo_height())

        self.img_offset_x = max(10, (cw - nw) // 2) if cw > nw else 10
        self.img_offset_y = max(10, (ch - nh) // 2) if ch > nh else 10

        resized = self.orig_image.resize((nw, nh), Image.Resampling.BILINEAR)
        self.photo_image = ImageTk.PhotoImage(resized)

        self.canvas.create_image(self.img_offset_x, self.img_offset_y, image=self.photo_image, anchor="nw", tags="page_img")
        
        self.canvas.config(scrollregion=(
            0, 0,
            max(cw, nw + self.img_offset_x * 2),
            max(ch, nh + self.img_offset_y * 2)
        ))

        self.draw_boxes()

    def redraw_boxes_only(self):
        self.canvas.delete("box_overlay")
        self.draw_boxes()

    def draw_boxes(self):
        if not self.boxes or not self.orig_image:
            return

        for idx, item in enumerate(self.boxes):
            bbox = item.get("box", [])
            c_idx = item.get("class_index", 1)
            color, name = CLASS_COLORS.get(c_idx, ("#2980B9", "本文"))

            is_sel = (idx == self.selected_box_idx)
            is_hov = (idx == self.hovered_box_idx)

            if len(bbox) >= 4:
                xs = [p[0] * self.scale + self.img_offset_x for p in bbox]
                ys = [p[1] * self.scale + self.img_offset_y for p in bbox]
                min_x, max_x = min(xs), max(xs)
                min_y, max_y = min(ys), max(ys)

                outline_color = "#F39C12" if is_sel else ("#E74C3C" if is_hov else color)
                width = 3 if (is_sel or is_hov) else 1

                self.canvas.create_rectangle(
                    min_x, min_y, max_x, max_y,
                    outline=outline_color,
                    width=width,
                    tags="box_overlay"
                )

                # Show text badge on hover or selection
                if is_hov or is_sel:
                    txt = item.get("text", "")
                    if txt:
                        short_txt = txt[:15] + "..." if len(txt) > 15 else txt
                        self.canvas.create_text(
                            min_x, max(0, min_y - 12),
                            text=f"[{name}] {short_txt}",
                            fill="#FFFFFF",
                            anchor="w",
                            font=("Meiryo UI", 9, "bold"),
                            tags="box_overlay"
                        )
