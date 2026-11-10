# Export de jeu (V2.18) : LOD1 cuit recentré (centre du pied 14,5 x 11 en X/Y, sol à 0), un seul matériau, FBX Unity.
import bpy, sys, os
src, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
o.name = o.data.name = "boulangerie_firmin"
CX, CY = 7.25, 5.5   # centre de l'emprise des murs (X 0-14,5 ; Y 0-11)
under = 0
for v in o.data.vertices:
    v.co.x -= CX; v.co.y -= CY
    if v.co.z < 0: under += 1; v.co.z = 0.0
print("sommets ramenés au sol :", under)
mat = bpy.data.materials.new("boulangerie_firmin")
o.data.materials.clear(); o.data.materials.append(mat)
for p in o.data.polygons: p.material_index = 0
o.data.update()
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', mesh_smooth_type='FACE',
    use_tspace=True, path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=False)
print("écrit", out, os.path.getsize(out))
