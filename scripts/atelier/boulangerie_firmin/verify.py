# Vérifications automatiques de la Boulangerie de Firmin.
#   /tmp/bvenv/bin/python -u -I scripts/verify.py [fichier.blend]   (défaut : Boulangerie_Firmin.blend)
# Écrit verification.json (et l'affiche). Chaque test : nom, ok (vrai/faux), mesure, attendu.
import sys, os, math, json, hashlib, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import bf_plan as P
from bf_lib import ROOT, open_blend, tri_count

args = [a for a in sys.argv[1:] if a.endswith(".blend")]
BLEND = args[0] if args else os.path.join(ROOT, "Boulangerie_Firmin.blend")
open_blend(BLEND)
sc = bpy.context.scene
for lc in bpy.context.view_layer.layer_collection.children:      # les rayons ne voient que le modèle détaillé
    if lc.name in ("LOD", "Collisions", "Materiaux", "Studio_Rendu", "Effets_Rendu"): lc.exclude = True
R = []
def test(nom, ok, mesure, attendu=""):
    R.append(dict(nom=nom, ok=bool(ok), mesure=mesure, attendu=attendu))
    print(("OK   " if ok else "ÉCHEC"), nom, "|", mesure, "|", attendu, flush=True)

def objs(prefix=None, cname=None):
    out = []
    for o in bpy.data.objects:
        if o.type != "MESH": continue
        if prefix and not o.name.startswith(prefix): continue
        if cname and not any(c.name == cname for c in o.users_collection): continue
        out.append(o)
    return out

def bbox(os_):
    xs, ys, zs = [], [], []
    for o in os_:
        for c in o.bound_box:
            v = o.matrix_world @ Vector(c); xs.append(v.x); ys.append(v.y); zs.append(v.z)
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)) if xs else None

def r3(t): return [round(v, 3) for v in t]

# ---------------------------------------------------------------- 1. repère et unités
us = sc.unit_settings
test("Unités : système métrique, échelle 1", us.system == "METRIC" and abs(us.scale_length - 1) < 1e-9, f"{us.system} × {us.scale_length}", "METRIC × 1")
roots = [o for o in bpy.data.objects if o.type == "MESH" and o.parent is None and (o.location.length > 1e-6 or o.rotation_euler.to_quaternion().angle > 1e-6 or (o.scale - Vector((1, 1, 1))).length > 1e-6)]
_tr = [o for o in roots if not o.name.startswith(("Prop_", "LOD", "COL_", "Studio", "Lumiere", "Nuancier_", "Fumee"))]
test("Repère : géométrie de construction en coordonnées monde (pivots à l'origine, sans rotation ni échelle)", len(_tr) == 0,
     f"{len(_tr)} objet(s) du bâtiment transformé(s) (nuancier exclu)", "0")

# ---------------------------------------------------------------- 2. volumes
def check_box(nom, os_, x0, x1, y0, y1, tol=0.07):
    b = bbox(os_)
    ok = b is not None and abs(b[0] - x0) <= tol and abs(b[1] - x1) <= tol and abs(b[2] - y0) <= tol and abs(b[3] - y1) <= tol
    test(nom, ok, f"X {b[0]:.3f}–{b[1]:.3f}  Y {b[2]:.3f}–{b[3]:.3f}" if b else "absent", f"X {x0}–{x1}  Y {y0}–{y1} (±{tol})")
check_box("Boutique : murs du rez", objs("Boutique_Mur_"), 0.0, 9.5, 0.0, 8.5)
check_box("Mur séparatif (coque + enduit côté fournil)", objs("MurSeparatif"), 9.5, 10.0, 0.0, 8.5)
check_box("Fournil : murs", objs("Fournil_Mur_"), 10.0, 14.5, 0.0, 8.0)
_fr = objs("Etage_Facade_PansDeBois") or objs("Etage_Facade_Enduit")
_h = None
if _fr:
    _best = None
    for _ob in _fr:
        _hit, _loc, _n, _i = _ob.ray_cast(Vector((4.75, -2.0, 4.0)), Vector((0, 1, 0)))
        if _hit and (_best is None or _loc.y < _best): _best = _loc.y
    _h = _best
