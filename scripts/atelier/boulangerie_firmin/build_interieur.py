# Étape B — four (massif, dôme, gueule, braises), charpente du fournil, intérieurs de la boutique et du fournil.
#   /tmp/bvenv/bin/python -u -I scripts/build_interieur.py   (lit work/A_archi.blend, écrit work/B_interieur.blend)
import sys, os, math, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix
import bf_plan as P
from bf_lib import *
from bf_props import *

T0 = time.time()
open_blend(os.path.join(WORK, "A_archi.blend"))
seed(22)
FONT = load_font()
def tag(o, role="decoratif", **kw):
    o["bf_role"] = role
    for k, v in kw.items(): o[k] = v
    return o

# =====================================================================
# Four
# =====================================================================
FB, (cx, cy), (rx, ry, rz_) = P.FOUR_BASE, P.FOUR_C, P.FOUR_R
ms = Pack("Four_Massif", ["mortier", "moellon", "taille"], "Four")
ms.box(FB["x0"] + 0.05, FB["x1"] - 0.05, FB["y0"], FB["y1"] - 0.05, 0.0, FB["z1"] - 0.08)
def rows_face(axis, fixed, out, a0, a1, z0, z1):
    z = z0
    while z < z1 - 0.06:
        h = min(U(0.18, 0.27), z1 - z); a = a0
        while a < a1 - 0.06:
            w = min(U(0.2, 0.45), a1 - a)
            if a1 - (a + w) < 0.15: w = a1 - a
            d = 0.16; c = fixed + out*(-d/2 - U(0.012, 0.03))
            if axis == "y": ms.box(a + 0.012, a + w - 0.012, c - d/2, c + d/2, z + 0.012, z + h - 0.012, m=1, jitter=0.01)
            else: ms.box(c - d/2, c + d/2, a + 0.012, a + w - 0.012, z + 0.012, z + h - 0.012, m=1, jitter=0.01)
            a += w
        z += h
rows_face("y", FB["y1"], 1, FB["x0"], FB["x1"], -0.02, FB["z1"] - 0.10)
rows_face("x", FB["x0"], -1, FB["y0"], FB["y1"], -0.02, FB["z1"] - 0.10)
rows_face("x", FB["x1"], 1, FB["y0"], FB["y1"], -0.02, FB["z1"] - 0.10)
ms.box(FB["x0"], FB["x1"], FB["y0"], FB["y1"], FB["z1"] - 0.10, FB["z1"], m=2)           # tablette de couronnement
tag(ms.done(bev=0.015), "statique")
# dôme : demi-ellipsoïde maçonné, noirci de suie, posé sur le massif (rien sous le terrain)
dm = Pack("Four_Dome", ["four"], "Four")
res = bmesh.ops.create_uvsphere(dm.bm, u_segments=40, v_segments=22, radius=1.0)
for v in res["verts"]:
    v.co = Vector((cx + v.co.x*rx, cy + v.co.y*ry, P.FOUR_DOME_Z0 + v.co.z*rz_)) + rv()*0.012
bmesh.ops.bisect_plane(dm.bm, geom=dm.bm.verts[:] + dm.bm.edges[:] + dm.bm.faces[:], plane_co=(0, 0, P.FOUR_DOME_Z0 - 0.02), plane_no=(0, 0, 1), clear_inner=True)
dm._fin([v for v in dm.bm.verts], 0, True, 0.0, None, {})
# collet maçonné entre le mur arrière du fournil et le dôme, percé par le tunnel de la gueule
bf = P.ouv("BF")
dm.box(11.50, bf["a0"], 8.0, 8.75, FB["z1"], 1.80); dm.box(bf["a1"], 12.80, 8.0, 8.75, FB["z1"], 1.80)
dm.box(bf["a0"], bf["a1"], 8.0, 8.75, bf["z_cle"] + 0.02, 1.80)
o = dm.done(); tag(o, "statique", four_id="F1")
ch = Pack("Four_Chambre", ["four", "cendre"], "Four")
ch.box(11.55, 12.75, 8.75, 8.95, FB["z1"], 1.75)
ch.box(bf["a0"] - 0.02, bf["a1"] + 0.02, 7.44, 8.95, FB["z1"], bf["z0"], m=1)                                       # sole
tag(ch.done(), "statique")
br = Pack("Four_Braises", ["braise", "cendre", "suie", "flamme", "flamme_coeur"], "Four")
# tour 3 : lit de braises plus dense et plus vif, bûches rougeoyantes, flammes basses (gueule mise en valeur)
for k in range(130):
    r_ = U(0.018, 0.042)
    br.sphere((U(11.88, 12.42), U(7.95, 8.80), bf["z0"] + U(0.0, 0.05)), (r_, r_*U(0.8, 1.2), r_*U(0.5, 0.8)), 0 if rnd.random() < 0.72 else 1, 7, 4, 0.006)
