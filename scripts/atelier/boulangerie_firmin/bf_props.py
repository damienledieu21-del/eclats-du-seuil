# Accessoires réutilisables : pains (pas de baguette), corbeilles, sacs « Farine », tonneaux, caisses, lanternes, bûches,
# pelles, fleurs. Chaque fonction ajoute des pièces dans un Pack (un objet par accessoire ou par groupe).
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler
from bf_lib import Pack, U, rnd, rv, text_mesh, frame_m, coll

def rz(a): return Matrix.Rotation(a, 4, "Z")

# ---------------------------------------------------------------- pains (tour 3 : bibliothèque de variantes)
# Chaque type de pain a quelques variantes, construites une fois en vraie géométrie (grignes creusées, lèvres
# soulevées, boules soudées, feuilletage), puis recopiées dans le lot avec une variation de taille, d'aplatissement,
# d'orientation et de cuisson (attribut "var" : 0 = bien cuit, 1 = blond, lu par les matières et par l'export).
import random as _random, zlib
from mathutils import noise as _noise
_LIB = {}
QUALITE = [1]                    # 1 = détail (premier plan), 0 = léger (étagères du fond, rayons lointains)
STATS = {}
_LAST = {}
def set_qualite(q): QUALITE[0] = q

def _cuisson():                  # du blond au bien cuit, la plupart dorés
    return max(0.0, rnd.random()**1.15)      # tour 3 bis : large étalement, du pâle doré au brun foncé

class _Var:
    """Variante de bibliothèque : sommets locaux (pain posé à l'origine), faces, clé de matière par face."""
    def __init__(self): self.co, self.faces, self.mk, self.sm, self.g = [], [], [], [], []
    def v(self, p, g=0.0): self.co.append(Vector(p)); self.g.append(g); return len(self.co) - 1
    def f(self, idx, m, smooth=True): self.faces.append(tuple(idx)); self.mk.append(m); self.sm.append(smooth)

def _place(pk, var, M, cuis, kind):
    gl = pk.bm.verts.layers.float.get("grigne") or pk.bm.verts.layers.float.new("grigne")
    fp = pk.bm.faces.layers.int.get("est_pain") or pk.bm.faces.layers.int.new("est_pain")
    vs = [pk.bm.verts.new(M @ c) for c in var.co]
    for v_, g in zip(vs, var.g): v_[gl] = g
    flip = M.to_3x3().determinant() < 0
    lay = pk.lay.get("var")
    for idx, m, s in zip(var.faces, var.mk, var.sm):
        try: f = pk.bm.faces.new([vs[i] for i in (reversed(idx) if flip else idx)])
        except ValueError: continue
        f.material_index = pk.mi(m); f.smooth = s; f[fp] = 1
        if lay is not None: f[lay] = cuis
    pk.n += 1
    STATS[kind] = STATS.get(kind, 0) + 1

def _cut_field(X, Y, cuts):
    """Grignes : (creux, décalage XY de la lèvre, hauteur de la lèvre). cuts : (p0, p1, largeur, profondeur, lèvre)."""
    g_tot, dz, dxy = 0.0, 0.0, Vector((0, 0))
    for (p0, p1, w, d, lip) in cuts:
        p0, p1 = Vector(p0), Vector(p1); t_ = p1 - p0; L = t_.length; t_ /= L
        n_ = Vector((-t_.y, t_.x)); q = Vector((X, Y)) - p0
        t = q.dot(t_)/L; s = q.dot(n_)
        if t <= 0.0 or t >= 1.0: continue
        tap = math.sin(math.pi*t)**0.6
        g = math.exp(-(s/w)**2)*tap
        l = lip*math.exp(-((s - 1.25*w)/(0.9*w))**2)*tap
        dz += -d*g + l
        dxy -= n_*(0.9*l)                       # la lèvre se soulève et avance au-dessus du creux
        g_tot = max(g_tot, g)
    return g_tot, dz, dxy