test("Étage : encorbellement de 18 cm en façade (nu du poteau X 4,75 mesuré par rayon)", _h is not None and abs(_h - (-P.ENC)) < 0.01, f"Y {_h:.3f}" if _h is not None else "absent", "-0,180")
ground = [o for o in objs() if any(c.name in ("Architecture_Boutique", "Architecture_Fournil", "Four") for c in o.users_collection)]
bb = bbox(ground)
test("Emprise au sol des volumes ≤ 14,50 × 11,00 (tolérés : abouts de l'encorbellement en Y < 0, appuis et bossages ≤ 5 cm)",
     bb and bb[0] >= -0.05 and bb[1] <= 14.55 and bb[2] >= -0.25 and bb[3] <= 11.02,
     f"X {bb[0]:.3f}–{bb[1]:.3f}  Y {bb[2]:.3f}–{bb[3]:.3f}" if bb else "absent", "X 0–14,50  Y 0–11,00")
sep = objs("MurSeparatif")
if sep:
    bs = bbox(sep); test("Mur séparatif : épaisseur 0,50", abs((bs[1] - bs[0]) - 0.5) < 0.005, f"{bs[1]-bs[0]:.3f}", "0,500")

# ---------------------------------------------------------------- outils de lancer de rayons
dg = bpy.context.evaluated_depsgraph_get()
def ray_objs(os_, o, d, maxd=50):
    best = None
    for ob in os_:
        hit, loc, nrm, idx = ob.ray_cast(Vector(o), Vector(d).normalized(), distance=maxd, depsgraph=dg)
        if hit:
            dist = (loc - Vector(o)).length
            if best is None or dist < best[0]: best = (dist, ob.name, loc)
    return best
def ray_scene(o, d, maxd=50):
    hit, loc, nrm, idx, ob, mw = sc.ray_cast(dg, Vector(o), Vector(d).normalized(), distance=maxd)
    return ((loc - Vector(o)).length, ob.name, loc) if hit else None

WALLS = objs("Boutique_Mur_") + objs("Fournil_Mur_") + objs("MurSeparatif") + objs("Etage_", None) + objs("PignonGauche_Enduit") + objs("Lucarne_Facade")
WALLS = objs("Boutique_Entrevous") + [o for o in WALLS if o.name.endswith(("Enduit", "Facade", "Gauche", "Arriere", "Droit", "MurSeparatif", "EnduitFournil")) or o.name.startswith(("Boutique_Mur_", "Fournil_Mur_"))]

# ---------------------------------------------------------------- 3. porte principale à X = 7,25
fac = objs("Boutique_Mur_Facade")
if fac:
    edges = []; prev = None
    x = 6.0
    while x < 8.6:
        h = ray_objs(fac, (x, -1.0, 1.2), (0, 1, 0), 3.0) is not None
        if prev is not None and h != prev: edges.append(round(x, 3))
        prev = h; x += 0.0025
    if len(edges) >= 2:
        c = (edges[0] + edges[-1])/2; w = edges[-1] - edges[0]
        test("Porte principale centrée à X = 7,25 (mesure par rayons sur le mur)", abs(c - 7.25) <= 0.01, f"bords {edges[0]:.3f} / {edges[-1]:.3f}, centre {c:.3f}, largeur {w:.3f}", "7,250 ± 0,01 ; largeur ≈ 1,20")
    else:
        test("Porte principale centrée à X = 7,25", False, f"bords trouvés {edges}", "2 bords")
    zs = [z/100 for z in range(5, 300)]
    top = 0.0
    for z in zs[10:]:
        if ray_objs(fac + objs("Boutique_Entrevous") + objs("Boutique_Moellons") + objs("Boutique_PierresDeTaille"), (7.25, -1.0, z), (0, 1, 0), 3.0) is not None: break
        top = z
    test("Porte principale : hauteur libre ≥ 2,10 au-dessus du sol fini", top - P.SOL >= 2.09, f"passage jusqu'à Z = {top:.2f} (clé de l'arc ; sol fini {P.SOL})", "≥ 2,10")

