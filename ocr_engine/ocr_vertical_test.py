import sys
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


# ========================================
# 設定
# ========================================

IMAGE_PATH = (
    sys.argv[1]
    if len(sys.argv) > 1
    else "ocr_input.png"
)

OUTPUT_DIR = Path("vertical_ocr_test")
OUTPUT_DIR.mkdir(exist_ok=True)


# ========================================
# 縦書き列を検出
# ========================================

def detect_vertical_columns(image):

    height, width = image.shape[:2]

    gray = cv2.cvtColor(
        image,
        cv2.COLOR_BGR2GRAY
    )

    # 黒文字を抽出
    binary = cv2.threshold(
        gray,
        180,
        255,
        cv2.THRESH_BINARY_INV
    )[1]

    # X方向の黒画素数
    projection = np.sum(
        binary > 0,
        axis=0
    )

    # 平滑化
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

    # 列検出閾値
    threshold = height * 0.035

    mask = smooth > threshold

    # 連続領域
    ranges = []

    start = None

    for x in range(width):

        if mask[x]:

            if start is None:
                start = x

        else:

            if start is not None:

                ranges.append(
                    (start, x)
                )

                start = None

    if start is not None:

        ranges.append(
            (start, width)
        )

    # 近接領域を結合
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

    # 列として妥当な幅
    columns = []

    for start, end in merged:

        width_col = end - start

        if width_col < 35:
            continue

        if width_col > 180:
            continue

        columns.append(
            (
                start,
                0,
                width_col,
                height
            )
        )

    # 縦書きは右から左
    columns.sort(
        key=lambda r: r[0],
        reverse=True
    )

    return columns


# ========================================
# 列画像を切り出して回転
# ========================================

def make_column_image(
    image,
    column,
    index
):

    x, y, w, h = column

    # 少し余白を追加
    margin_x = 8
    margin_y = 5

    x1 = max(
        0,
        x - margin_x
    )

    y1 = max(
        0,
        y - margin_y
    )

    x2 = min(
        image.shape[1],
        x + w + margin_x
    )

    y2 = min(
        image.shape[0],
        y + h + margin_y
    )

    crop = image[
        y1:y2,
        x1:x2
    ]

    # OpenCV → PIL
    pil_image = Image.fromarray(
        cv2.cvtColor(
            crop,
            cv2.COLOR_BGR2RGB
        )
    )

    # 縦書きを横書きにする
    #
    # 90度反時計回り
    rotated = pil_image.rotate(
        90,
        expand=True
    )

    output_path = (
        OUTPUT_DIR /
        f"vertical_col_{index:02d}.png"
    )

    rotated.save(
        output_path
    )

    return output_path


# ========================================
# PaddleOCR
# ========================================

def run_ocr(image_path):

    from paddleocr import PaddleOCR

    print(
        "PaddleOCRエンジンを作成します...",
        flush=True
    )

    ocr = PaddleOCR(
        lang="japan",
        enable_mkldnn=False,
    )

    print(
        "PaddleOCRエンジン作成完了",
        flush=True
    )

    print(
        f"OCR: {image_path}",
        flush=True
    )

    result = ocr.predict(
        str(image_path)
    )

    print(
        "OCR完了",
        flush=True
    )

    results = []

    for res in result:

        if hasattr(res, "json"):

            data = res.json

            if isinstance(data, str):

                data = json.loads(data)

        elif isinstance(res, dict):

            data = res

        else:

            continue

        if "res" in data:

            data = data["res"]

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

            polygon = None

            if i < len(polys):

                try:
                    polygon = polys[i].tolist()

                except AttributeError:
                    polygon = list(polys[i])

            results.append(
                {
                    "text": str(text),
                    "score": score,
                    "box": box,
                    "polygon": polygon
                }
            )

    return results


# ========================================
# メイン処理
# ========================================

def main():

    print(
        "========================================",
        flush=True
    )

    print(
        "縦書き列 → 回転 → PaddleOCR テスト",
        flush=True
    )

    print(
        "========================================",
        flush=True
    )

    print(
        f"入力画像: {IMAGE_PATH}",
        flush=True
    )

    # ------------------------------------
    # 画像読み込み
    # ------------------------------------

    image = cv2.imread(
        IMAGE_PATH
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

    # ------------------------------------
    # 列検出
    # ------------------------------------

    print(
        "縦書き列を検出します...",
        flush=True
    )

    columns = detect_vertical_columns(
        image
    )

    print(
        f"検出された列数: {len(columns)}",
        flush=True
    )

    # ------------------------------------
    # デバッグ画像
    # ------------------------------------

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

    debug_path = (
        OUTPUT_DIR /
        "vertical_columns_debug.png"
    )

    cv2.imwrite(
        str(debug_path),
        debug
    )

    print(
        f"列デバッグ画像: {debug_path}",
        flush=True
    )

    # ------------------------------------
    # 各列を切り出してOCR
    # ------------------------------------

    all_results = []

    for index, column in enumerate(
        columns,
        start=1
    ):

        x, y, w, h = column

        print()
        print(
            "----------------------------------------"
        )

        print(
            f"列 {index}: "
            f"x={x}, y={y}, "
            f"width={w}, height={h}",
            flush=True
        )

        # -------------------------------
        # 列画像作成
        # -------------------------------

        column_path = make_column_image(
            image,
            column,
            index
        )

        print(
            f"切り出し画像: {column_path}",
            flush=True
        )

        # -------------------------------
        # OCR
        # -------------------------------

        results = run_ocr(
            column_path
        )

        print(
            f"認識数: {len(results)}",
            flush=True
        )

        column_text = []

        for j, result in enumerate(
            results,
            start=1
        ):

            text = result["text"]

            score = result["score"]

            print(
                f"  {j}: "
                f"score={score:.3f} "
                f"{text}",
                flush=True
            )

            column_text.append(
                text
            )

        # --------------------------------
        # 列単位の結果
        # --------------------------------

        all_results.append(
            {
                "column": index,
                "x": x,
                "y": y,
                "width": w,
                "height": h,
                "image": str(column_path),
                "results": results,
                "text": "".join(column_text)
            }
        )

    # ====================================
    # JSON保存
    # ====================================

    json_path = (
        OUTPUT_DIR /
        "vertical_ocr_result.json"
    )

    with open(
        json_path,
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(
            {
                "image": IMAGE_PATH,
                "column_count": len(columns),
                "columns": all_results
            },
            f,
            ensure_ascii=False,
            indent=2
        )

    print()
    print(
        "========================================"
    )

    print(
        f"JSON保存完了: {json_path}",
        flush=True
    )

    # ====================================
    # テキスト保存
    # ====================================

    text_path = (
        OUTPUT_DIR /
        "vertical_ocr_result.txt"
    )

    with open(
        text_path,
        "w",
        encoding="utf-8"
    ) as f:

        for item in all_results:

            f.write(
                f"【列 {item['column']}】\n"
            )

            f.write(
                item["text"]
            )

            f.write(
                "\n\n"
            )

    print(
        f"テキスト保存完了: {text_path}",
        flush=True
    )

    print(
        "========================================",
        flush=True
    )

    print(
        "テスト終了",
        flush=True
    )


if __name__ == "__main__":
    main()