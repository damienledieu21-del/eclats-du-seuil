# Atelier Éclats du Seuil — bibliothèque commune : matières, constructeurs (boîtes, poutres, lots de pièces).
# Utilisée par exec() depuis les scripts de bâtiments ; lancement : python -I <script> <sortie> <vues|none> [export|bake].
import bpy, bmesh, math, sys, os, random
from mathutils import Vector, Matrix, Quaternion

OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/boulangerie2"
VIEWS = sys.argv[2].split(",") if len(sys.argv) > 2 and sys.argv[2] != "none" else []
MODE = sys.argv[3] if len(sys.argv) > 3 else ""
EXPORT = MODE == "export"
os.makedirs(OUT, exist_ok=True)
rnd = random.Random(1330)
def U(a, b): return rnd.uniform(a, b)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection

# =====================================================================
# Matières
# =====================================================================
def new_mat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; return m, nt, nt.nodes["Principled BSDF"], nt.links

def tc(nt):
    return nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]

def attr(nt, name):
    a = nt.nodes.new("ShaderNodeAttribute"); a.attribute_name = name; return a.outputs["Fac"]

def ramp(nt, L, fac, stops):
    r = nt.nodes.new("ShaderNodeValToRGB"); cr = r.color_ramp
    cr.elements[0].position, cr.elements[0].color = stops[0][0], stops[0][1] + (1,)
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], stops[-1][1] + (1,)
    for p, c in stops[1:-1]:
        e = cr.elements.new(p); e.color = c + (1,)
    L.new(fac, r.inputs["Fac"]); return r.outputs["Color"]

def noise(nt, L, vec, scale, detail=6, rough=0.55):
    n = nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail; n.inputs["Roughness"].default_value = rough
    if vec is not None: L.new(vec, n.inputs["Vector"])
    return n.outputs["Fac"]

def math_node(nt, L, op, a, b=None, clamp=False):
    m = nt.nodes.new("ShaderNodeMath"); m.operation = op; m.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None: continue
        if isinstance(v, (int, float)): m.inputs[i].default_value = v
        else: L.new(v, m.inputs[i])
    return m.outputs[0]

def mix_rgb(nt, L, fac, a, b, blend="MIX"):
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

def m_moellon():                                   # pierres du rez : chaque pierre a sa teinte
    m, nt, b, L = new_mat("Moellons")
    var = attr(nt, "var")
    base = ramp(nt, L, var, [(0.0, (0.20, 0.18, 0.15)), (0.3, (0.33, 0.29, 0.22)),
                             (0.55, (0.40, 0.33, 0.22)), (0.8, (0.30, 0.29, 0.27)), (1.0, (0.44, 0.39, 0.31))])
    n1 = noise(nt, L, tc(nt), 14, 8)
    c = mix_rgb(nt, L, 0.35, base, (0.22, 0.20, 0.17), "MULTIPLY")
    c = mix_rgb(nt, L, n1, c, base)
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.92
    bump(nt, L, b, noise(nt, L, tc(nt), 22, 10), 0.45)
    return m

def m_taille():                                    # pierre de taille calcaire (angles, piédroits)
    m, nt, b, L = new_mat("PierreTaille")
    var = attr(nt, "var")
    base = ramp(nt, L, var, [(0.0, (0.44, 0.40, 0.32)), (1.0, (0.53, 0.49, 0.40))])
    n1 = noise(nt, L, tc(nt), 30, 8)
    c = mix_rgb(nt, L, math_node(nt, L, "MULTIPLY", n1, 0.5), base, (0.45, 0.42, 0.36))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.85
    bump(nt, L, b, n1, 0.25)
    return m