# ---------------------------------------------------------------- 4. hauteurs et pentes
fr = objs("Etage_Facade_PansDeBois") or objs("Etage_Facade_Enduit")
if fr:
    b = bbox(fr); test("Étage : sablière haute à 5,85 (rez 3,05 + étage 2,80)", abs(b[5] - P.Z_SABL) < 0.01, f"{b[5]:.3f}", "5,850")
sv = objs("Boutique_Solives") or objs("Boutique_Entrevous")
if sv:
    b = bbox(sv); test("Rez : dessus des solives / plancher de l'étage à 3,05", abs(b[5] - P.Z_ETAGE) < 0.01, f"{b[5]:.3f}", "3,050")
def roof_z(x, y, names):
    os_ = [o for o in objs() if o.name.startswith(names)]
    h = ray_objs(os_, (x, y, 30.0), (0, 0, -1), 40)
    return 30.0 - h[0] if h else None
for nom, names, x, ya, yb, attendu in (("boutique, pan avant", ("Toit_Boutique_Voliges",), 8.0, 1.0, 3.0, P.PITCH_B),
                                        ("boutique, pan arrière", ("Toit_Boutique_Voliges",), 2.0, 7.0, 5.0, P.PITCH_B),
                                        ("fournil, pan avant", ("Toit_Fournil_Voliges",), 13.5, 1.0, 3.0, P.PITCH_F),
                                        ("fournil, pan arrière", ("Toit_Fournil_Voliges",), 13.5, 7.0, 5.0, P.PITCH_F)):
    za, zb = roof_z(x, ya, names), roof_z(x, yb, names)
    if za is None or zb is None: test(f"Pente du toit {nom}", False, "toit introuvable", f"{attendu}°"); continue
    pitch = math.degrees(math.atan2(abs(zb - za), abs(yb - ya)))
    test(f"Pente du toit {nom}", abs(pitch - attendu) < 0.3, f"{pitch:.2f}°", f"{attendu}° ± 0,3")
def ridge_from(names, x, yf, yb, T, pitch):
    a1, a2 = roof_z(x, yf[0], names), roof_z(x, yf[1], names); b1_, b2 = roof_z(x, yb[0], names), roof_z(x, yb[1], names)
    if None in (a1, a2, b1_, b2): return None
    sa = (a2 - a1)/(yf[1] - yf[0]); sb = (b2 - b1_)/(yb[1] - yb[0])
    y = (b1_ - sb*yb[0] - a1 + sa*yf[0])/(sa - sb)
    return a1 + sa*(y - yf[0]) - T/math.cos(math.radians(pitch)), y
rr = ridge_from(("Toit_Boutique_Voliges",), 8.0, (2.5, 3.5), (5.5, 6.5), 0.08, 48)
if rr:
    test("Faîtage géométrique de la boutique (intersection des plans mesurés, sous les voliges)", abs(rr[0] - P.RIDGE_B_Z) < 0.02,
         f"Z {rr[0]:.3f} à Y {rr[1]:.3f} (consigne ≈ 10,57 ; écart {rr[0]-10.57:+.3f}, dû à l'encorbellement)", f"{P.RIDGE_B_Z:.3f}")
top_all = bbox([o for o in objs() if any(c.name in ("Toitures", "Four") for c in o.users_collection)])
if top_all: test("Point le plus haut (faîtières, cheminée)", top_all[5] < 11.3, f"{top_all[5]:.3f}", "< 11,30")
rf = ridge_from(("Toit_Fournil_Voliges",), 13.5, (1.5, 2.5), (5.5, 6.5), 0.08, 30)
if rf:
    test("Faîtage géométrique du fournil", abs(rf[0] - P.RIDGE_F_Z) < 0.02, f"Z {rf[0]:.3f} à Y {rf[1]:.3f}", f"{P.RIDGE_F_Z:.3f}")

