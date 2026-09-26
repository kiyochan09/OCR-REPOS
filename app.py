import os
import sys
from pathlib import Path
import tkinter as tk

# Force UTF-8 encoding on Windows
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    try:
        import ctypes
        # Set DPI awareness (Per monitor DPI aware)
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except Exception:
            ctypes.windll.user32.SetProcessDPIAware()
    except Exception:
        pass

# Add repository root to path
base_dir = Path(__file__).resolve().parent
sys.path.insert(0, str(base_dir))

from gui.main_window import MainWindow
from gui.settings_manager import SettingsManager

def main():
    root = tk.Tk()
    root.title("OCR Translator - Python Edition")
    
    settings_mgr = SettingsManager()
    geom = settings_mgr.get("window_geometry", "1320x860")
    root.geometry(geom)
    root.minsize(980, 640)

    app = MainWindow(root)

    def on_closing():
        # Save geometry
        try:
            settings_mgr.set("window_geometry", root.geometry())
        except Exception:
            pass
        root.destroy()

    root.protocol("WM_DELETE_WINDOW", on_closing)
    root.mainloop()

if __name__ == "__main__":
    main()
