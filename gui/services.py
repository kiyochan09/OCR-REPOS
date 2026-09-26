import os
import sys
import json
import re
import shutil
import subprocess
import threading
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import pypdfium2 as pdfium
import xml.etree.ElementTree as ET

class UserDictService:
    def __init__(self, config_file=None):
        if config_file is None:
            self.config_file = Path(__file__).resolve().parent.parent / "ocr_engine" / "config" / "user_dictionary.json"
        else:
            self.config_file = Path(config_file)
        self.exact_replacements = {}
        self.regex_replacements = []
        self.load()

    def load(self):
        if self.config_file.exists():
            try:
                with open(self.config_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    self.exact_replacements = data.get("exact_replacements", {})
                    self.regex_replacements = data.get("regex_replacements", [])
                    return True
            except Exception as e:
                print(f"[UserDictService] Load error: {e}")
        return False

    def save(self):
        self.config_file.parent.mkdir(parents=True, exist_ok=True)
        data = {
            "_comment": "ユーザー定義OCR補正辞書。ここに登録した用語・正規表現はOCR認識結果に自動適用されます。",
            "exact_replacements": self.exact_replacements,
            "regex_replacements": self.regex_replacements
        }
        try:
            with open(self.config_file, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            return True
        except Exception as e:
            print(f"[UserDictService] Save error: {e}")
            return False

    def apply(self, text: str) -> str:
        if not text:
            return text
        result = text
        # 1. Exact replacements
        for src, tgt in self.exact_replacements.items():
            if src and src in result:
                result = result.replace(src, tgt)
        # 2. Regex replacements
        for rule in self.regex_replacements:
            if isinstance(rule, dict) and rule.get("enabled", True):
                pattern = rule.get("pattern", "")
                replacement = rule.get("replacement", "")
                if pattern:
                    try:
                        result = re.sub(pattern, replacement, result)
                    except Exception as e:
                        print(f"[UserDictService] Regex error for pattern '{pattern}': {e}")
        return result


class TcyRegistryService:
    def __init__(self, templates_dir=None):
        if templates_dir is None:
            self.templates_dir = Path(__file__).resolve().parent.parent / "ocr_engine" / "config" / "tcy_templates"
        else:
            self.templates_dir = Path(templates_dir)
        self.templates_dir.mkdir(parents=True, exist_ok=True)
        self.registry_file = self.templates_dir / "tcy_registry.json"
        self.templates = {}
        self.load()

    def load(self):
        if self.registry_file.exists():
            try:
                with open(self.registry_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    self.templates = data.get("templates", {})
                    return True
            except Exception as e:
                print(f"[TcyRegistryService] Load error: {e}")
        # Initialize default if empty
        if not self.templates:
            self.rebuild_default_registry()
        return False

    def rebuild_default_registry(self):
        self.templates = {}
        for i in range(100):
            val_str = f"{i:02d}" if i < 10 else str(i)
            prim = f"{val_str}.png"
            images = [prim]
            if (self.templates_dir / f"{val_str}_gothic.png").exists():
                images.append(f"{val_str}_gothic.png")
            if (self.templates_dir / f"{val_str}_arial.png").exists():
                images.append(f"{val_str}_arial.png")
            self.templates[str(i)] = {
                "value": str(i),
                "enabled": True,
                "primary_image": prim,
                "images": images,
                "description": f"{i} (縦中横)"
            }
        self.save()

    def save(self):
        data = {
            "_comment": "縦中横（10〜99）数字・画像登録辞書。各数値に対応する基準画像ファイルを登録できます。",
            "templates": self.templates
        }
        try:
            with open(self.registry_file, "w", encoding="utf-8") as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            return True
        except Exception as e:
            print(f"[TcyRegistryService] Save error: {e}")
            return False

    def get_template_path(self, img_name: str) -> Path:
        return self.templates_dir / img_name

    def register_custom_image(self, val_key: str, src_path: str):
        val_str = str(val_key)
        if not os.path.exists(src_path):
            return False
        dest_filename = f"custom_{val_str}_{Path(src_path).name}"
        dest_path = self.templates_dir / dest_filename
        
        # Open, convert to standard grayscale, trim ink, and save
        try:
            im = Image.open(src_path).convert("L")
            np_t = np.array(im)
            ink_y, ink_x = np.where(np_t < 200)
            if len(ink_y) > 0:
                crop = im.crop((ink_x.min(), ink_y.min(), ink_x.max() + 1, ink_y.max() + 1))
                crop.save(str(dest_path))
            else:
                shutil.copyfile(src_path, str(dest_path))

            if val_str not in self.templates:
                self.templates[val_str] = {
                    "value": val_str,
                    "enabled": True,
                    "primary_image": dest_filename,
                    "images": [dest_filename],
                    "description": f"{val_str} (縦中横)"
                }
            else:
                self.templates[val_str]["primary_image"] = dest_filename
                if dest_filename not in self.templates[val_str]["images"]:
                    self.templates[val_str]["images"].insert(0, dest_filename)
            self.save()
            return True
        except Exception as e:
            print(f"[TcyRegistryService] register_custom_image error: {e}")
            return False

    def generate_font_image(self, val_key: str, font_name="msmincho.ttc", font_size=32):
        val_str = str(val_key)
        font_dir = Path("C:/Windows/Fonts")
        fpath = font_dir / font_name
        if not fpath.exists():
            fpath = font_dir / "msgothic.ttc"
        try:
            font = ImageFont.truetype(str(fpath), font_size)
            im = Image.new("L", (80, 80), 255)
            draw = ImageDraw.Draw(im)
            bbox = draw.textbbox((0, 0), val_str, font=font)
            w = bbox[2] - bbox[0]
            h = bbox[3] - bbox[1]
            draw.text(((80 - w) // 2, (80 - h) // 2), val_str, font=font, fill=0)
            np_t = np.array(im)
            ink_y, ink_x = np.where(np_t < 200)
            if len(ink_y) > 0:
                crop = im.crop((ink_x.min(), ink_y.min(), ink_x.max() + 1, ink_y.max() + 1))
                dest_filename = f"{val_str}.png"
                dest_path = self.templates_dir / dest_filename
                crop.save(str(dest_path))
                if val_str in self.templates:
                    self.templates[val_str]["primary_image"] = dest_filename
                self.save()
                return True
        except Exception as e:
            print(f"[TcyRegistryService] generate_font_image error: {e}")
        return False


class PdfService:
    def __init__(self):
        self.doc = None
        self.pdf_path = None
        self.page_count = 0

    def open(self, pdf_path: str):
        self.close()
        self.pdf_path = pdf_path
        self.doc = pdfium.PdfDocument(pdf_path)
        self.page_count = len(self.doc)
        return self.page_count

    def close(self):
        if self.doc is not None:
            try:
                self.doc.close()
            except Exception:
                pass
            self.doc = None
            self.pdf_path = None
            self.page_count = 0

    def render_page(self, page_index: int, dpi: float = 300.0) -> Image.Image:
        if self.doc is None or page_index < 0 or page_index >= self.page_count:
            return None
        scale = dpi / 72.0
        page = self.doc[page_index]
        bitmap = page.render(scale=scale, rotation=0)
        pil_img = bitmap.to_pil()
        return pil_img

    def get_page_size(self, page_index: int):
        if self.doc is None or page_index < 0 or page_index >= self.page_count:
            return 0, 0
        page = self.doc[page_index]
        return page.get_width(), page.get_height()


class OcrRunnerService:
    def __init__(self, python_exe=None):
        if python_exe is None:
            self.python_exe = Path(__file__).resolve().parent.parent / "ocr_engine" / "venv" / "Scripts" / "python.exe"
            if not self.python_exe.exists():
                self.python_exe = Path(sys.executable)
        else:
            self.python_exe = Path(python_exe)
        self.ocr_script = Path(__file__).resolve().parent.parent / "ocr_engine" / "ocr.py"
        self.user_dict_service = UserDictService()

    def run_page_ocr(self, page_img: Image.Image, page_num: int, output_dir: str, options: dict = None, progress_cb=None):
        """
        Runs OCR on a given page PIL image synchronously.
        Saves temporary page image, runs ocr.py, parses results.
        """
        options = options or {}
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        temp_img_path = output_dir / f"page_{page_num:04d}.png"
        page_img.save(str(temp_img_path))
        
        cmd = [
            str(self.python_exe),
            str(self.ocr_script),
            "--sourceimg", str(temp_img_path),
            "--output", str(output_dir)
        ]
        
        if options.get("enable_tcy", True):
            cmd.append("--enable-tcy")

        render_dpi = options.get("render_dpi", 300)
        cmd.extend(["--pdf-render-dpi", str(render_dpi)])

        if progress_cb:
            progress_cb(f"P.{page_num} OCRプロセス起動中...")

        process = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            cwd=str(self.ocr_script.parent)
        )

        log_lines = []
        for line in process.stdout:
            log_lines.append(line)
            if progress_cb:
                progress_cb(line.strip())

        process.wait()

        # Parse outputs
        stem = f"page_{page_num:04d}"
        xml_path = output_dir / f"{stem}.xml"
        json_path = output_dir / f"{stem}.json"
        txt_path = output_dir / f"{stem}.txt"

        parsed = self.parse_ocr_result(xml_path, json_path, txt_path, page_img, output_dir, page_num)
        parsed["logs"] = "".join(log_lines)
        return parsed

    def parse_ocr_result(self, xml_path: Path, json_path: Path, txt_path: Path, page_img: Image.Image, output_dir: Path, page_num: int):
        headings = []
        body_lines = []
        tables = []
        annotations = []
        figures = []
        line_boxes = []

        img_w, img_h = page_img.size if page_img else (1000, 1000)

        # Parse JSON if exists for accurate bounding boxes
        if json_path.exists():
            try:
                with open(json_path, "r", encoding="utf-8") as f:
                    js_data = json.load(f)
                    contents = js_data.get("contents", [[]])[0]
                    for item in contents:
                        text = item.get("text", "")
                        # Apply user dict replacements
                        text = self.user_dict_service.apply(text)
                        bbox = item.get("boundingBox", [])
                        c_idx = item.get("class_index", 1)
                        is_vert = item.get("isVertical", "false") == "true"
                        conf = item.get("confidence", 1.0)
                        
                        box_data = {
                            "text": text,
                            "box": bbox,
                            "class_index": c_idx,
                            "is_vertical": is_vert,
                            "confidence": conf
                        }
                        line_boxes.append(box_data)

                        # Classification (0: 本文, 1: 見出し, 2: 表, 3: 注釈, etc.)
                        # Standard NDL mapping: 0=図/写真, 1=本文, 2=見出し, 3=キャプション/注釈, 4=表
                        # If XML is also parsed, XML structure gives exact blocks
            except Exception as e:
                print(f"[OcrRunnerService] JSON parse error: {e}")

        # Parse XML for hierarchical blocks
        if xml_path.exists():
            try:
                tree = ET.parse(str(xml_path))
                root = tree.getroot()
                
                # Check all lines by type
                for line in root.findall(".//LINE"):
                    l_type = line.get("TYPE", "本文")
                    l_str = line.get("STRING", "")
                    l_str = self.user_dict_service.apply(l_str)
                    
                    if "見出し" in l_type or l_type == "大見出し" or l_type == "中見出し" or l_type == "小見出し":
                        headings.append(l_str)
                    elif "注" in l_type or "キャプション" in l_type:
                        annotations.append(l_str)
                    else:
                        body_lines.append(l_str)

                # Extract figure crops if figure tags exist
                for idx, fig in enumerate(root.findall(".//FIGURE") + root.findall(".//IMAGE")):
                    fx = int(fig.get("X", 0))
                    fy = int(fig.get("Y", 0))
                    fw = int(fig.get("WIDTH", 0))
                    fh = int(fig.get("HEIGHT", 0))
                    if fw > 20 and fh > 20 and page_img:
                        crop_im = page_img.crop((fx, fy, fx + fw, fy + fh))
                        fig_file = output_dir / f"p{page_num:04d}_fig_{idx+1}.png"
                        crop_im.save(str(fig_file))
                        figures.append(str(fig_file))

            except Exception as e:
                print(f"[OcrRunnerService] XML parse error: {e}")

        # Fallback to text file if body lines are empty
        if not body_lines and txt_path.exists():
            try:
                with open(txt_path, "r", encoding="utf-8") as f:
                    txt = f.read()
                    txt = self.user_dict_service.apply(txt)
                    body_lines = txt.split("\n")
            except Exception:
                pass

        return {
            "page_num": page_num,
            "headings": headings,
            "body_text": "\n".join(body_lines),
            "tables": tables,
            "annotations": annotations,
            "figures": figures,
            "line_boxes": line_boxes
        }
