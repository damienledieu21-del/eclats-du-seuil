# Plan de la grande carte des Vallons (4 × 3 km, trois étages)

Plan technique et de contenu, écrit le 11 novembre 2026 après la **décision de Damien du 11 novembre 2026, 1 h 23** (« parfait ») sur la proposition de Claude. Documentation seulement : aucun code, aucun format ni journal changé par ce document.

Statuts, comme dans [CADRAGE.md](CADRAGE.md) :

| Statut | Sens |
|---|---|
| **Retenu (Damien)** | Décidé ou validé par Damien (ici, sauf mention : le 11 novembre 2026, 1 h 23). |
| **Recommandation (Claude)** | Proposition technique ou de contenu de ce plan ; confirmée ou corrigée avant le sous-lot qui la réalise. |
| **Rupture (à valider)** | Recommandation qui change l'architecture du cœur et serait difficile à défaire : **rien n'est fait sans l'accord de Damien** (CLAUDE.md, « Quand s'arrêter et demander »). |
| **Ouvert** | Vrai arbitrage, posé en section 9. |

## 0. Ce qui est retenu (Damien, 11 novembre 2026)

- **Taille** : Vallons d'Aube agrandis à **4 × 3 km** (12 km²), environ 15 min à pied d'un bout à l'autre, 5 min à cheval ; monde découpé en **cases chargées autour du joueur** ; détail proche, formes simplifiées au loin ; pensé pour le téléphone (Note8) et le PC.
- **Trois étages** : la surface ; le **dessous** (grottes, mines de la carrière, crypte de l'abbaye, égouts du Bourg, galeries sous les pierres du Seuil) ; l'**Envers** (reflet sombre de toute la région, ouvert par les failles, décidé le 29 octobre 2026, sujet 9).
- **Douze grands lieux** : Bourg d'Aube devenu ville fortifiée ; le village actuel au cœur ; la vallée de la rivière jusqu'à une cascade de 30 m avec une grotte derrière ; les coteaux de vignes et un château viticole ; la forêt des Loups et son arbre millénaire ; les hautes collines (bergers, cairns, sommet avec vue sur toute la région) ; l'abbaye en ruine et sa crypte ; les marais du lac avec une île accessible seulement l'hiver sur le lac gelé ; la carrière devenue mine à plusieurs niveaux ; le camp des bandits dans un ancien fort ; les terres corrompues du Seuil ; trois passages vers les régions voisines (col vers les Crêtes de Fer, pont vers la Forêt des Murmures, route d'Havre d'Ambre).
- **Monde vivant** : journées des villageois, caravanes que les bandits peuvent attaquer, corruption du Seuil qui s'étend vraiment si on ne la repousse pas, lieux reconstruits qui le restent, saisons qui changent la carte (lac gelé, vendanges, neige qui bloque le col).
- **Horizon** : depuis le sommet, Crêtes de Fer au loin, brume de la Forêt des Murmures, lune fendue la nuit.
- **Ordre** : découpage en cases et surface d'abord, puis les lieux un par un, puis le dessous, puis l'Envers. Le relief doux d'affichage est en cours (V2.24, branche `v2.24-relief`, hors de ce plan) ; le vrai relief qui compte en combat reste pour plus tard (L11-D6).

## 1. L'existant et ses plafonds

Relevé dans le code de `main` (`fd7e62a`).

| Élément | Aujourd'hui | Où | Bloque la grande carte ? |
|---|---|---|---|
| Aire des Vallons | 240 × 170 m, de [−100, −70] à [140, 100], une seule session de zone `zone.vallons` | `scenarios/zones/vallons.json`, `Zone` | Oui : une seule simulation pour toute la région |
| Coordonnées du moteur | ±1000 m (`Planar.MaxCoordinate`) ; vecteurs ±2000 m | `EdS.Domain/Space/Planar.cs` | Oui : la région fait 4 096 m de large |
| Obstacles | 64 par espace (`Planar.MaxObstacles`), 59 utilisés | `Planar.cs` | Oui : le Bourg fortifié seul en demande plus |
| Combattants | 128 par session (borne des règles 256), **morts et créatures qui lâchent compris** : 98 réapparitions par session, puis `Exhausted` | `rules.json`, `combat.limits.maxCombatants` | Oui pour une session qui reste ouverte des heures sur un serveur |
| Grille de navigation | case de 0,5 m, au plus 262 144 cases (512 × 512, soit 256 m de côté) ; au-delà la case double | `NavGrid.MaxCells` | Oui au-delà de 256 m de côté |
| Registre d'une sauvegarde | 100 000 clés (`zone.vallons/<session>/death/<id>` comprises) | `SaveValidator.MaxConsumed` | À terme : les morts s'accumulent |
| Données de zone | 64 lieux, 32 points sûrs, 16 portes, 32 routes de 32 points, 256 populations, 64 zones reliées | `ZoneValidator` | Oui pour une région entière dans un seul fichier |
| Portes | paires de portes entre zones ; passer une porte = sauvegarde, nouvelle session à la porte d'en face, effet de halte (question déjà posée) | `WorldSession.Travel`, [MONDE.md](MONDE.md#intérieurs) | Non : réutilisées pour le dessous et l'Envers |
| Laisse | relative à la **cible** (écart entre emprises > 18 à 20 m) ; aucune limite autour du point de départ | `ZonePopulations` | Oui : une créature aussi rapide que le joueur le suit sans fin |
| Affichage | `SpaceView` construit tout le décor de la zone d'un coup ; ressortir d'une maison reconstruit tout (à-coup sur téléphone) | `unity/Assets/EdS/Game/SpaceView*.cs`, `StyleWorld.cs` | Oui : il faut charger par cases |
| Monde vivant | journées des PNJ (V2.3), événements (V2.5), cicatrices du Seuil (V2.6) : **fonctions pures du temps du monde**, état minimal dans la sauvegarde du personnage | `WorldSession.Living`, `WorldEvents`, `WorldScars` | Non : ce modèle s'étend bien (section 5) |

Le jeu tourne encore **en solo sur le poste** (`AdventureSession`, `EdS.Runtime`) ; le monde partagé est le lot 13. Le plan est fait pour que le même code serve sur le serveur.

## 2. Architecture des cases

### 2.1 Repère de la région (Recommandation)

- **Un seul repère pour toute la région**, en mètres : `x` vers l'est, `z` vers le nord, comme aujourd'hui. **Le cœur garde ses coordonnées actuelles** : aucune quête, aucun point sûr, aucune porte, aucun lieu nommé ne bouge.
- **Étendue** : x de **−1 908 à +2 188**, z de **−1 358 à +1 714** (4 096 × 3 072 m, soit 12,6 km² : les « 4 × 3 km » arrondis à des puissances de deux).
- **Case** : carré de **128 m**. 32 colonnes × 24 rangées = **768 cases**. Nom : `c<colonne>_r<rangée>` (colonne 0 à l'ouest, rangée 0 au sud). Bords des cases : `x = −1 908 + 128 k`, `z = −1 358 + 128 k`. Ce décalage est choisi pour que l'aire actuelle ([−100, −70] à [140, 100]) tienne **entière** dans les quatre cases `c14_r10`, `c15_r10`, `c14_r11`, `c15_r11` (de [−116, −78] à [140, 178]).
- **Secteur de simulation** : 2 × 2 cases, **256 m**. 16 × 12 = **192 secteurs**, nommés `s<col>_<rang>`. Le cœur est le secteur `s07_05` : l'ancienne aire entière, avec un peu de place autour (78 m de plus au nord).
- **Pourquoi 128 m et 256 m** : 128 m, c'est ce qu'un téléphone affiche en détail autour du joueur (3 × 3 cases = 384 m) et un fichier de données de taille raisonnable ; 256 m, c'est la plus grande aire que la grille de navigation tient à 0,5 m (512 × 512), à la marge près (2.4). Traversée d'un secteur : environ 50 s à pied (5 m/s), 20 s à cheval (13 m/s, 4 km en 5 min ; `Planar.MaxSpeed` est de 20 m/s).

### 2.2 Trois façons de simuler une grande région

| | A. Une seule grande session | B. Une zone par case, portes entre elles | **C. Secteurs fixes avec marge et passage d'autorité** |
|---|---|---|---|
| Principe | `zone.vallons` couvre 4 × 3 km | chaque case est une zone ; on passe de l'une à l'autre comme par une porte | chaque secteur de 256 m est une session de zone ; le joueur change de secteur sans chargement ; une marge de 32 m garde les combats en cours |
| Plafonds à relever | coordonnées, obstacles (milliers), combattants (milliers), grille (case de 16 m), registre | aucun | coordonnées, obstacles (256), grille (marge), recyclage des places de combattants |
| Combat à cheval sur deux cases | naturel | impossible (on change de zone) | réglé par la marge et le territoire (2.5) |
| Coût serveur | toute la région simulée, même vide ; instantané et journal énormes | faible | seulement les secteurs occupés |
| Téléphone en solo (prototypes) | intenable | facile | 1 à 4 secteurs actifs, comme l'aire actuelle |
| Déterminisme | gardé, mais une session ingérable | gardé | gardé : secteurs au même tick, passages entre deux ticks, dans un ordre stable |
| Risque | élevé | coutures visibles toutes les 25 s | moyen : nouveau mécanisme bien borné |

**Recommandation (Claude)** : **C**. A ne tient ni sur téléphone ni sur serveur ; B contredit « le monde chargé autour du joueur » (on sentirait chaque bord). C garde le moteur tel qu'il est (une session = un espace fixe) et ajoute seulement le passage d'un combattant d'une session à l'autre.

### 2.3 Ce que fait chaque côté (Recommandation)

| | Serveur (lot 13) ou hôte local (solo, prototypes) | Client (Unity) |
|---|---|---|
| Simulation | Un secteur est **actif** s'il contient un joueur ou si un joueur est à moins de 96 m de son bord (au plus 4 secteurs par joueur). Tous les secteurs actifs avancent **au même tick du monde**, dans l'ordre de leur nom. Un secteur sans joueur depuis 60 s se **ferme** (sauvegarde de son registre et du temps, comme une fin de session aujourd'hui) | Ne simule que la prédiction de son propre déplacement (architecture §5) |
| Ce qui est envoyé | État filtré (option B retenue le 20 octobre 2026) des entités à moins de 96 m, **tous secteurs confondus** | Affiche, lisse, prédit |
| Monde vivant hors des secteurs actifs | Calculé, pas simulé : fonctions pures du temps du monde et du registre (section 5) | Affiche ce qu'on lui dit (caravane au loin, fumée du fort) |
| Cases à afficher | Ne s'en occupe pas | Charge et décharge les cases autour de la caméra (section 6) |

En solo (aujourd'hui), l'`AdventureSession` joue le rôle de l'hôte : même règle d'activation, mêmes passages ; rien ne dépend de Unity.

### 2.4 Secteurs, marge et passage d'un secteur à l'autre (Rupture, à valider)

- **Espace d'un secteur** : ses 256 m plus une **marge de 32 m** de chaque côté, soit 320 × 320 m. Les obstacles de la marge sont ceux des cases voisines (lus des mêmes données) ; le joueur y voit et s'y cogne comme ailleurs.
- **Propriété** : chaque entité appartient à **un seul** secteur à la fois. Les populations d'un secteur partent de son intérieur (au moins 8 m du bord, règle du validateur).
- **Passage** : un joueur (ou sa monture, son compagnon, un PNJ escorté, une caravane) qui a dépassé le bord de son secteur de **8 m** (hystérésis : pas d'aller-retour) et **n'est dans aucune rencontre** passe au voisin : départ en fin du tick `t` dans l'un, arrivée au tick `t + 1` dans l'autre, **avec tout son état de combat** (PV, ressources, recharges, altérations, orientation). Plusieurs passages au même tick : ordre stable (secteur de départ, puis identité).
- **En combat** : il reste dans son secteur, jusqu'au bout de la marge. S'il atteint le bout de la marge (32 m au-delà du bord) encore engagé, les créatures de la rencontre **lâchent** (règle de la laisse existante : ni mort, ni butin), puis il passe. Avec le territoire (2.5), cela n'arrive presque jamais.
- **Ce que cela demande au moteur** :
  1. **Arrivée avec état** : aujourd'hui une arrivée part d'un personnage plein. Il faut une arrivée qui reprend l'état exporté d'un combattant (une ligne de journal nouvelle : **format de journal 25**, moteur 0.31.0). Les journaux figés ne changent pas ; on en fige de nouveaux.
  2. **Coordonnées ±4 096 m** (`Planar.MaxCoordinate`, vecteurs ±8 192 m). Les produits restent sous 2⁶³ (différence maximale 8,2·10⁷ unités, carré 6,7·10¹⁵) ; à vérifier dans tout le précontrôle numérique. *Repli* : un repère local par secteur (décalage entier), sans toucher à la borne, au prix d'une conversion à chaque passage.
  3. **256 obstacles par espace** (au lieu de 64), avec un rangement par cases dans le moteur pour que le coût par pas ne grandisse pas ; borne du schéma de scénario relevée en conséquence (version de schéma à monter si elle y figure).
  4. **Grille de navigation** : `NavGrid.MaxCells` à 524 288 (320 m à 0,5 m = 409 600 cases).
  5. **Recyclage des places** : un combattant parti (mort, laisse, passage) libère sa place dans le plafond de 128, **sans que son identité soit jamais réutilisée** (architecture §4). Sans cela, un secteur ouvert des heures s'épuise (98 réapparitions). Changement de règle du moteur : versionné, testé, nouveau journal figé.
- **Clés du registre** : le cœur garde ses clés (`zone.vallons/<session>/death/<id>`, passages, points sûrs) ; les autres secteurs ajoutent leur nom (`zone.vallons/s08_05/<session>/death/<id>`). Les clés de mort d'une session fermée ne servent plus (elles protègent contre un rejeu **dans** la session) : elles sont **purgées à la fermeture**, ce qui règle le plafond de 100 000 (sauvegarde v18).

### 2.5 Créatures qui suivent le joueur (Recommandation)

- **Territoire** (nouveau, données de population) : rayon autour du point de départ, **40 m au plus**. Une créature engagée qui en sort lâche, comme pour la laisse. Départ à 8 m du bord au moins, territoire de 40 m : une créature ne dépasse jamais la marge de 32 m. La laisse actuelle (écart à la cible) reste.
- **Exceptions voulues** : boss du monde, chefs, poursuites scénarisées (meute qui traque, bandits qui chassent une caravane) sont placés au milieu de leur secteur, avec un territoire plus grand fixé à la main et vérifié par le validateur (le territoire tient dans la marge).
- **Ce qui suit vraiment le joueur** passe avec lui : sa monture, son compagnon, Pain-d'Épice, un PNJ escorté.

### 2.6 Combien ça coûte et ce qui ne casse pas

- **Journaux de référence** : intacts. La session du cœur se joue comme avant tant qu'on ne sort pas de son secteur. Le premier sous-lot (V2.25) ne change aucun comportement : il découpe les données et relève des bornes seulement.
- **Déterminisme** : chaque secteur a son `Pcg32` tiré de l'empreinte de son nom et de son numéro de session (comme les événements du monde) ; secteurs au même tick ; passages entre deux ticks, dans un ordre stable ; rien ne dépend de l'ordre d'arrivée des messages ni du nombre de secteurs actifs **pour un même secteur**.
- **Coût serveur** : à mesurer au V2.26 (`eds mesures secteurs`). Cible : un secteur de 60 combattants à moins de 1 ms par tick ; 20 à 40 secteurs actifs sur la région pour quelques centaines de joueurs.

## 3. Les trois étages (Recommandation, sur la base du Retenu)

| Étage | Comment c'est fait | Entrée | Données |
|---|---|---|---|
| **Surface** | Les 192 secteurs (section 2) | — | région `zone.vallons`, une donnée par case |
| **Dessous** | **Des zones à part, reliées par des portes** (mécanisme des intérieurs : chargement court, sauvegarde à la porte d'en face). Un dessous plus grand que 256 m (la mine) est lui-même découpé en secteurs. Un niveau de mine = une zone (étages reliés par des escaliers ou des puits, qui sont des portes) | Grotte derrière la cascade, bouche de la mine, escalier de la crypte, bouches d'égout, failles des pierres du Seuil | zones `zone.vallons.dessous.<nom>` (`indoor: false`, avec créatures) |
| **Envers** | **Une deuxième couche de la région**, même grille et mêmes coordonnées. Chaque case de l'Envers part de sa case de surface (mêmes obstacles par défaut), avec ses retouches (ruines effondrées, arbres morts, passages qui n'existent que là), ses populations, sa lumière | **Failles** : portes entre la surface et l'Envers **au même point** (fondu d'une à deux secondes) | `zone.vallons.envers`, une donnée par case |

- Rappel : la règle du moteur reste **un seul niveau par espace** (L11-D6). Les étages sont des espaces différents ; on change d'étage par un passage. Le vrai relief et les étages superposés dans un même espace restent pour plus tard.
- **Voyage** : le voyage rapide reste entre points sûrs découverts **de la surface** (Retenu, sujet 7). Points sûrs aussi au dessous (camp de mineurs) et dans l'Envers (autel de l'Aube), découvrables, mais le voyage rapide n'y mène pas (Recommandation).
- **Plafonds** : 16 portes par zone et 64 zones reliées ne suffiront pas (intérieurs du Bourg, dessous, failles) : à relever au V2.45.
- **Reprise d'une partie** : aujourd'hui une partie reprend à l'entrée de la zone sauvegardée ; sur une grande carte, elle reprendra au **dernier point sûr** (sauvegarde : secteur et point sûr).

## 4. Le plan de la région

### 4.1 Carte schématique

Un caractère = 100 m d'est en ouest, une ligne = 200 m du nord au sud. Repère : x de −1 900 (gauche) à +2 100 (droite), le cœur vers x = 0.

```
     z      x: -1900            -100 0  +300           +1300        +2100
 +1700 ......................^^^^^^^^X^^^^......
 +1500 FFFFFFFFFFFFFFFFFFF...^^^^^^M:^^^^^......
 +1300 FFFFFFFFFFFFFFFFFFF...^^^^:::^^^^^^......
 +1100 FFFFFFFFFFFFFFFFFFF.W.^^:::^^^^^%%%%%%%%%
  +900 FFFFFFFFFFTFFFFFFFF.~.:::^^^^^^^%%%%%%%%%
  +700 P:FFFFFFFFFFFFFFFFF.~::::^^^^^^^%%%%O%%%%
  +500 F:::FFFFFFFFFFFFFFF.~:..:+...::::%%%%%%%%
  +300 FFF:::::FFFFFFFFFFF.~:..::::::..%%%%%%%%%
  +100 FFFFFFF::::::#####@@@mmmmmm.....%%%%%%%%%
  -100 ............,#####@@@mmmmmm,.............
  -300 ....VVVVVVVVV######~=mmmmmm:::::.........
  -500 ....VVVVVVVVV:,,=========,,,...:B::::::::
  -700 ....VVVVCVVVV,,,====I====,,,.............
  -900 ....VVVVVVVVV,,,=========,,,.............
 -1100 ....VVVVVVVVV,,,=========,,,.............
 -1300 ............,,,,,,,,,,,,,,,,.............
```

`@` cœur (aire actuelle) · `#` Bourg d'Aube fortifié · `~` rivière · `W` cascade · `F` forêt des Loups · `T` arbre millénaire · `^` hautes collines · `M` sommet · `X` col vers les Crêtes de Fer · `+` abbaye · `%` terres corrompues · `O` pierres du Seuil · `m` carrière et mine · `B` fort des bandits · `=` lac · `,` marais · `I` île d'hiver · `V` coteaux de vignes · `C` château viticole · `P` pont vers la Forêt des Murmures · `:` routes (la route d'Havre d'Ambre sort à l'est à z ≈ −600) · `.` prés, champs, landes.

### 4.2 Les douze lieux en coordonnées (Recommandation ; noms de travail)

Rectangles approximatifs, en mètres ; altitudes **d'affichage** (le moteur reste plat).

| # | Lieu | Emprise | Repère | Altitude | Contenu proposé |
|---|---|---|---|---|---|
| 1 | **Cœur** (village actuel) | [−100, −70] à [140, 100] | place du village (−18, −12) | 20 à 30 m | Inchangé : village, moulin, ruines, carrière haute, petit Seuil, ancien bourg, champs, lac des Aubes, tour de guet, camp des rôdeurs |
| 2 | **Bourg d'Aube, ville fortifiée** | remparts de [−640, −240] à [−140, 200] | porte de l'Aube (−140, 0) | 25 m | Remparts, quatre portes, château du bailli, marché couvert, quartiers (artisans, temple, port fluvial), égouts. L'ancien bourg (x de −100 à −24) devient le **faubourg de l'Est**, devant la porte de l'Aube : rien n'y bouge |
| 3 | **Vallée de la rivière et cascade** | [−60, 100] à [260, 1 250] | cascade (120, 1 180), 30 m de chute | 30 à 110 m | Gorges, moulins à foulon, gué, pont suspendu, pêcheurs ; **grotte derrière la cascade** (entrée du dessous) |
| 4 | **Coteaux et château viticole** | [−1 500, −1 100] à [−750, −350] | château (−1 080, −700) | 40 à 120 m | Rangs de vignes, pressoir, chais, vendangeurs saisonniers, cave du château (petit dessous) |
| 5 | **Forêt des Loups et arbre millénaire** | [−1 850, 100] à [0, 1 550] | arbre (−950, 950) | 30 à 90 m | Clairières, bûcherons, tanière de la meute, ours (V2.9.2), cercle de druides autour de l'arbre ; la pointe actuelle ([−100, 41] à [4, 100]) est son orée |
| 6 | **Hautes collines** | [300, 700] à [1 500, 1 714] | sommet (900, 1 450) | 150 à 320 m | Bergeries, troupeaux, cairns sur la crête, **point de vue du sommet** (horizon, section 6.4), col au nord |
| 7 | **Abbaye en ruine et sa crypte** | [520, 420] à [720, 620] | cloître (620, 520) | 90 m | Église éventrée, cloître, jardins redevenus sauvages, Échos de moines ; escalier vers la **crypte** |
| 8 | **Marais du lac et île d'hiver** | marais [−700, −1 358] à [800, −150], lac [−300, −1 050] à [500, −250] | île (120, −700) | 0 à 3 m | Roselières, pontons, cabanes sur pilotis, grottes noyées ; l'**île** n'est atteignable qu'à pied sur la glace, l'hiver |
| 9 | **Carrière devenue mine** | [200, −300] à [650, 60] | bouche de la mine (380, −120) | 30 à 60 m | Carreau, treuils, wagonnets, village de mineurs ; **mine à plusieurs niveaux** au dessous. Le hameau de la carrière actuel reste |
| 10 | **Fort des bandits** | [1 250, −550] à [1 450, −350] | donjon (1 350, −450) | 70 m | Ancien fort de la frontière tenu par les bandits, au-dessus de la route d'Havre ; le camp des rôdeurs du cœur devient leur avant-poste (chapitre 4 inchangé) |
| 11 | **Terres corrompues du Seuil** | [1 250, 150] à [2 188, 1 150] | grand cercle de pierres (1 750, 650) | 60 à 90 m, sol fendu | Herbe décolorée, éclats affleurants, failles fréquentes, **galeries sous les pierres** ; la « Traînée » relie le petit Seuil du cœur aux terres corrompues par les hautes pierres |
| 12 | **Trois passages** | bords de la région | col (1 150, 1 714) ; pont (−1 908, 700) ; route (2 188, −600) | — | Col vers les Crêtes de Fer (fermé par la neige l'hiver), pont des Murmures sur la gorge de l'ouest, route d'Havre d'Ambre ; passages avec court chargement (Retenu, sujet 9). Tant que la région voisine n'existe pas : barrière expliquée (éboulement, pont gardé, péage) |

### 4.3 Routes, rivière, relief

- **Route d'Ambre** (grande route des caravanes) : pont des Murmures → lisière sud de la forêt → porte de l'Ouest du Bourg → grand-rue → porte de l'Aube → faubourg et village (routes actuelles) → hameau et mine → pied du fort des bandits → sortie d'Havre d'Ambre.
- **Route du Nord** : village → vallée de la rivière → embranchement de l'abbaye → lacets des collines → col.
- **Chemins** : des vignes (porte du Sud → château), du lac (village → pontons), des pierres (hautes pierres → terres corrompues), des bergers (crêtes et cairns).
- **Rivière** : de la cascade (120, 1 180) vers le sud, par le cœur (son lit actuel, x de 4 à 7), jusqu'au lac ; elle reste un « obstacle bas » qu'on voit par-dessus (essai du 22 octobre 2026), franchie par des ponts et des gués.
- **Relief d'affichage** : bas au sud (lac à 0 m), le cœur et le Bourg sur un plateau doux (20 à 30 m), les coteaux qui montent vers le sud-ouest, les collines au nord-est jusqu'à 320 m, les terres corrompues sur un plateau fendu à l'est. Les falaises et ravins infranchissables sont des **obstacles** pour le moteur.

### 4.4 Ne rien casser au cœur (Recommandation)

- Mêmes coordonnées, mêmes identifiants de lieux, de points sûrs, de portes, de populations ; les grands lieux ont des **identifiants nouveaux** (`foret_des_loups_profonde`, `ville_du_bourg`…) ; `PlacesAt` en rend déjà plusieurs.
- **Le chapitre 1 garde sa porte** : aujourd'hui le pré et la forêt ne s'atteignent que par la porte du moulin. La grande carte garde ce verrou par le décor : un **ravin des Ronces** borde la forêt au sud, de l'ouest de la région jusqu'à la rivière, et la vallée de la rivière est fermée par un éboulement au nord du cœur, jusqu'à la fin du chapitre 1 (la quête l'ouvre, comme la porte du moulin).
- Les bords actuels du cœur (rivière jusqu'au bord nord, haie jusqu'au bord ouest) deviennent des passages tenus par l'histoire, pas des murs.

### 4.5 Densité de contenu (Recommandation)

- **Règle** : quelque chose tous les 100 à 200 m, soit **au moins un point d'intérêt par case de 128 m** de terre ferme (environ 650 cases ; le lac et les falaises ne comptent pas). Vérifié par un outil (`eds carte densite`) qui liste les cases vides.
- **Trois tailles** : 12 grands lieux ; environ **40 lieux moyens** (hameau, ruine, grotte, camp, moulin, tour, ferme, chapelle de carrefour) ; environ **600 petits points** pris dans une trentaine de **gabarits** (coffre caché, feu de camp, autel, tombe, pierre gravée, point de vue, nœud de récolte, cabane, puits, épave, nid, PNJ de passage, petit défi, Écho, panneau, carte au trésor…).
- **Production** : les gabarits sont des données réutilisables ; un sous-lot de lieu pose ses points à la main dans ses cases et passe la vérification de densité.

## 5. Le monde vivant (Recommandation, sur la base du Retenu)

Principe déjà en place (V2.3, V2.5, V2.6) : **ce qui n'est pas vu est calculé, pas simulé**. Une règle pure tire l'état du temps du monde et d'un petit état gardé ; seuls les secteurs actifs font tourner le moteur.

| Système | Hors des secteurs actifs | Dans un secteur actif | État gardé | Coût |
|---|---|---|---|---|
| Journées des villageois (V2.3) | Rien : la place se calcule de l'heure | PNJ posés à leur place du moment | aucun | négligeable |
| **Caravanes** | Position = fonction du temps sur le graphe des routes (départ, vitesse, haltes) ; une attaque de bandits est un **événement du calendrier** sur un tronçon (tirage `Pcg32` comme V2.5). Sans joueur sur place, l'issue est tirée (pillée ou passée) | La caravane et ses gardes deviennent des combattants ; les joueurs peuvent la défendre ; elle passe d'un secteur à l'autre (2.4) | issue de chaque passage de caravane (registre) | quelques entiers par caravane |
| Conséquences des caravanes | Une caravane pillée change les prix du marché (V2.10) et donne une quête de reprise au fort | — | — | — |
| **Corruption du Seuil** | Un **palier de corruption par case** (0 à 4) qui avance depuis les terres corrompues tous les N jours du monde **si personne ne la repousse** ; elle progresse de case voisine en case voisine (les cicatrices de V2.6 généralisées) | Couleurs fanées, échos rôdeurs, failles plus fréquentes, PNJ qui fuient ; refermer les failles et purifier les autels fait **reculer** le front | 768 paliers et leur date (moins d'un kilo-octet) | négligeable |
| **Lieux reconstruits** | Un lieu a un état (ruine, chantier, reconstruit) ; une reconstruction terminée **reste** (H12 : reconstruction collective) | Le décor et les PNJ suivent l'état | état par lieu | négligeable |
| **Saisons** | Saison = fonction du temps du monde | Lac gelé (glace praticable, l'île s'ouvre), vendanges (PNJ et récoltes des coteaux), neige au col (le passage se ferme), feuillage, herbes et créatures de saison | aucun | négligeable |
| Populations des secteurs fermés | Rien ne vit : un secteur rouvert repart d'une session neuve (comme aujourd'hui) ; les morts récentes se rappellent par le registre | Réapparitions, laisse, territoire | registre du secteur | — |

- **Où vit l'état** : en solo (aujourd'hui), dans la sauvegarde du personnage, comme les cicatrices (V2.6). Dans le monde partagé (lot 13), dans un **état de région** sur le serveur, commun à tous ; la corruption et les reconstructions deviennent alors des chantiers collectifs (H2, chapitre public d'une région).
- **Ce qui n'est pas simulé** : les trajets de chaque villageois hors de vue, des bêtes qui se battent dans un secteur fermé, une économie complète de la région. Volontairement.

## 6. Le rendu (Recommandation)

### 6.1 Chargement par cases dans Unity

- **Anneau 0** (3 × 3 cases autour du joueur, 384 m) : tout le détail : modèles complets, PNJ, animaux, herbe à moins de 40 m, ombres.
- **Anneau 1** (jusqu'à 7 × 7 cases, environ 900 m) : modèles simplifiés, arbres en **imposteurs** (images d'arbre vues sous plusieurs angles), pas d'herbe, pas de PNJ.
- **Au-delà** : un **terrain basse résolution de toute la région** (maille de 32 m, environ 25 000 triangles), les forêts en nappes de feuillage, et les **silhouettes des grands lieux** (remparts, abbaye, château, donjon du fort, arbre millénaire, cascade, pierres du Seuil) en imposteurs dédiés : on voit toujours où l'on va.
- **Construction étalée** : le décor est déjà fait par le code (`SpaceView`, `StyleWorld`). On garde cette façon, case par case, **étalée sur plusieurs images** (au plus 2 ms par image), avec des objets recyclés ; une case quittée est démontée après un délai (pas d'à-coup en faisant demi-tour). Cela règle aussi l'à-coup en sortant d'une maison. Les Addressables de Unity seulement si la mémoire l'exige (gratuits, mais un nouveau paquet).
- **Relief** : la hauteur d'affichage de V2.24 devient une carte de hauteurs par case (129 × 129 sur PC, 65 × 65 sur téléphone), raccordée aux bords. V2.27 attend la fusion de V2.24.

### 6.2 Budget (cibles à mesurer)

| | Téléphone (Note8) | PC modeste (4 Go, GTX 470) | PC complet |
|---|---|---|---|
| Images par seconde | 30 | 30 à 60 | 60 |
| Distance de détail (anneau 0) | 1 case autour (384 m) | 1 case | 2 cases |
| Distance de vue (brume) | 600 m | 900 m | toute la région |
| Appels de dessin | ≤ 150 | ≤ 300 | ≤ 1 500 |
| Triangles visibles | ≤ 300 000 | ≤ 600 000 | ≤ 3 millions |
| Mémoire des textures | ≤ 400 Mo | ≤ 700 Mo (1,2 Go de mémoire vidéo) | libre |

- **Modèles demandés** : LOD0 / LOD1 / LOD2 et un imposteur pour les grands éléments ; textures 1024 au plus (512 pour le téléphone) ; atlas par lieu.
- La règle `MachineTier` (V2.12.4) choisit déjà entre téléphone, PC modeste et PC complet : elle choisit aussi les anneaux.

### 6.3 Horizon lointain

Trois **cartes peintes** sur le ciel, qui ne se chargent jamais comme des cases : les **Crêtes de Fer** au nord (pics enneigés, fumées des forges), la **brume de la Forêt des Murmures** à l'ouest (cimes géantes dans le brouillard), et la **lune fendue** la nuit (déjà dans le ciel, sujet 32). Le **point de vue du sommet** dissipe le brouillard de toute la région (sujet 30) et fait un tour de caméra.

## 7. Sous-lots

Numérotés à la suite de V2.24 (relief, en cours). Tailles : S (quelques jours d'agent), M (une semaine), L (deux semaines ou plus). Les modèles 3D sont **à demander à ChatGPT** (format FBX ou GLB, 1 unité = 1 m, pivot au sol, LOD0 / LOD1 / LOD2, textures 1024 au plus), sauf ce que l'atelier Blender de Claude fait déjà.

### Étape 1 : cases et surface

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.25** Cases et repère | Format de région (zones en schéma 13 : grille, cases, secteurs), découpage du cœur sans rien changer au jeu, outil `eds carte` (cases, densité, image de contrôle), relèvement des bornes de données (lieux, portes, routes par case) | M | — | aucun |
| **V2.26** Secteurs de simulation | **Rupture à valider** : arrivée avec état (journal 25, moteur 0.31.0), coordonnées ±4 096 m, 256 obstacles, grille de navigation, recyclage des places, territoire des populations, passage de secteur, activation et fermeture, purge du registre (sauvegarde v18), mesures | L | V2.25 | aucun |
| **V2.27** Chargement par cases dans Unity | Anneaux, construction étalée, recyclage, terrain basse résolution de la région, réglages selon `MachineTier`, mesures sur Note8 et PC modeste | L | V2.25, V2.24 fusionné | arbres de la région en 3 LOD + imposteurs (chêne, hêtre, pin, bouleau, saule, vigne) ; rochers (5 tailles) |
| **V2.28** Surface de 4 × 3 km | Toute la région praticable : relief d'affichage, rivière jusqu'à la cascade, lac et marais, nappes de forêt, collines, routes, ravins et falaises en obstacles, points sûrs aux carrefours, cheval à 13 m/s ; lieux en emplacements vides | M | V2.26, V2.27 | pont de pierre long, ponton, gué, bornes et panneaux, murets, clôtures, haies |
| **V2.29** Horizon | Cartes peintes du ciel, silhouettes lointaines, point de vue du sommet | S | V2.28 | silhouettes basses des Crêtes de Fer ; imposteurs des grands lieux (au fil des lieux) |

### Étape 2 : les lieux un par un

| Sous-lot | Lieu | Taille | Modèles à demander |
|---|---|---|---|
| **V2.30** | Bourg d'Aube fortifié (surface ; les intérieurs viennent ensuite, un à un) | L | remparts (pans droits, angles, tours rondes et carrées), quatre portes fortifiées, château du bailli, marché couvert, temple, quai, maisons de ville (3 gabarits × 3 tailles) |
| **V2.31** | Vallée de la rivière et cascade | M | cascade de 30 m (rochers, bassin), moulin à foulon, pont suspendu, cabane de pêcheur |
| **V2.32** | Forêt des Loups et arbre millénaire | M | arbre millénaire (avec imposteur), tanière, cabane de bûcheron, cercle de druides, souches et troncs couchés |
| **V2.33** | Coteaux et château viticole | M | château viticole, pressoir, chai, rangs de vigne (3 états : vert, mûr, vendangé), cuves, charrette de vendange |
| **V2.34** | Hautes collines et sommet | M | bergerie, enclos de pierre, cairns, moutons et chèvres, tour de signal du sommet |
| **V2.35** | Abbaye en ruine (surface) | M | église éventrée, cloître, dortoir en ruine, jardin envahi, statues brisées |
| **V2.36** | Marais du lac et île | M | cabanes sur pilotis, pontons, barques, roselières, îlot avec sa ruine, lac gelé (glace, neige) |
| **V2.37** | Carrière devenue mine (surface) | S | carreau de mine, treuil, wagonnets et rails, village de mineurs |
| **V2.38** | Fort des bandits | M | fort en ruine réparé de bric et de broc (enceinte, donjon, tours), palissades, tentes, cage |
| **V2.39** | Terres corrompues du Seuil | M | grand cercle de pierres, éclats affleurants, arbres morts, sol fendu, autels de purification |
| **V2.40** | Trois passages | S | col (arche de pierre, refuge), pont des Murmures, poste de péage de la route d'Havre |

### Étape 3 : le monde vivant (placé ici par Claude ; chacun peut avancer dès que son lieu existe)

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.41** | Caravanes et attaques des bandits | M | V2.26, V2.28, V2.38 | chariot bâché, attelage, coffres de marchandises |
| **V2.42** | Corruption qui s'étend et recule | M | V2.39 | variantes corrompues des arbres et rochers, autels purifiés |
| **V2.43** | Lieux reconstruits | M | lieux concernés | états « chantier » et « reconstruit » (échafaudages) des lieux choisis |
| **V2.44** | Saisons (lac gelé, vendanges, neige au col, feuillages) | M | V2.33, V2.36, V2.40 | variantes d'hiver (neige sur les toits, arbres nus), glace |

### Étape 4 : le dessous

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.45** | Étages : portes de dessous, zones non intérieures avec créatures, relèvement des portes et zones reliées, reprise au dernier point sûr | M | V2.26 | aucun |
| **V2.46** | Grotte derrière la cascade | S | V2.31, V2.45 | kit de grotte (parois, stalagmites, rideau d'eau vu de dedans) |
| **V2.47** | Mines de la carrière, plusieurs niveaux (donjon décidé au sujet 27) | L | V2.37, V2.45 | kit de galerie boisée, ascenseur de mine, effondrements, filons |
| **V2.48** | Crypte de l'abbaye (voir question 3) | M | V2.35, V2.45 | kit de crypte (voûtes, sarcophages, ossuaire) |
| **V2.49** | Égouts du Bourg | M | V2.30, V2.45 | kit d'égout (voûtes, canaux, grilles, échelles) |
| **V2.50** | Galeries sous les pierres du Seuil | M | V2.39, V2.45 | kit de galeries cristallines, éclats géants |

### Étape 5 : l'Envers (au lancement, PERIMETRE.md)

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.51** | Couche de l'Envers : cases en reflet, failles qui y mènent au même point, lumière et ciel de l'Envers | M | V2.28, V2.45 | ciel de l'Envers, variantes sombres des matériaux |
| **V2.52** | Contenu de l'Envers des douze lieux, ses ennemis, secrets et passages | L | V2.51 et les lieux | versions de l'Envers des grands lieux (ruines inversées, arbre millénaire mort…) |

Chaque sous-lot suit la méthode de CLAUDE.md (branche, tests touchés verts, PR en brouillon, CI verte, relecture indépendante puis Codex) ; un changement de format est versionné et documenté, avec de nouveaux journaux figés si le moteur change.

## 8. Risques

- **V2.26 est le seul vrai risque technique** : c'est lui qui touche au moteur. Il est isolé, mesuré et fait avant tout contenu.
- **Production** : 12 lieux et environ 650 points d'intérêt, c'est beaucoup de modèles et de textes. Les gabarits et la vérification de densité gardent le rythme ; un lieu peut être livré « habité » (points d'intérêt) avant d'être « beau » (modèles définitifs).
- **PC modeste et téléphone** : la mémoire de textures est le premier mur (essai du 9 novembre 2026). Atlas par lieu, textures de 512 sur téléphone, mesures à chaque lieu.

## 9. Questions pour Damien

1. **Rupture d'architecture (V2.26)** : valides-tu les secteurs de 256 m avec marge de 32 m, le passage d'un secteur à l'autre avec l'état du combat (journal au format 25, moteur 0.31.0), la borne des coordonnées à ±4 096 m et le recyclage des places de combattants ? Sans cet accord, V2.25 (données) et V2.27 (affichage) peuvent avancer, pas V2.26.
2. **Durée d'une saison du monde** : il faut que chacun voie le lac gelé et les vendanges. Recommandation : **une semaine réelle par saison** (une année du monde en quatre semaines).
3. **La crypte** : le sujet 27 avait décidé « crypte sous la chapelle » ; le 11 novembre, c'est « crypte de l'abbaye ». Recommandation : le donjon de la crypte est sous l'**abbaye** ; la chapelle du Bourg garde seulement un caveau (petit secret).
4. **Jusqu'où va la corruption ?** Recommandation : elle peut prendre les campagnes, l'abbaye et le hameau (PNJ qui fuient, boutiques fermées), mais **jamais le village du cœur ni l'intérieur des remparts**, et ne ferme jamais une quête de l'histoire.
