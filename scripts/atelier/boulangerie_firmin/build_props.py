# Étape C — accessoires extérieurs (objets indépendants, désactivables) : enseigne « Boulangerie » et miche dorée,
# lanterne, ardoise « Pain du jour », auvent rayé, étal, pains, corbeilles, sacs « Farine », tonneaux et caisses,
# jardinières (géraniums, lavande), lierre, banc et torchon rayé, pelle, bois de chauffage.
#   /tmp/bvenv/bin/python -u -I scripts/build_props.py   (lit work/B_interieur.blend, écrit work/C_props.blend)
import sys, os, math, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix
import bf_plan as P
from bf_lib import *
from bf_props import *

T0 = time.time()
open_blend(os.path.join(WORK, "B_interieur.blend"))
seed(33)
FONT = load_font()
FONT_CRAIE = None
for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSerifCondensed-Italic.ttf",):
    if os.path.exists(p): FONT_CRAIE = bpy.data.fonts.load(p, check_existing=True)
CE = "Props_Exterieurs"
def tag(o, role="decoratif", **kw):
    o["bf_role"] = role; o["bf_desactivable"] = True
    for k, v in kw.items(): o[k] = v
    return o

# ---------------------------------------------------------------- enseigne en potence
E = P.ENSEIGNE; x, za = E["x"], E["z_bras"]; y0 = -P.ENC
en = Pack("Prop_Enseigne_Boulangerie", ["fer", "peinture_rouge", "or"], CE)
en.box(x - 0.05, x + 0.05, y0 - 0.02, y0, za - 0.62, za + 0.08, m="fer")
en.tube([(x, y0, za), (x, y0 - E["saillie"], za), (x, y0 - E["saillie"] - 0.06, za + 0.05)], 0.018, "fer", nv=6)
en.tube([(x, y0, za - 0.55), (x, y0 - 0.35, za - 0.22), (x, y0 - 0.70, za - 0.02)], 0.014, "fer", nv=6)
for (cy, cz, r, a0, a1) in ((y0 - 0.30, za + 0.12, 0.11, -1.6, 3.6), (y0 - 0.62, za + 0.09, 0.08, -1.6, 3.4), (y0 - 0.25, za - 0.30, 0.09, 0.0, 5.0)):
    pts = []
    for i in range(16):
        t = a0 + (a1 - a0)*i/15; rr = r*(1 - 0.45*i/15)
        pts.append((x, cy + rr*math.cos(t), cz + rr*math.sin(t)))
    en.tube(pts, 0.009, "fer", nv=5)
pw0, pw1, pz0, pz1 = y0 - 1.12, y0 - 0.30, za - 0.80, za - 0.20
for yy in (pw0 + 0.08, pw1 - 0.08):
    en.tube([(x, yy, za - 0.01), (x, yy, pz1 + 0.04)], 0.007, "fer", nv=5)
    en.tube([(x, yy + 0.03*math.cos(2*math.pi*i/8), pz1 + 0.04 + 0.03*math.sin(2*math.pi*i/8)) for i in range(8)], 0.005, "fer", nv=4, closed=True)
en.box(x - 0.022, x + 0.022, pw0, pw1, pz0, pz1, m="peinture_rouge")
for (a, b, c, d) in ((pw0 - 0.02, pw1 + 0.02, pz1 - 0.025, pz1 + 0.01), (pw0 - 0.02, pw1 + 0.02, pz0 - 0.01, pz0 + 0.025),
                     (pw0 - 0.02, pw0 + 0.015, pz0, pz1), (pw1 - 0.015, pw1 + 0.02, pz0, pz1)):
    en.box(x - 0.03, x + 0.03, a, b, c, d, m="or")
tm = text_mesh("Boulangerie", size=0.125, extrude=0.003, font=FONT)
ymid = (pw0 + pw1)/2; zt = pz0 + 0.17
en.mesh(tm, frame_m((x + 0.025, ymid, zt), (0, 1, 0), (0, 0, 1), (1, 0, 0)), "or")
en.mesh(tm, frame_m((x - 0.025, ymid, zt), (0, -1, 0), (0, 0, 1), (-1, 0, 0)), "or")
bpy.data.meshes.remove(tm)
for s in (-1, 1):                                       # miche dorée en relief, sur chaque face
    en.sphere((x + s*0.022, ymid, pz1 - 0.17), (0.035, 0.15, 0.085), "or", 20, 12)
    for k in range(3):
        en.sphere((x + s*0.05, ymid + (k - 1)*0.07, pz1 - 0.155), (0.006, 0.013, 0.05), "peinture_rouge", 6, 4, 0.0, (0.6, 0, 0))