def _dome(nx, ny, rx, ry, h, cuts=(), bump=0.05, seed=0, crust="pain", foot=0.07, bend=0.0, rim_wave=0.0):
    """Pain en dôme : grille carrée projetée sur un disque elliptique (toutes les mailles servent), profil à pied
    rentré, grignes déformées en vraie géométrie, dessous plat. Le creux porte l'attribut de sommet "grigne"
    (0 à 1, interpolé) que la croûte lit pour éclaircir la mie dorée de l'entaille, sans farine."""
    V = _Var(); grid = {}; gval = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = -1 + 2*i/nx, -1 + 2*j/ny
            x = u*math.sqrt(max(0.0, 1 - v*v/2)); y = v*math.sqrt(max(0.0, 1 - u*u/2))
            r = min(1.0, math.hypot(x, y))
            z = h*(max(0.0, 1 - r**2.4))**0.42
            k = 1 - foot*(1 - z/h)**8 if h > 0 else 1
            X, Y = x*rx*k, y*ry*k
            if rim_wave: X += x*rx*rim_wave*math.cos(math.atan2(y, x)*28)*(r**6); Y += y*ry*rim_wave*math.cos(math.atan2(y, x)*28)*(r**6)
            nz = _noise.noise(Vector((X*9 + seed, Y*9, seed*0.37)))
            z *= 1 + bump*nz
            g, dz, dxy = _cut_field(X, Y, cuts)
            z = max(0.0, z + dz*min(1.0, z/(0.35*h + 1e-6))); X += dxy.x; Y += dxy.y
            if bend: Y += bend*(X/rx)**2*ry
            grid[i, j] = V.v((X, Y, z), g); gval[i, j] = g
    for i in range(nx):
        for j in range(ny):
            q = (grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1])
            gm = (gval[i, j] + gval[i + 1, j] + gval[i + 1, j + 1] + gval[i, j + 1])/4
            V.f(q, crust)
    return V                                  # pas de dessous : le pain est toujours posé (triangles économisés)

def _weld(V):
    """Soude les sommets confondus (pôles, anneaux de rayon nul) et retire les faces d'aire nulle."""
    idx, new = {}, _Var(); remap = []
    for c, g in zip(V.co, V.g):
        k = (round(c.x, 6), round(c.y, 6), round(c.z, 6))
        if k not in idx: idx[k] = new.v(c, g)
        remap.append(idx[k])
    for f, m, sm in zip(V.faces, V.mk, V.sm):
        r = []
        for i in f:
            j = remap[i]
            if j not in r: r.append(j)
        if len(r) < 3: continue
        pts = [new.co[i] for i in r]
        area = sum(((pts[i] - pts[0]).cross(pts[i + 1] - pts[0])).length/2 for i in range(1, len(pts) - 1))
        if area < 1e-8: continue
        new.f(r, m, sm)
    return new

def _lib(kind, q, k, build):
    key = (kind, q, k)
    if key not in _LIB:
        _LIB[key] = _weld(build(_random.Random(zlib.crc32(repr(key).encode()))))
    return _LIB[key]

def _pick(kind, n, build):
    q = QUALITE[0]; k = rnd.randrange(n)
    if n > 1 and _LAST.get(kind) == k: k = (k + 1 + rnd.randrange(n - 1)) % n      # jamais deux fois la même variante de suite
    _LAST[kind] = k
    return _lib(kind, q, k, lambda R: build(R, q, k))

def _M(x, y, z, a, sx, sy, sz, tilt=0.0):
    return Matrix.Translation((x, y, z)) @ rz(a) @ Matrix.Rotation(tilt, 4, "X") @ Matrix.Diagonal((sx, sy, sz, 1))

# ---- miche ronde : grigne en croix, en carré, en croix décentrée ou à trois coups parallèles
def _b_miche(R, q, k):
    n = 14 if q else 10
    w = 0.011; d = 0.030; lip = 0.016
    J = lambda: R.uniform(-0.012, 0.012)
    if k == 0:   cuts = [((-0.085, J()), (0.085, J()), w, d, lip), ((J(), 0.085), (J(), -0.085), w, d, lip)]
    elif k == 1:
        a = 0.050
        cuts = [((-a - 0.02, -a), (a + 0.01, -a + J()), w, d, lip), ((a, -a - 0.02), (a + J(), a + 0.01), w, d, lip),
                ((a + 0.02, a), (-a - 0.01, a + J()), w, d, lip), ((-a, a + 0.02), (-a + J(), -a - 0.01), w, d, lip)]
    elif k == 2: cuts = [((-0.09, 0.02), (0.08, 0.035), w*1.1, d*1.1, lip*1.2), ((0.01, 0.09), (0.03, -0.08), w, d, lip)]
    else:        cuts = [((-0.07, y), (0.07, y + J()), w*0.9, d*0.9, lip) for y in (-0.045, 0.0, 0.045)]
    return _dome(n, n, 0.13, 0.13, 0.085, cuts, 0.06, k*3.1)

def miche(pk, x, y, z, r=0.13, h=0.075, m="pain"):
    var = _pick("miche", 4, _b_miche); s = r/0.13
    _place(pk, var, _M(x, y, z, U(0, 6.283), s*U(0.95, 1.05), s*U(0.93, 1.05), (h/0.085)*U(0.85, 1.12)), _cuisson(), "miche")

