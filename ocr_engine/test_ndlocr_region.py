
from pathlib import Path
import subprocess
import json
import shutil

import numpy as np
import pypdfium2
from PIL import Image


# ============================================================
# 設定
# ============================================================

PDF_PATH = Path(
    r"C:\Users\natur\source\book_ocr_pipeline\test.pdf"
)

OUTPUT_DIR = Path(
    r"C:\Users\natur\source\repos\OCR_Translator\ocr_engine\ndlocr_region_test"
)

PAGE_INDEX = 0       # 0 = PDFの1ページ目

# 元ページ上のテスト領域
# 今回はtest.jsonで縦書き本文が検出されていた周辺を使用
REGION_X = 650
REGION_Y = 700
REGION_W = 180
REGION_H = 460

RENDER_DPI = 150


# ============================================================
# 出力フォルダ
# ============================================================

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

region_image_path = OUTPUT_DIR / "region_01.png"
region_json_path = OUTPUT_DIR / "region_01.json"


# ============================================================
# PDFをページ画像としてレンダリング
# ============================================================

print("PDF読み込み中...")

pdf = pypdfium2.PdfDocument(str(PDF_PATH))

if PAGE_INDEX >= len(pdf):
    raise RuntimeError(
        f"ページ番号が範囲外です。PDFページ数={len(pdf)}"
    )

page = pdf[PAGE_INDEX]

scale = RENDER_DPI / 72.0

bitmap = page.render(scale=scale)
pil_image = bitmap.to_pil().convert("RGB")

pdf.close()

print(
    f"ページ画像サイズ: "
    f"{pil_image.width} x {pil_image.height}"
)


# ============================================================
# 領域を切り出す
# ============================================================

x1 = REGION_X
y1 = REGION_Y
x2 = REGION_X + REGION_W
y2 = REGION_Y + REGION_H

print()
print("OCR領域:")
print(f"x={x1}-{x2}")
print(f"y={y1}-{y2}")

if x1 < 0 or y1 < 0 or x2 > pil_image.width or y2 > pil_image.height:
    raise RuntimeError(
        "指定領域がページ画像の範囲外です。"
    )

region = pil_image.crop((x1, y1, x2, y2))

region.save(region_image_path)

print()
print("領域画像保存:")
print(region_image_path)


# ============================================================
# NDLOCR-Lite実行
# ============================================================

ndlocr_command = shutil.which("ndlocr-lite")

if ndlocr_command is None:
    raise RuntimeError(
        "ndlocr-lite コマンドが見つかりません。"
    )

print()
print("NDLOCR-Lite実行中...")

command = [
    ndlocr_command,
    "--sourceimg",
    str(region_image_path),
    "--output",
    str(OUTPUT_DIR),
    "--device",
    "cpu",
    "--json-only",
]

print("実行コマンド:")
print(" ".join(f'"{x}"' if " " in x else x for x in command))
print()

result = subprocess.run(
    command,
    capture_output=True,
    text=True,
    encoding="utf-8",
    errors="replace",
)

print(result.stdout)

if result.returncode != 0:
    print(result.stderr)
    raise RuntimeError(
        f"NDLOCR-Liteがエラー終了しました。returncode={result.returncode}"
    )


# ============================================================
# JSONを探す
# ============================================================

generated_json = OUTPUT_DIR / "region_01.json"

if not generated_json.exists():
    # NDLOCR-Liteの入力ファイル名によって名前が変わる可能性があるため、
    # JSONを検索する
    json_files = list(OUTPUT_DIR.glob("*.json"))

    if not json_files:
        raise RuntimeError(
            "NDLOCR-LiteのJSON結果が見つかりません。"
        )

    generated_json = json_files[0]


# ============================================================
# JSON読み込み
# ============================================================

with open(
    generated_json,
    "r",
    encoding="utf-8",
) as f:
    data = json.load(f)


# ============================================================
# 元ページ座標へ戻して表示
# ============================================================

print()
print("=" * 60)
print("領域OCR結果")
print("=" * 60)

contents = data.get("contents", [])

if not contents:
    print("OCR結果がありません。")
else:
    lines = contents[0]

    for line in lines:

        text = line.get("text", "")

        bbox = line.get("boundingBox", [])

        is_vertical = line.get(
            "isVertical",
            "false"
        )

        confidence = line.get(
            "confidence",
            0
        )

        if len(bbox) != 4:
            continue

        # NDLOCR-Liteの座標は切り出した領域内の座標。
        # 元ページの座標へ戻す。
        original_bbox = []

        for point in bbox:
            px = point[0] + REGION_X
            py = point[1] + REGION_Y

            original_bbox.append(
                [px, py]
            )

        print()
        print(f"文字列      : {text}")
        print(f"縦書き      : {is_vertical}")
        print(f"confidence  : {confidence}")
        print(f"領域内座標  : {bbox}")
        print(f"元ページ座標: {original_bbox}")


print()
print("=" * 60)
print("領域OCRテスト完了")
print("=" * 60)