tag(en.done(bev=0.003), texte="Boulangerie")

# ---------------------------------------------------------------- lanterne de l'entrée
L = P.LANTERNE
la = Pack("Prop_Lanterne_Entree", ["fer", "verre", "cire", "flamme"], CE)
la.box(L["x"] - 0.06, L["x"] + 0.06, -0.02, 0.0, L["z"] - 0.12, L["z"] + 0.12, "fer")
la.tube([(L["x"], 0.0, L["z"]), (L["x"], -0.38, L["z"]), (L["x"], -0.40, L["z"] - 0.05)], 0.012, "fer", nv=6)
la.tube([(L["x"], 0.0, L["z"] - 0.25), (L["x"], -0.18, L["z"] - 0.08), (L["x"], -0.30, L["z"])], 0.009, "fer", nv=5)
lanterne(la, L["x"], -0.40, L["z"] - 0.30, 1.0)
tag(la.done(), "interactif")
point_light("Lumiere_Lanterne_Entree", (L["x"], -0.40, L["z"] - 0.36), 18, (1.0, 0.6, 0.3), 0.04)

# ---------------------------------------------------------------- ardoise « Pain du jour »
ar = Pack("Prop_Ardoise_PainDuJour", ["chene", "ardoise", "craie"], CE)
ax_, ay_, ang = 8.55, -0.95, 0.15
R = Matrix.Translation((ax_, ay_, 0)) @ Matrix.Rotation(ang, 4, "Z")
tilt = math.radians(12)
for s in (-1, 1):
    F = R @ Matrix.Translation((0, s*0.18, 0)) @ Matrix.Rotation(s*tilt, 4, "X")
    for xx in (-0.30, 0.30):
        ar.cube(F @ Matrix.Translation((xx, 0, 0.52)) @ Matrix.Diagonal((0.04, 0.03, 1.04, 1)), "chene")
    ar.cube(F @ Matrix.Translation((0, 0, 0.98)) @ Matrix.Diagonal((0.64, 0.03, 0.04, 1)), "chene")
    ar.cube(F @ Matrix.Translation((0, 0, 0.30)) @ Matrix.Diagonal((0.64, 0.03, 0.04, 1)), "chene")
    ar.cube(F @ Matrix.Translation((0, s*0.003, 0.64)) @ Matrix.Diagonal((0.56, 0.012, 0.64, 1)), "ardoise")
F = R @ Matrix.Translation((0, -0.18, 0)) @ Matrix.Rotation(-tilt, 4, "X")
tm = text_mesh("Pain\ndu jour", size=0.115, extrude=0.0015, font=FONT_CRAIE or FONT)
ar.mesh(tm, F @ Matrix.Translation((0, -0.012, 0.78)) @ Matrix.Rotation(math.pi/2, 4, "X"), "craie"); bpy.data.meshes.remove(tm)
pts = [(0.12*math.cos(2*math.pi*i/20), 0.0, 0.055*math.sin(2*math.pi*i/20)) for i in range(20)]
ar.tube([F @ Vector((p[0], -0.013, 0.50 + p[2])) for p in pts], 0.005, "craie", nv=4, closed=True)
for k in (-1, 0, 1):
    ar.tube([F @ Vector((k*0.06 - 0.025, -0.013, 0.49)), F @ Vector((k*0.06 + 0.025, -0.013, 0.515))], 0.004, "craie", nv=4)
tag(ar.done(bev=0.003), texte="Pain du jour")

