# Atelier : nos propres modèles 3D, construits par script Blender

Décision de Damien du 9 novembre 2026 : nous fabriquons nous-mêmes les modèles 3D du jeu. Ils sont à nous (tous droits réservés, comme le dépôt). Depuis la V2.18 (10 novembre 2026), Damien autorise à remplacer directement un bâtiment de l'atelier par son nouveau modèle validé.

## Boulangerie de Firmin (`boulangerie_firmin/`, V2.18)

Modèle validé par Damien le 10 novembre 2026, pour l'obstacle `maison2` des Vallons (14,5 × 11 m, porte au milieu du sud). Un seul fichier Blender procédural produit les vues, les LOD, les collisions et les exports.

- `bf_plan.py` : source unique des cotes (volumes, ouvertures, cheminée, four, enseigne, lanterne). Repère : origine au coin avant gauche au sol, X de 0 à 14,5, Y de 0 à 11 (façade à Y = 0, regardant −Y), Z vers le haut.
- `bf_lib.py`, `bf_props.py` : matières et constructeurs, accessoires.
- `build_archi.py`, `build_interieur.py`, `build_props.py`, `build_scene.py` (ou `build_all.sh`) : étapes A à D.
- `lod_export.py` : LOD1 et LOD2, collisions, cuisson de l'atlas (2048) et exports.
- `verify.py`, `plan_doc.py`, `planche.py`, `bf_views.py`, `render_view.py`, `run_renders.sh` : contrôles, plan coté, rendus.
- `export_jeu.py` : **version de jeu** du dépôt. Elle lit le LOD1 cuit (`export/Boulangerie_Firmin_LOD1.glb`, 619 triangles), le recentre sur l'emprise des murs (X − 7,25, Y − 5,5), ramène au sol ce qui le dépasse vers le bas, ne garde qu'un matériau et écrit `boulangerie_firmin.fbx` (« Forward −Z, Up Y », `bake_space_transform`).

Lancer, avec Blender en module Python (`python -m venv v && v/bin/pip install bpy==5.2.3`), depuis le dossier de l'atelier (les scripts attendent `scripts/`, `work/`, `export/`) :

```bash
bash scripts/build_all.sh
v/bin/python -u -I scripts/lod_export.py lod && v/bin/python -u -I scripts/lod_export.py cuisson && v/bin/python -u -I scripts/lod_export.py export
v/bin/python -I scripts/export_jeu.py -- export/Boulangerie_Firmin_LOD1.glb export_jeu/boulangerie_firmin.fbx
```

Textures du jeu : `export/textures/Boulangerie_LOD_Couleur.png` converti en `boulangerie_firmin_couleur.jpg` (qualité 92) et `Boulangerie_LOD_Normale.png` copié en `boulangerie_firmin_normale.png` (espace tangent OpenGL). Les fichiers vont dans `unity/Assets/EdS/Modeles/Batiments/<id de l'obstacle>/` (voir docs/UNITY.md).

Conventions : mètres, façade vers −Y dans Blender (+Z dans Unity), un seul matériau, pivot au centre de l'emprise au sol, rien sous le sol.

Le script de la V2.17 (`boulangerie_firmin.py`, `atelier_lib.py`) est remplacé ; il reste dans l'historique git.
