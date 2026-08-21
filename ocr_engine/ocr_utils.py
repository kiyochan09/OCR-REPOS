# =========================================================
# OCR共通処理
# =========================================================

import json
from pathlib import Path


def find_json_file(output_dir: Path, image_path: Path) -> Path:

    json_files = list(output_dir.rglob("*.json"))

    if not json_files:
        raise FileNotFoundError(
            f"NDLOCR-LiteのJSONが見つかりません。\n"
            f"出力先: {output_dir}"
        )

    # 入力画像と同名のJSONを優先
    stem = image_path.stem

    preferred = [
        p for p in json_files
        if p.stem == stem
    ]

    if preferred:
        return preferred[0]

    return json_files[0]


def parse_ndlocr_json(json_path: Path):

    with json_path.open(
        "r",
        encoding="utf-8"
    ) as f:

        data = json.load(f)

    results = []

    # -----------------------------------------------------
    # NDLOCR-Lite形式
    # -----------------------------------------------------

    if isinstance(data, dict) and "contents" in data:

        contents = data["contents"]

        if isinstance(contents, list):

            items = []

            for page in contents:

                if isinstance(page, list):
                    items.extend(page)

                elif isinstance(page, dict):
                    items.append(page)

        else:

            items = []

    # -----------------------------------------------------
    # その他の形式
    # -----------------------------------------------------

    elif isinstance(data, list):

        items = data

    elif isinstance(data, dict):

        if "results" in data:
            items = data["results"]

        elif "blocks" in data:
            items = data["blocks"]

        elif "ocr" in data:
            items = data["ocr"]

        else:
            items = [data]

    else:

        items = []

    # -----------------------------------------------------
    # OCR結果解析
    # -----------------------------------------------------

    for item in items:

        if not isinstance(item, dict):
            continue

        bbox = item.get("boundingBox")

        if bbox is None:
            bbox = item.get("bbox")

        if bbox is None:
            continue

        try:

            points = []

            for point in bbox:

                if len(point) >= 2:

                    points.append(
                        (
                            float(point[0]),
                            float(point[1])
                        )
                    )

            if not points:
                continue

            min_x = min(
                p[0] for p in points
            )

            max_x = max(
                p[0] for p in points
            )

            min_y = min(
                p[1] for p in points
            )

            max_y = max(
                p[1] for p in points
            )

            x = int(round(min_x))
            y = int(round(min_y))

            width = int(
                round(max_x - min_x)
            )

            height = int(
                round(max_y - min_y)
            )

        except Exception:

            continue

        if width <= 0 or height <= 0:
            continue

        # -------------------------------------------------
        # text
        # -------------------------------------------------

        text = str(
            item.get(
                "text",
                ""
            )
        )

        # -------------------------------------------------
        # confidence
        # -------------------------------------------------

        try:

            confidence = float(
                item.get(
                    "confidence",
                    item.get(
                        "score",
                        0.0
                    )
                )
            )

        except Exception:

            confidence = 0.0

        # -------------------------------------------------
        # 縦書き判定
        # -------------------------------------------------

        is_vertical = item.get(
            "isVertical",
            False
        )

        if isinstance(
            is_vertical,
            str
        ):

            is_vertical = (
                is_vertical.lower()
                == "true"
            )

        results.append(
            {
                "x": x,
                "y": y,
                "width": width,
                "height": height,
                "text": text,
                "confidence": confidence,
                "isVertical": bool(
                    is_vertical
                ),
                "id": item.get(
                    "id",
                    len(results)
                )
            }
        )

    return results

