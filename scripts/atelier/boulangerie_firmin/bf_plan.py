# Boulangerie de Firmin — plan coté (source unique des cotes).
# Python pur (sans bpy) : lu par la construction, la vérification, le plan coté et la doc.
# Repère de la mission : origine au coin avant gauche au sol, X vers la droite (façade vue de face),
# Y de la façade (Y = 0) vers l'arrière, Z vers le haut, mètres. La façade regarde -Y.
import math

T48, T30 = math.tan(math.radians(48)), math.tan(math.radians(30))

# ---------------------------------------------------------------- volumes
WALL = 0.50                      # murs de pierre (et mur séparatif)
SOL = 0.10                       # dessus du sol fini intérieur (tomettes, dalles), terrain à 0
RDC_H = 3.05                     # boutique : hauteur du rez (sol 0 -> plancher de l'étage)
ETAGE_H = 2.80                   # boutique : étage
Z_ETAGE = RDC_H                  # 3,05 : dessus du plancher / dessous de la sablière basse
Z_SABL = RDC_H + ETAGE_H         # 5,85 : sablière haute (pied de la charpente)
ENC = 0.18                       # encorbellement de l'étage en façade
FRAME_T = 0.20                   # épaisseur des pans de bois

BOUTIQUE = dict(x0=0.0, x1=9.5, y0=0.0, y1=8.5)
SEPARATIF = dict(x0=9.5, x1=10.0, y0=0.0, y1=8.5)
FOURNIL = dict(x0=10.0, x1=14.5, y0=0.0, y1=8.0)
CUL_DE_FOUR = dict(x0=10.4, x1=13.9, y0=8.0, y1=11.0)
EMPRISE = dict(x0=0.0, x1=14.5, y0=0.0, y1=11.0)

# ---------------------------------------------------------------- toits
PITCH_B = 48.0
ROOF_B_Y0, ROOF_B_Y1 = -ENC, BOUTIQUE["y1"]          # nus extérieurs des sablières : -0,18 et 8,50
RIDGE_B_Y = (ROOF_B_Y0 + ROOF_B_Y1) / 2               # 4,16
RIDGE_B_Z = Z_SABL + (ROOF_B_Y1 - ROOF_B_Y0) / 2 * T48  # faîtage géométrique (intersection des plans)
RIDGE_B_Z_SANS_ENC = Z_SABL + (BOUTIQUE["y1"] - BOUTIQUE["y0"]) / 2 * T48  # 10,57 de la consigne
ROOF_B_X0, ROOF_B_X1 = -0.40, 10.25                   # débords de rive (gauche) ; à droite, sur le mur séparatif
ROOF_B_OVER = 0.50                                    # débord d'égout mesuré dans la pente

PITCH_F = 30.0
F_EAVE = 3.20                                          # arase des murs du fournil
RIDGE_F_Y = (FOURNIL["y0"] + FOURNIL["y1"]) / 2        # 4,00
RIDGE_F_Z = F_EAVE + (FOURNIL["y1"] - FOURNIL["y0"]) / 2 * T30
ROOF_F_X0, ROOF_F_X1 = 10.0, 14.85
ROOF_F_OVER = 0.45

# appentis du cul-de-four : du mur arrière du fournil (sous l'égout) vers l'arrière
APP_Y0, APP_Z0 = 8.0, 2.90
APP_Y1, APP_Z1 = 11.25, 1.98
APP_X0, APP_X1 = 10.15, 14.15
APP_PITCH = math.degrees(math.atan2(APP_Z0 - APP_Z1, APP_Y1 - APP_Y0))

# ---------------------------------------------------------------- four
FOUR_SOLE = 0.90                                       # dessus de la sole (âtre)
FOUR_C = (12.15, 9.50)                                 # centre du dôme
FOUR_R = (1.62, 1.42, 1.00)                            # demi-axes du dôme extérieur (X, Y, Z au-dessus de la base)
FOUR_BASE = dict(x0=10.40, x1=13.90, y0=8.0, y1=11.0, z1=0.85)   # massif maçonné sous le dôme
FOUR_DOME_Z0 = FOUR_BASE["z1"]