# ---------------------------------------------------------------- 5. une seule lucarne
luc = [o for o in bpy.data.objects if o.get("lucarne_id")]
test("Une seule lucarne (objets étiquetés)", len({o["lucarne_id"] for o in luc}) == 1, f"{len({o['lucarne_id'] for o in luc})} lucarne(s) : {sorted({o['lucarne_id'] for o in luc})}", "1")
roofs_b = [o for o in objs() if o.name.startswith(("Toit_Boutique_", "Lucarne_"))]
bumps = []; inb = False; y_scan = 1.0
z_plane = P.Z_SABL + (y_scan - P.ROOF_B_Y0)*P.T48
x = -0.3
while x < 10.2:
    h = ray_objs(roofs_b, (x, y_scan, 30), (0, 0, -1), 40)
    up = h is not None and (30 - h[0]) > z_plane + 0.5
    if up and not inb: bumps.append([x, x])
    if up: bumps[-1][1] = x
    inb = up; x += 0.02
test("Une seule lucarne (balayage du pan avant par rayons)", len(bumps) == 1 and abs((bumps[0][0] + bumps[0][1])/2 - P.LUC["xc"]) < 0.08,
     f"{len(bumps)} saillie(s) : " + ", ".join(f"X {a:.2f}–{b_:.2f}" for a, b_ in bumps), f"1, centrée à X = {P.LUC['xc']}")

# ---------------------------------------------------------------- 6. ouvertures : cohérence plan / modèle / vues
def wall_ray(o):
    """Rayon perpendiculaire au mur, du dehors vers le dedans, au centre de l'ouverture."""
    a = (o["a0"] + o["a1"])/2; z = (o["z0"] + min(o["z1"], o["z0"] + 2.0))/2
    m = o["mur"]
    if m in ("av_b", "av_f"): return (a, -1.0, z), (0, 1, 0), 1.6
    if m == "etage_av": return (a, -1.0, z), (0, 1, 0), 1.3
    if m in ("ar_b", "etage_ar"): return (a, 9.5, z), (0, -1, 0), 1.6
    if m == "ar_f": return (a, 7.0, z), (0, 1, 0), 1.2
    if m in ("g_b", "etage_g"): return (-1.0, a, z), (1, 0, 0), 1.6
    if m == "d_f": return (15.5, a, z), (-1, 0, 0), 1.6
    if m == "sep": return (9.0, a, z), (1, 0, 0), 1.4
blockers = [o for o in objs() if any(c.name in ("Architecture_Boutique", "Architecture_Fournil") for c in o.users_collection)
            and not o.name.startswith(("Boutique_Sol", "Fournil_Sol", "Etage_Plancher", "Boutique_Escalier"))]
for o in P.OUV:
    org, d, L = wall_ray(o)
    h = ray_objs(blockers, org, d, L)
    test(f"Ouverture {o['id']} ({o['nom']}) percée dans le modèle", h is None, "libre" if h is None else f"bloquée par {h[1]} à {h[0]:.2f} m", "libre")
lw = P.LUC
h = ray_objs([o for o in objs() if o.name.startswith(("Lucarne_Facade", "Toit_Boutique_Voliges"))], ((lw["win"][0] + lw["win"][1])/2, -1.0, sum(lw["z_win"])/2), (0, 1, 0), 2.0)
test("Ouverture L1 (fenêtre de lucarne) percée", h is None, "libre" if h is None else f"bloquée par {h[1]}", "libre")
# ouvertures imprévues : balayage des façades extérieures, chaque trouée doit appartenir au plan
def scan(nom, mk, a_rng, z_rng, mur_ids, os_):
    stray = []
    a = a_rng[0]
    while a <= a_rng[1]:
        z = z_rng[0]
        while z <= z_rng[1]:
            org, d, L = mk(a, z)
            if ray_objs(os_, org, d, L) is None:
                ok = any(o["a0"] - 0.03 <= a <= o["a1"] + 0.03 and o["z0"] - 0.03 <= z <= max(o["z1"], o.get("z_cle", 0)) + 0.03
                         for o in P.OUV if o["mur"] in mur_ids)
                if not ok: stray.append((round(a, 2), round(z, 2)))
            z += 0.2
        a += 0.2
    test(f"Vue {nom} : aucune ouverture hors plan", len(stray) == 0, f"{len(stray)} point(s) traversant hors plan" + (f", ex. {stray[:4]}" if stray else ""), "0")
