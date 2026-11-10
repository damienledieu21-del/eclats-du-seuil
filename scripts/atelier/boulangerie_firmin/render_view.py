# Rend UNE vue depuis le fichier Blender unique (un processus par vue : le conteneur peut redémarrer).
#   /tmp/bvenv/bin/python -u -I scripts/render_view.py <vue> [final|essai] [fichier.blend]
#   essai : 25 % de la résolution, SAMPLES=16 par défaut -> renders/essais/ ; final : pleine résolution -> renders/
#   variables : SAMPLES, SCALE
import sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from bf_lib import ROOT, open_blend
import bf_views as V

view = sys.argv[1]
mode = sys.argv[2] if len(sys.argv) > 2 else "essai"
blend = sys.argv[3] if len(sys.argv) > 3 else os.path.join(ROOT, "Boulangerie_Firmin.blend")
open_blend(blend)
sc = bpy.context.scene
V.setup(sc)
final = mode == "final"
scale = float(os.environ.get("SCALE", "1.0" if final else "0.25"))
sc.cycles.samples = int(os.environ.get("SAMPLES", ("64" if view.startswith("07") else "40") if final else "16"))
sc.cycles.use_adaptive_sampling = True; sc.cycles.adaptive_threshold = 0.02
sc.render.use_persistent_data = False
sc.render.threads_mode = "AUTO"
cam = V.apply_view(sc, view, scale)
outdir = os.path.join(ROOT, "renders") if final else os.path.join(ROOT, "renders", "essais")
os.makedirs(outdir, exist_ok=True)
sc.render.filepath = os.path.join(outdir, view + ".png")
sig = V.signature()
t = time.time()
bpy.ops.render.render(write_still=True)
dt = time.time() - t
print("RENDU", view, sc.render.resolution_x, sc.render.resolution_y, f"{dt:.0f}s", sig, flush=True)
if final:
    mp = os.path.join(ROOT, "renders", "rendus_meta.json")
    meta = json.load(open(mp)) if os.path.exists(mp) else {}
    meta[view] = dict(blend=os.path.basename(blend), signature=sig, camera=cam.name, resolution=[sc.render.resolution_x, sc.render.resolution_y],
                      echantillons=sc.cycles.samples, secondes=round(dt), date=time.strftime("%Y-%m-%d %H:%M"))
    json.dump(meta, open(mp, "w"), indent=1, ensure_ascii=False)