# ---------------------------------------------------------------- ouvertures
# mur : "av_b" (Y=0, boutique), "g_b" (X=0), "ar_b" (Y=8,5), "sep" (X 9,5-10), "av_f" (Y=0 fournil),
# "d_f" (X=14,5), "ar_f" (Y=8, fournil), "etage_av" (Y=-0,18), "etage_g" (X=0), "etage_ar" (Y=8,5), "toit_av".
# a0..a1 : abscisse le long du mur (X pour les murs avant/arrière, Y pour les murs latéraux).
OUV = [
    dict(id="P1", nom="Porte principale (boutique)", mur="av_b", a0=6.65, a1=7.85, z0=SOL, z1=2.20,
         arc="segmentaire", z_cle=2.50, note="chêne clouté, imposte vitrée sous l'arc ; passage 1,20 x 2,10"),
    dict(id="V1", nom="Vitrine à petits carreaux", mur="av_b", a0=1.05, a1=5.55, z0=0.85, z1=2.35,
         note="poitrail de chêne au-dessus ; auvent rayé et étal dessous"),
    dict(id="G1", nom="Fenêtre du rez, pignon gauche", mur="g_b", a0=1.80, a1=2.60, z0=1.10, z1=2.10),
    dict(id="R1", nom="Fenêtre du rez, arrière boutique", mur="ar_b", a0=5.00, a1=5.80, z0=1.10, z1=2.10),
    dict(id="R2", nom="Porte de la cour", mur="ar_b", a0=8.05, a1=8.95, z0=SOL, z1=2.15),
    dict(id="B1", nom="Baie de passage boutique-fournil", mur="sep", a0=3.90, a1=5.20, z0=SOL, z1=2.20,
         note="linteau de chêne ; ligne de vue vers la bouche du four"),
    dict(id="PF", nom="Double porte du fournil", mur="av_f", a0=11.05, a1=13.45, z0=0.05, z1=2.30,
         arc="segmentaire", z_cle=2.60, note="deux battants de 1,20 ; arc de pierres taillées"),
    dict(id="FD1", nom="Fenêtre grillée, pignon droit du fournil", mur="d_f", a0=5.10, a1=5.80, z0=1.30, z1=2.10),
    dict(id="BF", nom="Bouche du four", mur="ar_f", a0=11.85, a1=12.45, z0=FOUR_SOLE, z1=1.10,
         arc="plein-cintre", z_cle=1.40, note="dans le mur arrière du fournil, face intérieure Y = 7,50"),
    dict(id="E1", nom="Fenêtre d'étage gauche (volets)", mur="etage_av", a0=1.90, a1=2.80, z0=3.95, z1=5.00),
    dict(id="E2", nom="Fenêtre d'étage droite (volets)", mur="etage_av", a0=6.70, a1=7.60, z0=3.95, z1=5.00),
    dict(id="EG1", nom="Fenêtre d'étage, pignon gauche", mur="etage_g", a0=3.70, a1=4.50, z0=4.00, z1=4.90),
    dict(id="EA1", nom="Fenêtre d'étage arrière gauche", mur="etage_ar", a0=2.00, a1=2.80, z0=4.00, z1=4.90),
    dict(id="EA2", nom="Fenêtre d'étage arrière droite", mur="etage_ar", a0=6.70, a1=7.50, z0=4.00, z1=4.90),
]
# lucarne unique, centrée sur le pan avant de la boutique
LUC = dict(id="L1", nom="Lucarne (unique, pan avant)", xc=4.75, w=1.40, y_face=0.40,
           win=(4.42, 5.08), z_win=(6.72, 7.50), pitch=50.0)
LUC["z_face"] = Z_SABL + (LUC["y_face"] - ROOF_B_Y0) * T48          # pied de la façade de lucarne sur le pan
LUC["z_eave"] = 7.62                                                  # égout de la lucarne
LUC["z_ridge"] = LUC["z_eave"] + LUC["w"] / 2 * math.tan(math.radians(LUC["pitch"]))
LUC["y_back"] = ROOF_B_Y0 + (LUC["z_ridge"] - Z_SABL) / T48           # où son faîtage rejoint le grand pan

# cheminée du four : conduit au-dessus de la hotte, à travers le pan arrière du fournil
CHEM = dict(id="C1", nom="Cheminée du four", x0=11.75, x1=12.55, y0=7.10, y1=8.00, z_top=7.10,
            hotte=dict(x0=11.35, x1=12.95, y0=6.75, y1=7.50, z0=1.75, z1=2.95))

# enseigne, lanterne
ENSEIGNE = dict(x=0.42, z_bras=4.05, saillie=1.10)
LANTERNE = dict(x=8.55, z=2.30)

# escalier (droit, le long du mur gauche) et trémie
ESC = dict(x0=0.55, x1=1.45, y0=4.30, y1=7.90, marches=15)
TREMIE = dict(x0=0.50, x1=1.50, y0=5.30, y1=7.95)

# ---------------------------------------------------------------- ligne de vue (exigence intérieure)
OEIL = (7.25, 1.30, SOL + 1.60)       # joueur juste entré, à hauteur d'œil
BOUCHE = (12.15, 7.50, 1.10)          # centre de la bouche, face intérieure du mur arrière du fournil

def ligne_de_vue_y(x):
    t = (x - OEIL[0]) / (BOUCHE[0] - OEIL[0])
    return OEIL[1] + t * (BOUCHE[1] - OEIL[1]), OEIL[2] + t * (BOUCHE[2] - OEIL[2])

def ouv(i):
    for o in OUV:
        if o["id"] == i: return o
    raise KeyError(i)

if __name__ == "__main__":
    print(f"faîtage boutique {RIDGE_B_Z:.3f} (consigne 10,57 ; sans encorbellement {RIDGE_B_Z_SANS_ENC:.3f})")
    print(f"faîtage fournil {RIDGE_F_Z:.3f} ; appentis {APP_PITCH:.1f}°")
    print("lucarne", {k: round(v, 3) if isinstance(v, float) else v for k, v in LUC.items()})
    for x in (9.5, 10.0):
        print("ligne de vue à x =", x, [round(v, 3) for v in ligne_de_vue_y(x)])