walls_scan = [o for o in WALLS]
scan("façade (rez boutique et fournil)", lambda a, z: ((a, -1.0, z), (0, 1, 0), 1.6), (0.7, 14.2), (0.3, 2.7), ("av_b", "av_f"), walls_scan)
scan("façade (étage)", lambda a, z: ((a, -1.0, z), (0, 1, 0), 1.3), (0.4, 9.2), (3.4, 5.6), ("etage_av",), walls_scan)
scan("côté gauche", lambda a, z: ((-1.0, a, z), (1, 0, 0), 1.6), (0.7, 7.8), (0.3, 5.6), ("g_b", "etage_g"), walls_scan)
scan("côté droit", lambda a, z: ((15.5, a, z), (-1, 0, 0), 1.6), (0.7, 7.3), (0.3, 3.0), ("d_f",), walls_scan)
scan("arrière (boutique)", lambda a, z: ((a, 9.5, z), (0, -1, 0), 1.6), (0.7, 9.2), (0.3, 5.6), ("ar_b", "etage_ar"), walls_scan)

# ---------------------------------------------------------------- 7. cul-de-four
four = [o for o in objs() if o.name.startswith(("Four_Dome", "Four_Massif"))]
if four:
    b = bbox(four)
    test("Cul-de-four : dôme et massif dans X 10,40–13,90, Y 8,00–11,00", b[0] >= 10.38 and b[1] <= 13.92 and b[2] >= 7.99 and b[3] <= 11.02,
         f"X {b[0]:.3f}–{b[1]:.3f}  Y {b[2]:.3f}–{b[3]:.3f}  Z {b[4]:.3f}–{b[5]:.3f}", "dans l'emprise")
    test("Cul-de-four : pas de géométrie enfouie sous le terrain", b[4] >= -0.03, f"Z min {b[4]:.3f}", "≥ -0,03")
    test("Un seul four (pas de duplication)", len([o for o in bpy.data.objects if o.name.startswith("Four_Dome")]) == 1,
         f"{len([o for o in bpy.data.objects if o.name.startswith('Four_Dome')])} dôme(s)", "1")
else:
    test("Cul-de-four présent", False, "pas encore construit", "Four_Dome, Four_Massif")

# ---------------------------------------------------------------- 8. cheminement intérieur et ligne de vue
vis = [o for o in objs() if o.visible_get() and not o.name.startswith(("Studio", "LOD", "COL_"))]
NONINTER = [o for o in vis if o.get("bf_role") != "interactif"]
def clear(a, b_, os_=None):
    d = Vector(b_) - Vector(a)
    h = ray_scene(a, d, d.length) if os_ is None else ray_objs(os_, a, d, d.length)
    return h
h = clear(P.OEIL, P.BOUCHE)
hit_mouth = h is None or h[1].startswith(("Four_Chambre", "Four_Braises", "Four_Sole"))
dist = (Vector(P.BOUCHE) - Vector(P.OEIL)).length
test("Ligne de vue : depuis l'entrée (œil 1,70) jusqu'à la bouche du four, sans obstacle", hit_mouth or (h and h[0] > dist - 0.05),
     ("dégagée" if h is None else f"premier objet touché : {h[1]} à {h[0]:.2f} m / {dist:.2f}"), "bouche visible")
n_ok = 0; n_tot = 0
for dx in (-0.25, 0.0, 0.25):
    for dz in (-0.12, 0.0, 0.12):
        tgt = (P.BOUCHE[0] + dx, P.BOUCHE[1], P.BOUCHE[2] + dz)
        h = clear(P.OEIL, tgt); n_tot += 1
        d_ = (Vector(tgt) - Vector(P.OEIL)).length
        if h is None or h[0] > d_ - 0.06 or h[1].startswith(("Four_Chambre", "Four_Braises", "Four_Sole")): n_ok += 1