# ---- bâtard : 3 à 5 grignes en biais qui se chevauchent, légèrement aplati
def _b_batard(R, q, k):
    nc = 3 + k % 3; L = 0.22; rx = 0.085
    nx, ny = (8, 20) if q else (6, 14)
    cuts = []
    for i in range(nc):
        yc = -L*0.70 + (2*L*0.70)*(i + 0.5)/nc
        dy = L*0.70/nc*1.35
        cuts.append(((-0.034, yc - dy), (0.034, yc + dy), 0.009, 0.024, 0.013))
    return _dome(nx, ny, rx, L, 0.068, cuts, 0.05, 7 + k)

def batard(pk, x, y, z, a=0.0, L=0.22, r=0.085, m="pain"):
    var = _pick("batard", 3, _b_batard)
    _place(pk, var, _M(x, y, z, a + U(-0.08, 0.08), (r/0.085)*U(0.95, 1.06), (L/0.22)*U(0.94, 1.05), (r/0.085)*U(0.80, 1.0)), _cuisson(), "batard")

# ---- seigle : plus sombre, bas, croûte craquelée (matière) et quelques fentes irrégulières
def _b_seigle(R, q, k):
    nx, ny = (10, 14) if q else (6, 10)
    cuts = [((R.uniform(-0.05, 0.05), R.uniform(-0.13, 0.13)), (R.uniform(-0.05, 0.05), R.uniform(-0.13, 0.13)), 0.006, 0.010, 0.002) for _ in range(2 + k)]
    cuts = [c for c in cuts if (Vector(c[0]) - Vector(c[1])).length > 0.04]
    return _dome(nx, ny, 0.10, 0.17, 0.062, cuts, 0.16, 20 + k, crust="seigle")

def seigle(pk, x, y, z, a=0.0):
    var = _pick("seigle", 3, _b_seigle)
    _place(pk, var, _M(x, y, z, a + U(-0.1, 0.1), U(0.92, 1.08), U(0.92, 1.08), U(0.85, 1.1)), _cuisson()*0.8, "seigle")

# ---- couronne : vraies boules soudées en anneau (tube balayé renflé à chaque boule, pincé entre deux)
def _b_couronne(R, q, k):
    nb = 7 + k % 3; per = 4 if q else 3; nv = 7 if q else 6
    V = _Var(); N = nb*per; Rr = 0.15; r = 0.055
    sizes = [R.uniform(0.88, 1.10) for _ in range(nb)]
    rings = []
    for i in range(N):
        t = i/N; b = int(t*nb); f = (t*nb) % 1.0
        bulge = 0.66 + 0.34*math.sin(math.pi*f)**0.55
        rad = r*sizes[b]*bulge
        th = 2*math.pi*t
        c = Vector((Rr*math.cos(th), Rr*math.sin(th), 0)); out = Vector((math.cos(th), math.sin(th), 0)); up = Vector((0, 0, 1))
        ring = []
        for j in range(nv):
            a = 2*math.pi*j/nv
            p = c + out*math.cos(a)*rad + up*(math.sin(a)*rad*0.80)
            ring.append(V.v((p.x, p.y, max(0.0, p.z + rad*0.80*0.92))))
        rings.append(ring)
    for i in range(N):
        r0, r1 = rings[i], rings[(i + 1) % N]
        for j in range(nv):
            V.f((r0[j], r1[j], r1[(j + 1) % nv], r0[(j + 1) % nv]), "pain")
    return V

def couronne(pk, x, y, z, R=0.15, r=0.055, m="pain"):
    var = _pick("couronne", 3, _b_couronne); s = R/0.15
    _place(pk, var, _M(x, y, z, U(0, 6.283), s*U(0.96, 1.04), s*U(0.94, 1.04), (r/0.055)*U(0.88, 1.08)), _cuisson(), "couronne")