def m_mortier():
    m, nt, b, L = new_mat("Mortier")
    n = noise(nt, L, tc(nt), 40, 8)
    L.new(ramp(nt, L, n, [(0.3, (0.30, 0.28, 0.24)), (0.7, (0.42, 0.39, 0.33))]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 1.0; bump(nt, L, b, n, 0.6)
    return m

def m_enduit():                                    # enduit à la chaux, sali vers le bas, coulures
    m, nt, b, L = new_mat("Enduit")
    v = tc(nt)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(v, sep.inputs["Vector"])
    low = math_node(nt, L, "MAP_RANGE" if False else "SUBTRACT", sep.outputs["Z"], 3.2)
    low = math_node(nt, L, "MULTIPLY", low, 0.9)
    low = math_node(nt, L, "SUBTRACT", 1.0, low, clamp=True)       # 1 en bas de l'étage, 0 plus haut
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (6, 6, 0.6)
    L.new(v, mp.inputs["Vector"])
    streak = noise(nt, L, mp.outputs["Vector"], 3, 4)
    dirt = math_node(nt, L, "ADD", math_node(nt, L, "MULTIPLY", low, 0.55),
                     math_node(nt, L, "MULTIPLY", math_node(nt, L, "SUBTRACT", streak, 0.45, clamp=True), 0.9))
    n = noise(nt, L, v, 9, 10)
    clean = ramp(nt, L, n, [(0.35, (0.74, 0.69, 0.58)), (0.65, (0.84, 0.80, 0.70))])
    c = mix_rgb(nt, L, math_node(nt, L, "MINIMUM", dirt, 0.75), clean, (0.42, 0.38, 0.31))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.95
    bump(nt, L, b, noise(nt, L, v, 35, 12), 0.35)
    return m

def m_torchis():
    m, nt, b, L = new_mat("Torchis")
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (40, 40, 6)
    L.new(tc(nt), mp.inputs["Vector"])
    n = noise(nt, L, mp.outputs["Vector"], 2, 12)
    L.new(ramp(nt, L, n, [(0.3, (0.30, 0.22, 0.13)), (0.7, (0.55, 0.44, 0.27))]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 1.0; bump(nt, L, b, n, 0.8)
    return m

def m_bois(name, dark, light, rough=0.72, scale=(1, 1, 1)):
    """Bois : veines (ondes déformées) + teinte propre à chaque pièce (Object Info)."""
    m, nt, b, L = new_mat(name)
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = scale
    L.new(tc(nt), mp.inputs["Vector"])
    w = nt.nodes.new("ShaderNodeTexWave"); w.wave_type = "RINGS"
    w.inputs["Scale"].default_value = 2.5; w.inputs["Distortion"].default_value = 9
    w.inputs["Detail"].default_value = 6
    L.new(mp.outputs["Vector"], w.inputs["Vector"])
    oi = nt.nodes.new("ShaderNodeObjectInfo")
    grain = ramp(nt, L, w.outputs["Fac"], [(0.0, dark), (1.0, light)])
    tint = ramp(nt, L, oi.outputs["Random"], [(0.0, (0.75, 0.75, 0.75)), (1.0, (1.15, 1.1, 1.05))])
    c = mix_rgb(nt, L, 1.0, grain, tint, "MULTIPLY")
    weather = noise(nt, L, tc(nt), 6, 6)
    c = mix_rgb(nt, L, math_node(nt, L, "MULTIPLY", weather, 0.35), c, (0.35, 0.33, 0.30))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = rough
    bump(nt, L, b, w.outputs["Fac"], 0.25)
    return m

def m_tuiles():
    m, nt, b, L = new_mat("Tuiles")
    var, moss = attr(nt, "var"), attr(nt, "moss")
    base = ramp(nt, L, var, [(0.0, (0.36, 0.14, 0.08)), (0.4, (0.46, 0.19, 0.10)),
                             (0.75, (0.40, 0.22, 0.14)), (1.0, (0.28, 0.13, 0.09))])
    n = noise(nt, L, tc(nt), 18, 8)
    base = mix_rgb(nt, L, math_node(nt, L, "MULTIPLY", n, 0.4), base, (0.18, 0.10, 0.07))
    mn = math_node(nt, L, "SUBTRACT", noise(nt, L, tc(nt), 26, 8), 0.5, clamp=True)
    mossmask = math_node(nt, L, "MULTIPLY", moss, math_node(nt, L, "MULTIPLY", mn, 6.0), clamp=True)
    c = mix_rgb(nt, L, mossmask, base, (0.20, 0.25, 0.08))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.85
    bump(nt, L, b, n, 0.3)
    return m

def m_pave():
    m, nt, b, L = new_mat("Paves")
    var = attr(nt, "var")
    base = ramp(nt, L, var, [(0.0, (0.26, 0.25, 0.23)), (0.5, (0.38, 0.36, 0.33)), (1.0, (0.33, 0.30, 0.26))])
    n = noise(nt, L, tc(nt), 25, 10)
    c = mix_rgb(nt, L, math_node(nt, L, "MULTIPLY", n, 0.5), base, (0.15, 0.14, 0.12))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.8
    bump(nt, L, b, n, 0.4)
    return m

def m_four():
    m, nt, b, L = new_mat("Four")
    vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.feature = "DISTANCE_TO_EDGE"; vor.inputs["Scale"].default_value = 5
    L.new(tc(nt), vor.inputs["Vector"])
    edge = math_node(nt, L, "MULTIPLY", vor.outputs["Distance"], 18, clamp=True)
    v2 = nt.nodes.new("ShaderNodeTexVoronoi"); v2.inputs["Scale"].default_value = 5; L.new(tc(nt), v2.inputs["Vector"])
    st = ramp(nt, L, v2.outputs["Color"], [(0.0, (0.24, 0.21, 0.17)), (0.5, (0.36, 0.31, 0.24)), (1.0, (0.30, 0.29, 0.27))])
    c = mix_rgb(nt, L, edge, (0.18, 0.17, 0.15), st)
    soot = math_node(nt, L, "MULTIPLY", noise(nt, L, tc(nt), 3, 4), 0.5)
    c = mix_rgb(nt, L, soot, c, (0.08, 0.07, 0.06))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.95
    bump(nt, L, b, edge, 0.7)
    return m

def m_simple(name, c, rough=0.6, metal=0.0, emit=None, strength=6.0):
    m, nt, b, L = new_mat(name)
    b.inputs["Base Color"].default_value = c + (1,)
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = emit + (1,)
        b.inputs["Emission Strength"].default_value = strength
    return m

def m_noise(name, c1, c2, scale, rough, bmp=0.3):
    m, nt, b, L = new_mat(name)
    n = noise(nt, L, tc(nt), scale, 8)
    L.new(ramp(nt, L, n, [(0.3, c1), (0.7, c2)]), b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough; bump(nt, L, b, n, bmp)
    return m

def m_pain():                                      # croûte dorée, farine sur le dessus
    m, nt, b, L = new_mat("Croute")
    n = noise(nt, L, tc(nt), 20, 8)
    crust = ramp(nt, L, n, [(0.25, (0.42, 0.20, 0.06)), (0.55, (0.66, 0.38, 0.12)), (0.8, (0.80, 0.55, 0.24))])
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Normal"], sep.inputs["Vector"])
    top = math_node(nt, L, "SUBTRACT", sep.outputs["Z"], 0.75)
    top = math_node(nt, L, "MULTIPLY", top, 4.0, clamp=True)
    flour = math_node(nt, L, "MULTIPLY", top, noise(nt, L, tc(nt), 60, 4), clamp=True)
    flour = math_node(nt, L, "MULTIPLY", flour, 1.6, clamp=True)
    c = mix_rgb(nt, L, flour, crust, (0.86, 0.82, 0.74))
    L.new(c, b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.65
    bump(nt, L, b, n, 0.5)
    return m

def m_vitrail():                                   # verre ancien sertis de plomb en losanges
    m, nt, b, L = new_mat("VerrePlomb")
    b.inputs["Base Color"].default_value = (0.80, 0.86, 0.80, 1)
    b.inputs["Transmission Weight"].default_value = 1.0; b.inputs["Roughness"].default_value = 0.04
    b.inputs["IOR"].default_value = 1.5
    bump(nt, L, b, noise(nt, L, tc(nt), 6, 2), 0.08)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(tc(nt), sep.inputs["Vector"])
    h = math_node(nt, L, "ADD", sep.outputs["X"], sep.outputs["Y"])
    def diag(sign):
        s = math_node(nt, L, "ADD" if sign > 0 else "SUBTRACT", h, sep.outputs["Z"])
        s = math_node(nt, L, "MULTIPLY", s, 1 / 0.2)
        s = math_node(nt, L, "FRACT", s)
        return math_node(nt, L, "LESS_THAN", s, 0.045)
    mask = math_node(nt, L, "MAXIMUM", diag(1), diag(-1))
    lead = nt.nodes.new("ShaderNodeBsdfPrincipled")
    lead.inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1); lead.inputs["Metallic"].default_value = 0.8
    lead.inputs["Roughness"].default_value = 0.45
    mix = nt.nodes.new("ShaderNodeMixShader")
    out = nt.nodes["Material Output"]
    L.new(mask, mix.inputs["Fac"]); L.new(b.outputs["BSDF"], mix.inputs[1]); L.new(lead.outputs["BSDF"], mix.inputs[2])
    L.new(mix.outputs["Shader"], out.inputs["Surface"])
    return m

def m_fumee():
    m = bpy.data.materials.new("Fumee"); m.use_nodes = True; nt = m.node_tree; L = nt.links
    for n_ in list(nt.nodes):
        if n_.type == "BSDF_PRINCIPLED": nt.nodes.remove(n_)
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (0.55, 0.55, 0.57, 1)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(tc(nt), sep.inputs["Vector"])
    mp = nt.nodes.new("ShaderNodeMapping"); L.new(tc(nt), mp.inputs["Vector"])
    mp.inputs["Scale"].default_value = (1.6, 1.6, 0.9)
    n = noise(nt, L, mp.outputs["Vector"], 1.8, 6, 0.6)
    d = math_node(nt, L, "SUBTRACT", n, 0.48, clamp=True)
    d = math_node(nt, L, "MULTIPLY", d, 5.0)
    fade = math_node(nt, L, "SUBTRACT", 1.0, math_node(nt, L, "MULTIPLY", sep.outputs["Z"], 0.2), clamp=True)
    # rayon : plus large en montant
    r = nt.nodes.new("ShaderNodeVectorMath"); r.operation = "LENGTH"
    cx = nt.nodes.new("ShaderNodeCombineXYZ"); L.new(sep.outputs["X"], cx.inputs["X"]); L.new(sep.outputs["Y"], cx.inputs["Y"])
    L.new(cx.outputs["Vector"], r.inputs[0])
    width = math_node(nt, L, "ADD", 0.25, math_node(nt, L, "MULTIPLY", sep.outputs["Z"], 0.22))
    radial = math_node(nt, L, "SUBTRACT", 1.0, math_node(nt, L, "DIVIDE", r.outputs["Value"], width), clamp=True)
    d = math_node(nt, L, "MULTIPLY", d, fade)
    d = math_node(nt, L, "MULTIPLY", d, radial)
    L.new(d, vol.inputs["Density"])
    L.new(vol.outputs["Volume"], nt.nodes["Material Output"].inputs["Volume"])
    return m

M = {
    "moellon": m_moellon(), "taille": m_taille(), "mortier": m_mortier(), "enduit": m_enduit(),
    "torchis": m_torchis(), "tuiles": m_tuiles(), "pave": m_pave(), "pain": m_pain(),
    "vitrail": m_vitrail(), "fumee": m_fumee(), "four": m_four(),
    "chene": m_bois("Chene", (0.07, 0.045, 0.028), (0.17, 0.105, 0.06), scale=(1, 1, 0.15)),
    "planche": m_bois("Planches", (0.18, 0.11, 0.06), (0.33, 0.22, 0.12), scale=(0.15, 1, 1)),
    "osier": m_bois("Osier", (0.35, 0.24, 0.12), (0.55, 0.40, 0.20), rough=0.8, scale=(4, 4, 4)),
    "buche": m_bois("Buches", (0.20, 0.14, 0.09), (0.42, 0.32, 0.20), rough=0.9, scale=(1, 0.3, 1)),
    "volet": m_bois("Volets", (0.10, 0.20, 0.17), (0.17, 0.29, 0.25), rough=0.6, scale=(1, 1, 0.15)),
    "fer": m_simple("Fer", (0.05, 0.05, 0.05), 0.55, 1.0),
    "or": m_simple("Dorure", (0.83, 0.60, 0.22), 0.3, 1.0),
    "peinture": m_simple("Peinture", (0.86, 0.78, 0.56), 0.6),
    "toile": m_noise("Toile", (0.45, 0.41, 0.33), (0.56, 0.52, 0.43), 80, 1.0, 0.4),
    "terre": m_noise("Terre", (0.24, 0.20, 0.15), (0.36, 0.31, 0.24), 6, 1.0, 0.6),
    "herbe": m_noise("Herbe", (0.13, 0.20, 0.06), (0.26, 0.33, 0.12), 5, 1.0, 0.8),
    "tomette": m_noise("Tomettes", (0.40, 0.15, 0.08), (0.52, 0.22, 0.12), 12, 0.7),
    "flamme": m_simple("Flamme", (1.0, 0.6, 0.3), emit=(1.0, 0.55, 0.22), strength=12),
    "braise": m_simple("Braise", (0.3, 0.1, 0.0), emit=(1.0, 0.32, 0.06), strength=25),
    "fleur": m_simple("Geranium", (0.70, 0.05, 0.06), 0.6),
    "feuille": m_noise("Feuilles", (0.08, 0.20, 0.05), (0.15, 0.32, 0.08), 30, 0.7),
    "suie": m_simple("Suie", (0.03, 0.03, 0.03), 0.95),
}

ALL = []           # objets du bâtiment (exportés)
DECOR = []         # sol, rue, fumée : non exportés
def keep(o, mat, export=True):
    if o.data and hasattr(o.data, "materials"): o.data.materials.append(M[mat])
    (ALL if export else DECOR).append(o); return o

def bevel(o, w=0.012, seg=2, angle=40):
    md = o.modifiers.new("Chanfrein", "BEVEL"); md.width = w; md.segments = seg
    md.limit_method = "ANGLE"; md.angle_limit = math.radians(angle); md.harden_normals = False
    return o

# ---------------------------------------------------------------------
# Constructeurs
# ---------------------------------------------------------------------
def box(name, x0, x1, y0, y1, z0, z1, mat, bev=0.0, export=True):
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(((x0+x1)/2, (y0+y1)/2, (z0+z1)/2)) @
                          Matrix.Diagonal((x1-x0, y1-y0, z1-z0, 1)))
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if bev: bevel(o, bev)
    return keep(o, mat, export)

def beam(name, p1, p2, depth, width, mat, bev=0.012):
    p1, p2 = Vector(p1), Vector(p2); v = p2 - p1
    q = v.to_track_quat("Y", "Z")
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((p1+p2)/2) @ q.to_matrix().to_4x4() @
                          Matrix.Diagonal((depth, v.length, width, 1)))
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    if bev: bevel(o, bev)
    return keep(o, mat)

def cyl(name, loc, r, depth, mat, rot=(0, 0, 0), verts=12, bev=0.0, export=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; bpy.ops.object.transform_apply(rotation=True)
    if bev: bevel(o, bev)
    bpy.ops.object.shade_smooth_by_angle() if hasattr(bpy.ops.object, "shade_smooth_by_angle") else None
    return keep(o, mat, export)

def blob(name, loc, scale, mat, seg=16, ring=10, jitter=0.0, rot=(0, 0, 0), export=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=1, location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    if jitter:
        for v in o.data.vertices:
            v.co += Vector((U(-1, 1), U(-1, 1), U(-1, 1))) * jitter
    bpy.ops.object.shade_smooth()
    return keep(o, mat, export)

def cut(target, x0, x1, y0, y1, z0, z1):
    c = box("_coupe", x0, x1, y0, y1, z0, z1, "mortier", export=False); DECOR.remove(c)
    md = target.modifiers.new("trou", "BOOLEAN"); md.object = c; md.operation = "DIFFERENCE"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=md.name)
    bpy.data.objects.remove(c)

class Lot:
    """Beaucoup de petites pièces dans un seul objet (pierres, tuiles, pavés), avec attributs par pièce."""
    def __init__(self, name, mat, attrs=("var",)):
        self.name, self.mat, self.bm = name, mat, bmesh.new()
        self.layers = {a: self.bm.faces.layers.float.new(a) for a in attrs}
        self.n = 0
    def add(self, matrix, jitter=0.0, **vals):
        r = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)
        for v in r["verts"]:
            if jitter: v.co += Vector((U(-1, 1), U(-1, 1), U(-1, 1)))*jitter
        faces = {f for v in r["verts"] for f in v.link_faces}
        vals.setdefault("var", rnd.random())
        for k, v in vals.items():
            lay = self.layers[k]
            for f in faces: f[lay] = v
        self.n += 1
    def done(self, bev=0.0, seg=2, angle=40, export=True):
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        o = bpy.data.objects.new(self.name, me); col.objects.link(o)
        if bev: bevel(o, bev, seg, angle)
        return keep(o, self.mat, export)

def basis(x, y, z, pos):
    m = Matrix((x, y, z)).transposed().to_4x4(); m.translation = pos; return m

