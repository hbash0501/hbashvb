# يحاكي عيوب المسح الحقيقية على الصور النظيفة بأربعة مستويات، بقيم ثابتة البذرة لتكرار القياس.
# python3 degrade.py <مجلد الصور النظيفة> <مجلد الإخراج>
import io, json, os, random, sys
import numpy as np
from PIL import Image, ImageFilter, ImageEnhance

LEVELS = {
    "clean":    dict(rot=0.0, scale=1.0,  blur=0.0, jpeg=92, noise=0,  contrast=1.0, gradient=0),
    "mild":     dict(rot=1.2, scale=0.85, blur=0.6, jpeg=75, noise=5,  contrast=0.9, gradient=25),
    "moderate": dict(rot=3.0, scale=0.67, blur=1.0, jpeg=50, noise=10, contrast=0.75, gradient=45),
    "severe":   dict(rot=5.5, scale=0.5,  blur=1.4, jpeg=35, noise=16, contrast=0.6, gradient=70),
}

def degrade(img, cfg, rng):
    g = img.convert("L")
    if cfg["contrast"] != 1.0:
        g = ImageEnhance.Contrast(g).enhance(cfg["contrast"])
    if cfg["rot"]:
        angle = cfg["rot"] * rng.choice([-1, 1]) * rng.uniform(0.7, 1.0)
        g = g.rotate(angle, resample=Image.BICUBIC, expand=True, fillcolor=245)
    if cfg["scale"] != 1.0:
        w, h = g.size
        g = g.resize((int(w * cfg["scale"]), int(h * cfg["scale"])), Image.BILINEAR)
    if cfg["blur"]:
        g = g.filter(ImageFilter.GaussianBlur(cfg["blur"]))
    a = np.asarray(g).astype(np.float32)
    if cfg["gradient"]:
        h, w = a.shape
        ramp = np.linspace(0, cfg["gradient"], w)[None, :] * np.linspace(0.4, 1.0, h)[:, None]
        a = a - ramp
    if cfg["noise"]:
        a = a + np.random.default_rng(rng.randint(0, 10**9)).normal(0, cfg["noise"], a.shape)
    g = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    buf = io.BytesIO()
    g.save(buf, "JPEG", quality=cfg["jpeg"])
    return Image.open(io.BytesIO(buf.getvalue()))

src, dst = sys.argv[1], sys.argv[2]
os.makedirs(dst, exist_ok=True)
truth = json.load(open(os.path.join(src, "truth.json"), encoding="utf-8"))
out = []
for t in truth:
    img = Image.open(os.path.join(src, t["name"] + ".png"))
    for level, cfg in LEVELS.items():
        rng = random.Random(f'{t["name"]}-{level}')
        name = f'{t["name"]}-{level}.jpg'
        degrade(img, cfg, rng).save(os.path.join(dst, name), quality=95)
        out.append(dict(t, file=name, level=level))
json.dump(out, open(os.path.join(dst, "truth.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("images:", len(out))
