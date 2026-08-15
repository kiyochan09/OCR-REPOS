import sys
import json
from pathlib import Path

import cv2
import numpy as np


# ========================================
# 設定
# ========================================

IMAGE_PATH = sys.argv[1] if len(sys.argv) >= 2 else "ocr_input.png"

OUTPUT_COLUMN_DEBUG = "ocr_columns_debug.png"
OUTPUT_COLUMN_INFO = "ocr_columns.json"


# ========================================
# 画像読み込み
# ========================================

print("画像を読み込みます")
print("画像:", IMAGE_PATH)

image = cv2.imread(IMAGE_PATH)

if image is None:
    print("画像を読み込めませんでした。")
    sys.exit(1)

height, width = image.shape[:2]

print(f"画像サイズ: {width} x {height}")


# ========================================
# グレースケール化
# ========================================

gray = cv2.cvtColor(
    image,
    cv2.COLOR_BGR2GRAY
)


# ========================================
# 二値化
# ========================================

_, binary = cv2.threshold(
    gray,
    200,
    255,
    cv2.THRESH_BINARY_INV
)


# ========================================
# 小さなノイズを除去
# ========================================

kernel = cv2.getStructuringElement(
    cv2.MORPH_RECT,
    (3, 3)
)

binary = cv2.morphologyEx(
    binary,
    cv2.MORPH_OPEN,
    kernel
)


# ========================================
# X方向の投影
#
# 各X座標にどれだけ黒い画素が
# 存在するかを調べる
# ========================================

projection = np.sum(
    binary > 0,
    axis=0
)


# ========================================
# 列検出用のしきい値
#
# 画像高さに対する割合で決める
# ========================================

threshold = height * 0.015

print()
print("列検出しきい値:", threshold)


# ========================================
# 黒画素があるX範囲を取得
# ========================================

active = projection > threshold


# ========================================
# 連続区間を取得
# ========================================

runs = []

start = None

for x in range(width):

    if active[x]:

        if start is None:
            start = x

    else:

        if start is not None:

            runs.append(
                (start, x - 1)
            )

            start = None


if start is not None:

    runs.append(
        (start, width - 1)
    )


print()
print("初期検出区間数:", len(runs))


# ========================================
# 近すぎる区間を結合
# ========================================

MERGE_GAP = 25

merged = []

for start, end in runs:

    if not merged:

        merged.append(
            [start, end]
        )

        continue

    previous = merged[-1]

    gap = start - previous[1] - 1

    if gap <= MERGE_GAP:

        previous[1] = end

    else:

        merged.append(
            [start, end]
        )


runs = merged


# ========================================
# 小さすぎる区間を除外
# ========================================

MIN_WIDTH = 25

columns = []

for start, end in runs:

    w = end - start + 1

    if w >= MIN_WIDTH:

        columns.append(
            [start, end]
        )


print()
print("自動検出された列数:", len(columns))


# ========================================
# 列を表示
# ========================================

for i, (x1, x2) in enumerate(columns, start=1):

    print(
        f"列 {i}: "
        f"x={x1} - {x2} "
        f"width={x2-x1+1}"
    )


# ========================================
# 列画像を少し広げる
#
# OCR用に左右へ余白を追加する
# ========================================

PADDING = 8

column_data = []


for i, (x1, x2) in enumerate(columns, start=1):

    crop_x1 = max(
        0,
        x1 - PADDING
    )

    crop_x2 = min(
        width,
        x2 + PADDING + 1
    )

    crop = image[
        0:height,
        crop_x1:crop_x2
    ]

    filename = (
        f"ocr_column_{i:02d}.png"
    )

    cv2.imwrite(
        filename,
        crop
    )

    column_data.append(
        {
            "id": i,
            "x1": crop_x1,
            "x2": crop_x2,
            "width": crop_x2 - crop_x1,
            "height": height,
            "image": filename
        }
    )


# ========================================
# デバッグ画像
# ========================================

debug = image.copy()


for i, column in enumerate(
    column_data,
    start=1
):

    x1 = column["x1"]
    x2 = column["x2"]

    cv2.rectangle(
        debug,
        (x1, 0),
        (x2, height - 1),
        (0, 0, 255),
        3
    )

    cv2.putText(
        debug,
        str(i),
        (x1 + 5, 40),
        cv2.FONT_HERSHEY_SIMPLEX,
        1.2,
        (0, 0, 255),
        3
    )


cv2.imwrite(
    OUTPUT_COLUMN_DEBUG,
    debug
)


# ========================================
# 列情報を保存
# ========================================

with open(
    OUTPUT_COLUMN_INFO,
    "w",
    encoding="utf-8"
) as f:

    json.dump(
        {
            "image": IMAGE_PATH,
            "width": width,
            "height": height,
            "columns": column_data
        },
        f,
        ensure_ascii=False,
        indent=2
    )


print()
print(
    "デバッグ画像保存:",
    OUTPUT_COLUMN_DEBUG
)

print(
    "列情報保存:",
    OUTPUT_COLUMN_INFO
)

print()
print("完了しました。")