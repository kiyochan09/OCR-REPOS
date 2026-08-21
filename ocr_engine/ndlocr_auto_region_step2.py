import sys
import json
import subprocess
from pathlib import Path
from PIL import Image
from ocr_utils import find_json_file, parse_ndlocr_json
from caption_detection import extract_table_captions
from table_detection import (
    detect_lines,
    analyze_detected_lines,
    detect_bordered_regions,
    create_bordered_regions_from_lines,
    analyze_bordered_region_lines,
    create_cell_candidates,
    assign_ocr_to_cell_candidates,
    create_table_regions_from_cells,
)
import cv2
import numpy as np

# =========================================================
# NDLOCR-Lite JSONを探す
# =========================================================


# =========================================================
# 罫線検出テスト
# =========================================================


# =========================================================
# 検出した罫線の座標解析
# =========================================================


# =========================================================
# 罫線から矩形領域を検出
# =========================================================


# =========================================================
# NDLOCR-Lite JSONを解析
# =========================================================


# =========================================================
# 線分から罫線領域を生成
# =========================================================


# =========================================================
# 罫線領域内部の罫線座標を解析
# =========================================================


# =========================================================
# 罫線領域からセル候補を生成
# =========================================================


# =========================================================
# セル候補にOCR結果を割り当てる
# =========================================================


# =========================================================
# 自動レイアウト領域生成
# =========================================================



# =========================================================
# 罫線領域＋セルOCR結果を表領域に変換
# =========================================================




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