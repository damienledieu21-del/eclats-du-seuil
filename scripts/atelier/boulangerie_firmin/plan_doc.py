# Génère PLAN_COORDONNEES.md et renders/plan_cote.png à partir de bf_plan.py (python3 système, matplotlib).
# python3 scripts/plan_doc.py
import os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import bf_plan as P
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Ellipse

def f(v): return f"{v:.2f}".replace(".", ",")

MURS = {"av_b": "façade boutique (Y = 0)", "g_b": "pignon gauche (X = 0)", "ar_b": "arrière boutique (Y = 8,50)",
        "sep": "mur séparatif (X 9,50-10,00)", "av_f": "façade fournil (Y = 0)", "d_f": "pignon droit fournil (X = 14,50)",
        "ar_f": "arrière fournil (Y = 8,00)", "etage_av": "étage, façade (Y = -0,18)", "etage_g": "étage, pignon gauche (X = 0)",
        "etage_ar": "étage, arrière (Y = 8,50)"}

def md():
    L = []
    w = L.append
    w("# Boulangerie de Firmin — plan coté et coordonnées\n")
    w("Généré par `scripts/plan_doc.py` depuis `scripts/bf_plan.py`, la source unique des cotes que lisent aussi la construction et la vérification. "
      "Les cotes sont en mètres.\n")
    w("## Repère\n")
    w("- Origine : coin avant gauche, au niveau du terrain. X va de gauche à droite (façade vue de face), Y de la façade (Y = 0) vers l'arrière, Z vers le haut.")
    w("- La façade regarde −Y dans Blender. Dans Unity (export avec axe avant −Z et axe haut Y), elle regarde +Z ; 1 unité = 1 m ; le pivot est à l'origine, au sol.")
    w(f"- Le sol fini intérieur est à Z = {f(P.SOL)} (tomettes et dalles) ; le terrain est à Z = 0.\n")
    w("## Volumes\n")
    w("| Volume | X | Y | Hauteurs |")
    w("|---|---|---|---|")
    B, S, F, C = P.BOUTIQUE, P.SEPARATIF, P.FOURNIL, P.CUL_DE_FOUR
    w(f"| Boutique, rez de pierre | {f(B['x0'])} – {f(B['x1'])} | {f(B['y0'])} – {f(B['y1'])} | rez 0 – {f(P.RDC_H)} ({f(P.RDC_H)} m) |")
    w(f"| Boutique, étage à colombages | {f(B['x0'])} – {f(B['x1'])} | {f(-P.ENC)} – {f(B['y1'])} (encorbellement de {int(P.ENC*100)} cm en façade) | {f(P.Z_ETAGE)} – {f(P.Z_SABL)} ({f(P.ETAGE_H)} m) |")
    w(f"| Mur séparatif (pierre, commun) | {f(S['x0'])} – {f(S['x1'])} | {f(S['y0'])} – {f(S['y1'])} | jusqu'au toit de la boutique (pignon de pierre) |")
    w(f"| Fournil, un niveau | {f(F['x0'])} – {f(F['x1'])} | {f(F['y0'])} – {f(F['y1'])} | arase {f(P.F_EAVE)} |")
    w(f"| Cul-de-four (massif et dôme) | {f(C['x0'])} – {f(C['x1'])} | {f(C['y0'])} – {f(C['y1'])} | massif 0 – {f(P.FOUR_BASE['z1'])}, dôme jusqu'à {f(P.FOUR_DOME_Z0 + P.FOUR_R[2])} |")
    w(f"| Appentis du cul-de-four | {f(P.APP_X0)} – {f(P.APP_X1)} | {f(P.APP_Y0)} – {f(P.APP_Y1)} | {f(P.APP_Z0)} → {f(P.APP_Z1)} (pente {P.APP_PITCH:.1f}°) |")
    w(f"| Emprise maximale | 0 – 14,50 | 0 – 11,00 | la cour, à l'arrière de la boutique (X 0 – 10, Y 8,5 – 11), reste libre |\n")
    w(f"Les murs de pierre font {f(P.WALL)} m d'épaisseur et les pans de bois de l'étage {f(P.FRAME_T)} m.\n")
    w("## Toitures\n")
    w(f"- **Boutique** : deux pans à {P.PITCH_B:.0f}°, faîtage parallèle à la rue, à Y = {f(P.RIDGE_B_Y)}. Les plans de toiture partent des nus extérieurs des sablières à Z = {f(P.Z_SABL)} (Y = {f(P.ROOF_B_Y0)} en façade, Y = {f(P.ROOF_B_Y1)} à l'arrière).")
    w(f"  - **Décision validée par Damien le 10 novembre 2026 : faîtage à 10,67 m, pente de 48°.**")
    w(f"  - **Faîtage recalculé : Z = {P.RIDGE_B_Z:.3f} m** (intersection des plans). La consigne donnait environ 10,57, ce qui correspond exactement à une portée de 8,50 m sans encorbellement ({P.RIDGE_B_Z_SANS_ENC:.3f}). **L'écart de +{(P.RIDGE_B_Z - P.RIDGE_B_Z_SANS_ENC)*100:.0f} cm** vient de l'encorbellement de 18 cm de l'étage, que le toit doit couvrir. Avec l'épaisseur des voliges, des tuiles et des faîtières, le point le plus haut est vers 10,95 m (valeur mesurée dans `RAPPORT.md`).")
    w(f"  - Rives : X = {f(P.ROOF_B_X0)} à gauche (débord de 0,40) et X = {f(P.ROOF_B_X1)} à droite, au-dessus du mur séparatif. Le débord d'égout est de {f(P.ROOF_B_OVER)} m dans la pente.")
    w(f"- **Fournil** : deux pans à {P.PITCH_F:.0f}°, faîtage parallèle à la rue à Y = {f(P.RIDGE_F_Y)}, **Z = {P.RIDGE_F_Z:.3f}**. Il s'adosse au mur séparatif, sous l'égout de la boutique ({f(P.Z_SABL)}). Rives de X = {f(P.ROOF_F_X0)} à {f(P.ROOF_F_X1)}. Le pignon droit est en pierre.")
    w(f"- **Appentis du cul-de-four** : un pan de {P.APP_PITCH:.1f}°, de Y = 8,00 (Z {f(P.APP_Z0)}, sous l'égout arrière du fournil) à Y = {f(P.APP_Y1)} (Z {f(P.APP_Z1)}), porté par deux poteaux de chêne.\n")
    w("## Ouvertures\n")
    w("Pour les murs avant et arrière, *a* est X ; pour les murs latéraux, *a* est Y. *z* est la hauteur de l'ouverture (passage libre pour les portes).\n")
    w("| Id | Ouverture | Mur | a0 – a1 | Largeur | z0 – z1 | Remarques |")
    w("|---|---|---|---|---|---|---|")
    for o in P.OUV:
        rem = []
        if o.get("arc"): rem.append(f"arc {o['arc']}, clé à {f(o['z_cle'])}")
        if o.get("note"): rem.append(o["note"])
        w(f"| {o['id']} | {o['nom']} | {MURS[o['mur']]} | {f(o['a0'])} – {f(o['a1'])} | {f(o['a1']-o['a0'])} | {f(o['z0'])} – {f(o['z1'])} | {' ; '.join(rem)} |")
    Lc = P.LUC
    w(f"| {Lc['id']} | {Lc['nom']} | pan avant de la boutique | {f(Lc['win'][0])} – {f(Lc['win'][1])} (axe X = {f(Lc['xc'])}) | {f(Lc['win'][1]-Lc['win'][0])} | {f(Lc['z_win'][0])} – {f(Lc['z_win'][1])} | façade de lucarne à Y = {f(Lc['y_face'])}, large de {f(Lc['w'])} ; égout {f(Lc['z_eave'])}, faîtage {f(Lc['z_ridge'])}, qui rejoint le grand pan à Y = {f(Lc['y_back'])} |")
    w("")
    w("Contrôles de cohérence en façade :")
    v1, p1, pf = P.ouv("V1"), P.ouv("P1"), P.ouv("PF")
    w(f"- **Porte principale centrée à X = {f((p1['a0']+p1['a1'])/2)}** (milieu exact des 14,50 m), donc dans la boutique.")
    w(f"- Vitrine à gauche de la porte : elle laisse {f(v1['a0'])} m de maçonnerie au chaînage d'angle (pierres d'angle jusqu'à environ 0,56) et {f(p1['a0']-v1['a1'])} m jusqu'à la porte (piédroits taillés de 0,24 à 0,38). Aucun contact.")
    w(f"- La double porte du fournil est centrée à X = {f((pf['a0']+pf['a1'])/2)} (milieu intérieur du fournil), avec {f(pf['a0']-10)} m de pierre à gauche et {f(14.5-pf['a1'])} m à droite.")
    e1, e2 = P.ouv("E1"), P.ouv("E2")
    w(f"- Fenêtres d'étage : axes X = {f((e1['a0']+e1['a1'])/2)} et {f((e2['a0']+e2['a1'])/2)}, symétriques par rapport à la lucarne (X = {f(Lc['xc'])}). Volets ouverts : de {f(e1['a0']-0.47)} à {f(e1['a1']+0.47)} et de {f(e2['a0']-0.47)} à {f(e2['a1']+0.47)}. La lucarne (de {f(Lc['xc']-Lc['w']/2)} à {f(Lc['xc']+Lc['w']/2)}) est dans le toit, au-dessus de la travée centrale à croix de Saint-André : pas de conflit.\n")
    w("## Pans de bois de la façade d'étage\n")
    w("Poteaux (axes X) : 0,09 · 1,32 · 1,84 · 2,86 · 3,40 · 4,75 · 6,10 · 6,64 · 7,66 · 8,18 · 9,41. Il y a une sablière basse sur les abouts de solives (encorbellement) et une sablière haute à 5,85. "
      "Les travées 3,40 – 4,75 et 4,75 – 6,10 portent des croix de Saint-André ; les travées d'extrémité, des décharges. Les fenêtres E1 et E2 sont entre les poteaux 1,84 – 2,86 et 6,64 – 7,66, avec appui et linteau ; sous chaque appui, une allège à tournisses. Assemblages chevillés.\n")
    w("## Cheminée et four\n")
    C_, H = P.CHEM, P.CHEM["hotte"]
    w(f"- **{C_['id']} — {C_['nom']}** : conduit X {f(C_['x0'])} – {f(C_['x1'])}, Y {f(C_['y0'])} – {f(C_['y1'])}, de la hotte jusqu'à Z = {f(C_['z_top'])} (couronnement et mitre au-dessus). Il traverse le pan arrière du fournil. C'est la seule cheminée.")
    w(f"- Hotte au-dessus de la bouche : X {f(H['x0'])} – {f(H['x1'])}, Y {f(H['y0'])} – {f(H['y1'])}, Z {f(H['z0'])} – {f(H['z1'])}.")
    w(f"- Four : massif maçonné X {f(P.FOUR_BASE['x0'])} – {f(P.FOUR_BASE['x1'])}, Y 8,00 – 11,00, Z 0 – {f(P.FOUR_BASE['z1'])}. Dôme centré en ({f(P.FOUR_C[0])} ; {f(P.FOUR_C[1])}), demi-axes {f(P.FOUR_R[0])} × {f(P.FOUR_R[1])} × {f(P.FOUR_R[2])}. Sole à Z = {f(P.FOUR_SOLE)}. La bouche BF s'ouvre dans le fournil. **Il n'y a qu'un seul four** : la boutique n'en a pas de copie.\n")
    w("## Circulation intérieure et ligne de vue\n")
    b1 = P.ouv("B1")
    y95, z95 = P.ligne_de_vue_y(9.5); y10, z10 = P.ligne_de_vue_y(10.0)
    w(f"- Parcours : rue → porte P1 → boutique (comptoir, étagères, présentoir) → baie B1 dans le mur séparatif → fournil (table, four, double porte PF vers la rue). Porte R2 vers la cour. L'escalier droit le long du mur gauche (X {f(P.ESC['x0'])} – {f(P.ESC['x1'])}, Y {f(P.ESC['y0'])} – {f(P.ESC['y1'])}, {P.ESC['marches']} marches) mène à une trémie (X {f(P.TREMIE['x0'])} – {f(P.TREMIE['x1'])}, Y {f(P.TREMIE['y0'])} – {f(P.TREMIE['y1'])}).")
    w(f"- Œil du joueur juste entré : ({', '.join(f(v) for v in P.OEIL)}). Centre de la bouche du four : ({', '.join(f(v) for v in P.BOUCHE)}).")
    w(f"- Cette ligne traverse le mur séparatif à Y = {f(y95)} (Z {f(z95)}) sur sa face boutique et à Y = {f(y10)} (Z {f(z10)}) sur sa face fournil. **La baie B1 (Y {f(b1['a0'])} – {f(b1['a1'])}, Z jusqu'à {f(b1['z1'])}) contient cette ligne avec au moins {f(min(y95-b1['a0'], b1['a1']-y10))} m de marge.** Le script de vérification le confirme par lancer de rayons.")
    w(f"- **Écart à signaler** : la consigne de la session proposait la baie « vers Y = 5,7 à 6,7 ». Avec la bouche à X = 12,15 et Y = 7,50, une baie à cet endroit ne laisserait voir la bouche qu'à un œil placé vers X ≈ 0,5, contre le mur gauche, et pas près de l'entrée. La contrainte retenue est la vue depuis l'entrée ; la baie est donc à Y {f(b1['a0'])} – {f(b1['a1'])} ({f(b1['a1']-b1['a0'])} × {f(b1['z1']-b1['z0'])} m). Avec 1,10 m de large (Y 3,95 – 5,05), le centre de la bouche était visible, mais son bord gauche frôlait le tableau de la baie ; elle a donc été portée à 1,30 m pour que la gueule entière soit visible. **Décision validée par Damien le 10 novembre 2026 : baie à Y 3,90 – 5,20, large de 1,30 m.**\n")
    w("## Décisions appliquées\n")
    w("- **Validées par Damien le 10 novembre 2026** : baie de passage B1 à Y 3,90 – 5,20 (1,30 × 2,10 m) ; faîtage de la boutique à 10,67 m avec une pente de 48°.")
    w("- Porte à X = 7,25, environ 1,20 × 2,10, sous un arc de pierre ; vitrine à gauche, sans contact avec le chaînage ni le piédroit.")
    w("- Une seule lucarne, centrée sur le pan avant de la boutique (X = 4,75).")
    w("- Deux fenêtres d'étage à volets vert sauge en façade, sans conflit avec la lucarne ni avec les poteaux.")
    w(f"- Mur séparatif de 0,50 m (X 9,50 – 10,00), percé d'une vraie baie de passage de {f(b1['a1']-b1['a0'])} × {f(b1['z1']-b1['z0'])} m (voir ci-dessus pour sa position).")
    w("- Bouche du four dans le mur arrière du fournil (Y = 8,00), centrée à X = 12,15.")
    w("- Les références (porte décentrée, deux vitrines, etc.) servent pour l'ambiance seulement. Les cotes de la mission priment.\n")
    open(os.path.join(ROOT, "PLAN_COORDONNEES.md"), "w").write("\n".join(L))

