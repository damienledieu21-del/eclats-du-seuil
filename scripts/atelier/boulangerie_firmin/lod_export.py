# Optimisation Unity : LOD1/LOD2 (extérieur seul, atlas cuit), collisions simplifiées, textures de matières
# raccordables pour le LOD0 (PC, avec intérieur), exports GLB et FBX, mesures (triangles, objets, matières).
#   /tmp/bvenv/bin/python -u -I scripts/lod_export.py lod       -> LOD1, LOD2, collisions dans Boulangerie_Firmin.blend
#   /tmp/bvenv/bin/python -u -I scripts/lod_export.py tuiles    -> export/textures/matieres/*.png (1 Kio, raccordables)
#   /tmp/bvenv/bin/python -u -I scripts/lod_export.py cuisson   -> atlas couleur + normale des LOD1/LOD2
#   /tmp/bvenv/bin/python -u -I scripts/lod_export.py export    -> export/*.glb, *.fbx, export_meta.json
import sys, os, math, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import bf_plan as P
from bf_lib import *

STAGE = sys.argv[1] if len(sys.argv) > 1 else "lod"
BLEND = os.path.join(ROOT, "Boulangerie_Firmin.blend")
EXP = os.path.join(ROOT, "export"); TEX = os.path.join(EXP, "textures"); TEXM = os.path.join(TEX, "matieres")
for d in (EXP, TEX, TEXM): os.makedirs(d, exist_ok=True)
T0 = time.time()
def log(*a): print(f"[{time.time() - T0:5.0f}s]", *a, flush=True)
open_blend(BLEND)
sc = bpy.context.scene
T48, T30 = P.T48, P.T30

EXT_COLLS = ("Architecture_Boutique", "Architecture_Fournil", "Toitures", "Menuiseries", "Four", "Props_Exterieurs")
LOD0_COLLS = EXT_COLLS + ("Props_Interieurs",)
def in_colls(o, names): return any(c.name in names for c in o.users_collection)
def meshes(names): return [o for o in bpy.data.objects if o.type == "MESH" and in_colls(o, names)]
def clear_coll(name):
    for o in list(coll(name).objects): bpy.data.objects.remove(o, do_unlink=True)

