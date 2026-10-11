"""从品牌展示 PNG 切出图标，生成 Windows 多尺寸 .ico。

一次性工具：图标改版时重跑一次即可。放 tools/ 不参与构建。
"""
from PIL import Image
import numpy as np

SRC = "icon/brand-showcase.png"
OUT = "icon/MuseSlice.ico"
BG = np.array([242, 242, 242])  # 展示图的近白底

im = Image.open(SRC).convert("RGB")
a = np.array(im).astype(int)

# 只在左半张里找图标（右半边是「图标+MuseSlice」文字版）
mask = np.abs(a[:, :520] - BG).sum(axis=2) > 60
ys, xs = np.where(mask)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()

# 裁下来后补成正方形并留 6% 边距，否则 16x16 时贴边会糊
mark = im.crop((x0, y0, x1 + 1, y1 + 1))
side = int(max(mark.size) * 1.12)
canvas = Image.new("RGB", (side, side), tuple(BG))
canvas.paste(mark, ((side - mark.width) // 2, (side - mark.height) // 2))

canvas.resize((256, 256), Image.LANCZOS).save(
    OUT, format="ICO",
    sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)],
)
print(f"wrote {OUT} from bbox ({x0},{y0})-({x1},{y1})")
