import sys
import json
from pathlib import Path

from PIL import Image
from paddleocr import PaddleOCR


# ----------------------------------------
# 設定
# ----------------------------------------

IMAGE_PATH = sys.argv[1] if len(sys.argv) > 1 else "test.png"

LAYOUT_PATH = "page_layout.json"


# ----------------------------------------
# page_layout.json からOCR領域を読み込む
# ----------------------------------------

def load_region_from_layout(layout_path):

    with open(
        layout_path,
        "r",
        encoding="utf-8"
    ) as f:

        layout = json.load(f)

    regions = (
        layout
        .get("template", {})
        .get("regions", [])
    )

    if not regions:
        raise ValueError(
            "page_layout.json にOCR領域がありません。"
        )

    return regions[0]


# ----------------------------------------
# OCR
# ----------------------------------------

def run_ocr(image_path, region):
    image = Image.open(image_path)
    print(f"元画像サイズ: {image.width} x {image.height}")

    x = int(region["x"])
    y = int(region["y"])
    width = int(region["width"])
    height = int(region["height"])

    x = max(0, x)
    y = max(0, y)
    right = min(image.width, x + width)
    bottom = min(image.height, y + height)

    if right <= x or bottom <= y:
        raise ValueError("OCR領域が画像の範囲外です。")

    crop = image.crop((x, y, right, bottom))
    crop_path = Path("ocr_region_crop.png")
    crop.save(crop_path)

    print(f"OCR領域: x={x}, y={y}, width={right - x}, height={bottom - y}")
    print(f"切り出し画像: {crop_path}")

    # ----------------------------------------
    # PaddleOCR
    # ----------------------------------------
    print()
    print("PaddleOCRを起動します...", flush=True)

    ocr = PaddleOCR(
    lang="japan",
    enable_mkldnn=False,

    text_det_limit_side_len=2081,
    text_det_limit_type="max",

    text_det_thresh=0.2,
    text_det_box_thresh=0.3,
    text_det_unclip_ratio=2.0,
)

    print("PaddleOCRエンジン作成完了", flush=True)
    print("OCRを実行します...", flush=True)

    result = ocr.predict(str(crop_path))

    print("OCR predict 完了", flush=True)
    print("OCR実行完了", flush=True)


    # ========================================
    # OCR検出領域を画像に描画
    # ========================================

    from PIL import ImageDraw

    debug_image = crop.copy()
    draw = ImageDraw.Draw(debug_image)

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

        # PaddleOCRの検出領域
        dt_polys = data.get("dt_polys", [])

        for poly in dt_polys:

            try:
                points = [
                    tuple(map(int, p))
                    for p in poly
                ]

                if len(points) >= 4:
                    draw.polygon(
                        points,
                        outline="red",
                        width=3
                    )

            except Exception:
                pass


    debug_path = Path("ocr_region_debug.png")

    debug_image.save(debug_path)

    print(
        f"検出領域画像保存: {debug_path}",
        flush=True
    )


    # ========================================
    # 文字検出数の確認
    # ========================================

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

        dt_polys = data.get(
            "dt_polys",
            []
        )

        print()
        print("=== 文字検出結果 ===")
        print(
            "文字領域検出数:",
            len(dt_polys)
        )

        for i, poly in enumerate(
            dt_polys,
            start=1
        ):

            try:
                print(
                    f"検出 {i}: {poly.tolist()}"
                )

            except AttributeError:

                print(
                    f"検出 {i}: {poly}"
                )


    # ========================================
    # OCR結果を処理
    # ========================================

    results = []

    for res in result:

        if hasattr(res, "json"):

            data = res.json

            if isinstance(data, str):
                data = json.loads(data)

        elif isinstance(res, dict):

            data = res

        else:

            print(
                "未知の結果形式:",
                type(res)
            )

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

            poly = None

            if i < len(polys):

                try:
                    poly = polys[i].tolist()

                except AttributeError:
                    poly = list(polys[i])

            results.append({
                    "text": str(text),
                    "score": score,
                    "box": box,
                    "polygon": poly
                })
    return results

# ========================================
# run_ocr() の戻り値
# ========================================




# ----------------------------------------
# メイン
# ----------------------------------------

if __name__ == "__main__":

    print("①画像を読み込みます")

    print(
        f"画像: {IMAGE_PATH}"
    )


    # ----------------------------------------
    # OCR領域を取得
    # ----------------------------------------

    region = load_region_from_layout(
        LAYOUT_PATH
    )

    print()

    print(
        "使用するOCR領域:"
    )

    print(region)


    # ----------------------------------------
    # OCR実行
    # ----------------------------------------

    results = run_ocr(
        IMAGE_PATH,
        region
    )


    # ----------------------------------------
    # OCR結果表示
    # ----------------------------------------

    print()

    print("②OCR結果")

    print(
        f"検出数: {len(results)}"
    )

    print()


    for i, item in enumerate(
        results,
        start=1
    ):

        print(
            f"ID={i:2d} "
            f"score={item['score']:.3f} "
            f"{item['text']}"
        )


    # ----------------------------------------
    # JSON保存
    # ----------------------------------------

    output = {

        "image": IMAGE_PATH,

        "region": region,

        "results": results

    }


    output_path = Path(
        "ocr_region_result.json"
    )


    with output_path.open(
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(
            output,
            f,
            ensure_ascii=False,
            indent=2
        )


    print()

    print(
        f"③保存完了: {output_path}"
    )