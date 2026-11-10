# Boulangerie de Firmin — bibliothèque Blender (bpy 5.2) : matières procédurales, collections, constructeur Pack.
# Dérivée de atelier_lib.py (mêmes recettes de matières), mais importable comme module, sans remise à zéro
# de la scène, et avec des matières retrouvées par leur nom (étapes relançables à partir d'un .blend).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Quaternion, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
WORK = os.path.join(ROOT, "work")
os.makedirs(WORK, exist_ok=True)

rnd = random.Random(1330)
def seed(s): rnd.seed(s)
def U(a, b): return rnd.uniform(a, b)
def rv(): return Vector((U(-1, 1), U(-1, 1), U(-1, 1)))

COLLS = ["Architecture_Boutique", "Architecture_Fournil", "Toitures", "Menuiseries", "Four", "Props_Exterieurs",
         "Props_Interieurs", "Materiaux", "Eclairage", "Cameras", "Collisions", "LOD", "Studio_Rendu"]

def coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    return c

def new_obj(name, data, cname):
    o = bpy.data.objects.new(name, data); coll(cname).objects.link(o); return o

def open_blend(path):
    bpy.ops.wm.open_mainfile(filepath=path)

def save_blend(path):
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("SAUVE", path, flush=True)

# =====================================================================
# Matières (noms MAT_*) — recettes d'atelier_lib, plus celles de l'intérieur et des accessoires
# =====================================================================
def new_mat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; return m, nt, nt.nodes["Principled BSDF"], nt.links

def tc(nt): return nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
def attr(nt, name):
    a = nt.nodes.new("ShaderNodeAttribute"); a.attribute_name = name; return a.outputs["Fac"]

def ramp(nt, L, fac, stops):
    r = nt.nodes.new("ShaderNodeValToRGB"); cr = r.color_ramp
    cr.elements[0].position, cr.elements[0].color = stops[0][0], tuple(stops[0][1]) + (1,)
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], tuple(stops[-1][1]) + (1,)
    for p, c in stops[1:-1]:
        e = cr.elements.new(p); e.color = tuple(c) + (1,)
    L.new(fac, r.inputs["Fac"]); return r.outputs["Color"]

def noise(nt, L, vec, scale, detail=6, rough=0.55):
    n = nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail; n.inputs["Roughness"].default_value = rough
    if vec is not None: L.new(vec, n.inputs["Vector"])
    return n.outputs["Fac"]

def mnode(nt, L, op, a, b=None, clamp=False):
    m = nt.nodes.new("ShaderNodeMath"); m.operation = op; m.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None: continue
        if isinstance(v, (int, float)): m.inputs[i].default_value = v
        else: L.new(v, m.inputs[i])
    return m.outputs[0]

def mix(nt, L, fac, a, b, blend="MIX"):
    m = nt.nodes.new("ShaderNodeMix"); m.data_type = "RGBA"; m.blend_type = blend
    for sock, v in ((m.inputs["Factor"], fac), (m.inputs[6], a), (m.inputs[7], b)):
        if isinstance(v, (int, float)): sock.default_value = v
        elif isinstance(v, tuple): sock.default_value = v + (1,) if len(v) == 3 else v
        else: L.new(v, sock)
    return m.outputs[2]

def bump(nt, L, b, h, strength, dist=0.02):
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = strength
    bp.inputs["Distance"].default_value = dist
    L.new(h, bp.inputs["Height"]); L.new(bp.outputs["Normal"], b.inputs["Normal"])

# Rampe de teinte par pièce (attribut "var") : gardée en Python pour l'export (couleurs de sommets).
VAR_RAMPS = {
    "Moellons": [(0.0, (0.19, 0.18, 0.16)), (0.25, (0.29, 0.27, 0.23)), (0.5, (0.35, 0.30, 0.22)), (0.75, (0.26, 0.26, 0.25)), (1.0, (0.40, 0.35, 0.27))],
    "PierreTaille": [(0.0, (0.38, 0.35, 0.29)), (1.0, (0.47, 0.43, 0.35))],
    "Tuiles": [(0.0, (0.25, 0.09, 0.05)), (0.4, (0.33, 0.12, 0.065)), (0.75, (0.29, 0.15, 0.10)), (1.0, (0.20, 0.09, 0.06))],
    "Tomettes": [(0.0, (0.38, 0.14, 0.07)), (0.5, (0.50, 0.21, 0.11)), (1.0, (0.44, 0.24, 0.15))],
    "Dalles": [(0.0, (0.30, 0.29, 0.27)), (0.5, (0.42, 0.39, 0.34)), (1.0, (0.36, 0.33, 0.28))],
    # tour 3 : teinte de cuisson des pains (var 0 = bien cuit, 1 = blond), multipliée par la croûte
    "Croute": [(0.0, (0.30, 0.22, 0.18)), (0.35, (0.52, 0.42, 0.34)), (0.7, (0.82, 0.74, 0.64)), (1.0, (1.0, 1.0, 1.0))],
    "Grigne": [(0.0, (0.40, 0.30, 0.24)), (0.5, (0.78, 0.70, 0.60)), (1.0, (1.0, 1.0, 1.0))],
    "CrouteSeigle": [(0.0, (0.52, 0.46, 0.42)), (1.0, (1.0, 1.0, 1.0))],
    "Brioche": [(0.0, (0.52, 0.44, 0.38)), (1.0, (1.0, 1.0, 1.0))],
}

