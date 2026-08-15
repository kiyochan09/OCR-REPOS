from paddleocr import PaddleOCR
import traceback
import sys

print("①開始", flush=True)

try:
    ocr = PaddleOCR(
        lang="japan",
        enable_mkldnn=False,
    )

    print("②エンジン作成完了", flush=True)

    print("③predict開始", flush=True)

    result = ocr.predict("test.png")

    print("④OCR実行完了", flush=True)
    print("検出数 =", len(result), flush=True)

except Exception:
    print("⑤例外発生", flush=True)
    traceback.print_exc()

finally:
    print("⑥Python終了", flush=True)