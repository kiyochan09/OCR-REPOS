import os
import json
from pathlib import Path

DEFAULT_SETTINGS = {
    "font_family": "Meiryo UI",
    "font_size": 12,
    "font_bold": False,
    "ocr_zoom": "Fit",
    "ocr_orientation": "auto",
    "book_type": "和書",
    "render_dpi": 300,
    "column_deck": "auto",
    "enable_tcy": True,
    "chars_per_line": 0,
    "annotation_numbering": "通し番号",
    "insert_page_break": True,
    "default_export_dir": "",
    "window_geometry": "1320x860",
    "last_pdf_path": "",
    "batch_size": 20
}

class SettingsManager:
    def __init__(self, config_dir=None):
        if config_dir is None:
            self.config_dir = Path(__file__).resolve().parent.parent / "ocr_engine" / "config"
        else:
            self.config_dir = Path(config_dir)
        self.config_dir.mkdir(parents=True, exist_ok=True)
        self.settings_file = self.config_dir / "app_settings.json"
        self.settings = self.load_settings()

    def load_settings(self):
        if self.settings_file.exists():
            try:
                with open(self.settings_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                    merged = DEFAULT_SETTINGS.copy()
                    merged.update(data)
                    return merged
            except Exception as e:
                print(f"[SettingsManager] Failed to load settings: {e}")
        return DEFAULT_SETTINGS.copy()

    def save_settings(self):
        try:
            with open(self.settings_file, "w", encoding="utf-8") as f:
                json.dump(self.settings, f, ensure_ascii=False, indent=2)
            return True
        except Exception as e:
            print(f"[SettingsManager] Failed to save settings: {e}")
            return False

    def get(self, key, default=None):
        return self.settings.get(key, default if default is not None else DEFAULT_SETTINGS.get(key))

    def set(self, key, value):
        self.settings[key] = value
        self.save_settings()

    def reset_to_defaults(self):
        self.settings = DEFAULT_SETTINGS.copy()
        self.save_settings()
