# Planche récapitulative des 7 rendus et du plan coté (python3 système + PIL).
#   python3 scripts/planche.py  -> renders/planche_boulangerie_firmin.png
import os, json
from PIL import Image, ImageDraw, ImageFont
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))); R = os.path.join(ROOT, "renders")
VUES = [("01_facade_orthographique", "1 · Façade — orthographique"), ("02_cote_droit_orthographique", "2 · Côté droit (fournil, cul-de-four) — ortho."),
        ("03_cote_gauche_orthographique", "3 · Côté gauche — orthographique"), ("04_arriere_orthographique", "4 · Arrière — orthographique"),
        ("05_vue_trois_quarts", "5 · Trois-quarts, caméra à 1,70 m"), ("06_vue_de_dessus_toiture", "6 · Toiture vue de dessus — ortho."),
        ("07_interieur_boutique", "7 · Intérieur de la boutique, vue vers la baie et le four"), ("plan_cote", "Plan coté (document séparé : PLAN_COORDONNEES.md)")]
def font(sz, bold=False):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf" if bold else "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",):
        if os.path.exists(p): return ImageFont.truetype(p, sz)
    return ImageFont.load_default()
TW, CAP, PAD = 1200, 54, 30
tiles = []
for f, cap in VUES:
    p = os.path.join(R, f + ".png")
    if not os.path.exists(p): print("absent :", f); continue
    im = Image.open(p).convert("RGB"); w = TW if f not in ("07_interieur_boutique", "plan_cote") else TW*3//2 + PAD//2
    im = im.resize((w, int(w*im.size[1]/im.size[0])), Image.LANCZOS)
    tiles.append((im, cap, f))
rows = [tiles[0:3], tiles[3:6], tiles[6:8]]
W = 3*TW + 4*PAD
H = 160 + sum(max(t[0].size[1] for t in r) + CAP + PAD for r in rows if r) + 90
board = Image.new("RGB", (W, H), (236, 233, 226)); d = ImageDraw.Draw(board)
d.text((PAD, 40), "Éclats du Seuil — Vallons d'Aube · Boulangerie de Firmin", font=font(58, True), fill=(50, 38, 28))
meta = {}
mp = os.path.join(R, "rendus_meta.json")
if os.path.exists(mp): meta = json.load(open(mp))
sig = {m["signature"] for m in meta.values()} if meta else set()
d.text((PAD, 112), f"Un seul modèle Blender (Boulangerie_Firmin.blend, signature {', '.join(sig) or '?'}) · cotes en m · façade 14,50 m, porte à X = 7,25 · faîtage 10,67",
       font=font(26), fill=(90, 80, 70))
y = 160
for r in rows:
    if not r: continue
    x = PAD; hmax = max(t[0].size[1] for t in r)
    for im, cap, f in r:
        board.paste(im, (x, y + CAP))
        d.text((x, y + 8), cap, font=font(30, True), fill=(60, 45, 32))
        x += im.size[0] + PAD
    y += hmax + CAP + PAD
d.text((PAD, H - 60), "Rendus Cycles, fond gris neutre et lumière naturelle douce (soleil orienté pour chaque vue) ; intérieur : jour par les baies et lanternes. Les annotations figurent seulement sur le plan coté.",
       font=font(24), fill=(90, 80, 70))
out = os.path.join(R, "planche_boulangerie_firmin.png"); board.save(out, optimize=True); print(out, board.size)
