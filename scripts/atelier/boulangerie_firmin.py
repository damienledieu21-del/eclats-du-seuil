# Atelier Éclats du Seuil — boulangerie de Firmin (obstacle maison2 des Vallons, emprise 14,5 × 11 m, porte au milieu du sud).
# Maison à colombages (boutique) + fournil en annexe avec son cul-de-four, sa cheminée et son bûcher.
# python -I boulangerie_firmin.py <sortie> <vues|none> [export|bake]
#   export : modèle détaillé (.glb, .blend) ; bake : version de jeu légère + textures cuites (.fbx, .jpg, .png).
import os
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "atelier_lib.py")).read())

# =====================================================================
# Plan (Blender : X est, Y nord, Z haut ; façade au sud = -Y, qui devient +Z dans Unity)
# =====================================================================
MX0, MX1, MY0, MY1 = -6.9, 2.6, -5.0, 3.6          # maison, rez de pierre
AX0, AX1, AY0, AY1 = 2.6, 6.9, -4.4, 3.6           # fournil
ZP, Z1, Z2 = 0.25, 3.2, 5.8
UX0, UX1, UY0, UY1 = MX0 - 0.1, MX1 + 0.1, MY0 - 0.35, MY1 + 0.05
WALL = 0.5
DOOR = (-0.55, 0.55, ZP, ZP + 2.15)
WIN = (-5.6, -1.6, 1.05, 2.45)
NWIN = (-5.5, -4.7, 1.4, 2.4)
FDOOR = (3.55, 5.15, ZP, ZP + 2.35)                 # grande porte du fournil
FWIN = (5.75, 6.35, 1.35, 2.15)
OVEN = (5.0, AY1)                                   # centre du cul-de-four, contre le mur nord du fournil

_h = [U(0.22, 0.38) for _ in range(10)]; _k = (Z1 - ZP)/sum(_h)
COURSES = []; _z = ZP
for _x in _h:
    COURSES.append((_z, _z + _x*_k)); _z += _x*_k

# =====================================================================
# Sol et rue (décor des images, pas dans le jeu)
# =====================================================================
box("Terre", -4000, 4000, -4000, 4000, -0.12, -0.02, "terre", export=False)
box("Herbe", -4000, 4000, 5.6, 4000, -0.02, 0.0, "herbe", export=False)
if VIEWS:
    pv = Lot("Paves", "pave")
    y = -5.25
    while y > -12.5:
        d = U(0.15, 0.19); x = -11 + U(0, 0.1)
        while x < 11:
            w = U(0.15, 0.22)
            pv.add(Matrix.Translation((x + w/2, y - d/2 + U(-0.01, 0.01), -0.035 + U(-0.008, 0.012))) @
                   Matrix.Rotation(U(-0.06, 0.06), 4, "Z") @ Matrix.Rotation(U(-0.03, 0.03), 4, "X") @
                   Matrix.Diagonal((w - 0.025, d - 0.025, 0.09, 1)))
            x += w
        y -= d
    pv.done(bev=0.025, seg=2, export=False)

# =====================================================================
# Maçon : pierres une à une, chaînes d'angle, piédroits
# =====================================================================
stones = Lot("Moellons", "moellon")
dressed = Lot("PierresDeTaille", "taille")
QL, QS = 0.56, 0.30

def face_point(face, a, n, z):
    axis, fixed, out = face
    return Vector((a, fixed + out*n, z)) if axis == "y" else Vector((fixed + out*n, a, z))

def face_block(lot, face, a0, a1, z0, z1, depth, proud, tilt=0.0):
    axis, fixed, out = face
    c = face_point(face, (a0 + a1)/2, proud - depth/2, (z0 + z1)/2)
    nrm = Vector((0, out, 0)) if axis == "y" else Vector((out, 0, 0))
    sx = (a1 - a0, depth, z1 - z0) if axis == "y" else (depth, a1 - a0, z1 - z0)
    lot.add(Matrix.Translation(c) @ Matrix.Rotation(tilt, 4, nrm) @ Matrix.Diagonal(sx + (1,)),
            jitter=0.022 if lot is stones else 0.004)

def stone_face(face, a0, a1, openings, long_first, quoins=(True, True)):
    for k, (z0, z1) in enumerate(COURSES):
        course = z1 - z0
        qa = QL if ((k % 2 == 0) == long_first) else QS
        busy = []
        if quoins[0]:
            face_block(dressed, face, a0 - 0.02, a0 + qa, z0 + 0.008, z1 - 0.008, 0.35, 0.035, U(-0.004, 0.004))
            busy.append((a0 - 0.1, a0 + qa + 0.012))
        if quoins[1]:
            face_block(dressed, face, a1 - qa, a1 + 0.02, z0 + 0.008, z1 - 0.008, 0.35, 0.035, U(-0.004, 0.004))
            busy.append((a1 - qa - 0.012, a1 + 0.1))
        for (oa0, oa1, oz0, oz1) in openings:
            if z1 <= oz0 - 0.001 or z0 >= oz1 + 0.001:
                continue
            jw = 0.38 if k % 2 == 0 else 0.24
            if z0 >= oz0 - 0.001 and z1 <= oz1 + 0.35:
                face_block(dressed, face, oa0 - jw, oa0, z0 + 0.008, z1 - 0.008, 0.45, 0.035)
                face_block(dressed, face, oa1, oa1 + jw, z0 + 0.008, z1 - 0.008, 0.45, 0.035)
            busy.append((oa0 - jw - 0.012, oa1 + jw + 0.012))
        busy.sort()
        free, cur = [], a0 - (0.0 if quoins[0] else 0.0)
        for b0, b1 in busy:
            if b0 > cur: free.append((cur, b0))
            cur = max(cur, b1)
        if a1 > cur: free.append((cur, a1))
        for f0, f1 in free:
            a = f0
            while f1 - a > 0.08:
                w = U(0.16, 0.48)
                if f1 - (a + w) < 0.2: w = f1 - a
                hh = course*U(0.55, 0.92); zc = z0 + course/2 + U(-0.035, 0.035)
                if rnd.random() < 0.35 and w < 0.4:
                    face_block(stones, face, a + 0.012, a + w - 0.012, z0 + 0.012, zc - 0.006, 0.16, U(0.015, 0.04), U(-0.05, 0.05))
                    face_block(stones, face, a + 0.012, a + w - 0.012, zc + 0.006, z1 - 0.012, 0.16, U(0.015, 0.04), U(-0.05, 0.05))
                else:
                    face_block(stones, face, a + 0.014, a + w - 0.014, zc - hh/2, zc + hh/2, 0.16, U(0.015, 0.045), U(-0.04, 0.04))
                a += w

