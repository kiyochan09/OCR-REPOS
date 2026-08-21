import sys
import json
import subprocess
from pathlib import Path
from PIL import Image
from ocr_utils import find_json_file, parse_ndlocr_json
import cv2
import numpy as np

# =========================================================
# NDLOCR-Lite JSONを探す
# =========================================================


# =========================================================
# 罫線検出テスト
# =========================================================

def detect_lines(
    image_path: Path,
    output_dir: Path
):
    """
    元画像から横罫線・縦罫線を検出する。

    現段階では、
    「表」「コラム」「図」などの分類は行わない。

    あくまで、
    画像上の直線を正しく検出できるかを確認する。
    """

    print()
    print("========== 罫線検出開始 ==========")

    # -----------------------------------------------------
    # 画像読み込み
    # -----------------------------------------------------

    image = cv2.imread(
        str(image_path)
    )

    if image is None:

        raise RuntimeError(
            f"画像を読み込めません: {image_path}"
        )

    height, width = image.shape[:2]

    print(
        f"罫線検出画像サイズ: "
        f"{width}×{height}",
        flush=True
    )

    # -----------------------------------------------------
    # グレースケール
    # -----------------------------------------------------

    gray = cv2.cvtColor(
        image,
        cv2.COLOR_BGR2GRAY
    )

    gray_path = (
        output_dir
        / "line_gray.png"
    )

    cv2.imwrite(
        str(gray_path),
        gray
    )

    # -----------------------------------------------------
    # 二値化
    #
    # 白背景 → 0
    # 黒い文字・罫線 → 255
    # -----------------------------------------------------

    binary = cv2.adaptiveThreshold(

        gray,

        255,

        cv2.ADAPTIVE_THRESH_MEAN_C,

        cv2.THRESH_BINARY_INV,

        15,

        10
    )

    binary_path = (
        output_dir
        / "line_binary.png"
    )

    cv2.imwrite(
        str(binary_path),
        binary
    )

    # -----------------------------------------------------
    # 横罫線検出
    # -----------------------------------------------------

    horizontal_kernel_length = max(
        10,
        width // 20
    )

    horizontal_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            horizontal_kernel_length,
            1
        )
    )

    horizontal_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        horizontal_kernel
    )

    horizontal_path = (
        output_dir
        / "horizontal_lines.png"
    )

    cv2.imwrite(
        str(horizontal_path),
        horizontal_lines
    )

    # -----------------------------------------------------
    # 縦罫線検出
    # -----------------------------------------------------

    vertical_kernel_length = max(
        10,
        height // 20
    )

    vertical_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            1,
            vertical_kernel_length
        )
    )

    vertical_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        vertical_kernel
    )

    vertical_path = (
        output_dir
        / "vertical_lines.png"
    )

    cv2.imwrite(
        str(vertical_path),
        vertical_lines
    )

    # -----------------------------------------------------
    # 横＋縦
    # -----------------------------------------------------

    detected_lines = cv2.bitwise_or(

        horizontal_lines,

        vertical_lines
    )

    detected_path = (
        output_dir
        / "detected_lines.png"
    )

    cv2.imwrite(
        str(detected_path),
        detected_lines
    )

    # -----------------------------------------------------
    # 元画像に罫線を重ねる
    # -----------------------------------------------------

    overlay = image.copy()

    # 検出された横線
    overlay[horizontal_lines > 0] = (
        0,
        0,
        255
    )

    # 検出された縦線
    overlay[vertical_lines > 0] = (
        255,
        0,
        0
    )

    overlay_path = (
        output_dir
        / "detected_lines_overlay.png"
    )

    cv2.imwrite(
        str(overlay_path),
        overlay
    )

    # -----------------------------------------------------
    # 検出数を計算
    # -----------------------------------------------------

    horizontal_pixels = int(
        cv2.countNonZero(
            horizontal_lines
        )
    )

    vertical_pixels = int(
        cv2.countNonZero(
            vertical_lines
        )
    )

    total_pixels = int(
        cv2.countNonZero(
            detected_lines
        )
    )

    print(
        f"横罫線画素数: "
        f"{horizontal_pixels}",
        flush=True
    )

    print(
        f"縦罫線画素数: "
        f"{vertical_pixels}",
        flush=True
    )

    print(
        f"罫線総画素数: "
        f"{total_pixels}",
        flush=True
    )

    print(
        f"横罫線画像: "
        f"{horizontal_path}",
        flush=True
    )

    print(
        f"縦罫線画像: "
        f"{vertical_path}",
        flush=True
    )

    print(
        f"重ね合わせ画像: "
        f"{overlay_path}",
        flush=True
    )

    print(
        "========== 罫線検出終了 ==========",
        flush=True
    )

    return {
        "horizontal_pixels": horizontal_pixels,
        "vertical_pixels": vertical_pixels,
        "total_pixels": total_pixels,
        "horizontal_image": str(horizontal_path),
        "vertical_image": str(vertical_path),
        "overlay_image": str(overlay_path)
    }

# =========================================================
# 検出した罫線の座標解析
# =========================================================

