# Atelier : nos propres modèles 3D, construits par script Blender

Décision de Damien du 9 novembre 2026 : nous fabriquons nous-mêmes les modèles 3D du jeu. Ils sont à nous (tous droits réservés, comme le dépôt).

- `atelier_lib.py` : bibliothèque commune. Elle contient les matières (moellons, pierre de taille, enduit vieilli, chêne, tuiles avec mousse, pavés, pain…) et les constructeurs (boîtes, poutres, lots de milliers de pièces avec une teinte propre à chacune).
- `boulangerie_firmin.py` : la boulangerie de Firmin, pour l'obstacle `maison2` des Vallons (14,5 × 11 m, porte au milieu du sud). Elle se compose de la maison à colombages et du fournil avec son cul-de-four.

Chaque bâtiment se construit en deux versions :
1. une version **détaillée** (pierre par pierre, tuile par tuile ; environ 380 000 triangles) pour les images ;
2. une version **de jeu** (moins de 1 000 triangles), sur laquelle le détail est « cuit » en deux textures : couleur (`_couleur.jpg`) et relief (`_normale.png`, espace tangent OpenGL).

## Lancer

Blender en module Python : `python -m venv v && v/bin/pip install bpy==5.2.3`.

```bash
v/bin/python -I boulangerie_firmin.py <sortie> none bake                  # version de jeu : .fbx + textures
v/bin/python -I boulangerie_firmin.py <sortie> aube_face,jour_rue          # images de la version détaillée
v/bin/python -I boulangerie_firmin.py <sortie> jour_rue bake               # images de la version de jeu
```

Variables : `BAKE_C` et `BAKE_N` (taille des textures, 2048 par défaut pour la couleur), `RX`, `RY`, `SAMPLES` (rendu).

Les fichiers produits vont dans `unity/Assets/EdS/Modeles/Batiments/<id de l'obstacle>/` (voir docs/UNITY.md).

Conventions : mètres, façade vers −Y dans Blender (+Z dans Unity), export FBX « Forward −Z, Up Y », un seul matériau, pied dans l'emprise de l'obstacle avec 6 % de marge.
