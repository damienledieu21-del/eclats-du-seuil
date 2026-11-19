#!/usr/bin/env python3
"""Sous-lot F4 : icônes peintes des 108 références des fêtes d'Orsédia (PNG 128 x 128 avec transparence).

- Plat, monnaie et récompense (P01, M01, R01) : l'icône 627 x 627 du dossier des fêtes (`Icones/…_icone.png`), cadre gardé, marge grise
  retirée (recadrage au carré du cadre), réduite à 128.
- Décorations et costume (D01 à D05, C01) : l'image de référence (`Objets/….png`, fond gris uni) détourée (le fond, couleur des coins,
  devient transparent avec un bord adouci), recadrée sur l'objet et centrée dans 128 x 128 (marge de 6 px).

Usage : python3 -I scripts/icones_fetes.py DOSSIER_DES_FETES DOSSIER_DE_SORTIE
(DOSSIER_DES_FETES : `Eclats_du_Seuil_AssetLab/Fetes`, qui contient `EDS_Fetes_T1_Janvier_Mars`… ; sortie : `fete_f01_d01.png`…)
Demande Pillow. Déterministe.
"""
import glob
import os
import sys

from PIL import Image, ImageChops, ImageFilter

SIZE = 128


def framed(path):
    im = Image.open(path).convert('RGB')
    w, h = im.size
    # Le cadre occupe le carré central (environ 12 % de marge grise de chaque côté).
    m = int(w * 0.105)
    return im.crop((m, m, w - m, h - m)).resize((SIZE, SIZE), Image.LANCZOS).convert('RGBA')


def cut_out(path):
    im = Image.open(path).convert('RGB')
    w, h = im.size
    corners = [im.getpixel(p) for p in ((2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3))]
    bg = tuple(sum(c[i] for c in corners) // 4 for i in range(3))
    diff = ImageChops.difference(im, Image.new('RGB', im.size, bg)).convert('L')
    # Écart au fond : 0 à 14 transparent, 40 et plus opaque, entre les deux adouci.
    alpha = diff.point(lambda v: 0 if v < 14 else 255 if v >= 40 else int((v - 14) * 255 / 26))
    alpha = alpha.filter(ImageFilter.MedianFilter(5))
    box = alpha.point(lambda v: 255 if v > 60 else 0).getbbox() or (0, 0, w, h)
    rgba = im.convert('RGBA')
    rgba.putalpha(alpha)
    rgba = rgba.crop(box)
    inner = SIZE - 12
    scale = inner / max(rgba.size)
    rgba = rgba.resize((max(1, round(rgba.size[0] * scale)), max(1, round(rgba.size[1] * scale))), Image.LANCZOS)
    out = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    out.alpha_composite(rgba, ((SIZE - rgba.size[0]) // 2, (SIZE - rgba.size[1]) // 2))
    return out


def main(root, out):
    os.makedirs(out, exist_ok=True)
    done = 0
    for obj in sorted(glob.glob(os.path.join(root, 'EDS_Fetes_T*', 'Objets', 'F[0-9][0-9]_[A-Z][0-9][0-9]_*.png'))):
        name = os.path.basename(obj)
        code = name[:7]
        kind = code[4]
        if kind in 'PMR':
            icon = glob.glob(os.path.join(os.path.dirname(os.path.dirname(obj)), 'Icones', code + '_*_icone.png'))
            if not icon:
                sys.exit('icône manquante : ' + code)
            img = framed(icon[0])
        else:
            img = cut_out(obj)
        img.save(os.path.join(out, 'fete_' + code.lower() + '.png'), optimize=True)
        done += 1
    print(done)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
