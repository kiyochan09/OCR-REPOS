import json
import sys
import cv2
import numpy as np
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter
from paddleocr import PaddleOCR


# ============================================================
# 設定
# ============================================================

INPUT_IMAGE = Path("ocr_input.png")
COLUMNS_JSON = Path("ocr_columns.json")

OUTPUT_JSON = Path("ocr_columns_result.json")
DEBUG_IMAGE = Path("ocr_columns_ocr_debug.png")

# 列の左右に少し余白を追加
COLUMN_MARGIN = 3


# ============================================================
# OCR結果を取り出す
# ============================================================

def extract_result_data(res):

    if hasattr(res, "json"):
        data = res.json

        if isinstance(data, str):
            data = json.loads(data)

    elif isinstance(res, dict):
        data = res

    else:
        return None

    if "res" in data:
        data = data["res"]

    return data


# ============================================================
# 列情報を読み込む
# ============================================================

def load_columns():

    with open(
        COLUMNS_JSON,
        "r",
        encoding="utf-8"
    ) as f:

        data = json.load(f)

    # {"columns": [...]}
    if isinstance(data, dict):

        if "columns" in data:
            columns = data["columns"]

        else:
            raise ValueError(
                "ocr_columns.json に columns がありません。"
            )

    # [...]
    elif isinstance(data, list):

        columns = data

    else:

        raise ValueError(
            "ocr_columns.json の形式が正しくありません。"
        )

    return columns


# ============================================================
# メイン
# ============================================================

print("========================================")
print("縦書き列別OCR")
print("========================================")
print()

# ------------------------------------------------------------
# 画像読み込み
# ------------------------------------------------------------

print("①画像を読み込みます")
print(f"画像: {INPUT_IMAGE}")

if not INPUT_IMAGE.exists():

    print(
        f"エラー: {INPUT_IMAGE} がありません。"
    )

    sys.exit(1)

image = Image.open(INPUT_IMAGE).convert("RGB")

print(
    f"元画像サイズ: {image.width} x {image.height}"
)

# ------------------------------------------------------------
# 列情報読み込み
# ------------------------------------------------------------

print()
print("②自動検出した列情報を読み込みます")

columns = load_columns()

print(
    f"自動検出列数: {len(columns)}"
)

# x座標で並べ替え
# ========================================
# 列を右 → 左の順番に並べる
# ========================================

columns = sorted(
    columns,
    key=lambda c: int(c["x1"]),
    reverse=True
)

print("OCR実行順（右→左）")

for i, column in enumerate(columns, start=1):
    print(
        f"{i}: "
        f"id={column['id']} "
        f"x1={column['x1']} "
        f"x2={column['x2']} "
        f"width={column['width']}"
    )

print()
print("OCR順序:")
print("右 → 左")

for col in columns:
    print(
        f"列 {col['id']}: "
        f"x1={col['x1']} "
        f"x2={col['x2']} "
        f"width={col['width']}"
    )


# ------------------------------------------------------------
# PaddleOCR
# ------------------------------------------------------------

print()
print("③PaddleOCRを起動します...")

ocr = PaddleOCR(
    lang="japan",
    enable_mkldnn=False,
)

print("PaddleOCRエンジン作成完了")


# ------------------------------------------------------------
# デバッグ画像
# ------------------------------------------------------------

debug_image = image.copy()

draw = ImageDraw.Draw(debug_image)


# ------------------------------------------------------------
# OCR結果
# ------------------------------------------------------------

all_results = []


# ============================================================
# 列ごとのOCR
# ============================================================

