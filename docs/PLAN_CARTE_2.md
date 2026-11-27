# Plan de la seconde carte : les voisines des Vallons

Proposition de Claude, écrite le 27 novembre 2026 à la demande de Damien (« préparer la seconde carte », en voyant grand). **Documentation seulement** : aucun code, aucun format, aucun journal ne change avec ce document.

**Tout ce qui suit est une Recommandation (Claude)**, sauf ce qui est marqué **Retenu** (déjà décidé par Damien, avec sa source). Rien n'est tranché à la place de Damien : les arbitrages sont posés en section 9. Les statuts sont ceux de [CADRAGE.md](CADRAGE.md) et de [PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md).

Plan schématique : [plan_carte_2.png](cartes/plan_carte_2.png) (fait avec matplotlib, pas à l'échelle).

## 0. Ce qui est déjà retenu et que ce plan suit

| Sujet | Retenu | Source |
|---|---|---|
| Régions d'Orsédia | Dix régions au lancement ; Havre d'Ambre (capitale neutre, marais d'ambre, Ambriens), Forêt des Murmures (arbres géants, brume, Échos, Sylvains, Sylvebrune, Mère des Murmures, Écho de la reine Ysolde), Crêtes de Fer (montagnes, mines, forges géantes, Roche-nés, Forge-Haute, Brunhal la Forgeronne), Ruines d'Ysoria (ancienne capitale, seulement des Échos, Sire Galeran, cœur de l'Envers, **ouvertes peu à peu, la fin s'y joue**) ; dans chaque région : son Envers, 1 à 3 donjons, un boss du monde, son Gardien et son Éclat, ses métiers, sa monture, son climat qui compte | Feuille de route, sujet 9 (« on prend tout ») |
| Disposition | Carte d'Orsédia et carte de l'Envers validées le 11 novembre 2026 : Havre à l'ouest, Murmures au nord-ouest, Crêtes au nord-est, Ysoria à l'est | [PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md) § 4.0 bis |
| Sorties des Vallons | Route d'Havre (−1 908, −240), pont des Murmures (−1 520, 470 puis −1 908, 900), route du col (2 100, 1 220), route de l'Est réservée (2 188, 150 ; pont effondré, éboulement gardé par l'Ordre) | idem, V2.40 |
| Envers | Monde miroir de chaque région, failles toujours ouvertes au même point, pas de limite de durée, reprise à l'autel, la corruption de la surface s'y reflète, cheval permis, pas de saisons ni de météo propre | Sujet 9 ; décisions du 26 novembre 2026 (PR #260, #261) |
| Envers des Vallons | Niveaux de 24 à 28 dans l'ordre de la surface ; il reprend les sorties : **pont brisé flottant** des Murmures, col, **péage renversé** de la route d'Ambre (barrés pour l'instant) | Décisions du 27 novembre 2026 (PR #262), [MONDE.md](MONDE.md#lenvers-des-vallons--le-contenu-des-douze-lieux-chantier-v252a) |
| Niveaux | Vallons de 1 à 22, Gardien du Seuil au 23 ; zones **en mélange** (niveau minimum, montent au niveau du joueur) ; plafond « qui monte à chaque extension » (31 octobre 2026), avec aussi la phrase « pas de plafond de niveau » (sujet 54) : à clarifier (question Q3) | CADRAGE Q18, sujets 4 et 54 |
| Histoire | Sire Galeran est le **Sans-Repos**, chef caché des Rassembleurs ; Celui-qui-Attend, créature de l'Envers ; Maître Corvin, dernier Seuillard **figé dans l'ambre** ; méchants régionaux reliés au Sans-Repos ; grand retournement tardif (l'Éveillé est l'âme du fils d'Ysolde) ; plusieurs fins ; un Éclat par région | Sujet 2 (« l'histoire au max ») |
| Donjons, boss | 1 à 3 donjons par région, seul ou en groupe ; un boss du monde par région, signes avant ; invasions de l'Envers d'une semaine | Sujets 27 et 39 |
| Progression | **Pouvoirs de l'Éveillé** : chaque Éclat retrouvé donne un pouvoir (voir les Échos, marcher un instant dans l'Envers, franchir les failles) qui **ouvre de nouveaux chemins dans les anciennes zones** | Sujet 4 (« on prend tout ») |
| Ton | Épique et sombre, avec des respirations ; devise « Ce qui est brisé peut encore briller. » | [ECRITURE.md](ECRITURE.md) ; CADRAGE § 7 quinquies |

## 1. Vue d'ensemble

La seconde carte, c'est **l'acte II** : on sort des Vallons par les trois passages faits au V2.40, et la route de l'Est s'ouvre enfin. Quatre régions, chacune avec son Envers :

| Région | Taille | Ville | Niveaux surface | Niveaux Envers | Peuple | Méchant régional | Gardien et Éclat |
|---|---|---|---|---|---|---|---|
| **Havre d'Ambre** | 4 × 3 km | Havre d'Ambre (capitale neutre) | 24 à 28 | 28 à 30 | Ambriens, Lacustres | l'Acheteur au sceau d'ambre (la Couronne) | l'Archiviste d'Ambre ; **Éclat d'Ambre** |
| **Forêt des Murmures** | 4 × 3 km | Sylvebrune | 28 à 32 | 32 à 35 | Sylvains | la Mère des Murmures | la Gardienne de Brume ; **Éclat du Linceul** |
| **Crêtes de Fer** | 4 × 3 km | Forge-Haute | 28 à 32 | 32 à 35 | Roche-nés | Brunhal la Forgeronne | le Gardien de l'Enclume ; **Éclat de Fer** |
| **Lisière d'Ysoria** | 2 × 1,5 km | camp de l'Ordre de l'Aube | 32 à 35 | 35 | aucun : des Échos | Sire Galeran, le Sans-Repos (il fuit) | aucun (le cœur d'Ysoria reste fermé) |

- **Même architecture que les Vallons** : chaque région est une zone à région (cases de 128 m, secteurs de 256 m, V2.25.3 et V2.26), avec ses dessous et son Envers (couche miroir, V2.51). Identifiants proposés : `zone.havre_ambre`, `zone.foret_murmures`, `zone.cretes_de_fer`, `zone.lisiere_ysoria`, et `zone.envers_<région>`.
- **Ordre** : Havre d'abord (la porte du monde, niveau 24 juste après le Gardien), puis Murmures et Crêtes **dans l'ordre que le joueur veut** (zones en mélange : toutes deux à 28 minimum), puis la Lisière d'Ysoria, qui demande les trois Éclats (question Q4).
- **Durée visée** : environ 25 à 35 heures de plus (quête principale de 4 chapitres, une quarantaine de quêtes secondaires, 5 donjons, 3 boss du monde), dans l'esprit de « plein de quêtes différentes » (sujet 54).

## 2. Les régions et leurs raccords

### 2.1 Raccords avec les Vallons

Chaque région voisine a son propre repère (x vers l'est, z vers le nord, comme les Vallons). Le passage est une porte entre deux zones à région (court chargement, retenu au sujet 9), avec le cheval et le compagnon emportés (nouveau, section 5, N1).

| Passage | Côté Vallons (existant, V2.40) | Côté voisine (proposé) | Envers |
|---|---|---|---|
| Route d'Havre | péage, bord ouest (−1 908, −240) | bord est d'Havre, péage ambrien (2 188, −240) | péage renversé → Envers d'Havre |
| Pont des Murmures | pont de pierre (−1 520, 470), bord ouest côté nord (−1 908, 900) | coin sud-est de la forêt, orée des brumes (2 188, −1 300) | **pont brisé flottant** → Envers des Murmures |
| Route du col | col, bord nord-est (2 100, 1 220) | coin sud-ouest des Crêtes, refuge du col (−1 800, −1 614) | arche du col renversée → Envers des Crêtes |
| Route de l'Est | pont effondré, bord est (2 188, 150) | bord ouest de la Lisière, au pont de l'Est | dans l'Envers, **le pont est entier** (voir 4.4) |
| Havre ↔ Murmures | — | chemin des dunes d'ambre, du nord d'Havre au sud-ouest de la forêt | idem dans l'Envers |

Sorties **réservées** pour la troisième carte (tracé et barrière expliquée posés dès maintenant, comme la route de l'Est au V2.40) : estuaire d'Havre vers la Côte des Brumes et Port-Brume ; route du sud d'Havre vers les Hautes Terres de l'Aube ; glacier des Crêtes vers Givre-Lune ; descente des cendres des Crêtes vers le Plateau des Cendres ; Porte des Sept Couronnes vers le cœur d'Ysoria.

### 2.2 Havre d'Ambre : « la ville où tout s'achète »

- **Identité** : la capitale neutre de la Paix d'Ambre, ville blanche et dorée au bord d'un estuaire, sous un grand dôme ; tout s'y vend, même les éclats volés. Autour, des **champs de blé doré** et les **marais d'ambre**, où la résine suinte des arbres et fige ce qui s'y attarde.
- **Biome** : estuaire, marais à résine, dunes basses, champs et moulins.
- **Ambiance** : lumière de miel le jour, lanternes et ruelles la nuit ; politesse et poignards. Musique : luths, cloches du port.
- **Saisons** : été doré (moisson, marché des moissons) ; automne de brume d'ambre sur les marais (récolte de résine) ; hiver doux et pluvieux, marées hautes qui noient les chemins bas ; printemps des lucioles d'ambre.
- **Lien avec les Vallons** : la route d'Ambre et ses caravanes (V2.41) y finissent ; le sceau d'ambre, l'ordre scellé et le passeur d'Ambre (chapitres 2 et 3) y mènent ; les prix du marché des Vallons suivent ceux d'Havre (lien des caravanes).
- **Lien avec l'Envers** : l'Envers d'Havre est **la ville figée** : l'ambre y est noire, les habitants y sont des reflets pris dans la résine en plein geste, le dôme est retourné.

### 2.3 Forêt des Murmures : « la forêt qui se souvient »

- **Identité** : arbres géants dans la brume, où les Échos murmurent avec la voix des vivants ; c'est là que la reine Ysolde a enterré son fils, avant la Rupture.
- **Biome** : forêt d'arbres géants (troncs de 10 m), sous-bois de fougères et de champignons géants, rivières lentes, gorge et cascade du pont (la suite de la gorge des Vallons).
- **Ambiance** : silence, lumière en rayons, chants lointains ; la **brume** qui change les sentiers (section 5, N4). Musique : flûtes, voix sans paroles.
- **Saisons** : automne flamboyant (le plus beau de la région) ; hiver de brume givrée ; printemps de floraison des lucioles ; été des orages secs.
- **Lien avec les Vallons** : le pont des Murmures (V2.40) ; la forêt des Loups en est l'orée lointaine (même meute, mêmes chapeaux-de-lune) ; Sauge et Frère Anselme y ont des souvenirs (quêtes secondaires).
- **Lien avec l'Envers** : la **Forêt des Silences** : les murmures s'y taisent, les arbres géants y sont morts et l'Arbre-Mère y pend la tête en bas.

### 2.4 Crêtes de Fer : « la montagne qui forge »

- **Identité** : citadelle-forge des Roche-nés taillée dans la falaise, cheminées qui fument jour et nuit ; Brunhal la Forgeronne y forge des Éclats corrompus pour les Rassembleurs.
- **Biome** : montagne, alpages, glaciers au nord, lac de soufre et sources chaudes, galeries.
- **Ambiance** : vent, fer, braises ; le **froid** compte hors des chemins (N4). Musique : marteaux en rythme, cors graves.
- **Saisons** : hiver qui ferme le col (déjà retenu au V2.44) et ouvre les pistes de glace ; printemps des avalanches (passages qui ferment et rouvrent) ; été de la transhumance des bouquetins ; automne de la grande coulée de Forge-Haute (fête de métier).
- **Lien avec les Vallons** : la route du col (V2.40), le fort des bandits qui la surveille ; Maëlle, la forgeronne des Vallons, est une Roche-née partie de Forge-Haute (quête secondaire « La lettre de Maëlle ») ; les filons de la mine des Vallons sont la pointe de ceux des Crêtes.
- **Lien avec l'Envers** : **la Forge éteinte** : les feux y brûlent violet et froid, et l'on y forge des âmes.

### 2.5 Lisière d'Ysoria : « là où tout a commencé »

- **Identité** : la première enceinte de l'ancienne capitale des Sept Couronnes, où le Seuil a éclaté. Personne n'y vit : seulement des Échos, rejouant les gestes d'il y a trois cents ans. **Seule la lisière s'ouvre** (retenu : « ouvertes peu à peu ») ; le cœur reste fermé par la Porte des Sept Couronnes.
- **Biome** : faubourgs effondrés, aqueduc brisé, lierre violet, failles à ciel ouvert.
- **Ambiance** : la lune fendue toujours visible, même le jour ; les Échos qui rejouent la Rupture (la foule qui fuit, les cloches) ; la plus forte corruption du monde.
- **Saisons** : à la surface, oui, mais assourdies (ni fleurs ni feuilles : la neige y tombe grise).
- **Lien avec les Vallons** : les **terres corrompues des Vallons sont un bras d'Ysoria** : la route de l'Est passe entre elles et la mine ; purifier la Lisière affaiblit la source des Vallons (N8).
- **Lien avec l'Envers** : l'Envers de la Lisière montre, au loin, **le cœur de l'Envers** (la porte de lumière, devenue violette) : la promesse de la troisième carte.

## 3. Les grands lieux, leurs dessous et leur Envers

Niveaux proposés ; « ville » = sans ennemis (comme le Bourg). Chaque lieu a son Envers à +4 environ, dans l'ordre de la surface (règle retenue pour les Vallons le 27 novembre 2026, étendue ici en recommandation).

### 3.1 Havre d'Ambre (24 à 28 ; Envers 28 à 30)

| Lieu | Niv. | Contenu | Envers |
|---|---|---|---|
| Péage ambrien et champs dorés | 24 | arrivée de la route d'Havre, moulins, fermes, épouvantails, caravanes | champs de blé noir, moulins à l'arrêt, faucheurs renversés (28) |
| **Cité de Havre d'Ambre** | ville | remparts blancs, Porte d'Ambre, dôme du **Palais de la Couronne**, **hôtel des ventes**, banque, **arène**, quartier des Guildes (Guilde des aventuriers, grand siège), quartier des maisons (terrain du sujet 8), bas-quais et port fluvial | **Havre figé** : habitants pris dans l'ambre noire en plein geste, dôme retourné, marché silencieux (28) |
| Bas-quais (nuit) | 25 | coupe-bourses, contrebandiers, ruelles | reflets des noyés du port (29) |
| **Marais d'ambre** et **Mirelune** (village lacustre) | 25 à 26 | arbres à ambre, blocs d'ambre où sont figées des bêtes anciennes, ambreries, pontons, salamandres d'ambre, crapauds-buffles | marais où les reflets se lèvent de la résine (29) |
| Estuaire et phare | 27 | phare de l'estuaire, épaves, sortie fermée vers la Côte des Brumes | phare éteint qui éclaire en violet (30) |
| **Catacombes d'Ambre** (donjon, 2 niveaux, sous la cité) | 26 à 27 | ossuaires des Sept Couronnes, archives de la Couronne, Rassembleurs ; chef : **l'Archiviste d'Ambre** (Gardien d'Havre, Seuillard rongé par la folie, 3 formes) | — |
| **Ambrerie engloutie** (donjon, 2 niveaux, sous les marais) | 27 à 28 | caves à résine noyées (la marée des grottes noyées, réutilisée), **tombeau de Maître Corvin** figé dans l'ambre ; chef : **le Conservateur** (Ambrien figé revenu, 2 formes) | — |
| Boss du monde : **Léviathan d'ambre** | 30 | salamandre géante des marais, signes avant : la résine bouillonne, les oiseaux fuient | — |

### 3.2 Forêt des Murmures (28 à 32 ; Envers 32 à 35)

| Lieu | Niv. | Contenu | Envers |
|---|---|---|---|
| Orée des brumes (pont des Murmures) | 28 | suite de la gorge, cascade du pont, poste sylvain | pont brisé flottant, orée muette (32) |
| **Sylvebrune** | ville | ville dans les arbres géants : maisons-nids, ponts suspendus, escaliers en spirale, lanternes-lucioles ; conseil des Sylvains | Sylvebrune pendue, nids vides (32) |
| Bois des géants | 29 | les plus grands arbres, loups-mousse, araignées de brume | arbres morts géants (33) |
| **Clairière de la Reine** et **chapelle du Linceul** | 30 | tombeau du fils d'Ysolde, statue de la reine, **Écho de la reine Ysolde** | la chapelle intacte, comme au jour de l'enterrement (34) |
| Cercle de brume (ruines des Seuillards) | 31 | pierres runiques, stèles en langue de l'Envers ; chef : **la Gardienne de Brume** (Gardienne des Murmures, 3 formes) | cercle silencieux (34) |
| **Cœur des Murmures** (l'Arbre-Mère) | 32 | la **Mère des Murmures**, chef de chapitre (3 formes) | Arbre-Mère pendu (35) |
| **Gouffre des Racines** (donjon, 3 niveaux) | 30 à 32 | racines-ponts, lacs souterrains, nids d'araignées ; chef : **Racine-qui-Boit** | — |
| Boss du monde : **Cerf-Brume** | 33 | cerf géant aux bois de brume ; signes : la brume tombe d'un coup, les Échos se taisent | — |

### 3.3 Crêtes de Fer (28 à 32 ; Envers 32 à 35)

| Lieu | Niv. | Contenu | Envers |
|---|---|---|---|
| Refuge du col (côté Crêtes) | 28 | refuge, lacets, bouquetins | arche du col renversée (32) |
| **Viaduc des Ancêtres** | 29 | pont géant sur le gouffre, statues des ancêtres | viaduc suspendu en morceaux dans le vide (33) |
| **Forge-Haute** | ville | citadelle taillée dans la falaise, grande forge, haut-fourneau, ascenseurs à contrepoids, maisons roche-nées | **Forge éteinte** : flammes violettes et froides (32) |
| Sources chaudes et lac de soufre | 30 | bassins fumants (ils réchauffent : N4), chiens de forge sauvages | lac de soufre gelé (34) |
| Halle des Ancêtres | 31 | tombes taillées des Roche-nés, l'**Enclume des Éclats** ; chef : **le Gardien de l'Enclume** (Gardien des Crêtes, 3 formes) | halle où les ancêtres sont des reflets (34) |
| Glacier du nord | 31 | crevasses, sortie fermée vers Givre-Lune | glacier fendu (35) |
| **Grande Fonderie de Brunhal** (donjon, 3 niveaux) | 30 à 32 | galeries de coulée, rivière de métal, golems de scories ; chef de chapitre : **Brunhal la Forgeronne** (3 formes) | — |
| Boss du monde : **Bête des cimes** | 33 | bête-bélier géante ; signes : la montagne gronde, avalanches | — |

### 3.4 Lisière d'Ysoria (32 à 35 ; Envers 35)

| Lieu | Niv. | Contenu | Envers |
|---|---|---|---|
| Pont de l'Est | 32 | le pont effondré, rebâti par un chantier (V2.43) ou franchi par l'Envers (4.4) | **pont entier** (35) |
| Camp de l'Ordre de l'Aube | — | avant-poste de l'Ordre, Sœur Albe et ses supérieurs ; point sûr | — |
| Faubourgs écroulés | 33 | Échos de la foule qui fuit, chevaliers-échos des Sept Couronnes | faubourgs debout, peuplés d'âmes (35) |
| Aqueduc brisé | 34 | aqueduc à franchir, gargouilles-échos | — |
| **Porte des Sept Couronnes** | 35 | sept statues couronnées ; **Sire Galeran, le Sans-Repos** (chef de fin d'acte : il fuit à mi-vie) | vue sur le cœur de l'Envers |

## 4. Ennemis, chefs et trame

### 4.1 Bestiaire (niveaux visés)

| Région | Communs | Élites et chefs | Boss du monde |
|---|---|---|---|
| Havre | coupe-bourses et contrebandiers (24 à 25), salamandres d'ambre (25), crapauds-buffles (26), Rassembleurs (26 à 27), gardes de la Couronne corrompus (27) | Archiviste d'Ambre (27), Conservateur (28), l'Acheteur (28, chapitre) | Léviathan d'ambre (30) |
| Murmures | loups-mousse (28), araignées de brume (29), racineux (30), Échos sylvains (30) | Gardienne de Brume (31), Racine-qui-Boit (32), Mère des Murmures (32) | Cerf-Brume (33) |
| Crêtes | bouquetins sauvages (28), chiens de forge (29), golems de scories (30), Roche-nés renégats (30 à 31) | Gardien de l'Enclume (31), Brunhal (32) | Bête des cimes (33) |
| Lisière | chevaliers-échos (33), gargouilles-échos (34) | Sire Galeran (35) | — |
| Envers | reflets de chaque espèce (+4), habitants renversés, chiens de cendre | Reflet de l'Acheteur (30), Reflet d'Ysolde (35), Reflet de Brunhal (35) | invasions (N7) |

Règle des échos (retenue le 5 novembre 2026) : tout ce qui revient après sa mort ou sort du Seuil est un écho ; les bêtes de la forêt et de la montagne qui se repeuplent n'en sont pas.

### 4.2 Factions

| Faction | Rôle dans l'acte II | Réputation (V2.7) |
|---|---|---|
| **Couronne d'Ambre** | divisée : une moitié achète les éclats pour l'Acheteur, l'autre (Renart) veut le démasquer | oui, deux voies qui s'excluent : loyalistes ou réformateurs |
| **Ordre de l'Aube** | Sœur Albe a écrit ; ses supérieurs arrivent, tiennent le camp d'Ysoria ; un traître dans leurs rangs (piste des Hautes Terres) | oui |
| **Rassembleurs** | partout : ils achètent à Havre, forgent aux Crêtes, chantent aux Murmures | non (ennemis) |
| **Guilde des aventuriers** | grand siège à Havre : primes de toutes les régions, rangs nouveaux | oui (déjà) |
| **Sylvains** | conseil de Sylvebrune, méfiant envers les Éveillés | oui |
| **Roche-nés** | clans de Forge-Haute, divisés par Brunhal | oui |
| **Fêlés** | **Nyx** apparaît aux Murmures ; un Fêlé entend Celui-qui-Attend comme Ysarn | non (préparation des Îles Fendues) |

### 4.3 Quête principale : acte II, « Les trois Éclats »

| Chapitre | Région | Niv. | Résumé |
|---|---|---|---|
| **6. Le sceau d'ambre** | Havre | 24 à 28 | On suit le sceau d'ambre jusqu'à la capitale. Renart, espion ambrien, aide en mentant. Le passeur d'Ambre est enfin nommé ; l'Acheteur est un conseiller de la Couronne. Dans l'Ambrerie engloutie, on trouve **Maître Corvin figé dans l'ambre** : on le libère à moitié (sa tête parle, son corps reste pris). Il connaît les Gardiens. Fin : l'Archiviste d'Ambre vaincu, **Éclat d'Ambre** et son souvenir (Galeran choisit la reine contre l'Ordre). Pouvoir : **Vue des Échos**. |
| **7. La Mère des Murmures** | Murmures | 28 à 32 | Corvin envoie le joueur à la chapelle du Linceul. L'**Écho de la reine Ysolde** cherche encore son fils et appelle le joueur « mon petit » (graine du grand retournement, sans rien dire). La Mère des Murmures se nourrit des voix des morts pour les Rassembleurs. Fin : Gardienne de Brume, **Éclat du Linceul**, souvenir (le petit linceul blanc). Pouvoir : **Pas d'Envers** (trois secondes dans l'Envers sans faille). |
| **8. Le feu de Forge-Haute** | Crêtes | 28 à 32 | Maëlle écrit à sa sœur de Forge-Haute ; les clans se déchirent. Brunhal forge des « clés » d'Éclats pour rouvrir le Seuil. Fin : Gardien de l'Enclume, **Éclat de Fer**, souvenir (la forge qui a taillé la Porte de lumière). Pouvoir : **Franchir les failles fermées**. |
| **9. La route de l'Est** | Lisière | 32 à 35 | Les trois Éclats ouvrent la route de l'Est : par l'Envers d'abord (le pont y est entier), puis par le pont rebâti. À la Porte des Sept Couronnes, le Sans-Repos se montre : **c'est Sire Galeran**, celui dont le joueur revit les souvenirs depuis le chapitre 1. Il prend un Éclat et fuit dans l'Envers. La voix de la vieille porte du moulin parle par la bouche d'Ysarn : Celui-qui-Attend « n'attend plus ». |

Ordre : chapitre 6 d'abord ; 7 et 8 dans l'ordre choisi (zones en mélange) ; 9 quand les deux sont finis (Q4). Chaque chapitre suit le modèle des Vallons : une principale à son nom et deux à quatre secondaires.

### 4.4 Quêtes secondaires (choix ; une quarantaine au total)

- **Havre** : « La dette de Renart » ; « Le marché des moissons » (fête locale) ; « Les lucioles d'ambre » ; « Le crapaud qui parlait » (Lacustres, humour) ; « Le dernier sceau » (réformateurs contre loyalistes) ; « Maison à Havre » (le terrain du quartier des maisons, sujet 8) ; primes de l'arène.
- **Murmures** : « La lettre de Sauge » ; « Le loup blanc de la forêt d'en face » (Gaspard) ; « Ce que disent les arbres » (langue de l'Envers, N9) ; « Le nid tombé » ; « Nyx » (première rencontre).
- **Crêtes** : « La lettre de Maëlle » ; « Le bouquetin borgne » (monture, N6) ; « La grande coulée » (fête de métier) ; « Les ancêtres oubliés » ; « Le refuge du col » (sauvetage d'hiver).
- **Lisière et transversales** : « Le pont de l'Est » (chantier collectif, lieux reconstruits) ; « Les cinq lignes » (une cinquième ligne gravée, à la Lisière : « Il n'est pas mort. ») ; « L'Ordre se souvient » (Sœur Albe, le traître).
- **Envers** : « Les reflets de la reine » : dans chaque Envers, un fragment de la vie d'Ysolde avant la Rupture (secrets et stèles, schéma 18), qui prépare la fin cachée.

### 4.5 Suite de l'histoire du Seuil

- Ce que le joueur apprend : qui achète (l'Acheteur), pourquoi (rouvrir le Seuil), **qui commande (Galeran)**, et que les Gardiens étaient des Seuillards rongés par la folie.
- Ce qui reste caché pour la troisième carte : que veut Celui-qui-Attend ; le grand retournement (l'Éveillé et le fils d'Ysolde : seulement des indices) ; le traître de l'Ordre (Hautes Terres) ; Morvaine et la Côte des Brumes ; le cœur d'Ysoria.
- Corvin, mentor peu sûr (retenu) : il a besoin des trois Éclats **pour se libérer de l'ambre**. Ce qu'il fera libéré reste ouvert.

## 5. Mécaniques nouvelles

Tailles : S (quelques jours d'agent), M (une semaine), L (deux semaines ou plus), comme [PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md) § 7.

| # | Mécanique | Ce qu'elle fait | Taille | Format touché |
|---|---|---|---|---|
| N1 | **Passages entre régions** | Porte entre deux zones à région (court chargement) avec cheval, compagnon et quête suivie emportés ; carte du monde qui montre les régions découvertes ; voyage rapide entre points sûrs de surface de régions différentes | M | zones (schéma 19 : `regionLink`), sauvegarde (région courante) |
| N2 | **Niveaux 25 à 35** | Plafond relevé (Q3), courbe d'expérience, catalogue d'objets au-delà du niveau 20 (aujourd'hui le plafond du catalogue), talents des nouveaux niveaux | M | règles (contenu 0.19.0), catalogue d'objets v9 |
| N3 | **Pouvoirs de l'Éveillé** | Un pouvoir par Éclat : **Vue des Échos** (voir et suivre les traces d'Échos, stèles cachées), **Pas d'Envers** (3 s dans l'Envers : traverser un obstacle marqué), **Franchir les failles fermées** ; chacun **ouvre des chemins nouveaux dans les Vallons** (une dizaine de passages et de secrets déjà semés) | L | moteur (pas d'Envers : nouvel état, journal et instantané nouveaux), zones (passages à pouvoir), sauvegarde |
| N4 | **Climat qui compte** | **Froid** des Crêtes : jauge hors des chemins et loin des feux, qui ralentit puis coupe la régénération (Q10) ; feux, sources chaudes et manteaux la vident. **Brume** des Murmures : vue réduite et sentiers balisés qui changent selon l'heure du monde (calculés, pas simulés) | M | saisons (schéma 3 : climat de région), règles |
| N5 | **Capitale** | Hôtel des ventes (en solo : courtiers PNJ aux prix vivants, V2.10 ; le vrai, entre joueurs, au lot 13 ; Q9), banque (coffre commun aux personnages), arène (vagues, classement local) | L (3 PR) | marchands v8, sauvegarde |
| N6 | **Monture régionale** | **Bouquetin des Crêtes** : passe les pentes raides marquées (raccourcis de montagne) ; le cheval des marais d'Havre plus tard | M | monture (moteur : passages à monture), sauvegarde |
| N7 | **Invasions de l'Envers** | Retenu au sujet 39 (une semaine, qui change le monde) : quand la corruption d'une région reste haute, une **faille géante** s'ouvre, l'Envers déborde (reflets à la surface, ciel violet) ; elle se referme en purifiant trois autels et en battant son héraut | L | corruption v3, événements du monde |
| N8 | **Veines de corruption** | Chaque région a sa source et ses autels (V2.42 réutilisé) ; les sources sont reliées : celle d'Ysoria nourrit celle des Vallons ; purifier la Lisière donne un répit aux Vallons | S | corruption v3 |
| N9 | **Langue de l'Envers** | Les stèles (schéma 18) donnent des mots ; avec assez de mots, des répliques et des passages cachés s'ouvrent (Atlas de la Rupture) | M | histoire (schéma 17), sauvegarde |
| N10 | **Ascenseurs et nacelles** | Portes de genre « puits » entre deux points de surface (Forge-Haute, viaduc), réutilisant les portes et l'ascenseur de la mine | S | zones (genre de porte) |

## 6. Découpage en sous-lots

À la suite des V2.5x (V2.53 à V2.56 sont pris). Même méthode que [PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md) : chaque lieu « habité » avant d'être « beau », découpé en « a » (cœur) et « b » (Unity) quand c'est utile.

### Étape 0 : les fondations

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.57** | Passages entre régions (N1) ; carte du monde ; tracé des sorties réservées | M | V2.40, V2.45 | aucun |
| **V2.58** | Niveaux 25 à 35 (N2) : règles, courbe, catalogue d'objets v9 (paliers 24 à 35) | M | — | aucun |
| **V2.59** | Horizon d'Orsédia : silhouettes lointaines des trois villes vues des Vallons et des voisines | S | V2.29 | 3 silhouettes |

### Étape 1 : Havre d'Ambre

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.60** | Surface d'Havre (4 × 3 km) et la cité (remparts, quartiers, PNJ et journées) | L | V2.57 | kit de la cité |
| **V2.60.1** | Capitale (N5) : courtiers, banque, arène | L | V2.60 | intérieurs de la capitale |
| **V2.61** | Marais d'ambre, Mirelune, champs dorés, estuaire ; corruption d'Havre (N8) | M | V2.60 | kit des marais, 2 créatures |
| **V2.62** | Chapitre 6 et ses secondaires ; pouvoir Vue des Échos (N3, 1re partie) | L | V2.61, V2.58 | Corvin figé, 2 humains |
| **V2.63** | Catacombes d'Ambre (donjon) | M | V2.60, V2.45 | kit des catacombes, Archiviste |
| **V2.63.1** | Ambrerie engloutie (donjon, marée réutilisée) | M | V2.61, V2.50.1 | kit de l'ambrerie |
| **V2.64** | Léviathan d'ambre, boss du monde | M | V2.61 | 1 créature |

### Étape 2 : Forêt des Murmures (peut avancer en parallèle de l'étape 3)

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.65** | Surface de la forêt et Sylvebrune | L | V2.57 | kit de la forêt et de Sylvebrune |
| **V2.66** | Brume et sentiers mouvants (N4, 1re partie) | M | V2.65 | 1 pierre-guide |
| **V2.67** | Gouffre des Racines (donjon, 3 niveaux) | L | V2.65, V2.45 | kit du gouffre, 2 créatures |
| **V2.68** | Chapitre 7, clairière de la Reine ; pouvoir Pas d'Envers (N3, 2e partie) | L | V2.62, V2.66 | chapelle, Écho d'Ysolde, Mère des Murmures |
| **V2.69** | Cerf-Brume, boss du monde | M | V2.65 | 1 créature |

### Étape 3 : Crêtes de Fer

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.70** | Surface des Crêtes et Forge-Haute ; ascenseurs (N10) | L | V2.57 | kit de Forge-Haute |
| **V2.71** | Froid (N4, 2e partie) et bouquetin (N6) | M | V2.70 | feu de refuge, bouquetin |
| **V2.72** | Grande Fonderie (donjon, 3 niveaux) | L | V2.70, V2.45 | kit de la fonderie, 2 créatures |
| **V2.73** | Chapitre 8, Halle des Ancêtres ; pouvoir Franchir les failles (N3, 3e partie) | L | V2.62, V2.71 | halle, Brunhal, enclume |
| **V2.74** | Bête des cimes, boss du monde | M | V2.70 | 1 créature |

### Étape 4 : Lisière d'Ysoria

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.75** | Ouverture de la route de l'Est, chantier du pont (V2.43), Lisière (2 × 1,5 km), veine vers les Vallons (N8) | M | V2.57, V2.43 | kit de la Lisière |
| **V2.76** | Chapitre 9 ; Galeran ; langue de l'Envers (N9) | M | V2.68, V2.73, V2.75 | Galeran, trône brisé |

### Étape 5 : l'Envers de la seconde carte

| Sous-lot | Contenu | Taille | Dépend de | Modèles |
|---|---|---|---|---|
| **V2.77** | Couches de l'Envers des quatre régions (reflet, failles, autels), passages depuis l'Envers des Vallons (pont brisé flottant, col, péage renversé), pont de l'Est entier | M | V2.51, V2.57 et chaque surface | variantes de l'Envers |
| **V2.78** | Contenu de l'Envers (lieux, reflets, chefs, secrets, « Les reflets de la reine ») | L | V2.77 | pièces de l'Envers, Reflet d'Ysolde |
| **V2.79** | Invasions de l'Envers (N7) | L | V2.77, V2.61 | faille géante, héraut |

Chemin le plus court vers une seconde carte jouable : V2.57, V2.58, V2.60, V2.61, V2.62 (Havre et le chapitre 6), puis les autres en parallèle.

## 7. Kits Tripo et coût estimé

Barème donné : **environ 25 crédits** par modèle statique (20 à 30), **65** par créature animée. **La marche bipède de Tripo échoue** (golem de cristal, V2.50b) : créatures **quadrupèdes** ou à pattes multiples quand elles marchent (rig de Tripo, comme le rat et l'araignée), et **humanoïdes** en modèle statique Tripo puis rig et animations **Mixamo** ou **Blender** (comme les personnages actuels ; [CREDITS.md](CREDITS.md)). Les humanoïdes ordinaires (gardes, bandits, contrebandiers, moines) **réutilisent** les personnages existants : aucun crédit.

| Sous-lot | Modèles | Statiques | Animés | Crédits |
|---|---|---|---|---|
| V2.59 | silhouettes : dôme d'Havre, arbres géants de Sylvebrune, Forge-Haute | 3 | 0 | 75 |
| V2.60 | rempart blanc (droit, tour), Porte d'Ambre, dôme du Palais, hôtel des ventes, arène, maisons ambriennes (3 gabarits), quai et grue, pont à arches, phare, fontaine, moulin doré | 14 | 0 | 350 |
| V2.60.1 | comptoir de banque, coffre-fort, étal de courtier, gradins d'arène, râtelier d'arène, tableau des enchères | 6 | 0 | 150 |
| V2.61 | arbre à ambre (2), bloc d'ambre avec bête figée, cuve de résine, maison lacustre, barque plate, épouvantail ; **salamandre d'ambre**, **crapaud-buffle** (quadrupèdes) | 7 | 2 | 305 |
| V2.62 | Maître Corvin dans l'ambre ; Renart et l'Acheteur (humanoïdes, Mixamo) | 3 | 0 | 75 |
| V2.63 | kit des catacombes (murs, voûtes, ossuaire, archives, sarcophage royal, grille, escalier, colonnes, autel, rayonnages, lustre, coffre) ; Archiviste (humanoïde, Mixamo) | 13 | 0 | 325 |
| V2.63.1 | kit de l'ambrerie (cuves noyées, coulées d'ambre, piliers, tombeau de Corvin, échafaudage, bassin, rocher d'ambre, coffre, passerelle, vanne) | 10 | 0 | 250 |
| V2.64 | **Léviathan d'ambre** (quadrupède) | 0 | 1 | 65 |
| V2.65 | arbre géant (3 tailles), maison-nid, pont suspendu d'arbre, escalier en spirale, lanterne-luciole, autel sylvain, champignons géants (2), racines arquées, pierre runique | 12 | 0 | 300 |
| V2.66 | pierre-guide | 1 | 0 | 25 |
| V2.67 | kit du gouffre (parois de racines, racine-pont, lac souterrain, nid de soie, colonne de racines, éboulis, champignons lumineux, cocon, autel de racines, coffre, passerelle, cascade souterraine, entrée) ; **racineux** (quadrupède), **araignée de brume** (8 pattes) | 13 | 2 | 455 |
| V2.68 | chapelle du Linceul, tombeau de l'enfant, statue d'Ysolde ; Écho d'Ysolde (humanoïde, Mixamo) ; Mère des Murmures (statique, animée dans Blender) | 5 | 0 | 125 |
| V2.69 | **Cerf-Brume** (quadrupède) | 0 | 1 | 65 |
| V2.70 | rempart de roche (2), grande forge, haut-fourneau, cheminée, viaduc (2 pièces), statue d'ancêtre, maisons roche-nées (2), refuge, bassin fumant, enseigne de forge | 13 | 0 | 325 |
| V2.71 | feu de refuge ; **bouquetin** (monture, quadrupède) | 1 | 1 | 90 |
| V2.72 | kit de la fonderie (galerie de coulée, rivière de métal, creuset, moules, chaînes, grue, enclume géante, porte de fer, piliers, scories, passerelle, coffre, autel des clés) ; golem de scories (statique, animé dans Blender) ; **chien de forge** (quadrupède) | 14 | 1 | 415 |
| V2.73 | halle des Ancêtres (3 pièces), Enclume des Éclats ; Brunhal (humanoïde, Mixamo) | 5 | 0 | 125 |
| V2.74 | **Bête des cimes** (quadrupède) | 0 | 1 | 65 |
| V2.75 | pont de l'Est (ruine, reconstruit), aqueduc (2), colonnes et arches (3), tente et palissade de l'Ordre (2), Porte des Sept Couronnes, statue couronnée | 11 | 0 | 275 |
| V2.76 | Galeran (humanoïde, Mixamo), trône brisé | 2 | 0 | 50 |
| V2.77 | dôme retourné, maison figée, arbre géant mort (2), forge éteinte (2), pont de l'Est entier | 7 | 0 | 175 |
| V2.78 | pièces de l'Envers (8) ; Reflet d'Ysolde (humanoïde, Mixamo) | 9 | 0 | 225 |
| V2.79 | faille géante ; **héraut de l'invasion** (chien de cendre géant, quadrupède) | 1 | 1 | 90 |
| **Total** | | **150** | **10** | **4 400** |

- **Par région** : Havre 1 520 (avec V2.59 : 1 595) ; Murmures 970 ; Crêtes 1 020 ; Lisière 325 ; Envers 490.
- **Réutilisé sans crédit** : personnages Mixamo existants, loups, ours, sangliers et chiens de cendre (reflets avec le rendu de faille), kits de mine, de grotte noyée et de crypte (piliers, escaliers, coffres), ciel et matériaux de l'Envers, toits enneigés par shader.
- **Variante économe** (Q11) : réutiliser davantage (une maison par ville, kits de donjons croisés) ramène le total vers **2 500**.
- Aucune dépense n'est faite sans l'accord de Damien (CLAUDE.md) : chaque kit est soumis avec son sous-lot.

## 8. Risques

- **Volume** : quatre régions, c'est quatre fois les Vallons en surface. Les gabarits de points d'intérêt et l'outil de densité (`eds carte densite`) sont indispensables ; une région peut sortir « habitée » avant d'être « belle ».
- **Pas d'Envers** (N3) touche le moteur : nouvel état de combattant, format de journal et instantané nouveaux, nouveaux journaux figés. C'est le seul point proche d'une rupture : à confirmer par Damien avant le V2.62 (question Q8).
- **Téléphone** : une capitale dense est le premier mur de mémoire (comme le Bourg). Atlas par quartier, mesures au V2.60.
- **Humanoïdes animés** : dépendent de Mixamo ou de Blender (la marche bipède de Tripo échoue).

## 9. Questions pour Damien

Rien n'est tranché : chaque ligne reste une **recommandation** tant que Damien n'a pas répondu.

| # | Question | Options | Recommandation (Claude) |
|---|---|---|---|
| Q1 | Périmètre de la seconde carte | **A.** Havre, Murmures, Crêtes et la Lisière d'Ysoria ; **B.** les trois régions seulement, route de l'Est toujours fermée ; **C.** les quatre, avec Ysoria entière | **A** : la route de l'Est réservée depuis le V2.40 trouve sa suite, et le cœur d'Ysoria reste pour la fin |
| Q2 | Taille des régions | **A.** toutes à 4 × 3 km ; **B.** trois à 4 × 3 km, la Lisière à 2 × 1,5 km ; **C.** toutes à 2 × 1,5 km | **B** |
| Q3 | Plafond de niveau (aujourd'hui 30 dans `rules.json` ; deux phrases notées : « plafond qui monte » et « pas de plafond ») | **A.** 35 pour la seconde carte ; **B.** 40 ; **C.** pas de plafond, l'expérience ralentit | **A**, relevé à chaque carte ; la Constellation du Seuil prend le relais après |
| Q4 | Ordre des régions | **A.** linéaire : Havre, Murmures, Crêtes, Ysoria ; **B.** Havre d'abord, Murmures et Crêtes au choix, Ysoria à la fin ; **C.** tout libre | **B** |
| Q5 | Quand révéler que le Sans-Repos est Galeran | **A.** fin du chapitre 9, à la Porte des Sept Couronnes ; **B.** plus tard (troisième carte) ; **C.** dès Havre, par Corvin | **A** |
| Q6 | Ouverture de l'Envers des voisines | **A.** en même temps que la surface ; **B.** l'Envers d'une région s'ouvre à la fin de son chapitre ; **C.** seulement par les passages de l'Envers des Vallons | **B**, et les passages de l'Envers des Vallons s'ouvrent au même moment |
| Q7 | Passer le pont de l'Est | **A.** par l'Envers seulement (le pont y est entier) ; **B.** par un chantier collectif (lieux reconstruits) ; **C.** les deux : l'Envers d'abord, puis le chantier ouvre la surface | **C** (« ce qui est brisé peut encore briller ») |
| Q8 | Pouvoirs de l'Éveillé | **A.** un pouvoir actif par Éclat, qui rouvre des chemins aux Vallons (moteur touché pour le Pas d'Envers) ; **B.** des passifs seulement, sans changer le moteur ; **C.** plus tard | **A** |
| Q9 | Hôtel des ventes avant le monde partagé | **A.** courtiers PNJ en solo, le vrai au lot 13 ; **B.** seulement au lot 13 ; **C.** pas d'hôtel des ventes, des marchands | **A** |
| Q10 | Froid des Crêtes | **A.** gêne douce : ralentit et coupe la régénération, jamais de dégâts ; **B.** vrai danger : dégâts au bout de la jauge ; **C.** décor seulement | **A** |
| Q11 | Budget Tripo (environ 4 400 crédits estimés) | **A.** tout validé d'avance ; **B.** validé kit par kit, à chaque sous-lot ; **C.** version économe, environ 2 500 | **B** |
| Q12 | Monture des Crêtes | **A.** bouquetin qui passe les pentes raides (raccourcis) ; **B.** apparence seulement ; **C.** plus tard | **A** |