# maison
box("Soubassement", MX0 - 0.1, MX1 + 0.1, MY0 - 0.1, MY1 + 0.1, -0.02, ZP, "taille", bev=0.02)
core = box("Coque", MX0 + 0.04, MX1 - 0.04, MY0 + 0.04, MY1 - 0.04, ZP, Z1, "mortier")
cut(core, MX0 + WALL, MX1 - WALL, MY0 + WALL, MY1 - WALL, ZP + 0.01, Z1 - 0.15)
cut(core, DOOR[0], DOOR[1], MY0 - 0.2, MY0 + WALL + 0.1, DOOR[2], DOOR[3])
cut(core, WIN[0], WIN[1], MY0 - 0.2, MY0 + WALL + 0.1, WIN[2], WIN[3])
cut(core, NWIN[0], NWIN[1], MY1 - WALL - 0.1, MY1 + 0.2, NWIN[2], NWIN[3])
stone_face(("y", MY0, -1), MX0, MX1, [(DOOR[0], DOOR[1], DOOR[2], DOOR[3] + 0.22), (WIN[0], WIN[1], WIN[2] - 0.12, WIN[3] + 0.38)], True)
stone_face(("y", MY1, 1), MX0, MX1, [(NWIN[0], NWIN[1], NWIN[2] - 0.1, NWIN[3] + 0.15)], True, quoins=(True, False))
stone_face(("x", MX0, -1), MY0 + 0.03, MY1 - 0.03, [], False)
stone_face(("x", MX1, 1), MY0 + 0.03, AY0 - 0.02, [], False, quoins=(True, False))
face_block(dressed, ("y", MY0, -1), WIN[0] - 0.1, WIN[1] + 0.1, WIN[2] - 0.13, WIN[2], 0.6, 0.07)
face_block(dressed, ("y", MY1, 1), NWIN[0] - 0.1, NWIN[1] + 0.1, NWIN[2] - 0.1, NWIN[2], 0.6, 0.06)
face_block(dressed, ("y", MY1, 1), NWIN[0] - 0.15, NWIN[1] + 0.15, NWIN[3], NWIN[3] + 0.18, 0.5, 0.035)
# fournil
box("SoubassementFournil", AX0, AX1 + 0.1, AY0 - 0.1, AY1 + 0.1, -0.02, ZP, "taille", bev=0.02)
fcore = box("CoqueFournil", AX0 + 0.04, AX1 - 0.04, AY0 + 0.04, AY1 - 0.04, ZP, Z1, "mortier")
cut(fcore, FDOOR[0], FDOOR[1], AY0 - 0.2, AY0 + 0.3, FDOOR[2], FDOOR[3])
cut(fcore, FWIN[0], FWIN[1], AY0 - 0.2, AY0 + 0.3, FWIN[2], FWIN[3])
stone_face(("y", AY0, -1), AX0 + 0.02, AX1, [(FDOOR[0], FDOOR[1], FDOOR[2], FDOOR[3] + 0.25), (FWIN[0], FWIN[1], FWIN[2] - 0.1, FWIN[3] + 0.15)], False, quoins=(False, True))
stone_face(("y", AY1, 1), MX1 + 0.02, AX1, [], False, quoins=(False, True))
stone_face(("x", AX1, 1), AY0 + 0.03, AY1 - 0.03, [], True)
face_block(dressed, ("y", AY0, -1), FWIN[0] - 0.08, FWIN[1] + 0.08, FWIN[2] - 0.1, FWIN[2], 0.4, 0.06)
face_block(dressed, ("y", AY0, -1), FWIN[0] - 0.12, FWIN[1] + 0.12, FWIN[3], FWIN[3] + 0.16, 0.4, 0.035)
stones.done(bev=0.03, seg=2)
dressed.done(bev=0.012, seg=1)
blob("Suie", (NWIN[0] + 0.4, MY1 + 0.035, NWIN[3] + 0.5), (0.45, 0.01, 0.35), "suie")

# =====================================================================
# Porte de la boutique (entrouverte), porte du fournil (fermée)
# =====================================================================
box("Seuil", DOOR[0] - 0.05, DOOR[1] + 0.05, MY0 - 0.12, MY0 + WALL, ZP - 0.02, ZP + 0.03, "taille", bev=0.015)
box("Marche", DOOR[0] - 0.2, DOOR[1] + 0.2, MY0 - 0.6, MY0 - 0.1, -0.02, ZP - 0.07, "taille", bev=0.02)
box("LinteauPorte", DOOR[0] - 0.28, DOOR[1] + 0.28, MY0 - 0.05, MY0 + WALL, DOOR[3], DOOR[3] + 0.24, "chene", bev=0.015)

def door(x0, x1, y_face, z0, z1, hinge_x, open_angle, inward, name="Porte", planks=6):
    """Porte de planches cloutées, tournée sur ses gonds (en x = hinge_x)."""
    dw = x1 - x0 - 0.04
    side = 1 if hinge_x <= x0 + 0.05 else -1
    hinge = Vector((hinge_x, y_face + inward, 0))
    def piece(nm, u0, u1, n0, n1, za, zb, mat, bev=0.006):
        o = box(nm, u0, u1, n0, n1, za, zb, mat, bev=bev)
        o.data.transform(Matrix.Rotation(open_angle*side, 4, "Z")) if side > 0 else None
        if side < 0:
            o.data.transform(Matrix.Scale(-1, 4, (1, 0, 0)))
            o.data.transform(Matrix.Rotation(-open_angle, 4, "Z"))
            o.data.flip_normals()
        o.data.transform(Matrix.Translation(hinge))
        return o
    for i in range(planks):
        u0 = i*dw/planks
        piece(name, u0 + 0.004, u0 + dw/planks - 0.004, 0.0, 0.055, z0 + 0.02, z1 - 0.03, "planche")
    for z in (z0 + 0.3, (z0 + z1)/2 - 0.07, z1 - 0.35):
        piece("Traverse", 0.05, dw - 0.05, 0.055, 0.09, z, z + 0.14, "planche")
        piece("Penture", 0.0, dw*0.75, -0.012, 0.0, z + 0.04, z + 0.1, "fer", bev=0.0)
        for i in range(planks):
            u = (i + 0.5)*dw/planks
            piece("Clou", u - 0.012, u + 0.012, -0.022, 0.0, z + 0.058, z + 0.082, "fer", bev=0.004)
    piece("Anneau", dw - 0.16, dw - 0.13, -0.05, 0.0, z0 + 1.0, z0 + 1.12, "fer", bev=0.0)

door(DOOR[0], DOOR[1], MY0, DOOR[2], DOOR[3], DOOR[0] + 0.03, math.radians(58), 0.3)
fmid = (FDOOR[0] + FDOOR[1])/2
door(FDOOR[0], fmid, AY0, FDOOR[2], FDOOR[3], FDOOR[0] + 0.02, 0.0, 0.2, "PorteFournil", 4)
door(fmid, FDOOR[1], AY0, FDOOR[2], FDOOR[3], FDOOR[1] - 0.02, 0.0, 0.2, "PorteFournil", 4)
box("LinteauFournil", FDOOR[0] - 0.3, FDOOR[1] + 0.3, AY0 - 0.05, AY0 + 0.3, FDOOR[3], FDOOR[3] + 0.26, "chene", bev=0.015)
box("SeuilFournil", FDOOR[0] - 0.05, FDOOR[1] + 0.05, AY0 - 0.1, AY0 + 0.3, ZP - 0.02, ZP + 0.03, "taille", bev=0.015)
box("VitreFournil", FWIN[0], FWIN[1], AY0 + 0.12, AY0 + 0.13, FWIN[2], FWIN[3], "vitrail")
for x in (FWIN[0] + 0.02, (FWIN[0] + FWIN[1])/2, FWIN[1] - 0.02):
    box("Barreau", x - 0.015, x + 0.015, AY0 + 0.05, AY0 + 0.08, FWIN[2], FWIN[3], "fer")

# =====================================================================
# Vitrine, enseigne peinte, étal
# =====================================================================
FRAME_Y = MY0 + 0.12
box("Vitrail", WIN[0], WIN[1], FRAME_Y + 0.02, FRAME_Y + 0.03, WIN[2], WIN[3], "vitrail")
for zz in ((WIN[3] - 0.06, WIN[3]), (WIN[2], WIN[2] + 0.06)):
    box("Dormant", WIN[0] - 0.02, WIN[1] + 0.02, FRAME_Y - 0.03, FRAME_Y + 0.08, zz[0], zz[1], "chene", bev=0.01)
for i in range(7):
    x = WIN[0] + i*(WIN[1] - WIN[0])/6
    box("Montant", x - 0.035, x + 0.035, FRAME_Y - 0.03, FRAME_Y + 0.08, WIN[2], WIN[3], "chene", bev=0.008)
box("Traverse", WIN[0], WIN[1], FRAME_Y - 0.03, FRAME_Y + 0.08, 1.72, 1.79, "chene", bev=0.008)
box("LinteauVitrine", WIN[0] - 0.3, WIN[1] + 0.3, MY0 - 0.06, MY0 + WALL, WIN[3], WIN[3] + 0.4, "chene", bev=0.015)
bpy.ops.object.text_add(location=((WIN[0] + WIN[1])/2, MY0 - 0.065, WIN[3] + 0.2), rotation=(math.pi/2, 0, 0))
t = bpy.context.object; t.data.body = "BOULANGERIE"; t.data.align_x = "CENTER"; t.data.align_y = "CENTER"
t.data.size = 0.27; t.data.extrude = 0.004; t.data.space_character = 1.15
bpy.ops.object.convert(target="MESH"); t.name = "Lettres"; keep(t, "peinture")