for k in range(4):
    p0 = (U(11.92, 12.02), U(8.15, 8.55), bf["z0"] + 0.05); p1 = (U(12.28, 12.38), U(8.35, 8.75), bf["z0"] + 0.07)
    br.cyl(p0, p1, U(0.032, 0.045), 2, seg=8)
    br.cyl((p0[0] + 0.03, p0[1], p0[2] - 0.012), (p1[0] - 0.03, p1[1], p1[2] - 0.012), U(0.02, 0.03), 0, seg=6)   # dessous incandescent
for k in range(16):                                                          # flammes basses, en langues
    fx, fy = U(11.92, 12.38), U(8.10, 8.75)
    hh = U(0.07, 0.20); ww = U(0.022, 0.045)
    br.cyl((fx, fy, bf["z0"] + 0.03), (fx + U(-0.03, 0.03), fy + U(-0.02, 0.02), bf["z0"] + 0.03 + hh), ww, "flamme", seg=7, r2=0.003)
    br.cyl((fx, fy - 0.005, bf["z0"] + 0.03), (fx, fy - 0.005, bf["z0"] + 0.03 + hh*0.55), ww*0.5, "flamme_coeur", seg=6, r2=0.002)
tag(br.done())
# voûte de la gueule : douelles de pierre noircies dans l'épaisseur du mur et du collet (lisibles dans la lueur)
vt = Pack("Four_Chambre_Voute", ["taille", "four"], "Four")
xc_, zs_, R_ = (bf["a0"] + bf["a1"])/2, bf["z1"], (bf["a1"] - bf["a0"])/2 - 0.012
y_ = 7.56; ring = 0
while y_ < 8.72:
    L_ = U(0.10, 0.14); n_ = 7; off = 0.5*(ring % 2)
    for i in range(n_ + 1):
        a0_ = math.pi*max(0.0, (i - off))/n_; a1_ = math.pi*min(float(n_), (i + 1 - off))/n_
        if a1_ - a0_ < 0.05: continue
        am = (a0_ + a1_)/2; da = (a1_ - a0_)
        c_ = Vector((xc_ + math.cos(am)*(R_ + 0.04), y_ + L_/2, zs_ + math.sin(am)*(R_ + 0.04)))
        rad = Vector((math.cos(am), 0, math.sin(am))); tng = Vector((-math.sin(am), 0, math.cos(am)))
        vt.cube(frame_m(c_, tng*(da*(R_ + 0.04) - 0.012), Vector((0, L_ - 0.012, 0)), rad*0.08), 0 if rnd.random() < 0.45 else 1, jitter=0.004)
    for sx in (-1, 1):                                                       # piédroits
        vt.box(xc_ + sx*(R_ + 0.005), xc_ + sx*(R_ + 0.07), y_ + 0.006, y_ + L_ - 0.006, bf["z0"] + 0.005, zs_ - 0.004, 0 if rnd.random() < 0.5 else 1, jitter=0.003)
    y_ += L_; ring += 1
tag(vt.done(bev=0.006), "statique")
point_light("Lumiere_Braises", (12.15, 8.45, 1.02), 150, (1.0, 0.22, 0.03), 0.15)
point_light("Lumiere_GueuleFour", (12.15, 7.80, 1.06), 10, (1.0, 0.28, 0.05), 0.10)
# lueur de la gueule portée vers le fournil et la baie : sol, pelles, montants de la baie
sl = bpy.data.lights.new("Lumiere_Lueur_Four", "SPOT"); sl.energy = 1800; sl.color = (1.0, 0.33, 0.06); sl.spot_size = math.radians(115); sl.spot_blend = 0.8; sl.shadow_soft_size = 0.20
so_ = bpy.data.objects.new("Lumiere_Lueur_Four", sl); so_.location = (12.15, 7.30, 1.02)
so_.rotation_mode = "QUATERNION"; so_.rotation_quaternion = (Vector((10.4, 4.6, 0.55)) - Vector(so_.location)).to_track_quat("-Z", "Y")
so_.visible_camera = False; so_.visible_glossy = True; coll("Eclairage").objects.link(so_)
# tour 3 bis : lueur orange douce sur le sol devant la gueule (la gueule ne l'éclaire pas : la hotte la masque)
sg = bpy.data.lights.new("Lumiere_Lueur_Sol", "SPOT"); sg.energy = 1100; sg.color = (1.0, 0.30, 0.05); sg.spot_size = math.radians(100); sg.spot_blend = 1.0; sg.shadow_soft_size = 0.25
sgo = bpy.data.objects.new("Lumiere_Lueur_Sol", sg); sgo.location = (12.15, 7.25, 0.95)
sgo.rotation_mode = "QUATERNION"; sgo.rotation_quaternion = (Vector((12.15, 5.9, 0.10)) - Vector(sgo.location)).to_track_quat("-Z", "Y")
sgo.visible_camera = False; coll("Eclairage").objects.link(sgo)
# tablette et porte de four en tôle posée contre le parement
tb = Pack("Four_Tablette", ["taille", "fer"], "Four")
tb.box(11.45, 12.85, 7.12, 7.46, bf["z0"] - 0.10, bf["z0"] - 0.02)
tb.cube(Matrix.Translation((13.62, 7.38, 0.42)) @ Matrix.Rotation(0.12, 4, "X") @ Matrix.Diagonal((0.62, 0.012, 0.50, 1)), m=1)
tb.tube([(13.45, 7.36, 0.55), (13.62, 7.32, 0.58), (13.79, 7.36, 0.55)], 0.009, 1, nv=6)
tag(tb.done(bev=0.01), "statique")
print("four", round(time.time() - T0), "s", flush=True)

