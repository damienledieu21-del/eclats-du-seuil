#!/usr/bin/env python3
"""Sous-lot V2.33 : icônes peintes des poissons et des appâts (pêche, #181).

Même méthode que `defense_de_sanglier`, `ferrure` et `peau_de_loup` (essai réaliste 6) : une silhouette de game-icons.net
(CC BY 3.0, auteurs cités dans unity/Assets/EdS/Interface/LICENCES.md et docs/CREDITS.md), peinte par le code (dégradé de la
couleur de l'espèce, motif, relief, contour sombre, ombre portée), en PNG 128 x 128 avec transparence.

Sources (https://github.com/game-icons/icons : le fichier `<auteur>/<nom>.svg` du dépôt est attendu, dans le dossier donné en argument,
sous le nom `<auteur>_<nom>.svg`, par exemple `delapouite_fish-escape.svg` pour `delapouite/fish-escape.svg`) :
  delapouite/fish-escape.svg  (le poisson qui saute, seul : la forme qui contient le point 400, 80) -> les cinq poissons
  cathelineau/earth-worm.svg                                                    -> item.ver_de_terre
  lorc/dragonfly.svg                                                             -> item.mouche_de_mai
  darkzaitzev/fried-fish.svg                                                     -> item.friture_du_lac
  darkzaitzev/fish-cooked.svg                                                    -> item.truite_meuniere
  delapouite/hot-meal.svg                                                        -> item.quenelles_de_brochet
  delapouite/cooking-pot.svg                                                     -> item.matelote_de_carpe_doree

Usage : python3 -I scripts/icones_peche.py DOSSIER_DES_SVG DOSSIER_DE_SORTIE
Demande rsvg-convert et Pillow. Déterministe (motifs tirés d'une graine fixe).
"""
import io
import math
import random
import re
import subprocess
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

SIZE = 128
WORK = 512


def silhouette(svg_text, seed=None):
    """
    Masque (L, WORK x WORK) des tracés blancs d'une icône de game-icons.net (le carré noir du fond est retiré). Avec `seed` (x, y en
    pixels de travail), seule la forme d'un seul tenant qui contient ce point est gardée (ses trous, comme l'œil, restent vides).
    """
    paths = re.findall(r'<path[^>]*fill="#fff"[^>]*/>', svg_text)
    if not paths:
        raise SystemExit("aucun tracé blanc")
    doc = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">' + "".join(paths) + "</svg>"
    png = subprocess.run(["rsvg-convert", "-w", str(WORK), "-h", str(WORK)], input=doc.encode(), capture_output=True, check=True).stdout
    mask = Image.open(io.BytesIO(png)).convert("RGBA").split()[3]
    if seed is None:
        return mask
    solid = mask.point(lambda v: 255 if v >= 128 else 0)
    ImageDraw.floodfill(solid, seed, 100)
    keep = solid.point(lambda v: 255 if v == 100 else 0).filter(ImageFilter.MaxFilter(3))
    return ImageChops.multiply(mask, keep)