box("Etal", WIN[0] - 0.05, WIN[1] + 0.05, MY0 - 0.66, MY0 - 0.02, 0.97, 1.03, "planche", bev=0.01)
for x in (WIN[0] + 0.12, WIN[1] - 0.12):
    beam("Console", (x, MY0, 0.5), (x, MY0 - 0.6, 0.97), 0.08, 0.08, "chene")
    beam("Chainette", (x, MY0 - 0.62, 1.03), (x, MY0 - 0.04, 1.75), 0.012, 0.012, "fer", bev=0)

def loaf(kind, x, y, z, rz=0.0):
    if kind == "miche":
        blob("Miche", (x, y, z + 0.075), (0.16, 0.16, 0.085), "pain", 20, 12, 0.004)
    elif kind == "batard":
        blob("Batard", (x, y, z + 0.065), (0.09, 0.24, 0.07), "pain", 20, 12, 0.003, rot=(0, 0, rz))
    elif kind == "couronne":
        bpy.ops.mesh.primitive_torus_add(major_radius=0.15, minor_radius=0.058, major_segments=24, minor_segments=10, location=(x, y, z + 0.05))
        o = bpy.context.object; o.name = "Couronne"; o.scale.z = 0.8
        bpy.ops.object.transform_apply(scale=True); bpy.ops.object.shade_smooth(); keep(o, "pain")
    elif kind == "croissant":
        bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.03, major_segments=16, minor_segments=8, location=(x, y, z + 0.03))
        o = bpy.context.object; o.name = "Croissant"
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y > 0.02], context="VERTS")
        bm.to_mesh(o.data); bm.free()
        o.rotation_euler.z = rz; bpy.ops.object.transform_apply(rotation=True); bpy.ops.object.shade_smooth(); keep(o, "pain")

def basket(x, y, z, r=0.2, h=0.1):
    cyl("Corbeille", (x, y, z + h/2), r, h, "osier", verts=20, bev=0.01)
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.015, location=(x, y, z + h)); keep(bpy.context.object, "osier")
    cyl("Lin", (x, y, z + h + 0.002), r - 0.02, 0.004, "toile", verts=20)

EY, EZ, E0 = MY0 - 0.34, 1.03, WIN[0]
basket(E0 + 0.35, EY, EZ, 0.22, 0.08)
loaf("miche", E0 + 0.30, EY + 0.03, EZ + 0.08, 0.3); loaf("miche", E0 + 0.42, EY - 0.08, EZ + 0.10, 1.1)
for i, k in enumerate(["batard", "batard", "couronne", "miche", "batard", "couronne"]):
    loaf(k, E0 + 0.9 + i*0.42, EY + U(-0.04, 0.04), EZ, U(-0.2, 0.2))
basket(WIN[1] - 0.35, EY, EZ, 0.2, 0.07)
for i in range(5):
    loaf("croissant", WIN[1] - 0.43 + (i % 3)*0.09, EY - 0.08 + (i // 3)*0.12, EZ + 0.07, U(-0.4, 0.4))
box("PainEpices", WIN[1] - 0.95, WIN[1] - 0.72, EY - 0.16, EY - 0.03, EZ, EZ + 0.08, "buche", bev=0.01)

# auvent au-dessus de la vitrine et de la porte
AWX = (WIN[0] - 0.35, DOOR[1] + 0.3)
nch = 13
for i in range(nch):
    x = AWX[0] + 0.1 + i*(AWX[1] - AWX[0] - 0.2)/(nch - 1)
    beam("Chevron", (x, MY0, 3.0), (x, MY0 - 1.0, 2.72), 0.07, 0.09, "chene")
for i in range(10):
    t0, t1 = i/10, (i + 1)/10 - 0.004
    yA, zA = MY0 - t0, 3.06 - 0.28*t0; yB, zB = MY0 - t1, 3.06 - 0.28*t1
    beam("Volige", (AWX[0] - 0.05, (yA + yB)/2, (zA + zB)/2), (AWX[1] + 0.05, (yA + yB)/2, (zA + zB)/2),
         math.hypot(yA - yB, zA - zB), 0.025, "planche", bev=0.004)
for x in (AWX[0] + 0.15, (AWX[0] + AWX[1])/2, AWX[1] - 0.15):
    beam("Jambe", (x, MY0, 2.4), (x, MY0 - 0.85, 2.76), 0.08, 0.08, "chene")

# =====================================================================
# Intérieur de la boutique (vu par la porte et la vitrine)
# =====================================================================
IX0, IX1, IY0, IY1 = MX0 + WALL, MX1 - WALL, MY0 + WALL, MY1 - WALL
box("Carreaux", IX0, IX1, IY0, IY1, ZP, ZP + 0.012, "tomette")
box("Plafond", IX0, IX1, IY0, IY1, Z1 - 0.17, Z1 - 0.15, "planche")
for i in range(9):
    x = IX0 + 0.2 + i*(IX1 - IX0 - 0.4)/8
    box("Solive", x - 0.07, x + 0.07, IY0, IY1, Z1 - 0.32, Z1 - 0.17, "chene", bev=0.01)
SH0, SH1 = IX0 + 0.3, IX1 - 0.3
for k in range(4):
    z = 0.9 + k*0.45
    box("Etagere", SH0, SH1, IY1 - 0.45, IY1 - 0.02, z, z + 0.035, "planche", bev=0.006)
    x = SH0 + 0.1
    while x < SH1 - 0.3:
        if rnd.random() < 0.5:
            blob("Miche", (x + 0.13, IY1 - 0.22, z + 0.11), (0.13, 0.13, 0.075), "pain", 14, 8, 0.004); x += 0.28
        else:
            blob("Batard", (x + 0.1, IY1 - 0.22, z + 0.09), (0.075, 0.2, 0.06), "pain", 14, 8, 0.003); x += 0.18
for x in (SH0, SH1):
    box("Montant", x - 0.03, x + 0.03, IY1 - 0.45, IY1 - 0.02, ZP, 2.5, "chene", bev=0.006)
box("Comptoir", -4.6, -1.2, IY0 + 1.7, IY0 + 2.3, ZP, 1.0, "planche", bev=0.01)
box("Plateau", -4.68, -1.12, IY0 + 1.65, IY0 + 2.35, 1.0, 1.05, "chene", bev=0.01)
basket(-2.4, IY0 + 1.98, 1.05, 0.25, 0.1)
for i in range(3):
    blob("Brioche", (-2.5 + i*0.1, IY0 + 1.95, 1.2), (0.07, 0.07, 0.06), "pain", 12, 8)
bx = IX1 - 0.02
box("Gueule", bx - 0.05, bx, -0.4, 0.4, 0.7, 1.2, "braise")
box("Voussure", bx - 0.12, bx, -0.52, 0.52, 1.2, 1.36, "taille", bev=0.01)
for yy in (-0.52, 0.4):
    box("Piedroit", bx - 0.12, bx, yy, yy + 0.12, 0.6, 1.2, "taille", bev=0.01)
box("Tablette", bx - 0.4, bx, -0.55, 0.55, 0.6, 0.68, "taille", bev=0.01)
for (lx, ly, lz, e, c) in ((-2.0, -1.5, 2.6, 200, (1.0, 0.72, 0.45)), (bx - 0.5, 0.0, 0.95, 90, (1.0, 0.45, 0.15))):
    ld = bpy.data.lights.new("Lueur", "POINT"); ld.energy = e; ld.color = c; ld.shadow_soft_size = 0.6
    lo = bpy.data.objects.new("Lueur", ld); lo.location = (lx, ly, lz); col.objects.link(lo)

# =====================================================================
# Cul-de-four (derrière le fournil), son appentis, cheminée, bûcher
# =====================================================================
oven = blob("CulDeFour", (OVEN[0], OVEN[1], ZP), (1.25, 1.3, 1.55), "four", 28, 16, 0.015)
md = oven.modifiers.new("Bosses", "DISPLACE"); tex = bpy.data.textures.new("bosses", "VORONOI")
tex.noise_scale = 0.18; md.texture = tex; md.strength = 0.04
box("BaseFour", OVEN[0] - 1.4, OVEN[0] + 1.4, AY1, AY1 + 1.45, -0.02, ZP + 0.05, "taille", bev=0.02)
tf = Lot("ToitFour", "tuiles", ("var", "moss"))
dsl_o = Vector((0, 1.5, -0.8)).normalized(); n_o = Vector((0, 0.8, 1.5)).normalized()
for r in range(9):
    for c in range(20):
        xx = OVEN[0] - 1.6 + c*0.17 + (0.085 if r % 2 else 0)
        if xx > OVEN[0] + 1.6: continue
        p = Vector((xx, AY1 + 0.02, 2.45)) + dsl_o*(0.06 + r*0.18) + n_o*0.03
        tf.add(basis(Vector((1, 0, 0))*0.165, dsl_o*0.27, n_o*0.014, p + dsl_o*0.13), moss=U(0.3, 0.9))
tf.done()
bo = box("VoligeFour", -1.65, 1.65, 0, 1.75, -0.03, 0.0, "chene")
bo.data.transform(Matrix.Translation((OVEN[0], AY1 + 0.02, 2.45)) @ basis(Vector((1, 0, 0)), dsl_o, n_o, Vector()))
for xx in (OVEN[0] - 1.5, OVEN[0] + 1.5):
    beam("PoteauFour", (xx, AY1 + 1.5, -0.02), (xx, AY1 + 1.5, 1.6), 0.12, 0.12, "chene")
beam("SabliereFour", (OVEN[0] - 1.6, AY1 + 1.5, 1.62), (OVEN[0] + 1.6, AY1 + 1.5, 1.62), 0.12, 0.12, "chene")

for row in range(6):
    for c in range(22):
        yy = -2.4 + c*0.16 + (0.08 if row % 2 else 0)
        if yy > 1.0: continue
        bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.075 + U(-0.012, 0.012), depth=0.5 + U(-0.05, 0.05),
                                            location=(AX1 + 0.3 + U(-0.03, 0.03), yy, ZP + 0.02 + row*0.13),
                                            rotation=(U(-0.05, 0.05), math.pi/2, U(-0.05, 0.05)))
        o = bpy.context.object; o.name = "Buche"
        if rnd.random() < 0.5:
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0.02, 0), plane_no=(0, 1, 0), clear_outer=True)
            bmesh.ops.contextual_create(bm, geom=[e for e in bm.edges if e.is_boundary])
            bm.to_mesh(o.data); bm.free()
        bpy.ops.object.transform_apply(rotation=True); keep(o, "buche")
