import sys
from pathlib import Path

import cv2
import numpy as np


# ========================================
# 入力画像
# ========================================

IMAGE_PATH = (
    sys.argv[1]
    if len(sys.argv) > 1
    else "ocr_input.png"
)

OUTPUT_PATH = "vertical_columns_debug2.png"


print("①画像を読み込みます", flush=True)
print(f"画像: {IMAGE_PATH}", flush=True)


image = cv2.imread(
    IMAGE_PATH,
    cv2.IMREAD_COLOR
)

if image is None:
    raise FileNotFoundError(
        f"画像を読み込めません: {IMAGE_PATH}"
    )


height, width = image.shape[:2]

print(
    f"画像サイズ: {width} x {height}",
    flush=True
)


# ========================================
# グレースケール
# ========================================

gray = cv2.cvtColor(
    image,
    cv2.COLOR_BGR2GRAY
)


# ========================================
# 黒文字を抽出
# ========================================

binary = cv2.threshold(
    gray,
    180,
    255,
    cv2.THRESH_BINARY_INV
)[1]


# ========================================
# X方向の黒画素数を計算
#
# 縦書きなら、各列の位置で
# 黒画素が縦方向に多く存在する。
# ========================================

projection = np.sum(
    binary > 0,
    axis=0
)


# ========================================
# X方向に平滑化
# ========================================

kernel_size = 21

kernel = np.ones(
    kernel_size,
    dtype=np.float32
) / kernel_size

smooth = np.convolve(
    projection,
    kernel,
    mode="same"
)


# ========================================
# 閾値
#
# ページ高さに対して一定割合以上
# 黒画素がある場所を文字列候補とする。
# ========================================

threshold = height * 0.035

print(
    f"列検出閾値: {threshold:.1f}",
    flush=True
)


mask = smooth > threshold


# ========================================
# X方向の連続領域を取得
# ========================================

ranges = []

start = None

for x in range(width):

    if mask[x]:

        if start is None:
            start = x

    else:

        if start is not None:

            end = x

            ranges.append(
                (start, end)
            )

            start = None


if start is not None:

    ranges.append(
        (start, width)
    )


# ========================================
# 近接した領域を結合
# ========================================

merged = []

MERGE_GAP = 35

for start, end in ranges:

    if not merged:

        merged.append(
            [start, end]
        )

    else:

        previous = merged[-1]

        gap = start - previous[1]

        if gap <= MERGE_GAP:

            previous[1] = end

        else:

            merged.append(
                [start, end]
            )


# ========================================
# 列として妥当な幅だけ残す
# ========================================

columns = []

for start, end in merged:

    x = start
    w = end - start

    # 極端に細いものを除外
    if w < 35:
        continue

    # 極端に太いものを除外
    if w > 180:
        continue

    columns.append(
        (x, 0, w, height)
    )


# ========================================
# 右 → 左に並べる
# ========================================

columns.sort(
    key=lambda r: r[0],
    reverse=True
)


# ========================================
# 結果表示
# ========================================

print()
print("②縦書き列候補")
print(
    f"検出数: {len(columns)}",
    flush=True
)


for i, (x, y, w, h) in enumerate(
    columns,
    start=1
):

    print(
        f"{i:2d}: "
        f"x={x:4d}-{x+w:4d} "
        f"width={w:3d}",
        flush=True
    )


# ========================================
# デバッグ画像
# ========================================

debug = image.copy()


for i, (x, y, w, h) in enumerate(
    columns,
    start=1
):

    cv2.rectangle(
        debug,
        (x, y),
        (x + w, y + h),
        (0, 0, 255),
        3
    )

    cv2.putText(
        debug,
        str(i),
        (x + 5, 45),
        cv2.FONT_HERSHEY_SIMPLEX,
        1.2,
        (0, 0, 255),
        3
    )


# ========================================
# 保存
# ========================================

cv2.imwrite(
    OUTPUT_PATH,
    debug
)


print()
print(
    f"③デバッグ画像保存: {OUTPUT_PATH}",
    flush=True
)

print("④終了", flush=True)