# ---------------------------------------------------------------- auvent en toile rayée crème et rouge brique délavé
v1 = P.ouv("V1")
ax0, ax1 = v1["a0"] - 0.25, v1["a1"] + 0.25
yb, zb, yf, zf = -0.03, 2.80, -1.10, 2.32
au = Pack("Prop_Auvent", ["toile_creme", "toile_rouge", "fer", "chene"], CE)
nstr = 20; sw = (ax1 - ax0)/nstr; nseg = 8
for i in range(nstr):
    m = i % 2
    xa, xb_ = ax0 + i*sw, ax0 + (i + 1)*sw
    for k in range(nseg):
        t0, t1 = k/nseg, (k + 1)/nseg
        def P_(t): return Vector((0, yb + (yf - yb)*t, zb + (zf - zb)*t - 0.035*math.sin(math.pi*t)))
        a, b = P_(t0), P_(t1); c = (a + b)/2; d = b - a
        nrm = Vector((0, -d.z, d.y)).normalized()
        au.cube(frame_m(Vector(((xa + xb_)/2, 0, 0)) + c, Vector((xb_ - xa + 0.001, 0, 0)), d, nrm*0.006), m)
    poly = [(xa, zf + 0.005), (xb_, zf + 0.005), (xb_, zf - 0.12)]
    for j in range(9):
        t = j/8; poly.append(((xa + xb_)/2 + (sw/2)*math.cos(t*math.pi), zf - 0.12 - (sw/2)*math.sin(t*math.pi)*0.8))
    au.prism([Vector((px, yf - 0.004, pz)) for px, pz in poly], Vector((0, 0.006, 0)), m)
au.box(ax0 - 0.04, ax1 + 0.04, -0.06, 0.0, zb - 0.05, zb + 0.06, "chene")
au.tube([(ax0 - 0.02, yf + 0.01, zf + 0.01), (ax1 + 0.02, yf + 0.01, zf + 0.01)], 0.012, "fer", nv=6)
for xs in (ax0 - 0.01, (ax0 + ax1)/2, ax1 + 0.01):
    au.tube([(xs, 0.0, 2.02), (xs, -0.55, 2.20), (xs, yf + 0.02, zf - 0.005)], 0.010, "fer", nv=6)
    au.box(xs - 0.03, xs + 0.03, -0.015, 0.0, 1.97, 2.07, "fer")
tag(au.done())

# ---------------------------------------------------------------- étal extérieur et pains
et = Pack("Prop_Etal_Pains", ["planche", "chene", "toile_creme", "pain", "farine", "seigle", "brioche", "osier", "lin_raye"], CE)
ex0, ex1, ey0, ey1, ez = v1["a0"], v1["a1"], -0.70, -0.10, 0.80
for k in range(4):
    yy = ey0 + k*(ey1 - ey0)/4
    et.box(ex0, ex1, yy + 0.004, yy + (ey1 - ey0)/4 - 0.004, ez - 0.04, ez, "planche")
et.box(ex0, ex1, ey0 - 0.03, ey0, ez - 0.14, ez, "planche")
for xx in (ex0 + 0.15, (ex0 + ex1)/2, ex1 - 0.15):
    et.beam((xx, ey0 + 0.06, 0.0), (xx, ey1 - 0.04, ez - 0.05), 0.06, 0.06, "chene")
    et.beam((xx, ey1 - 0.04, 0.0), (xx, ey0 + 0.06, ez - 0.05), 0.06, 0.06, "chene")