test("Ligne de vue : part visible de la bouche (9 rayons sur la gueule)", n_ok >= 6, f"{n_ok}/{n_tot} rayons dégagés", "≥ 6/9")
# tour 3 : la caméra 07 a été avancée dans la boutique, dans l'axe de la baie ; même contrôle depuis son œil
import bf_views as _BV
_c7 = _BV.VIEWS["07_interieur_boutique"][1]
n_ok7 = 0
for dx in (-0.25, 0.0, 0.25):
    for dz in (-0.12, 0.0, 0.12):
        tgt = (P.BOUCHE[0] + dx, P.BOUCHE[1], P.BOUCHE[2] + dz)
        h = clear(_c7, tgt); d_ = (Vector(tgt) - Vector(_c7)).length
        if h is None or h[0] > d_ - 0.06 or h[1].startswith(("Four_Chambre", "Four_Braises", "Four_Sole")): n_ok7 += 1
_in_b = 0.5 < _c7[0] < P.SEPARATIF["x0"] and 0.5 < _c7[1] < 7.5 and abs(_c7[2] - (P.SOL + 1.60)) < 0.05
test("Caméra 07 : dans la boutique, à hauteur d'œil, bouche du four visible par la baie (9 rayons)", _in_b and n_ok7 >= 6,
     f"caméra {r3(_c7)} ; {n_ok7}/9 rayons dégagés", "boutique, Z 1,70, ≥ 6/9")
for nom, a, b_ in (("rue → boutique par la porte P1", (7.25, -1.5, 1.0), (7.25, 2.5, 1.0)),
                   ("boutique → fournil par la baie B1", (8.6, 4.5, 1.0), (11.0, 4.5, 1.0)),
                   ("boutique → fournil, à 1,90 m", (8.6, 4.5, 1.9), (11.0, 4.5, 1.9)),
                   ("boutique → cour par la porte R2", (8.5, 7.0, 1.0), (8.5, 9.5, 1.0))):
    h = clear(a, b_, NONINTER)
    test(f"Cheminement {nom} (portes, objets interactifs, ignorées)", h is None, "libre" if h is None else f"bloqué par {h[1]} à {h[0]:.2f} m", "libre")
b1 = P.ouv("B1")
test("Baie B1 : passage ≥ 0,90 × 2,00", (b1["a1"] - b1["a0"]) >= 0.9 and (b1["z1"] - b1["z0"]) >= 2.0, f"{b1['a1']-b1['a0']:.2f} × {b1['z1']-b1['z0']:.2f}", "≥ 0,90 × 2,00")
st = objs("Boutique_Escalier")
if st:
    b = bbox(st); test("Escalier : arrive au niveau de l'étage", abs(b[5] - (P.Z_ETAGE + 0.03)) < 0.08 or b[5] > P.Z_ETAGE, f"Z max {b[5]:.3f}", f"≈ {P.Z_ETAGE + 0.03:.2f}")

# ---------------------------------------------------------------- 9. géométrie aberrante
bad = []; buried = []; big = []; nan = 0; degenerate = 0
for o in objs():
    if o.name.startswith(("Studio", "Fumee")): continue
    me = o.data
    for v in me.vertices:
        if not all(math.isfinite(c) for c in v.co): nan += 1
    for p in me.polygons:
        if p.area < 1e-9: degenerate += 1
    b = bbox([o])
    if b[4] < -0.06 and not o.name.startswith(("LOD", "COL_")): buried.append(f"{o.name} ({b[4]:.2f})")
    if max(b[1] - b[0], b[3] - b[2], b[5] - b[4]) > 30: big.append(o.name)
test("Aucune coordonnée non finie", nan == 0, str(nan), "0")
test("Faces dégénérées (aire nulle)", degenerate < 50, str(degenerate), "< 50")
test("Pas de géométrie enfouie sous le terrain (< -0,06 m)", not buried, ", ".join(buried[:6]) or "aucune", "aucune")
test("Pas d'objet démesuré (> 30 m)", not big, ", ".join(big) or "aucun", "aucun")
empty = [o.name for o in objs() if len(o.data.polygons) == 0]
test("Pas d'objet maillage vide", not empty, ", ".join(empty[:6]) or "aucun", "aucun")