box("AppuiBucher", AX1 + 0.02, AX1 + 0.6, -2.5, 1.1, -0.02, ZP, "taille", bev=0.01)

# =====================================================================
# Charpentier : étage à colombages
# =====================================================================
box("Etage", UX0 + 0.05, UX1 - 0.05, UY0 + 0.05, UY1 - 0.05, Z1, Z2, "enduit")
for i in range(16):
    x = UX0 + 0.2 + i*(UX1 - UX0 - 0.4)/15
    box("AboutSolive", x - 0.075, x + 0.075, UY0 - 0.02, MY0 + 0.2, Z1 - 0.17, Z1, "chene", bev=0.012)

def frame_face(axis, fixed, a0, a1, posts, braces, windows, out, planter=False):
    d = 0.09
    def B(name, a, b, z0, z1, depth=d):
        lo, hi = sorted((fixed, fixed + depth*out))
        if axis == "y": return box(name, min(a, b), max(a, b), lo, hi, z0, z1, "chene", bev=0.014)
        return box(name, lo, hi, min(a, b), max(a, b), z0, z1, "chene", bev=0.014)
    def P(a, z, off=0.0):
        return (a, fixed + (d/2 + off)*out, z) if axis == "y" else (fixed + (d/2 + off)*out, a, z)
    def peg(a, z):
        rot = (math.pi/2, 0, 0) if axis == "y" else (0, math.pi/2, 0)
        cyl("Cheville", P(a, z, d/2 + 0.004), 0.018, 0.02, "buche", rot=rot, verts=8)
    B("Sabliere", a0 - 0.05, a1 + 0.05, Z1, Z1 + 0.2, d + 0.02)
    B("SabliereHaute", a0 - 0.05, a1 + 0.05, Z2 - 0.2, Z2, d + 0.02)
    xs = [a0 + i*(a1 - a0)/(posts - 1) for i in range(posts)]
    for a in xs:
        B("Poteau", a - 0.085 + U(-0.01, 0.01), a + 0.085 + U(-0.01, 0.01), Z1 + 0.2, Z2 - 0.2)
        peg(a, Z1 + 0.28); peg(a, Z2 - 0.28)
    for i in range(posts - 1):
        if i in braces:
            ax, bx_ = xs[i] + 0.09, xs[i + 1] - 0.09
            beam("Croix", P(ax, Z1 + 0.22), P(bx_, Z2 - 0.22), d, 0.15, "chene", bev=0.012)
            beam("Croix", P(bx_, Z1 + 0.22), P(ax, Z2 - 0.22), d*0.92, 0.15, "chene", bev=0.012)
            peg((ax + bx_)/2, (Z1 + Z2)/2)
        elif i not in windows:
            B("Entretoise", xs[i], xs[i + 1], 4.42, 4.58)
            if rnd.random() < 0.6:
                beam("Guette", P(xs[i] + 0.09, Z1 + 0.22), P(xs[i + 1] - 0.09, 4.42), d*0.9, 0.13, "chene", bev=0.012)
    for i in windows:
        c = (xs[i] + xs[i + 1])/2
        if axis == "y": box("VitreEtage", c - 0.38, c + 0.38, fixed - 0.005, fixed + 0.005, 4.0, 5.0, "vitrail")
        else: box("VitreEtage", fixed - 0.005, fixed + 0.005, c - 0.38, c + 0.38, 4.0, 5.0, "vitrail")
        B("Appui", c - 0.52, c + 0.52, 3.84, 4.0, d + 0.04)
        B("Linteau", c - 0.52, c + 0.52, 5.0, 5.15)
        B("Meneau", c - 0.03, c + 0.03, 4.0, 5.0, 0.05)
        B("Traverse", c - 0.4, c + 0.4, 4.62, 4.67, 0.05)
        for s in (-1, 1):
            a = c + s*0.64
            lo, hi = sorted((fixed + (d + 0.01)*out, fixed + (d + 0.06)*out))
            for k in range(3):
                pa = a - 0.21 + k*0.14
                if axis == "y": box("Volet", pa + 0.003, pa + 0.137, lo, hi, 3.98, 5.02, "volet", bev=0.005)
                else: box("Volet", lo, hi, pa + 0.003, pa + 0.137, 3.98, 5.02, "volet", bev=0.005)
            for zz in (4.15, 4.85):
                f0, f1 = (hi, hi + 0.012) if out > 0 else (lo - 0.012, lo)
                if axis == "y": box("PentureVolet", a - 0.2, a + 0.2, f0, f1, zz, zz + 0.04, "fer")
                else: box("PentureVolet", f0, f1, a - 0.2, a + 0.2, zz, zz + 0.04, "fer")
        if planter and axis == "y":
            yb = fixed + out*0.32
            box("Jardiniere", c - 0.48, c + 0.48, yb - 0.12, yb + 0.12, 3.72, 3.9, "planche", bev=0.008)
            for k in range(28):
                fx, fy = c + U(-0.42, 0.42), yb + U(-0.09, 0.09)
                blob("Feuille", (fx, fy, 3.92 + U(0, 0.06)), (0.05, 0.05, 0.025), "feuille", 6, 4, 0.01)
                if k % 2 == 0:
                    blob("Fleur", (fx + U(-0.03, 0.03), fy, 3.99 + U(0, 0.08)), (0.03, 0.03, 0.03), "fleur", 6, 4, 0.006)
            for k in range(6):
                fx = c + U(-0.42, 0.42)
                beam("Tige", (fx, yb + out*0.11, 3.9), (fx + U(-0.05, 0.05), yb + out*0.16, 3.62 + U(-0.1, 0.05)), 0.012, 0.012, "feuille", bev=0)

frame_face("y", UY0, UX0, UX1, 9, {0, 7}, {1, 3, 5}, -1, planter=True)
frame_face("y", UY1, UX0, UX1, 9, {0, 7}, {2, 5}, 1)
frame_face("x", UX0, UY0, UY1, 8, {0, 6}, {3}, -1)
frame_face("x", UX1, UY0, UY1, 8, {0, 6}, set(), 1)
for face, a, z in ((("y", UY0, -1), -6.2, 3.6), (("y", UY0, -1), 1.9, 5.2), (("x", UX0, -1), 1.8, 3.55), (("y", UY1, 1), -2.0, 5.3)):
    p = face_point(face, a, 0.052, z)
    blob("Torchis", p, (0.24, 0.004, 0.15) if face[0] == "y" else (0.004, 0.24, 0.15), "torchis", 10, 6, 0.006)

