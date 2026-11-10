# Étape A — architecture : murs (coques percées + maçonnerie pierre à pierre), pans de bois, planchers,
# escalier, toitures tuile à tuile, lucarne, cheminée et hotte, menuiseries.
#   /tmp/bvenv/bin/python -u -I scripts/build_archi.py            -> work/A_archi.blend
#   /tmp/bvenv/bin/python -u -I scripts/build_archi.py blockout   -> work/00_blockout.blend (volumes seuls)
import sys, os, math, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion
import bf_plan as P
from bf_lib import *

T0 = time.time()
BLOCKOUT = "blockout" in sys.argv
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = "METRIC"; sc.unit_settings.scale_length = 1.0; sc.unit_settings.length_unit = "METERS"
for c in COLLS: coll(c)
seed(11)
T48, T30 = P.T48, P.T30
W, INS = P.WALL, 0.06
Z_RDC = 2.85                       # arase des murs du rez de la boutique (dessous des solives)
Zv = Vector((0, 0, 1))

def P3(ax, a, n, z): return Vector((a, n, z)) if ax == "y" else Vector((n, a, z))
def NV(ax, out): return Vector((0, out, 0)) if ax == "y" else Vector((out, 0, 0))
def UV(ax): return Vector((1, 0, 0)) if ax == "y" else Vector((0, 1, 0))
def tag(o, role="statique", **kw):
    o["bf_role"] = role
    for k, v in kw.items(): o[k] = v
    return o

# ---------------------------------------------------------------- géométrie des arcs
def arc_geo(a0, a1, zs, zc):
    hs = (a1 - a0)/2; s = zc - zs; R = (hs*hs + s*s)/(2*s)
    return (a0 + a1)/2, hs, R, zc - R, math.asin(min(hs/R, 1.0))

def arch_pts(a0, a1, zs, zc, n=12):
    mid, hs, R, zcen, pm = arc_geo(a0, a1, zs, zc)
    return [(mid + R*math.sin(-pm + 2*pm*i/n), zcen + R*math.cos(-pm + 2*pm*i/n)) for i in range(n + 1)]

def open_poly(o, grow=0.0):
    a0, a1, z0, z1 = o["a0"] - grow, o["a1"] + grow, o["z0"] - grow, o["z1"]
    if o.get("arc"):
        return [(a0, z0)] + arch_pts(a0, a1, z1, o["z_cle"] + grow) + [(a1, z0)]
    return [(a0, z0), (a0, z1 + grow), (a1, z1 + grow), (a1, z0)]

# ---------------------------------------------------------------- coques de murs
def assign(o, rule):
    me = o.data; names = [m.name for m in me.materials]
    for p in me.polygons:
        mm = mat(rule(Vector(p.center), Vector(p.normal)))
        if mm.name not in names: me.materials.append(mm); names.append(mm.name)
        p.material_index = names.index(mm.name)

def rule_wall(out, inner, ext="mortier", reveal="taille", ztop=2.8):
    def r(c, n):
        d = n.dot(out)
        if d > 0.7: return ext
        if d < -0.7: return inner
        if n.z > 0.7 and c.z > ztop: return "mortier"
        return reveal
    return r

def coque(name, cname, ax, n0, n1, poly, opens=(), rule=None):
    pk = Pack(name, ["mortier"], cname)
    pk.prism([P3(ax, a, n0, z) for a, z in poly], NV(ax, 1)*(n1 - n0))
    o = pk.done()
    if opens:
        cp = Pack("_coupe", ["mortier"], cname)
        for ou in opens:
            cp.prism([P3(ax, a, n0 - 0.3, z) for a, z in open_poly(ou)], NV(ax, 1)*(n1 - n0 + 0.6))
        boolean_cut(o, cp.done())
    if rule: assign(o, rule)
    return tag(o)

# ---------------------------------------------------------------- maçonnerie pierre à pierre
COURSES = []; _z = -0.02
while _z < 11.4:
    _h = U(0.21, 0.33); COURSES.append((_z, _z + _h)); _z += _h
QL, QS = 0.56, 0.30