# ---------------------------------------------------------------- 10. collections et nomenclature
need = ["Architecture_Boutique", "Architecture_Fournil", "Toitures", "Menuiseries", "Four", "Props_Exterieurs", "Props_Interieurs",
        "Materiaux", "Eclairage", "Cameras", "Collisions", "LOD"]
miss = [c for c in need if c not in bpy.data.collections]
test("Collections de la mission (§ 8) présentes", not miss, "manquantes : " + ", ".join(miss) if miss else "12/12", "12/12")

# ---------------------------------------------------------------- 11. rendus : même scène source
meta_p = os.path.join(ROOT, "renders", "rendus_meta.json")
VUES = ["01_facade_orthographique", "02_cote_droit_orthographique", "03_cote_gauche_orthographique", "04_arriere_orthographique",
        "05_vue_trois_quarts", "06_vue_de_dessus_toiture", "07_interieur_boutique"]
if os.path.exists(meta_p):
    meta = json.load(open(meta_p))
    sigs = {meta[v]["signature"] for v in VUES if v in meta}
    have = [v for v in VUES if v in meta and os.path.exists(os.path.join(ROOT, "renders", v + ".png"))]
    test("Rendus : les 7 vues existent", len(have) == 7, f"{len(have)}/7", "7/7")
    test("Rendus : une seule scène source (même fichier, même signature géométrique)", len(sigs) == 1 and len({meta[v]['blend'] for v in have}) == 1,
         f"{len(sigs)} signature(s) ; fichier {set(meta[v]['blend'] for v in have)}", "1")
    from_sig = None
    try:
        import bf_views; from_sig = bf_views.signature()
    except Exception as e: from_sig = None
    if from_sig: test("Rendus : signature identique à la scène vérifiée", sigs == {from_sig}, f"{from_sig}", "identique")
    small = [v for v in have if max(meta[v]["resolution"]) < 2048]
    test("Rendus : ≥ 2048 px sur le plus grand côté", not small, ", ".join(small) or "tous ≥ 2048", "tous")
else:
    test("Rendus présents", False, "renders/rendus_meta.json absent", "7 vues")

# ---------------------------------------------------------------- 12. exports et échelles
exp = os.path.join(ROOT, "export")
meta_e = os.path.join(exp, "export_meta.json")
if os.path.exists(meta_e):
    em = json.load(open(meta_e))
    for f, info in em.get("fichiers", {}).items():
        p = os.path.join(exp, f)
        if not os.path.exists(p): test(f"Export {f} présent", False, "absent", "présent"); continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if f.endswith(".glb"): bpy.ops.import_scene.gltf(filepath=p)
        elif f.endswith(".fbx"): bpy.ops.import_scene.fbx(filepath=p)
        ms = [o for o in bpy.data.objects if o.type == "MESH"]
        sel = [o for o in ms if info.get("mesure") is None or any(o.name.startswith(k) for k in info["mesure"])]
        b = bbox(sel)
        dims = (b[1] - b[0], b[3] - b[2], b[5] - b[4]) if b else (0, 0, 0)
        exp_d = info["dims"]
        zmin_ok = b[4] > (-0.25 if "Collisions" in f else -0.07)
        ok = all(abs(dims[i] - exp_d[i]) < 0.05 for i in range(3)) and zmin_ok
        test(f"Export {f} : échelle 1 m = 1 unité, Z haut à l'import, pied au sol (assise des pierres ≤ 7 cm ; dalles de collision 0,20 m)", ok,
             f"dimensions {dims[0]:.2f} × {dims[1]:.2f} × {dims[2]:.2f}, Z min {b[4]:.3f} ; {len(ms)} maillages ; {tri_count(sel)} triangles",
             f"{exp_d[0]:.2f} × {exp_d[1]:.2f} × {exp_d[2]:.2f}")
        if f.endswith(".glb"):
            imgs = [i for i in bpy.data.images if i.size[0] > 0]
            test(f"Export {f} : textures embarquées", len(imgs) >= info.get("textures_min", 1), f"{len(imgs)} image(s)", f"≥ {info.get('textures_min', 1)}")
else:
    test("Exports présents", False, "export/export_meta.json absent", "GLB, FBX")