# =====================================================================
# Charpente apparente du fournil (fermes à poinçon)
# =====================================================================
cf = Pack("Fournil_Fermes", ["chene"], "Architecture_Fournil")
for xf in (11.30, 12.95):
    if P.CHEM["x0"] - 0.15 < xf < P.CHEM["x1"] + 0.15: continue
    cf.box(xf - 0.09, xf + 0.09, P.WALL - 0.1, 8.0 - P.WALL + 0.1, P.F_EAVE - 0.24, P.F_EAVE)
    cf.box(xf - 0.07, xf + 0.07, 3.93, 4.07, P.F_EAVE, P.RIDGE_F_Z - 0.05)
    for s in (-1, 1):
        y0 = 4.0 + s*(4.0 - P.WALL + 0.05)
        cf.beam((xf, y0, P.F_EAVE), (xf, 4.0 + s*0.08, P.RIDGE_F_Z - 0.10), 0.13, 0.17)
        cf.beam((xf, 4.0 + s*0.07, P.F_EAVE + 0.7), (xf, 4.0 + s*1.2, P.F_EAVE + 0.02 + (4.0 - 1.2)*0 + 0.05), 0.10, 0.12)
tag(cf.done(bev=0.01), "statique")

# =====================================================================
# Fournil : table farinée, pâtons, pétrin, pelles accrochées, sacs, rangements, bûches
# =====================================================================
fn = Pack("Props_Fournil_Table", ["chene_clair", "farine", "mie", "toile_creme", "osier", "fer", "chene"], "Props_Interieurs")
tx0, tx1, ty0, ty1, tz = 12.70, 13.85, 2.00, 4.60, 0.88
fn.box(tx0, tx1, ty0, ty1, tz - 0.07, tz)
for x in (tx0 + 0.08, tx1 - 0.08):
    for y in (ty0 + 0.1, ty1 - 0.1):
        fn.box(x - 0.05, x + 0.05, y - 0.05, y + 0.05, P.SOL, tz - 0.07)
    fn.box(x - 0.03, x + 0.03, ty0 + 0.1, ty1 - 0.1, 0.25, 0.32)
for k in range(9):                                                          # farine répandue
    fn.cube(Matrix.Translation((U(tx0 + 0.15, tx1 - 0.15), U(ty0 + 0.15, ty1 - 0.15), tz + 0.001)) @ Matrix.Rotation(U(0, 3), 4, "Z") @
            Matrix.Diagonal((U(0.2, 0.5), U(0.15, 0.4), 0.003, 1)), m="farine")
fn.box(tx0 + 0.12, tx0 + 0.62, ty0 + 0.15, ty1 - 0.25, tz, tz + 0.006, m="toile_creme")               # couche de lin
for i in range(2):
    for j in range(5):
        paton(fn, tx0 + 0.24 + i*0.24, ty0 + 0.35 + j*0.42, tz + 0.006, U(0.075, 0.09))
for j in range(3):                                                           # pâtons façonnés en bannetons
    corbeille(fn, tx1 - 0.25, ty0 + 0.45 + j*0.55, tz, 0.13, 0.08, linge=True)
    paton(fn, tx1 - 0.25, ty0 + 0.45 + j*0.55, tz + 0.05, 0.10)