# =====================================================================
if STAGE == "lod":
    clear_coll("LOD"); clear_coll("Collisions")
    seed(5)
    def lod_pack(detail):
        pk = Pack("LOD1_Boulangerie_Exterieur" if detail else "LOD2_Boulangerie_Exterieur", ["mortier"], "LOD")
        B = pk.box
        Z = P.Z_SABL
        B(0, 9.5, 0, 8.5, -0.02, 2.85); B(0, 9.5, 0, 8.5, 2.85, 3.05)                             # rez boutique
        B(0, 9.5, -P.ENC, 8.5, 3.05, Z)                                                           # étage
        B(10.0, 14.5, 0, 8.0, -0.02, P.F_EAVE)                                                    # fournil
        # mur séparatif et ses pignons, pignon gauche, pignon droit du fournil
        pk.prism([Vector((9.5, y, z)) for y, z in [(0, 0), (8.5, 0), (8.5, Z), (P.RIDGE_B_Y, P.RIDGE_B_Z), (0, Z + 0.18*T48)]], Vector((0.5, 0, 0)))
        pk.prism([Vector((0.0, y, z)) for y, z in [(-P.ENC, Z), (8.5, Z), (P.RIDGE_B_Y, P.RIDGE_B_Z)]], Vector((0.2, 0, 0)))
        pk.prism([Vector((14.3, y, z)) for y, z in [(0, P.F_EAVE), (8.0, P.F_EAVE), (4.0, P.RIDGE_F_Z)]], Vector((0.2, 0, 0)))
        # toits : un pavé par pan (dessous + tuiles), faîtage
        def pan_box(xa, xb, ridge, dsl, nrm, Ls, T=0.14):
            pk.cube(frame_m(ridge + dsl*(Ls/2) + nrm*(T/2), Vector((xb - xa, 0, 0)), dsl*Ls, nrm*T))
        for (xa, xb, y0, y1, ze, pitch, over) in ((P.ROOF_B_X0, P.ROOF_B_X1, P.ROOF_B_Y0, P.ROOF_B_Y1, Z, 48, P.ROOF_B_OVER),
                                                   (P.ROOF_F_X0, P.ROOF_F_X1, 0.0, 8.0, P.F_EAVE, 30, P.ROOF_F_OVER)):
            p = math.radians(pitch); yc, half = (y0 + y1)/2, (y1 - y0)/2; zr = ze + half*math.tan(p); Ls = half/math.cos(p) + over
            for s in (-1, 1):
                pan_box(xa, xb, Vector(((xa + xb)/2, yc, zr)), Vector((0, s*math.cos(p), -math.sin(p))), Vector((0, s*math.sin(p), math.cos(p))), Ls)
            if detail: pk.cyl((xa, yc, zr + 0.16), (xb, yc, zr + 0.16), 0.13, seg=6)
        pa = math.atan2(P.APP_Z0 - P.APP_Z1, P.APP_Y1 - P.APP_Y0)
        pan_box(P.APP_X0, P.APP_X1, Vector(((P.APP_X0 + P.APP_X1)/2, 8.0, P.APP_Z0)), Vector((0, math.cos(pa), -math.sin(pa))), Vector((0, math.sin(pa), math.cos(pa))),
                math.hypot(3.25, P.APP_Z0 - P.APP_Z1) + 0.25, 0.12)
        L_ = P.LUC; xl0, xl1 = L_["xc"] - L_["w"]/2, L_["xc"] + L_["w"]/2
        yce = P.ROOF_B_Y0 + (L_["z_eave"] - Z)/T48
        pk.prism([Vector((x, L_["y_face"], z)) for x, z in [(xl0, L_["z_face"] - 0.1), (xl1, L_["z_face"] - 0.1), (xl1, L_["z_eave"]), (L_["xc"], L_["z_ridge"]), (xl0, L_["z_eave"])]],
                 Vector((0, yce - L_["y_face"] + 0.6, 0)))
        C = P.CHEM; B(C["x0"], C["x1"], C["y0"], C["y1"], 3.4, C["z_top"] + 0.35)
        FB = P.FOUR_BASE; B(FB["x0"], FB["x1"], FB["y0"], FB["y1"], -0.02, FB["z1"])
        res = bmesh.ops.create_uvsphere(pk.bm, u_segments=16 if detail else 10, v_segments=8 if detail else 6, radius=1.0)
        for v in res["verts"]: v.co = Vector((P.FOUR_C[0] + v.co.x*P.FOUR_R[0], P.FOUR_C[1] + v.co.y*P.FOUR_R[1], P.FOUR_DOME_Z0 + max(v.co.z, -0.05)*P.FOUR_R[2]))
        pk._fin(res["verts"], 0, False, 0.0, None, {})
        for xp in (P.APP_X0 + 0.12, P.APP_X1 - 0.12): B(xp - 0.08, xp + 0.08, 10.77, 10.93, 0, 2.2)
        if detail:                                                                                   # gros accessoires extérieurs
            v1 = P.ouv("V1")
            pk.cube(frame_m(Vector(((v1["a0"] + v1["a1"])/2, -0.56, 2.56)), Vector((v1["a1"] - v1["a0"] + 0.5, 0, 0)), Vector((0, -1.07, -0.48)), Vector((0, -0.04, 0.09))))
            B(v1["a0"] - 0.25, v1["a1"] + 0.25, -1.11, -1.09, 2.12, 2.33)                         # lambrequin
            B(v1["a0"], v1["a1"], -0.72, -0.08, 0.0, 0.95)                                          # étal
            B(14.52, 15.12, 0.52, 4.98, -0.02, 1.48)                                                # bûcher
            B(0.38, 0.46, -1.42, -0.48, 3.25, 4.10)                                                 # enseigne
            B(-0.58, -0.18, 3.05, 4.55, 0.0, 0.48)                                                  # banc
            B(8.25, 8.85, -1.15, -0.75, 0.0, 1.0)                                                   # ardoise
            for (x, y, r) in ((9.18, -0.45, 0.3), (8.20, 9.05, 0.32), (8.85, 9.25, 0.3)): pk.cyl((x, y, 0), (x, y, 0.9), r, seg=8)
            B(10.25, 11.25, -0.85, -0.20, 0.0, 0.62)                                                # sacs et caisse
            B(-0.06, 0.0, 0.0, 1.3, 0.0, 4.4); B(0.0, 0.6, -0.06, 0.0, 0.0, 2.7)                     # lierre
            for i in ("E1", "E2"):
                o = P.ouv(i); B(o["a0"] - 0.12, o["a1"] + 0.12, -0.48, -0.22, 3.62, 3.95)
        o = pk.done()
        # faces jamais vues : dessous au sol, dessus cachés sous les toits
        bm = bmesh.new(); bm.from_mesh(o.data); dead = []
        for f in bm.faces:
            c = f.calc_center_median(); n = f.normal
            if n.z < -0.9 and c.z < 0.05: dead.append(f)
            elif n.z > 0.9 and f.calc_area() > 1.0 and any(abs(c.z - zz) < 0.03 for zz in (2.85, 3.05, P.Z_SABL, P.F_EAVE)): dead.append(f)
            elif n.z < -0.9 and any(abs(c.z - zz) < 0.03 for zz in (2.85, 3.05)) and c.y > -0.1: dead.append(f)
            elif abs(n.x) > 0.9 and ((abs(c.x - 9.5) < 0.02) or (abs(c.x - 10.0) < 0.02 and n.x < 0) or (abs(c.x - 0.2) < 0.02 and n.x > 0)
                                     or (abs(c.x - 14.3) < 0.02 and n.x < 0)): dead.append(f)          # faces internes (mur séparatif, pignons)
        bmesh.ops.delete(bm, geom=dead, context="FACES"); bm.to_mesh(o.data); bm.free()
        log(o.name, "faces cachées supprimées", len(dead))
        bpy.context.view_layer.objects.active = o
        for ob in bpy.context.selected_objects: ob.select_set(False)
        o.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        o["bf_role"] = "lod"
        return o
    l1 = lod_pack(True); l2 = lod_pack(False)
    # collisions simplifiées (pavés convexes ; portes P1, B1, R2 laissées libres ; PF fermée)
    cp = []
    def col(name, x0, x1, y0, y1, z0, z1):
        pk = Pack("COL_" + name, ["mortier"], "Collisions"); pk.box(x0, x1, y0, y1, z0, z1); o = pk.done(); o["bf_role"] = "collision"; o.display_type = "WIRE"; cp.append(o)
    p1, b1, r2 = P.ouv("P1"), P.ouv("B1"), P.ouv("R2")
    col("Sol_Boutique", 0, 9.5, 0, 8.5, -0.2, P.SOL); col("Sol_Fournil", 10, 14.5, 0, 8, -0.2, P.SOL)
    col("Mur_Facade_G", 0, p1["a0"], 0, 0.5, 0, 3.05); col("Mur_Facade_D", p1["a1"], 9.5, 0, 0.5, 0, 3.05); col("Mur_Facade_Linteau", p1["a0"], p1["a1"], 0, 0.5, p1["z1"] + 0.02, 3.05)
    col("Mur_Gauche", 0, 0.5, 0, 8.5, 0, 3.05)
    col("Mur_Arriere_G", 0, r2["a0"], 8.0, 8.5, 0, 3.05); col("Mur_Arriere_D", r2["a1"], 9.5, 8.0, 8.5, 0, 3.05); col("Mur_Arriere_Linteau", r2["a0"], r2["a1"], 8.0, 8.5, r2["z1"], 3.05)
    col("Separatif_Avant", 9.5, 10, 0, b1["a0"], 0, P.Z_SABL); col("Separatif_Arriere", 9.5, 10, b1["a1"], 8.5, 0, P.Z_SABL); col("Separatif_Linteau", 9.5, 10, b1["a0"], b1["a1"], b1["z1"], P.Z_SABL)
    col("Fournil_Facade", 10, 14.5, 0, 0.5, 0, P.F_EAVE); col("Fournil_Droit", 14, 14.5, 0, 8, 0, P.F_EAVE); col("Fournil_Arriere", 10, 14.5, 7.5, 8, 0, P.F_EAVE)
    col("Plancher_Etage", 0, 9.5, -P.ENC, 8.5, 2.85, 3.08)
    col("Etage_Murs_Facade", 0, 9.5, -P.ENC, 0.02, 3.08, P.Z_SABL); col("Etage_Murs_Gauche", 0, 0.2, 0, 8.5, 3.08, P.Z_SABL); col("Etage_Murs_Arriere", 0, 9.5, 8.3, 8.5, 3.08, P.Z_SABL)
    col("Toit_Boutique", P.ROOF_B_X0, P.ROOF_B_X1, -0.6, 8.9, P.Z_SABL, P.RIDGE_B_Z)
    col("Toit_Fournil", 10, 14.85, -0.4, 8.4, P.F_EAVE, P.RIDGE_F_Z)
    col("Four", P.FOUR_BASE["x0"], P.FOUR_BASE["x1"], 8.0, 11.0, 0, P.FOUR_DOME_Z0 + P.FOUR_R[2])
    col("Comptoir", 3.0, 6.2, 4.75, 5.4, 0, 1.0); col("Presentoir", 6.25, 7.65, 4.75, 5.4, 0, 1.34)
    col("Etageres_Fond", 1.7, 7.9, 7.45, 8.0, 0, 2.35); col("Table_Fournil", 12.7, 13.85, 2.0, 4.6, 0, 0.92); col("Petrin", 13.17, 13.95, 5.88, 7.42, 0, 0.96)
    col("Bucher", 14.52, 15.12, 0.52, 4.98, 0, 1.48); col("Etal", 1.05, 5.55, -0.72, -0.08, 0, 0.95)
    # escalier : pente simplifiée (pavé incliné)
    E = P.ESC; L_ = math.hypot(E["y1"] - E["y0"], P.Z_ETAGE - P.SOL); a = math.atan2(P.Z_ETAGE - P.SOL, E["y1"] - E["y0"])
    pk = Pack("COL_Escalier_Rampe", ["mortier"], "Collisions")
    pk.cube(Matrix.Translation(((E["x0"] + E["x1"])/2, (E["y0"] + E["y1"])/2, (P.SOL + P.Z_ETAGE)/2 - 0.1)) @ Matrix.Rotation(a, 4, "X") @ Matrix.Diagonal((E["x1"] - E["x0"], L_, 0.2, 1)))
    o = pk.done(); o["bf_role"] = "collision"; o.display_type = "WIRE"; cp.append(o)
    for c_ in ("LOD", "Collisions"): coll(c_).hide_render = True
    for lc in bpy.context.view_layer.layer_collection.children:
        if lc.name in ("LOD", "Collisions"): lc.hide_viewport = True
    log("LOD1", tri_count([l1]), "triangles ; LOD2", tri_count([l2]), "; collisions", len(cp), "pavés", tri_count(cp), "triangles")
    save_blend(BLEND)