def m_var_stone(name, scale_n=14, rough=0.92, bmp=0.45, dark=(0.22, 0.20, 0.17)):
    m, nt, b, L = new_mat("MAT_" + name)
    base = ramp(nt, L, attr(nt, "var"), VAR_RAMPS[name])
    n1 = noise(nt, L, tc(nt), scale_n, 8)
    c = mix(nt, L, 0.35, base, dark, "MULTIPLY")
    c = mix(nt, L, n1, c, base)
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = rough
    bump(nt, L, b, noise(nt, L, tc(nt), 22, 10), bmp)
    return m

def m_moellon(): return m_var_stone("Moellons")
def m_taille(): return m_var_stone("PierreTaille", 30, 0.85, 0.25, (0.45, 0.42, 0.36))
def m_tomette():
    m = m_var_stone("Tomettes", 18, 0.6, 0.3, (0.30, 0.12, 0.07))
    return m
def m_dalle(): return m_var_stone("Dalles", 16, 0.8, 0.35, (0.20, 0.19, 0.17))

def m_mortier():
    m, nt, b, L = new_mat("MAT_Mortier")
    n = noise(nt, L, tc(nt), 40, 8)
    L.new(ramp(nt, L, n, [(0.3, (0.36, 0.34, 0.29)), (0.7, (0.48, 0.45, 0.39))]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 1.0; bump(nt, L, b, n, 0.6)
    return m

def m_enduit(name="MAT_Enduit", clean_c=((0.58, 0.53, 0.42), (0.68, 0.63, 0.51)), z_dirt=3.5, dirt_c=(0.33, 0.29, 0.23), soot_top=None):
    m, nt, b, L = new_mat(name)
    v = tc(nt)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(v, sep.inputs["Vector"])
    low = mnode(nt, L, "SUBTRACT", sep.outputs["Z"], z_dirt)
    low = mnode(nt, L, "MULTIPLY", low, 0.9)
    low = mnode(nt, L, "SUBTRACT", 1.0, low, clamp=True)
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (6, 6, 0.6)
    L.new(v, mp.inputs["Vector"])
    streak = noise(nt, L, mp.outputs["Vector"], 3, 4)
    dirt = mnode(nt, L, "ADD", mnode(nt, L, "MULTIPLY", low, 0.45),
                 mnode(nt, L, "MULTIPLY", mnode(nt, L, "SUBTRACT", streak, 0.47, clamp=True), 0.8))
    n = noise(nt, L, v, 9, 10)
    clean = ramp(nt, L, n, [(0.35, clean_c[0]), (0.65, clean_c[1])])
    c = mix(nt, L, mnode(nt, L, "MINIMUM", dirt, 0.7), clean, dirt_c)
    if soot_top is not None:      # noircissement en haut des murs (fournil)
        hi = mnode(nt, L, "MULTIPLY", mnode(nt, L, "SUBTRACT", sep.outputs["Z"], soot_top), 0.7, clamp=True)
        hi = mnode(nt, L, "MULTIPLY", hi, noise(nt, L, v, 2.5, 4))
        c = mix(nt, L, mnode(nt, L, "MULTIPLY", hi, 1.6, clamp=True), c, (0.10, 0.09, 0.08))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.95
    bump(nt, L, b, noise(nt, L, v, 35, 12), 0.35)
    return m

def m_torchis():
    m, nt, b, L = new_mat("MAT_Torchis")
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (40, 40, 6)
    L.new(tc(nt), mp.inputs["Vector"])
    n = noise(nt, L, mp.outputs["Vector"], 2, 12)
    L.new(ramp(nt, L, n, [(0.3, (0.30, 0.22, 0.13)), (0.7, (0.55, 0.44, 0.27))]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 1.0; bump(nt, L, b, n, 0.8)
    return m

def m_bois(name, dark, light, rough=0.72, scale=(1, 1, 1)):
    m, nt, b, L = new_mat("MAT_" + name)
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = scale
    L.new(tc(nt), mp.inputs["Vector"])
    w = nt.nodes.new("ShaderNodeTexWave"); w.wave_type = "RINGS"
    w.inputs["Scale"].default_value = 2.5; w.inputs["Distortion"].default_value = 9
    w.inputs["Detail"].default_value = 6
    L.new(mp.outputs["Vector"], w.inputs["Vector"])
    grain = ramp(nt, L, w.outputs["Fac"], [(0.0, dark), (1.0, light)])
    tint = ramp(nt, L, attr(nt, "var"), [(0.0, (0.78, 0.78, 0.78)), (1.0, (1.12, 1.08, 1.04))])
    c = mix(nt, L, 1.0, grain, tint, "MULTIPLY")
    weather = noise(nt, L, tc(nt), 6, 6)
    c = mix(nt, L, mnode(nt, L, "MULTIPLY", weather, 0.35), c, (0.35, 0.33, 0.30))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = rough
    bump(nt, L, b, w.outputs["Fac"], 0.25)
    return m

def m_tuiles():
    m, nt, b, L = new_mat("MAT_Tuiles")
    var, moss = attr(nt, "var"), attr(nt, "moss")
    base = ramp(nt, L, var, VAR_RAMPS["Tuiles"])
    n = noise(nt, L, tc(nt), 18, 8)
    base = mix(nt, L, mnode(nt, L, "MULTIPLY", n, 0.5), base, (0.14, 0.08, 0.06))
    mn = mnode(nt, L, "SUBTRACT", noise(nt, L, tc(nt), 26, 8), 0.5, clamp=True)
    mossmask = mnode(nt, L, "MULTIPLY", moss, mnode(nt, L, "MULTIPLY", mn, 6.0), clamp=True)
    c = mix(nt, L, mossmask, base, (0.13, 0.16, 0.06))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.85
    bump(nt, L, b, n, 0.3)
    return m

def m_four():
    m, nt, b, L = new_mat("MAT_FourSuie")
    vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 5
    L.new(tc(nt), vor.inputs["Vector"])
    edge = mnode(nt, L, "MULTIPLY", vor.outputs["Distance"], 18, clamp=True)
    v2 = nt.nodes.new("ShaderNodeTexVoronoi"); v2.inputs["Scale"].default_value = 5; L.new(tc(nt), v2.inputs["Vector"])
    st = ramp(nt, L, v2.outputs["Color"], [(0.0, (0.24, 0.21, 0.17)), (0.5, (0.36, 0.31, 0.24)), (1.0, (0.30, 0.29, 0.27))])
    c = mix(nt, L, edge, (0.16, 0.15, 0.13), st)
    soot = mnode(nt, L, "MULTIPLY", noise(nt, L, tc(nt), 2.5, 4), 0.75)
    c = mix(nt, L, soot, c, (0.05, 0.045, 0.04))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.95
    bump(nt, L, b, edge, 0.7)
    return m

def m_simple(name, c, rough=0.6, metal=0.0, emit=None, strength=6.0, coat=0.0):
    m, nt, b, L = new_mat("MAT_" + name)
    b.inputs["Base Color"].default_value = tuple(c) + (1,)
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if coat: b.inputs["Coat Weight"].default_value = coat
    if emit:
        b.inputs["Emission Color"].default_value = tuple(emit) + (1,)
        b.inputs["Emission Strength"].default_value = strength
    return m

def m_noise(name, c1, c2, scale, rough, bmp=0.3):
    m, nt, b, L = new_mat("MAT_" + name)
    n = noise(nt, L, tc(nt), scale, 8)
    L.new(ramp(nt, L, n, [(0.3, c1), (0.7, c2)]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough; bump(nt, L, b, n, bmp)
    return m

def m_pain(name="Croute", stops=((0.25, (0.40, 0.22, 0.085)), (0.55, (0.60, 0.38, 0.17)), (0.8, (0.76, 0.56, 0.30))), flour=1.0, rough=0.62,
           crackle=0.0, coat=0.0, grigne=(0.80, 0.54, 0.21)):
    """Croûte (tour 3) : teinte de cuisson par pain (attribut var, rampe VAR_RAMPS : 0 bien cuit, 1 blond), farine en
    plaques irrégulières sur le dessus, croûte craquelée en option (seigle), dorure brillante en option (viennoiseries)."""
    m, nt, b, L = new_mat("MAT_" + name)
    n = noise(nt, L, tc(nt), 20, 8)
    crust = ramp(nt, L, n, list(stops))
    tint = ramp(nt, L, attr(nt, "var"), VAR_RAMPS[name])
    crust = mix(nt, L, 1.0, crust, tint, "MULTIPLY")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Normal"], sep.inputs["Vector"])
    top = mnode(nt, L, "SUBTRACT", sep.outputs["Z"], 0.55)
    top = mnode(nt, L, "MULTIPLY", top, 3.0, clamp=True)
    patch = ramp(nt, L, noise(nt, L, tc(nt), 9, 3), [(0.50, (0, 0, 0)), (0.66, (1, 1, 1))])          # plaques de farine
    fl = mnode(nt, L, "MULTIPLY", top, patch, clamp=True)
    fl = mnode(nt, L, "MULTIPLY", fl, noise(nt, L, tc(nt), 140, 3), clamp=True)
    fl = mnode(nt, L, "MULTIPLY", fl, flour, clamp=True)
    h = n
    if crackle:
        vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 38
        L.new(tc(nt), vor.inputs["Vector"])
        cr = mnode(nt, L, "MULTIPLY", vor.outputs["Distance"], 14, clamp=True)       # 0 dans la fente, 1 sur l'écaille
        crust = mix(nt, L, mnode(nt, L, "SUBTRACT", 1.0, cr), crust, (0.55, 0.47, 0.38))  # fentes claires, farinées
        h = mnode(nt, L, "ADD", mnode(nt, L, "MULTIPLY", cr, crackle), mnode(nt, L, "MULTIPLY", n, 0.3))
    gr = nt.nodes.new("ShaderNodeMapRange"); gr.inputs["From Min"].default_value = 0.22; gr.inputs["From Max"].default_value = 0.65
    L.new(attr(nt, "grigne"), gr.inputs["Value"]); gfac = gr.outputs["Result"]
    gcol = mix(nt, L, 1.0, mix(nt, L, mnode(nt, L, "MULTIPLY", n, 0.5), grigne, tuple(min(1.0, x*1.12) for x in grigne)), tint, "MULTIPLY")
    crust = mix(nt, L, gfac, crust, gcol)
    # tour 3 bis : bords de grigne brûlés par plaques (la lèvre soulevée, là où le pain a le plus cuit)
    edge = mnode(nt, L, "MULTIPLY", mnode(nt, L, "MULTIPLY", gfac, mnode(nt, L, "SUBTRACT", 1.0, gfac)), 4.0, clamp=True)
    edge = mnode(nt, L, "MULTIPLY", edge, ramp(nt, L, noise(nt, L, tc(nt), 6, 3), [(0.45, (0, 0, 0)), (0.62, (1, 1, 1))]), clamp=True)
    burnt = mix(nt, L, 1.0, crust, (0.30, 0.22, 0.18), "MULTIPLY")
    crust = mix(nt, L, mnode(nt, L, "MULTIPLY", edge, 0.9, clamp=True), crust, burnt)
    fl = mnode(nt, L, "MULTIPLY", fl, mnode(nt, L, "SUBTRACT", 1.0, gfac), clamp=True)
    c = mix(nt, L, fl, crust, (0.86, 0.83, 0.76))
    L.new(c, b.inputs["Base Color"])
    L.new(mnode(nt, L, "ADD", mnode(nt, L, "MULTIPLY", fl, 0.35), rough), b.inputs["Roughness"])
    if coat:
        b.inputs["Coat Weight"].default_value = coat; b.inputs["Coat Roughness"].default_value = 0.18
    bump(nt, L, b, h, 0.5 if not crackle else 0.8)
    return m

def m_jute(name="ToileSac"):
    """Toile de jute : trame visible (fils de 4 mm alternés, comme un tissage toile), fibres et salissures."""
    m, nt, b, L = new_mat("MAT_" + name)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(tc(nt), sep.inputs["Vector"])
    hcoord = mnode(nt, L, "ADD", sep.outputs["X"], sep.outputs["Y"])
    k = 2*math.pi/0.0042
    su = mnode(nt, L, "SINE", mnode(nt, L, "MULTIPLY", hcoord, k))
    sv = mnode(nt, L, "SINE", mnode(nt, L, "MULTIPLY", sep.outputs["Z"], k))
    hu = mnode(nt, L, "SINE", mnode(nt, L, "MULTIPLY", hcoord, k/2))
    hv = mnode(nt, L, "SINE", mnode(nt, L, "MULTIPLY", sep.outputs["Z"], k/2))
    chk = mnode(nt, L, "GREATER_THAN", mnode(nt, L, "MULTIPLY", hu, hv), 0.0)
    au = mnode(nt, L, "ABSOLUTE", su); av = mnode(nt, L, "ABSOLUTE", sv)
    weave = mix(nt, L, chk, av, au)                                  # fil de chaîne ou de trame au-dessus
    fib = noise(nt, L, tc(nt), 260, 4)
    hgt = mnode(nt, L, "ADD", weave, mnode(nt, L, "MULTIPLY", fib, 0.35))
    base = ramp(nt, L, noise(nt, L, tc(nt), 7, 4), [(0.3, (0.36, 0.25, 0.12)), (0.7, (0.50, 0.37, 0.19))])
    c = mix(nt, L, mnode(nt, L, "MULTIPLY", mnode(nt, L, "SUBTRACT", 1.0, hgt), 0.55, clamp=True), base, (0.17, 0.13, 0.08))
    dust = mnode(nt, L, "MULTIPLY", ramp(nt, L, noise(nt, L, tc(nt), 5, 3), [(0.55, (0, 0, 0)), (0.75, (1, 1, 1))]), 0.35)
    c = mix(nt, L, dust, c, (0.80, 0.77, 0.70))                      # traces de farine
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 1.0
    bump(nt, L, b, hgt, 0.75, 0.004)
    return m

def m_vitrail():
    m, nt, b, L = new_mat("MAT_VerrePlomb")
    b.inputs["Base Color"].default_value = (0.80, 0.86, 0.80, 1)
    b.inputs["Transmission Weight"].default_value = 1.0; b.inputs["Roughness"].default_value = 0.05
    b.inputs["IOR"].default_value = 1.5
    bump(nt, L, b, noise(nt, L, tc(nt), 6, 2), 0.08)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(tc(nt), sep.inputs["Vector"])
    h = mnode(nt, L, "ADD", sep.outputs["X"], sep.outputs["Y"])
    def diag(sign):
        s = mnode(nt, L, "ADD" if sign > 0 else "SUBTRACT", h, sep.outputs["Z"])
        s = mnode(nt, L, "MULTIPLY", s, 1 / 0.16)
        s = mnode(nt, L, "FRACT", s)
        return mnode(nt, L, "LESS_THAN", s, 0.05)
    mask = mnode(nt, L, "MAXIMUM", diag(1), diag(-1))
    lead = nt.nodes.new("ShaderNodeBsdfPrincipled")
    lead.inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1); lead.inputs["Metallic"].default_value = 0.8
    lead.inputs["Roughness"].default_value = 0.45
    mx = nt.nodes.new("ShaderNodeMixShader")
    out = nt.nodes["Material Output"]
    L.new(mask, mx.inputs["Fac"]); L.new(b.outputs["BSDF"], mx.inputs[1]); L.new(lead.outputs["BSDF"], mx.inputs[2])
    L.new(mx.outputs["Shader"], out.inputs["Surface"])
    return m

def m_verre():
    m, nt, b, L = new_mat("MAT_Verre")
    b.inputs["Base Color"].default_value = (0.86, 0.90, 0.86, 1)
    b.inputs["Transmission Weight"].default_value = 1.0; b.inputs["Roughness"].default_value = 0.03
    bump(nt, L, b, noise(nt, L, tc(nt), 4, 2), 0.06)
    return m

def m_verre_vitrine():
    """Verre de présentoir : transparent pour les rayons d'ombre (le contenu reste éclairé)."""
    m, nt, b, L = new_mat("MAT_VerreVitrine")
    b.inputs["Base Color"].default_value = (0.92, 0.94, 0.92, 1); b.inputs["Transmission Weight"].default_value = 1.0
    b.inputs["Roughness"].default_value = 0.02
    lp = nt.nodes.new("ShaderNodeLightPath"); tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader")
    L.new(lp.outputs["Is Shadow Ray"], mx.inputs["Fac"]); L.new(b.outputs["BSDF"], mx.inputs[1]); L.new(tr.outputs["BSDF"], mx.inputs[2])
    L.new(mx.outputs["Shader"], nt.nodes["Material Output"].inputs["Surface"])
    return m

def m_fumee():
    """Fumée légère : densité de bruit, panache qui s'élargit et dérive (suit le cisaillement du maillage)."""
    m = bpy.data.materials.new("MAT_Fumee"); m.use_nodes = True; nt = m.node_tree; L = nt.links
    for n_ in list(nt.nodes):
        if n_.type == "BSDF_PRINCIPLED": nt.nodes.remove(n_)
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (0.30, 0.30, 0.32, 1)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(tc(nt), sep.inputs["Vector"])
    Z = sep.outputs["Z"]
    X = mnode(nt, L, "SUBTRACT", sep.outputs["X"], mnode(nt, L, "MULTIPLY", Z, 0.25))
    Y = mnode(nt, L, "ADD", sep.outputs["Y"], mnode(nt, L, "MULTIPLY", Z, 0.15))
    mp = nt.nodes.new("ShaderNodeMapping"); L.new(tc(nt), mp.inputs["Vector"]); mp.inputs["Scale"].default_value = (1.4, 1.4, 0.8)
    n = noise(nt, L, mp.outputs["Vector"], 1.6, 5, 0.55)
    d = mnode(nt, L, "MULTIPLY", mnode(nt, L, "SUBTRACT", n, 0.34, clamp=True), 9.0)
    cx = nt.nodes.new("ShaderNodeCombineXYZ"); L.new(X, cx.inputs["X"]); L.new(Y, cx.inputs["Y"])
    r = nt.nodes.new("ShaderNodeVectorMath"); r.operation = "LENGTH"; L.new(cx.outputs["Vector"], r.inputs[0])
    width = mnode(nt, L, "ADD", 0.30, mnode(nt, L, "MULTIPLY", Z, 0.22))
    radial = mnode(nt, L, "SUBTRACT", 1.0, mnode(nt, L, "DIVIDE", r.outputs["Value"], width), clamp=True)
    fade = mnode(nt, L, "SUBTRACT", 1.0, mnode(nt, L, "MULTIPLY", Z, 0.13), clamp=True)
    d = mnode(nt, L, "MULTIPLY", mnode(nt, L, "MULTIPLY", d, radial), fade)
    L.new(d, vol.inputs["Density"])
    L.new(vol.outputs["Volume"], nt.nodes["Material Output"].inputs["Volume"])
    return m

def m_studio():
    return m_simple("StudioSol", (0.62, 0.62, 0.62), 0.9)

RECETTES = {
    "moellon": ("MAT_Moellons", m_moellon), "taille": ("MAT_PierreTaille", m_taille),
    "mortier": ("MAT_Mortier", m_mortier), "enduit": ("MAT_Enduit", m_enduit),
    "enduit_int": ("MAT_EnduitInterieur", lambda: m_enduit("MAT_EnduitInterieur", ((0.80, 0.77, 0.69), (0.88, 0.85, 0.78)), 0.6, (0.55, 0.50, 0.42))),
    "enduit_fournil": ("MAT_EnduitFournil", lambda: m_enduit("MAT_EnduitFournil", ((0.70, 0.66, 0.58), (0.80, 0.76, 0.68)), 0.6, (0.40, 0.36, 0.30), soot_top=1.6)),
    "torchis": ("MAT_Torchis", m_torchis), "tuiles": ("MAT_Tuiles", m_tuiles),
    "tomette": ("MAT_Tomettes", m_tomette), "dalle": ("MAT_Dalles", m_dalle),
    "four": ("MAT_FourSuie", m_four), "vitrail": ("MAT_VerrePlomb", m_vitrail), "verre": ("MAT_Verre", m_verre), "verre_vitrine": ("MAT_VerreVitrine", m_verre_vitrine),
    "fumee": ("MAT_Fumee", m_fumee), "studio": ("MAT_StudioSol", m_studio),
    "chene": ("MAT_Chene", lambda: m_bois("Chene", (0.045, 0.030, 0.019), (0.11, 0.070, 0.042), scale=(1, 1, 0.15))),
    "chene_clair": ("MAT_ChenePatine", lambda: m_bois("ChenePatine", (0.20, 0.13, 0.08), (0.36, 0.25, 0.15), 0.6, scale=(1, 1, 0.15))),
    "planche": ("MAT_Planches", lambda: m_bois("Planches", (0.12, 0.08, 0.045), (0.24, 0.16, 0.09), scale=(0.15, 1, 1))),
    "plancher": ("MAT_Plancher", lambda: m_bois("Plancher", (0.16, 0.10, 0.055), (0.30, 0.20, 0.11), 0.55, scale=(1, 0.12, 1))),
    "osier": ("MAT_Osier", lambda: m_bois("Osier", (0.35, 0.24, 0.12), (0.58, 0.43, 0.22), rough=0.8, scale=(5, 5, 5))),
    "buche": ("MAT_Buches", lambda: m_bois("Buches", (0.22, 0.16, 0.10), (0.46, 0.35, 0.22), rough=0.9, scale=(1, 0.3, 1))),
    "ecorce": ("MAT_Ecorce", lambda: m_noise("Ecorce", (0.10, 0.08, 0.06), (0.22, 0.18, 0.13), 25, 1.0, 0.9)),
    "volet": ("MAT_VoletsSauge", lambda: m_bois("VoletsSauge", (0.22, 0.28, 0.24), (0.31, 0.37, 0.32), rough=0.7, scale=(1, 1, 0.15))),
    "fer": ("MAT_FerForge", lambda: m_simple("FerForge", (0.035, 0.035, 0.035), 0.55, 1.0)),
    "or": ("MAT_Dorure", lambda: m_simple("Dorure", (0.80, 0.58, 0.22), 0.32, 1.0)),
    "peinture": ("MAT_PeintureCreme", lambda: m_simple("PeintureCreme", (0.86, 0.79, 0.58), 0.6)),
    "peinture_rouge": ("MAT_PeintureRouge", lambda: m_simple("PeintureRouge", (0.36, 0.09, 0.06), 0.65)),
    "toile": ("MAT_ToileSac", m_jute),
    "ficelle": ("MAT_Ficelle", lambda: m_noise("Ficelle", (0.55, 0.47, 0.33), (0.70, 0.62, 0.46), 300, 0.9, 0.5)),
    "toile_creme": ("MAT_ToileCreme", lambda: m_noise("ToileCreme", (0.74, 0.69, 0.57), (0.82, 0.78, 0.66), 70, 0.95, 0.2)),
    "toile_rouge": ("MAT_ToileBrique", lambda: m_noise("ToileBrique", (0.42, 0.15, 0.10), (0.52, 0.22, 0.15), 70, 0.95, 0.2)),
    "lin_raye": ("MAT_LinBleu", lambda: m_noise("LinBleu", (0.20, 0.26, 0.38), (0.26, 0.32, 0.44), 80, 0.95, 0.2)),
    "encre": ("MAT_EncreSac", lambda: m_simple("EncreSac", (0.12, 0.10, 0.09), 0.9)),
    "terre": ("MAT_Terreau", lambda: m_noise("Terreau", (0.10, 0.07, 0.05), (0.18, 0.13, 0.09), 30, 1.0, 0.7)),
    "feuille": ("MAT_Feuilles", lambda: m_noise("Feuilles", (0.06, 0.17, 0.04), (0.14, 0.30, 0.07), 30, 0.65)),
    "lierre": ("MAT_Lierre", lambda: m_noise("Lierre", (0.03, 0.10, 0.03), (0.09, 0.20, 0.06), 12, 0.45)),
    "fleur": ("MAT_Geranium", lambda: m_simple("Geranium", (0.62, 0.04, 0.05), 0.6)),
    "lavande": ("MAT_Lavande", lambda: m_simple("Lavande", (0.33, 0.24, 0.52), 0.7)),
    "terrecuite": ("MAT_TerreCuite", lambda: m_noise("TerreCuite", (0.42, 0.20, 0.11), (0.55, 0.28, 0.16), 20, 0.85)),
    "ardoise": ("MAT_Ardoise", lambda: m_noise("Ardoise", (0.05, 0.055, 0.06), (0.11, 0.115, 0.12), 25, 0.75, 0.2)),
    "craie": ("MAT_Craie", lambda: m_simple("Craie", (0.85, 0.85, 0.82), 0.95)),
    "pain": ("MAT_Croute", m_pain),
    "seigle": ("MAT_CrouteSeigle", lambda: m_pain("CrouteSeigle", ((0.25, (0.13, 0.065, 0.035)), (0.55, (0.22, 0.12, 0.06)), (0.8, (0.30, 0.17, 0.09))), 1.4, 0.8, crackle=1.0, grigne=(0.42, 0.25, 0.11))),
    "brioche": ("MAT_Brioche", lambda: m_pain("Brioche", ((0.25, (0.36, 0.13, 0.025)), (0.55, (0.60, 0.29, 0.06)), (0.8, (0.76, 0.46, 0.13))), 0.0, 0.30, coat=0.7, grigne=(0.85, 0.62, 0.28))),
    "grigne": ("MAT_Grigne", lambda: m_pain("Grigne", ((0.25, (0.62, 0.36, 0.11)), (0.55, (0.76, 0.50, 0.19)), (0.8, (0.85, 0.62, 0.30))), 0.0, 0.5)),
    "mie": ("MAT_Pate", lambda: m_noise("Pate", (0.78, 0.70, 0.56), (0.86, 0.80, 0.67), 30, 0.7, 0.1)),
    "farine": ("MAT_Farine", lambda: m_noise("Farine", (0.80, 0.78, 0.73), (0.92, 0.91, 0.87), 40, 1.0, 0.15)),
    "flamme": ("MAT_Flamme", lambda: m_simple("Flamme", (1.0, 0.6, 0.3), emit=(1.0, 0.24, 0.02), strength=9)),
    "braise": ("MAT_Braise", lambda: m_simple("Braise", (0.3, 0.1, 0.0), emit=(1.0, 0.13, 0.015), strength=9)),
    "flamme_coeur": ("MAT_FlammeCoeur", lambda: m_simple("FlammeCoeur", (1.0, 0.5, 0.2), emit=(1.0, 0.42, 0.05), strength=10)),
    "plomb": ("MAT_PlombNoue", lambda: m_simple("PlombNoue", (0.20, 0.21, 0.22), 0.5, 0.6)),
    "joint": ("MAT_JointTomettes", lambda: m_noise("JointTomettes", (0.10, 0.085, 0.07), (0.17, 0.14, 0.11), 30, 1.0, 0.3)),
    "suie": ("MAT_Suie", lambda: m_simple("Suie", (0.03, 0.03, 0.03), 0.95)),
    "cendre": ("MAT_Cendre", lambda: m_noise("Cendre", (0.12, 0.11, 0.10), (0.30, 0.28, 0.26), 30, 1.0)),
    "cuivre": ("MAT_Cuivre", lambda: m_simple("Cuivre", (0.70, 0.38, 0.22), 0.35, 1.0)),
    "faience": ("MAT_Faience", lambda: m_simple("Faience", (0.82, 0.80, 0.72), 0.25)),
    "cire": ("MAT_Bougie", lambda: m_simple("Bougie", (0.85, 0.80, 0.66), 0.5, emit=(1.0, 0.7, 0.35), strength=0.25)),
}

def mat(key):
    name, fn = RECETTES[key]
    m = bpy.data.materials.get(name)
    if m is None:
        m = fn(); m.name = name; m.use_fake_user = True
    return m

# =====================================================================
# Constructeur : beaucoup de pièces dans un seul objet, plusieurs matières, attributs par pièce
# =====================================================================
class Pack:
    def __init__(self, name, mats, cname, attrs=("var",)):
        self.name, self.cname = name, cname
        self.mats = [mats] if isinstance(mats, str) else list(mats)
        self.bm = bmesh.new()
        self.lay = {a: self.bm.faces.layers.float.new(a) for a in attrs}
        self.n = 0

    def mi(self, m):
        if isinstance(m, int): return m
        if m not in self.mats: self.mats.append(m)
        return self.mats.index(m)

    def _fin(self, verts, m, smooth=False, jitter=0.0, axis=None, vals=None):
        if jitter:
            for v in verts: v.co += rv() * jitter
        faces = {f for v in verts for f in v.link_faces}
        vals = dict(vals or {})
        if "var" in self.lay: vals.setdefault("var", rnd.random())
        i = self.mi(m)
        for f in faces:
            f.material_index = i
            if smooth == "side" and axis is not None:
                f.normal_update(); f.smooth = abs(f.normal.dot(axis)) < 0.85
            else:
                f.smooth = bool(smooth)
            for k, val in vals.items():
                if k in self.lay: f[self.lay[k]] = val
        self.n += 1
        return faces

    def cube(self, matrix, m=0, jitter=0.0, smooth=False, keep=None, drop=None, **vals):
        """Pavé unité transformé. Normales toujours sortantes (repères indirects corrigés).
        keep : directions monde des seules faces à garder ; drop : direction des faces à supprimer (ex. face enfouie)."""
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)
        faces = list({f for v in r["verts"] for f in v.link_faces})
        if matrix.to_3x3().determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        if keep is not None or drop is not None:
            for f in faces: f.normal_update()
            dead = []
            for f in faces:
                if drop is not None and f.normal.dot(drop) > 0.8: dead.append(f)
                elif keep is not None and not any(f.normal.dot(k) > 0.8 for k in keep): dead.append(f)
            bmesh.ops.delete(self.bm, geom=dead, context="FACES_ONLY")
            r["verts"] = [v for v in r["verts"] if v.is_valid and v.link_faces]
        return self._fin(r["verts"], m, smooth, jitter, None, vals)

    def box(self, x0, x1, y0, y1, z0, z1, m=0, jitter=0.0, **vals):
        x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1)); z0, z1 = sorted((z0, z1))
        return self.cube(Matrix.Translation(((x0+x1)/2, (y0+y1)/2, (z0+z1)/2)) @ Matrix.Diagonal((x1-x0, y1-y0, z1-z0, 1)), m, jitter, **vals)

    def beam(self, p1, p2, depth, width, m=0, roll=0.0, jitter=0.0, **vals):
        p1, p2 = Vector(p1), Vector(p2); v = p2 - p1
        q = v.to_track_quat("Y", "Z")
        R = q.to_matrix().to_4x4() @ Matrix.Rotation(roll, 4, "Y")
        return self.cube(Matrix.Translation((p1+p2)/2) @ R @ Matrix.Diagonal((depth, v.length, width, 1)), m, jitter, **vals)

    def cyl(self, p1, p2, r, m=0, seg=12, r2=None, smooth=True, caps=True, **vals):
        p1, p2 = Vector(p1), Vector(p2); v = p2 - p1
        R = v.to_track_quat("Z", "Y").to_matrix().to_4x4()
        res = bmesh.ops.create_cone(self.bm, cap_ends=caps, cap_tris=False, segments=seg, radius1=r,
                                    radius2=r if r2 is None else r2, depth=v.length,
                                    matrix=Matrix.Translation((p1+p2)/2) @ R)
        return self._fin(res["verts"], m, "side" if smooth else False, 0.0, v.normalized(), vals)

    def sphere(self, loc, scale, m=0, seg=16, ring=10, jitter=0.0, rot=(0, 0, 0), smooth=True, **vals):
        M = Matrix.Translation(loc) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=ring, radius=1.0, matrix=M)
        return self._fin(res["verts"], m, smooth, jitter, None, vals)

    def tube(self, pts, radii, m=0, nv=8, closed=False, caps=True, smooth=True, flat=1.0, **vals):
        """Tube balayé le long d'une polyligne (rayons par point ; flat < 1 aplatit la section)."""
        pts = [Vector(p) for p in pts]; n = len(pts)
        if isinstance(radii, (int, float)): radii = [radii] * n
        tans = []
        for i in range(n):
            if closed: t = pts[(i+1) % n] - pts[i-1]
            else: t = pts[min(i+1, n-1)] - pts[max(i-1, 0)]
            tans.append(t.normalized())
        ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
        nrm = tans[0].cross(ref).normalized()
        rings = []
        for i in range(n):
            if i > 0:
                nrm = (nrm - tans[i]*nrm.dot(tans[i])).normalized()
            bi = tans[i].cross(nrm)
            ring = []
            for k in range(nv):
                a = 2*math.pi*k/nv
                ring.append(self.bm.verts.new(pts[i] + (nrm*math.cos(a) + bi*math.sin(a)*flat) * radii[i]))
            rings.append(ring)
        verts = [v for r in rings for v in r]
        for i in range(n if closed else n-1):
            r0, r1 = rings[i], rings[(i+1) % n]
            for k in range(nv):
                self.bm.faces.new((r0[k], r0[(k+1) % nv], r1[(k+1) % nv], r1[k]))
        if caps and not closed:
            self.bm.faces.new(list(reversed(rings[0]))); self.bm.faces.new(rings[-1])
        return self._fin(verts, m, smooth, 0.0, None, vals)

    def prism(self, poly, ext, m=0, **vals):
        """Prisme : polygone (points 3D coplanaires) extrudé du vecteur ext."""
        a = [self.bm.verts.new(Vector(p)) for p in poly]
        b = [self.bm.verts.new(Vector(p) + Vector(ext)) for p in poly]
        n = len(a)
        self.bm.faces.new(a); self.bm.faces.new(list(reversed(b)))
        for i in range(n):
            j = (i+1) % n; self.bm.faces.new((a[i], a[j], b[j], b[i]))
        return self._fin(a + b, m, False, 0.0, None, vals)

    def hull(self, pts, m=0, **vals):
        vs = [self.bm.verts.new(Vector(p)) for p in pts]
        r = bmesh.ops.convex_hull(self.bm, input=vs)
        dead = [v for v in r.get("geom_interior", []) if isinstance(v, bmesh.types.BMVert)]
        if dead: bmesh.ops.delete(self.bm, geom=dead, context="VERTS")
        vs = [v for v in vs if v.is_valid]
        return self._fin(vs, m, False, 0.0, None, vals)

    def mesh(self, me, matrix, m=0, smooth=None, **vals):
        """Copie un maillage (par ex. un texte converti) dans le lot."""
        vs = [self.bm.verts.new(matrix @ v.co) for v in me.vertices]
        for p in me.polygons:
            try:
                f = self.bm.faces.new([vs[i] for i in p.vertices])
                if smooth is not None: f.smooth = smooth
            except ValueError: pass
        return self._fin(vs, m, smooth if smooth is not None else False, 0.0, None, vals)

    def done(self, bev=0.0, seg=1, angle=35, recalc=True, obj_name=None):
        if recalc:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        fp = self.bm.faces.layers.int.get("est_pain")
        if bev and fp is not None:
            # tour 3 : les pains ne sont pas chanfreinés (leurs arêtes vives sont des grignes) ; même règle d'angle ailleurs
            bw = self.bm.edges.layers.float.get("bevel_weight_edge") or self.bm.edges.layers.float.new("bevel_weight_edge")
            lim = math.radians(angle)
            for e in self.bm.edges:
                lf = e.link_faces
                e[bw] = 1.0 if (len(lf) == 2 and not lf[0][fp] and not lf[1][fp] and e.calc_face_angle(0.0) > lim) else 0.0
        me = bpy.data.meshes.new(obj_name or self.name); self.bm.to_mesh(me); self.bm.free()
        for k in self.mats: me.materials.append(mat(k))
        o = new_obj(obj_name or self.name, me, self.cname)
        if bev:
            md = o.modifiers.new("Chanfrein", "BEVEL"); md.width = bev; md.segments = seg
            md.limit_method = "ANGLE"; md.angle_limit = math.radians(angle); md.harden_normals = False
            if "bevel_weight_edge" in me.attributes: md.limit_method = "WEIGHT"
        return o

def text_mesh(body, size=1.0, extrude=0.01, align="CENTER", spacing=1.0, font=None, bevel=0.0):
    """Texte en vraie géométrie : rend un maillage (couché dans XY, lisible depuis +Z)."""
    cu = bpy.data.curves.new("txt", "FONT"); cu.body = body; cu.size = size; cu.extrude = extrude
    cu.align_x = align; cu.align_y = "CENTER"; cu.space_character = spacing; cu.bevel_depth = bevel; cu.resolution_u = 3
    if font: cu.font = font
    o = bpy.data.objects.new("txt", cu); bpy.context.scene.collection.objects.link(o)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    bpy.data.objects.remove(o); bpy.data.curves.remove(cu)
    return me

def load_font():
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSerif-Bold.ttf",
              "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf"):
        if os.path.exists(p):
            return bpy.data.fonts.load(p, check_existing=True)
    return None

def frame_m(origin, ex, ey, ez):
    """Matrice d'un repère (axes donnés en colonnes)."""
    m = Matrix((ex, ey, ez)).transposed().to_4x4(); m.translation = Vector(origin); return m

def boolean_cut(target, cutter, solver="EXACT"):
    md = target.modifiers.new("trou", "BOOLEAN"); md.object = cutter; md.operation = "DIFFERENCE"; md.solver = solver
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(target.evaluated_get(dg))
    target.modifiers.remove(md)
    old = target.data; target.data = me; bpy.data.meshes.remove(old)
    bpy.data.objects.remove(cutter)

def recalc(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:]); bm.to_mesh(o.data); bm.free()

def tri_count(objs):
    dg = bpy.context.evaluated_depsgraph_get(); n = 0
    for o in objs:
        if o.type != "MESH": continue
        ev = o.evaluated_get(dg); me = ev.to_mesh(); me.calc_loop_triangles(); n += len(me.loop_triangles); ev.to_mesh_clear()
    return n