fn.box(tx1 - 0.5, tx1 - 0.42, ty1 - 0.5, ty1 - 0.38, tz, tz + 0.10, m="fer")                                   # coupe-pâte
fn.cube(Matrix.Translation((tx0 + 0.85, ty1 - 0.32, tz + 0.006)) @ Matrix.Rotation(0.4, 4, "Z") @ Matrix.Diagonal((0.14, 0.10, 0.01, 1)), m="chene")  # corne
fn.cyl((tx0 + 0.7, ty0 + 0.22, tz), (tx0 + 0.7, ty0 + 0.22, tz + 0.10), 0.16, "chene", seg=20, r2=0.20, caps=False)             # jatte
fn.cyl((tx0 + 0.7, ty0 + 0.22, tz), (tx0 + 0.7, ty0 + 0.22, tz + 0.01), 0.16, "chene", seg=20)
for k in range(14):                                                         # farine au sol
    fn.cube(Matrix.Translation((U(12.4, 14.0), U(1.6, 5.0), P.SOL + 0.002)) @ Matrix.Rotation(U(0, 3), 4, "Z") @ Matrix.Diagonal((U(0.1, 0.4), U(0.1, 0.3), 0.003, 1)), m="farine")
tag(fn.done(bev=0.006))
pt = Pack("Props_Fournil_Petrin", ["chene", "farine", "fer"], "Props_Interieurs")
px0, px1, py0, py1 = 13.25, 13.95, 5.95, 7.35
pt.hull([(px0, py0, 0.55), (px1, py0, 0.55), (px0, py1, 0.55), (px1, py1, 0.55), (px0 - 0.08, py0 - 0.05, 0.92), (px1, py0 - 0.05, 0.92),
         (px0 - 0.08, py1 + 0.05, 0.92), (px1, py1 + 0.05, 0.92)])
for x in (px0 + 0.06, px1 - 0.06):
    for y in (py0 + 0.08, py1 - 0.08):
        pt.box(x - 0.045, x + 0.045, y - 0.045, y + 0.045, P.SOL, 0.56)
pt.box(px0 - 0.1, px1 + 0.02, py0 - 0.07, py1 + 0.07, 0.92, 0.96, m=0)
pt.box(px0 + 0.1, px1 - 0.1, py0 + 0.2, py1 - 0.3, 0.96, 0.963, m=1)
tag(pt.done(bev=0.008))
pk = Pack("Props_Fournil_PellesAccrochees", ["chene_clair", "fer", "chene"], "Props_Interieurs")
pk.box(10.02, 10.08, 0.9, 3.9, 2.30, 2.40, m="chene")
for k, y in enumerate((1.15, 1.75, 2.35, 3.0, 3.6)):
    pk.cyl((10.02, y, 2.35), (10.20, y, 2.35), 0.018, "chene", seg=6)
    L = U(2.2, 2.7) if k % 2 == 0 else U(1.6, 1.9)
    pelle(pk, (10.12, y + U(-0.04, 0.04), max(P.SOL + 0.03, 2.40 - L + 0.12)), (10.12, y, 2.62), 0.30 if k % 2 == 0 else 0.22, 0.45 if k % 2 == 0 else 0.30)
pk.cyl((10.06, 4.2, 1.95), (10.06, 4.2, 2.6), 0.012, "fer", seg=6)                     # rable (râteau à braises), pendu
pelle(pk, (12.95, 7.15, P.SOL), (12.80, 7.42, 2.35), 0.30, 0.45)
pelle(pk, (13.20, 7.12, P.SOL), (13.05, 7.42, 2.20), 0.24, 0.32)
tag(pk.done(bev=0.004))
sk = Pack("Props_Fournil_SacsFarine", ["toile"], "Props_Interieurs")
for (x, y, z, a, s) in ((13.55, 0.80, P.SOL, 0.3, 1.0), (13.62, 1.28, P.SOL, -0.2, 1.05), (13.25, 1.05, P.SOL, 1.4, 0.95), (13.58, 1.02, P.SOL + 0.34, 0.1, 0.92)):
    sac(sk, x, y, z, a, s, FONT)
tag(sk.done())
et = Pack("Props_Fournil_Rangements", ["planche", "osier", "terrecuite", "faience", "fer", "chene", "toile_creme"], "Props_Interieurs")
ex0, ex1, ey0, ey1 = 10.02, 10.40, 5.60, 7.15
for y in (ey0 + 0.03, ey1 - 0.03):
    et.box(ex0, ex1, y - 0.03, y + 0.03, P.SOL, 2.10, m="chene")