et.box(ex0 + 0.3, ex1 - 0.3, ey0 - 0.035, ey1, ez, ez + 0.004, "toile_creme")
et.box(ex0 + 0.3, ex1 - 0.3, ey0 - 0.039, ey0 - 0.035, ez - 0.22, ez + 0.004, "toile_creme")
corbeille(et, ex0 + 0.35, -0.40, ez, 0.22, 0.09)
miche(et, ex0 + 0.30, -0.36, ez + 0.07, 0.13); miche(et, ex0 + 0.43, -0.47, ez + 0.09, 0.11)
assortiment(et, ex0 + 0.65, ex1 - 0.75, -0.40, ez + 0.004, ("batard", "couronne", "seigle", "miche", "batard", "couronne", "seigle", "miche"))
corbeille(et, ex1 - 0.38, -0.40, ez, 0.2, 0.07)
for i in range(5): croissant(et, ex1 - 0.46 + (i % 3)*0.09, -0.48 + (i // 3)*0.12, ez + 0.06, U(-0.5, 0.5))
for i in range(3): brioche(et, ex1 - 0.30 + (i % 2)*0.1, -0.30 - (i // 2)*0.1, ez + 0.06, 0.85)
tag(et.done(bev=0.004))

# ---------------------------------------------------------------- jardinières de l'étage (géraniums, lavande) et pots
jd = Pack("Prop_Jardinieres_Etage", ["planche", "terre", "feuille", "fleur", "lavande", "fer"], CE)
for i in ("E1", "E2"):
    o = P.ouv(i); zj = o["z0"] - 0.12 - 0.02
    jd.box(o["a0"] - 0.12, o["a1"] + 0.12, -P.ENC - 0.30, -P.ENC - 0.04, zj - 0.20, zj, "planche")
    jd.box(o["a0"] - 0.10, o["a1"] + 0.10, -P.ENC - 0.28, -P.ENC - 0.06, zj - 0.03, zj - 0.01, "terre")
    for xx in (o["a0"] - 0.02, o["a1"] + 0.02):
        jd.tube([(xx, -P.ENC, zj - 0.40), (xx, -P.ENC - 0.25, zj - 0.20)], 0.010, "fer", nv=5)
    geraniums(jd, o["a0"] - 0.08, o["a1"] + 0.08, -P.ENC - 0.27, -P.ENC - 0.07, zj - 0.02, 30, lav=(i == "E2"))
    if i == "E1":
        for k in range(8):
            xx = U(o["a0"] - 0.06, o["a1"] + 0.06)
            jd.tube([(xx, -P.ENC - 0.30, zj - 0.02), (xx + U(-0.05, 0.05), -P.ENC - 0.36, zj - 0.15), (xx + U(-0.08, 0.08), -P.ENC - 0.37, zj - U(0.3, 0.42))], 0.005, "feuille", nv=4)
tag(jd.done(bev=0.004))
pl = Pack("Prop_Pots_Lavande", ["terrecuite", "terre", "feuille", "lavande", "fleur"], CE)
for (px, py, lav) in ((6.25, -0.38, True), (8.05, -0.35, False), (0.95, -0.95, True)):
    pl.cyl((px, py, 0.0), (px, py, 0.32), 0.15, "terrecuite", seg=18, r2=0.19, caps=False)
    pl.cyl((px, py, 0.0), (px, py, 0.02), 0.15, "terrecuite", seg=18)
    pl.tube([(px + 0.19*math.cos(2*math.pi*k/18), py + 0.19*math.sin(2*math.pi*k/18), 0.32) for k in range(18)], 0.018, "terrecuite", nv=5, closed=True)
    pl.cyl((px, py, 0.26), (px, py, 0.28), 0.18, "terre", seg=18)
    geraniums(pl, px - 0.13, px + 0.13, py - 0.13, py + 0.13, 0.28, 16, lav=lav)
tag(pl.done())

# ---------------------------------------------------------------- lierre sur l'angle avant gauche
li = Pack("Prop_Lierre_Angle", ["ecorce", "lierre"], CE)
def leaf_at(p, nrm, s):
    t = Vector((U(-1, 1), U(-1, 1), U(0.2, 1))); t = (t - nrm*t.dot(nrm)).normalized()
    b = nrm.cross(t)
    nn = (nrm + Vector((U(-0.4, 0.4), U(-0.4, 0.4), U(-0.2, 0.5)))*0.6).normalized()
    pts = [p + t*(-0.1*s), p + b*0.55*s + t*0.25*s, p + t*1.0*s + b*0.1*s, p + t*0.95*s, p - b*0.55*s + t*0.25*s]
    pts = [q + nn*0.01*(i % 2) for i, q in enumerate(pts)]
    vs = [li.bm.verts.new(q) for q in pts]
    f = li.bm.faces.new(vs); li._fin(vs, "lierre", False, 0.0, None, {})
def branch(face, a, z, hmax, depth=0):
    pts = []; rads = []
    for k in range(60):
        if z > hmax: break
        if face == "y": p = Vector((a, -0.05, z)); nrm = Vector((0, -1, 0))
        else: p = Vector((-0.05, a, z)); nrm = Vector((-1, 0, 0))
        pts.append(p); rads.append(0.012*(1 - k/70) + 0.004)
        for j in range(3):
            q = p + (Vector((U(-0.12, 0.12), 0, U(-0.05, 0.08))) if face == "y" else Vector((0, U(-0.12, 0.12), U(-0.05, 0.08)))) + nrm*U(0.0, 0.05)
            leaf_at(q, nrm, U(0.035, 0.065))
        a += U(-0.07, 0.08); a = max(0.02, a); z += U(0.06, 0.10)
        if depth < 1 and rnd.random() < 0.06: branch(face, a, z, hmax*U(0.8, 1.0), depth + 1)
    if len(pts) > 1: li.tube(pts, rads, "ecorce", nv=5)
for k in range(5): branch("y", U(0.05, 0.6), 0.0, U(1.6, 2.75))
for k in range(6): branch("x", U(0.05, 1.3), 0.0, U(2.2, 4.6))
tag(li.done())

# ---------------------------------------------------------------- banc et torchon rayé (le long du pignon gauche)
bc = Pack("Prop_Banc_Torchon", ["chene_clair", "chene", "toile_creme", "lin_raye", "osier", "pain", "farine"], CE)
bx0, bx1, by0, by1, bz = -0.58, -0.18, 3.05, 4.55, 0.46
bc.box(bx0, bx1, by0, by1, bz - 0.06, bz, "chene_clair")
for yy in (by0 + 0.12, by1 - 0.12):
    for xx in (bx0 + 0.07, bx1 - 0.07):
        bc.beam((xx + (0.04 if xx < -0.38 else -0.04), yy, 0.0), (xx, yy, bz - 0.06), 0.05, 0.05, "chene")
bc.box(bx0 + 0.05, bx1 - 0.05, by0 + 0.1, by1 - 0.1, 0.16, 0.20, "chene")
ty0, ty1 = by1 - 0.55, by1 - 0.15
for k in range(10):
    yy0 = ty0 + k*(ty1 - ty0)/10; m = "lin_raye" if k in (1, 2, 7, 8) else "toile_creme"
    bc.box(bx0 + 0.02, bx1 + 0.01, yy0, yy0 + (ty1 - ty0)/10, bz, bz + 0.005, m)
    bc.box(bx1 + 0.01, bx1 + 0.016, yy0, yy0 + (ty1 - ty0)/10, bz - 0.30, bz + 0.005, m)
corbeille(bc, (bx0 + bx1)/2, by0 + 0.35, bz, 0.15, 0.08)
miche(bc, (bx0 + bx1)/2, by0 + 0.35, bz + 0.05, 0.11)
tag(bc.done(bev=0.004), "interactif")

# ---------------------------------------------------------------- sacs « Farine », caisse, tonneaux
sk = Pack("Prop_Sacs_Farine", ["toile", "planche", "chene"], CE)
caisse(sk, 10.60, -0.42, 0.0, 0.58, 0.40, 0.34, 0.05)
sac(sk, 10.52, -0.45, 0.34, 0.1, 0.95, FONT)
sac(sk, 11.00, -0.55, 0.0, -0.25, 1.0, FONT)
sac(sk, 10.25, -0.62, 0.0, 0.3, 0.9, FONT)
tag(sk.done(), texte="Farine")
tn = Pack("Prop_Tonneaux_Caisses", ["planche", "fer", "chene", "osier", "pain", "farine", "toile_creme", "seigle"], CE)
tonneau(tn, 9.18, -0.45, 0.28, 0.85)
corbeille(tn, 9.18, -0.45, 0.85, 0.24, 0.09); miche(tn, 9.15, -0.42, 0.90, 0.13); seigle(tn, 9.24, -0.52, 0.92, 0.6)
tonneau(tn, 8.20, 9.05, 0.30, 0.88); tonneau(tn, 8.85, 9.25, 0.28, 0.82)
caisse(tn, 6.9, 9.0, 0.0, 0.6, 0.42, 0.34, 0.1); caisse(tn, 6.95, 9.02, 0.34, 0.56, 0.40, 0.32, -0.08)
caisse(tn, 13.95, 11.25 - 0.55, 0.0, 0.5, 0.36, 0.3, 0.0) if False else None
tag(tn.done(bev=0.003))

# ---------------------------------------------------------------- pelle à enfourner appuyée contre la façade du fournil
pe = Pack("Prop_Pelle_Enfourner", ["chene_clair"], CE)
pelle(pe, (13.86, -0.50, 0.02), (13.80, -0.05, 2.55), 0.34, 0.50)
tag(pe.done(bev=0.003), "interactif")

# ---------------------------------------------------------------- bois de chauffage fendu, contre le mur extérieur droit
bw = Pack("Prop_Bois_Chauffage", ["ecorce", "buche", "taille", "chene"], CE)
bw.box(14.52, 15.12, 0.55, 4.95, -0.02, 0.07, "taille")
pile_bois(bw, 0.62, 4.90, 14.82, 0.07, 10, L=0.52, axis="x")
for yy in (0.52, 4.98):
    bw.box(14.56, 14.62, yy - 0.03, yy + 0.03, 0.07, 1.55, "chene"); bw.box(15.02, 15.08, yy - 0.03, yy + 0.03, 0.07, 1.55, "chene")
tag(bw.done())
print("accessoires", round(time.time() - T0), "s", flush=True)
save_blend(os.path.join(WORK, "C_props.blend"))
print("FIN étape C", round(time.time() - T0), "s", flush=True)