def stone_face(st, dr, ax, plane, out, a0, a1, opens=(), long_first=True, quoins=(True, True),
               zmin=-0.02, zmax=3.0, ztop=None, zbot=None):
    N_ = NV(ax, out)
    def blk(lot, b0, b1, z0, z1, depth, off, tilt=0.0, jit=0.0):
        if ztop: z1 = min(z1, ztop(b0), ztop(b1))
        if zbot: z0 = max(z0, zbot(b0), zbot(b1))
        if z1 - z0 < 0.07 or b1 - b0 < 0.05: return
        c = P3(ax, (b0 + b1)/2, plane + out*(-off - depth/2), (z0 + z1)/2)
        sx = (b1 - b0, depth, z1 - z0) if ax == "y" else (depth, b1 - b0, z1 - z0)
        lot.cube(Matrix.Translation(c) @ Matrix.Rotation(tilt, 4, N_) @ Matrix.Diagonal(sx + (1,)), jitter=jit, drop=-N_)
    def row(f0, f1, z0, z1):
        a = f0
        while f1 - a > 0.08:
            w = U(0.14, 0.56)
            if f1 - (a + w) < 0.2: w = f1 - a
            h = z1 - z0
            if h > 0.16 and rnd.random() < 0.3 and w < 0.42:
                zc = z0 + h/2 + U(-0.03, 0.03)
                blk(st, a + 0.012, a + w - 0.012, z0 + 0.012, zc - 0.006, 0.16, U(0.02, 0.04), U(-0.05, 0.05), 0.02)
                blk(st, a + 0.012, a + w - 0.012, zc + 0.006, z1 - 0.012, 0.16, U(0.02, 0.04), U(-0.05, 0.05), 0.02)
            else:
                hh = h*U(0.55, 0.94) if h > 0.14 else h - 0.02
                zc = z0 + h/2 + U(-0.02, 0.02)
                blk(st, a + 0.014, a + w - 0.014, zc - hh/2, zc + hh/2, 0.16, U(0.02, 0.045), U(-0.06, 0.06), 0.03)
            a += w
    for k, (cz0, cz1) in enumerate(COURSES):
        if cz1 <= zmin + 0.03 or cz0 >= zmax - 0.03: continue
        z0, z1 = max(cz0, zmin), min(cz1, zmax)
        busy = []
        qa = QL if ((k % 2 == 0) == long_first) else QS
        if quoins[0]:
            blk(dr, a0, a0 + qa, z0 + 0.008, z1 - 0.008, 0.35, 0.003, U(-0.004, 0.004), 0.004); busy.append((a0 - 0.1, a0 + qa + 0.012))
        if quoins[1]:
            blk(dr, a1 - qa, a1, z0 + 0.008, z1 - 0.008, 0.35, 0.003, U(-0.004, 0.004), 0.004); busy.append((a1 - qa - 0.012, a1 + 0.1))
        for o in opens:
            ob = o["z0"] - o.get("sill", 0.0); zs = o["zs"]
            tt = o["zc"] + o["d"] if o["kind"] == "arc" else zs + o.get("lintel", 0.0)
            if z1 <= ob + 0.001 or z0 >= tt - 0.001: continue
            jw = 0.38 if k % 2 == 0 else 0.24
            e0, e1 = o["a0"] - jw, o["a1"] + jw
            busy.append((e0 - 0.012, e1 + 0.012))
            if ob - z0 > 0.07: row(e0, e1, z0, ob)
            jz0, jz1 = max(z0, o["z0"]), min(z1, zs)
            if jz1 - jz0 > 0.06:
                blk(dr, e0, o["a0"], jz0 + 0.008, jz1 - 0.008, 0.45, 0.003, 0, 0.004)
                blk(dr, o["a1"], e1, jz0 + 0.008, jz1 - 0.008, 0.45, 0.003, 0, 0.004)
            if o["kind"] == "rect":
                if z1 - tt > 0.07: row(e0, e1, tt, z1)
            elif z1 > zs + 0.07:
                mid, hs, R, zcen, pm = arc_geo(o["a0"], o["a1"], zs, o["zc"]); Re = R + o["d"]
                def extr(x):
                    dx = abs(x - mid)
                    return zcen + math.sqrt(Re*Re - dx*dx) if dx < Re else -9
                a = e0
                while a < e1 - 0.05:
                    w = min(U(0.2, 0.32), e1 - a)
                    if e1 - (a + w) < 0.12: w = e1 - a
                    ze = max(extr(a), extr(a + w), extr(min(max(mid, a), a + w)))
                    lo = max(z0, ze + 0.012, zs if abs(a + w/2 - mid) < hs + 0.02 else z0)
                    if z1 - lo > 0.07:
                        blk(st, a + 0.012, a + w - 0.012, lo, z1 - 0.012, 0.16, U(0.02, 0.04), 0.0, 0.012)
                    a += w
        busy.sort(); cur = a0; free = []
        for b0, b1 in busy:
            if b0 > cur: free.append((cur, b0))
            cur = max(cur, b1)
        if a1 > cur: free.append((cur, a1))
        for f0, f1 in free: row(f0, f1, z0, z1)
    # voussoirs, appuis et linteaux de pierre
    for o in opens:
        if o["kind"] == "arc":
            mid, hs, R, zcen, pm = arc_geo(o["a0"], o["a1"], o["zs"], o["zc"]); d = o["d"]
            rm = R + d/2; n = max(5, int(2*pm*rm/0.21)) | 1
            for i in range(n):
                p0 = -pm + 2*pm*i/n; p1 = -pm + 2*pm*(i + 1)/n; pc = (p0 + p1)/2
                dd = d*(1.15 if i == n//2 else 1.0)
                wdt = (p1 - p0)*(R + dd/2) - 0.014
                c = P3(ax, mid + (R + dd/2)*math.sin(pc), plane + out*(-0.003 - 0.225), zcen + (R + dd/2)*math.cos(pc))
                tv = UV(ax)*math.cos(pc) - Zv*math.sin(pc); rv_ = UV(ax)*math.sin(pc) + Zv*math.cos(pc)
                dr.cube(frame_m(c, tv*wdt, N_*0.45, rv_*dd), jitter=0.003, drop=-N_)
        elif o.get("lintel") and not o.get("wood"):
            blk(dr, o["a0"] - 0.14, o["a1"] + 0.14, o["zs"] + 0.006, o["zs"] + o["lintel"] - 0.006, 0.5, 0.0)
        if o.get("sill"):
            blk(dr, o["a0"] - 0.07, o["a1"] + 0.07, o["z0"] - o["sill"] + 0.004, o["z0"], 0.55, -0.045)

def sopen(i, sill=0.0, lintel=0.0, d=0.30, wood=False):
    o = P.ouv(i)
    if o.get("arc"): return dict(a0=o["a0"], a1=o["a1"], z0=o["z0"], kind="arc", zs=o["z1"], zc=o["z_cle"], d=d, sill=sill)
    return dict(a0=o["a0"], a1=o["a1"], z0=o["z0"], kind="rect", zs=o["z1"], lintel=lintel, sill=sill, wood=wood)

# =====================================================================
# 1. Murs de pierre
# =====================================================================
o_ = P.ouv
ZF = lambda y: P.F_EAVE + (4.0 - abs(y - 4.0))*T30                       # rampant du fournil (plan de toiture)
ZB = lambda y: P.Z_SABL + min(y - P.ROOF_B_Y0, P.ROOF_B_Y1 - y)*T48       # rampant de la boutique
intB, intF = "enduit_int", "enduit_fournil"
coque("Boutique_Mur_Facade", "Architecture_Boutique", "y", INS, W, [(INS, 0), (INS, Z_RDC), (9.5, Z_RDC), (9.5, 0)],
      [o_("V1"), o_("P1")], rule_wall(Vector((0, -1, 0)), intB))
coque("Boutique_Mur_Gauche", "Architecture_Boutique", "x", INS, W, [(W, 0), (W, Z_RDC), (8.5 - W, Z_RDC), (8.5 - W, 0)],
      [o_("G1")], rule_wall(Vector((-1, 0, 0)), intB))
coque("Boutique_Mur_Arriere", "Architecture_Boutique", "y", 8.5 - W, 8.5 - INS, [(INS, 0), (INS, Z_RDC), (9.5, Z_RDC), (9.5, 0)],
      [o_("R1"), o_("R2")], rule_wall(Vector((0, 1, 0)), intB))
sep_poly = [(INS, 0), (INS, P.Z_SABL + (INS - P.ROOF_B_Y0)*T48 - 0.02), (P.RIDGE_B_Y, P.RIDGE_B_Z - 0.03),
            (8.5 - INS, P.Z_SABL - 0.02), (8.5 - INS, 0)]
coque("MurSeparatif", "Architecture_Boutique", "x", 9.5, 10.0 - INS, sep_poly, [o_("B1")],
      lambda c, n: "enduit_int" if n.x < -0.7 else ("mortier" if (abs(n.x) > 0.7 or abs(n.y) > 0.7 or c.z > 2.3) else "enduit_int"))
skin = Pack("MurSeparatif_EnduitFournil", ["enduit_fournil"], "Architecture_Fournil")
skin.prism([P3("x", a, 10.0 - INS, z) for a, z in [(W, 0), (W, P.F_EAVE), (4.0, P.RIDGE_F_Z - 0.05), (8.0 - W, P.F_EAVE), (8.0 - W, 0)]], Vector((INS, 0, 0)))
sk = skin.done(); boolean_cut(sk, (lambda cp: (cp.prism([P3("x", a, 9.8, z) for a, z in open_poly(o_("B1"))], Vector((0.4, 0, 0))), cp.done())[1])(Pack("_c", ["mortier"], "Architecture_Fournil"))); tag(sk)
coque("Fournil_Mur_Facade", "Architecture_Fournil", "y", INS, W, [(10.0, 0), (10.0, P.F_EAVE), (14.5 - INS, P.F_EAVE), (14.5 - INS, 0)],
      [o_("PF")], rule_wall(Vector((0, -1, 0)), intF, ztop=3.1))
fd_poly = [(W, 0), (W, P.F_EAVE), (INS, P.F_EAVE), (4.0, P.RIDGE_F_Z - 0.03), (8.0 - INS, P.F_EAVE), (8.0 - W, P.F_EAVE), (8.0 - W, 0)]
coque("Fournil_Mur_Droit", "Architecture_Fournil", "x", 14.5 - W, 14.5 - INS, fd_poly, [o_("FD1")], rule_wall(Vector((1, 0, 0)), intF, ztop=3.1))
coque("Fournil_Mur_Arriere", "Architecture_Fournil", "y", 8.0 - W, 8.0 - INS, [(10.0, 0), (10.0, P.F_EAVE), (14.5 - INS, P.F_EAVE), (14.5 - INS, 0)],
      [o_("BF")], rule_wall(Vector((0, 1, 0)), intF, reveal="four", ztop=3.1))

if not BLOCKOUT:
    stB = Pack("Boutique_Moellons", ["moellon"], "Architecture_Boutique")
    stBF = Pack("Boutique_Moellons_Facade", ["moellon"], "Architecture_Boutique")
    stFF = Pack("Fournil_Moellons_Facade", ["moellon"], "Architecture_Fournil")
    drB = Pack("Boutique_PierresDeTaille", ["taille"], "Architecture_Boutique")
    stF = Pack("Fournil_Moellons", ["moellon"], "Architecture_Fournil")
    drF = Pack("Fournil_PierresDeTaille", ["taille"], "Architecture_Fournil")
    # façade : boutique (0-9,5) puis mur séparatif (9,5-10, chaîne jusqu'à l'égout), fournil adossé (coup de sabre en X = 10)
    stone_face(stBF, drB, "y", 0.0, -1, 0.0, 10.0, [sopen("V1", sill=0.13, lintel=0.27, wood=True), sopen("P1", d=0.32)],
               True, (True, False), zmax=Z_RDC)
    stone_face(drB, drB, "y", 0.0, -1, 9.5, 10.0, [], False, (False, False), zmin=Z_RDC, zmax=P.Z_SABL + 0.15)
    stone_face(stB, drB, "x", 0.0, -1, 0.0, 8.5, [sopen("G1", sill=0.10, lintel=0.24)], False, (True, True), zmax=Z_RDC)
    stone_face(stB, drB, "y", 8.5, 1, 0.0, 10.0, [sopen("R1", sill=0.10, lintel=0.24), sopen("R2", lintel=0.26)],
               True, (True, True), zmax=Z_RDC)
    stone_face(drB, drB, "y", 8.5, 1, 9.5, 10.0, [], False, (False, False), zmin=Z_RDC, zmax=P.Z_SABL + 0.1)
    # pignon de pierre du mur séparatif, au-dessus du toit du fournil
    stone_face(stB, drB, "x", 10.0, 1, 0.0, 8.5, [], True, (True, True), zmin=-0.02, zmax=11.0,
               ztop=lambda y: ZB(min(max(y, 0.0), 8.5)) - 0.05,
               zbot=lambda y: (ZF(min(max(y, 0.0), 8.0)) + 0.02) if y < 8.0 - 0.01 else -0.02)
    stone_face(stFF, drF, "y", 0.0, -1, 10.0, 14.5, [sopen("PF", d=0.36)], False, (False, True), zmax=P.F_EAVE)
    stone_face(stF, drF, "x", 14.5, 1, 0.0, 8.0, [sopen("FD1", sill=0.10, lintel=0.22)], True, (True, True), zmax=6.0,
               ztop=lambda y: ZF(min(max(y, 0.0), 8.0)) - 0.03)
    stone_face(stF, drF, "y", 8.0, 1, 10.0, 14.5, [sopen("BF", d=0.26)], True, (False, True), zmax=P.F_EAVE)
    # parement de pierre autour de la bouche du four (face intérieure du mur arrière du fournil)
    stone_face(stF, drF, "y", 7.44, -1, 11.05, 13.25, [sopen("BF", d=0.26)], True, (False, False), zmin=0.10, zmax=P.CHEM["hotte"]["z0"])
    for p_ in (stB, stBF, drB, stF, stFF, drF): print(p_.name, p_.n, flush=True)
    tag(stBF.done(bev=0.03, seg=2, recalc=False)); tag(stFF.done(bev=0.03, seg=2, recalc=False))
    tag(stB.done(bev=0.03, seg=1, recalc=False)); tag(stF.done(bev=0.03, seg=1, recalc=False))
    tag(drB.done(bev=0.012, seg=1, recalc=False)); tag(drF.done(bev=0.012, seg=1, recalc=False))
    # remplissage d'enduit entre les abouts de solives (dessus des murs du rez)
    fl = Pack("Boutique_Entrevous", ["enduit"], "Architecture_Boutique")
    fl.box(0.0, 9.5, 0.0, W, Z_RDC, P.Z_ETAGE); fl.box(0.0, 9.5, 8.5 - W, 8.5, Z_RDC, P.Z_ETAGE); fl.box(0.0, W, W, 8.5 - W, Z_RDC, P.Z_ETAGE)
    tag(fl.done())
print("murs", round(time.time() - T0), "s", flush=True)

# =====================================================================
# 2. Étage à pans de bois (façade en encorbellement, pignon gauche, arrière)
# =====================================================================
def colombage(name, ax, plane, out, a0, a1, posts, bays, wins, z0=P.Z_ETAGE, z1=P.Z_SABL, skip=()):
    N_ = NV(ax, out)
    slab = Pack(name + "_Enduit", ["enduit"], "Architecture_Boutique")
    slab.prism([P3(ax, a, plane - out*0.03, z) for a, z in [(a0, z0), (a0, z1), (a1, z1), (a1, z0)]], -N_*0.17)
    so = slab.done()
    if wins:
        cp = Pack("_c", ["mortier"], "Architecture_Boutique")
        for w in wins: cp.prism([P3(ax, a, plane + out*0.2, z) for a, z in open_poly(w)], -N_*0.6)
        boolean_cut(so, cp.done())
    assign(so, lambda c, n: "enduit" if n.dot(N_) > 0.7 else ("enduit_int" if n.dot(N_) < -0.7 else "enduit"))
    tag(so)
    if BLOCKOUT: return
    pk = Pack(name + "_PansDeBois", ["chene"], "Architecture_Boutique")
    def B(b0, b1, zz0, zz1, d0=0.0, d1=0.10):
        n0, n1 = plane - out*d0, plane - out*d1
        if ax == "y": pk.box(b0, b1, n0, n1, zz0, zz1)
        else: pk.box(n0, n1, b0, b1, zz0, zz1)
    def Pt(a, z, d=0.05): return P3(ax, a, plane - out*d, z)
    def diag(aa, za, ab, zb, w=0.15): pk.beam(Pt(aa, za), Pt(ab, zb), 0.095, w)
    def peg(a, z): pk.cyl(Pt(a, z, -0.006), Pt(a, z, 0.02), 0.017, "buche", seg=8)
    B(a0, a1, z0, z0 + 0.20, 0.0, 0.20); B(a0, a1, z1 - 0.20, z1, 0.0, 0.20)
    zb, zt = z0 + 0.20, z1 - 0.20
    for i, a in enumerate(posts):
        if i in skip: continue
        B(a - 0.085 + U(-0.008, 0.008), a + 0.085 + U(-0.008, 0.008), zb, zt, 0.0, 0.12)
        peg(a, zb + 0.08); peg(a, zt - 0.08)
    for i in range(len(posts) - 1):
        l, r = posts[i] + 0.085, posts[i + 1] - 0.085; kind = bays.get(i, "ent")
        if kind == "croix":
            diag(l, zb, r, zt); diag(r, zb, l, zt); peg((l + r)/2, (zb + zt)/2)
        elif kind in ("guette_g", "guette_d"):
            if kind == "guette_g": diag(l, zt - 0.02, r, zb + 0.02)
            else: diag(l, zb + 0.02, r, zt - 0.02)
            B(l, r, 4.36, 4.50, 0.0, 0.10)
        elif kind == "ent":
            B(l, r, 4.38, 4.52, 0.0, 0.10)
        elif isinstance(kind, dict):          # fenêtre
            w = kind
            B(l - 0.02, r + 0.02, w["z0"] - 0.12, w["z0"], -0.035, 0.13)
            B(l, r, w["z1"], w["z1"] + 0.14, 0.0, 0.11)
            diag(l, zb, r, w["z0"] - 0.12, 0.12); diag(r, zb, l, w["z0"] - 0.12, 0.12)
            m_ = (l + r)/2
            if zt - (w["z1"] + 0.14) > 0.12: B(m_ - 0.06, m_ + 0.06, w["z1"] + 0.14, zt, 0.0, 0.10)
    tag(pk.done(bev=0.012, seg=1))

def wd(i): o = P.ouv(i); return dict(a0=o["a0"], a1=o["a1"], z0=o["z0"], z1=o["z1"])
E1, E2, EG1, EA1, EA2 = wd("E1"), wd("E2"), wd("EG1"), wd("EA1"), wd("EA2")
FRONT_POSTS = [0.09, 1.30, 1.815, 2.885, 3.40, 4.75, 6.10, 6.615, 7.685, 8.20, 9.41]
colombage("Etage_Facade", "y", -P.ENC, -1, 0.0, 9.5, FRONT_POSTS,
          {0: "guette_g", 2: E1, 4: "croix", 5: "croix", 7: E2, 9: "guette_d"}, [E1, E2])
colombage("Etage_PignonGauche", "x", 0.0, -1, 0.02, 8.5, [-0.09, 1.2, 2.4, 3.615, 4.585, 5.8, 7.1, 8.41],
          {0: "guette_g", 3: EG1, 5: "croix", 6: "guette_d"}, [EG1], skip=(0,))
colombage("Etage_Arriere", "y", 8.5, 1, 0.20, 9.5, [0.09, 1.2, 1.915, 2.885, 3.9, 4.75, 5.6, 6.615, 7.585, 8.4, 9.41],
          {0: "guette_d", 2: EA1, 4: "croix", 5: "croix", 7: EA2, 9: "guette_g"}, [EA1, EA2], skip=(0,))

# pignon gauche à pans de bois au-dessus de la sablière haute
gp = Pack("PignonGauche_Enduit", ["enduit"], "Architecture_Boutique")
gpoly = [(-P.ENC + 0.02, P.Z_SABL), (8.48, P.Z_SABL), (P.RIDGE_B_Y, P.RIDGE_B_Z - 0.06)]
gp.prism([P3("x", a, 0.03, z) for a, z in gpoly], Vector((0.17, 0, 0)))
go = gp.done(); assign(go, lambda c, n: "enduit" if n.x < -0.7 else ("enduit_int" if n.x > 0.7 else "enduit")); tag(go)
if not BLOCKOUT:
    gk = Pack("PignonGauche_PansDeBois", ["chene"], "Architecture_Boutique")
    def gP(y, z): return Vector((0.05, y, z))
    yA, yB, yC, zC = -P.ENC + 0.08, 8.42, P.RIDGE_B_Y, P.RIDGE_B_Z - 0.12
    gk.beam(gP(yA, P.Z_SABL + 0.05), gP(yC, zC), 0.10, 0.20); gk.beam(gP(yB, P.Z_SABL + 0.05), gP(yC, zC), 0.10, 0.20)
    gk.box(0.0, 0.10, yC - 0.09, yC + 0.09, P.Z_SABL, zC - 0.1)
    ze = P.Z_SABL + 1.75
    ye0, ye1 = yA + (ze - P.Z_SABL)/T48, yB - (ze - P.Z_SABL)/T48
    gk.box(0.0, 0.10, ye0, ye1, ze, ze + 0.18)
    for y in (1.6, 6.7):
        top = P.Z_SABL + min(y - yA, yB - y)*T48 - 0.1
        gk.box(0.0, 0.10, y - 0.08, y + 0.08, P.Z_SABL, min(top, ze))
    for s in (-1, 1):
        gk.beam(gP(yC + s*0.09, ze + 0.18), gP(yC + s*1.1, ze + 0.18 + 1.1*T48 - 0.25), 0.095, 0.14)
        gk.beam(gP(yC + s*0.2, P.Z_SABL), gP(yC + s*1.7, ze), 0.095, 0.14)
    tag(gk.done(bev=0.012))
print("étage", round(time.time() - T0), "s", flush=True)

# =====================================================================
# 3. Planchers, solives, poutre, escalier
# =====================================================================
if not BLOCKOUT:
    sol = Pack("Boutique_Sol_Tomettes", ["tomette"], "Architecture_Boutique")
    r_ = 0.095; dx = r_*math.sqrt(3); dy = 1.5*r_
    yy, row_ = W - 0.02, 0
    while yy < 8.5 - W + r_:
        xx = W - 0.02 + (dx/2 if row_ % 2 else 0)
        while xx < 9.5 + r_:
            res = bmesh.ops.create_circle(sol.bm, cap_ends=True, cap_tris=False, segments=6, radius=r_ - 0.008,
                                          matrix=Matrix.Translation((xx + U(-0.003, 0.003), yy, P.SOL + U(-0.003, 0.0))) @ Matrix.Rotation(math.pi/6 + U(-0.03, 0.03), 4, "Z"))
            for f in {f for v in res["verts"] for f in v.link_faces}:
                f.normal_update()
                if f.normal.z < 0: f.normal_flip()
            sol._fin(res["verts"], 0, False, 0.0, None, {"var": rnd.random()})
            xx += dx
        yy += dy; row_ += 1
    tag(sol.done(recalc=False))
    base = Pack("Boutique_Sol_Chape", ["joint"], "Architecture_Boutique"); base.box(W - 0.02, 9.52, W - 0.02, 8.5 - W + 0.02, -0.02, P.SOL - 0.004); tag(base.done())
    fd = Pack("Fournil_Sol_Dalles", ["dalle"], "Architecture_Fournil")
    yy = W - 0.02
    while yy < 8.0 - W:
        h = min(U(0.35, 0.6), 8.0 - W - yy); xx = 10.0
        while xx < 14.0:
            w = min(U(0.4, 0.75), 14.0 - xx)
            fd.box(xx + 0.008, xx + w - 0.008, yy + 0.008, yy + h - 0.008, P.SOL - 0.06, P.SOL + U(-0.006, 0.0), jitter=0.004)
            xx += w
        yy += h
    fd.box(10.0, 14.0, W, 8.0 - W, -0.02, P.SOL - 0.06, m="mortier")
    tag(fd.done(bev=0.01))
    # solives (abouts en façade = encorbellement), poutre maîtresse, plancher de l'étage
    sv = Pack("Boutique_Solives", ["chene"], "Architecture_Boutique")
    T_ = P.TREMIE
    x = 0.62
    while x < 9.45:
        if T_["x0"] - 0.1 < x < T_["x1"] + 0.1:
            sv.box(x - 0.07, x + 0.07, -0.22, T_["y0"], Z_RDC, P.Z_ETAGE)
        else:
            sv.box(x - 0.07, x + 0.07, -0.22, 8.44, Z_RDC, P.Z_ETAGE)
        x += 0.55
    sv.box(T_["x0"], T_["x1"] + 0.1, T_["y0"] - 0.14, T_["y0"], Z_RDC, P.Z_ETAGE)            # chevêtre de trémie
    sv.box(T_["x1"] + 0.02, T_["x1"] + 0.16, T_["y0"], 8.0, Z_RDC, P.Z_ETAGE)
    sv.box(W, 9.5, 4.02, 4.36, Z_RDC - 0.34, Z_RDC)                                           # poutre maîtresse
    for xc_ in (W + 0.02, 9.48):
        for s in (-1, 1):
            sv.beam((xc_, 4.19 + s*0.05, Z_RDC - 0.34 - 0.55), (xc_ + (0.45 if xc_ < 5 else -0.45), 4.19 + s*0.05, Z_RDC - 0.34), 0.10, 0.12) if s < 0 else None
    tag(sv.done(bev=0.01))
    pl = Pack("Etage_Plancher", ["plancher"], "Architecture_Boutique")
    yy = 0.02
    while yy < 8.30:
        w = min(U(0.18, 0.26), 8.30 - yy)
        segs = [(0.20, 9.5)]
        if T_["y0"] < yy + w/2 < T_["y1"]: segs = [(0.20, T_["x0"]), (T_["x1"] + 0.16, 9.5)]
        for s0, s1 in segs:
            xx = s0
            while s1 - xx > 0.04:
                L_ = min(U(2.0, 4.5), s1 - xx)
                pl.box(xx + 0.002, xx + L_ - 0.002, yy + 0.003, yy + w - 0.003, P.Z_ETAGE, P.Z_ETAGE + 0.03)
                xx += L_
        yy += w
    tag(pl.done())
    # escalier droit le long du mur gauche (15 marches)
    E = P.ESC; es = Pack("Boutique_Escalier", ["chene_clair"], "Architecture_Boutique")
    n = E["marches"]; rise = (P.Z_ETAGE + 0.03 - P.SOL)/n; go_ = (E["y1"] - E["y0"])/(n - 1)
    for i in range(n - 1):
        y0_ = E["y0"] + i*go_; z_ = P.SOL + (i + 1)*rise
        es.box(E["x0"] + 0.02, E["x1"] - 0.02, y0_ - 0.03, y0_ + go_ + 0.02, z_ - 0.045, z_)
        es.box(E["x0"] + 0.04, E["x1"] - 0.04, y0_ - 0.02, y0_ - 0.0, z_ - rise, z_ - 0.045, m="planche")
    for xs in (E["x0"] - 0.0, E["x1"] - 0.06):
        es.beam((xs + 0.03, E["y0"] - 0.25, P.SOL), (xs + 0.03, E["y1"] + 0.05, P.Z_ETAGE + 0.05), 0.06, 0.28, m="chene")
    for i in range(0, n - 1, 2):
        y0_ = E["y0"] + i*go_ + go_/2; z_ = P.SOL + (i + 1)*rise
        es.box(E["x1"] - 0.05, E["x1"] - 0.01, y0_ - 0.02, y0_ + 0.02, z_, z_ + 0.9, m="chene")
    es.beam((E["x1"] - 0.03, E["y0"] - 0.1, P.SOL + 1.0), (E["x1"] - 0.03, E["y1"], P.Z_ETAGE + 0.95), 0.06, 0.06, m="chene")
    tag(es.done(bev=0.006), "statique")
print("planchers", round(time.time() - T0), "s", flush=True)

# =====================================================================
# 4. Toitures
# =====================================================================
TW, TL, EXPO, TV = 0.17, 0.27, 0.105, 0.08

def pan(tiles, ridge, ex, dsl, nrm, RW, Ls, skip, moss_bias):
    tilt = math.atan(0.022/TL)
    vs = []; v = 0.03
    while v < Ls - TL - 0.01: vs.append(v); v += EXPO
    vs.append(Ls - TL)
    for r, v in enumerate(vs):
        x = -RW/2 + (TW/2 if r % 2 else 0.0)
        if r % 2: x = -RW/2
        first = True
        while x < RW/2 - 0.02:
            w = TW/2 if (first and r % 2) else min(TW, RW/2 - x)
            first = False
            if w < 0.05: break
            hump = 0.02*math.sin(math.pi*min((v + TL/2)/Ls, 1.0))*(1 - min(abs(x + w/2)/(RW/2), 1))**0.5
            tl = tilt + U(-0.012, 0.012)
            ey = dsl*math.cos(tl) + nrm*math.sin(tl); ez = nrm*math.cos(tl) - dsl*math.sin(tl)
            q = Quaternion(ez, U(-0.035, 0.035))
            p = ridge + ex*(x + w/2) + dsl*(v + TL/2) + nrm*(TV + 0.010 + U(0, 0.004) - hump)
            if not (skip and skip(p)):
                moss = max(0.0, 0.5*math.sin(p.x*1.7 + v*2.3) + 0.5*math.sin(p.x*0.6 - v*1.1 + p.y) + moss_bias + 0.25*(v/Ls) + U(-0.2, 0.2))
                tiles.cube(frame_m(p, (q @ ex)*(w - 0.006), (q @ ey)*TL, ez*0.013), moss=min(moss, 1.0), keep=[ez, (q @ ey).normalized()])
            x += w

def roof(prefix, cname, xa, xb, y0, y1, zeave, pitch, over, M=None, skip=None, moss=0.45, rives=(True, True),
         sides=(-1, 1), rafters=True):
    M = M or Matrix.Identity(4); R3 = M.to_3x3()
    p = math.radians(pitch); yc, half = (y0 + y1)/2, (y1 - y0)/2
    zr = zeave + half*math.tan(p); Ls = half/math.cos(p) + over; RW = xb - xa; xm = (xa + xb)/2
    tiles = Pack(prefix + "_Tuiles", ["tuiles"], cname, attrs=("var", "moss"))
    vol = Pack(prefix + "_Voliges", ["planche"], cname)
    ch = Pack(prefix + "_Charpente", ["chene"], cname)
    ridge = M @ Vector((xm, yc, zr)); ex = R3 @ Vector((1, 0, 0)); up = R3 @ Vector((0, 0, 1))
    for side in sides:
        dsl = R3 @ Vector((0, side*math.cos(p), -math.sin(p))); nrm = R3 @ Vector((0, side*math.sin(p), math.cos(p)))
        vol.cube(frame_m(ridge + dsl*(Ls/2) + nrm*(TV/2), ex*RW, dsl*Ls, nrm*TV))
        if rafters and not BLOCKOUT:
            x = -RW/2 + 0.35
            while x < RW/2 - 0.2:
                pa = ridge + ex*x - nrm*0.065
                if not (skip and skip(pa + dsl*1.0 + nrm*0.2, True)):
                    ch.beam(pa, pa + dsl*(Ls - over*0.7), 0.08, 0.13)
                x += 0.6
        if not BLOCKOUT: pan(tiles, ridge, ex, dsl, nrm, RW, Ls, skip, moss if side > 0 else -0.35)
        for k, xe in enumerate((-RW/2, RW/2)):
            if rives[k] and not BLOCKOUT:
                pr = ridge + ex*(xe + (0.02 if k else -0.02)) + nrm*(TV + 0.03)
                vol.beam(pr, pr + dsl*Ls, 0.035, 0.22)
    if not BLOCKOUT and len(sides) == 2:
        fx = -RW/2
        while fx < RW/2 - 0.05:
            L_ = min(0.40, RW/2 - fx)
            a = ridge + ex*fx + up*(TV + 0.04)
            tiles.cyl(a, a + ex*(L_ - 0.01), 0.12, seg=12, var=rnd.random(), moss=U(0.0, 0.5))
            fx += 0.37
        vol.cube(frame_m(ridge + up*(TV + 0.025), ex*RW, (R3 @ Vector((0, 1, 0)))*0.34, up*0.05), m="mortier")
    ch.beam(ridge - ex*(RW/2 - 0.1) - up*0.12, ridge + ex*(RW/2 - 0.1) - up*0.12, 0.14, 0.18) if (rafters and not BLOCKOUT and len(sides) == 2) else None
    out = dict(zr=zr, Ls=Ls, tiles=tiles, vol=vol, ch=ch)
    return out

L_ = P.LUC
def z_main_front(y): return P.Z_SABL + (y - P.ROOF_B_Y0)*T48
def z_dorm(x): return L_["z_ridge"] - abs(x - L_["xc"])*math.tan(math.radians(L_["pitch"]))
Y_CHEEK = P.ROOF_B_Y0 + (L_["z_eave"] - P.Z_SABL)/T48
def skip_main(p, rafter=False):
    if p.y < P.RIDGE_B_Y and abs(p.x - L_["xc"]) < L_["w"]/2 + (0.10 if not rafter else 0.05) and p.y > L_["y_face"] - 0.08:
        return p.z < z_dorm(p.x) + 0.07
    return False
CH = P.CHEM
def skip_four(p, rafter=False):
    return CH["x0"] - 0.10 < p.x < CH["x1"] + 0.10 and CH["y0"] - 0.10 < p.y < CH["y1"] + 0.25

rb = roof("Toit_Boutique", "Toitures", P.ROOF_B_X0, P.ROOF_B_X1, P.ROOF_B_Y0, P.ROOF_B_Y1, P.Z_SABL, P.PITCH_B, P.ROOF_B_OVER, skip=skip_main)
rf = roof("Toit_Fournil", "Toitures", P.ROOF_F_X0, P.ROOF_F_X1, P.FOURNIL["y0"], P.FOURNIL["y1"], P.F_EAVE, P.PITCH_F, P.ROOF_F_OVER,
          skip=skip_four, rives=(False, True), moss=0.6)
Md = Matrix.Translation((L_["xc"], 0, 0)) @ Matrix.Rotation(math.pi/2, 4, "Z")
def skip_dorm(p, rafter=False): return p.z < z_main_front(p.y) + TV + 0.02
rl = roof("Lucarne", "Toitures", L_["y_face"] - 0.24, L_["y_back"] - 0.02, -L_["w"]/2, L_["w"]/2, L_["z_eave"], L_["pitch"], 0.16,
          M=Md, skip=skip_dorm, rives=(True, False), rafters=False, moss=0.2)
# noues de plomb entre la lucarne et le grand pan (recouvrent les coupes des tuiles)
if not BLOCKOUT:
    nq = Pack("Lucarne_Noues", ["plomb"], "Toitures")
    t50 = math.tan(math.radians(L_["pitch"]))
    def valley(d):
        z = L_["z_ridge"] - d*t50
        return P.ROOF_B_Y0 + (z - P.Z_SABL)/T48, z
    n_main = Vector((0, -math.sin(math.radians(48)), math.cos(math.radians(48))))
    for sgn in (-1, 1):
        n_d = Vector((sgn*math.sin(math.radians(L_["pitch"])), 0, math.cos(math.radians(L_["pitch"]))))
        na = (n_main + n_d).normalized()
        d0, d1 = 0.0, L_["w"]/2 + 0.16*math.cos(math.radians(L_["pitch"]))
        y0, z0 = valley(d0); y1, z1 = valley(d1)
        A = Vector((L_["xc"] + sgn*d0, y0, z0)); B_ = Vector((L_["xc"] + sgn*d1, y1, z1))
        ex_ = (B_ - A).normalized(); ey_ = na.cross(ex_).normalized()
        c = (A + B_)/2 + na*(TV + 0.035)
        nq.cube(frame_m(c, ex_*((B_ - A).length + 0.12), ey_*0.30, na*0.012))
    tag(nq.done(bev=0.004))

# appentis du cul-de-four (un seul pan)
app_tiles = Pack("Toit_Appentis_Tuiles", ["tuiles"], "Toitures", attrs=("var", "moss"))
app_vol = Pack("Toit_Appentis_Voliges", ["planche"], "Toitures")
pa_ = math.atan2(P.APP_Z0 - P.APP_Z1, P.APP_Y1 - P.APP_Y0)
dsl_a = Vector((0, math.cos(pa_), -math.sin(pa_))); nrm_a = Vector((0, math.sin(pa_), math.cos(pa_)))
RWa = P.APP_X1 - P.APP_X0; Lsa = math.hypot(P.APP_Y1 - P.APP_Y0, P.APP_Z0 - P.APP_Z1) + 0.25
ridge_a = Vector(((P.APP_X0 + P.APP_X1)/2, P.APP_Y0, P.APP_Z0))
app_vol.cube(frame_m(ridge_a + dsl_a*(Lsa/2) + nrm_a*(TV/2), Vector((RWa, 0, 0)), dsl_a*Lsa, nrm_a*TV))
if not BLOCKOUT:
    pan(app_tiles, ridge_a, Vector((1, 0, 0)), dsl_a, nrm_a, RWa, Lsa, None, 0.5)
    for xe in (P.APP_X0 - 0.02, P.APP_X1 + 0.02):
        pr = Vector((xe, P.APP_Y0, P.APP_Z0)) + nrm_a*(TV + 0.03); app_vol.beam(pr, pr + dsl_a*Lsa, 0.035, 0.2)
    ac = Pack("Toit_Appentis_Charpente", ["chene"], "Toitures")
    yp = 10.85
    zp = P.APP_Z0 - (yp - P.APP_Y0)*math.tan(pa_) - 0.20
    for xp in (P.APP_X0 + 0.12, P.APP_X1 - 0.12):
        ac.box(xp - 0.08, xp + 0.08, yp - 0.08, yp + 0.08, 0.0, zp)
        ac.box(xp - 0.13, xp + 0.13, yp - 0.13, yp + 0.13, -0.02, 0.12, m="taille")
        ac.beam((xp, yp, zp - 0.55), (xp + (0.5 if xp < 12 else -0.5), yp, zp), 0.10, 0.12)
    ac.box(P.APP_X0, P.APP_X1, yp - 0.08, yp + 0.08, zp, zp + 0.18)
    ac.box(P.APP_X0, P.APP_X1, P.APP_Y0, P.APP_Y0 + 0.14, P.APP_Z0 - 0.32, P.APP_Z0 - 0.12)
    x = P.APP_X0 + 0.2
    while x < P.APP_X1:
        if not skip_four(Vector((x, 8.05, 3.0))):
            ac.beam(Vector((x, P.APP_Y0, P.APP_Z0)) - nrm_a*0.065, Vector((x, P.APP_Y0, P.APP_Z0)) - nrm_a*0.065 + dsl_a*(Lsa - 0.1), 0.08, 0.12)
        x += 0.6
    tag(ac.done(bev=0.01))
# solin de mortier le long du mur séparatif (toit du fournil)
so_ = Pack("Toit_Fournil_Solin", ["mortier"], "Toitures")
for s in (-1, 1):
    a = Vector((10.0, 4.0, P.RIDGE_F_Z + TV + 0.02)); dsl = Vector((0, s*math.cos(math.radians(30)), -math.sin(math.radians(30))))
    so_.beam(a + Vector((0.05, 0, 0)), a + Vector((0.05, 0, 0)) + dsl*(4.0/math.cos(math.radians(30)) + P.ROOF_F_OVER), 0.10, 0.08)
tag(so_.done())
for r_ in (rb, rf, rl):
    for key in ("tiles", "vol", "ch"):
        pk = r_[key]
        if pk.n: r_[key + "_o"] = tag(pk.done(bev=0.006 if key == "ch" else 0.0, recalc=(key != "tiles")))
        else: pk.bm.free()
tag(app_vol.done())
if app_tiles.n: tag(app_tiles.done(recalc=False))
else: app_tiles.bm.free()
# trous dans les voliges : lucarne et cheminée
cpl = Pack("_c", ["mortier"], "Toitures")
cpl.box(L_["xc"] - L_["w"]/2 + 0.08, L_["xc"] + L_["w"]/2 - 0.08, L_["y_face"] + 0.05, Y_CHEEK - 0.05, 5.0, 9.5)
boolean_cut(rb["vol_o"], cpl.done())
cpf = Pack("_c", ["mortier"], "Toitures"); cpf.box(CH["x0"] - 0.03, CH["x1"] + 0.03, CH["y0"] - 0.03, CH["y1"] + 0.3, 2.5, 7.0)
boolean_cut(rf["vol_o"], cpf.done())
print("toits", round(time.time() - T0), "s  faîtage géométrique", round(rb["zr"], 3), flush=True)

# lucarne : façade à pans de bois, jouées enduites
xl0, xl1 = L_["xc"] - L_["w"]/2, L_["xc"] + L_["w"]/2
lf = Pack("Lucarne_Facade", ["enduit"], "Toitures")
lpoly = [(xl0, L_["z_face"] - 0.06), (xl1, L_["z_face"] - 0.06), (xl1, L_["z_eave"]), (L_["xc"], L_["z_ridge"] - 0.05), (xl0, L_["z_eave"])]
lf.prism([Vector((a, L_["y_face"] + 0.03, z)) for a, z in lpoly], Vector((0, 0.14, 0)))
for xj in (xl0, xl1 - 0.08):
    lf.prism([Vector((xj, L_["y_face"] + 0.03, L_["z_face"] - 0.06)), Vector((xj, L_["y_face"] + 0.03, L_["z_eave"])),
              Vector((xj, Y_CHEEK + 0.05, L_["z_eave"] - 0.02)), Vector((xj, Y_CHEEK + 0.05, L_["z_eave"] - 0.08))], Vector((0.08, 0, 0)))
lo = lf.done()
lw = dict(a0=L_["win"][0], a1=L_["win"][1], z0=L_["z_win"][0], z1=L_["z_win"][1])
cpw = Pack("_c", ["mortier"], "Toitures"); cpw.prism([Vector((a, L_["y_face"] - 0.2, z)) for a, z in open_poly(lw)], Vector((0, 0.6, 0)))
boolean_cut(lo, cpw.done())
assign(lo, lambda c, n: "enduit" if (n.y < -0.7 or abs(n.x) > 0.7) else "enduit_int")
tag(lo, lucarne_id="L1")
if not BLOCKOUT:
    lk = Pack("Lucarne_PansDeBois", ["chene"], "Toitures")
    yf = L_["y_face"]
    lk.box(xl0, xl0 + 0.13, yf, yf + 0.11, L_["z_face"] - 0.05, L_["z_eave"]); lk.box(xl1 - 0.13, xl1, yf, yf + 0.11, L_["z_face"] - 0.05, L_["z_eave"])
    lk.box(xl0, xl1, yf - 0.02, yf + 0.13, lw["z0"] - 0.12, lw["z0"])
    lk.box(xl0, xl1, yf, yf + 0.11, lw["z1"], lw["z1"] + 0.12)
    lk.box(xl0 - 0.03, xl1 + 0.03, yf - 0.01, yf + 0.12, L_["z_eave"] - 0.14, L_["z_eave"])
    lk.box(L_["xc"] - 0.06, L_["xc"] + 0.06, yf, yf + 0.11, L_["z_eave"], L_["z_ridge"] - 0.15)
    tag(lk.done(bev=0.01))
print("lucarne", round(time.time() - T0), "s", flush=True)

# =====================================================================
# 5. Cheminée du four et hotte
# =====================================================================
H = CH["hotte"]
ho = Pack("Four_Hotte", ["enduit_fournil"], "Four")
ho.hull([(H["x0"], H["y0"], H["z0"]), (H["x1"], H["y0"], H["z0"]), (H["x0"], H["y1"], H["z0"]), (H["x1"], H["y1"], H["z0"]),
         (CH["x0"], CH["y0"], H["z1"]), (CH["x1"], CH["y0"], H["z1"]), (CH["x0"], H["y1"], H["z1"]), (CH["x1"], H["y1"], H["z1"])])
ho.box(CH["x0"] + 0.02, CH["x1"] - 0.02, CH["y0"] + 0.02, H["y1"], H["z1"] - 0.05, 3.9)
if not BLOCKOUT:
    ho.box(H["x0"] - 0.08, H["x1"] + 0.08, H["y0"] - 0.10, H["y0"] + 0.06, H["z0"] - 0.22, H["z0"], m="chene")     # manteau
    for xs in (H["x0"] - 0.02, H["x1"] - 0.12):
        ho.box(xs, xs + 0.14, H["y0"] - 0.06, H["y0"] + 0.10, H["z0"] - 0.55, H["z0"] - 0.22, m="taille")            # corbeaux
tag(ho.done(bev=0.008), cheminee_id="C1")
cs = Pack("Four_Cheminee", ["moellon"], "Four")
z = H["z1"] - 0.05; k = 0
if BLOCKOUT:
    cs.box(CH["x0"], CH["x1"], CH["y0"], CH["y1"], 3.4, CH["z_top"]); z = CH["z_top"]
while z < CH["z_top"] - 0.05 and not BLOCKOUT:
    h = min(U(0.2, 0.28), CH["z_top"] - z)
    xm_, ym_ = (CH["x0"] + CH["x1"])/2 + U(-0.12, 0.12), (CH["y0"] + CH["y1"])/2 + U(-0.12, 0.12)
    if k % 2 == 0:
        cs.box(CH["x0"], xm_ - 0.006, CH["y0"], CH["y1"], z + 0.006, z + h - 0.006, jitter=0.008)
        cs.box(xm_ + 0.006, CH["x1"], CH["y0"], CH["y1"], z + 0.006, z + h - 0.006, jitter=0.008)
    else:
        cs.box(CH["x0"], CH["x1"], CH["y0"], ym_ - 0.006, z + 0.006, z + h - 0.006, jitter=0.008)
        cs.box(CH["x0"], CH["x1"], ym_ + 0.006, CH["y1"], z + 0.006, z + h - 0.006, jitter=0.008)
    z += h; k += 1
if not BLOCKOUT:
    cs.box(CH["x0"] + 0.03, CH["x1"] - 0.03, CH["y0"] + 0.03, CH["y1"] - 0.03, H["z1"], CH["z_top"], m="mortier")
    cs.box(CH["x0"] - 0.09, CH["x1"] + 0.09, CH["y0"] - 0.09, CH["y1"] + 0.09, CH["z_top"], CH["z_top"] + 0.10, m="taille")
    zt = CH["z_top"] + 0.10; xm_ = (CH["x0"] + CH["x1"])/2
    for s in (-1, 1):
        cs.beam((xm_ + s*0.30, CH["y0"] + 0.1, zt), (xm_ + s*0.30, CH["y1"] - 0.1, zt), 0.06, 0.20, m="taille") if False else None
        cs.cube(frame_m(Vector((xm_ + s*0.17, (CH["y0"] + CH["y1"])/2, zt + 0.17)), Vector((math.cos(math.radians(55))*0.42, 0, -s*math.sin(math.radians(55))*0.42)),
                        Vector((0, CH["y1"] - CH["y0"] - 0.05, 0)), Vector((s*math.sin(math.radians(55))*0.025, 0, math.cos(math.radians(55))*0.025))), m="tuiles")
        cs.box(xm_ + s*0.30 - 0.06, xm_ + s*0.30 + 0.06, CH["y0"] + 0.05, CH["y1"] - 0.05, zt, zt + 0.06, m="taille")
    cs.box(CH["x0"] + 0.12, CH["x1"] - 0.12, CH["y0"] + 0.12, CH["y1"] - 0.12, zt - 0.002, zt + 0.004, m="suie")
o = cs.done(bev=0.015); tag(o, cheminee_id="C1")
print("cheminée", round(time.time() - T0), "s", flush=True)

# =====================================================================
# 6. Menuiseries
# =====================================================================
def fenetre(name, ax, n_c, a0, a1, z0, z1, glass="vitrail", bars=False, croisee=True, cname="Menuiseries"):
    pk = Pack(name, ["chene", glass], cname)
    def Bx(b0, b1, zz0, zz1, d0, d1, m=0):
        if ax == "y": pk.box(b0, b1, n_c + d0, n_c + d1, zz0, zz1, m=m)
        else: pk.box(n_c + d0, n_c + d1, b0, b1, zz0, zz1, m=m)
    f = 0.06
    Bx(a0, a1, z0, z0 + f, -0.04, 0.04); Bx(a0, a1, z1 - f, z1, -0.04, 0.04)
    Bx(a0, a0 + f, z0, z1, -0.04, 0.04); Bx(a1 - f, a1, z0, z1, -0.04, 0.04)
    if croisee and a1 - a0 > 0.55:
        m_ = (a0 + a1)/2; Bx(m_ - 0.035, m_ + 0.035, z0, z1, -0.035, 0.035)
    if croisee and z1 - z0 > 0.8:
        zt = z0 + (z1 - z0)*0.66; Bx(a0, a1, zt - 0.035, zt + 0.035, -0.035, 0.035)
    Bx(a0 + 0.01, a1 - 0.01, z0 + 0.01, z1 - 0.01, -0.006, 0.006, m=1)
    if bars:
        k = 0; a = a0 + 0.12
        while a < a1 - 0.08:
            if ax == "y": pk.box(a - 0.014, a + 0.014, n_c - 0.13, n_c - 0.10, z0 - 0.02, z1 + 0.02, m="fer")
            else: pk.box(n_c + 0.10, n_c + 0.13, a - 0.014, a + 0.014, z0 - 0.02, z1 + 0.02, m="fer")
            a += 0.12
    return tag(pk.done(bev=0.006), "statique")

def leaf(pk, w, h, planks=6, t=0.055, glazed=None, nails=True, strap=0.75, sign=1):
    """Battant de planches dans un repère local : charnière en x = 0, largeur vers +x, face extérieure en y = 0 (vers -y)."""
    for i in range(planks):
        u0 = i*w/planks
        pk.box(u0 + 0.003, u0 + w/planks - 0.003, 0.0, t, 0.0, h, m="planche", var=rnd.random())
    for zz in (0.25, h/2 - 0.07, h - 0.40):
        pk.box(0.05, w - 0.05, t, t + 0.035, zz, zz + 0.14, m="planche")
        pk.box(0.0, w*strap, -0.010, 0.0, zz + 0.035, zz + 0.10, m="fer")
        pk.cyl((0.0, -0.01, zz + 0.067), (0.0, 0.07, zz + 0.067), 0.025, m="fer", seg=8)
        if nails:
            for i in range(planks):
                u = (i + 0.5)*w/planks
                pk.box(u - 0.011, u + 0.011, -0.020, 0.0, zz + 0.056, zz + 0.079, m="fer")
    if glazed:
        gx0, gx1, gz0, gz1 = glazed
        pk.box(gx0 - 0.04, gx1 + 0.04, -0.012, 0.0, gz0 - 0.04, gz1 + 0.04, m="chene")
        pk.box(gx0, gx1, t*0.5 - 0.004, t*0.5 + 0.004, gz0, gz1, m="vitrail")
        for g in (gx0 + (gx1 - gx0)/3, gx0 + 2*(gx1 - gx0)/3):
            pk.box(g - 0.008, g + 0.008, -0.03, -0.012, gz0, gz1, m="fer")
    pk.tube([(w - 0.14, -0.055, h*0.48 - 0.06), (w - 0.14, -0.055 - 0.05, h*0.48), (w - 0.14, -0.055, h*0.48 + 0.06)], 0.010, m="fer", nv=6)

def placed(pk_src_fn, name, M, cname="Menuiseries", role="interactif", **kw):
    pk = Pack(name, ["planche"], cname)
    pk_src_fn(pk)
    bmesh.ops.transform(pk.bm, matrix=M, verts=pk.bm.verts[:])
    return tag(pk.done(bev=0.004), role, **kw)

if not BLOCKOUT:
    p1 = P.ouv("P1"); yd = 0.30
    # porte principale : battant ouvert vers l'intérieur (70°), petite vitre et imposte vitrée sous l'arc
    placed(lambda pk: leaf(pk, p1["a1"] - p1["a0"] - 0.05, p1["z1"] - p1["z0"] - 0.01, 6, glazed=(0.42, 0.74, 1.42, 1.66)),
           "Menuiserie_P1_Battant", Matrix.Translation((p1["a0"] + 0.03, yd, p1["z0"])) @ Matrix.Rotation(math.radians(88), 4, "Z"), ouverture_id="P1")
    dm = Pack("Menuiserie_P1_Dormant", ["chene", "vitrail"], "Menuiseries")
    dm.box(p1["a0"] - 0.02, p1["a0"] + 0.05, yd - 0.04, yd + 0.10, p1["z0"], p1["z1"]); dm.box(p1["a1"] - 0.05, p1["a1"] + 0.02, yd - 0.04, yd + 0.10, p1["z0"], p1["z1"])
    dm.box(p1["a0"], p1["a1"], yd - 0.04, yd + 0.10, p1["z1"], p1["z1"] + 0.08)
    imp = [(a, z) for a, z in arch_pts(p1["a0"], p1["a1"], p1["z1"], p1["z_cle"])]
    dm.prism([Vector((a, yd + 0.025, max(z, p1["z1"] + 0.08))) for a, z in imp], Vector((0, 0.012, 0)), m=1)
    for xg in (p1["a0"] + 0.4, p1["a1"] - 0.4):
        dm.box(xg - 0.025, xg + 0.025, yd, yd + 0.06, p1["z1"] + 0.08, p1["z_cle"] - 0.02)
    dm.box(p1["a0"] - 0.10, p1["a1"] + 0.10, -0.10, W, -0.02, p1["z0"], m="taille")              # seuil
    tag(dm.done(bev=0.005), ouverture_id="P1")
    # double porte du fournil (fermée), tympan de planches sous l'arc
    pf = P.ouv("PF"); yf_ = 0.22; wl = (pf["a1"] - pf["a0"])/2 - 0.02
    placed(lambda pk: leaf(pk, wl, pf["z1"] - pf["z0"], 4, strap=0.8), "Menuiserie_PF_BattantGauche",
           Matrix.Translation((pf["a0"] + 0.02, yf_, pf["z0"])), ouverture_id="PF")
    placed(lambda pk: leaf(pk, wl, pf["z1"] - pf["z0"], 4, strap=0.8), "Menuiserie_PF_BattantDroit",
           Matrix.Translation((pf["a1"] - 0.02, yf_, pf["z0"])) @ Matrix.Scale(-1, 4, (1, 0, 0)), ouverture_id="PF")
    tp = Pack("Menuiserie_PF_Tympan", ["planche", "chene"], "Menuiseries")
    pts = arch_pts(pf["a0"], pf["a1"], pf["z1"], pf["z_cle"], 16)
    tp.prism([Vector((a, yf_, max(z, pf["z1"]))) for a, z in pts], Vector((0, 0.05, 0)))
    tp.box(pf["a0"], pf["a1"], yf_ - 0.02, yf_ + 0.10, pf["z1"] - 0.02, pf["z1"] + 0.10, m=1)
    tp.box(pf["a0"] - 0.08, pf["a1"] + 0.08, -0.08, W, -0.02, pf["z0"], m="taille")
    tag(tp.done(bev=0.004), ouverture_id="PF")
    # porte de la cour (fermée)
    r2 = P.ouv("R2")
    placed(lambda pk: leaf(pk, r2["a1"] - r2["a0"] - 0.04, r2["z1"] - r2["z0"] - 0.01, 5), "Menuiserie_R2_Porte",
           Matrix.Translation((r2["a1"] - 0.02, 8.5 - 0.22, r2["z0"])) @ Matrix.Rotation(math.pi, 4, "Z"), ouverture_id="R2")
    sr = Pack("Menuiserie_R2_Seuil", ["taille"], "Menuiseries"); sr.box(r2["a0"] - 0.08, r2["a1"] + 0.08, 8.0, 8.62, -0.02, P.SOL); tag(sr.done(bev=0.01))
    # vitrine à petits carreaux (petits bois de chêne), poitrail de chêne
    v1 = P.ouv("V1"); yv = 0.14
    vt = Pack("Menuiserie_V1_Vitrine", ["chene", "verre"], "Menuiseries")
    a0, a1, z0, z1 = v1["a0"], v1["a1"], v1["z0"], v1["z1"]
    vt.box(a0, a1, yv - 0.05, yv + 0.06, z0, z0 + 0.09); vt.box(a0, a1, yv - 0.05, yv + 0.06, z1 - 0.07, z1)
    nb = 3
    for i in range(nb + 1):
        x = a0 + i*(a1 - a0)/nb; hw = 0.05 if i in (0, nb) else 0.045
        vt.box(max(a0, x - hw), min(a1, x + hw), yv - 0.05, yv + 0.06, z0, z1)
    for i in range(nb):
        b0, b1 = a0 + i*(a1 - a0)/nb + 0.045, a0 + (i + 1)*(a1 - a0)/nb - 0.045
        for k in range(1, 5):
            x = b0 + k*(b1 - b0)/5; vt.box(x - 0.016, x + 0.016, yv - 0.02, yv + 0.025, z0 + 0.09, z1 - 0.07)
        for k in range(1, 5):
            zz = z0 + 0.09 + k*(z1 - 0.07 - z0 - 0.09)/5; vt.box(b0, b1, yv - 0.02, yv + 0.025, zz - 0.016, zz + 0.016)
    vt.box(a0 + 0.02, a1 - 0.02, yv - 0.004, yv + 0.004, z0 + 0.05, z1 - 0.04, m=1)
    vt.box(a0 - 0.22, a1 + 0.22, -0.025, W, z1, z1 + 0.27)                                         # poitrail
    for x in (a0 - 0.12, a1 + 0.12):
        vt.cyl((x, -0.03, z1 + 0.135), (x, 0.0, z1 + 0.135), 0.022, m="fer", seg=8)
    tag(vt.done(bev=0.006), ouverture_id="V1")
    # fenêtres
    g1, r1, fd1 = P.ouv("G1"), P.ouv("R1"), P.ouv("FD1")
    tag(fenetre("Menuiserie_G1_Fenetre", "x", 0.18, g1["a0"], g1["a1"], g1["z0"], g1["z1"]), ouverture_id="G1")
    tag(fenetre("Menuiserie_R1_Fenetre", "y", 8.5 - 0.18, r1["a0"], r1["a1"], r1["z0"], r1["z1"]), ouverture_id="R1")
    tag(fenetre("Menuiserie_FD1_Fenetre", "x", 14.5 - 0.30, fd1["a0"], fd1["a1"], fd1["z0"], fd1["z1"], bars=True), ouverture_id="FD1")
    for i, ax, nc in (("E1", "y", -P.ENC + 0.10), ("E2", "y", -P.ENC + 0.10), ("EG1", "x", 0.10), ("EA1", "y", 8.5 - 0.10), ("EA2", "y", 8.5 - 0.10)):
        o_ = P.ouv(i); tag(fenetre(f"Menuiserie_{i}_Fenetre", ax, nc, o_["a0"], o_["a1"], o_["z0"], o_["z1"]), ouverture_id=i)
    tag(fenetre("Menuiserie_L1_Fenetre", "y", L_["y_face"] + 0.09, lw["a0"], lw["a1"], lw["z0"], lw["z1"], croisee=True), ouverture_id="L1")
    # volets vert sauge des deux fenêtres d'étage (ouverts contre le mur)
    for i in ("E1", "E2"):
        o_ = P.ouv(i); vk = Pack(f"Menuiserie_{i}_Volets", ["volet", "fer"], "Menuiseries")
        yb = -P.ENC - 0.008
        for b0, b1 in ((o_["a0"] - 0.47, o_["a0"] - 0.02), (o_["a1"] + 0.02, o_["a1"] + 0.47)):
            for k in range(3):
                pa = b0 + k*(b1 - b0)/3
                vk.box(pa + 0.003, pa + (b1 - b0)/3 - 0.003, yb - 0.035, yb, o_["z0"] - 0.02, o_["z1"] + 0.02, var=rnd.random())
            for zz in (o_["z0"] + 0.12, o_["z1"] - 0.2):
                vk.box(b0 + 0.03, b1 - 0.03, yb - 0.06, yb - 0.035, zz, zz + 0.10)
                vk.box(b0 + 0.01, b1 - 0.01, yb - 0.068, yb - 0.06, zz + 0.03, zz + 0.07, m=1)
            vk.beam((b0 + 0.06, yb - 0.048, o_["z0"] + 0.22), (b1 - 0.06, yb - 0.048, o_["z1"] - 0.2), 0.025, 0.09)
            xh = b0 + 0.02 if b0 < o_["a0"] else b1 - 0.02
            vk.cyl((xh, yb - 0.09, (o_["z0"] + o_["z1"])/2), (xh, yb + 0.01, (o_["z0"] + o_["z1"])/2), 0.010, m=1, seg=6)
        tag(vk.done(bev=0.004), "decoratif", ouverture_id=i)
    # linteaux de chêne de la baie de passage
    b1_ = P.ouv("B1"); bl = Pack("Menuiserie_B1_Linteau", ["chene"], "Menuiseries")
    bl.box(9.47, 10.03, b1_["a0"] - 0.25, b1_["a1"] + 0.25, b1_["z1"], b1_["z1"] + 0.24)
    bl.box(9.5, 10.0, b1_["a0"] - 0.03, b1_["a1"] + 0.03, -0.02, P.SOL, m="taille")
    tag(bl.done(bev=0.008), ouverture_id="B1")
print("menuiseries", round(time.time() - T0), "s", flush=True)

sc["bf_source"] = "Boulangerie_Firmin"
save_blend(os.path.join(WORK, "00_blockout.blend" if BLOCKOUT else "A_archi.blend"))
print("FIN étape A", round(time.time() - T0), "s", flush=True)