for z in (0.40, 0.90, 1.40, 1.90):
    et.box(ex0, ex1, ey0, ey1, z, z + 0.03)
    if z < 1.5:
        for j in range(3):
            corbeille(et, ex0 + 0.19, ey0 + 0.27 + j*0.5, z + 0.03, 0.15, 0.08, linge=False)
    else:
        for j in range(4):
            yy = ey0 + 0.22 + j*0.36
            et.cyl((ex0 + 0.19, yy, z + 0.03), (ex0 + 0.19, yy, z + 0.25), 0.08 if j % 2 else 0.06, "terrecuite" if j % 2 else "faience", seg=14, r2=0.06 if j % 2 else 0.05)
tag(et.done(bev=0.005))
bb = Pack("Props_Fournil_BoisFour", ["ecorce", "buche"], "Props_Interieurs")
pile_bois(bb, 10.25, 11.15, 7.15, P.SOL, 4, L=0.55, axis="y")
tag(bb.done())
ln = Pack("Props_Fournil_Lanterne", ["fer", "verre", "cire", "flamme"], "Props_Interieurs")
ln.tube([(12.2, 3.1, P.F_EAVE - 0.24), (12.2, 3.1, 2.55)], 0.006, "fer", nv=5); lanterne(ln, 12.2, 3.1, 2.38)
tag(ln.done())
point_light("Lumiere_Fournil", (12.2, 3.1, 2.32), 220, (1.0, 0.60, 0.30), 0.08)

# =====================================================================
# Boutique : comptoir, présentoir à viennoiseries, étagères de pains, étal derrière la vitrine, lanternes
# =====================================================================
cp = Pack("Props_Boutique_Comptoir", ["chene", "chene_clair", "cuivre", "fer"], "Props_Interieurs")
cx0, cx1, cy0, cy1, cz = 3.00, 6.20, 4.75, 5.40, 1.00
cp.box(cx0, cx1, cy0 + 0.05, cy1, P.SOL, cz - 0.05)
k = 0; x = cx0 + 0.05
while x < cx1 - 0.3:                                                          # panneaux du devant
    cp.box(x + 0.05, x + 0.75, cy0 + 0.03, cy0 + 0.05, 0.25, cz - 0.15, m=1); x += 0.8
cp.box(cx0 - 0.04, cx1 + 0.04, cy0 - 0.03, cy1 + 0.03, cz - 0.05, cz)
cp.box(cx0, cx1, cy0 + 0.02, cy0 + 0.05, P.SOL, 0.18)
bx_, by_ = cx0 + 0.55, cy0 + 0.30                                              # balance de comptoir (Roberval, laiton)
cp.box(bx_ - 0.22, bx_ + 0.22, by_ - 0.09, by_ + 0.09, cz, cz + 0.10, m="chene")
cp.cyl((bx_, by_, cz + 0.10), (bx_, by_, cz + 0.20), 0.02, "cuivre", seg=8)
cp.box(bx_ - 0.20, bx_ + 0.20, by_ - 0.015, by_ + 0.015, cz + 0.20, cz + 0.225, m="cuivre")
for sx in (-1, 1):
    cp.cyl((bx_ + sx*0.19, by_, cz + 0.225), (bx_ + sx*0.19, by_, cz + 0.245), 0.11, "cuivre", seg=14, r2=0.13)
    for k in range(3): cp.cyl((bx_ + sx*0.19 + 0.012*k, by_ + 0.02, cz + 0.255), (bx_ + sx*0.19 + 0.012*k, by_ + 0.02, cz + 0.26 + 0.01*k), 0.02 + 0.006*k, "fer", seg=8)