for column_no, col in enumerate(columns, start=1):

    column_results = []
    
    x = int(col["x1"])
    y = int(col.get("y", 0))

    width = int(col["width"])

    height = int(
        col.get(
            "height",
            image.height - y
        )
    )

    # 少し余白を追加
    x0 = max(
        0,
        x - COLUMN_MARGIN
    )

    y0 = max(
        0,
        y
    )

    x1 = min(
        image.width,
        x + width + COLUMN_MARGIN
    )

    y1 = min(
        image.height,
        y + height
    )

    crop = image.crop(
        (
            x0,
            y0,
            x1,
            y1
        )
    )

    # 列画像保存
    column_path = Path(
        f"ocr_column_{column_no:02d}.png"
    )

    crop.save(column_path)

    print()
    print("----------------------------------------")
    print(
        f"列 {column_no}/{len(columns)}"
    )

    print(
        f"範囲: x={x0}-{x1}, "
        f"y={y0}-{y1}"
    )

    print(
        f"画像保存: {column_path}"
    )

    # --------------------------------------------------------
    # デバッグ画像に列番号を描画
    # --------------------------------------------------------

    draw.rectangle(
        [x0, y0, x1, y1],
        outline="red",
        width=3
    )

    draw.text(
        (x0 + 3, y0 + 3),
        str(column_no),
        fill="red"
    )

    draw.text(
    (x0 + 3, y0 + 3),
    str(column_no),
    fill="red"
    )

    # --------------------------------------------------------
    # OCR用に縦書き列を90度回転
    # --------------------------------------------------------

    column_image = Image.open(column_path)

    ocr_image = column_image.rotate(
        90,
        expand=True
    )

    rotated_path = Path(
        f"ocr_column_{column_no:02d}_rotated.png"
    )

    ocr_image.save(rotated_path)

    print("OCR用回転画像保存:", rotated_path)

    # --------------------------------------------------------
    # OCR
    # --------------------------------------------------------

    print("OCR実行中...")

    result = ocr.predict(
        str(rotated_path)
    )

    # --------------------------------------------------------
    # 結果解析
    # --------------------------------------------------------

    for res in result:

        data = extract_result_data(res)

        if data is None:
            continue

        texts = data.get(
            "rec_texts",
            []
        )

        scores = data.get(
            "rec_scores",
            []
        )

        boxes = data.get(
            "rec_boxes",
            []
        )

        polys = data.get(
            "rec_polys",
            []
        )

        for i, text in enumerate(texts):

            score = (
                float(scores[i])
                if i < len(scores)
                else 0.0
            )

            box = None

            if i < len(boxes):

                try:
                    box = boxes[i].tolist()

                except AttributeError:
                    box = list(boxes[i])

            poly = None

            if i < len(polys):

                try:
                    poly = polys[i].tolist()

                except AttributeError:
                    poly = list(polys[i])

            column_results.append(
                {
                    "text": str(text),
                    "score": score,
                    "box": box,
                    "polygon": poly
                }
            )

    # --------------------------------------------------------
    # 縦書きなので上→下に並べる
    # --------------------------------------------------------

    column_results.sort(
        key=lambda r:
            r["box"][1]
            if r["box"] is not None
            else 0
    )

    print(
        f"列内検出数: {len(column_results)}"
    )

    for j, item in enumerate(
        column_results,
        start=1
    ):

        print(
            f"  {j}: "
            f"score={item['score']:.3f} "
            f"{item['text']}"
        )

    # --------------------------------------------------------
    # 列結果保存
    # --------------------------------------------------------

    all_results.append(
        {
            "column": column_no,
            "x": x,
            "y": y,
            "width": width,
            "height": height,
            "results": column_results
        }
    )


# ============================================================
# デバッグ画像保存
# ============================================================

debug_image.save(
    DEBUG_IMAGE
)

print()
print(
    f"④デバッグ画像保存: {DEBUG_IMAGE}"
)


# ============================================================
# JSON保存
# ============================================================

output = {
    "image": str(INPUT_IMAGE),
    "column_count": len(columns),
    "reading_order": "right_to_left",
    "columns": all_results
}

with open(
    OUTPUT_JSON,
    "w",
    encoding="utf-8"
) as f:

    json.dump(
        output,
        f,
        ensure_ascii=False,
        indent=2
    )


print(
    f"⑤保存完了: {OUTPUT_JSON}"
)


# ============================================================
# 全文を表示
# ============================================================

print()
print("========================================")
print("⑥縦書きOCR結果")
print("========================================")

for column in all_results:

    print()
    print(
        f"【列 {column['column']}】"
    )

    for item in column["results"]:

        print(
            item["text"],
            end=""
        )

    print()


print()
print("========================================")
print("完了しました")
print("========================================")