# ---- brioche à tête : corps cannelé (moule), tête ronde un peu penchée, dorure brillante
def _b_brioche(R, q, k):
    seg = 16 if q else 12; V = _Var()
    prof = [(0.040, 0.0), (0.058, 0.012), (0.068, 0.030), (0.066, 0.046), (0.050, 0.060), (0.026, 0.068), (0.0, 0.070)]
    if not q: prof = [prof[i] for i in (0, 2, 3, 5, 6)]
    flutes = 10
    rings = []
    for (r, z) in prof:
        ring = []
        for j in range(seg):
            a = 2*math.pi*j/seg; fl = 1 + 0.07*abs(math.cos(a*flutes/2))*(1 - z/0.07) if r > 0 else 1
            ring.append(V.v((r*fl*math.cos(a), r*fl*math.sin(a), z)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for j in range(seg):
            V.f((rings[i][j], rings[i][(j + 1) % seg], rings[i + 1][(j + 1) % seg], rings[i + 1][j]), "brioche")
    hs, hr = (8, 6) if q else (6, 4)
    off = Vector((R.uniform(-0.006, 0.006), R.uniform(-0.006, 0.006), 0))
    hv = {}
    for i in range(hr + 1):
        ph = math.pi*i/hr
        for j in range(hs):
            th = 2*math.pi*j/hs
            p = off + Vector((0.034*math.sin(ph)*math.cos(th), 0.034*math.sin(ph)*math.sin(th), 0.088 - 0.030*math.cos(ph)))
            hv[i, j] = V.v(p)
    for i in range(hr):
        for j in range(hs):
            V.f((hv[i, j], hv[i, (j + 1) % hs], hv[i + 1, (j + 1) % hs], hv[i + 1, j]), "brioche")
    return V

def brioche(pk, x, y, z, s=1.0):
    var = _pick("brioche", 2, _b_brioche)
    _place(pk, var, _M(x, y, z, U(0, 6.283), s*U(0.94, 1.06), s*U(0.94, 1.06), s*U(0.92, 1.08)), U(0.35, 0.95), "brioche")

# ---- croissant : corps roulé (bourrelets de feuilletage), pointes effilées et recourbées
def _b_croissant(R, q, k):
    npt, nv = (19, 8) if q else (13, 6)
    V = _Var(); rings = []
    curve = 1.0 + 0.25*k
    for i in range(npt):
        t = -1 + 2*i/(npt - 1)
        ang = t*1.15*curve
        c = Vector((math.sin(ang)*0.07, -math.cos(ang)*0.07 + 0.035, 0))
        taper = max(0.0, 1 - abs(t)**1.8)
        ridge = 1 + 0.16*abs(math.sin(math.pi*3.5*(t + 1)))
        rad = (0.033*taper + 0.004)*ridge
        tan = Vector((math.cos(ang), math.sin(ang), 0)); side = Vector((-tan.y, tan.x, 0)); up = Vector((0, 0, 1))
        zc = rad*0.72 - 0.006*abs(t)**3
        ring = []
        for j in range(nv):
            a = 2*math.pi*j/nv
            p = c + side*math.cos(a)*rad + up*(math.sin(a)*rad*0.72 + zc)
            ring.append(V.v((p.x, p.y, max(0.0, p.z))))
        rings.append(ring)
    for i in range(npt - 1):
        for j in range(nv):
            V.f((rings[i][j], rings[i + 1][j], rings[i + 1][(j + 1) % nv], rings[i][(j + 1) % nv]), "brioche")
    V.f(list(reversed(rings[0])), "brioche"); V.f(rings[-1], "brioche")
    return V

def croissant(pk, x, y, z, a=0.0, s=1.0):
    var = _pick("croissant", 2, _b_croissant)
    _place(pk, var, _M(x, y, z - 0.005*s, a, s*U(0.94, 1.06), s*U(0.94, 1.06), s*U(0.9, 1.1)), U(0.3, 0.9), "croissant")

def paton(pk, x, y, z, r=0.085):
    pk.sphere((x, y, z + 0.035), (r, r*U(0.95, 1.05), 0.045), "mie", 12, 7, 0.003)

def assortiment(pk, x0, x1, y, z, kinds=("miche", "batard", "couronne", "seigle", "batard", "miche")):
    step = (x1 - x0)/len(kinds)
    for i, k in enumerate(kinds):
        x = x0 + (i + 0.5)*step
        if k == "miche": miche(pk, x, y + U(-0.03, 0.03), z, U(0.11, 0.14), U(0.065, 0.085))
        elif k == "batard": batard(pk, x, y + U(-0.03, 0.03), z, U(-0.3, 0.3), U(0.19, 0.24), U(0.078, 0.09))
        elif k == "couronne": couronne(pk, x, y, z, U(0.13, 0.16))
        elif k == "seigle": seigle(pk, x, y, z, U(-0.3, 0.3))
        elif k == "brioche": brioche(pk, x, y, z, U(0.9, 1.1))
        elif k == "croissant": croissant(pk, x, y, z, U(0, 6))

# ---------------------------------------------------------------- vannerie, toile, bois
def corbeille(pk, x, y, z, r=0.2, h=0.10, linge=True):
    pk.cyl((x, y, z), (x, y, z + h), r*0.86, "osier", seg=16, r2=r, caps=False)
    pk.cyl((x, y, z), (x, y, z + 0.012), r*0.86, "osier", seg=16)
    n = 16
    pk.tube([(x + r*math.cos(2*math.pi*i/n), y + r*math.sin(2*math.pi*i/n), z + h) for i in range(n)], 0.014, "osier", nv=6, closed=True)
    for k in (2,):
        rr = r*0.86 + (r - r*0.86)*k/4
        pk.tube([(x + rr*1.003*math.cos(2*math.pi*i/n), y + rr*1.003*math.sin(2*math.pi*i/n), z + h*k/4) for i in range(n)], 0.006, "osier", nv=4, closed=True)
    if linge:
        pk.cyl((x, y, z + h*0.55), (x, y, z + h*0.55 + 0.004), r*0.9, "toile_creme", seg=14)

def _sac_shape(R, nu=26, nv=16):
    """Sac de toile posé : fond tassé et élargi, panse aplatie en coussin, plis verticaux qui convergent vers le col
    froncé, collerette plissée au-dessus du lien. Rayons (X, Y) et hauteur en mètres, origine au sol."""
    H, zc = 0.50, 0.385                      # hauteur totale, hauteur du col (lien)
    prof = []                                # (z, rx, ry, plis)
    for i in range(nv + 1):
        t = i/nv
        if t < 0.70:                         # panse
            s = t/0.70; z = zc*s
            bulge = math.sin(math.pi*(0.18 + 0.82*s))**0.55
            rx = 0.235*(0.92 + 0.10*(1 - s)**3)*(0.30 + 0.70*bulge) if s > 0.25 else 0.235*(0.97 + 0.06*s)
            ry = 0.170*(0.30 + 0.70*bulge) if s > 0.25 else 0.170*(0.94 + 0.08*s)
            if s < 0.06: rx *= 0.93 + s; ry *= 0.93 + s        # arrondi du fond
            pl = 0.10*s**2.2
            if s > 0.80: k = 1 - (s - 0.80)/0.20*0.80; rx, ry = 0.235*k*0.62, 0.170*k*0.62   # resserrement vers le col
        else:                                # collerette froncée
            s = (t - 0.70)/0.30; z = zc + (H - zc)*s
            rx = 0.042 + 0.050*s**0.8; ry = 0.034 + 0.040*s**0.8; pl = 0.30 + 0.20*s
        prof.append((z, rx, ry, pl))
    ph = [R.uniform(0, 6.28) for _ in range(3)]
    V = _Var(); rings = []
    for i, (z, rx, ry, pl) in enumerate(prof):
        ring = []
        for j in range(nu):
            a = 2*math.pi*j/nu
            f = 1 + pl*math.sin(a*7 + ph[0] + 0.4*math.sin(z*20))*(0.6 + 0.4*math.sin(a*3 + ph[1]))
            dent = 0.03*math.sin(a*2 + ph[2])*math.sin(math.pi*min(1, z/0.35))
            x = rx*math.cos(a)*(f + dent); y = ry*math.sin(a)*(f + dent)
            zz = z + (0.022*math.sin(a*7 + ph[1])*((z - zc)/(H - zc))**1.5 if z > zc else 0.0) + (-0.012*(1 - math.cos(a*2))*max(0, 1 - z/0.06) if z < 0.06 else 0)
            ring.append(V.v((x, y, max(0.0, zz))))
        rings.append(ring)
    for i in range(nv):
        for j in range(nu):
            V.f((rings[i][j], rings[i][(j + 1) % nu], rings[i + 1][(j + 1) % nu], rings[i + 1][j]), "toile")
    return V, zc                             # fond ouvert : le sac est posé

def _ficelle(pk, M, zc, s):
    """Ficelle : deux tours serrés au col, nœud et deux brins pendants."""
    for k in range(2):
        n = 14; pts = []
        for i in range(n):
            a = 2*math.pi*i/n
            pts.append(M @ Vector((0.046*math.cos(a)*1.02, 0.036*math.sin(a)*1.02, zc - 0.006 + 0.010*k + 0.002*math.sin(a))))
        pk.tube(pts, 0.0045*s, "ficelle", nv=5, closed=True)
    kn = M @ Vector((0.0, -0.040, zc))
    pk.sphere(kn, (0.010*s, 0.008*s, 0.009*s), "ficelle", 6, 4, 0.0)
    for sx in (-1, 1):
        pts = [M @ Vector((sx*0.004, -0.042, zc)), M @ Vector((sx*0.016, -0.050, zc - 0.03)), M @ Vector((sx*0.022, -0.054, zc - 0.07 - 0.02*(sx > 0)))]
        pk.tube(pts, 0.0035*s, "ficelle", nv=4)

def _sac_text(var, font, texte):
    """Mot imprimé : texte en vraie géométrie, plaqué sur la panse (projection sur la surface, 1,5 mm devant)."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromPolygons([tuple(c) for c in var.co], [f for f in var.faces])
    tm = text_mesh(texte, size=0.078, extrude=0.0, font=font)
    co = []
    for v in tm.vertices:
        o = Vector((v.co.x, -0.5, 0.175 + v.co.y))
        hit = bvh.ray_cast(o, Vector((0, 1, 0)), 1.0)
        if hit[0] is None: co.append(Vector((v.co.x, -0.16, 0.175 + v.co.y)))
        else: co.append(hit[0] - Vector((0, 0.0018, 0)))
    polys = [list(p.vertices) for p in tm.polygons]
    bpy.data.meshes.remove(tm)
    return co, polys

def sac(pk, x, y, z, a=0.0, s=1.0, font=None, texte="FARINE"):
    key = ("sac", texte, rnd.randrange(3))
    if key not in _LIB:
        R = _random.Random(zlib.crc32(repr(key).encode()))
        var, zc = _sac_shape(R)
        _LIB[key] = (var, zc, _sac_text(var, font, texte))
    var, zc, (tco, tpolys) = _LIB[key]
    M = Matrix.Translation((x, y, z)) @ rz(a) @ Matrix.Diagonal((s*U(0.96, 1.04), s*U(0.95, 1.05), s*U(0.95, 1.03), 1))
    _place(pk, var, M, rnd.random(), "sac")
    vs = [pk.bm.verts.new(M @ c) for c in tco]
    for p in tpolys:
        try: f = pk.bm.faces.new([vs[i] for i in p]); f.material_index = pk.mi("encre"); f.smooth = False
        except ValueError: pass
    _fl = pk.lay.get("var")
    _ficelle(pk, M, zc, s)

def tonneau(pk, x, y, r=0.30, h=0.88, z=0.0):
    n = 20; rings = 9
    pts = []
    for k in range(rings):
        zz = z + h*k/(rings - 1)
        pts.append((zz, r*(1 + 0.12*math.cos(math.pi*(zz - z - h/2)/h))))
    for i in range(n):                                        # douelles
        a0, a1 = 2*math.pi*i/n, 2*math.pi*(i + 1)/n - 0.012
        quad = []
        for zz, rr in pts:
            quad.append(((x + rr*math.cos(a0), y + rr*math.sin(a0), zz), (x + rr*math.cos(a1), y + rr*math.sin(a1), zz)))
        vs = [(pk.bm.verts.new(a), pk.bm.verts.new(b)) for a, b in quad]
        for k in range(len(vs) - 1):
            pk.bm.faces.new((vs[k][0], vs[k][1], vs[k + 1][1], vs[k + 1][0]))
        pk._fin([v for pair in vs for v in pair], "planche", True, 0.0, None, {})
    pk.cyl((x, y, z + 0.03), (x, y, z + h - 0.03), r*0.96, "planche", seg=n)
    for zz in (z + 0.08, z + 0.26, z + h - 0.26, z + h - 0.08):
        rr = r*(1 + 0.12*math.cos(math.pi*(zz - z - h/2)/h)) + 0.006
        pk.cyl((x, y, zz - 0.02), (x, y, zz + 0.02), rr, "fer", seg=n, caps=False)

def caisse(pk, x, y, z, w=0.55, d=0.38, h=0.32, a=0.0):
    R = Matrix.Translation((x, y, z)) @ rz(a)
    def B(x0, x1, y0, y1, z0, z1, m="planche"):
        pk.cube(R @ Matrix.Translation(((x0 + x1)/2, (y0 + y1)/2, (z0 + z1)/2)) @ Matrix.Diagonal((x1 - x0, y1 - y0, z1 - z0, 1)), m)
    for k in range(3):
        zz = 0.01 + k*(h - 0.02)/3
        B(-w/2, w/2, -d/2, -d/2 + 0.02, zz, zz + (h - 0.02)/3 - 0.015); B(-w/2, w/2, d/2 - 0.02, d/2, zz, zz + (h - 0.02)/3 - 0.015)
        B(-w/2, -w/2 + 0.02, -d/2, d/2, zz, zz + (h - 0.02)/3 - 0.015); B(w/2 - 0.02, w/2, -d/2, d/2, zz, zz + (h - 0.02)/3 - 0.015)
    for sx in (-1, 1):
        for sy in (-1, 1):
            B(sx*w/2 - (0.045 if sx > 0 else 0), sx*w/2 + (0.0 if sx > 0 else 0.045), sy*d/2 - (0.045 if sy > 0 else 0), sy*d/2 + (0 if sy > 0 else 0.045), 0, h, "chene")
    B(-w/2, w/2, -d/2, d/2, 0.0, 0.015)

def buche(pk, p1, p2, r, fendue=True):
    if fendue and rnd.random() < 0.6:
        # demi-bûche : secteur de cylindre (face fendue claire)
        v = Vector(p2) - Vector(p1); L = v.length; R = v.to_track_quat("Z", "Y").to_matrix().to_4x4()
        n = 7; a0 = U(0, 6); span = math.pi*U(0.9, 1.3)
        ring0, ring1 = [], []
        for i in range(n + 1):
            a = a0 + span*i/n
            for ring, zz in ((ring0, -L/2), (ring1, L/2)):
                ring.append(pk.bm.verts.new((Matrix.Translation((Vector(p1) + Vector(p2))/2) @ R) @ Vector((r*math.cos(a), r*math.sin(a), zz))))
        c0 = pk.bm.verts.new((Matrix.Translation((Vector(p1) + Vector(p2))/2) @ R) @ Vector((0, 0, -L/2)))
        c1 = pk.bm.verts.new((Matrix.Translation((Vector(p1) + Vector(p2))/2) @ R) @ Vector((0, 0, L/2)))
        bark = []
        for i in range(n):
            bark.append(pk.bm.faces.new((ring0[i], ring0[i + 1], ring1[i + 1], ring1[i])))
        split = [pk.bm.faces.new((ring0[0], ring1[0], c1, c0)), pk.bm.faces.new((ring0[-1], c0, c1, ring1[-1]))]
        ends = [pk.bm.faces.new([c0] + list(reversed(ring0))), pk.bm.faces.new([c1] + ring1)]
        pk._fin(ring0 + ring1 + [c0, c1], "ecorce", False, 0.0, None, {})
        for f in split + ends: f.material_index = pk.mi("buche")
        for f in bark: f.smooth = True
    else:
        pk.cyl(p1, p2, r, "ecorce", seg=9)

def pile_bois(pk, x0, x1, y, z0, rows, L=0.42, axis="y"):
    """Pile de bûches couchées (axe perpendiculaire au mur, axis = direction des bûches)."""
    for row in range(rows):
        x = x0 + (0.07 if row % 2 else 0.0)
        while x < x1 - 0.07:
            r = U(0.055, 0.085)
            zc = z0 + 0.075 + row*0.135 + U(-0.01, 0.01)
            off = U(-0.04, 0.04)
            if axis == "y": buche(pk, (x, y - L/2 + off, zc), (x + U(-0.02, 0.02), y + L/2 + off, zc + U(-0.02, 0.02)), r)
            else: buche(pk, (y - L/2 + off, x, zc), (y + L/2 + off, x + U(-0.02, 0.02), zc + U(-0.02, 0.02)), r)
            x += 2*r + U(0.0, 0.015)

def pelle(pk, base, top, blade_w=0.34, blade_l=0.48, m="planche_clair"):
    base, top = Vector(base), Vector(top); d = (top - base).normalized()
    pk.cyl(base + d*blade_l*0.9, top, 0.018, "chene_clair", seg=8)
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3: side = Vector((1, 0, 0))
    side.normalize(); nrm = side.cross(d)
    c = base + d*blade_l/2
    pk.cube(frame_m(c, side*blade_w, d*blade_l, nrm*0.012), "chene_clair")
    pk.cube(frame_m(base + d*(blade_l + 0.03), side*0.08, d*0.08, nrm*0.03), "chene_clair")

def lanterne(pk, x, y, z, s=1.0):
    """Lanterne de fer : cage, vitres, bougie (la lumière est ajoutée à part)."""
    h = 0.30*s; w = 0.17*s
    for dx in (-w/2, w/2):
        for dy in (-w/2, w/2):
            pk.box(x + dx - 0.008, x + dx + 0.008, y + dy - 0.008, y + dy + 0.008, z - h/2, z + h/2, "fer")
    pk.box(x - w/2 - 0.01, x + w/2 + 0.01, y - w/2 - 0.01, y + w/2 + 0.01, z - h/2 - 0.02, z - h/2, "fer")
    pk.hull([(x - w/2 - 0.02, y - w/2 - 0.02, z + h/2), (x + w/2 + 0.02, y - w/2 - 0.02, z + h/2), (x - w/2 - 0.02, y + w/2 + 0.02, z + h/2),
             (x + w/2 + 0.02, y + w/2 + 0.02, z + h/2), (x, y, z + h/2 + 0.12*s)], "fer")
    pk.tube([(x, y, z + h/2 + 0.12*s), (x, y, z + h/2 + 0.17*s)], 0.025*s, "fer", nv=8, closed=False)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        if sx: pk.box(x + sx*w/2 - 0.002, x + sx*w/2 + 0.002, y - w/2 + 0.008, y + w/2 - 0.008, z - h/2 + 0.01, z + h/2 - 0.01, "verre")
        else: pk.box(x - w/2 + 0.008, x + w/2 - 0.008, y + sy*w/2 - 0.002, y + sy*w/2 + 0.002, z - h/2 + 0.01, z + h/2 - 0.01, "verre")
    pk.cyl((x, y, z - h/2), (x, y, z - h/2 + 0.09*s), 0.022*s, "cire", seg=10)
    pk.sphere((x, y, z - h/2 + 0.11*s), (0.012*s, 0.012*s, 0.022*s), "flamme", 8, 6)

def point_light(name, loc, energy, color=(1.0, 0.62, 0.32), radius=0.06):
    ld = bpy.data.lights.new(name, "POINT"); ld.energy = energy; ld.color = color; ld.shadow_soft_size = radius
    o = bpy.data.objects.new(name, ld); o.location = loc; o.visible_camera = False; o.visible_glossy = False; o.visible_transmission = False
    coll("Eclairage").objects.link(o); return o

def portal(name, loc, rot, sx, sy):
    ld = bpy.data.lights.new(name, "AREA"); ld.shape = "RECTANGLE"; ld.size = sx; ld.size_y = sy; ld.cycles.is_portal = True
    o = bpy.data.objects.new(name, ld); o.location = loc; o.rotation_euler = rot; coll("Eclairage").objects.link(o); return o

def geraniums(pk, x0, x1, y0, y1, z, n=26, lav=False):
    for k in range(n):
        fx, fy = U(x0, x1), U(y0, y1)
        pk.sphere((fx, fy, z + U(0.02, 0.07)), (0.055, 0.055, 0.03), "feuille", 5, 3, 0.012)
        if lav:
            for j in range(3):
                bx, by = fx + U(-0.03, 0.03), fy + U(-0.03, 0.03)
                pk.cyl((bx, by, z + 0.03), (bx + U(-0.03, 0.03), by + U(-0.03, 0.03), z + U(0.18, 0.28)), 0.004, "feuille", seg=4, smooth=False)
                pk.sphere((bx + U(-0.03, 0.03), by + U(-0.03, 0.03), z + U(0.2, 0.3)), (0.012, 0.012, 0.045), "lavande", 4, 3, 0.003)
        elif k % 2 == 0:
            for j in range(4):
                pk.sphere((fx + U(-0.035, 0.035), fy + U(-0.035, 0.035), z + U(0.09, 0.15)), (0.018, 0.018, 0.016), "fleur", 4, 3, 0.004)

def _b_chausson(R, q, k):
    n = 8 if q else 6
    cuts = [((-0.035 + 0.035*i, -0.022), (-0.028 + 0.035*i, 0.020), 0.0035, 0.004, 0.0) for i in range(3)]
    return _dome(n + 4, n, 0.078, 0.046, 0.030, cuts, 0.03, 40 + k, crust="brioche", foot=0.03, bend=-0.35, rim_wave=0.025)

def chausson(pk, x, y, z, a=0.0, s=1.0):
    """Chausson aux pommes : demi-lune dorée, bord pincé, trois entailles en feuille."""
    var = _pick("chausson", 2, _b_chausson)
    _place(pk, var, _M(x, y, z, a, s*U(0.95, 1.05), s*U(0.95, 1.05), s*U(0.9, 1.1)), U(0.35, 0.9), "chausson")

def lavande_sechee(pk, x, y, z, n=14):
    """Bouquet de lavande séchée suspendu tête en bas (lien en x, y, z)."""
    pk.tube([(x, y, z), (x, y, z - 0.10)], 0.004, "toile", nv=4)
    pk.cyl((x, y, z - 0.10), (x, y, z - 0.13), 0.018, "toile", seg=8)
    for k in range(n):
        a = 2*math.pi*k/n + U(-0.2, 0.2); spread = U(0.02, 0.08); L = U(0.32, 0.42)
        tip = (x + spread*math.cos(a), y + spread*math.sin(a), z - 0.13 - L)
        pk.cyl((x, y, z - 0.12), tip, 0.0025, "feuille", seg=3, smooth=False)
        pk.sphere((tip[0], tip[1], tip[2] - 0.035), (0.010, 0.010, 0.045), "lavande", 4, 3, 0.002)