# =====================================================================
elif STAGE == "tuiles":
    # une texture raccordable (couleur + normale, 1024 px pour 2 m) par matière procédurale du LOD0
    SIZE = int(os.environ.get("TUILE", "1024"))
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 1
    bk = sc.render.bake; bk.use_selected_to_active = False; bk.margin = 0
    used = set()
    for o in meshes(LOD0_COLLS):
        for m in o.data.materials:
            if m: used.add(m.name)
    Z0 = {"MAT_Enduit": 5.0, "MAT_EnduitInterieur": 2.0, "MAT_EnduitFournil": 1.0}
    done = 0
    for name in sorted(used):
        m = bpy.data.materials[name]
        b = m.node_tree.nodes.get("Principled BSDF")
        if b is None or not b.inputs["Base Color"].is_linked: continue           # matières unies : facteurs seulement
        out_c = os.path.join(TEXM, name[4:] + "_couleur.png"); out_n = os.path.join(TEXM, name[4:] + "_normale.png")
        if os.path.exists(out_c) and os.path.exists(out_n): continue
        z0 = Z0.get(name, 0.0)
        me = bpy.data.meshes.new("plan"); me.from_pydata([(0, 0, z0), (2, 0, z0), (2, 0, z0 + 2), (0, 0, z0 + 2)], [], [(0, 1, 2, 3)])
        uv = me.uv_layers.new(name="UVMap")
        for li, (u, v) in zip(range(4), ((0, 0), (1, 0), (1, 1), (0, 1))): uv.data[li].uv = (u, v)
        a = me.attributes.new("var", "FLOAT", "FACE"); a.data[0].value = 1.0
        me.attributes.new("moss", "FLOAT", "FACE").data[0].value = 0.0
        me.materials.append(m)
        po = bpy.data.objects.new("plan", me); sc.collection.objects.link(po)
        for ob in bpy.context.selected_objects: ob.select_set(False)
        po.select_set(True); bpy.context.view_layer.objects.active = po
        nt = m.node_tree
        def target(nm, noncolor):
            img = bpy.data.images.new(nm, SIZE, SIZE, alpha=False); img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
            n = nt.nodes.new("ShaderNodeTexImage"); n.image = img
            for x in nt.nodes: x.select = False
            n.select = True; nt.nodes.active = n; return img, n
        trans = b.inputs["Transmission Weight"].default_value
        img_c, nc = target(name + "_c", False)
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
        img_n, nn = target(name + "_n", True); bk.normal_space = "TANGENT"
        bpy.ops.object.bake(type="NORMAL")
        for img, path in ((img_c, out_c), (img_n, out_n)):
            px = np.array(img.pixels[:], dtype=np.float32).reshape(SIZE, SIZE, 4)
            t = np.abs(np.linspace(-1, 1, SIZE)); w1 = np.clip((1.0 - t)*2.0, 0, 1)     # raccord en deux passes :
            wx = w1[None, :, None]; px = px*wx + np.roll(px, SIZE//2, 1)*(1 - wx)        # horizontale, puis
            wy = w1[:, None, None]; px = px*wy + np.roll(px, SIZE//2, 0)*(1 - wy)        # verticale
            img.pixels[:] = px.ravel(); img.filepath_raw = path; img.file_format = "PNG"; img.save()
        nt.nodes.remove(nc); nt.nodes.remove(nn)
        bpy.data.objects.remove(po); bpy.data.meshes.remove(me)
        done += 1; log("tuile", name, SIZE)
    log("tuiles cuites :", done)

# =====================================================================
elif STAGE == "cuisson":
    SIZE_C = int(os.environ.get("BAKE_C", "2048")); SIZE_N = int(os.environ.get("BAKE_N", "2048"))
    l1 = bpy.data.objects["LOD1_Boulangerie_Exterieur"]; l2 = bpy.data.objects["LOD2_Boulangerie_Exterieur"]
    high_src = [o for o in meshes(EXT_COLLS) if not o.name.startswith(("Boutique_Sol", "Fournil_Sol", "Etage_Plancher", "Boutique_Escalier"))]
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new(); mats = []
    for o in high_src:                                         # tout le détail en un seul objet (cuisson bien plus rapide)
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        remap = []
        for m in me.materials:
            if m.name not in [x.name for x in mats]: mats.append(m)
            remap.append([x.name for x in mats].index(m.name))
        for p in me.polygons: p.material_index = remap[p.material_index] if remap else 0
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    hme = bpy.data.meshes.new("detail_cuisson"); bm.to_mesh(hme); bm.free()
    for m in mats: hme.materials.append(m)
    high = bpy.data.objects.new("detail_cuisson", hme); sc.collection.objects.link(high)
    log("détail fusionné", len(hme.polygons), "faces")
    vb = bpy.data.materials["MAT_Verre"].node_tree.nodes["Principled BSDF"]; vl = bpy.data.materials["MAT_VerrePlomb"].node_tree.nodes["Principled BSDF"]
    for b_ in (vb, vl): b_.inputs["Base Color"].default_value = (0.04, 0.05, 0.05, 1); b_.inputs["Transmission Weight"].default_value = 0.0
    am = bpy.data.materials.get("MAT_LOD_Atlas") or bpy.data.materials.new("MAT_LOD_Atlas"); am.use_nodes = True
    for o in (l1, l2):
        o.data.materials.clear(); o.data.materials.append(am)
    nt = am.node_tree
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE": nt.nodes.remove(n)
    def target(nm, size, noncolor):
        img = bpy.data.images.get(nm)
        if img: bpy.data.images.remove(img)
        img = bpy.data.images.new(nm, size, size, alpha=False); img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = img
        for x in nt.nodes: x.select = False
        n.select = True; nt.nodes.active = n; return img
    coll("LOD").hide_render = False; l1.hide_render = False
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 1
    bk = sc.render.bake; bk.use_selected_to_active = True; bk.use_cage = False; bk.cage_extrusion = 0.14; bk.max_ray_distance = 0.75; bk.margin = 8
    for ob in bpy.context.selected_objects: ob.select_set(False)
    high.select_set(True); l1.select_set(True); bpy.context.view_layer.objects.active = l1
    img_c = target("Boulangerie_LOD_Couleur", SIZE_C, False)
    bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
    img_c.filepath_raw = os.path.join(TEX, "Boulangerie_LOD_Couleur.png"); img_c.file_format = "PNG"; img_c.save(); log("atlas couleur", SIZE_C)
    img_n = target("Boulangerie_LOD_Normale", SIZE_N, True); bk.normal_space = "TANGENT"
    bpy.ops.object.bake(type="NORMAL")
    img_n.filepath_raw = os.path.join(TEX, "Boulangerie_LOD_Normale.png"); img_n.file_format = "PNG"; img_n.save(); log("atlas normale", SIZE_N)
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE": nt.nodes.remove(n)
    b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.85
    tc_ = nt.nodes.new("ShaderNodeTexImage"); tc_.image = bpy.data.images.load(os.path.join(TEX, "Boulangerie_LOD_Couleur.png"), check_existing=True)
    nt.links.new(tc_.outputs["Color"], b.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(os.path.join(TEX, "Boulangerie_LOD_Normale.png"), check_existing=True); tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    for b_ in (vb,): b_.inputs["Base Color"].default_value = (0.86, 0.90, 0.86, 1); b_.inputs["Transmission Weight"].default_value = 1.0
    vl.inputs["Base Color"].default_value = (0.80, 0.86, 0.80, 1); vl.inputs["Transmission Weight"].default_value = 1.0
    bpy.data.objects.remove(high); bpy.data.meshes.remove(hme)
    # LOD2 : même atlas, maillage réduit (UV conservées)
    old = l2.data; l2.data = l1.data.copy(); l2.data.name = "LOD2_Boulangerie_Exterieur"; bpy.data.meshes.remove(old)
    md = l2.modifiers.get("Reduction") or l2.modifiers.new("Reduction", "DECIMATE"); md.ratio = 0.45; md.use_collapse_triangulate = True
    coll("LOD").hide_render = True
    save_blend(BLEND)
    log("cuisson terminée")

# =====================================================================
elif STAGE == "export":
    dg = bpy.context.evaluated_depsgraph_get()
    tmp = coll("_EXPORT"); tmp.hide_render = True
    def box_uv(me, size=2.0):
        uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
        for p in me.polygons:
            n = p.normal; ax = max(range(3), key=lambda i: abs(n[i]))
            for li in p.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co
                u, v = ((co.y, co.z) if ax == 0 else (co.x, co.z) if ax == 1 else (co.x, co.y))
                uv.data[li].uv = (u/size, v/size)
    def ramp_eval(stops, t):
        if t <= stops[0][0]: return stops[0][1]
        for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
            if t <= p1:
                f = (t - p0)/(p1 - p0) if p1 > p0 else 0
                return tuple(c0[i] + (c1[i] - c0[i])*f for i in range(3))
        return stops[-1][1]
    WOOD = {"MAT_Chene", "MAT_ChenePatine", "MAT_Planches", "MAT_Plancher", "MAT_Osier", "MAT_Buches", "MAT_VoletsSauge"}
    def vcol(me):
        """Teinte propre à chaque pièce (attribut var) -> couleur de sommet, multipliée par la texture (COLOR_0 en glTF)."""
        if "var" not in me.attributes: return
        var = me.attributes["var"]
        if var.domain != "FACE": return
        ca = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
        cols = np.ones((len(me.loops), 4), dtype=np.float32)
        for p in me.polygons:
            m = me.materials[p.material_index].name if me.materials else ""
            t = var.data[p.index].value; r = (1, 1, 1)
            key = m[4:]
            if key in VAR_RAMPS:
                c = ramp_eval(VAR_RAMPS[key], t); cm = ramp_eval(VAR_RAMPS[key], 1.0)
                r = tuple(min(1.0, c[i]/max(cm[i], 1e-3)) for i in range(3))
            elif m in WOOD:
                k = (0.78 + 0.34*t)/1.12; r = (k, k, k)
            for li in p.loop_indices: cols[li, :3] = r
        ca.data.foreach_set("color", cols.ravel())
        me.color_attributes.active_color = ca
    EXPM = {}
    def exp_mat(src):
        if src.name in EXPM: return EXPM[src.name]
        b0 = src.node_tree.nodes.get("Principled BSDF") if src.node_tree else None
        m = bpy.data.materials.new("EXP_" + src.name[4:]); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
        pc = os.path.join(TEXM, src.name[4:] + "_couleur.png"); pn = os.path.join(TEXM, src.name[4:] + "_normale.png")
        if b0 is not None:
            for k in ("Roughness", "Metallic", "Transmission Weight", "IOR", "Emission Strength", "Coat Weight"):
                try: b.inputs[k].default_value = b0.inputs[k].default_value
                except Exception: pass
            for k in ("Base Color", "Emission Color"):
                b.inputs[k].default_value = b0.inputs[k].default_value
        if os.path.exists(pc):
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(pc, check_existing=True); nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        if os.path.exists(pn):
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(pn, check_existing=True); t.image.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(t.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
        if src.name == "MAT_Fumee": b.inputs["Alpha"].default_value = 0.0
        EXPM[src.name] = m; return m
    ROLE = {}
    def copy_lod0(o, suffix=""):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        me.name = o.name + suffix
        box_uv(me); vcol(me)
        for i, m in enumerate(me.materials): me.materials[i] = exp_mat(m)
        c = bpy.data.objects.new(o.name + suffix, me); tmp.objects.link(c)
        for k in ("bf_role", "bf_desactivable", "ouverture_id", "texte", "lucarne_id", "cheminee_id", "four_id"):
            if k in o: c[k] = o[k]
        c["collection_source"] = o.users_collection[0].name
        return c
    def select(objs):
        for ob in bpy.context.view_layer.objects: ob.select_set(False)
        for ob in objs: ob.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
    def bbox(objs):
        cs = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        return [min(c.x for c in cs), max(c.x for c in cs), min(c.y for c in cs), max(c.y for c in cs), min(c.z for c in cs), max(c.z for c in cs)]
    def dims(b): return [round(b[1] - b[0], 3), round(b[3] - b[2], 3), round(b[5] - b[4], 3)]
    GLTF = dict(export_format="GLB", use_selection=True, export_apply=True, export_yup=True, export_texcoords=True, export_normals=True,
                export_materials="EXPORT", export_image_format="JPEG", export_jpeg_quality=88, export_extras=True)
    def gltf(path, objs, **kw):
        select(objs); args = dict(GLTF); args.update(kw)
        try: bpy.ops.export_scene.gltf(filepath=path, export_vertex_color="ACTIVE", **args)
        except TypeError: bpy.ops.export_scene.gltf(filepath=path, **args)
    FBX = dict(use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
               axis_forward="-Z", axis_up="Y", object_types={"MESH"}, mesh_smooth_type="FACE", path_mode="RELATIVE", embed_textures=False,
               use_custom_props=True, colors_type="SRGB")
    meta = dict(date=time.strftime("%Y-%m-%d %H:%M"), repere="Blender Z haut ; glTF Y haut ; FBX : axe avant -Z, haut Y, échelle appliquée (1 unité = 1 m)",
                fichiers={}, lod={})
    # ---- LOD0 (PC) : tout le détail, intérieur compris, accessoires séparés
    src0 = meshes(LOD0_COLLS)
    log("LOD0 :", len(src0), "objets source")
    c0 = [copy_lod0(o) for o in src0]
    static0 = [c for c in c0 if not c.name.startswith(("Prop_", "Props_"))]
    props0 = [c for c in c0 if c.name.startswith(("Prop_", "Props_"))]
    t_static, t_props = tri_count(static0), tri_count(props0)
    mats0 = sorted({m.name for c in c0 for m in c.data.materials})
    b0 = bbox(c0)
    p = os.path.join(EXP, "Boulangerie_Firmin_LOD0_PC.glb"); gltf(p, c0)
    meta["fichiers"]["Boulangerie_Firmin_LOD0_PC.glb"] = dict(dims=dims(b0), textures_min=10, octets=os.path.getsize(p))
    meta["lod"]["LOD0_PC"] = dict(triangles=t_static + t_props, triangles_architecture=t_static, triangles_accessoires=t_props, objets=len(c0),
                                  accessoires_desactivables=len(props0), matieres=len(mats0), contenu="architecture + intérieur + accessoires (Props_* séparés)")
    log("LOD0", t_static + t_props, "triangles", len(c0), "objets", len(mats0), "matières")
    ext0 = [c for c in static0 if c["collection_source"] in EXT_COLLS]
    meta["lod"]["LOD0_exterieur_seul"] = dict(triangles=tri_count(ext0) + tri_count([c for c in props0 if c["collection_source"] == "Props_Exterieurs"]))
    # ---- LOD1, LOD2 : extérieur, atlas
    l1 = bpy.data.objects["LOD1_Boulangerie_Exterieur"]; l2 = bpy.data.objects["LOD2_Boulangerie_Exterieur"]
    for o, nm in ((l1, "LOD1"), (l2, "LOD2")):
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg)); me.validate(clean_customdata=False); c = bpy.data.objects.new(o.name, me); tmp.objects.link(c)
        f = f"Boulangerie_Firmin_{nm}.glb"; gltf(os.path.join(EXP, f), [c])
        meta["fichiers"][f] = dict(dims=dims(bbox([c])), textures_min=2, octets=os.path.getsize(os.path.join(EXP, f)))
        meta["lod"][nm] = dict(triangles=tri_count([c]), objets=1, matieres=1, atlas="textures/Boulangerie_LOD_Couleur.png + _Normale.png (2048)")
        ROLE[nm] = c
        log(nm, meta["lod"][nm]["triangles"], "triangles")
    # ---- collisions
    cols = [o for o in coll("Collisions").objects if o.type == "MESH"]
    cc = []
    for o in cols:
        me = o.data.copy(); c = bpy.data.objects.new(o.name, me); tmp.objects.link(c); c["bf_role"] = "collision"; cc.append(c)
    f = "Boulangerie_Firmin_Collisions.glb"; gltf(os.path.join(EXP, f), cc, export_materials="NONE")
    meta["fichiers"][f] = dict(dims=dims(bbox(cc)), textures_min=0, octets=os.path.getsize(os.path.join(EXP, f)))
    meta["lod"]["Collisions"] = dict(triangles=tri_count(cc), objets=len(cc), forme="pavés convexes ; portes P1, R2 et baie B1 libres")
    # ---- FBX unique pour Unity : LODGroup automatique par suffixes _LOD0/_LOD1/_LOD2, collisions COL_*
    for c in c0: c.name = c.name + "_LOD0"
    ROLE["LOD1"].name = "Boulangerie_Exterieur_LOD1"; ROLE["LOD2"].name = "Boulangerie_Exterieur_LOD2"
    for img in bpy.data.images:
        if img.filepath and not img.filepath.startswith("//") and os.path.exists(bpy.path.abspath(img.filepath)): pass
    fbx_objs = c0 + [ROLE["LOD1"], ROLE["LOD2"]] + cc
    select(fbx_objs)
    fp = os.path.join(EXP, "Boulangerie_Firmin.fbx")
    bpy.ops.export_scene.fbx(filepath=fp, **FBX)
    meta["fichiers"]["Boulangerie_Firmin.fbx"] = dict(dims=dims(bbox(c0)), mesure=["Boulangerie_Exterieur_LOD1"], octets=os.path.getsize(fp))
    meta["fichiers"]["Boulangerie_Firmin.fbx"]["dims"] = dims(bbox([ROLE["LOD1"]]))
    meta["textures"] = sorted(os.listdir(TEXM)) + ["Boulangerie_LOD_Couleur.png", "Boulangerie_LOD_Normale.png"]
    meta["octets_textures"] = sum(os.path.getsize(os.path.join(TEXM, f)) for f in os.listdir(TEXM)) + sum(os.path.getsize(os.path.join(TEX, f)) for f in os.listdir(TEX) if f.endswith(".png"))
    json.dump(meta, open(os.path.join(EXP, "export_meta.json"), "w"), indent=1, ensure_ascii=False)
    log("exports écrits", list(meta["fichiers"]))
    # le .blend de travail n'est pas réenregistré : les copies d'export restent temporaires