def png():
    fig = plt.figure(figsize=(22, 15), dpi=110)
    fig.suptitle("Boulangerie de Firmin — plan coté (m)  ·  origine : coin avant gauche au sol  ·  façade vers −Y", fontsize=15)
    # ----- plan
    ax = fig.add_axes([0.03, 0.05, 0.45, 0.85]); ax.set_aspect("equal"); ax.set_title("Plan du rez (Y vers le haut = vers l'arrière)")
    def R(x0, x1, y0, y1, **k): ax.add_patch(Rectangle((x0, y0), x1-x0, y1-y0, **k))
    W = P.WALL
    gray = dict(fc="#b9b2a5", ec="k", lw=0.8)
    R(0, 9.5, 0, W, **gray); R(0, 9.5, 8.5-W, 8.5, **gray); R(0, W, 0, 8.5, **gray)
    R(9.5, 10, 0, 8.5, fc="#8f877a", ec="k", lw=0.8)
    R(10, 14.5, 0, W, **gray); R(10, 14.5, 8-W, 8, **gray); R(14.5-W, 14.5, 0, 8, **gray)
    R(P.FOUR_BASE["x0"], P.FOUR_BASE["x1"], 8, 11, fc="#d9cfc0", ec="k", lw=0.8, ls="--")
    ax.add_patch(Ellipse(P.FOUR_C, 2*P.FOUR_R[0], 2*P.FOUR_R[1], fc="#6b625a", ec="k"))
    R(P.APP_X0, P.APP_X1, 8, P.APP_Y1, fill=False, ec="#a0522d", lw=1, ls=":")
    R(-P.ENC*0, 9.5, -P.ENC, 0, fill=False, ec="#5a3a1a", lw=1, ls="--")
    for o in P.OUV:
        c = "#2a6fdb"
        if o["mur"] in ("av_b", "av_f"): R(o["a0"], o["a1"], -0.05, W+0.05, fc="white", ec=c, lw=1.5); ax.text((o["a0"]+o["a1"])/2, -0.75, f"{o['id']}\n{f(o['a0'])}–{f(o['a1'])}", ha="center", va="top", fontsize=8, color=c)
        elif o["mur"] == "ar_b": R(o["a0"], o["a1"], 8.5-W-0.05, 8.55, fc="white", ec=c, lw=1.5); ax.text((o["a0"]+o["a1"])/2, 8.9, f"{o['id']} {f(o['a0'])}–{f(o['a1'])}", ha="center", fontsize=8, color=c)
        elif o["mur"] == "ar_f": R(o["a0"], o["a1"], 8-W-0.05, 8.05, fc="#ff8c3a", ec=c, lw=1.5); ax.text((o["a0"]+o["a1"])/2, 7.1, f"{o['id']}", ha="center", fontsize=8, color=c)
        elif o["mur"] == "g_b": R(-0.05, W+0.05, o["a0"], o["a1"], fc="white", ec=c, lw=1.5); ax.text(-0.2, (o["a0"]+o["a1"])/2, f"{o['id']} {f(o['a0'])}–{f(o['a1'])}", ha="right", va="center", fontsize=8, color=c)
        elif o["mur"] == "d_f": R(14.45, 14.55-W, o["a0"], o["a1"], fc="white", ec=c, lw=1.5); ax.text(14.7, (o["a0"]+o["a1"])/2, f"{o['id']} {f(o['a0'])}–{f(o['a1'])}", va="center", fontsize=8, color=c)
        elif o["mur"] == "sep": R(9.45, 10.05, o["a0"], o["a1"], fc="white", ec=c, lw=1.5); ax.text(9.75, o["a1"]+0.15, f"{o['id']}\n{f(o['a0'])}–{f(o['a1'])}", ha="center", fontsize=8, color=c)
    Ch = P.CHEM; R(Ch["x0"], Ch["x1"], Ch["y0"], Ch["y1"], fc="#444", ec="k"); ax.text(Ch["x1"]+0.1, Ch["y0"]-0.3, "C1 cheminée", fontsize=8)
    R(P.ESC["x0"], P.ESC["x1"], P.ESC["y0"], P.ESC["y1"], fill=False, ec="#7a4b20", hatch="--"); ax.text(1.6, 6.0, "escalier", fontsize=8, rotation=90)
    ax.plot([P.OEIL[0], P.BOUCHE[0]], [P.OEIL[1], P.BOUCHE[1]], color="#e0218a", lw=1.5, ls="-.")
    ax.plot(*P.OEIL[:2], "o", color="#e0218a"); ax.text(P.OEIL[0]-0.2, P.OEIL[1]+0.25, "œil 1,70", color="#e0218a", fontsize=8, ha="right")
    ax.text(4.7, 4.0, "BOUTIQUE\n0–9,50 × 0–8,50", ha="center", fontsize=11); ax.text(12.25, 3.0, "FOURNIL\n10–14,50 × 0–8", ha="center", fontsize=11)
    ax.text(12.15, 10.7, "cul-de-four 10,40–13,90 × 8–11", ha="center", fontsize=8); ax.text(4.7, 9.8, "cour (libre)", ha="center", fontsize=9, color="#555")
    ax.plot([7.25, 7.25], [-1.6, 9.0], color="r", lw=0.8, ls=":"); ax.text(7.3, -1.55, "X = 7,25 (milieu)", color="r", fontsize=8)
    for x in (0, 9.5, 10, 14.5): ax.annotate("", (x, -2.2), (0 if x else 0, -2.2))
    ax.annotate("", (0, -2.3), (14.5, -2.3), arrowprops=dict(arrowstyle="<->")); ax.text(7.25, -2.55, "14,50", ha="center", va="top")
    ax.annotate("", (15.6, 0), (15.6, 11), arrowprops=dict(arrowstyle="<->")); ax.text(15.75, 5.5, "11,00", rotation=90, va="center")
    ax.set_xlim(-2.2, 16.3); ax.set_ylim(-3.0, 11.6); ax.grid(alpha=0.2)
    # ----- façade
    ax2 = fig.add_axes([0.52, 0.47, 0.46, 0.43]); ax2.set_aspect("equal"); ax2.set_title("Élévation de la façade (Y = 0), vue de face")
    def R2(x0, x1, z0, z1, **k): ax2.add_patch(Rectangle((x0, z0), x1-x0, z1-z0, **k))
    R2(0, 9.5, 0, P.RDC_H, fc="#c9c0b0", ec="k"); R2(0, 9.5, P.RDC_H, P.Z_SABL, fc="#efe6d2", ec="k"); R2(9.5, 10, 0, P.Z_SABL, fc="#8f877a", ec="k")
    R2(10, 14.5, 0, P.F_EAVE, fc="#c9c0b0", ec="k")
    ax2.plot([P.ROOF_B_X0, P.ROOF_B_X1, P.ROOF_B_X1, P.ROOF_B_X0, P.ROOF_B_X0], [P.Z_SABL-0.4, P.Z_SABL-0.4, P.RIDGE_B_Z, P.RIDGE_B_Z, P.Z_SABL-0.4], color="#a0522d")
    ax2.plot([10, P.ROOF_F_X1, P.ROOF_F_X1, 10], [P.F_EAVE-0.2, P.F_EAVE-0.2, P.RIDGE_F_Z, P.RIDGE_F_Z], color="#a0522d")
    for o in P.OUV:
        if o["mur"] in ("av_b", "av_f", "etage_av"):
            R2(o["a0"], o["a1"], o["z0"], o["z1"], fc="#9fc3f0", ec="#2a6fdb")
            if o.get("z_cle"): ax2.plot([o["a0"], (o["a0"]+o["a1"])/2, o["a1"]], [o["z1"], o["z_cle"], o["z1"]], color="#2a6fdb")
            ax2.text((o["a0"]+o["a1"])/2, o["z0"]-0.12, f"{o['id']} {f(o['a0'])}–{f(o['a1'])}\nz {f(o['z0'])}–{f(o['z1'])}", ha="center", va="top", fontsize=7)
    Lc = P.LUC; R2(Lc["win"][0], Lc["win"][1], *Lc["z_win"], fc="#9fc3f0", ec="#2a6fdb")
    ax2.plot([Lc["xc"]-Lc["w"]/2, Lc["xc"], Lc["xc"]+Lc["w"]/2], [Lc["z_eave"], Lc["z_ridge"], Lc["z_eave"]], color="#a0522d")
    ax2.text(Lc["xc"], Lc["z_ridge"]+0.15, "L1 lucarne X = 4,75", ha="center", fontsize=8)
    R2(Ch["x0"], Ch["x1"], P.RIDGE_F_Z-1.0, Ch["z_top"], fc="#777", ec="k", alpha=0.6); ax2.text(Ch["x1"]+0.1, Ch["z_top"]-0.3, f"C1 → {f(Ch['z_top'])}", fontsize=8)
    for z, t in ((P.RDC_H, "3,05 rez"), (P.Z_SABL, "5,85 sablière"), (P.RIDGE_B_Z, f"{f(P.RIDGE_B_Z)} faîtage (consigne ≈ 10,57)"), (P.F_EAVE, "3,20 arase fournil"), (P.RIDGE_F_Z, f"{f(P.RIDGE_F_Z)} faîtage fournil")):
        ax2.axhline(z, color="#888", lw=0.5, ls=":"); ax2.text(15.0, z, t, fontsize=8, va="center")
    ax2.axvline(7.25, color="r", lw=0.8, ls=":")
    ax2.set_xlim(-0.8, 19.5); ax2.set_ylim(-0.8, 11.3); ax2.grid(alpha=0.2)
    # ----- coupe
    ax3 = fig.add_axes([0.52, 0.05, 0.46, 0.38]); ax3.set_aspect("equal"); ax3.set_title("Coupes transversales (profils des toits, vus de la gauche)")
    yb = [P.ROOF_B_Y0 - P.ROOF_B_OVER*math.cos(math.radians(48)), P.RIDGE_B_Y, P.ROOF_B_Y1 + P.ROOF_B_OVER*math.cos(math.radians(48))]
    zb = [P.Z_SABL - P.ROOF_B_OVER*math.sin(math.radians(48)), P.RIDGE_B_Z, P.Z_SABL - P.ROOF_B_OVER*math.sin(math.radians(48))]
    ax3.plot(yb, zb, color="#a0522d", lw=2, label="boutique 48°")
    ax3.plot([-P.ENC, -P.ENC, 0, 0, 8.5, 8.5], [P.Z_SABL, P.RDC_H, P.RDC_H, 0, 0, P.Z_SABL], color="k")
    ax3.plot([-0.39, 4.0, 8.39], [P.F_EAVE-0.225, P.RIDGE_F_Z, P.F_EAVE-0.225], color="#d2691e", lw=2, label="fournil 30°")
    ax3.plot([P.APP_Y0, P.APP_Y1], [P.APP_Z0, P.APP_Z1], color="#cd853f", lw=2, label="appentis")
    from matplotlib.patches import Polygon
    ax3.add_patch(Polygon([(P.FOUR_C[1] + P.FOUR_R[1]*math.cos(t*math.pi/40), P.FOUR_DOME_Z0 + P.FOUR_R[2]*math.sin(t*math.pi/40)) for t in range(41)], fc="#6b625a"))
    ax3.add_patch(Rectangle((8, 0), 3, P.FOUR_BASE["z1"], fc="#d9cfc0", ec="k"))
    ax3.plot([Lc["y_face"], Lc["y_face"], Lc["y_back"]], [Lc["z_face"], Lc["z_ridge"], Lc["z_ridge"]], color="#5a3a1a", lw=1.5, label="lucarne")
    ax3.legend(loc="upper right", fontsize=8); ax3.set_xlim(-1.5, 12.5); ax3.set_ylim(-0.3, 11.2); ax3.grid(alpha=0.2)
    os.makedirs(os.path.join(ROOT, "renders"), exist_ok=True)
    fig.savefig(os.path.join(ROOT, "renders", "plan_cote.png")); plt.close(fig)

if __name__ == "__main__":
    md(); png(); print("plan écrit")
