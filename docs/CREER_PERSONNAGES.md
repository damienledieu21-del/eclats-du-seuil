# Créer tes propres personnages et objets (V2.16)

Guide pour Damien. Tu m'as dit le 9 novembre 2026 : « designer des choses nous-mêmes comme les personnages ou autres ». Voici comment, **sans savoir faire de la 3D**, avec des outils gratuits, dans l'ordre :

1. **MakeHuman** : tu fabriques un humain réaliste avec des curseurs (comme un jeu de création de personnage). C'est le plus simple, commence par là.
2. **Unity** : un menu du jeu range ce personnage tout seul et le branche (un PNJ du village, ou ton héros).
3. **Blender** : pour retoucher un objet, ou me laisser en fabriquer par script.
4. **Un dessin au crayon** : tu me l'envoies, je m'en occupe.

Direction artistique : **réaliste, adaptée à ton PC** (4 Go de RAM, GTX 470, « mode PC modeste »). Tout ce guide respecte cette limite.

---

## 0. Les licences en deux phrases

- **Ce que tu fabriques dans MakeHuman est à toi et libre** : les personnages exportés sont **CC0** (domaine public : tu peux les modifier, les distribuer et les vendre sans rien demander). C'est écrit dans la [FAQ de MakeHuman](https://static.makehumancommunity.org/oldsite/faq/can_i_sell_models_created_with_makehuman.html) : « The exported models end up being licensed CC0 », et les éléments de base (« core assets ») sont CC0 ([page licence](https://static.makehumancommunity.org/about/license.html)). Le programme lui-même est sous licence AGPL, mais cela ne touche pas les personnages que tu en sors.
- **Les vêtements et cheveux téléchargés sur le site** de MakeHuman viennent d'auteurs différents : certains sont **CC0**, d'autres **CC BY** (tu peux les utiliser en citant l'auteur). **Règle du dépôt : CC0 ou CC BY seulement. Jamais** « NC » (pas d'usage commercial), « ND » (pas de modification) ni « SA ». Chaque élément affiche sa licence sur sa page du site : regarde-la avant de le choisir, et note-la dans le manifeste (étape 6).

Aucun fichier de Mixamo ni de l'Asset Store n'entre jamais dans le dépôt : tes personnages restent dans ton dossier `unity/Assets/Style` (non suivi par git), comme les autres packs.

---

## 1. Installer (une seule fois)

