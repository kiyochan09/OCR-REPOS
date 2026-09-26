@echo off
chcp 65001 > nul
cd /d "%~dp0"
if exist "ocr_engine\venv\Scripts\python.exe" (
    start "" "ocr_engine\venv\Scripts\python.exe" app.py
) else (
    start "" python app.py
)