# =====================================================================
# Couvreur : pignons, charpente, toits tuile par tuile
# =====================================================================
def tri_prism(name, pts, x0, x1, mat):
    me = bpy.data.meshes.new(name); bm = bmesh.new()
    f0 = [bm.verts.new((x0, a, b)) for a, b in pts]; f1 = [bm.verts.new((x1, a, b)) for a, b in pts]
    bm.faces.new(f0); bm.faces.new(list(reversed(f1)))
    for i in range(len(pts)):
        j = (i + 1) % len(pts); bm.faces.new([f0[i], f0[j], f1[j], f1[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o); return keep(o, mat)

def gable(xg, out, y0, y1, zb, zt, timber=True):
    yc = (y0 + y1)/2
    tri_prism("Pignon", [(y0, zb), (y1, zb), (yc, zt - 0.05)], min(xg, xg - 0.25*out), max(xg, xg - 0.25*out), "enduit")
    if not timber: return
    xf = xg + 0.045*out
    beam("Arbaletrier", (xf, y0 + 0.15, zb), (xf, yc - 0.05, zt - 0.25), 0.09, 0.17, "chene")
    beam("Arbaletrier", (xf, y1 - 0.15, zb), (xf, yc + 0.05, zt - 0.25), 0.09, 0.17, "chene")
    h = zt - zb
    box("Entrait", xf - 0.045, xf + 0.045, y0 + (y1 - y0)*0.12, y1 - (y1 - y0)*0.12, zb + h*0.25, zb + h*0.25 + 0.16, "chene", bev=0.012)
    box("Poincon", xf - 0.045, xf + 0.045, yc - 0.085, yc + 0.085, zb + h*0.25 + 0.16, zt - 0.4, "chene", bev=0.012)
    for s in (-1, 1):
        beam("Lien", (xf, yc + s*0.08, zb + h*0.45), (xf, yc + s*0.9, zb + h*0.25 + 0.16), 0.085, 0.12, "chene")

def roof(name, xa, xb, y0, y1, zeave, pitch_deg, over=0.55, sides=(-1, 1), moss_north=0.45):
    """Toit à deux pans, faîtage le long de X, tuile par tuile. Rend (z du faîtage, longueur de pan, matrices des pans)."""
    pitch = math.radians(pitch_deg)
    yc, half = (y0 + y1)/2, (y1 - y0)/2
    zr = zeave + half*math.tan(pitch)
    Ls = half/math.cos(pitch) + over
    T = 0.12; RW = xb - xa; xm = (xa + xb)/2
    lot = Lot(name, "tuiles", ("var", "moss"))
    pans = []
    for side in sides:
        dsl = Vector((0, side*math.cos(pitch), -math.sin(pitch)))
        nrm = Vector((0, side*math.sin(pitch), math.cos(pitch)))
        ridge = Vector((xm, yc, zr))
        frame = Matrix.Translation(ridge + dsl*(Ls/2) + nrm*(T/2)) @ basis(Vector((1, 0, 0)), dsl, nrm, Vector())
        o = box("Volige", -RW/2, RW/2, -Ls/2, Ls/2, -T/2, T/2, "chene"); o.data.transform(frame)
        pans.append((side, dsl, nrm, ridge))
        TW, TL, EXPO = 0.17, 0.27, 0.105
        tilt = math.atan(0.022/TL)
        for r in range(int((Ls - 0.05)/EXPO)):
            v = 0.12 + r*EXPO
            x = -RW/2 + (TW/2 if r % 2 else 0.0)
            sag = 0.025*math.sin(math.pi*min(v/Ls, 1))
            while x < RW/2 - 0.02:
                w = TW if x + TW < RW/2 else RW/2 - x
                if w < 0.05: break
                q = Quaternion(nrm, U(-0.04, 0.04)) @ Quaternion(Vector((1, 0, 0)), -tilt*side)
                ex, ey, ez = q @ Vector((1, 0, 0)), q @ dsl, q @ nrm
                hump = sag*(1 - abs(x)/(RW/2))**0.5
                p = ridge + Vector((x + w/2, 0, 0)) + dsl*(v + TL/2) + nrm*(T + 0.012 + U(0, 0.004) - hump)
                moss = max(0.0, 0.5*math.sin((x + xm)*1.7 + v*2.3) + 0.5*math.sin((x + xm)*0.6 - v*1.1)
                           + (moss_north if side > 0 else -0.35) + 0.25*(v/Ls) + U(-0.2, 0.2))
                lot.add(basis(ex*(w - 0.006), ey*TL, ez*0.013, p), moss=min(moss, 1.0))
                x += w
    lot.done()
    fx = xa
    while fx < xb - 0.05:
        cyl("Faitiere", (fx + 0.19, yc, zr + T + 0.04), 0.13, 0.4, "tuiles", rot=(0, math.pi/2, U(-0.02, 0.02)), verts=12)
        ALL[-1].data.attributes.new("var", "FLOAT", "FACE")
        for a_ in ALL[-1].data.attributes["var"].data: a_.value = U(0, 1)
        fx += 0.36
    box("Embarrure", xa, xb, yc - 0.17, yc + 0.17, zr + T - 0.02, zr + T + 0.02, "mortier")
    for xe in (xa - 0.02, xb + 0.02):
        for side in sides:
            dsl = Vector((0, side*math.cos(pitch), -math.sin(pitch)))
            a = Vector((xe, yc, zr + 0.1)); beam("Rive", a, a + dsl*Ls, 0.04, 0.24, "planche", bev=0.008)
    return zr, Ls, T

MAIN_PITCH, ANNEX_PITCH = 48, 40
gable(UX0 + 0.05, -1, UY0 + 0.05, UY1 - 0.05, Z2, Z2 + (UY1 - UY0)/2*math.tan(math.radians(MAIN_PITCH)))
gable(UX1 - 0.05, 1, UY0 + 0.05, UY1 - 0.05, Z2, Z2 + (UY1 - UY0)/2*math.tan(math.radians(MAIN_PITCH)))
ZR, LS, T = roof("Tuiles", UX0 - 0.42, UX1 + 0.42, UY0, UY1, Z2, MAIN_PITCH)
gable(AX1 - 0.04, 1, AY0 + 0.04, AY1 - 0.04, Z1, Z1 + (AY1 - AY0)/2*math.tan(math.radians(ANNEX_PITCH)), timber=False)
ZRA, LSA, _ = roof("TuilesFournil", UX1 - 0.02, AX1 + 0.4, AY0, AY1, Z1, ANNEX_PITCH, over=0.45)
# pignon du fournil : bardage de planches verticales
for i in range(26):
    yy = AY0 + 0.1 + i*(AY1 - AY0 - 0.2)/25
    ztop = Z1 + ((AY1 - AY0)/2 - abs(yy - (AY0 + AY1)/2))*math.tan(math.radians(ANNEX_PITCH)) - 0.05
    if ztop > Z1 + 0.1:
        box("Bardage", AX1 - 0.02, AX1 + 0.01, yy - 0.15, yy + 0.15, Z1, ztop, "planche", bev=0.004)

# cheminée du fournil, en pierre, avec sa fumée
ch = Lot("Cheminee", "moellon")
CX0, CX1, CY0, CY1 = 4.6, 5.4, 1.6, 2.4
cz = Z1
while cz < ZRA + 1.0:
    h = U(0.2, 0.27)
    ch.add(Matrix.Translation(((CX0 + CX1)/2 + U(-0.01, 0.01), (CY0 + CY1)/2 + U(-0.01, 0.01), cz + h/2)) @
           Matrix.Rotation(U(-0.02, 0.02), 4, "Z") @ Matrix.Diagonal((CX1 - CX0, CY1 - CY0, h - 0.012, 1)), jitter=0.01)
    cz += h
ch.done(bev=0.02)
box("Couronnement", CX0 - 0.1, CX1 + 0.1, CY0 - 0.1, CY1 + 0.1, cz, cz + 0.1, "taille", bev=0.015)
box("Mitre", CX0 + 0.2, CX1 - 0.2, CY0 + 0.2, CY1 - 0.2, cz + 0.1, cz + 0.32, "tuiles")
box("SuieMitre", CX0 + 0.18, CX1 - 0.18, CY0 + 0.18, CY1 - 0.18, cz + 0.32, cz + 0.33, "suie")
smoke = cyl("Fumee", ((CX0 + CX1)/2, (CY0 + CY1)/2, 0), 1.6, 6.0, "fumee", verts=16, export=False)
smoke.data.transform(Matrix.Translation((0, 0, 3.0))); smoke.location.z = cz + 0.33
for v in smoke.data.vertices:
    v.co.x -= 0.18*v.co.z; v.co.y += 0.08*v.co.z
FUMEE = smoke

# =====================================================================
# Enseigne, lanterne, sacs, tonneau, banc, pelle
# =====================================================================
SX, SZ = MX0 + 0.3, 3.85
beam("Potence", (SX, UY0, SZ), (SX, UY0 - 1.1, SZ), 0.035, 0.035, "fer", bev=0)
beam("Jambette", (SX, UY0, SZ - 0.5), (SX, UY0 - 0.65, SZ), 0.03, 0.03, "fer", bev=0)
for k in range(5):
    a0_, a1_ = k*math.pi/3, (k + 1)*math.pi/3
    beam("Volute", (SX, UY0 - 0.45 + 0.12*math.cos(a0_), SZ + 0.12 + 0.12*math.sin(a0_)),
         (SX, UY0 - 0.45 + 0.12*math.cos(a1_), SZ + 0.12 + 0.12*math.sin(a1_)), 0.02, 0.02, "fer", bev=0)
for yy in (UY0 - 0.38, UY0 - 1.02):
    beam("Chainette", (SX, yy, SZ), (SX, yy, SZ - 0.16), 0.012, 0.012, "fer", bev=0)
box("Panneau", SX - 0.03, SX + 0.03, UY0 - 1.06, UY0 - 0.34, SZ - 0.8, SZ - 0.16, "planche", bev=0.01)
for zz in (SZ - 0.19, SZ - 0.8):
    box("Cadre", SX - 0.035, SX + 0.035, UY0 - 1.08, UY0 - 0.32, zz, zz + 0.035, "or")
for s in (-1, 1):
    blob("MicheDoree", (SX + s*0.045, UY0 - 0.7, SZ - 0.5), (0.025, 0.22, 0.15), "or", 20, 12)
    for k in range(5):
        beam("Epi", (SX + s*0.034, UY0 - 0.48 - k*0.012, SZ - 0.72), (SX + s*0.034, UY0 - 0.42 - k*0.03, SZ - 0.3), 0.004, 0.02, "or", bev=0)

LX = DOOR[1] + 0.32
beam("BrasLanterne", (LX, MY0, 2.45), (LX, MY0 - 0.38, 2.45), 0.03, 0.03, "fer", bev=0)
for dx in (-0.08, 0.08):
    for dy in (-0.08, 0.08):
        box("Arete", LX + dx - 0.01, LX + dx + 0.01, MY0 - 0.38 + dy - 0.01, MY0 - 0.38 + dy + 0.01, 2.05, 2.33, "fer")
box("Flamme", LX - 0.025, LX + 0.025, MY0 - 0.405, MY0 - 0.355, 2.12, 2.2, "flamme")
box("VitresLanterne", LX - 0.075, LX + 0.075, MY0 - 0.455, MY0 - 0.305, 2.06, 2.32, "vitrail")
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.15, depth=0.12, location=(LX, MY0 - 0.38, 2.39), rotation=(0, 0, math.pi/4))
keep(bpy.context.object, "fer")
ld = bpy.data.lights.new("Lanterne", "POINT"); ld.energy = 25; ld.color = (1.0, 0.62, 0.3); ld.shadow_soft_size = 0.05
lo = bpy.data.objects.new("Lanterne", ld); lo.location = (LX, MY0 - 0.38, 2.16); col.objects.link(lo)
LANTERN_LIGHT = ld

def sack(x, y, z, rz, s=1.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    o = bpy.context.object; o.name = "SacFarine"
    md_ = o.modifiers.new("sub", "SUBSURF"); md_.levels = 3
    bpy.ops.object.modifier_apply(modifier="sub")
    for v in o.data.vertices:
        zz = v.co.z + 0.5
        pinch = 1.0 - 0.55*max(0.0, zz - 0.72)/0.28
        bulge = 1.0 + 0.12*math.sin(math.pi*min(zz, 1.0))
        v.co.x *= 0.42*pinch*bulge; v.co.y *= 0.3*pinch*bulge; v.co.z = zz*0.62
        v.co += Vector((U(-1, 1), U(-1, 1), U(-1, 1)))*0.008
    o.scale = (s, s, s); o.rotation_euler.z = rz; o.location = (x, y, z)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.shade_smooth(); keep(o, "toile")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.06*s, minor_radius=0.012, location=(x, y, z + 0.5*s)); keep(bpy.context.object, "toile")
sack(1.55, -5.4, 0.0, 0.3); sack(1.95, -5.5, 0.0, -0.4, 0.95); sack(1.75, -5.15, 0.0, 1.2, 0.9)
beam("Manche", (3.2, AY0 - 0.12, 0.05), (3.38, AY0 - 0.06, 2.2), 0.04, 0.04, "planche", bev=0.006)
beam("Pelle", (3.18, AY0 - 0.13, 0.05), (3.23, AY0 - 0.11, 0.55), 0.015, 0.32, "planche", bev=0.005)

def barrel(x, y):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.3, depth=0.9, location=(x, y, 0.45))
    o = bpy.context.object; o.name = "Tonneau"
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 0.5], cuts=8)
    for v in bm.verts:
        f = 1 + 0.12*math.cos(math.pi*v.co.z/0.9); v.co.x *= f; v.co.y *= f
    bm.to_mesh(o.data); bm.free(); keep(o, "planche")
    for zz, rr in ((0.1, 0.305), (0.3, 0.33), (0.6, 0.33), (0.8, 0.305)):
        cyl("Cercle", (x, y, zz), rr + 0.01, 0.04, "fer", verts=24)