def fit(mask, rotate=0.0, stretch=(1.0, 1.0), margin=12):
    """Tourne, étire, recadre et centre la silhouette dans un carré de 128 (marge pour l'ombre)."""
    if rotate:
        mask = mask.rotate(rotate, resample=Image.BICUBIC, expand=True)
    box = mask.getbbox()
    mask = mask.crop(box)
    w, h = mask.size
    mask = mask.resize((max(1, int(w * stretch[0])), max(1, int(h * stretch[1]))), Image.LANCZOS)
    w, h = mask.size
    scale = (SIZE - 2 * margin) / max(w, h)
    mask = mask.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    out = Image.new("L", (SIZE, SIZE), 0)
    out.paste(mask, ((SIZE - mask.size[0]) // 2, (SIZE - mask.size[1]) // 2))
    return out


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def paint(mask, top, bottom, pattern=None, seed=1):
    """Peint la silhouette : dégradé vertical, grain, motif, relief, contour et ombre."""
    rng = random.Random(seed)
    box = mask.getbbox()
    y0, y1 = box[1], box[3]
    body = Image.new("RGB", (SIZE, SIZE))
    px = body.load()
    for y in range(SIZE):
        t = min(1.0, max(0.0, (y - y0) / max(1, y1 - y0)))
        c = lerp(top, bottom, t)
        for x in range(SIZE):
            g = rng.randint(-9, 9)
            px[x, y] = tuple(max(0, min(255, v + g)) for v in c)
    if pattern:
        layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        pattern(ImageDraw.Draw(layer), box, rng)
        body = Image.alpha_composite(body.convert("RGBA"), layer).convert("RGB")
    # Relief : lumière en haut à gauche, ombre en bas à droite, d'après le bord de la silhouette.
    soft = mask.filter(ImageFilter.GaussianBlur(2.2))
    light = ImageChops.subtract(soft, ImageChops.offset(soft, 2, 2))
    dark = ImageChops.subtract(soft, ImageChops.offset(soft, -2, -2))
    body = ImageChops.add(body, Image.merge("RGB", [light.point(lambda v: v * 0.8)] * 3))
    body = ImageChops.subtract(body, Image.merge("RGB", [dark.point(lambda v: v * 0.7)] * 3))
    icon = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    # Ombre portée, puis contour sombre (silhouette grossie), puis le corps.
    shadow = mask.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(3))
    icon.paste((0, 0, 0, 150), (3, 4), ImageChops.offset(shadow, 0, 0).crop((0, 0, SIZE - 3, SIZE - 4)))
    outline = mask.filter(ImageFilter.MaxFilter(5))
    icon.paste((22, 18, 14, 255), (0, 0), outline)
    icon.paste(body, (0, 0), mask)
    return icon


def spots(color, count, radius):
    def draw(d, box, rng):
        for _ in range(count):
            x = rng.uniform(box[0], box[2])
            y = rng.uniform(box[1], box[3])
            r = rng.uniform(radius * 0.6, radius)
            d.ellipse((x - r, y - r, x + r, y + r), fill=color)
    return draw


def stripes(color, count, width):
    def draw(d, box, rng):
        for i in range(count):
            x = box[0] + (box[2] - box[0]) * (0.25 + 0.5 * i / max(1, count - 1))
            d.line((x - 10, box[1], x + 10, box[3]), fill=color, width=width)
    return draw


def scales(color):
    def draw(d, box, rng):
        for y in range(box[1], box[3], 7):
            for x in range(box[0] + (y // 7 % 2) * 4, box[2], 8):
                d.arc((x - 4, y - 4, x + 4, y + 4), 20, 160, fill=color, width=1)
    return draw


def bands(color, count):
    def draw(d, box, rng):
        h = box[3] - box[1]
        for i in range(1, count + 1):
            y = box[1] + h * i / (count + 1)
            d.line((box[0], y, box[2], y + 6), fill=color, width=2)
    return draw


def main(src, out):
    fish = silhouette(open(f"{src}/delapouite_fish-escape.svg").read(), seed=(400, 80))
    worm = silhouette(open(f"{src}/cathelineau_earth-worm.svg").read())
    fly = silhouette(open(f"{src}/lorc_dragonfly.svg").read())
    fried = silhouette(open(f"{src}/darkzaitzev_fried-fish.svg").read())
    cooked = silhouette(open(f"{src}/darkzaitzev_fish-cooked.svg").read())
    cloche = silhouette(open(f"{src}/delapouite_hot-meal.svg").read())
    pot = silhouette(open(f"{src}/delapouite_cooking-pot.svg").read())
    icons = {
        # Gardon : argenté, nageoires rougies (dégradé vers le rose en bas).
        "gardon": paint(fit(fish), (206, 214, 222), (176, 120, 120), scales((255, 255, 255, 70)), seed=11),
        # Perche : vert olive, bandes sombres verticales.
        "perche": paint(fit(fish, stretch=(1.0, 1.15)), (128, 150, 70), (214, 170, 90), stripes((40, 50, 20, 150), 5, 5), seed=12),
        # Truite : brun doré, points noirs et rouges.
        "truite": paint(fit(fish), (150, 120, 70), (225, 205, 150),
                        lambda d, b, r: (spots((30, 25, 20, 190), 22, 2.2)(d, b, r), spots((200, 50, 40, 200), 8, 1.8)(d, b, r)), seed=13),
        # Brochet : long, vert sombre, taches claires.
        "brochet": paint(fit(fish, rotate=-12, stretch=(1.45, 0.8)), (60, 90, 50), (180, 190, 120), spots((215, 220, 160, 170), 26, 2.4), seed=14),
        # Carpe dorée : trapue, or, grosses écailles.
        "carpe_doree": paint(fit(fish, stretch=(0.95, 1.3)), (232, 180, 60), (180, 110, 30), scales((120, 70, 10, 120)), seed=15),
        # Ver de terre : rose brun, anneaux.
        "ver_de_terre": paint(fit(worm), (196, 120, 120), (150, 80, 80), bands((110, 50, 50, 140), 9), seed=16),
        # Mouche de mai : ailes claires, corps ambré.
        "mouche_de_mai": paint(fit(fly), (236, 228, 196), (168, 128, 60), bands((120, 90, 40, 90), 6), seed=17),
        # Plats de poisson (décision de Damien du 16 novembre 2026, vers 14 h) : même méthode.
        # Friture du lac : panure dorée, mouchetée.
        "friture_du_lac": paint(fit(fried), (238, 196, 96), (176, 112, 40), spots((120, 70, 20, 120), 40, 1.6), seed=18),
        # Truite meunière : beurre noisette, sur la braise.
        "truite_meuniere": paint(fit(cooked), (214, 170, 110), (150, 84, 40), scales((90, 50, 20, 90)), seed=19),
        # Quenelles de brochet : la cloche d'argent du service, reflets crème.
        "quenelles_de_brochet": paint(fit(cloche), (236, 236, 228), (168, 160, 140), bands((255, 248, 220, 60), 4), seed=20),
        # Matelote de carpe dorée au cidre : la marmite, cuivre et sauce ambrée.
        "matelote_de_carpe_doree": paint(fit(pot), (200, 120, 70), (120, 60, 30), bands((240, 190, 90, 80), 3), seed=21),
    }
    for name, icon in icons.items():
        icon.save(f"{out}/{name}.png", optimize=True)
        print(name)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