tag(cp.done(bev=0.006), "statique")
pc = Pack("Props_Boutique_PainsComptoir", ["pain", "farine", "osier", "toile_creme", "brioche", "seigle"], "Props_Interieurs")
corbeille(pc, 5.30, 5.05, cz, 0.24, 0.10)
for i in range(4): brioche(pc, 5.22 + (i % 2)*0.15, 4.98 + (i // 2)*0.14, cz + 0.06)
corbeille(pc, 4.55, 5.05, cz, 0.22, 0.09); miche(pc, 4.50, 5.02, cz + 0.05, 0.13); miche(pc, 4.62, 5.12, cz + 0.07, 0.11)
batard(pc, 3.95, 5.08, cz, 1.4); couronne(pc, 5.85, 5.05, cz, 0.13, 0.045)
tag(pc.done())
pv = Pack("Props_Boutique_PresentoirViennoiseries", ["chene", "verre_vitrine", "brioche", "faience", "osier", "toile_creme"], "Props_Interieurs")
vx0, vx1 = 6.25, 7.65
pv.box(vx0, vx1, cy0, cy1, P.SOL, 0.85)
pv.box(vx0 - 0.03, vx1 + 0.03, cy0 - 0.03, cy1 + 0.03, 0.85, 0.89)
for x in (vx0 + 0.02, vx1 - 0.02):
    for y in (cy0 + 0.02, cy1 - 0.02):
        pv.box(x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.89, 1.32)
pv.box(vx0, vx1, cy0, cy1, 1.30, 1.34)
pv.box(vx0 + 0.02, vx1 - 0.02, cy0 - 0.004, cy0 + 0.004, 0.89, 1.30, m="verre_vitrine"); pv.box(vx0 + 0.02, vx1 - 0.02, cy1 - 0.004, cy1 + 0.004, 0.89, 1.30, m="verre_vitrine")
for x in (vx0, vx1): pv.box(x - 0.004, x + 0.004, cy0 + 0.02, cy1 - 0.02, 0.89, 1.30, m="verre_vitrine")
pv.box(vx0 + 0.02, vx1 - 0.02, cy0 + 0.02, cy1 - 0.02, 1.34, 1.348, m="verre_vitrine")
pv.box(vx0 + 0.03, vx1 - 0.03, cy0 + 0.03, cy1 - 0.03, 1.09, 1.10, m="verre_vitrine")                       # tablette de verre
for lvl, zt in enumerate((0.905, 1.10)):
    for j, xx in enumerate((vx0 + 0.24, vx0 + 0.70, vx0 + 1.16)):
        yc_ = (cy0 + cy1)/2
        pv.box(xx - 0.21, xx + 0.21, yc_ - 0.24, yc_ + 0.24, zt, zt + 0.012, m="osier" if (j + lvl) % 2 else "faience")
        kind = (j + 2*lvl) % 3
        for r in range(3):
            for c in range(3):
                px_, py_ = xx - 0.13 + c*0.13 + U(-0.01, 0.01), yc_ - 0.15 + r*0.15 + U(-0.01, 0.01)
                if kind == 0: croissant(pv, px_, py_, zt + 0.012, U(0, 6.3), 0.85)
                elif kind == 1: brioche(pv, px_, py_, zt + 0.012, 0.75)
                else: chausson(pv, px_, py_, zt + 0.012, U(-0.4, 0.4), 0.95)
tag(pv.done(bev=0.004))
# étagères de pains contre le mur du fond (de part et d'autre de la fenêtre R1)
set_qualite(0)                                                               # pains du fond : variantes légères
eb = Pack("Props_Boutique_EtageresPains", ["chene", "planche", "pain", "farine", "seigle", "osier", "toile_creme"], "Props_Interieurs")
for (sx0, sx1) in ((1.75, 4.75), (6.05, 7.85)):
    for x in (sx0, sx1):
        eb.box(x - 0.04, x + 0.04, 7.50, 7.98, P.SOL, 2.35, m="chene")
    eb.box(sx0, sx1, 7.94, 7.98, 0.45, 2.35, m="planche")
    for lvl, z in enumerate((0.55, 1.05, 1.55, 2.02)):
        eb.cube(Matrix.Translation(((sx0 + sx1)/2, 7.74, z)) @ Matrix.Rotation(-0.10, 4, "X") @ Matrix.Diagonal((sx1 - sx0 - 0.08, 0.46, 0.03, 1)), m="planche")
        eb.box(sx0 + 0.04, sx1 - 0.04, 7.48, 7.51, z - 0.01, z + 0.06, m="chene")
        for yrow in (7.86, 7.62):
            x = sx0 + 0.14
            zz = z + 0.02 + (7.74 - yrow)*0.10 + 0.012
            while x < sx1 - 0.14:
                kind = rnd.random()
                if lvl == 3 and kind < 0.4:
                    corbeille(eb, x + 0.05, yrow, z + 0.015, 0.13, 0.09, linge=True); miche(eb, x + 0.05, yrow, z + 0.06, 0.10); x += 0.30
                elif kind < 0.33:
                    miche(eb, x + 0.07, yrow, zz, U(0.11, 0.13)); x += 0.25
                elif kind < 0.62:
                    batard(eb, x + 0.02, yrow, zz, math.pi/2 + U(-0.15, 0.15), 0.19, 0.072); x += 0.17
                elif kind < 0.82:
                    seigle(eb, x + 0.05, yrow, zz, math.pi/2); x += 0.22
                else:
                    couronne(eb, x + 0.10, yrow, zz, 0.12, 0.045); x += 0.28
tag(eb.done(bev=0.004))
set_qualite(1)
# étal intérieur derrière la vitrine (pain visible de la rue)
ev = Pack("Props_Boutique_EtalVitrine", ["chene", "toile_creme", "pain", "farine", "seigle", "osier", "brioche"], "Props_Interieurs")
v1 = P.ouv("V1")
ev.box(v1["a0"] + 0.05, v1["a1"] - 0.05, 0.52, 0.86, 0.94, 0.98)                     # gradin bas, contre la vitre
ev.box(v1["a0"] + 0.05, v1["a1"] - 0.05, 0.86, 1.22, 1.24, 1.28)                     # gradin haut
ev.box(v1["a0"] + 0.05, v1["a1"] - 0.05, 0.84, 0.88, P.SOL, 1.24)
for x in (v1["a0"] + 0.12, (v1["a0"] + v1["a1"])/2, v1["a1"] - 0.12):
    ev.box(x - 0.03, x + 0.03, 0.55, 0.61, P.SOL, 0.94); ev.box(x - 0.03, x + 0.03, 1.15, 1.21, P.SOL, 1.24)
ev.box(v1["a0"] + 0.1, v1["a1"] - 0.1, 0.53, 0.85, 0.98, 0.984, m="toile_creme"); ev.box(v1["a0"] + 0.1, v1["a1"] - 0.1, 0.87, 1.21, 1.28, 1.284, m="toile_creme")
assortiment(ev, v1["a0"] + 0.12, v1["a1"] - 0.12, 0.68, 0.984, ("miche", "couronne", "batard", "seigle", "miche", "batard", "couronne", "miche", "seigle", "batard", "miche", "couronne"))
assortiment(ev, v1["a0"] + 0.15, v1["a1"] - 0.15, 1.04, 1.284, ("seigle", "miche", "batard", "miche", "couronne", "seigle", "miche", "batard", "miche", "seigle"))
for xb in (v1["a0"] + 0.45, (v1["a0"] + v1["a1"])/2, v1["a1"] - 0.5):
    corbeille(ev, xb, 1.04, 1.284, 0.17, 0.09)
    for i in range(4): croissant(ev, xb - 0.05 + (i % 2)*0.1, 1.00 + (i // 2)*0.08, 1.35, U(0, 6), 0.8)
for i in range(5): brioche(ev, v1["a0"] + 1.6 + i*0.12, 0.62, 0.984, 0.85)
tag(ev.done(bev=0.004))
# paniers, tonneau, banc côté baie (bas, hors ligne de vue)
pb = Pack("Props_Boutique_Paniers", ["osier", "pain", "farine", "toile_creme", "planche", "fer", "seigle"], "Props_Interieurs")
tonneau(pb, 8.95, 0.95, 0.26, 0.75, P.SOL)
corbeille(pb, 8.95, 0.95, P.SOL + 0.75, 0.22, 0.08); seigle(pb, 8.92, 0.93, P.SOL + 0.80, 0.5)
for (x, y, r) in ((3.45, 4.35, 0.24), (4.25, 4.30, 0.26), (5.55, 4.38, 0.22), (2.30, 4.30, 0.22), (6.55, 4.35, 0.22), (7.95, 4.75, 0.24)):
    corbeille(pb, x, y, P.SOL, r, 0.24); miche(pb, x - 0.04, y, P.SOL + 0.17, 0.12); miche(pb, x + 0.07, y + 0.06, P.SOL + 0.20, 0.11)
corbeille(pb, 0.95, 3.2, P.SOL, 0.22, 0.28); batard(pb, 0.95, 3.18, P.SOL + 0.18, 0.6)
tag(pb.done(bev=0.004))
rp = Pack("Props_Boutique_RayonPains", ["chene", "planche", "pain", "farine", "seigle", "osier", "toile_creme"], "Props_Interieurs")
rx0, rx1, ry0, ry1 = 9.08, 9.48, 1.55, 3.20
for y in (ry0 + 0.03, ry1 - 0.03):
    rp.box(rx0, rx1, y - 0.03, y + 0.03, P.SOL, 1.95, m="chene")
for lvl, z in enumerate((0.45, 0.90, 1.35, 1.80)):
    rp.box(rx0, rx1, ry0, ry1, z, z + 0.03, m="planche")
    y = ry0 + 0.16
    while y < ry1 - 0.14:
        k = rnd.random()
        if lvl == 0 and k < 0.5: corbeille(rp, (rx0 + rx1)/2, y + 0.08, z + 0.03, 0.15, 0.10); miche(rp, (rx0 + rx1)/2, y + 0.08, z + 0.07, 0.11); y += 0.36
        elif k < 0.4: miche(rp, (rx0 + rx1)/2, y + 0.06, z + 0.03, 0.12); y += 0.27
        elif k < 0.7: batard(rp, (rx0 + rx1)/2, y + 0.02, z + 0.03, 0.0, 0.17, 0.07); y += 0.20
        else: couronne(rp, (rx0 + rx1)/2, y + 0.11, z + 0.03, 0.12, 0.045); y += 0.29
tag(rp.done(bev=0.004))
lv = Pack("Props_Boutique_LavandeSechee", ["toile", "feuille", "lavande"], "Props_Interieurs")
for x in (5.2, 5.75, 6.3, 6.85, 7.4, 7.95, 8.5, 9.05):
    lavande_sechee(lv, x + U(-0.06, 0.06), 4.19 + U(-0.1, 0.1), 2.51, 14)
tag(lv.done())
sb = Pack("Props_Boutique_SacsFarine", ["toile"], "Props_Interieurs")
for (x, y, z, a, s_) in ((8.55, 3.55, P.SOL, -0.55, 1.0), (8.98, 3.70, P.SOL, -0.75, 0.95), (8.75, 3.62, P.SOL + 0.33, -0.6, 0.9)):
    sac(sb, x, y, z, a, s_, FONT)
tag(sb.done(), texte="Farine")
# lanternes suspendues aux solives
lb = Pack("Props_Boutique_Lanternes", ["fer", "verre", "cire", "flamme"], "Props_Interieurs")
for (x, y) in ((3.2, 2.6), (6.6, 2.6), (4.6, 6.4), (8.05, 3.75), (7.55, 6.0)):
    lb.tube([(x, y, 2.85), (x, y, 2.30)], 0.006, "fer", nv=5); lanterne(lb, x, y, 2.12)
    point_light(f"Lumiere_Boutique_{x:.0f}_{y:.0f}", (x, y, 2.06), 140, (1.0, 0.62, 0.32), 0.08)
lb.box(9.44, 9.5, 3.30, 3.40, 1.9, 2.2, m="fer"); lb.tube([(9.47, 3.35, 2.1), (9.25, 3.35, 2.15), (9.2, 3.35, 2.05)], 0.008, "fer", nv=5)
lanterne(lb, 9.2, 3.35, 1.86, 0.8)
point_light("Lumiere_Boutique_Applique", (9.2, 3.35, 1.80), 25, (1.0, 0.6, 0.3), 0.06)
tag(lb.done())
def area(name, loc, sx, sy, energy, color=(1.0, 0.70, 0.42)):
    ld = bpy.data.lights.new(name, "AREA"); ld.shape = "RECTANGLE"; ld.size = sx; ld.size_y = sy; ld.energy = energy; ld.color = color
    o = bpy.data.objects.new(name, ld); o.location = loc; o.visible_camera = False; o.visible_glossy = False; o.visible_transmission = False
    coll("Eclairage").objects.link(o); return o
area("Lumiere_Boutique_Ambiance", (4.8, 3.6, 2.80), 7.5, 5.5, 480)
area("Lumiere_Boutique_Vitrine", ((v1["a0"] + v1["a1"])/2, 0.95, 2.45), v1["a1"] - v1["a0"], 0.6, 260, (1.0, 0.74, 0.48))
area("Lumiere_Fournil_Ambiance", (12.2, 4.0, 3.0), 3.5, 4.0, 110, (1.0, 0.62, 0.35))
# portails de lumière du jour (échantillonnage du ciel par les baies ; pas de lumière ajoutée)
for o_ in P.OUV:
    a0, a1, z0, z1 = o_["a0"], o_["a1"], o_["z0"], o_["z1"]; c = (a0 + a1)/2; zc = (z0 + z1)/2
    if o_["mur"] in ("av_b", "av_f"): portal("Portail_" + o_["id"], (c, 0.52, zc), (-math.pi/2, 0, 0), a1 - a0, z1 - z0)
    elif o_["mur"] == "ar_b": portal("Portail_" + o_["id"], (c, 7.98, zc), (math.pi/2, 0, 0), a1 - a0, z1 - z0)
    elif o_["mur"] == "g_b": portal("Portail_" + o_["id"], (0.52, c, zc), (0, math.pi/2, 0), z1 - z0, a1 - a0)
    elif o_["mur"] == "d_f": portal("Portail_" + o_["id"], (13.98, c, zc), (0, -math.pi/2, 0), z1 - z0, a1 - a0)
print("intérieurs", round(time.time() - T0), "s", flush=True)
save_blend(os.path.join(WORK, "B_interieur.blend"))
import bf_props as _bp; print("PAINS", _bp.STATS, flush=True)
print("FIN étape B", round(time.time() - T0), "s", flush=True)