barrel(6.45, -4.85); barrel(5.85, -4.95)
box("Banc", -3.3, -1.6, MY1 + 0.25, MY1 + 0.7, 0.42, 0.48, "planche", bev=0.01)
for xx in (-3.2, -1.7):
    box("PiedBanc", xx - 0.04, xx + 0.04, MY1 + 0.3, MY1 + 0.65, 0.0, 0.42, "planche", bev=0.006)

HIGH = [o for o in ALL if o.type == "MESH"]
def fix_normals(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
for o in HIGH:
    if o.name.startswith(("Volige", "Tuiles", "ToitFour", "Lot")) or o.name in ("Tuiles", "TuilesFournil"):
        fix_normals(o)

# =====================================================================
# Bilan, export du modèle détaillé
# =====================================================================
dg = bpy.context.evaluated_depsgraph_get()
tris = 0
for o in HIGH:
    me = o.evaluated_get(dg).to_mesh(); me.calc_loop_triangles(); tris += len(me.loop_triangles); o.evaluated_get(dg).to_mesh_clear()
print(f"DETAILLE objets {len(HIGH)} triangles {tris} pierres {stones.n} tuiles(maison) ?")
xs_ = [(o.matrix_world @ Vector(c)).x for o in HIGH for c in o.bound_box]
ys_ = [(o.matrix_world @ Vector(c)).y for o in HIGH for c in o.bound_box]
zs_ = [(o.matrix_world @ Vector(c)).z for o in HIGH for c in o.bound_box]
print(f"PIED x {min(xs_):.2f}..{max(xs_):.2f}  y {min(ys_):.2f}..{max(ys_):.2f}  haut {max(zs_):.2f}")
if EXPORT:
    bpy.ops.object.select_all(action="DESELECT")
    for o in HIGH: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, "boulangerie_firmin_detaillee.glb"), use_selection=True, export_apply=True)

