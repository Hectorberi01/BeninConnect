"""Génère les illustrations hors ligne de BéninConnect (aucune photo réelle, aucune licence tierce).

Usage : python3 tools/generate_assets.py  (depuis la racine du dépôt)
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "src", "BeninConnect.App", "Assets", "Images")

PALETTES = [
    ("#065F46", "#10B981", "#FCD116", "#E8112D"),
    ("#0E7490", "#67E8F9", "#FDE68A", "#F97316"),
    ("#7C2D12", "#F97316", "#FCD34D", "#14532D"),
    ("#1E3A8A", "#60A5FA", "#FBBF24", "#F472B6"),
    ("#4C1D95", "#A78BFA", "#FDE68A", "#34D399"),
    ("#134E4A", "#2DD4BF", "#FEF3C7", "#FB7185"),
    ("#78350F", "#F59E0B", "#FEF9C3", "#047857"),
    ("#831843", "#F472B6", "#FDE68A", "#0EA5E9"),
]


def rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def gradient(w, h, top, bottom, diagonal=False):
    img = Image.new("RGB", (w, h))
    px = img.load()
    t1, t2 = rgb(top), rgb(bottom)
    for y in range(h):
        for x in range(0, w, 1):
            t = (x / w * 0.4 + y / h * 0.6) if diagonal else y / h
            px[x, y] = mix(t1, t2, t)
    return img


def sunset_beach(w, h, pal, rnd):
    img = gradient(w, h, pal[3], pal[2])
    d = ImageDraw.Draw(img)
    sun_r = h // 5
    cx = int(w * rnd.uniform(0.3, 0.7))
    d.ellipse([cx - sun_r, int(h * 0.55) - sun_r, cx + sun_r, int(h * 0.55) + sun_r], fill=rgb("#FFF7ED"))
    sea_top = int(h * 0.62)
    d.rectangle([0, sea_top, w, h], fill=rgb(pal[0]))
    for i in range(8):
        y = sea_top + 10 + i * 14
        d.line([(rnd.randint(0, w // 2), y), (rnd.randint(w // 2, w), y)], fill=mix(rgb(pal[0]), (255, 255, 255), 0.35), width=3)
    for base_x in (int(w * 0.12), int(w * 0.85)):
        trunk_h = int(h * 0.45)
        d.line([(base_x, h), (base_x + 20, h - trunk_h)], fill=(30, 30, 30), width=10)
        top = (base_x + 20, h - trunk_h)
        for ang in range(0, 360, 45):
            rad = math.radians(ang)
            d.line([top, (top[0] + int(math.cos(rad) * 90), top[1] + int(math.sin(rad) * 40) + 20)], fill=(30, 30, 30), width=8)
    return img


def skyline(w, h, pal, rnd, snow=False):
    img = gradient(w, h, pal[0], pal[1])
    d = ImageDraw.Draw(img)
    for _ in range(60):
        x, y = rnd.randint(0, w), rnd.randint(0, h // 2)
        d.ellipse([x, y, x + 3, y + 3], fill=(255, 255, 255))
    x = 0
    while x < w:
        bw = rnd.randint(40, 110)
        bh = rnd.randint(int(h * 0.25), int(h * 0.7))
        color = mix(rgb(pal[0]), (0, 0, 0), 0.45)
        d.rectangle([x, h - bh, x + bw, h], fill=color)
        for wy in range(h - bh + 12, h - 10, 22):
            for wx in range(x + 8, x + bw - 8, 16):
                if rnd.random() < 0.45:
                    d.rectangle([wx, wy, wx + 7, wy + 10], fill=rgb(pal[2]))
        if snow:
            d.rectangle([x, h - bh - 6, x + bw, h - bh], fill=(245, 248, 255))
        x += bw + rnd.randint(2, 10)
    if snow:
        for _ in range(250):
            sx, sy = rnd.randint(0, w), rnd.randint(0, h)
            d.ellipse([sx, sy, sx + 4, sy + 4], fill=(255, 255, 255))
    return img


def food_bowl(w, h, pal, rnd):
    img = gradient(w, h, pal[2], pal[1], diagonal=True)
    d = ImageDraw.Draw(img)
    cx, cy, r = w // 2, h // 2 + 20, int(h * 0.36)
    d.ellipse([cx - r - 18, cy - r - 18, cx + r + 18, cy + r + 18], fill=(255, 255, 255))
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgb(pal[3]))
    for _ in range(26):
        a = rnd.uniform(0, math.tau)
        dist = rnd.uniform(0, r * 0.75)
        x, y = cx + math.cos(a) * dist, cy + math.sin(a) * dist
        s = rnd.randint(10, 26)
        d.ellipse([x - s, y - s, x + s, y + s], fill=rnd.choice([rgb(pal[0]), rgb("#FDE68A"), rgb("#14532D"), (255, 255, 255)]))
    d.ellipse([cx + r + 30, cy - 40, cx + r + 120, cy + 50], fill=(250, 250, 245))
    return img


def wax_pattern(w, h, pal, rnd):
    img = Image.new("RGB", (w, h), rgb(pal[0]))
    d = ImageDraw.Draw(img)
    step = 80
    for y in range(-step, h + step, step):
        for x in range(-step, w + step, step):
            off = (y // step) % 2 * step // 2
            cx, cy = x + off, y
            d.ellipse([cx - 32, cy - 32, cx + 32, cy + 32], fill=rgb(pal[1]))
            d.ellipse([cx - 18, cy - 18, cx + 18, cy + 18], fill=rgb(pal[2]))
            d.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=rgb(pal[3]))
            d.polygon([(cx + 40, cy), (cx + 52, cy - 12), (cx + 64, cy), (cx + 52, cy + 12)], fill=rgb(pal[2]))
    return img


def kente_stripes(w, h, pal, rnd):
    img = Image.new("RGB", (w, h), rgb(pal[0]))
    d = ImageDraw.Draw(img)
    x = 0
    while x < w:
        sw = rnd.randint(20, 60)
        d.rectangle([x, 0, x + sw, h], fill=rgb(rnd.choice(pal)))
        for y in range(0, h, 24):
            if rnd.random() < 0.5:
                d.rectangle([x + 4, y, x + sw - 4, y + 10], fill=rgb(rnd.choice(pal)))
        x += sw
    return img


def crowd(w, h, pal, rnd):
    img = gradient(w, h, pal[1], pal[0])
    d = ImageDraw.Draw(img)
    for row in range(4):
        y_base = int(h * 0.45) + row * 55
        for i in range(14):
            x = i * (w // 12) + rnd.randint(-15, 15) + (row % 2) * 30
            col = mix(rgb(pal[rnd.randint(0, 3)]), (0, 0, 0), 0.25 + row * 0.12)
            d.ellipse([x - 22, y_base - 70, x + 22, y_base - 26], fill=col)
            d.rounded_rectangle([x - 36, y_base - 22, x + 36, y_base + 60], radius=28, fill=col)
    d.rectangle([w * 0.2, 30, w * 0.8, 110], fill=mix(rgb(pal[2]), (255, 255, 255), 0.4))
    return img


def tech(w, h, pal, rnd):
    img = gradient(w, h, pal[0], pal[1], diagonal=True)
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = rnd.randint(0, w), rnd.randint(0, h)
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=rgb(pal[2]))
        x2, y2 = x + rnd.randint(-160, 160), y + rnd.randint(-100, 100)
        d.line([(x, y), (x2, y2)], fill=mix(rgb(pal[2]), rgb(pal[0]), 0.5), width=2)
    lw, lh = int(w * 0.46), int(h * 0.42)
    lx, ly = (w - lw) // 2, (h - lh) // 2 - 20
    d.rounded_rectangle([lx, ly, lx + lw, ly + lh], radius=14, fill=(20, 24, 33))
    d.rounded_rectangle([lx + 12, ly + 12, lx + lw - 12, ly + lh - 12], radius=6, fill=rgb(pal[1]))
    for i in range(6):
        d.rectangle([lx + 30, ly + 34 + i * 22, lx + 30 + rnd.randint(80, lw - 80), ly + 44 + i * 22], fill=(255, 255, 255))
    d.polygon([(lx - 40, ly + lh), (lx + lw + 40, ly + lh), (lx + lw + 70, ly + lh + 26), (lx - 70, ly + lh + 26)], fill=(40, 44, 55))
    return img


def plane(w, h, pal, rnd):
    img = gradient(w, h, pal[1], "#FFFFFF")
    d = ImageDraw.Draw(img)
    for _ in range(7):
        cx, cy = rnd.randint(0, w), rnd.randint(h // 2, h)
        for k in range(5):
            r = rnd.randint(40, 80)
            ox = rnd.randint(-80, 80)
            d.ellipse([cx + ox - r, cy - r // 2, cx + ox + r, cy + r // 2], fill=(255, 255, 255))
    px, py = int(w * 0.55), int(h * 0.35)
    d.polygon([(px - 160, py + 20), (px + 160, py - 20), (px + 180, py - 8), (px - 140, py + 34)], fill=(250, 250, 250))
    d.polygon([(px - 10, py), (px + 60, py - 110), (px + 90, py - 110), (px + 50, py + 4)], fill=(235, 238, 242))
    d.polygon([(px - 120, py + 22), (px - 140, py - 30), (px - 115, py - 30), (px - 95, py + 18)], fill=rgb(pal[3]))
    return img


def graduation(w, h, pal, rnd):
    img = gradient(w, h, pal[0], pal[1], diagonal=True)
    d = ImageDraw.Draw(img)
    for _ in range(80):
        x, y = rnd.randint(0, w), rnd.randint(0, h)
        d.rectangle([x, y, x + 8, y + 14], fill=rgb(rnd.choice(pal[2:])))
    cx, cy = w // 2, h // 2
    d.polygon([(cx - 180, cy), (cx, cy - 80), (cx + 180, cy), (cx, cy + 80)], fill=(15, 15, 20))
    d.rectangle([cx - 90, cy + 20, cx + 90, cy + 90], fill=(15, 15, 20))
    d.line([(cx, cy), (cx + 140, cy + 40), (cx + 140, cy + 120)], fill=rgb(pal[2]), width=6)
    return img


def books(w, h, pal, rnd):
    img = gradient(w, h, pal[2], pal[1])
    d = ImageDraw.Draw(img)
    x = int(w * 0.1)
    shelf_y = int(h * 0.8)
    while x < w * 0.9:
        bw = rnd.randint(26, 48)
        bh = rnd.randint(int(h * 0.35), int(h * 0.62))
        d.rectangle([x, shelf_y - bh, x + bw, shelf_y], fill=rgb(rnd.choice(pal)))
        d.rectangle([x + 4, shelf_y - bh + 20, x + bw - 4, shelf_y - bh + 26], fill=(255, 255, 255))
        x += bw + 3
    d.rectangle([0, shelf_y, w, shelf_y + 20], fill=mix(rgb(pal[0]), (0, 0, 0), 0.3))
    return img


def lagoon(w, h, pal, rnd):
    img = gradient(w, h, pal[2], pal[1])
    d = ImageDraw.Draw(img)
    water = int(h * 0.6)
    d.rectangle([0, water, w, h], fill=rgb(pal[0]))
    for i in range(5):
        hx = int(w * (0.1 + i * 0.2))
        top = water - rnd.randint(70, 110)
        for sx in (hx - 40, hx + 40):
            d.line([(sx, top + 50), (sx, water + 40)], fill=(60, 40, 25), width=6)
        d.rectangle([hx - 50, top + 20, hx + 50, top + 60], fill=(120, 85, 50))
        d.polygon([(hx - 64, top + 24), (hx, top - 20), (hx + 64, top + 24)], fill=(170, 130, 60))
    for _ in range(3):
        bx = rnd.randint(50, w - 150)
        by = water + rnd.randint(30, 90)
        d.polygon([(bx, by), (bx + 120, by), (bx + 100, by + 16), (bx + 20, by + 16)], fill=(50, 35, 25))
    return img


def drums(w, h, pal, rnd):
    img = wax_pattern(w, h, (pal[0], pal[0], pal[1], pal[0]), rnd)
    img = img.filter(ImageFilter.GaussianBlur(3))
    d = ImageDraw.Draw(img)
    for i, x in enumerate((w * 0.3, w * 0.5, w * 0.7)):
        x = int(x)
        top = int(h * (0.3 + 0.05 * i))
        d.polygon([(x - 60, top), (x + 60, top), (x + 40, h - 30), (x - 40, h - 30)], fill=rgb(pal[3]))
        d.ellipse([x - 60, top - 20, x + 60, top + 20], fill=rgb("#FDE68A"))
        for k in range(5):
            d.line([(x - 55 + k * 27, top + 10), (x - 30 + k * 15, h - 30)], fill=(40, 25, 15), width=3)
    return img


def field(w, h, pal, rnd):
    img = gradient(w, h, pal[2], pal[1])
    d = ImageDraw.Draw(img)
    horizon = int(h * 0.4)
    d.rectangle([0, horizon, w, h], fill=rgb(pal[0]))
    for i in range(12):
        y = horizon + i * (h - horizon) // 12
        d.line([(0, y), (w, y)], fill=mix(rgb(pal[0]), (255, 255, 255), 0.15), width=2)
    for i in range(-6, 7):
        d.line([(w // 2, horizon), (w // 2 + i * 160, h)], fill=mix(rgb(pal[0]), (255, 255, 255), 0.25), width=3)
    d.ellipse([w * 0.7, h * 0.08, w * 0.7 + 90, h * 0.08 + 90], fill=(255, 250, 230))
    return img


def football(w, h, pal, rnd):
    img = Image.new("RGB", (w, h), rgb("#15803D"))
    d = ImageDraw.Draw(img)
    for i in range(0, w, 80):
        d.rectangle([i, 0, i + 40, h], fill=rgb("#16A34A"))
    d.rectangle([20, 20, w - 20, h - 20], outline=(255, 255, 255), width=5)
    d.line([(w // 2, 20), (w // 2, h - 20)], fill=(255, 255, 255), width=5)
    d.ellipse([w // 2 - 70, h // 2 - 70, w // 2 + 70, h // 2 + 70], outline=(255, 255, 255), width=5)
    bx, by = int(w * 0.62), int(h * 0.62)
    d.ellipse([bx - 34, by - 34, bx + 34, by + 34], fill=(255, 255, 255))
    d.regular_polygon((bx, by, 13), 5, fill=(20, 20, 20))
    return img


def festival(w, h, pal, rnd):
    img = gradient(w, h, "#0B1220", pal[0])
    d = ImageDraw.Draw(img)
    for _ in range(14):
        x, y = rnd.randint(0, w), rnd.randint(0, h // 2)
        r = rnd.randint(30, 90)
        col = rgb(rnd.choice(pal[1:]))
        for k in range(r, 0, -6):
            d.ellipse([x - k, y - k, x + k, y + k], fill=mix(col, rgb("#0B1220"), k / r))
    stage = int(h * 0.7)
    d.polygon([(0, h), (w * 0.2, stage), (w * 0.8, stage), (w, h)], fill=(20, 20, 30))
    for i in range(30):
        x = rnd.randint(0, w)
        d.ellipse([x - 16, h - 60 + rnd.randint(-10, 10), x + 16, h - 28], fill=(5, 5, 10))
    return img


POST_GENERATORS = [
    sunset_beach, skyline, food_bowl, wax_pattern, graduation, crowd, tech, plane,
    lambda w, h, p, r: skyline(w, h, p, r, snow=True), kente_stripes, drums, field, football, books, lagoon, festival,
]


def soften(img):
    overlay = Image.new("RGB", img.size, (255, 255, 255))
    return Image.blend(img, overlay, 0.04)


def save_jpg(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "JPEG", quality=82, optimize=True, progressive=True)


def community_cover(i, rnd):
    pal = PALETTES[i % len(PALETTES)]
    gen = [wax_pattern, kente_stripes, skyline, crowd, tech, sunset_beach, field, lagoon][i % 8]
    img = gen(800, 400, pal, rnd)
    if gen in (wax_pattern, kente_stripes):
        img = img.filter(ImageFilter.GaussianBlur(1.2))
    return img


def onboarding(index, rnd):
    w = h = 640
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    green, yellow, red, light = rgb("#087F5B"), rgb("#FCD116"), rgb("#E8112D"), rgb("#D1FAE5")
    d.ellipse([60, 60, w - 60, h - 60], fill=light + (255,))
    if index == 1:
        # Globe et membres reliés
        d.ellipse([170, 170, 470, 470], fill=green + (255,))
        for k in range(1, 4):
            d.arc([170, 170 + k * 40, 470, 470 - k * 40], 0, 360, fill=(255, 255, 255, 140), width=3)
        d.line([(320, 170), (320, 470)], fill=(255, 255, 255, 140), width=3)
        pts = [(150, 220), (500, 200), (520, 430), (130, 450), (320, 110)]
        for a in pts:
            for b in pts:
                d.line([a, b], fill=green + (90,), width=2)
        for j, (x, y) in enumerate(pts):
            col = [yellow, red, green, yellow, red][j]
            d.ellipse([x - 30, y - 30, x + 30, y + 30], fill=(255, 255, 255, 255))
            d.ellipse([x - 22, y - 22, x + 22, y + 22], fill=col + (255,))
    elif index == 2:
        # Bulles de discussion + calendrier
        d.rounded_rectangle([130, 160, 400, 320], radius=40, fill=green + (255,))
        d.polygon([(170, 310), (160, 370), (230, 318)], fill=green + (255,))
        for k in range(3):
            d.rounded_rectangle([170, 200 + k * 34, 360 - k * 50, 218 + k * 34], radius=8, fill=(255, 255, 255, 230))
        d.rounded_rectangle([300, 300, 520, 500], radius=26, fill=(255, 255, 255, 255))
        d.rounded_rectangle([300, 300, 520, 350], radius=26, fill=red + (255,))
        d.rectangle([300, 330, 520, 350], fill=red + (255,))
        for r_ in range(3):
            for c_ in range(4):
                x = 325 + c_ * 48
                y = 370 + r_ * 40
                fill = yellow if (r_, c_) == (1, 2) else rgb("#E5E7EB")
                d.rounded_rectangle([x, y, x + 32, y + 26], radius=6, fill=fill + (255,))
    else:
        # Réseau mondial : continents stylisés et lignes
        d.ellipse([140, 140, 500, 500], fill=(255, 255, 255, 255))
        blobs = [(220, 230, 90, 60), (360, 210, 70, 50), (300, 330, 60, 90), (420, 360, 50, 40), (210, 390, 40, 30)]
        for (x, y, rx, ry) in blobs:
            d.ellipse([x - rx, y - ry, x + rx, y + ry], fill=green + (255,))
        route = [(220, 230), (360, 210), (300, 340), (420, 360), (210, 390)]
        d.line(route + [route[0]], fill=yellow + (255,), width=5)
        for (x, y) in route:
            d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=red + (255,))
    return img


def main():
    rnd = random.Random(2026)
    for i, gen in enumerate(POST_GENERATORS, start=1):
        img = soften(gen(800, 500, PALETTES[(i * 3) % len(PALETTES)], rnd))
        save_jpg(img, os.path.join(ROOT, "Posts", f"post_{i:02d}.jpg"))

    for i in range(15):
        save_jpg(soften(community_cover(i, rnd)), os.path.join(ROOT, "Covers", f"community_{i + 1:02d}.jpg"))

    event_gens = [food_bowl, tech, crowd, books, football, crowd, sunset_beach, festival, drums, lagoon]
    for i, gen in enumerate(event_gens, start=1):
        save_jpg(soften(gen(800, 420, PALETTES[(i * 5) % len(PALETTES)], rnd)), os.path.join(ROOT, "Events", f"event_{i:02d}.jpg"))

    cover_gens = [kente_stripes, wax_pattern, sunset_beach, skyline, lagoon, field]
    for i, gen in enumerate(cover_gens, start=1):
        img = gen(900, 340, PALETTES[(i * 2) % len(PALETTES)], rnd).filter(ImageFilter.GaussianBlur(1.5))
        save_jpg(img, os.path.join(ROOT, "Profile", f"cover_{i:02d}.jpg"))

    os.makedirs(os.path.join(ROOT, "Onboarding"), exist_ok=True)
    for i in (1, 2, 3):
        onboarding(i, rnd).save(os.path.join(ROOT, "Onboarding", f"onboarding_{i}.png"), optimize=True)

    # Icône Android
    icon = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(icon)
    d.rounded_rectangle([0, 0, 511, 511], radius=120, fill=rgb("#087F5B") + (255,))
    for a, b in [((160, 320), (256, 160)), ((256, 160), (352, 320)), ((160, 320), (352, 320))]:
        d.line([a, b], fill=(255, 255, 255, 220), width=22)
    d.ellipse([200, 104, 312, 216], fill=(255, 255, 255, 255))
    d.ellipse([104, 264, 216, 376], fill=rgb("#FCD116") + (255,))
    d.ellipse([296, 264, 408, 376], fill=rgb("#E8112D") + (255,))
    icon.save(os.path.join(os.path.dirname(__file__), "..", "src", "BeninConnect.Android", "Icon.png"))


if __name__ == "__main__":
    main()