| Quoi | Où | Pour quoi |
|---|---|---|
| **MakeHuman** (version 1.2 ou plus récente) | [makehumancommunity.org](https://static.makehumancommunity.org/) → *Download* | créer les humains |
| **Blender** (gratuit, 4.x) | [blender.org](https://www.blender.org/download/) | retoucher ou créer des objets |
| Unity 6.3 et le dépôt | déjà installés (voir `docs/UNITY.md`) | le jeu |

Pour MakeHuman, prends l'installateur Windows, laisse tout par défaut. Au premier lancement, il crée un dossier de données (par exemple `Documents\makehuman`) : c'est là que tes personnages sauvegardés et les éléments téléchargés iront.

---

## 2. Le budget de ton PC (à garder en tête)

Le jeu tourne sur ton PC modeste : chaque personnage coûte de la mémoire. Les limites ci-dessous sont celles que le menu d'import vérifie pour toi (`src/EdS.Runtime/MadeHumans.cs`).

| | PC modeste (ton PC) | PC ordinaire | Téléphone |
|---|---|---|---|
| **Triangles** par personnage (corps + cheveux + vêtements) | **15 000 conseillés** | 30 000 conseillés | à éviter |
| Refus au-delà de | 60 000 | 60 000 | |
| **Textures** (côté en pixels) | **1024 au plus** | 2048 | 512 |
| Nombre de PNJ différents à l'écran en même temps | une dizaine | plus | peu |

Règles simples :

- Un humain de MakeHuman **sans** options de haute qualité tient en général dans le budget. Si le menu d'import te dit « au-dessus des 15 000 conseillés », ce n'est pas bloquant (c'est un conseil), mais deux ou trois personnages comme ça suffisent pour ton PC : garde les plus lourds pour les PNJ importants (le héros, Gaspard…).
- **Au-dessus de 60 000 triangles, l'import refuse** le personnage (il ferait ramer tout un village).
- Les textures sont **réduites automatiquement** à 1024 pixels au moment de l'import : inutile de les redimensionner toi-même.
- Moins de pièces = plus léger : un personnage sans chapeau, sans cape et sans accessoires coûte nettement moins cher.

---

## 3. Créer un humain dans MakeHuman (pas à pas)

> Les captures sont **décrites** (je ne peux pas t'en joindre ici). Les noms d'onglets sont ceux de MakeHuman 1.2 ; si ta version dit un peu autre chose, cherche le mot le plus proche.

**Vue d'ensemble de l'écran** : en haut, une rangée d'onglets (*Files, Modelling, Materials, Geometries, Pose/Animate, Rendering, Settings…*). Au milieu, ton personnage en 3D, que tu tournes avec la souris. À gauche et à droite, les curseurs de l'onglet choisi.

### Étape 1 : le corps (onglet *Modelling*)

1. Ouvre **Modelling → Main** : les curseurs **Gender** (homme/femme), **Age** (enfant à vieillard), **Muscle**, **Weight**, **Height**, **Proportions**, plus la **couleur de peau** (curseurs de teinte). Bouge-les en regardant le personnage changer.
2. **Visage** : onglet **Modelling → Face** puis les sous-onglets (nez, bouche, oreilles, yeux, menton…) : chaque curseur change une partie. Va doucement : de petits changements suffisent pour qu'un visage soit unique.
3. Les autres sous-onglets (**Torso, Arms and hands, Legs, Head, Neck, Hip…**) règlent la carrure. Pour un PNJ du village, un corps très simple suffit.

Astuce : **sauvegarde souvent** (*Files → Save*, nomme le fichier comme le personnage). Tu pourras le rouvrir et le modifier plus tard.

### Étape 2 : l'âge et la silhouette

Le curseur **Age** va de 1 à 90 ans. Quelques repères : 25 ans est le milieu des curseurs de beauté, 50-60 ans donne de la maturité (Gaspard, le doyen), 75 et plus un vieillard. **Weight** et **Muscle** donnent un forgeron costaud ou un archiviste maigre.

### Étape 3 : la peau et les yeux (onglet *Materials*)

- **Materials → Skin/Material** : une liste de peaux (texture) à cliquer ; choisis une peau réaliste de la liste de base (elles sont CC0).
- **Eyes** : couleur des yeux. **Eyebrows**, **Eyelashes** : sourcils et cils (laisse-les, ils rendent le visage vivant).

### Étape 4 : cheveux et vêtements (onglet *Geometries*)

- **Geometries → Hair** : une liste de coiffures. Clique une coiffure pour la poser ; clique « None » pour l'enlever.
- **Geometries → Clothes** : une liste de vêtements (chemise, pantalon, bottes, robe…). Tu peux en cumuler plusieurs. **Pour la fantasy médiévale, cherche** : chemise ample, pantalon simple, bottes, tunique, robe. Une tenue simple vaut mieux qu'une tenue moderne.
- **Geometries → Topologies** (ou *Proxy mesh* selon la version) : si MakeHuman propose un **maillage léger** (low-poly, « game »), choisis-le : c'est le meilleur moyen de rester sous 15 000 triangles.
- Des vêtements et cheveux de plus se téléchargent sur le **site de MakeHuman, page *Assets*** (certaines versions ont aussi l'onglet *Community* pour les télécharger depuis le programme). **Contrôle la licence de chacun (CC0 ou CC BY).** Les éléments livrés avec MakeHuman (« system assets ») sont CC0.

### Étape 5 : le squelette (onglet *Pose/Animate*)

C'est **l'étape la plus importante** pour que le personnage bouge dans le jeu.

1. **Pose/Animate → Skeleton**.
2. Choisis **« Game engine »** (le squelette fait pour les moteurs de jeu ; la documentation de MakeHuman l'appelle « game rig » et dit de le prendre pour Unity).
3. Ne choisis **ni** « Default » **ni** MHX ni Rigify : Unity ne les reconnaît pas aussi bien comme humanoïde.

Pourquoi : le jeu fait bouger tous ses humains avec les animations de Quaternius (marche, attente, combat…) par **retargeting Humanoid** : Unity transpose ces animations sur n'importe quel squelette humanoïde. Le squelette « Game engine » est reconnu tout seul.

### Étape 6 : exporter (onglet *Files → Export*)

1. **Files → Export**.
2. **Mesh format** : **Filmbox (fbx)**. Laisse les réglages par défaut, sauf :
3. **Scale units** : choisis **meter** (mètres).
4. Coche l'export des **textures** et, si l'option existe, « **Feet on ground** » (pieds au sol).
5. **Nom du fichier** : le nom du personnage (par exemple `Gaspard`).
6. **Dossier de destination** : clique sur les trois points `…` et choisis **le bon dossier du jeu** (étape suivante). MakeHuman y crée l'`.fbx` et un sous-dossier de **textures**.
7. Clique **Export**.

---

## 4. Où le déposer

Un dossier par personnage, dans le projet du jeu :

```
unity/Assets/Style/MakeHuman/
├── PNJ.txt                     (qui porte quel personnage : créé par le menu, voir étape 8)
├── Gaspard/
│   ├── Gaspard.fbx             (l'export de MakeHuman)
│   ├── textures/               (ses images, créées par l'export)
│   └── MANIFESTE.md            (licences et crédits : le menu le crée, tu le remplis)
└── Odile/
    ├── Odile.fbx
    └── …
```

- **Le nom du dossier est le nom du personnage.** Un dossier qui porte le **prénom d'un PNJ** (`Gaspard`, `Odile`, `Maëlle`…) est pris tout seul pour ce PNJ.
- Si tu exportes depuis MakeHuman directement dans ce dossier (étape 6.6), il n'y a rien à copier.
- `Assets/Style` n'est **pas** suivi par git : tes fichiers restent sur ton PC. Pas de risque de les envoyer par erreur.

### Le manifeste (`MANIFESTE.md`)

Comme pour les autres vagues de ressources, chaque personnage a un petit fichier qui dit **ce qu'il contient et sous quelle licence**. Le menu l'écrit pour toi la première fois (un modèle à remplir) ; il contient :

- ton nom (le personnage est CC0 : « Licence du personnage : CC0 ») ;
- **une ligne par élément pris dans la bibliothèque de MakeHuman** : nom, auteur, licence, lien. Pour un élément **CC BY**, recopie aussi l'auteur dans `docs/CREDITS.md` (dis-le-moi, je le fais).

Si la licence écrite est autre chose que CC0 ou CC BY (NC, ND, SA…), **le menu te le signale** dans son rapport.

---

## 5. Dans Unity : le menu qui range tout

1. Ouvre le projet Unity (dossier `unity`), attends la fin de l'import.
2. Lance une fois **Éclats du Seuil → Préparer l'essai de style** (si tu ne l'as pas déjà fait : il prépare les animations humanoïdes de Quaternius, indispensables).
3. Menu **Éclats du Seuil → Importer les personnages MakeHuman**.

Le menu, pour chaque dossier de `Assets/Style/MakeHuman/` :

- règle le modèle en **Humanoid** (le squelette « Game engine » est reconnu) ;
- fait des **matières URP** à partir des textures (couleur, relief) ; les cheveux, sourcils et cils sont **découpés** (transparence propre) ;
- **plafonne les textures** à 1024 pixels (512 sur téléphone) ;
- compte les **triangles** et te dit si c'est trop lourd ;
- crée un **prefab** par personnage dans `Assets/Style/Generated/MakeHuman/` ;
- inscrit le personnage au **catalogue** du jeu (`Assets/Style/Resources/StyleCatalog`, liste « Made Looks »).

À la fin, une fenêtre donne le **rapport** : une ligne par personnage (« prêt », « refusé » avec la raison), les lignes de `PNJ.txt` qui visent un personnage absent, et ce qu'il faut corriger. Tu peux **relancer le menu autant que tu veux** : il refait ce qui a changé.

(« Préparer l'essai de style » fait aussi cet import à la fin, s'il y a un dossier `MakeHuman`.)

### Donner un personnage à un PNJ : `PNJ.txt`

Le menu crée `Assets/Style/MakeHuman/PNJ.txt` au premier import, avec des exemples en commentaire. Une ligne = un PNJ :

```
# qui porte quel personnage ?
gaspard = Gaspard.fbx
npc.odile = Odile
heros = Aldric/Aldric.fbx
```

- À gauche : l'**identifiant du PNJ** (le prénom en minuscules, sans accent ; `npc.odile` marche aussi). `heros` désigne le héros. Un identifiant de villageois comme `bourg/bavard1` marche aussi.
- À droite : le **fichier** (`Gaspard.fbx`) ou le **nom du dossier** (`Odile`).
- Une ligne qui commence par `#`, `//` ou `;` est un commentaire. Une ligne mal écrite est **ignorée et signalée** (numéro de ligne dans le rapport du menu) : le jeu ne plante jamais.
- Un PNJ **sans** ligne dont le prénom est celui d'un dossier prend ce dossier tout seul.
- Après avoir modifié `PNJ.txt`, **relance le menu d'import** (il recopie le fichier dans le catalogue).

**Les animations** (attente, marche, travail, combat…) sont celles de Quaternius : elles s'appliquent à ton personnage par retargeting Humanoid, sans rien faire. Si elles manquent (rapport : « Pas d'animations humanoïdes »), lance d'abord « Préparer l'essai de style ».

**Ce qui n'est pas pris** : le Gardien (il garde sa stature), les bêtes et les ennemis. Si Unity ne trouve pas un squelette humanoïde, le personnage est refusé et le PNJ garde son corps d'avant.

### Réglages dans le catalogue

Dans l'inspecteur du `StyleCatalog` (section « V2.16 ») :

- **Use Made Humans** : décoché, tous les PNJ et le héros reprennent leurs corps d'avant (utile pour comparer).
- Dans chaque ligne de **Made Looks** : **Height** (taille en mètres, 1,75 par défaut ; mets 1,60 pour une petite villageoise) et **Yaw** (tourner le modèle s'il regarde de travers).

---

## 6. Le créateur de personnage dans le jeu (première version)

Pour **ton héros**, le jeu a maintenant un **créateur de personnage** :

- **À la création** d'une nouvelle partie, la fenêtre « Ton personnage » s'ouvre toute seule (le jeu est en pause).
- **À tout moment** : **Échap** (pause) → **Réglages** → **Mon personnage…**

Tu y choisis avec les flèches `<` `>` :

| Choix | Options |
|---|---|
| **Corps** | Homme, Femme, puis **chaque personnage MakeHuman importé** (par son nom de dossier) |
| **Coiffure** | Aucune, Rasée, Raie sur le côté, Longue, Chignons, Courte (selon le corps) |
| **Barbe** | Oui / Non (homme) |
| **Peau** | Claire ou Sombre |
| **Cheveux** | Noir, Brun, Châtain, Roux, Blond, Gris |
| **Tenue** | 13 couleurs : lin écru, vert de saule, rouille, bleu de guède, toile claire, tabard bleu, rouge, robe de laine, cuir, prune, vert de forêt, cuir sombre, acier bleuté |

**Valider** garde le choix **dans la sauvegarde** de ton personnage (il revient à chaque reprise) et redessine ton héros tout de suite. **Annuler** (ou Échap) ne change rien. Un personnage MakeHuman a **sa propre peau, ses cheveux et ses vêtements** : les autres choix sont grisés pour lui.

Limites de cette première version : pas encore d'**aperçu en direct** pendant le choix (le héros change à la validation) ; deux peaux seulement pour les corps de Quaternius (pour d'autres teints, fabrique-toi un personnage dans MakeHuman, c'est fait pour ça) ; pas de réglage de taille ni de visage. Dis-moi ce que tu veux en premier.

---

## 7. Quand ça ne marche pas

| Ce que tu vois | Pourquoi, et quoi faire |
|---|---|
| Le rapport dit « Unity n'a pas trouvé un squelette humanoïde » | Le squelette n'est pas « Game engine ». Dans MakeHuman : *Pose/Animate → Skeleton → Game engine*, réexporte. |
| Personnage **tout blanc** ou **rose** | Les textures manquent. Vérifie qu'elles sont dans le dossier du personnage (sous-dossier `textures`) puis relance le menu. Si ça reste, envoie-moi le nom du fichier. |
| Cheveux ou cils en **bloc plein** | Leur nom de matière est inhabituel : envoie-moi le nom (je l'ajoute à la liste, `MadeHumans.IsCutoutMaterial`). |
| « au-dessus des 15 000 conseillés » | Pas bloquant. Pour alléger : maillage léger (étape 4), moins de vêtements, ou **Blender → modificateur Decimate** (section 8). |
| « trop lourd » (plus de 60 000) | Refusé. Même remède, de façon plus marquée. |
| Le PNJ garde son ancien corps | Le rapport t'a dit pourquoi (ligne de `PNJ.txt` mal écrite, personnage refusé, **animations absentes**). Relance le menu après correction. |
| Le personnage flotte ou s'enfonce dans le sol | Mets **Height** plus petit/grand dans la ligne du catalogue, ou réexporte avec « Feet on ground ». |
| Il regarde de travers | Règle **Yaw** (180 pour le retourner). |

---

## 8. Blender : retoucher ou créer des objets

Blender (gratuit) sert à tout ce qui n'est **pas** un humain : un tabouret, une enseigne, un tonneau abîmé, un objet que tu veux modifier.

### Retoucher un modèle (le minimum à savoir)

- **Ouvrir** un fichier : *File → Import → FBX* (ou *glTF*). Souris : bouton du milieu pour tourner, molette pour zoomer, Maj + bouton du milieu pour déplacer.
- **Alléger** un modèle trop lourd : clique-le, onglet **Modifier** (la clé à molette) → *Add Modifier → Generate → Decimate* → mets **Ratio** à 0,5 (moitié moins de triangles), regarde si la forme tient, puis **Apply**.
- **Exporter** : *File → Export → FBX (.fbx)*, coche **Selected Objects** (si tu as sélectionné ton objet), et enregistre dans un dossier de `unity/Assets/Style/Blender/<nom>/`.

### Me faire créer un objet par script (bpy)

Blender se pilote par script Python (« bpy »). Tu n'as **rien à programmer** : tu me décris l'objet, **j'écris le script**, tu le lances.

**Ta demande** (en français, comme tu parles) : *« Un tabouret de taverne, 45 cm de haut, bois brun, assise ronde de 30 cm, trois pieds. »* Plus tu donnes de **dimensions** (en cm), de **matières** (bois, fer, tissu) et de **couleurs**, plus c'est proche.

**Ce que je te rends** : un fichier `tabouret.py`. Pour le lancer :

1. Ouvre **Blender**, onglet **Scripting** (en haut).
2. **Open** → choisis `tabouret.py` → **Run Script** (le triangle ▶).
3. L'objet apparaît ; l'export FBX se fait tout seul à l'endroit écrit dans le script (je le règle sur `Assets/Style/Blender/<nom>/`).

(En ligne de commande, sans ouvrir la fenêtre : `blender -b -P tabouret.py`.)

Voici à quoi ressemble ce que j'écris (un tabouret, volontairement simple et **léger** : quelques dizaines de triangles) :

```python
import bpy, math, os

# Repartir d'une scène vide.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# Matière : bois brun (couleur de base).
bois = bpy.data.materials.new("Bois")
bois.diffuse_color = (0.35, 0.22, 0.12, 1.0)

pieces = []

# Assise : cylindre de 30 cm de diamètre, 4 cm d'épaisseur, à 45 cm du sol.
bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.15, depth=0.04, location=(0, 0, 0.43))
pieces.append(bpy.context.active_object)

# Trois pieds, écartés de 120 degrés, un peu inclinés.
for i in range(3):
    angle = i * 2 * math.pi / 3
    x, y = 0.09 * math.cos(angle), 0.09 * math.sin(angle)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.015, depth=0.42, location=(x, y, 0.21))
    pied = bpy.context.active_object
    pied.rotation_euler = (0.12 * math.sin(angle), -0.12 * math.cos(angle), 0)
    pieces.append(pied)

# Tout réunir en un seul objet, avec sa matière.
for p in pieces:
    p.select_set(True)
bpy.context.view_layer.objects.active = pieces[0]
bpy.ops.object.join()
tabouret = bpy.context.active_object
tabouret.name = "Tabouret"
tabouret.data.materials.append(bois)

# Export FBX (dossier à adapter à ton PC, créé s'il manque).
dossier = os.path.expanduser("~/unity-eds/Assets/Style/Blender/Tabouret")
os.makedirs(dossier, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(dossier, "Tabouret.fbx"), use_selection=True)
```

Ce que je te dirai **à chaque fois** : le **nombre de triangles** de l'objet, ses **dimensions réelles**, et l'endroit exact où le déposer.

**Limites honnêtes** : un script fabrique des **formes simples** (meubles, caisses, barils, enseignes, outils grossiers, bâtiments en blocs). Un objet **organique** ou très détaillé (statue, arbre, visage) se sculpte à la main ou se trouve en CC0 ; dans ce cas je te propose plutôt une source libre (Poly Haven, Quaternius, Kenney…). Les objets venus de Blender ne sont **pas encore branchés automatiquement** au décor : dis-moi où tu veux l'objet (dans quelle maison, sur quel étal), je l'ajoute dans une version suivante.

---

## 9. Dessine une idée au crayon, envoie-la-moi

Tu n'as pas besoin de savoir modéliser : **un dessin suffit**.

1. **Dessine** sur papier (crayon, stylo, comme tu veux). Si tu peux : une vue **de face** et une vue **de profil**, même grossières.
2. **Annote** : écris les **dimensions** (« 1 m 20 de haut »), les **matières** (bois, métal, tissu), les **couleurs** (« brun rouille, ferrures noires »), et **à quoi ça sert** (« enseigne de l'auberge, accrochée au mur »).
3. **Photographie** à plat, de jour, sans ombre de la main, en entier dans l'image (la photo du téléphone suffit).
4. **Envoie-la-moi** dans la conversation, avec une phrase : *« Voici une enseigne pour l'auberge »*. (Pour la garder dans le dépôt : un dossier `docs/idees/`, avec un nom clair ; dis-le-moi et je l'ajoute.)

**Ce que je fais ensuite** :

- un **objet** : j'écris le script Blender (section 8) d'après ton dessin, et je te donne une version à regarder ;
- un **personnage** : je te donne la **recette MakeHuman** (valeurs approximatives des curseurs, vêtements à choisir, couleurs), à partir de ton dessin ;
- un **bâtiment, un décor, un monstre** : je te dis ce qu'on peut faire avec nos outils et ce qu'il vaut mieux chercher en CC0.

**Limites** : je ne vois que ce que tu m'envoies, et mon interprétation d'un dessin est **approximative** : on fait une première version, tu me dis « plus large », « moins foncé », « sans le chapeau », et on recommence. Chaque tour est rapide.

---

## 10. Aide-mémoire

1. MakeHuman : *Modelling* (corps, visage) → *Materials* (peau, yeux) → *Geometries* (cheveux, vêtements) → **Pose/Animate → Skeleton → Game engine** → *Files → Export* (**fbx, mètres**).
2. Déposer dans `unity/Assets/Style/MakeHuman/<Nom>/`.
3. Unity : **Préparer l'essai de style** (une fois), puis **Éclats du Seuil → Importer les personnages MakeHuman**.
4. `PNJ.txt` : `gaspard = Gaspard.fbx` ; le héros : **Échap → Réglages → Mon personnage…**
5. Licences : **CC0 ou CC BY seulement**, tout noté dans `MANIFESTE.md`.
6. Budget PC modeste : **15 000 triangles**, **textures 1024**.
7. Besoin d'un objet : décris-le ou dessine-le, envoie-moi.