# =====================================================================
# Version de jeu : formes simples, puis cuisson des détails (couleur + relief)
# =====================================================================
LOW = []
def lbox(x0, x1, y0, y1, z0, z1):
    me = bpy.data.meshes.new("b"); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(((x0+x1)/2, (y0+y1)/2, (z0+z1)/2)) @ Matrix.Diagonal((x1-x0, y1-y0, z1-z0, 1)))
    bm.to_mesh(me); bm.free(); o = bpy.data.objects.new("bas", me); col.objects.link(o); LOW.append(o); return o
def lframe(o, frame):
    o.data.transform(frame); return o
def lbeam(p1, p2, depth, width):
    p1, p2 = Vector(p1), Vector(p2); v = p2 - p1
    o = lbox(-depth/2, depth/2, -v.length/2, v.length/2, -width/2, width/2)
    return lframe(o, Matrix.Translation((p1 + p2)/2) @ v.to_track_quat("Y", "Z").to_matrix().to_4x4())
def lprism(pts, x0, x1):
    me = bpy.data.meshes.new("p"); bm = bmesh.new()
    f0 = [bm.verts.new((x0, a, b)) for a, b in pts]; f1 = [bm.verts.new((x1, a, b)) for a, b in pts]
    bm.faces.new(f0); bm.faces.new(list(reversed(f1)))
    for i in range(len(pts)):
        j = (i + 1) % len(pts); bm.faces.new([f0[i], f0[j], f1[j], f1[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("bas", me); col.objects.link(o); LOW.append(o); return o
def lsphere(loc, scale, seg=12, ring=7):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=1, location=loc)
    o = bpy.context.object; o.scale = scale; bpy.ops.object.transform_apply(scale=True); col.objects.unlink(o) if False else None
    LOW.append(o); return o
def lcyl(loc, r, depth, verts=8, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    o = bpy.context.object; bpy.ops.object.transform_apply(rotation=True); LOW.append(o); return o

def build_low():
    lbox(MX0 - 0.1, MX1 + 0.1, MY0 - 0.1, MY1 + 0.1, -0.02, ZP)                  # soubassements
    lbox(AX0, AX1 + 0.1, AY0 - 0.1, AY1 + 0.1, -0.02, ZP)
    lbox(MX0 + 0.01, MX1 - 0.01, MY0 + 0.01, MY1 - 0.01, ZP, Z1)                  # rez (plan des pierres)
    lbox(AX0, AX1 - 0.01, AY0 + 0.01, AY1 - 0.01, ZP, Z1)
    lbox(UX0 - 0.1, UX1 + 0.1, UY0 - 0.02, MY0 + 0.05, Z1 - 0.17, Z1)             # abouts de solives
    lbox(UX0 + 0.06, UX1 - 0.06, UY0 + 0.06, UY1 - 0.06, Z1, Z2)                  # étage (plan de l'enduit)
    hm = (UY1 - UY0)/2*math.tan(math.radians(MAIN_PITCH))
    for xg, out in ((UX0 + 0.05, -1), (UX1 - 0.05, 1)):
        lprism([(UY0 + 0.05, Z2), (UY1 - 0.05, Z2), ((UY0 + UY1)/2, Z2 + hm - 0.05)], min(xg, xg + 0.06*out), max(xg, xg + 0.06*out))
    ha = (AY1 - AY0)/2*math.tan(math.radians(ANNEX_PITCH))
    lprism([(AY0 + 0.04, Z1), (AY1 - 0.04, Z1), ((AY0 + AY1)/2, Z1 + ha - 0.05)], AX1 - 0.04, AX1 + 0.01)
    for (xa, xb, y0, y1, ze, pd, ov) in ((UX0 - 0.42, UX1 + 0.42, UY0, UY1, Z2, MAIN_PITCH, 0.55), (UX1 - 0.02, AX1 + 0.4, AY0, AY1, Z1, ANNEX_PITCH, 0.45)):
        pitch = math.radians(pd); yc, half = (y0 + y1)/2, (y1 - y0)/2
        zr = ze + half*math.tan(pitch); Ls = half/math.cos(pitch) + ov; RW = xb - xa; xm = (xa + xb)/2
        for side in (-1, 1):
            dsl = Vector((0, side*math.cos(pitch), -math.sin(pitch))); nrm = Vector((0, side*math.sin(pitch), math.cos(pitch)))
            ridge = Vector((xm, yc, zr))
            o = lbox(-RW/2, RW/2, -Ls/2, Ls/2, -0.0, 0.12 + 0.03)
            lframe(o, Matrix.Translation(ridge + dsl*(Ls/2)) @ basis(Vector((1, 0, 0)), dsl, nrm, Vector()))
        lcyl((xm, yc, zr + 0.16), 0.15, RW, 6, (0, math.pi/2, 0))
    lbox(CX0 - 0.1, CX1 + 0.1, CY0 - 0.1, CY1 + 0.1, Z1, cz + 0.33)               # cheminée
    lsphere((OVEN[0], OVEN[1], ZP), (1.27, 1.32, 1.57), 14, 8)                       # cul-de-four
    lbox(OVEN[0] - 1.4, OVEN[0] + 1.4, AY1, AY1 + 1.45, -0.02, ZP + 0.05)
    o = lbox(-1.65, 1.65, 0, 1.75, -0.03, 0.06)
    lframe(o, Matrix.Translation((OVEN[0], AY1 + 0.02, 2.45)) @ basis(Vector((1, 0, 0)), dsl_o, n_o, Vector()))
    for xx in (OVEN[0] - 1.5, OVEN[0] + 1.5): lbox(xx - 0.06, xx + 0.06, AY1 + 1.44, AY1 + 1.56, -0.02, 1.62)
    lbox(AX1 + 0.02, AX1 + 0.62, -2.5, 1.1, -0.02, ZP + 0.8)                      # bûcher
    # auvent, étal, consoles
    o = lbox(AWX[0] - 0.05, AWX[1] + 0.05, -1.0, 0.0, -0.01, 0.04)
    lframe(o, Matrix.Translation((0, MY0, 3.04)) @ Matrix.Rotation(math.atan2(0.28, 1.0), 4, "X"))
    for x in (AWX[0] + 0.15, (AWX[0] + AWX[1])/2, AWX[1] - 0.15): lbeam((x, MY0, 2.4), (x, MY0 - 0.85, 2.76), 0.08, 0.08)
    lbox(WIN[0] - 0.05, WIN[1] + 0.05, MY0 - 0.66, MY0 - 0.02, 0.97, 1.13)
    for x in (WIN[0] + 0.12, WIN[1] - 0.12): lbeam((x, MY0, 0.5), (x, MY0 - 0.6, 0.97), 0.08, 0.08)
    lbox(WIN[0] - 0.3, WIN[1] + 0.3, MY0 - 0.07, MY0 + 0.02, WIN[3], WIN[3] + 0.4)  # linteau peint
    lbox(DOOR[0] - 0.2, DOOR[1] + 0.2, MY0 - 0.6, MY0 - 0.1, -0.02, ZP - 0.07)      # marche
    # jardinières de l'étage
    for c in [UX0 + (i + 0.5)*(UX1 - UX0)/8 for i in (1, 3, 5)]:
        lbox(c - 0.48, c + 0.48, UY0 - 0.44, UY0 - 0.2, 3.72, 4.02)
    # enseigne, lanterne
    lbeam((SX, UY0, SZ), (SX, UY0 - 1.1, SZ), 0.04, 0.04)
    lbox(SX - 0.05, SX + 0.05, UY0 - 1.08, UY0 - 0.32, SZ - 0.82, SZ - 0.14)
    lbox(LX - 0.1, LX + 0.1, MY0 - 0.48, MY0 - 0.28, 2.04, 2.45)
    # sacs, tonneaux, banc, pelle
    for (x, y, s) in ((1.55, -5.4, 1.0), (1.95, -5.5, 0.95), (1.75, -5.15, 0.9)):
        lsphere((x, y, 0.3*s), (0.3*s, 0.24*s, 0.32*s), 8, 5)
    for (x, y) in ((6.45, -4.85), (5.85, -4.95)): lcyl((x, y, 0.45), 0.34, 0.9, 10)
    lbox(-3.3, -1.6, MY1 + 0.25, MY1 + 0.7, 0.0, 0.48)
    lbeam((3.2, AY0 - 0.12, 0.05), (3.38, AY0 - 0.06, 2.2), 0.05, 0.05)
    lbox(3.05, 3.36, AY0 - 0.16, AY0 - 0.08, 0.05, 0.55)
    # tout en un seul objet
    bpy.ops.object.select_all(action="DESELECT")
    for o in LOW: o.select_set(True)
    bpy.context.view_layer.objects.active = LOW[0]
    bpy.ops.object.join()
    lowo = bpy.context.object; lowo.name = "boulangerie_firmin"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    fix_normals(lowo)
    # faces que personne ne voit : dessous posés au sol, dessus du rez et de l'étage cachés sous les toits
    bm = bmesh.new(); bm.from_mesh(lowo.data)
    hidden = []
    for f in bm.faces:
        c = f.calc_center_median(); n = f.normal
        if n.z < -0.9 and (c.z < 0.3 or abs(c.z - Z1) < 0.06):
            hidden.append(f)
        elif n.z > 0.9 and f.calc_area() > 1.0 and (abs(c.z - Z1) < 0.06 or abs(c.z - Z2) < 0.06):
            hidden.append(f)
    bmesh.ops.delete(bm, geom=hidden, context="FACES")
    bm.to_mesh(lowo.data); bm.free()
    print("CACHEES supprimees", len(hidden), flush=True)
    return lowo

if MODE == "bake":
    SIZE_C = int(os.environ.get("BAKE_C", "4096")); SIZE_N = int(os.environ.get("BAKE_N", "2048"))
    low = build_low()
    me = low.data; me.calc_loop_triangles()
    print(f"JEU triangles {len(me.loop_triangles)}")
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    # verre sombre pour la cuisson (la vitrine ne doit pas sortir blanche)
    vb = M["vitrail"].node_tree.nodes["Principled BSDF"]
    vb.inputs["Base Color"].default_value = (0.05, 0.06, 0.06, 1); vb.inputs["Transmission Weight"].default_value = 0.0
    mat = bpy.data.materials.new("boulangerie_firmin"); mat.use_nodes = True; low.data.materials.append(mat)
    nt = mat.node_tree
    def target(name, size, noncolor):
        img = bpy.data.images.new(name, size, size, alpha=False); img.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
        node = nt.nodes.new("ShaderNodeTexImage"); node.image = img
        for n_ in nt.nodes: n_.select = False
        node.select = True; nt.nodes.active = node
        return img
    scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"; scene.cycles.samples = int(os.environ.get("BAKE_S", "1"))
    bk = scene.render.bake
    bk.use_selected_to_active = True; bk.use_cage = False; bk.cage_extrusion = 0.14; bk.max_ray_distance = 0.75; bk.margin = 6
    # le détail en un seul objet (modificateurs appliqués) : la cuisson est bien plus rapide
    bpy.ops.object.select_all(action="DESELECT")
    for o in HIGH: o.select_set(True)
    bpy.context.view_layer.objects.active = HIGH[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    high1 = bpy.context.view_layer.objects.active
    print("DETAIL fusionne", len(high1.data.polygons), flush=True)
    HIGH[:] = [high1]
    bpy.ops.object.select_all(action="DESELECT")
    high1.select_set(True)
    low.select_set(True); bpy.context.view_layer.objects.active = low
    img_c = target("couleur", SIZE_C, False)
    bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
    img_c.filepath_raw = os.path.join(OUT, "boulangerie_firmin_couleur.jpg"); img_c.file_format = "JPEG"
    scene.render.image_settings.quality = 92; img_c.save()
    print("CUIT couleur")
    img_n = target("normale", SIZE_N, True)
    bk.normal_space = "TANGENT"
    bpy.ops.object.bake(type="NORMAL")
    img_n.filepath_raw = os.path.join(OUT, "boulangerie_firmin_normale.png"); img_n.file_format = "PNG"; img_n.save()
    print("CUIT normale")
    # matière d'aperçu : les deux textures
    for n_ in list(nt.nodes):
        if n_.type == "TEX_IMAGE": nt.nodes.remove(n_)
    b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.85
    tc_ = nt.nodes.new("ShaderNodeTexImage"); tc_.image = img_c; nt.links.new(tc_.outputs["Color"], b.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img_n
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    bpy.ops.object.select_all(action="DESELECT"); low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "boulangerie_firmin.fbx"), use_selection=True, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                             object_types={"MESH"}, mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "boulangerie_firmin_jeu.blend"))
    print("EXPORT fbx")
    # pour les images de contrôle : on ne montre que la version de jeu
    for o in HIGH: o.hide_render = True
    vb.inputs["Base Color"].default_value = (0.80, 0.86, 0.80, 1); vb.inputs["Transmission Weight"].default_value = 1.0

# =====================================================================
# Ambiances et rendus
# =====================================================================
world = bpy.data.worlds.new("Ciel"); scene.world = world; world.use_nodes = True
wn = world.node_tree; bg = wn.nodes["Background"]
sky = wn.nodes.new("ShaderNodeTexSky")
for t_ in ("NISHITA", "MULTIPLE_SCATTERING", "SINGLE_SCATTERING"):
    try: sky.sky_type = t_; break
    except Exception: pass
wn.links.new(sky.outputs["Color"], bg.inputs["Color"])
sun = bpy.data.lights.new("Soleil", "SUN"); sun.angle = math.radians(1.5)
so = bpy.data.objects.new("Soleil", sun); col.objects.link(so)

def ambiance(name):
    if name == "aube": az, el, energy, color, exp, bgs, lamp = 108, 5, 3.6, (1.0, 0.62, 0.36), 0.1, 0.22, 25
    else: az, el, energy, color, exp, bgs, lamp = 205, 46, 3.4, (1.0, 0.96, 0.9), -0.6, 0.28, 0
    a, e = math.radians(az), math.radians(el)
    to_sun = Vector((math.sin(a)*math.cos(e), math.cos(a)*math.cos(e), math.sin(e)))
    so.rotation_mode = "QUATERNION"; so.rotation_quaternion = (-to_sun).to_track_quat("-Z", "Y")
    sun.energy = energy; sun.color = color
    try: sky.sun_elevation = e; sky.sun_rotation = -math.atan2(to_sun.x, to_sun.y) + math.pi/2; sky.sun_disc = False
    except Exception: pass
    bg.inputs["Strength"].default_value = bgs; scene.view_settings.exposure = exp
    LANTERN_LIGHT.energy = lamp

scene.render.engine = "CYCLES"; scene.cycles.device = "CPU"
scene.cycles.samples = int(os.environ.get("SAMPLES", "48")); scene.cycles.use_denoising = True
scene.cycles.max_bounces = 6; scene.cycles.transmission_bounces = 6; scene.cycles.volume_bounces = 1
scene.render.resolution_x = int(os.environ.get("RX", "1280")); scene.render.resolution_y = int(os.environ.get("RY", "720"))
scene.view_settings.view_transform = "AgX"
try: scene.view_settings.look = "AgX - Medium High Contrast"
except Exception: pass
cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); col.objects.link(cam); scene.camera = cam
SHOTS = {
    "aube_face":    ("aube", (-10.5, -21.0, 3.0), (0.0, -1.0, 4.6), 28, None),
    "aube_vitrine": ("aube", (-3.1, -8.6, 1.55), (-3.4, -5.0, 1.55), 35, 3.7),
    "aube_porte":   ("aube", (-3.2, -9.6, 1.9), (0.2, -5.2, 2.3), 32, 5.5),
    "jour_fournil": ("jour", (14.5, -12.5, 3.0), (2.5, 0.0, 3.8), 26, None),
    "jour_rue":     ("jour", (8.5, -19.0, 1.7), (-0.8, -3.0, 4.2), 24, None),
    "jour_dos":     ("jour", (12.0, 13.5, 3.5), (2.5, 1.5, 3.2), 26, None),
}
for v in VIEWS:
    amb, loc, tgt, lens, focus = SHOTS[v]
    ambiance(amb)
    cam.location = loc; cam_d.lens = lens
    cam.rotation_mode = "QUATERNION"; cam.rotation_quaternion = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y")
    cam_d.dof.use_dof = focus is not None
    if focus: cam_d.dof.focus_distance = focus; cam_d.dof.aperture_fstop = 2.8
    scene.render.filepath = os.path.join(OUT, v + ("_jeu" if MODE == "bake" else "") + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDU", v)