def analyze_detected_lines(
    image_path: Path,
    output_dir: Path
):
    """
    横罫線・縦罫線を線分として解析する。

    現段階では矩形判定を行わない。
    検出された線の位置・長さを取得し、
    次段階の矩形生成に利用する。
    """

    print()
    print("========== 罫線座標解析開始 ==========")

    # -----------------------------------------------------
    # 画像読み込み
    # -----------------------------------------------------

    image = cv2.imread(
        str(image_path)
    )

    if image is None:

        raise RuntimeError(
            f"画像を読み込めません: {image_path}"
        )

    image_height, image_width = image.shape[:2]

    # -----------------------------------------------------
    # グレースケール
    # -----------------------------------------------------

    gray = cv2.cvtColor(
        image,
        cv2.COLOR_BGR2GRAY
    )

    # -----------------------------------------------------
    # 二値化
    # -----------------------------------------------------

    binary = cv2.adaptiveThreshold(

        gray,

        255,

        cv2.ADAPTIVE_THRESH_MEAN_C,

        cv2.THRESH_BINARY_INV,

        15,

        10
    )

    # -----------------------------------------------------
    # 横罫線検出
    # -----------------------------------------------------

    horizontal_kernel_length = max(
        10,
        image_width // 20
    )

    horizontal_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            horizontal_kernel_length,
            1
        )
    )

    horizontal_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        horizontal_kernel
    )

    # -----------------------------------------------------
    # 縦罫線検出
    # -----------------------------------------------------

    vertical_kernel_length = max(
        10,
        image_height // 20
    )

    vertical_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            1,
            vertical_kernel_length
        )
    )

    vertical_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        vertical_kernel
    )

    # =====================================================
    # 横線の線分抽出
    # =====================================================

    horizontal_contours, _ = cv2.findContours(

        horizontal_lines,

        cv2.RETR_EXTERNAL,

        cv2.CHAIN_APPROX_SIMPLE
    )

    horizontal_segments = []

    for contour in horizontal_contours:

        x, y, w, h = cv2.boundingRect(
            contour
        )

        # 短すぎる線は除外
        if w < 10:
            continue

        horizontal_segments.append(

            {
                "x1": int(x),
                "y": int(y),
                "x2": int(x + w - 1),
                "y2": int(y),
                "length": int(w),
                "thickness": int(h)
            }
        )

    # Y座標 → X座標順
    horizontal_segments.sort(

        key=lambda line: (
            line["y"],
            line["x1"]
        )
    )

    # =====================================================
    # 縦線の線分抽出
    # =====================================================

    vertical_contours, _ = cv2.findContours(

        vertical_lines,

        cv2.RETR_EXTERNAL,

        cv2.CHAIN_APPROX_SIMPLE
    )

    vertical_segments = []

    for contour in vertical_contours:

        x, y, w, h = cv2.boundingRect(
            contour
        )

        # 短すぎる線は除外
        if h < 10:
            continue

        vertical_segments.append(

            {
                "x": int(x),
                "y1": int(y),
                "x2": int(x),
                "y2": int(y + h - 1),
                "length": int(h),
                "thickness": int(w)
            }
        )

    # X座標 → Y座標順
    vertical_segments.sort(

        key=lambda line: (
            line["x"],
            line["y1"]
        )
    )

    # =====================================================
    # 結果表示
    # =====================================================

    print()
    print(
        f"横線候補数: "
        f"{len(horizontal_segments)}"
    )

    for i, line in enumerate(
        horizontal_segments
    ):

        print(

            f"  H{i + 1}: "

            f"X={line['x1']}～{line['x2']}, "

            f"Y={line['y']}, "

            f"長さ={line['length']}",

            flush=True
        )

    print()

    print(
        f"縦線候補数: "
        f"{len(vertical_segments)}"
    )

    for i, line in enumerate(
        vertical_segments
    ):

        print(

            f"  V{i + 1}: "

            f"X={line['x']}, "

            f"Y={line['y1']}～{line['y2']}, "

            f"長さ={line['length']}",

            flush=True
        )

    # =====================================================
    # JSON保存
    # =====================================================

    result = {

        "image": str(
            image_path
        ),

        "image_width": int(
            image_width
        ),

        "image_height": int(
            image_height
        ),

        "horizontal_lines":
            horizontal_segments,

        "vertical_lines":
            vertical_segments
    }

    output_json = (
        output_dir
        / "detected_line_segments.json"
    )

    with output_json.open(
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(

            result,

            f,

            ensure_ascii=False,

            indent=2
        )

    # =====================================================
    # 確認画像
    # =====================================================

    overlay = image.copy()

    # -----------------------------------------------------
    # 横線：赤
    # -----------------------------------------------------

    for line in horizontal_segments:

        cv2.line(

            overlay,

            (
                line["x1"],
                line["y"]
            ),

            (
                line["x2"],
                line["y2"]
            ),

            (0, 0, 255),

            2
        )

    # -----------------------------------------------------
    # 縦線：青
    # -----------------------------------------------------

    for line in vertical_segments:

        cv2.line(

            overlay,

            (
                line["x"],
                line["y1"]
            ),

            (
                line["x2"],
                line["y2"]
            ),

            (255, 0, 0),

            2
        )

    overlay_path = (
        output_dir
        / "line_segments_overlay.png"
    )

    cv2.imwrite(

        str(overlay_path),

        overlay
    )

    print()
    print(
        f"線分JSON: "
        f"{output_json}"
    )

    print(
        f"線分確認画像: "
        f"{overlay_path}"
    )

    print(
        "========== 罫線座標解析終了 ==========",
        flush=True
    )

    return result

# =========================================================
# 罫線から矩形領域を検出
# =========================================================

def detect_bordered_regions(
    image_path: Path,
    output_dir: Path
):
    """
    横罫線・縦罫線から閉じた矩形領域を検出する。

    現段階では「表」とは判定しない。
    あくまで「罫線によって囲まれた領域」を検出する。
    """

    print()
    print("========== 罫線領域検出開始 ==========")

    # -----------------------------------------------------
    # 画像読み込み
    # -----------------------------------------------------

    image = cv2.imread(
        str(image_path)
    )

    if image is None:

        raise RuntimeError(
            f"画像を読み込めません: {image_path}"
        )

    image_height, image_width = image.shape[:2]

    # -----------------------------------------------------
    # グレースケール
    # -----------------------------------------------------

    gray = cv2.cvtColor(
        image,
        cv2.COLOR_BGR2GRAY
    )

    # -----------------------------------------------------
    # 二値化
    # -----------------------------------------------------

    binary = cv2.adaptiveThreshold(

        gray,

        255,

        cv2.ADAPTIVE_THRESH_MEAN_C,

        cv2.THRESH_BINARY_INV,

        15,

        10
    )

    # -----------------------------------------------------
    # 横罫線
    # -----------------------------------------------------

    horizontal_kernel_length = max(
        10,
        image_width // 20
    )

    horizontal_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            horizontal_kernel_length,
            1
        )
    )

    horizontal_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        horizontal_kernel
    )

    # -----------------------------------------------------
    # 縦罫線
    # -----------------------------------------------------

    vertical_kernel_length = max(
        10,
        image_height // 20
    )

    vertical_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (
            1,
            vertical_kernel_length
        )
    )

    vertical_lines = cv2.morphologyEx(

        binary,

        cv2.MORPH_OPEN,

        vertical_kernel
    )

    # -----------------------------------------------------
    # 横＋縦
    # -----------------------------------------------------

    line_image = cv2.bitwise_or(

        horizontal_lines,

        vertical_lines
    )

    # -----------------------------------------------------
    # 線を少し太くする
    #
    # 交差部分を確実につなげるため
    # -----------------------------------------------------

    connect_kernel = cv2.getStructuringElement(

        cv2.MORPH_RECT,

        (3, 3)
    )

    connected_lines = cv2.dilate(

        line_image,

        connect_kernel,

        iterations=1
    )

    # -----------------------------------------------------
    # 輪郭検出
    # -----------------------------------------------------

    contours, hierarchy = cv2.findContours(

        connected_lines,

        cv2.RETR_LIST,

        cv2.CHAIN_APPROX_SIMPLE
    )

    regions = []

    # -----------------------------------------------------
    # 矩形候補を取得
    # -----------------------------------------------------

    for contour in contours:

        x, y, w, h = cv2.boundingRect(
            contour
        )

        # ---------------------------------------------
        # 小さすぎる領域を除外
        # ---------------------------------------------

        if w < 30 or h < 20:
            continue

        # ---------------------------------------------
        # 画像全体に近いものを除外
        #
        # ページ全体を囲むような輪郭を
        # 罫線領域として扱わない
        # ---------------------------------------------

        area_ratio = (
            (w * h)
            /
            (image_width * image_height)
        )

        if area_ratio > 0.90:
            continue

        # ---------------------------------------------
        # 横線・縦線が実際に存在するか確認
        # ---------------------------------------------

        roi_horizontal = horizontal_lines[
            y:min(y + h, image_height),
            x:min(x + w, image_width)
        ]

        roi_vertical = vertical_lines[
            y:min(y + h, image_height),
            x:min(x + w, image_width)
        ]

        horizontal_pixels = cv2.countNonZero(
            roi_horizontal
        )

        vertical_pixels = cv2.countNonZero(
            roi_vertical
        )

        # ---------------------------------------------
        # 横線と縦線の両方が存在する領域だけ採用
        # ---------------------------------------------

        if horizontal_pixels == 0:
            continue

        if vertical_pixels == 0:
            continue

        # ---------------------------------------------
        # 重複チェック用
        # ---------------------------------------------

        regions.append(
            {
                "x": int(x),
                "y": int(y),
                "width": int(w),
                "height": int(h),
                "area": int(w * h),
                "horizontal_pixels": int(
                    horizontal_pixels
                ),
                "vertical_pixels": int(
                    vertical_pixels
                )
            }
        )

    # -----------------------------------------------------
    # 小さい矩形を内包する大きな矩形がある場合、
    # まずはすべて保存する。
    #
    # 後段で「表全体」に統合する。
    # -----------------------------------------------------

    regions.sort(
        key=lambda r: (
            r["y"],
            r["x"],
            -r["area"]
        )
    )

    # -----------------------------------------------------
    # JSON保存
    # -----------------------------------------------------

    bordered_result = {

        "image": str(
            image_path
        ),

        "image_width": int(
            image_width
        ),

        "image_height": int(
            image_height
        ),

        "regions": regions
    }

    bordered_json = (
        output_dir
        / "bordered_regions.json"
    )

    with bordered_json.open(
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(

            bordered_result,

            f,

            ensure_ascii=False,

            indent=2
        )

    # -----------------------------------------------------
    # 確認用画像
    # -----------------------------------------------------

    overlay = image.copy()

    for i, region in enumerate(regions):

        x = region["x"]
        y = region["y"]
        w = region["width"]
        h = region["height"]

        cv2.rectangle(

            overlay,

            (x, y),

            (x + w, y + h),

            (0, 255, 0),

            2
        )

        cv2.putText(

            overlay,

            f"R{i + 1}",

            (x, max(15, y - 5)),

            cv2.FONT_HERSHEY_SIMPLEX,

            0.5,

            (0, 255, 0),

            1,

            cv2.LINE_AA
        )

    overlay_path = (
        output_dir
        / "bordered_regions_overlay.png"
    )

    cv2.imwrite(

        str(overlay_path),

        overlay
    )

    # -----------------------------------------------------
    # 結果表示
    # -----------------------------------------------------

    print(
        f"罫線矩形候補数: "
        f"{len(regions)}",
        flush=True
    )

    print(
        f"罫線領域JSON: "
        f"{bordered_json}",
        flush=True
    )

    print(
        f"罫線領域確認画像: "
        f"{overlay_path}",
        flush=True
    )

    for i, region in enumerate(regions):

        print(

            f"  R{i + 1}: "

            f"x={region['x']}, "

            f"y={region['y']}, "

            f"width={region['width']}, "

            f"height={region['height']}",

            flush=True
        )

    print(
        "========== 罫線領域検出終了 ==========",
        flush=True
    )

    return regions

# =========================================================
# NDLOCR-Lite JSONを解析
# =========================================================


# =========================================================
# 線分から罫線領域を生成
# =========================================================

def create_bordered_regions_from_lines(
    line_data,
    image_width,
    image_height
):
    """
    検出された横線・縦線から
    罫線で囲まれた大きな領域を生成する。

    現段階では「表」とは判定しない。
    type は bordered とする。
    """

    horizontal_lines = (
        line_data["horizontal_lines"]
    )

    vertical_lines = (
        line_data["vertical_lines"]
    )

    # -----------------------------------------------------
    # 十分に長い横線
    #
    # ページ幅の60%以上を対象とする
    # -----------------------------------------------------

    horizontal_candidates = [

        line

        for line in horizontal_lines

        if line["length"]
        >= image_width * 0.60
    ]

    # -----------------------------------------------------
    # 十分に長い縦線
    #
    # ページ高さの40%以上を対象とする
    # -----------------------------------------------------

    vertical_candidates = [

        line

        for line in vertical_lines

        if line["length"]
        >= image_height * 0.40
    ]

    print()
    print(
        "========== 罫線領域生成 =========="
    )

    print(
        f"長い横線候補: "
        f"{len(horizontal_candidates)}"
    )

    for line in horizontal_candidates:

        print(

            f"  H: "
            f"X={line['x1']}～{line['x2']}, "
            f"Y={line['y']}, "
            f"L={line['length']}"
        )

    print(
        f"長い縦線候補: "
        f"{len(vertical_candidates)}"
    )

    for line in vertical_candidates:

        print(

            f"  V: "
            f"X={line['x']}, "
            f"Y={line['y1']}～{line['y2']}, "
            f"L={line['length']}"
        )

    regions = []

    # -----------------------------------------------------
    # 横線2本の組み合わせ
    # -----------------------------------------------------

    for i in range(
        len(horizontal_candidates)
    ):

        top = horizontal_candidates[i]

        for j in range(
            i + 1,
            len(horizontal_candidates)
        ):

            bottom = horizontal_candidates[j]

            # 上下関係
            if bottom["y"] <= top["y"]:
                continue

            # -------------------------------------------------
            # 上線と下線の間隔
            # -------------------------------------------------

            region_height = (
                bottom["y"]
                - top["y"]
            )

            if region_height < 30:
                continue

            # -------------------------------------------------
            # 横線の共通範囲
            # -------------------------------------------------

            common_x1 = max(
                top["x1"],
                bottom["x1"]
            )

            common_x2 = min(
                top["x2"],
                bottom["x2"]
            )

            if common_x2 <= common_x1:
                continue

            # -------------------------------------------------
            # この上下線をつなぐ縦線を探す
            # -------------------------------------------------

            left_vertical = None
            right_vertical = None

            for vertical in vertical_candidates:

                x = vertical["x"]

                # 縦線が上下の横線の範囲内にあるか
                if x < common_x1:
                    continue

                if x > common_x2:
                    continue

                # 縦線が上下の横線を十分につないでいるか
                if vertical["y1"] > top["y"] + 5:
                    continue

                if vertical["y2"] < bottom["y"] - 5:
                    continue

                # 左端候補
                if left_vertical is None:

                    left_vertical = vertical

                elif x < left_vertical["x"]:

                    left_vertical = vertical

            # -------------------------------------------------
            # 右端の縦線
            # -------------------------------------------------

            for vertical in vertical_candidates:

                x = vertical["x"]

                if x < common_x1:
                    continue

                if x > common_x2:
                    continue

                if vertical["y1"] > top["y"] + 5:
                    continue

                if vertical["y2"] < bottom["y"] - 5:
                    continue

                if right_vertical is None:

                    right_vertical = vertical

                elif x > right_vertical["x"]:

                    right_vertical = vertical

            # -------------------------------------------------
            # 左右の縦線が存在するか
            # -------------------------------------------------

            if (
                left_vertical is None
                or right_vertical is None
            ):
                continue

            # 同じ縦線なら無効
            if (
                left_vertical["x"]
                >= right_vertical["x"]
            ):
                continue

            # -------------------------------------------------
            # 矩形
            # -------------------------------------------------

            x1 = left_vertical["x"]
            x2 = right_vertical["x"]

            y1 = top["y"]
            y2 = bottom["y"]

            width = x2 - x1
            height = y2 - y1

            if width < 30 or height < 30:
                continue

            regions.append(

                {
                    "name": "罫線領域",
                    "type": "bordered",
                    "x": int(x1),
                    "y": int(y1),
                    "width": int(width),
                    "height": int(height)
                }
            )

    # -----------------------------------------------------
    # 重複・内包領域を整理
    #
    # 現段階では最大の罫線領域を採用
    # -----------------------------------------------------

    if regions:

        regions.sort(

            key=lambda r:
                r["width"] * r["height"],

            reverse=True
        )

        regions = [
            regions[0]
        ]

    # -----------------------------------------------------
    # 結果
    # -----------------------------------------------------

    print(
        f"生成された罫線領域数: "
        f"{len(regions)}"
    )

    for i, region in enumerate(
        regions
    ):

        print(

            f"  B{i + 1}: "

            f"x={region['x']}, "

            f"y={region['y']}, "

            f"width={region['width']}, "

            f"height={region['height']}"
        )

    print(
        "===================================="
    )

    return regions

# =========================================================
# 罫線領域内部の罫線座標を解析
# =========================================================

def analyze_bordered_region_lines(
    line_data,
    bordered_regions
):
    """
    罫線領域の内部に存在する
    縦罫線・横罫線を整理する。

    現段階ではセル生成や表判定は行わない。
    """

    print()
    print(
        "========== 内部罫線解析開始 =========="
    )

    analyzed_regions = []

    horizontal_lines = line_data.get(
        "horizontal_lines",
        []
    )

    vertical_lines = line_data.get(
        "vertical_lines",
        []
    )

    for region_index, region in enumerate(
        bordered_regions
    ):

        rx1 = region["x"]
        ry1 = region["y"]

        rx2 = (
            rx1
            + region["width"]
        )

        ry2 = (
            ry1
            + region["height"]
        )

        print()
        print(
            f"--- 罫線領域 B{region_index + 1} ---"
        )

        print(
            f"領域: "
            f"X={rx1}～{rx2}, "
            f"Y={ry1}～{ry2}"
        )

        # =================================================
        # 領域内部の横罫線
        # =================================================

        region_horizontal = []

        for line in horizontal_lines:

            y = line["y"]

            if y < ry1 - 3:
                continue

            if y > ry2 + 3:
                continue

            # 横線と領域のX範囲が重なっているか
            overlap_x1 = max(
                line["x1"],
                rx1
            )

            overlap_x2 = min(
                line["x2"],
                rx2
            )

            if overlap_x2 <= overlap_x1:
                continue

            region_horizontal.append(
                {
                    "y": int(y),
                    "x1": int(overlap_x1),
                    "x2": int(overlap_x2),
                    "length": int(
                        overlap_x2
                        - overlap_x1
                        + 1
                    )
                }
            )

        # -------------------------------------------------
        # Y座標の近い線を統合
        # -------------------------------------------------

        region_horizontal.sort(
            key=lambda line: line["y"]
        )

        horizontal_positions = []

        for line in region_horizontal:

            if not horizontal_positions:

                horizontal_positions.append(
                    line["y"]
                )

                continue

            previous_y = (
                horizontal_positions[-1]
            )

            if abs(
                line["y"]
                - previous_y
            ) <= 2:

                # 平均位置
                horizontal_positions[-1] = int(
                    round(
                        (
                            previous_y
                            + line["y"]
                        )
                        / 2
                    )
                )

            else:

                horizontal_positions.append(
                    line["y"]
                )

        # =================================================
        # 領域内部の縦罫線
        # =================================================

        region_vertical = []

        for line in vertical_lines:

            x = line["x"]

            if x < rx1 - 3:
                continue

            if x > rx2 + 3:
                continue

            # 縦線と領域のY範囲が重なっているか
            overlap_y1 = max(
                line["y1"],
                ry1
            )

            overlap_y2 = min(
                line["y2"],
                ry2
            )

            if overlap_y2 <= overlap_y1:
                continue

            region_vertical.append(
                {
                    "x": int(x),
                    "y1": int(overlap_y1),
                    "y2": int(overlap_y2),
                    "length": int(
                        overlap_y2
                        - overlap_y1
                        + 1
                    )
                }
            )

        # -------------------------------------------------
        # X座標の近い線を統合
        # -------------------------------------------------

        region_vertical.sort(
            key=lambda line: line["x"]
        )

        vertical_positions = []

        for line in region_vertical:

            if not vertical_positions:

                vertical_positions.append(
                    line["x"]
                )

                continue

            previous_x = (
                vertical_positions[-1]
            )

            if abs(
                line["x"]
                - previous_x
            ) <= 2:

                vertical_positions[-1] = int(
                    round(
                        (
                            previous_x
                            + line["x"]
                        )
                        / 2
                    )
                )

            else:

                vertical_positions.append(
                    line["x"]
                )

        # =================================================
        # 結果表示
        # =================================================

        print()
        print(
            "横罫線Y座標:"
        )

        print(
            horizontal_positions
        )

        print()
        print(
            "縦罫線X座標:"
        )

        print(
            vertical_positions
        )

        # =================================================
        # 結果保存
        # =================================================

        analyzed_regions.append(
            {
                "name": region["name"],
                "type": region["type"],
                "x": rx1,
                "y": ry1,
                "width": region["width"],
                "height": region["height"],
                "horizontal_positions":
                    horizontal_positions,
                "vertical_positions":
                    vertical_positions
            }
        )

    print()
    print(
        "========== 内部罫線解析終了 ==========",
        flush=True
    )

    return analyzed_regions

# =========================================================
# 罫線領域からセル候補を生成
# =========================================================

def create_cell_candidates(
    analyzed_regions
):
    """
    罫線領域のX/Y座標から
    セル候補を生成する。

    現段階ではセルを確定しない。
    あくまで矩形候補として扱う。
    """

    print()
    print(
        "========== セル候補生成開始 =========="
    )

    all_regions = []

    for region_index, region in enumerate(
        analyzed_regions
    ):

        x_positions = region.get(
            "vertical_positions",
            []
        )

        y_positions = region.get(
            "horizontal_positions",
            []
        )

        if len(x_positions) < 2:
            continue

        if len(y_positions) < 2:
            continue

        print()
        print(
            f"--- 罫線領域 B{region_index + 1} ---"
        )

        print(
            f"列数候補: "
            f"{len(x_positions) - 1}"
        )

        print(
            f"行数候補: "
            f"{len(y_positions) - 1}"
        )

        cells = []

        # -------------------------------------------------
        # Y方向
        # -------------------------------------------------

        for row in range(
            len(y_positions) - 1
        ):

            y1 = y_positions[row]
            y2 = y_positions[row + 1]

            # -------------------------------------------------
            # X方向
            # -------------------------------------------------

            for column in range(
                len(x_positions) - 1
            ):

                x1 = x_positions[column]
                x2 = x_positions[column + 1]

                width = x2 - x1
                height = y2 - y1

                if width <= 0:
                    continue

                if height <= 0:
                    continue

                cells.append(
                    {
                        "row": row + 1,
                        "column": column + 1,
                        "x": x1,
                        "y": y1,
                        "width": width,
                        "height": height,

                        # OCR割り当て用
                        "text": "",
                        "ocr_count": 0
                    }
                )                

        # -------------------------------------------------
        # 結果表示
        # -------------------------------------------------

        print(
            f"生成セル候補数: "
            f"{len(cells)}"
        )

        for cell in cells:

            print(
                f"  "
                f"R{cell['row']}C{cell['column']}: "
                f"x={cell['x']}, "
                f"y={cell['y']}, "
                f"w={cell['width']}, "
                f"h={cell['height']}"
            )

        all_regions.append(
            {
                "name": region["name"],
                "type": region["type"],
                "x": region["x"],
                "y": region["y"],
                "width": region["width"],
                "height": region["height"],
                "cells": cells
            }
        )

    print()
    print(
        "========== セル候補生成終了 ==========",
        flush=True
    )

    return all_regions

# =========================================================
# セル候補にOCR結果を割り当てる
# =========================================================

def assign_ocr_to_cell_candidates(
    cell_candidate_regions,
    results
):
    """
    各セル候補にOCR結果を割り当てる。

    OCRの中心点がセル内にある場合、
    そのOCRをセルに所属させる。

    現段階では「表セル」とは判定しない。
    """

    print()
    print(
        "========== セルOCR割り当て開始 =========="
    )

    analyzed_regions = []

    for region_index, region in enumerate(
        cell_candidate_regions
    ):

        print()
        print(
            f"--- 罫線領域 B{region_index + 1} ---"
        )

        analyzed_cells = []

        for cell in region.get(
            "cells",
            []
        ):

            cell_x1 = cell["x"]
            cell_y1 = cell["y"]

            cell_x2 = (
                cell_x1
                + cell["width"]
            )

            cell_y2 = (
                cell_y1
                + cell["height"]
            )

            cell_ocr = []

            for ocr in results:

                center_x = (
                    ocr["x"]
                    + ocr["width"] / 2.0
                )

                center_y = (
                    ocr["y"]
                    + ocr["height"] / 2.0
                )

                if (
                    center_x >= cell_x1
                    and center_x <= cell_x2
                    and center_y >= cell_y1
                    and center_y <= cell_y2
                ):

                    cell_ocr.append(
                        ocr
                    )

            analyzed_cell = dict(
                cell
            )

            analyzed_cell["ocr_count"] = len(cell_ocr)

            analyzed_cell["ocr"] = cell_ocr

            # -------------------------------------------------
            # OCR文字列
            # -------------------------------------------------
            texts = [
                ocr["text"]
                for ocr in cell_ocr
                if ocr.get("text")
            ]

            analyzed_cell["text"] = " ".join(texts)

            analyzed_cells.append(
                analyzed_cell
            )

        analyzed_regions.append(
            {
                "name": region["name"],
                "type": region["type"],
                "x": region["x"],
                "y": region["y"],
                "width": region["width"],
                "height": region["height"],
                "cells": analyzed_cells
            }
        )

        # -------------------------------------------------
        # 表示
        # -------------------------------------------------

        for cell in analyzed_cells:

            if cell["ocr_count"] == 0:
                continue

            texts = [
                ocr["text"]
                for ocr in cell["ocr"]
            ]

            text = " ".join(
                texts
            )

            print(
                f"  "
                f"R{cell['row']}C{cell['column']}: "
                f"OCR={cell['ocr_count']}, "
                f"text={text}"
            )

    print()
    print(
        "========== セルOCR割り当て終了 ==========",
        flush=True
    )

    return analyzed_regions

# =========================================================
# 自動レイアウト領域生成
# =========================================================



# =========================================================
# 罫線領域＋セルOCR結果を表領域に変換
# =========================================================

def create_table_regions_from_cells(
    analyzed_cell_regions
):
    """
    罫線領域とセルOCR結果から
    auto_layout.json用の表regionを生成する。
    """

    regions = []

    for table_index, table in enumerate(
        analyzed_cell_regions,
        start=1
    ):

        cells = []

        for cell in table.get(
            "cells",
            []
        ):

            texts = [
                ocr["text"]
                for ocr in cell.get(
                    "ocr",
                    []
                )
            ]

            text = " ".join(
                texts
            )

            cells.append(
                {
                    "row": cell["row"],
                    "column": cell["column"],

                    "x": cell["x"],
                    "y": cell["y"],
                    "width": cell["width"],
                    "height": cell["height"],

                    "text": text,

                    "ocr_count": cell.get(
                        "ocr_count",
                        0
                    )
                }
            )

        regions.append(
            {
                "name": f"表{table_index}",
                "type": "table",

                "x": table["x"],
                "y": table["y"],
                "width": table["width"],
                "height": table["height"],

                "rows": len(
                    set(
                        cell["row"]
                        for cell in cells
                    )
                ),

                "columns": len(
                    set(
                        cell["column"]
                        for cell in cells
                    )
                ),

                "cells": cells
            }
        )

    return regions



# =========================================================
# 表セル内のOCRを本文判定から除外
# =========================================================

def remove_table_ocr(
    results,
    analyzed_cell_regions
):
    """
    表セルに割り当てられたOCRを
    本文領域の判定対象から除外する。
    """

    table_ocr_ids = set()

    for region in analyzed_cell_regions:

        for cell in region.get(
            "cells",
            []
        ):

            for ocr in cell.get(
                "ocr",
                []
            ):

                table_ocr_ids.add(
                    id(ocr)
                )

    body_results = [

        ocr

        for ocr in results

        if id(ocr) not in table_ocr_ids
    ]

    return body_results

# =========================================================
# 表以外のOCRから本文領域を生成
# =========================================================

def create_body_regions(
    body_results
):
    """
    表セルに割り当てられていないOCR結果から
    本文候補領域を生成する。

    縦書き・横書きの両方に対応する。
    """

    if not body_results:
        return []

    # -----------------------------------------------------
    # OCR結果を縦書き・横書きに分離
    # -----------------------------------------------------

    vertical_results = [
        r
        for r in body_results
        if r.get("isVertical", False)
    ]

    horizontal_results = [
        r
        for r in body_results
        if not r.get("isVertical", False)
    ]

    regions = []

    # -----------------------------------------------------
    # 縦書き本文
    # -----------------------------------------------------

    if vertical_results:

        vertical_results.sort(
            key=lambda r: r["x"]
        )

        groups = []

        current_group = []

        column_gap = 30

        for r in vertical_results:

            if not current_group:

                current_group.append(r)

                continue

            previous = current_group[-1]

            gap = abs(
                r["x"] - previous["x"]
            )

            if gap <= column_gap:

                current_group.append(r)

            else:

                groups.append(
                    current_group
                )

                current_group = [r]

        if current_group:

            groups.append(
                current_group
            )

        # -------------------------------------------------
        # 最大グループを本文候補とする
        # -------------------------------------------------

        if groups:

            groups.sort(
                key=lambda g: len(g),
                reverse=True
            )

            main_group = groups[0]

            min_x = min(
                r["x"]
                for r in main_group
            )

            min_y = min(
                r["y"]
                for r in main_group
            )

            max_x = max(
                r["x"] + r["width"]
                for r in main_group
            )

            max_y = max(
                r["y"] + r["height"]
                for r in main_group
            )

            regions.append(
                {
                    "name": "本文",
                    "type": "body",
                    "x": int(min_x),
                    "y": int(min_y),
                    "width": int(max_x - min_x),
                    "height": int(max_y - min_y),
                    "orientation": "vertical",
                    "ocr_count": len(main_group)
                }
            )

    # -----------------------------------------------------
    # 横書き本文
    # -----------------------------------------------------

    if horizontal_results:

        horizontal_results.sort(
            key=lambda r: r["y"]
        )

        groups = []

        current_group = []

        line_gap = 20

        for r in horizontal_results:

            if not current_group:

                current_group.append(r)

                continue

            previous = current_group[-1]

            gap = abs(
                r["y"] - previous["y"]
            )

            if gap <= line_gap:

                current_group.append(r)

            else:

                groups.append(
                    current_group
                )

                current_group = [r]

        if current_group:

            groups.append(
                current_group
            )

        # -------------------------------------------------
        # 横書きOCRがまとまっている場合
        # -------------------------------------------------

        if groups:

            groups.sort(
                key=lambda g: len(g),
                reverse=True
            )

            main_group = groups[0]

            min_x = min(
                r["x"]
                for r in main_group
            )

            min_y = min(
                r["y"]
                for r in main_group
            )

            max_x = max(
                r["x"] + r["width"]
                for r in main_group
            )

            max_y = max(
                r["y"] + r["height"]
                for r in main_group
            )

            regions.append(
                {
                    "name": "本文横",
                    "type": "body",
                    "x": int(min_x),
                    "y": int(min_y),
                    "width": int(max_x - min_x),
                    "height": int(max_y - min_y),
                    "orientation": "horizontal",
                    "ocr_count": len(main_group)
                }
            )

    return regions

# =========================================================
# メイン処理
# =========================================================

def main():

    # -----------------------------------------------------
    # 引数確認
    # -----------------------------------------------------

    if len(sys.argv) < 3:

        print(
            "Usage: "
            "ndlocr_auto_region.py "
            "<image> <output_dir>"
        )

        sys.exit(1)

    # -----------------------------------------------------
    # 入力画像
    # -----------------------------------------------------

    image_path = Path(
        sys.argv[1]
    ).resolve()

    # -----------------------------------------------------
    # 出力フォルダ
    # -----------------------------------------------------

    output_dir = Path(
        sys.argv[2]
    ).resolve()

    # -----------------------------------------------------
    # 入力画像確認
    # -----------------------------------------------------

    if not image_path.exists():

        print(
            f"画像がありません: "
            f"{image_path}"
        )

        sys.exit(1)

    output_dir.mkdir(
        parents=True,
        exist_ok=True
    )

    # -----------------------------------------------------
    # Python実行ファイル
    #
    # ★ここが重要
    # -----------------------------------------------------

    python_exe = Path(
        sys.executable
    )

    # -----------------------------------------------------
    # NDLOCR-Liteコマンド
    # -----------------------------------------------------

    command = [

        str(python_exe),

        "-m",

        "ocr",

        "--sourceimg",

        str(image_path),

        "--output",

        str(output_dir),

        "--json-only",

        "--device",

        "cpu"
    ]

    # -----------------------------------------------------
    # デバッグ情報
    # -----------------------------------------------------

    print(
        "NDLOCR-Liteを実行します...",
        flush=True
    )

    print(
        "Python実行ファイル:",
        python_exe,
        flush=True
    )

    print(
        "作業ディレクトリ:",
        Path.cwd(),
        flush=True
    )

    print(
        "入力画像:",
        image_path,
        flush=True
    )

    print(
        "入力画像存在:",
        image_path.exists(),
        flush=True
    )

    print(
        "出力フォルダ:",
        output_dir,
        flush=True
    )

    print(
        "出力フォルダ存在:",
        output_dir.exists(),
        flush=True
    )

    print(
        "コマンド:",
        " ".join(command),
        flush=True
    )

    # -----------------------------------------------------
    # NDLOCR-Lite実行
    # -----------------------------------------------------

    process = subprocess.run(

        command,

        stdout=subprocess.PIPE,

        stderr=subprocess.PIPE,

        text=True,

        encoding="utf-8",

        errors="replace"
    )

    # -----------------------------------------------------
    # 標準出力
    # -----------------------------------------------------

    if process.stdout:

        print(
            process.stdout,
            flush=True
        )

    # -----------------------------------------------------
    # エラー出力
    # -----------------------------------------------------

    if process.stderr:

        print(
            "========== NDLOCR-Lite ERROR ==========",
            file=sys.stderr,
            flush=True
        )

        print(
            process.stderr,
            file=sys.stderr,
            flush=True
        )

        print(
            "========================================",
            file=sys.stderr,
            flush=True
        )

    # -----------------------------------------------------
    # 終了コード確認
    # -----------------------------------------------------

    if process.returncode != 0:

        print(
            f"NDLOCR-Lite終了コード: "
            f"{process.returncode}",
            file=sys.stderr,
            flush=True
        )

        sys.exit(
            process.returncode
        )

    # -----------------------------------------------------
    # JSON検索
    # -----------------------------------------------------

    json_path = find_json_file(
        output_dir,
        image_path
    )

    print(
        f"JSON: {json_path}",
        flush=True
    )

    # -----------------------------------------------------
    # JSON解析
    # -----------------------------------------------------

    results = parse_ndlocr_json(
        json_path
    )

    # -----------------------------------------------------
    # OCR結果表示
    # -----------------------------------------------------

    print()

    print(
        "========== OCR検出結果 =========="
    )

    for i, r in enumerate(results):

        print(
            f"[{i:02d}] "
            f"x={r['x']}, "
            f"y={r['y']}, "
            f"w={r['width']}, "
            f"h={r['height']}, "
            f"vertical={r['isVertical']}, "
            f"text={r['text']}"
        )

    print(
        "=================================="
    )

    print()

    # -----------------------------------------------------
    # 画像サイズ取得
    #
    # NDLOCRのimginfoが0でも、
    # 実際の画像から取得する
    # -----------------------------------------------------

    try:

        with Image.open(
            image_path
        ) as img:

            image_width, image_height = (
                img.size
            )

    except Exception as e:

        print(
            f"画像サイズ取得エラー: {e}",
            file=sys.stderr
        )

        image_width = 0
        image_height = 0

    print(
        f"実画像サイズ: "
        f"{image_width}×{image_height}",
        flush=True
    )

    # -----------------------------------------------------
    # C#用OCR JSON
    # -----------------------------------------------------

    result = {

        "image": str(
            image_path
        ),

        "json": str(
            json_path
        ),

        "results": results
    }

    output_json = (
        output_dir
        / "auto_regions.json"
    )

    with output_json.open(
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(
            result,
            f,
            ensure_ascii=False,
            indent=2
        )

    print(
        f"自動領域JSON: "
        f"{output_json}",
        flush=True
    )

    print(
        f"検出数: "
        f"{len(results)}",
        flush=True
    )

    # -----------------------------------------------------
    # 罫線検出テスト
    # -----------------------------------------------------

    line_result = detect_lines(
        image_path,
        output_dir
    )

    # -----------------------------------------------------
    # 罫線座標解析
    # -----------------------------------------------------

    line_data = analyze_detected_lines(
        image_path,
        output_dir
    )

    # -----------------------------------------------------
    # 罫線領域生成
    # -----------------------------------------------------

    bordered_regions = create_bordered_regions_from_lines(
        line_data,
        image_width,
        image_height
    )

    # -----------------------------------------------------
    # 罫線領域内部の解析
    # -----------------------------------------------------

    analyzed_bordered_regions = analyze_bordered_region_lines(
        line_data,
        bordered_regions
    )

    # -----------------------------------------------------
    # セル候補生成
    # -----------------------------------------------------

    cell_candidate_regions = create_cell_candidates(
        analyzed_bordered_regions
    )

    # -----------------------------------------------------
    # セル候補へのOCR割り当て
    # -----------------------------------------------------

    analyzed_cell_regions = assign_ocr_to_cell_candidates(
        cell_candidate_regions,
        results
    )

    # -----------------------------------------------------
    # セルOCR結果から表領域を生成
    # -----------------------------------------------------

    table_regions = create_table_regions_from_cells(
        analyzed_cell_regions
    )

    print()
    print(
        "========== 表領域生成開始 ==========",
        flush=True
    )

    print(
        f"検出表数: {len(table_regions)}",
        flush=True
    )

    for table_index, table in enumerate(
        table_regions,
        start=1
    ):

        print(
            f"  表{table_index}: "
            f"x={table['x']}, "
            f"y={table['y']}, "
            f"width={table['width']}, "
            f"height={table['height']}, "
            f"rows={table['rows']}, "
            f"columns={table['columns']}",
            flush=True
        )

    print(
        "========== 表領域生成終了 ==========",
        flush=True
    )

    # -----------------------------------------------------
    # 表セル内のOCRを本文判定から除外
    # -----------------------------------------------------

    body_results = remove_table_ocr(
        results,
        analyzed_cell_regions
    )

    print()
    print(
        "========== 本文OCR候補作成 ==========",
        flush=True
    )

    print(
        f"全OCR数: {len(results)}",
        flush=True
    )

    print(
        f"表セル内OCR除外後: {len(body_results)}",
        flush=True
    )

    # -----------------------------------------------------
    # 表見出しを抽出
    #
    # 表の直上にあるOCRだけを表見出し候補とする。
    # 表の下にあるOCRは表へ取り込まず、本文候補として残す。
    # -----------------------------------------------------

    def extract_table_captions(
        body_results,
        table_regions
    ):
        captions = []

        caption_max_gap = 40

        for table_index, table in enumerate(
            table_regions,
            start=1
        ):
            tx1 = table["x"]
            ty1 = table["y"]
            tx2 = tx1 + table["width"]

            candidates = []

            for ocr in body_results:
                ox1 = ocr["x"]
                oy1 = ocr["y"]
                ox2 = ox1 + ocr["width"]
                oy2 = oy1 + ocr["height"]

                # 表より上にあるOCRだけを対象にする
                if oy2 > ty1:
                    continue

                gap = ty1 - oy2

                if gap > caption_max_gap:
                    continue

                # 表と横方向に重なっているか確認
                overlap_x1 = max(ox1, tx1)
                overlap_x2 = min(ox2, tx2)

                if overlap_x2 <= overlap_x1:
                    continue

                overlap_width = overlap_x2 - overlap_x1
                ocr_width = max(1, ox2 - ox1)
                overlap_ratio = overlap_width / ocr_width

                if overlap_ratio < 0.30:
                    continue

                candidates.append(ocr)

            if candidates:
                candidates.sort(
                    key=lambda r: (
                        ty1 - (r["y"] + r["height"]),
                        r["x"]
                    )
                )

                captions.append(
                    {
                        "table_index": table_index,
                        "ocr": candidates[0]
                    }
                )

        return captions

    table_captions = extract_table_captions(
        body_results,
        table_regions
    )

    print()
    print(
        "========== 表見出し判定 ==========",
        flush=True
    )

    caption_ids = set()

    for item in table_captions:
        table_index = item["table_index"]
        caption = item["ocr"]

        caption_ids.add(id(caption))

        print(
            f"表{table_index} 見出し候補: "
            f"x={caption['x']}, "
            f"y={caption['y']}, "
            f"text={caption['text']}",
            flush=True
        )

    # -----------------------------------------------------
    # 表見出しを除いた本文候補
    #
    # 表の下にある本文OCRはここに残る。
    # -----------------------------------------------------

    body_results_without_captions = [
        ocr
        for ocr in body_results
        if id(ocr) not in caption_ids
    ]

    print(
        f"見出し除外後本文候補数: "
        f"{len(body_results_without_captions)}",
        flush=True
    )

    # -----------------------------------------------------
    # 表以外のOCRから本文領域を生成
    # -----------------------------------------------------

    body_regions = create_body_regions(
        body_results_without_captions
    )

    print(
        f"本文領域数: {len(body_regions)}",
        flush=True
    )

    for region in body_regions:
        print(
            f"  {region['name']}: "
            f"x={region['x']}, "
            f"y={region['y']}, "
            f"width={region['width']}, "
            f"height={region['height']}, "
            f"orientation={region.get('orientation')}, "
            f"ocr_count={region.get('ocr_count')}",
            flush=True
        )

    print(
        "========== 本文OCR候補作成終了 ==========",
        flush=True
    )

    # -----------------------------------------------------
    # 表領域 + 本文領域
    # -----------------------------------------------------

    regions = (
        table_regions
        + body_regions
    )

    # -----------------------------------------------------
    # auto_layout.json
    # -----------------------------------------------------

    layout_result = {

        "image": str(
            image_path
        ),

        "image_width": image_width,

        "image_height": image_height,

        "regions": regions
    }

    layout_json = (

        output_dir
        / "auto_layout.json"
    )

    with layout_json.open(
        "w",
        encoding="utf-8"
    ) as f:

        json.dump(

            layout_result,

            f,

            ensure_ascii=False,

            indent=2
        )

    print(
        f"自動レイアウトJSON: "
        f"{layout_json}",
        flush=True
    )

    print(
        f"自動領域数: "
        f"{len(regions)}",
        flush=True
    )

    # -----------------------------------------------------
    # 領域表示
    # -----------------------------------------------------

    for region in regions:

        print(

            f"  {region['name']}: "

            f"x={region['x']}, "

            f"y={region['y']}, "

            f"width={region['width']}, "

            f"height={region['height']}, "

            f"type={region['type']}",

            flush=True
        )
    

# =========================================================
# プログラム開始
# =========================================================

if __name__ == "__main__":

    main()