# ---------------------------------------------------------------- 13. second tour
if os.path.exists(meta_e):
    em = json.load(open(meta_e)); t0 = em["lod"]["LOD0_PC"]["triangles"]
    test("LOD0 allégé d'au moins 35 % (référence tour 1 : 704 429 triangles)", t0 <= 704429*0.65, f"{t0} triangles ({(t0/704429 - 1)*100:+.1f} %)", "≤ 457 879")
open_blend(BLEND)
fu = [o for o in bpy.data.objects if o.name.startswith("Fumee")]
test("Fumée de la cheminée présente, dans la collection d'effets (non exportée)", len(fu) == 1 and fu[0].users_collection[0].name == "Effets_Rendu",
     f"{len(fu)} objet(s), collection {fu[0].users_collection[0].name if fu else '-'}", "1, Effets_Rendu")
nq = bpy.data.objects.get("Lucarne_Noues")
test("Lucarne : noues de raccord présentes (2 bandes)", nq is not None and len(nq.data.polygons) >= 12, f"{len(nq.data.polygons) if nq else 0} faces", "≥ 12")

# ---------------------------------------------------------------- 14. troisième tour : pains et sacs
from mathutils.bvhtree import BVHTree
pains = [o for o in objs() if "est_pain" in o.data.attributes]
nf = ng = 0; tp = 0
dg_ = bpy.context.evaluated_depsgraph_get()
for o in pains:
    me = o.data; ep = me.attributes["est_pain"].data
    nf += sum(1 for d in ep if d.value)
    if "grigne" in me.attributes: ng += sum(1 for d in me.attributes["grigne"].data if d.value > 0.5)
    ev = o.evaluated_get(dg_); m2 = ev.to_mesh(); m2.calc_loop_triangles()
    if "est_pain" in m2.attributes:
        a2 = m2.attributes["est_pain"].data; tp += sum(1 for t in m2.loop_triangles if a2[t.polygon_index].value)
    ev.to_mesh_clear()
test("Pains en vraie géométrie : grignes creusées (sommets de grigne) et budget de triangles tenu",
     len(pains) >= 8 and ng >= 1000 and tp <= 113346, f"{len(pains)} objets, {nf} faces de pain, {ng} sommets de grigne, {tp} triangles de pain (tour 2 : 113 346)", "≥ 1000 sommets ; ≤ 113 346 triangles")
sacs = [o for o in objs() if "SacsFarine" in o.name or o.name == "Prop_Sacs_Farine"]
worst = 0.0; n_ink = 0; ok_f = True
for o in sacs:
    me = o.data; names = [m.name if m else "" for m in me.materials]
    if "MAT_Ficelle" not in names or "MAT_EncreSac" not in names: ok_f = False; continue
    it, ie = names.index("MAT_ToileSac"), names.index("MAT_EncreSac")
    tv = [p for p in me.polygons if p.material_index == it]
    bvh = BVHTree.FromPolygons([v.co for v in me.vertices], [list(p.vertices) for p in tv])
    iv = {i for p in me.polygons if p.material_index == ie for i in p.vertices}
    n_ink += len(iv)
    for i in iv:
        loc, nrm, idx, d = bvh.find_nearest(me.vertices[i].co)
        if d is not None: worst = max(worst, d)
test("Sacs « FARINE » : ficelle au col et lettres plaquées sur la toile (≤ 6 mm de la surface)", len(sacs) == 3 and ok_f and n_ink > 0 and worst <= 0.006,
     f"{len(sacs)} lots de sacs, {n_ink} sommets d'encre, écart max {worst*1000:.1f} mm", "3 lots ; ≤ 6 mm")

n_ok = sum(r["ok"] for r in R)
print(f"\nBILAN {n_ok}/{len(R)} tests réussis ({os.path.basename(BLEND)})")
json.dump(dict(fichier=os.path.basename(BLEND), date=time.strftime("%Y-%m-%d %H:%M"), reussis=n_ok, total=len(R), tests=R),
          open(os.path.join(ROOT, "verification.json" if not args else "verification_" + os.path.basename(BLEND).replace(".blend", "") + ".json"), "w"),
          ensure_ascii=False, indent=1)
