# Caméras, studio (fond gris clair), soleil et réglages de rendu ; signature de la scène source.
import bpy, math, hashlib
from mathutils import Vector, Euler
from bf_lib import coll, mat

VIEWS = {
    # nom : (type, position, cible, objectif mm ou échelle ortho, résolution, azimut et hauteur du soleil)
    "01_facade_orthographique":     ("ORTHO", (7.35, -40.0, 5.45), (7.35, 0.0, 5.45), 17.4, (2560, 1920), (-35, 42)),
    "02_cote_droit_orthographique": ("ORTHO", (40.0, 5.15, 5.45), (0.0, 5.15, 5.45), 15.6, (2560, 1920), (60, 40)),
    "03_cote_gauche_orthographique": ("ORTHO", (-40.0, 5.15, 5.45), (0.0, 5.15, 5.45), 15.6, (2560, 1920), (-120, 40)),
    "04_arriere_orthographique":    ("ORTHO", (7.35, 45.0, 5.45), (7.35, 0.0, 5.45), 17.4, (2560, 1920), (150, 42)),
    "05_vue_trois_quarts":          ("PERSP", (-5.6, -12.8, 1.70), (6.3, 3.0, 4.75), 25.0, (2560, 1600), (-40, 38)),
    "06_vue_de_dessus_toiture":     ("TOP", (7.35, 5.0, 40.0), (7.35, 5.0, 0.0), 17.2, (2560, 2048), (-30, 55)),
    "07_interieur_boutique":        ("PERSP", (7.25, 1.30, 1.68), (9.28, 5.87, 1.33), 24.0, (2560, 1600), (-25, 30)),
}

def look(cam, loc, tgt):
    cam.location = loc
    d = Vector(tgt) - Vector(loc)
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = d.to_track_quat("-Z", "Y")

def setup(scene):
    """Idempotent : crée studio, ciel, soleil et caméras s'ils n'existent pas."""
    cc = coll("Cameras"); ce = coll("Eclairage"); cs = coll("Studio_Rendu")
    if "Studio_Sol" not in bpy.data.objects:
        me = bpy.data.meshes.new("Studio_Sol")
        me.from_pydata([(-150, -150, -0.003), (150, -150, -0.003), (150, 150, -0.003), (-150, 150, -0.003)], [], [(0, 1, 2, 3)])
        me.materials.append(mat("studio"))
        o = bpy.data.objects.new("Studio_Sol", me); cs.objects.link(o)
    w = bpy.data.worlds.get("Monde_Studio")
    if w is None:
        w = bpy.data.worlds.new("Monde_Studio"); w.use_nodes = True
        nt = w.node_tree; L = nt.links; out = nt.nodes["World Output"]; bg = nt.nodes["Background"]
        bg.inputs["Color"].default_value = (0.80, 0.84, 0.92, 1); bg.inputs["Strength"].default_value = 0.7
        bg2 = nt.nodes.new("ShaderNodeBackground"); bg2.name = "FondGris"
        bg2.inputs["Color"].default_value = (0.70, 0.70, 0.70, 1); bg2.inputs["Strength"].default_value = 1.0
        lp = nt.nodes.new("ShaderNodeLightPath"); mx = nt.nodes.new("ShaderNodeMixShader")
        L.new(lp.outputs["Is Camera Ray"], mx.inputs["Fac"]); L.new(bg.outputs["Background"], mx.inputs[1]); L.new(bg2.outputs["Background"], mx.inputs[2])
        L.new(mx.outputs["Shader"], out.inputs["Surface"])
    scene.world = w
    if "Soleil" not in bpy.data.objects:
        sd = bpy.data.lights.new("Soleil", "SUN"); sd.angle = math.radians(8); sd.energy = 3.6; sd.color = (1.0, 0.96, 0.90)
        so = bpy.data.objects.new("Soleil", sd); ce.objects.link(so)
    for name, v in VIEWS.items():
        cn = "CAM_" + name
        if cn not in bpy.data.objects:
            cd = bpy.data.cameras.new(cn); co = bpy.data.objects.new(cn, cd); cc.objects.link(co)
        co = bpy.data.objects[cn]; cd = co.data
        kind, loc, tgt, f, res, sun = v
        look(co, loc, tgt)
        cd.clip_start = 0.05 if kind == "PERSP" else 1.0; cd.clip_end = 200
        if kind in ("ORTHO", "TOP"):
            cd.type = "ORTHO"; cd.ortho_scale = f
        else:
            cd.type = "PERSP"; cd.lens = f; cd.sensor_width = 36
        co["resolution"] = list(res); co["soleil"] = list(sun)
    r = scene.render
    r.engine = "CYCLES"; scene.cycles.device = "CPU"
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6; scene.cycles.diffuse_bounces = 3; scene.cycles.volume_bounces = 0; scene.cycles.volume_step_rate = 4.0; scene.cycles.glossy_bounces = 2
    scene.cycles.transmission_bounces = 6; scene.cycles.transparent_max_bounces = 8
    scene.cycles.caustics_reflective = False; scene.cycles.caustics_refractive = False
    scene.cycles.sample_clamp_indirect = 8.0
    scene.view_settings.view_transform = "AgX"
    try: scene.view_settings.look = "AgX - Medium High Contrast"
    except Exception: pass
    r.image_settings.file_format = "PNG"; r.image_settings.color_mode = "RGB"
    for cn in ("LOD", "Collisions", "Materiaux"):
        c = bpy.data.collections.get(cn)
        if c: c.hide_render = True

def apply_view(scene, name, scale=1.0):
    kind, loc, tgt, f, res, sun = VIEWS[name]
    cam = bpy.data.objects["CAM_" + name]; scene.camera = cam
    scene.render.resolution_x = int(res[0]*scale); scene.render.resolution_y = int(res[1]*scale)
    scene.render.resolution_percentage = 100
    az, el = map(math.radians, sun)
    so = bpy.data.objects["Soleil"]
    to_sun = Vector((math.sin(az)*math.cos(el), -math.cos(az)*math.cos(el), math.sin(el)))
    so.rotation_mode = "QUATERNION"; so.rotation_quaternion = (-to_sun).to_track_quat("-Z", "Y")
    interior = name.startswith("07")
    so.data.energy = 3.2 if not interior else 4.5
    w = scene.world.node_tree.nodes
    w["Background"].inputs["Strength"].default_value = 0.7 if not interior else 1.6
    scene.view_settings.exposure = -0.55 if not interior else -0.1
    fu = bpy.data.objects.get("Fumee_Cheminee")
    if fu: fu.hide_render = name.startswith(("06", "07"))      # pas de fumée sur la vue de dessus (masquerait la cheminée) ni dedans
    st = bpy.data.objects.get("Studio_Sol")
    if st: st.hide_render = False
    return cam

def signature():
    """Empreinte de la géométrie du bâtiment (hors studio, LOD, collisions) : identique pour les 7 vues."""
    h = hashlib.sha1(); n = 0; nv = 0
    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        if o.type != "MESH" or o.name.startswith(("Studio", "LOD", "COL_")): continue
        if any(c.name in ("LOD", "Collisions", "Studio_Rendu", "Materiaux") for c in o.users_collection): continue
        me = o.data; n += 1; nv += len(me.vertices)
        s = sum((abs(v.co.x) + 2*abs(v.co.y) + 3*abs(v.co.z)) for i_, v in enumerate(me.vertices) if i_ % 7 == 0)
        h.update(f"{o.name}:{len(me.vertices)}:{len(me.polygons)}:{s:.3f};".encode())
    return f"{n}obj-{nv}v-{h.hexdigest()[:12]}"
