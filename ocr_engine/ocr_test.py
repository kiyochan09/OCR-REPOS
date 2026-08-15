from paddleocr import PaddleOCR

IMAGE_PATH = "test.png"

ocr = PaddleOCR(
    lang="japan",
    enable_mkldnn=False,
)

result = ocr.predict(IMAGE_PATH)

for res in result:
    res.print()