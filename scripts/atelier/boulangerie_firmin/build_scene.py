# Étape D — scène finale : studio, ciel, soleil, caméras des 7 vues, nuancier des matières (collection Materiaux),
# rôles Unity, polices empaquetées.  Lit work/C_props.blend, écrit Boulangerie_Firmin.blend (le fichier unique des rendus).
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Matrix
from bf_lib import *
import bf_views as V

open_blend(os.path.join(WORK, "C_props.blend"))
sc = bpy.context.scene
V.setup(sc)
# fumée légère de la cheminée du four (volume ; rendu seulement, jamais exportée)
import bf_plan as P
if "Fumee_Cheminee" not in bpy.data.objects:
    C = P.CHEM; ce = coll("Effets_Rendu")
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=0.45, radius2=2.0, depth=7.0, matrix=Matrix.Translation((0, 0, 3.5)))
    for v in bm.verts: v.co.x += 0.25*v.co.z; v.co.y -= 0.15*v.co.z           # légère dérive au vent, vers +X (au-dessus du fournil)
    me = bpy.data.meshes.new("Fumee_Cheminee"); bm.to_mesh(me); bm.free(); me.materials.append(mat("fumee"))
    o = bpy.data.objects.new("Fumee_Cheminee", me); ce.objects.link(o)
    o.location = ((C["x0"] + C["x1"])/2, (C["y0"] + C["y1"])/2, C["z_top"] + 0.40)
    o["bf_role"] = "effet_rendu"
# nuancier : une sphère par matière (référence pour Unity ; non rendu, non exporté)
cm = coll("Materiaux"); cm.hide_render = True
mats = sorted([m for m in bpy.data.materials if m.name.startswith("MAT_")], key=lambda m: m.name)
for i, m in enumerate(mats):
    n = "Nuancier_" + m.name[4:]
    if n in bpy.data.objects: continue
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=0.35)
    me = bpy.data.meshes.new(n); bm.to_mesh(me); bm.free(); me.materials.append(m)
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.new(n, me); cm.objects.link(o)
    o.location = (-6 - (i // 8)*1.0, 2 + (i % 8)*1.0, 0.4)
    o["bf_role"] = "nuancier"
for o in bpy.data.objects:
    if o.type == "MESH" and "bf_role" not in o: o["bf_role"] = "statique"
try: bpy.ops.file.pack_all()
except Exception as e: print("pack_all", e)
sc["bf_source"] = "Boulangerie_Firmin"
save_blend(os.path.join(ROOT, "Boulangerie_Firmin.blend"))
print("FIN étape D")
