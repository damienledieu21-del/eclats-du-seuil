# Plan de la grande carte des Vallons (4 × 3 km, trois étages)

Plan technique et de contenu, écrit le 11 novembre 2026 après la **décision de Damien du 11 novembre 2026, 1 h 23** (« parfait ») sur la proposition de Claude, complété par ses **réponses du même jour, vers 2 h 20** (section 0 bis). Documentation seulement : aucun code, aucun format ni journal changé par ce document.

Statuts, comme dans [CADRAGE.md](CADRAGE.md) :

| Statut | Sens |
|---|---|
| **Retenu (Damien)** | Décidé ou validé par Damien (ici, sauf mention : le 11 novembre 2026, 1 h 23). |
| **Recommandation (Claude)** | Proposition technique ou de contenu de ce plan ; confirmée ou corrigée avant le sous-lot qui la réalise. |
| **Rupture (à valider)** | Recommandation qui change l'architecture du cœur et serait difficile à défaire : **rien n'est fait sans l'accord de Damien** (CLAUDE.md, « Quand s'arrêter et demander »). |
| **Ouvert** | Vrai arbitrage, posé en section 9. |

## 0. Ce qui est retenu (Damien, 11 novembre 2026)

- **Taille** : Vallons d'Aube agrandis à **4 × 3 km** (12 km²), environ 15 min à pied d'un bout à l'autre, 5 min à cheval ; monde découpé en **cases chargées autour du joueur** ; détail proche, formes simplifiées au loin ; pensé pour le téléphone (Note8) et le PC.
- **Trois étages** : la surface ; le **dessous** (grottes, mines de la carrière, crypte de l'abbaye, égouts du Bourg, galeries sous les pierres du Seuil ; s'y ajoute le donjon des **grottes noyées du lac**, décidé au sujet 27, V2.50.1) ; l'**Envers** (reflet sombre de toute la région, ouvert par les failles, décidé le 29 octobre 2026, sujet 9).
- **Douze grands lieux** : Bourg d'Aube devenu ville fortifiée ; le village actuel au cœur ; la vallée de la rivière jusqu'à une cascade de 30 m avec une grotte derrière ; les coteaux de vignes et un château viticole ; la forêt des Loups et son arbre millénaire ; les hautes collines (bergers, cairns, sommet avec vue sur toute la région) ; l'abbaye en ruine et sa crypte ; les marais du lac avec une île accessible seulement l'hiver sur le lac gelé ; la carrière devenue mine à plusieurs niveaux ; le camp des bandits dans un ancien fort ; les terres corrompues du Seuil ; trois passages vers les régions voisines (col vers les Crêtes de Fer, pont vers la Forêt des Murmures, route d'Havre d'Ambre).
- **Monde vivant** : journées des villageois, caravanes que les bandits peuvent attaquer, corruption du Seuil qui s'étend vraiment si on ne la repousse pas, lieux reconstruits qui le restent, saisons qui changent la carte (lac gelé, vendanges, neige qui bloque le col).
- **Horizon** : depuis le sommet, Crêtes de Fer au loin, brume de la Forêt des Murmures, lune fendue la nuit.
- **Ordre** : découpage en cases et surface d'abord, puis les lieux un par un, puis le dessous, puis l'Envers. Le relief doux d'affichage est en cours (V2.24, branche `v2.24-relief`, hors de ce plan) ; le vrai relief qui compte en combat reste pour plus tard (L11-D6).

## 0 bis. Réponses de Damien du 11 novembre 2026, vers 2 h 20 (Retenu)

1. **Rupture d'architecture du V2.26** (secteurs, passage d'un secteur à l'autre avec l'état du combat, nouveau format de journal, nouvelle version du moteur, coordonnées ±4 096 m) : « Oui, on le fait ».
2. **Saisons** : déjà tranché le 29 octobre 2026 (sujet 12) : **saisons du monde comme dans la vraie vie** (l'hiver du jeu pendant le vrai hiver). Le plan suit cette règle (section 5).
3. **Bourg d'Aube** : les boutiques et leurs quêtes (auberge, Guilde, boulangerie, épicerie, herboristerie, mercerie, chapelle, maisons nommées comme celle de Rosalie) **déménagent dans la ville fortifiée**, 1 km à l'ouest, selon son plan illustré. Le déménagement est un sous-lot à part (V2.30.1), sans casser ni les sauvegardes ni les quêtes.
4. **Crypte-donjon sous l'abbaye** ; la chapelle garde une petite crypte.
5. **Corruption** : **jamais les villes** (ni le village du cœur, ni l'intérieur de la palissade du Bourg) ; elle s'arrête aux portes.

## 0 ter. Décisions de Damien du 16 novembre 2026 (Retenu)

Rapportées par la session principale ; ordre de travail : V2.30.1, puis le sommet, puis la quête du cheval.

1. **Quête du cheval** : une **vraie petite quête** pour obtenir la monture, au lieu de l'avoir directement à la fin de la mission du chapitre 1 (V2.28d (2b)).
2. **Point de vue du sommet** : y monter **lève le brouillard sur toute la région** (et non plus 1 024 m autour, décision du 15 novembre 2026, V2.29 (2)).
3. **Porte nord du Bourg**, reliée au **chemin du pont des Murmures** : prévue avec ce chemin au **V2.40** (elle n'est pas triviale avec le V2.30.1 : le chemin du pont n'est pas tracé et passerait le ravin des Ronces, verrou du chapitre 1).

Puis, vers 10 h (après la PR #190) :

4. **Emplacement du bourg déménagé** (V2.30.1, au nord-ouest de la place du marché) : **gardé** ; on l'ajustera après le test dans Unity.
5. **Panneaux aux croisements** entre le village et le Bourg : **dès le V2.30.1** (fait : quatre panneaux, direction et distance du Bourg et du village).
6. **Quête du cheval** : Achille, Brume et les rives du lac **confirmés**. **Plus tard** (noté, pas maintenant) : ramener un vrai cheval à la longe jusqu'à la ferme, avec le modèle 3D, quand ses animations seront prêtes.

Puis, vers 16 h 30 :

7. **Grand Solitaire et Bourg déménagé** : le boss du monde attaque **devant la porte de l'Est** et **n'entre pas dans les murs** (choix de Claude au V2.30.1, confirmé).

Puis, vers 23 h :

8. **Peupler les lieux** dès maintenant, avec le **bestiaire existant** (meute et tanière dans la forêt, ours, etc.), pour les lieux des V2.31 et V2.32 comme pour ceux des V2.33 à V2.40.
9. **Pont suspendu** : un **vrai troisième passage** sur la rivière (pas sur le gué nord), relié par des sentiers.

Puis, le 17 novembre 2026 vers 1 h 10 :

10. **Pont suspendu ouvert avec les gués** par la quête des passages de la vallée : **gardé**.
11. **Créatures de nuit** dans les nouveaux lieux, avec le bestiaire de nuit existant (loups de la nuit, échos) : **oui** (vallée et forêt comprises, au V2.33 et suivants).

Puis, le 18 novembre 2026 :

12. **Points sûrs** devant l'entrée de la mine (`bouche_de_la_mine`), devant le fort (`devant_le_fort`) et plus bas sur la route du col des Crêtes de Fer, là où la pente le permet (V2.40).
13. **Passerelle des Ronces** : elle s'ouvre à la **fin du chapitre 1**, avec la quête qui le termine (V2.40).

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
- **Étendue** : x de **−1 908 à +2 188**, z de **−1 614 à +1 458** (4 096 × 3 072 m, soit 12,6 km² : les « 4 × 3 km » arrondis à des puissances de deux).
- **Case** : carré de **128 m**. 32 colonnes × 24 rangées = **768 cases**. Nom : `c<colonne>_r<rangée>` (colonne 0 à l'ouest, rangée 0 au sud). Bords des cases : `x = −1 908 + 128 k`, `z = −1 614 + 128 k`. Ce décalage est choisi pour que l'aire actuelle ([−100, −70] à [140, 100]) tienne **entière** dans les quatre cases `c14_r12`, `c15_r12`, `c14_r13`, `c15_r13` (de [−116, −78] à [140, 178]).
- **Secteur de simulation** : 2 × 2 cases, **256 m**. 16 × 12 = **192 secteurs**, nommés `s<col>_<rang>`. Le cœur est le secteur `s07_06` : l'ancienne aire entière, avec un peu de place autour (78 m de plus au nord).
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

### 2.4 Secteurs, marge et passage d'un secteur à l'autre (Rupture, Retenu par Damien le 11 novembre 2026)

- **Espace d'un secteur** : ses 256 m plus une **marge de 32 m** de chaque côté, soit 320 × 320 m. Les obstacles de la marge sont ceux des cases voisines (lus des mêmes données) ; le joueur y voit et s'y cogne comme ailleurs.
- **Propriété** : chaque entité appartient à **un seul** secteur à la fois. Les populations d'un secteur partent de son intérieur (au moins 8 m du bord, règle du validateur).
- **Passage** : un joueur (ou sa monture, son compagnon, un PNJ escorté, une caravane) qui a dépassé le bord de son secteur de **8 m** (hystérésis : pas d'aller-retour) et **n'est dans aucune rencontre** passe au voisin : départ en fin du tick `t` dans l'un, arrivée au tick `t + 1` dans l'autre, **avec tout son état de combat** (PV, ressources, recharges, altérations, orientation). Plusieurs passages au même tick : ordre stable (secteur de départ, puis identité).
- **En combat** : il reste dans son secteur, jusqu'au bout de la marge. S'il atteint le bout de la marge (32 m au-delà du bord) encore engagé, les créatures de la rencontre **lâchent** (règle de la laisse existante : ni mort, ni butin), puis il passe. Sous-lot V2.28b (4) : une créature de la rencontre arrivée elle-même au bout de la marge lâche aussi, même si le joueur n'y est pas (des créatures plus rapides ne le retiennent plus en se mettant entre lui et le bout) : on finit toujours par lâcher au bout de la marge. Avec le territoire (2.5), cela n'arrive presque jamais.
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

- **Journaux de référence** : intacts. La session du cœur se joue comme avant tant qu'on ne sort pas de son secteur. Le premier sous-lot (V2.25.3, appelé V2.25 dans la première version du plan) ne change aucun comportement : il découpe les données et relève des bornes seulement.
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
- **Plafonds** : 16 portes par zone et 64 zones reliées ne suffiront pas (intérieurs du Bourg, dessous, failles) : à relever au V2.45. Relevés au V2.45.1 (schéma de zone 15) : 64 portes par zone sans région, 512 (et 32 par case) avec région, 256 zones reliées ([DONNEES.md](DONNEES.md#étages-dessous-et-genres-de-porte-chantier-v245)).
- **Dessous (V2.45)** — **Retenu (Damien, 21 novembre 2026, rapporté par la session principale : les 4 recommandations des PR #238 et #239)** : (1) les dessous gardent des identifiants `zone.<nom>`, l'étage est dit par le champ `floor` (pas `zone.vallons.dessous.<nom>` ; le format des sauvegardes ne change pas) ; (2) les créatures d'un dessous partent à **8 m au moins** de chaque porte ; (3) le voyage rapide est **permis depuis** un dessous (il n'y mène jamais) ; (4) **jamais de cheval** au dessous. Les autres choix proposés (identifiants à points, aucune règle ou 16 m, voyage refusé depuis un dessous, cheval dans les grands dessous) sont écartés.
- **Reprise d'une partie** — **Retenu (Damien, 13 novembre 2026, 15 h 00 : « La 1 »)** : une partie relancée reprend au **dernier point sûr dont le personnage s'est approché à moins de 5 m, même sans y avoir fait de halte** (sauvegarde : secteur et point sûr). Avant le V2.28, elle reprenait à l'entrée de la zone sauvegardée. L'autre choix proposé (seulement le dernier point sûr où l'on a fait une halte) est écarté. Réalisé au V2.28a (sauvegarde v19, [DONNEES.md](DONNEES.md#point-de-reprise-sous-lot-v228a)).
- **Chef qui poursuit le joueur hors de son secteur** — **Retenu (Damien, 14 novembre 2026, 11 h 20 : « AAA », rapporté par la session principale)** : il lâche au bout de la marge de 32 m, revient à sa place et **reprend tous ses PV** (même forme). L'autre choix (le chef suit le joueur dans l'autre secteur) est écarté. Réalisé au V2.28b (2c) ([DONNEES.md](DONNEES.md#chefs-liés-à-leur-secteur-sous-lot-v228b-2c-première-partie)).
- **Renouvellement d'une session pendant un combat de chef** — **Retenu (Damien, 14 novembre 2026, 11 h 20 : « AAA », rapporté par la session principale)** : le combat en cours **continue tel quel** (forme et PV emportés, comme ceux du joueur) ; le chef ne recommence à sa première forme qu'à une vraie réouverture du secteur. Réalisé au V2.28b (2c).
- **Événements du monde et secteurs** — **Retenu (Damien, 14 novembre 2026, 11 h 20 : « AAA », rapporté par la session principale)** : chaque événement est lié au **secteur de son lieu**, et le validateur vérifie que ses groupes y tiennent, comme pour les chefs. Réalisé dans la suite du V2.28b (2c) ([DONNEES.md](DONNEES.md#événements-du-monde-et-cicatrices-liés-à-leur-secteur-sous-lot-v228b-2c-suite)).
- **Créatures d'événement au renouvellement d'une session** — **Retenu (Damien, 14 novembre 2026, 17 h 18 : « AAA », 1re question de la PR #160, rapporté par la session principale)** : elles **continuent telles quelles** dans la session nouvelle, avec leur place et leurs PV. Réalisé au V2.28b (2c), suite.
- **Événement dans un secteur fermé** — **Retenu (Damien, 14 novembre 2026, 17 h 18 : « AAA », 2e question de la PR #160)** : il a lieu au **temps du monde**, avec la même issue que si le joueur était absent ; ses créatures arrivent si le joueur ouvre le secteur pendant qu'il dure. Réalisé au V2.28b (2c), suite.
- **Créature d'événement qui suit le joueur hors de son secteur** — **Retenu (Damien, 14 novembre 2026, 17 h 18 : « AAA », 3e question de la PR #160)** : elle le lâche au bout de la marge de 32 m, revient à sa place et **reprend tous ses PV**, comme un chef. Réalisé au V2.28b (2c), suite (essai dédié).
- **Créature qui atteint le bout de la marge avant le joueur** — **Retenu (Damien, 14 novembre 2026, 21 h 03 : « oui », question de la PR #163, rapporté par la session principale)** : une créature qui poursuit le joueur hors de son secteur **lâche dès qu'elle atteint elle-même le bout de la marge**, même si le joueur n'y est pas encore. Réalisé au V2.28b (4).
- **Voyage rapide vers un point sûr bloqué** — **Retenu (Damien, 13 novembre 2026, 19 h 51 : « Non 2 pardon », rapporté par la session principale)** : quand aucune place n'est libre à 8 m de la destination (créatures sur le point sûr), le voyage est **refusé** et le personnage ne bouge pas, avec un message. L'autre choix (arriver à l'entrée de la zone) est écarté. Le couvre-feu, où le joueur ne choisit pas sa destination, garde le repli sur l'entrée de la zone. Réalisé au V2.28b (2) ([DONNEES.md](DONNEES.md#règle-darrivée-à-un-point-sûr-sous-lot-v228b)).
- **Niveaux des lieux de la région** — **Retenu (Damien, 15 novembre 2026, vers 9 h 45, rapporté par la session principale)** : ils restent sous le Gardien du Seuil (23, fin du chapitre 5) ; toute la région est entre 1 et 22, dans un ordre cohérent avec l'existant (camp des rôdeurs 16, Borgne 17). Appliqué au V2.28c (3) (PR #170, liste dans [MONDE.md](MONDE.md)).

## 4. Le plan de la région

### 4.0 La carte illustrée de Damien (Retenu)

Damien a fait dessiner la carte de la région (ChatGPT) et l'a **validée** le 11 novembre 2026 : [été](cartes/carte_ete.jpg) et [hiver](cartes/carte_hiver.jpg) (copies réduites à 1 024 px ; originaux de 1 448 px dans l'AssetLab, `Eclats_du_Seuil_AssetLab/Carte_Vallons/carte_ete.png` et `carte_hiver.png`). **C'est elle qui fixe la disposition** ; ce plan la traduit en coordonnées.

- **Lecture** : la carte est en vue cavalière ; les bâtiments y sont grossis. Conversion retenue par Claude : la place du village (vers le pixel 690, 525 de l'original) tombe sur la place actuelle (−18, −12), puis **2,83 m par pixel** (la région de 4 096 m remplit les 1 448 px ; l'échelle dessinée dit environ 3,3 m par pixel : écart accepté, la carte n'est pas un relevé). Les positions ci-dessous sont donc **approximatives**, à ±100 m ; les tailles des lieux sont celles du jeu, pas celles du dessin.
- **Le Seuil** : la carte met les **terres corrompues au sud-est** ; dans les Vallons actuels, la petite zone du Seuil (pierres dressées, de [40, 24] à [60, 40]) est **au nord-est du village**. Conciliation (Recommandation) : la petite zone actuelle **reste où elle est** (chapitre 5, failles, cicatrices : rien ne bouge) ; c'est une **fêlure avancée**, reliée aux terres corrompues du sud-est par une veine souterraine (les galeries sous les pierres du Seuil passent dessous) ; en surface, la corruption qui avance (section 5) part des terres du sud-est et **remonte vers le village**, en suivant les vergers et la rivière, et nourrit la fêlure. Le joueur voit donc une seule histoire : le grand Seuil au sud-est, une fissure déjà ouverte aux portes du village.
- **Deux autres conciliations** (Recommandation) : la **tour de guet** et le **camp des rôdeurs** du cœur restent ; la tour du dessin, au nord-est, est la **tour des bandits** près de leur fort. La **carrière** actuelle (à l'est du village) est la vieille carrière à ciel ouvert ; la grande carrière et sa **mine** sont 1 km plus au nord-est, avec leur hameau ; le hameau actuel reste.

### 4.0 bis Les Vallons dans Orsédia (Retenu)

Deux cartes du continent, validées par Damien le 11 novembre 2026 : [Orsédia](cartes/carte_orsedia.jpg) et [Orsédia dans l'Envers](cartes/carte_orsedia_envers.jpg) (copies réduites ; originaux dans l'AssetLab, `Carte_Vallons/carte_orsedia.png` et `carte_orsedia_envers.png`). Les Vallons y sont au centre-ouest ; Havre d'Ambre et ses marais à l'ouest (la Côte des Brumes et Port-Brume au-delà, sur la mer) ; la Forêt des Murmures au nord-ouest ; Givre-Lune au nord ; les Crêtes de Fer et Forge-Haute au nord-est ; les Ruines d'Ysoria au centre, **à l'est des Vallons** ; le Plateau des Cendres à l'est ; les Hautes Terres de l'Aube au sud ; les Îles Fendues au sud-est.

Les sorties de la région suivent ce continent (Recommandation) :

| Sortie | Où dans les Vallons | Vers | Quand |
|---|---|---|---|
| Route d'Havre d'Ambre | bord ouest (−1 908, −240) | ouest | V2.40 |
| Pont de pierre sur la gorge | (−1 520, 470), puis le bord ouest **côté nord** (−1 908, 900) | Forêt des Murmures, au nord-ouest | V2.40 |
| Route du col | bord nord-est (2 100, 1 220) | Crêtes de Fer, au nord-est | V2.40 |
| **Route de l'Est** (4e sortie, réservée) | de l'abbaye (1 710, 145) au bord est (2 188, 150), entre la mine et les terres corrompues | Ruines d'Ysoria | **plus tard** : le tracé et une barrière expliquée (pont effondré, éboulement gardé par l'Ordre) sont posés dès V2.40 pour que rien ne soit à déplacer ; l'ouverture vient avec la région d'Ysoria |

L'Envers des Vallons (V2.51) reprend les mêmes sorties, vers l'Envers des régions voisines de la seconde carte.

### 4.1 Carte schématique

Un caractère = 100 m d'est en ouest, une ligne = 200 m du nord au sud. x de −1 900 (gauche) à +2 100 (droite) ; le cœur (`@`) vers x = 0.

```
     z
 +1400 |||||............^^^^M^^^^^^^^^^^^^^^^^^^
 +1200 |||||FFFFFFFFFFFFF^^^^^^^^^^^^^^^^::::::X
 +1000 |||||FFFFFFFFFFFFFb^^^^^~W^^^^:B:::^^^^^^
  +800 |||||FFFFFTFFFFFFF:....~~.....:.....t....
  +600 |||||FFFFFFFFFFFFF:...~~.....::..........
  +400 ||||P:::FFFFFFFFFF::.~~...::mmmmm........
  +200 |||||F#######FFFFF@@@~::::::mmmmm:::+....
    +0 ......#######:::::@@@::..................
  -200 H:::::#######....."~~"":::::o...%%%%%%%%%
  -400 ......#######.....""~"".ooo:::::%%%%%%%%%
  -600 ..VVVVCVVVVV......""~~".ooooo...:::O%%%%%
  -800 ..VVVVVVVVVV..,,===============,,%%%%%%%%
 -1000 ..VVVVVVVVVV..,,===============,,%%%%%%%%
 -1200 ..VVVVVVVVVV..,,=====I=========,,%%%%%%%%
 -1400 ..VVVVVVVVVV..,,===============,,%%%%%%%%
 -1600 .........................................
```

`@` cœur (aire actuelle) · `#` Bourg d'Aube fortifié · `~` rivière · `W` cascade · `F` forêt des Loups · `T` arbre millénaire · `|` gorge de l'ouest · `P` pont vers la Forêt des Murmures · `H` route d'Havre d'Ambre · `V` coteaux de vignes · `C` château viticole · `"` champs, ferme et grange · `o` vergers · `=` lac des Aubes · `,` marais · `I` île d'hiver · `^` hautes collines · `M` sommet · `b` bergerie · `B` fort des bandits · `t` tour des bandits · `X` route du col vers les Crêtes de Fer · `m` grande carrière et mine · `+` abbaye en ruine · `%` terres corrompues du Seuil · `O` pierres levées · `:` routes et chemins · `.` prés et landes.

### 4.2 Les douze lieux en coordonnées (Recommandation ; positions tirées de la carte de Damien)

Rectangles approximatifs, en mètres ; altitudes **d'affichage** (le moteur reste plat).

| # | Lieu | Emprise | Repère | Altitude | Contenu proposé |
|---|---|---|---|---|---|
| 1 | **Cœur** (village, moulin et pont) | [−100, −70] à [140, 100] | place du village (−18, −12) | 30 m | Inchangé, sauf le bourg qui déménage (ligne 2) : village, moulin, ruines, vieille carrière, petite zone du Seuil, champs, étang, tour de guet, camp des rôdeurs |
| 2 | **Bourg d'Aube, ville fortifiée** | palissade ronde, centre (−1 010, −70), environ 600 m de diamètre | porte de l'Est (−710, −50) | 35 m | Palissade de pieux, tours de bois, trois portes (est, ouest vers Havre d'Ambre, nord vers le pont), église, place du marché, maisons serrées, égouts. **Retenu (Damien, 11 novembre 2026)** : les boutiques, l'auberge, la Guilde, la chapelle et les maisons nommées du bourg actuel **y déménagent** (V2.30.1), selon le [plan illustré du Bourg](cartes/plan_bourg_ete.jpg) |
| 3 | **Vallée de la rivière et cascade** | de la cascade (590, 1 080) au cœur (5, 100) | cascade, 30 m de chute | 30 à 140 m | Gorges, gué, pêcheurs ; **grotte derrière la cascade** (entrée du dessous) |
| 4 | **Coteaux et château viticole** | [−1 700, −1 500] à [−780, −500] | château (−1 330, −680) | 40 à 120 m | Rangs de vignes en terrasses, pressoir, chais, vendangeurs, jardins à la française, cave du château |
| 5 | **Forêt des Loups et arbre millénaire** | [−1 500, 280] à [−160, 1 280] | arbre (−850, 780) | 40 à 100 m | Clairières, bûcherons, tanière de la meute, ours, cercle autour de l'arbre ; la pointe actuelle ([−100, 41] à [4, 100]) en est l'orée |
| 6 | **Gorge et pont de pierre** (passage vers la Forêt des Murmures) | gorge [−1 908, 280] à [−1 500, 1 300] | pont (−1 520, 470) | gorge de 60 m | Torrent au fond, pont de pierre à une arche ; la route continue vers l'ouest |
| 7 | **Hautes collines, bergerie, cairns, sommet** | [−200, 900] à [2 188, 1 458] | sommet (180, 1 360) ; bergerie (−130, 1 080) ; cairns (190, 1 220) | 150 à 350 m | Troupeaux, enclos de pierre, cairns sur la crête, **point de vue du sommet** |
| 8 | **Fort des bandits et sa tour** | fort [1 050, 900] à [1 300, 1 090] | fort (1 170, 990) ; tour (1 740, 820) | 160 à 200 m | Fort de rondins et de pierre sur une butte ; la tour surveille la route du col ; le camp des rôdeurs du cœur est leur avant-poste (chapitre 4 inchangé) |
| 9 | **Route du col vers les Crêtes de Fer** | du fort au bord nord-est | sortie (2 100, 1 220) | jusqu'à 350 m | Lacets, refuge ; la neige ferme le col l'hiver |
| 10 | **Grande carrière et mine** | [850, 150] à [1 250, 400] | bouche de la mine (1 185, 355) ; hameau des mineurs (1 000, 145) | 80 à 110 m | Gradins de taille, treuil, wagonnets ; **mine à plusieurs niveaux** au dessous |
| 11 | **Abbaye en ruine et sa crypte** | [1 600, 60] à [1 820, 230] | nef (1 710, 145) | 90 m | Nef sans toit, rosace, cloître, Échos de moines ; escalier vers la **crypte** |
| 12 | **Lac des Aubes, marais et île d'hiver** | lac [−300, −1 360] à [1 150, −850] ; marais tout autour | île (180, −1 230) | 0 à 3 m | Roselières, pontons, entrée des **grottes noyées** (donjon du sujet 27, V2.50.1) ; l'**île** ne s'atteint qu'à pied sur la glace, l'hiver. L'étang actuel du cœur (`lac`) reste : la rivière en sort et descend au grand lac |
| 13 | **Terres corrompues du Seuil** | [1 290, −1 350] à [2 188, −110] | pierres levées (1 550, −620) | 40 à 70 m, sol fendu | Herbe grise, arbres morts, failles violettes, éclats ; **galeries sous les pierres** |
| 14 | **Route d'Havre d'Ambre** | du Bourg au bord ouest | sortie (−1 908, −240) | — | Grande route des caravanes ; poste de péage |

Les « douze grands lieux » de la décision sont les lignes 1 à 5, 7, 8, 10 à 13, plus les trois passages (6, 9, 14). Champs, ferme et grange (vers (60, −450)) et vergers (vers (720, −450)) prolongent le cœur au sud et au sud-est, comme sur la carte. Les passages ont un court chargement (Retenu, sujet 9) ; tant que la région voisine n'existe pas, une barrière expliquée (éboulement, pont gardé, péage).

### 4.2 bis Plans illustrés des lieux (Retenu, validés par Damien le 11 novembre 2026)

| Lieu | Plans (copies réduites ; originaux dans l'AssetLab, `Carte_Vallons/`) |
|---|---|
| Bourg d'Aube fortifié | [été](cartes/plan_bourg_ete.jpg), [hiver](cartes/plan_bourg_hiver.jpg) |
| Abbaye en ruine | [jour](cartes/plan_abbaye_jour.jpg), [nuit](cartes/plan_abbaye_nuit.jpg) |
| Château viticole | [été](cartes/plan_chateau_ete.jpg), [vendanges](cartes/plan_chateau_vendanges.jpg) |
| Marais du lac et île | [été](cartes/plan_marais_ete.jpg), [hiver](cartes/plan_marais_hiver.jpg) |
| Fort des bandits | [jour](cartes/plan_fort_jour.jpg), [nuit](cartes/plan_fort_nuit.jpg) |
| Carrière et mine (coupe sur trois niveaux, rivière souterraine, cristaux du Seuil au fond) | [jour](cartes/plan_mine_jour.jpg), [nuit](cartes/plan_mine_nuit.jpg) |

Les sous-lots de ces lieux (V2.30, V2.33, V2.35, V2.36, V2.37, V2.38, V2.47, V2.50.1) partent de ces plans.

### 4.3 Routes, rivière, relief

- **Route d'Ambre** (caravanes) : sortie d'Havre d'Ambre à l'ouest → porte ouest du Bourg → porte est → route de la ville (l'ancienne grand-rue) → village → vers l'est et la mine → fort des bandits → col des Crêtes de Fer. Les caravanes passent donc **sous le fort** : c'est là que les bandits attaquent.
- **Route de l'Est** (réservée, voir 4.0 bis) : abbaye → bord est, vers les Ruines d'Ysoria.
- **Chemins** : du pont des Murmures (porte nord du Bourg → gorge), des vignes (Bourg → château), de la bergerie (village → collines → sommet), de l'abbaye (mine → abbaye), du lac (village → champs → pontons), des pierres (vergers → terres corrompues).
- **Rivière** : de la cascade (590, 1 080), elle descend vers le sud-ouest jusqu'au cœur (son lit actuel, x de 4 à 7), passe sous le pont du moulin, traverse l'étang puis les champs et se jette dans le grand lac. Elle reste un « obstacle bas » qu'on voit par-dessus (essai du 22 octobre 2026), franchie par des ponts et des gués.
- **Relief d'affichage** : le lac au plus bas (0 m) au sud ; le village et le Bourg sur un plateau doux (30 à 35 m) ; les coteaux qui montent au sud-ouest ; la gorge profonde à l'ouest ; les collines au nord, jusqu'à 350 m ; la mine et l'abbaye sur un épaulement à l'est ; les terres corrompues en cuvette fendue au sud-est. Falaises, gorge et ravins infranchissables sont des **obstacles** pour le moteur.

### 4.4 Ne rien casser au cœur (Recommandation)

- **Exception retenue : le bourg déménage** (V2.30.1, ci-dessous). Pour le reste : mêmes coordonnées, mêmes identifiants de lieux, de points sûrs, de portes, de populations ; les grands lieux ont des **identifiants nouveaux** (`foret_des_loups_profonde`, `ville_du_bourg`, `grand_lac`…) ; `PlacesAt` en rend déjà plusieurs.
- **Le chapitre 1 garde sa porte** : aujourd'hui le pré et la forêt ne s'atteignent que par la porte du moulin. La grande carte garde ce verrou par le décor : un **ravin des Ronces** borde la forêt au sud et à l'est, de la gorge jusqu'à la rivière, et la vallée de la rivière est fermée par un éboulement au nord du cœur, jusqu'à la fin du chapitre 1. **Retenu (Damien, 15 novembre 2026, vers 9 h 45)** : l'éboulement et les gués de la vallée s'ouvrent **par une quête à la fin du chapitre 1**, comme la porte du moulin (quête minimale « Les passages de la vallée », V2.28c (4) : Gaspard la propose une fois le chapitre 1 fini, Aldric au gué ouvre les trois passages).
- Les bords actuels du cœur (rivière jusqu'au bord nord, haie jusqu'au bord ouest) deviennent des passages tenus par l'histoire, pas des murs.

### 4.4 bis Le déménagement du bourg (V2.30.1, Recommandation sur le Retenu)

But : la même partie se reprend après le déménagement, avec les mêmes quêtes au même point, les boutiques simplement 1 km plus à l'ouest.

- **Identifiants inchangés** : zones des intérieurs (`zone.auberge`, `zone.guilde`, `zone.boulangerie`, `zone.epicerie`, `zone.herboristerie`, `zone.mercerie`, `zone.chapelle`, `zone.maison`, archives), portes (`porte_auberge`…), point sûr `place_du_bourg`, lieu `bourg`, PNJ, quêtes, objectifs. **Seules leurs positions changent** dans `vallons.json` (portes, obstacles des maisons, point sûr, rectangle du lieu, étals, places et emplois du temps des PNJ, haltes, nœuds, repères de quête). Les intérieurs eux-mêmes ne changent pas.
- **Sauvegardes** : elles gardent des identifiants (zone, point sûr, registre, quêtes), pas de position dans le bourg ; un point sûr découvert le reste (même clé). Une partie sauvegardée **dans un intérieur** ressort par la même porte, désormais dans la ville. Une partie sauvegardée dans les Vallons reprend au dernier point sûr (section 3) : si c'était la place du bourg, elle reprend sur la nouvelle place. À vérifier au sous-lot : tout champ de sauvegarde qui contiendrait une position (sinon migration versionnée, nouvelle version de sauvegarde, l'ancienne encore lue).
- **Quêtes** : un test rejoue chaque quête qui passe par le bourg (chapitres 1 à 5, Guilde, boutiques, fêtes, grand jour de marché) d'après les nouvelles positions ; les distances de marche des premières quêtes grandissent (environ 1 km, 3 à 4 min à pied) : premier point sûr de la ville découvert tôt, et un chemin balisé.
- **Ce qui reste à l'ancien emplacement** (x de −100 à −24) : un hameau de quelques fermes et la route de la ville ; les obstacles libérés laissent de la place au cœur.
- **Journaux** : contenu de la zone et de son scénario en nouvelle version ; les journaux figés de référence ne changent pas (ils ne viennent pas de cette zone) ; ceux des essais de zone sont figés à nouveau.
- **Dépendances** : V2.26 (la ville est hors du secteur du cœur), V2.28 (surface, et reprise au dernier point sûr, dont dépendent les sauvegardes ci-dessus) et V2.30 (la ville construite).

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
| **Corruption du Seuil** | **Retenu (Damien, 11 novembre 2026)** : jamais les villes, elle s'arrête aux portes (village du cœur, intérieur de la palissade du Bourg). Un **palier de corruption par case** (0 à 4) qui avance depuis les terres corrompues tous les N jours du monde **si personne ne la repousse** ; elle progresse de case voisine en case voisine (les cicatrices de V2.6 généralisées) | Couleurs fanées, échos rôdeurs, failles plus fréquentes, PNJ qui fuient ; refermer les failles et purifier les autels fait **reculer** le front | 768 paliers et leur date (moins d'un kilo-octet) | négligeable |
| **Lieux reconstruits** | Un lieu a un état (ruine, chantier, reconstruit) ; une reconstruction terminée **reste** (H12 : reconstruction collective). **Retenu (Damien, 19 novembre 2026)** : 5 lieux pour commencer (hameau, pont effondré, moulin à foulon, tour de signal, dortoir de l'abbaye), paliers de contributions variées (or, matériaux de métier, combats et exploration autour du lieu), jamais perdue ; un lieu reconstruit apporte décor, PNJ, services (marchand, artisan, point sûr, voyage) et son intérieur | Le décor et les PNJ suivent l'état | état par lieu (sauvegarde en solo ; état de région au lot 13) | négligeable |
| **Saisons** | **Retenu (sujet 12)** : comme dans la vraie vie, l'hiver du jeu pendant le vrai hiver. Le cœur n'a pas d'horloge réelle : la **saison est une donnée d'entrée** fournie par le serveur (ou l'hôte local) d'après la date du jour, au début de chaque session, comme le temps (V2.2) ; elle est écrite dans l'en-tête et la sauvegarde, donc rejouable | Lac gelé (glace praticable, l'île s'ouvre), vendanges (PNJ et récoltes des coteaux), neige au col (le passage se ferme), feuillage, herbes et créatures de saison | aucun | négligeable |
| Populations des secteurs fermés | Rien ne vit : un secteur rouvert repart d'une session neuve (comme aujourd'hui) ; les morts récentes se rappellent par le registre | Réapparitions, laisse, territoire | registre du secteur | — |

- **Où vit l'état** : en solo (aujourd'hui), dans la sauvegarde du personnage, comme les cicatrices (V2.6). Dans le monde partagé (lot 13), dans un **état de région** sur le serveur, commun à tous ; la corruption et les reconstructions deviennent alors des chantiers collectifs (H2, chapitre public d'une région).
- **Ce qui n'est pas simulé** : les trajets de chaque villageois hors de vue, des bêtes qui se battent dans un secteur fermé, une économie complète de la région. Volontairement.

## 6. Le rendu (Recommandation)

### 6.1 Chargement par cases dans Unity

- **Anneau 0** (3 × 3 cases autour du joueur, 384 m) : tout le détail : modèles complets, PNJ, animaux, herbe à moins de 40 m, ombres.
- **Anneau 1** (jusqu'à 7 × 7 cases, environ 900 m) : modèles simplifiés, arbres en **imposteurs** (images d'arbre vues sous plusieurs angles), pas d'herbe, pas de PNJ.
- **Au-delà** : un **terrain basse résolution de toute la région** (maille de 32 m, environ 25 000 triangles), les forêts en nappes de feuillage, et les **silhouettes des grands lieux** (palissade du Bourg, abbaye, château, donjon du fort, arbre millénaire, cascade, pierres du Seuil) en imposteurs dédiés : on voit toujours où l'on va.
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

Trois **cartes peintes** sur le ciel, qui ne se chargent jamais comme des cases : les **Crêtes de Fer** au nord-est (pics enneigés, fumées des forges), la **brume de la Forêt des Murmures** au nord-ouest (§ 4.0 bis) (cimes géantes dans le brouillard), et la **lune fendue** la nuit (déjà dans le ciel, sujet 32). Le **point de vue du sommet** dissipe le brouillard de toute la région (sujet 30) et fait un tour de caméra.

## 7. Sous-lots

Numérotés à la suite de V2.24 (relief, en cours). Tailles : S (quelques jours d'agent), M (une semaine), L (deux semaines ou plus). Les modèles 3D sont **à demander à ChatGPT** (format FBX ou GLB, 1 unité = 1 m, pivot au sol, LOD0 / LOD1 / LOD2, textures 1024 au plus), sauf ce que l'atelier Blender de Claude fait déjà.

### Étape 1 : cases et surface

Numérotation : le premier sous-lot, appelé V2.25 dans la première version du plan, est devenu **V2.25.3** (V2.25, V2.25.1 et V2.25.2 ont été pris entre-temps par les modèles 3D) ; les suivants gardent leur numéro.

**Avancement** : V2.25.3 réalisé (12 novembre 2026, en PR) : zones en schéma 13 (bloc `region`), découpage du cœur sans changement de jeu, bornes relevées et par case, outil `eds carte` ([DONNEES.md](DONNEES.md#cases-et-repère-de-la-région-sous-lot-v2253)). Écarts au plan : les points sûrs (32), populations (256) et zones reliées (64) gardent leurs bornes (relevées avec V2.28 et V2.45) ; `eds carte densite` compte pour l'instant les lieux, points sûrs, portes et populations comme points d'intérêt (les gabarits du § 4.5 viendront avec les lieux).

**Avancement V2.26** (12 novembre 2026, en PR, découpé en PR empilées pour la revue) : **(a) moteur** : arrivée avec état emporté (journal 25, moteur 0.31.0, scénarios en schéma 18), places recyclées d'une zone (identités jamais réutilisées, 1 024 par session), coordonnées ±4 096 m, 256 obstacles rangés par cases, grille de navigation de 524 288 cases ([MOTEUR.md](MOTEUR.md#secteurs-de-simulation--moteur-sous-lot-v226)). Écart au plan : **les altérations ne passent pas** d'un secteur à l'autre (le passage se fait hors rencontre ; une altération reste liée à sa source). **(b) secteurs** : territoire des populations (zones en schéma 14), découpage en secteurs avec marge (`SectorMap`), hôte des secteurs (`SectorWorld` : activation à 96 m, fermeture après 60 s, passage avec l'état au tick suivant, hystérésis de 8 m, rencontre retenue jusqu'au bout de la marge, plusieurs joueurs dans un ordre stable) ([DONNEES.md](DONNEES.md#secteurs-de-simulation-et-territoire-sous-lot-v226-pr-b)). Écart au plan : le jeu solo reste une seule session (celle du secteur du cœur, identique) jusqu'à la surface étendue (V2.28). **(c) registre et mesures** : sauvegarde **v18** (registre purgé des clés de mort à la fermeture de leur session, sessions des secteurs `sectors`) et `eds mesures secteurs` ([DONNEES.md](DONNEES.md#registre-purgé-et-sessions-des-secteurs-sous-lot-v226-pr-c)). **Mesures du 12 novembre 2026** (`eds mesures secteurs --ticks 1200`, machine partagée à 2 cœurs ; revue Codex de la PR #151 : au calme, la cible est à plus de 60 m de tout loup et personne ne s'engage ; mesure refaite à charge moyenne ≈ 4) : secteur de 60 combattants et 195 obstacles ; revue Codex de la PR #151 (10e tour) : les loups « répartis » sont maintenant sur une grille de 8 × 8 places couvrant les deux axes du secteur, et la mesure compte les loups engagés (3 engagés, 56 non engagés ; 59 et 0 tous engagés ; 0 et 59 au calme) ; moyenne par tick **0,21 à 0,29 ms** répartis, **0,64 à 0,68 ms** tous engagés autour d'une cible (contournement du décor, pire cas), **0,04 ms** au calme (deux passages) : **cible de 1 ms atteinte dans les trois cas** (95e centile 0,39 à 0,44, 0,77 à 1,00 et 0,05 à 0,07 ms ; pics isolés de 2 à 14 ms, attribués à la machine partagée) ; 22 à 25 ko alloués par tick par des phases existantes du moteur. Une première mesure, machine chargée (charge ≈ 15) et cible du cas calme trop proche des loups, donnait 0,46, 2,06 et 1,06 ms. **Avant et après V2.26, même scénario** (60 combattants, 60 obstacles, espace de 320 m centré sur l'origine, jouable par les deux moteurs ; 1 000 ticks après 100 de mise en route, deux passes) : moteur 0.30.0 : 22 066 octets par tick répartis, 23 532 à 23 574 tous engagés ; moteur 0.31.0 : 22 066 répartis, 23 576 à 23 624 tous engagés (écart de quelques dizaines d'octets, sous la variation d'une passe à l'autre) : **le V2.26 n'ajoute pas d'allocation par tick** ; temps par tick du même ordre (0,27 à 0,40 ms répartis, 0,96 à 1,39 ms engagés, machine chargée). À reprendre avant le monde partagé : allocations par tick du moteur.

**Avancement V2.27** (13 novembre 2026, en PR) : chargement du **terrain** de la région par cases dans Unity. `StreamingProfile` (réglages selon `MachineTier` : anneau 0 de 1 case autour sur téléphone et PC modeste, 2 sur PC complet, cartes de hauteurs 65 × 65 / 129 × 129 ; anneau 1 jusqu'à 2 cases sur téléphone, 3 sur PC, en 17 × 17 / 33 × 33 ; brume à 600 m, 900 m, toute la région ; 2 ms par image ; budgets de mémoire et de triangles), `CellStreamer` (case d'ancrage avec hystérésis de 16 m, délai de déchargement de 10 s, budget de mémoire et de triangles, un changement à la fois, ordre stable), `TerrainPatch` (construction rangée par rangée, bords raccordés aux mêmes points, jupes, tableaux recyclés), `RegionTerrain` (terrain lointain en maille de 32 m, 12 blocs troués sous les cases chargées ; case et bloc montrés à la même image), `CellStreamView` dans Unity (objets et maillages recyclés, gardé en entrant dans une maison) ; hauteurs : `GroundRelief` de V2.24 ; mesures : `eds mesures cases` ([UNITY.md](UNITY.md#chargement-par-cases-sous-lot-v227)). **Écarts au plan** : seul le terrain est chargé par cases ; le décor du cœur reste construit d'un coup par `SpaceView` (il est toujours dans l'anneau 0), arbres en imposteurs, rochers, herbe et silhouettes des grands lieux attendent les modèles demandés et la surface (V2.28) ; mesures sur le processeur d'un PC de développement seulement (ni Note8 ni PC modeste réels).

**Avancement V2.28** (13 novembre 2026) : le sous-lot est **plus gros que prévu** (le plan le notait M) : rendre toute la région praticable demande de faire passer la partie solo (`WorldSession`, `AdventureSession`, `RealtimeCombat`, l'affichage Unity) d'**une** session de zone à l'hôte des secteurs (`SectorWorld` : plusieurs sessions ouvertes, identité et moteur du joueur qui changent à chaque passage, registre partagé). Il est donc découpé en PR empilées (découpage de Claude, pas une décision de Damien) :

| Partie | Contenu | État |
|---|---|---|
| **V2.28a** Reprise au dernier point sûr | Sauvegarde **v19** : `resume` (dernier point sûr approché et son secteur) ; le jeu relancé reprend à ce point sûr, plus à l'entrée de la zone (retenu par Damien le 13 novembre 2026, § 3) ; point sûr occupé : place libre la plus proche, sinon l'entrée ; v18 lue et mise à jour ([DONNEES.md](DONNEES.md#point-de-reprise-sous-lot-v228a)) | fusionné (PR #155) |
| **V2.28b** Partie solo sur l'hôte des secteurs | Découpé à son tour (ci-dessous) : **(1) l'hôte sous la partie, à parité** ; (2) passage du personnage solo d'un secteur à l'autre ; (3) affichage de plusieurs secteurs dans Unity | (1) en PR |
| **V2.28c** Surface étendue | Aire de la zone portée à toute la région ; rivière jusqu'à la cascade, lac et marais, gorge, ravins et falaises en obstacles ; routes ; points sûrs aux carrefours (borne de 32 à relever) ; lieux en emplacements vides ; relief d'affichage de la région (altitudes du § 4.2) ; journaux des essais de zone figés à nouveau | découpé : **(1)** plafond des obstacles d'une aire relevé à 4 096 (scénarios en schéma 19, chaque secteur simulé au plus 256), fusionné (#165) ; **(2a)** carte et décor d'une fenêtre autour du secteur du personnage pour une aire de plus de 768 m (petite carte, décor, relief de l'aire entière, images bornées), fusionné (#166) ; **(2b)** l'aire portée à toute la région et les grands obstacles (rivières, lac, gorge, ravin, éboulement, falaises ; verrou du chapitre 1), en PR (#167) ; **(3)** routes de la région (route d'Ambre d'un bord à l'autre, six chemins), huit points sûrs aux carrefours (borne de 32 gardée : 14 points sûrs), quinze lieux vides, relief d'affichage de la région (repères du § 4.2), en PR |
| **V2.28d** Décor par cases dans Unity et monture | Rivière, lac, nappes de forêt et routes construits par cases (`CellStreamView`), carte peinte de la région ; cheval à 13 m/s (la monture retenue au sujet 7 n'existe pas encore dans le cœur) | découpé : **(1)** décor de la région par cases (eau, ravins, routes, arbres, rochers ; `CellDecor`), en PR (la carte peinte de la région existe depuis V2.28c (2a)) ; **(2)** monture, découpée : **(2a)** moteur (commandes `mount` et `dismount`, cheval à 13 m/s, hors combat seulement, on descend pour combattre ; moteur 0.34.0, journal 28, scénarios en schéma 22, instantané 9 ; [MOTEUR.md](MOTEUR.md#monture-sous-lot-v228d-2)), en PR ; **(2b)** partie solo : sauvegarde v22 (`character.mount`), cheval donné à la fin de la mission du chapitre 1, monture transmise hors des intérieurs, montée de 1 s par le pont temps réel, touche U dans Unity ([DONNEES.md](DONNEES.md#monture-dans-la-partie-solo-sous-lot-v228d-2b)), en PR ; **(2c)** Unity (modèle du cheval, animations, bouton tactile) et réseau, à faire |

**Avancement V2.28b** (13 novembre 2026 ; découpage de Claude) :

| Partie | Contenu | État |
|---|---|---|
| **V2.28b (1)** L'hôte sous la partie solo, à parité | La session d'une zone à région est tenue par `SectorWorld` (personnage entré avec la session de son secteur, aire du moment, crochets du tick, borne exacte du registre) ; tant que l'aire tient dans le secteur du cœur, même journal octet par octet, mêmes événements, même sauvegarde qu'avant ; **règle d'arrivée à un point sûr** unifiée (jeu relancé, halte, reprise, voyage, couvre-feu : place libre la plus proche, sinon l'entrée) ([DONNEES.md](DONNEES.md#partie-solo-sur-lhôte-des-secteurs-sous-lot-v228b-première-partie)) | en PR |
| **V2.28b (2)** Passage du personnage solo | Découpé : **(2a)** `WorldSession` suit le personnage d'un secteur à l'autre (identité et moteur qui changent, compteurs d'usure et de gestes repris, registre commun aux secteurs ouverts, populations et récompenses de chaque secteur, sessions des secteurs sauvegardées), zones à plusieurs secteurs sans chef ni événement du monde ([DONNEES.md](DONNEES.md#passage-du-personnage-solo-dun-secteur-à-lautre-sous-lot-v228b-2-première-partie)) ; **(2b)** `AdventureSession` et `RealtimeCombat` rebranchés à chaque passage, renouvellement sans coupure d'une session terminée (zones sans chef ni événement du monde ; les Vallons au (2c)) ([DONNEES.md](DONNEES.md#aventure-et-pont-temps-réel-à-chaque-passage--session-renouvelée-sans-coupure-sous-lot-v228b-2b)) ; **(2c)** chefs, événements du monde et cicatrices liés à leur secteur (première partie, chefs : [DONNEES.md](DONNEES.md#chefs-liés-à-leur-secteur-sous-lot-v228b-2c-première-partie)) | (2a) fusionné (PR #157) ; (2b) fusionné (PR #158) ; (2c) chefs fusionné (PR #159) ; (2c) événements et cicatrices en PR |
| **V2.28b (3)** Unity | `SpaceView` et la carte montrent les créatures et le décor de tous les secteurs ouverts ; prédiction du déplacement à travers un passage ([UNITY.md](UNITY.md#plusieurs-secteurs-à-lécran-sous-lot-v228b-3)) | en PR |

Limite à lever avant les lieux (V2.30) : l'aire d'une zone est un seul combat positionné, plafonné à **256 obstacles pour toute la région** (59 pris par le cœur) ; la surface nue tient dessous, le Bourg fortifié non : l'aire devra être assemblée de données par case (§ 3, « une donnée par case »).

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.25.3** Cases et repère | Format de région (zones en schéma 13 : grille, cases, secteurs), découpage du cœur sans rien changer au jeu, outil `eds carte` (cases, densité, image de contrôle), relèvement des bornes de données (lieux, portes, routes par case) | M | — | aucun |
| **V2.26** Secteurs de simulation | **Rupture retenue par Damien** (11 novembre 2026) : arrivée avec état (journal 25, moteur 0.31.0), coordonnées ±4 096 m, 256 obstacles, grille de navigation, recyclage des places, territoire des populations, passage de secteur, activation et fermeture, purge du registre (sauvegarde v18), mesures | L | V2.25.3 | aucun |
| **V2.27** Chargement par cases dans Unity | Anneaux, construction étalée, recyclage, terrain basse résolution de la région, réglages selon `MachineTier`, mesures sur Note8 et PC modeste | L | V2.25.3, V2.24 fusionné | arbres de la région en 3 LOD + imposteurs (chêne, hêtre, pin, bouleau, saule, vigne) ; rochers (5 tailles) |
| **V2.28** Surface de 4 × 3 km | Toute la région praticable : relief d'affichage, rivière jusqu'à la cascade, lac et marais, nappes de forêt, collines, routes, ravins et falaises en obstacles, points sûrs aux carrefours, cheval à 13 m/s ; **reprise d'une partie au dernier point sûr** (sauvegarde : secteur et point sûr ; section 3) ; lieux en emplacements vides | M | V2.26, V2.27 | pont de pierre long, ponton, gué, bornes et panneaux, murets, clôtures, haies |
| **V2.29** Horizon | Cartes peintes du ciel, silhouettes lointaines, point de vue du sommet | S | V2.28 | silhouettes basses des Crêtes de Fer ; imposteurs des grands lieux (au fil des lieux) |

**Avancement V2.29** (15 novembre 2026) : première partie en PR : silhouettes de l'horizon lointain (Crêtes de Fer au nord-est, Forêt des Murmures au nord-ouest, collines ; `HorizonProfile`, `HorizonView`, [UNITY.md](UNITY.md#horizon-lointain-sous-lot-v229-première-partie)). **Deuxième partie** (V2.29 (2), en PR) : brouillard de carte à dissiper (cases vues à 128 m, sauvegarde v23), point de vue du sommet (décision de Damien du 15 novembre 2026 : à la première visite, 200 XP, entrée au carnet, brouillard dissipé à 1 024 m autour), la mer à l'ouest sur l'horizon ([DONNEES.md](DONNEES.md#brouillard-de-carte-et-point-de-vue-du-sommet-sous-lot-v229-2)). À faire : cartes peintes (modèles et textures à demander), tour de caméra du sommet.

**Avancement V2.29.1** (16 novembre 2026, en PR) : décision de Damien du 16 novembre 2026 : monter au point de vue du sommet **lève le brouillard sur toute la région** (avant : 1 024 m autour) ; `Viewpoint.WholeRegion`, `ExploredCells.RevealAll` ; aucun changement de format ([DONNEES.md](DONNEES.md#brouillard-de-carte-et-point-de-vue-du-sommet-sous-lot-v229-2)).

### Étape 2 : les lieux un par un

**Avancement V2.30** (15 novembre 2026, en PR) : le Bourg fortifié en surface (palissade, dix tours, portes est et ouest, poterne sud, grande maison, église, halle, place du marché, écurie, 121 maisons, point sûr de la place ; [MONDE.md](MONDE.md)). Écart au plan : pas de porte nord (le plan illustré n'en a pas ; le chemin du pont des Murmures n'est pas tracé), une poterne sud sur le chemin des vignes à la place.

**Avancement V2.28d (3)** (16 novembre 2026, en PR, empilée sur le V2.30.1) : la quête du cheval (§ 0 ter) : « Le cheval de la ferme », Achille et Brume, rives du lac ; le cheval n'est plus donné par la fin de la mission du chapitre 1 ([DONNEES.md](DONNEES.md#monture-dans-la-partie-solo-sous-lot-v228d-2b)).

**Avancement V2.30.1** (16 novembre 2026, en PR) : le bourg a déménagé **en bloc** dans la ville fortifiée, décalé de (−1 032, −34) m (§ 4.4 bis) : auberge, chapelle, Guilde (`maison6`), maison de Rosalie (`maison5`), boulangerie (`maison2`), épicerie (`maison4`), herboristerie (`maison7`), mercerie (`maison1`), puits et étals de la place ; portes, point sûr `place_du_bourg`, lieux `bourg` et `four_de_firmin`, places de nuit, emplois du temps des PNJ, invités des fêtes et étals du grand jour de marché suivent, **identifiants inchangés** (intérieurs, quêtes, sauvegardes : aucun changement de format, la reprise au point sûr `place_du_bourg` se fait sur la nouvelle place, le secteur sauvegardé n'étant pas comparé). Le bloc est au nord-ouest de la place du marché, entre la route d'Ambre et les îlots du nord (deux maisons de la ville retirées, `maison_ville_74` et `maison_ville_75`), relié à la place du marché par la `rue_du_marche` ; la `rue_des_boutiques` reprend la grand-rue de l'ouest. Il reste à l'ancien emplacement un **hameau** (`maison3`, `maison8`) et la grand-rue jusqu'à la route d'Ambre. Porte nord : non faite, notée pour le V2.40 (§ 0 ter).

**Avancement V2.31 et V2.32** (16 novembre 2026, en PR, une PR pour les deux) : vallée de la rivière et cascade (bassin, rochers, moulin à foulon, cabane du pêcheur, pont suspendu, vrai troisième passage, sentiers et point sûr du pied de la cascade), forêt des Loups (arbre millénaire et cercle de druides, tanière, cabane du bûcheron, sentier et point sûr de l'arbre) ([MONDE.md](MONDE.md)). Créatures : sangliers de la rive est, meute de la tanière, ours. Écarts au plan : souches, troncs couchés, roue du moulin et ponton attendent leurs modèles (pas d'habit existant).

**Avancement V2.33 à V2.39** (16 novembre 2026, en PR, une PR pour les sept, empilée sur celle du V2.31) : coteaux et château viticole, hautes collines, abbaye en ruine, marais du lac et île des saules dormants (verrou d'hiver accroché), carrière devenue mine (surface), fort des bandits, terres corrompues ([MONDE.md](MONDE.md)). Créatures du bestiaire existant (décision du 16 novembre, 23 h) : sangliers, cerfs, loups, échos, bandits. Écarts au plan : ni bergers ni moutons (pas de modèle ni de bête paisible de ce genre) ; pontons, barques, wagonnets, rails, cuves, charrette et états de la vigne (vert, mûr, vendangé : V2.44) attendent leurs modèles ; les murs de l'abbaye prennent l'habit des éboulements.

**Avancement V2.41** (21 novembre 2026, découpage de Claude, PR empilées) : **V2.41.1 cœur**, en PR : caravanes calculées et non simulées (fichier `caravans.json` v1, `CaravanSchedule`), attaque des bandits en événement du monde de sorte « caravan » à la halte sous le fort, issue tirée sans le joueur (gardes), prime à chaque caravane sauvée, contrecoup des marchands si elle est pillée ([DONNEES.md](DONNEES.md#caravanes-et-attaques-des-bandits-sous-lot-v2411)) ; **V2.41.2 Unity**, en PR : chariots, attelage et coffres en formes de remplacement, branchés par leur nom de modèle prévu ; liste des modèles dans l'atelier. **Décisions de Damien du 21 novembre 2026 pour le V2.41** (Retenu : les quatre recommandations de la PR #240, appliquées par défaut, sont validées) : (1) **une caravane par jour, attaquée 35 % des jours** ; (2) **sans le joueur, les gardes tiennent une fois sur deux** ; **si le joueur est présent et perd, la caravane est pillée**, sans tirage des gardes (précision de Damien du 21 novembre 2026) ; (3) **la première fois, récompense entière (1 200 xp, 8 or, 2 potions), puis une prime de 2 or** à chaque caravane sauvée ; (4) **pillage : contrecoup des prix et des stocks pendant 3 jours, plus une quête de reprise au fort** (sous-lot **V2.41.3**, à faire). **Dépense validée le même jour** : modèles Tripo des caravanes (chariot bâché, attelage, coffres de marchandises), 90 crédits au plus. **Écarts au plan** : la caravane et ses gardes ne sont pas des combattants (les bandits ne visent que le joueur) ; la quête de reprise au fort n'est pas faite (V2.41.3). **V2.41.3 reprise au fort**, en PR : après un pillage, trois coffres au fort, gardés par ses bandits, à reprendre en trois jours ; chaque coffre enlève un jour au contrecoup, tous l'annulent et donnent 400 xp et 2 or la première fois, puis 2 or ; un deuxième pillage remet la quête à zéro (caravanes v2, sauvegarde v31 ; [DONNEES.md](DONNEES.md#caravanes-et-attaques-des-bandits-sous-lot-v2411)) ; **décisions de Damien du 21 novembre 2026** (Retenu : les quatre recommandations de la PR #243) : (5) **400 xp et 2 or à la première reprise complète, puis 2 or seuls à chaque reprise suivante** (anti-triche, décision de Damien du même jour, comme une caravane sauvée) ; (6) **un jour de contrecoup en moins par coffre repris, annulé avec les trois** ; (7) **un 2e pillage fait repartir la quête à zéro** ; (8) **le chef des bandits reste à son camp**.

**Avancement V2.42** (21 novembre 2026, découpage de Claude, PR empilées) : **V2.42.1 cœur**, en PR : `corruption.json` v1 (source au cercle des pierres levées, huit terres et leurs autels), extension et paliers recalculés du temps du monde, purification d'un autel (éclat de Seuil, répit de 4 jours, corruption tenue par la source), sauvegarde v32 ([DONNEES.md](DONNEES.md#corruption-qui-sétend-et-recule-sous-lot-v2421)) ; **Décisions de Damien du 21 novembre 2026** : vitesse, purification et recul comme recommandé ; créatures plus fortes de 10 % par palier dans une terre corrompue ; purification de la source par un gros défi qui donne un répit à toute la région, **à faire au V2.42.3** ; les 8 modèles Tripo (240 crédits) sont validés. **V2.42.2 Unity** : décor corrompu en formes de remplacement et teintes, autels, branchés par leur nom de modèle prévu (liste dans l'atelier des modèles, `Corruption/modeles.json`, rien de lancé).

**Avancement V2.45.1** (21 novembre 2026, en PR) : zones en schéma 15 : étage `floor` (« surface », « dessous » : zone à part, avec créatures, jamais un intérieur), genre de porte `kind` (porte, escalier, puits, trappe, faille ; les deux portes qui se répondent sont du même genre), créatures d'un dessous à 8 m au moins de chaque porte, bornes des portes et des zones reliées relevées ([DONNEES.md](DONNEES.md#étages-dessous-et-genres-de-porte-chantier-v245)). Écart au plan : les identifiants restent `zone.<nom>` (le champ `floor` dit l'étage), pour ne pas changer le format des sauvegardes. Aucun dessous livré : ils viennent avec les V2.46 à V2.50.1.

**Avancement V2.45.2** (21 novembre 2026, en PR, empilée sur le V2.45.1) : un dessous dans le monde : créatures, découverte des points sûrs et reprise comme dehors ; le voyage rapide n'y mène pas ; ni cheval, ni neige ni glace pour l'instant (la pluie au V2.46) (à l'abri comme une pièce) ([MONDE.md](MONDE.md#étages--le-dessous-chantier-v245)).

**Avancement V2.46** (21 novembre 2026, en PR) : la grotte derrière la cascade, premier vrai dessous : zone `zone.grotte_cascade` (schéma 15, `floor: "dessous"`, 40 × 36 m, salle du rideau niveau 10, salle du bassin niveau 12), porte au pied de la falaise de la cascade (600, 1 076), deux échos et un ours (bestiaire existant) à 8 m au moins de la porte, gardien (grand ours à deux formes) qui donne l'**anneau de la cascade** à sa première défaite (option retenue par Damien le 21 novembre 2026) ; rendu Unity : roche, pénombre, rideau d'eau vu de dedans, ni pluie, ni neige, ni glace ([MONDE.md](MONDE.md#la-grotte-derrière-la-cascade-chantier-v246)). Kit de grotte : 8 modèles Tripo (240 crédits, validés par Damien le 21 novembre 2026), finis dans Blender et branchés ; formes simples en repli. Écart au plan : le kit passe de 3 à 8 modèles (pilier, bassin, cristaux, éboulis, coffre en plus), à la demande de Damien du 21 novembre 2026.

**Avancement V2.47.1** (22 novembre 2026, en PR) : les mines de la carrière, trois dessous d'après la coupe du [plan de la mine](cartes/plan_mine_jour.jpg) : **galeries boisées** (`zone.mine_galeries`, niveaux 18 et 19, bandits pilleurs, chef des pilleurs), **galeries des filons** (`zone.mine_filons`, niveau 20, camp des mineurs, échos et ours, bête des éboulis), **cœur de cristal** (`zone.mine_cristaux`, niveaux 21 et 22, rivière souterraine passée par une passerelle, échos, gardien des cristaux à trois formes qui donne le **cœur de cristal du Seuil**) ; porte devant les planches de la bouche (1 185, 360), **ascenseur de la mine** (porte de genre puits) entre le premier et le deuxième niveau, **échelles du fond** (escalier) vers le troisième ([MONDE.md](MONDE.md#les-mines-de-la-carrière-chantier-v247)). Aucun format ne change. Choix de Claude, à valider (PR) : l'ascenseur est une porte de genre puits, sans mécanisme de plus ; le cœur de cristal est de niveau 20 (le catalogue s'arrête au niveau 20).

**Avancement V2.40** (17 novembre 2026, en PR, empilée sur celle des V2.33 à V2.39) : porte nord du Bourg et chemin du pont des Murmures (§ 0 ter, décision du 16 novembre), passerelle des Ronces ouverte à la fin du chapitre 1 (décision du 18 novembre), pont des Murmures gardé, col des Crêtes de Fer (arche, refuge, éboulement), péage d'Havre d'Ambre, route de l'Est et pont effondré, créatures de jour et de nuit ([MONDE.md](MONDE.md)). Point sûr des lacets du col (décision du 18 novembre). Écarts au plan : pas de tablier ni de route au-delà de la gorge (avec la Forêt des Murmures : le pont des Murmures est cosmétique pour l'instant) ; les barrières (pont effondré, col, péage) se contournent jusqu'au bord de la région : toute la bordure de chaque passage sera fermée à l'arrivée des régions voisines ; pas de point sûr au refuge même (pente).

| Sous-lot | Lieu | Taille | Modèles à demander |
|---|---|---|---|
| **V2.30** | Bourg d'Aube fortifié, à 1 km à l'ouest du village, d'après son plan illustré (surface) | L | palissade ronde de pieux (pans droits et courbes), tours et chemin de ronde de bois, trois portes fortifiées, église, halle du marché, maisons de ville serrées (3 gabarits × 3 tailles) |
| **V2.30.1** | **Déménagement du bourg** (4.4 bis ; après V2.26, V2.28 et V2.30) : boutiques, auberge, Guilde, chapelle et maisons nommées dans la ville ; positions migrées, identifiants gardés, quêtes et sauvegardes vérifiées | M | façades des boutiques adaptées aux rues de la ville si besoin (les modèles de l'atelier sont réutilisés) |
| **V2.31** | Vallée de la rivière et cascade | M | cascade de 30 m (rochers, bassin), moulin à foulon, pont suspendu, cabane de pêcheur |
| **V2.32** | Forêt des Loups et arbre millénaire | M | arbre millénaire (avec imposteur), tanière, cabane de bûcheron, cercle de druides, souches et troncs couchés |
| **V2.33** | Coteaux et château viticole | M | château viticole, pressoir, chai, rangs de vigne (3 états : vert, mûr, vendangé), cuves, charrette de vendange |
| **V2.34** | Hautes collines et sommet | M | bergerie, enclos de pierre, cairns, moutons et chèvres, tour de signal du sommet |
| **V2.35** | Abbaye en ruine (surface) | M | église éventrée, cloître, dortoir en ruine, jardin envahi, statues brisées |
| **V2.36** | Marais du lac et île, d'après le [plan des marais](cartes/plan_marais_ete.jpg) (et [hiver](cartes/plan_marais_hiver.jpg)) | M | cabanes sur pilotis, pontons, barques, roselières, îlot avec sa ruine, lac gelé (glace, neige) |
| **V2.37** | Carrière devenue mine (surface), d'après le [plan de la mine](cartes/plan_mine_jour.jpg) | S | carreau de mine, treuil, wagonnets et rails, village de mineurs |
| **V2.38** | Fort des bandits, d'après le [plan du fort](cartes/plan_fort_jour.jpg) (et [nuit](cartes/plan_fort_nuit.jpg)) | M | fort de rondins sur soubassement de pierre (enceinte, donjon, tours d'angle), tour de guet isolée, tentes, cage |
| **V2.39** | Terres corrompues du Seuil | M | grand cercle de pierres, éclats affleurants, arbres morts, sol fendu, autels de purification |
| **V2.40** | Trois passages, et le tracé fermé de la route de l'Est vers les Ruines d'Ysoria | S | col (arche de pierre, refuge), pont des Murmures, poste de péage de la route d'Havre, pont effondré de la route de l'Est |

### Étape 3 : le monde vivant (placé ici par Claude ; chacun peut avancer dès que son lieu existe)

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.41** | Caravanes et attaques des bandits | M | V2.26, V2.28, V2.38 | chariot bâché, attelage, coffres de marchandises |
| **V2.42** | Corruption qui s'étend et recule | M | V2.39 | variantes corrompues des arbres et rochers, autels purifiés |
| **V2.43** | Lieux reconstruits (décisions de Damien du 19 novembre 2026, [CADRAGE](CADRAGE.md) § 7 terdecies), en 4 PR : **V2.43.1** cœur (catalogue `rebuilds`, règles, état dans la sauvegarde) ; **V2.43.2** contenu des 5 lieux (paliers, textes, PNJ et services) ; **V2.43.3** Unity (décor selon l'état) ; **V2.43.4** intérieurs des lieux reconstruits et mise à jour de la PR #202 | M | lieux concernés | états « chantier » et « reconstruit » (échafaudages) des lieux choisis ; on réutilise d'abord l'échafaudage du fort et les modèles du lot C et du Bourg |
| **V2.44** | Saisons comme dans la vraie vie (sujet 12) : saison donnée par le serveur d'après la date, lac gelé, vendanges, neige au col, feuillages | M | V2.33, V2.36, V2.40 | variantes d'hiver (neige sur les toits, arbres nus), glace |


**Décisions de Damien du 20 novembre 2026 pour le V2.44** (Retenu, [CADRAGE.md](CADRAGE.md#7-ter-saisons-retenu)) : la neige ralentit le déplacement hors des chemins (aucun effet sur les chemins), hors combat seulement sauf contrainte du moteur, valeur modeste à confirmer ; vendanges = récolte de raisin en septembre, repousse comme les cèpes (événement au château plus tard) ; créatures de saison avec le bestiaire existant (ours qui hibernent, plus de loups l'hiver) ; toits enneigés par shader sur tous les bâtiments, sans crédit. Découpage en PR empilées : **V2.44.1** le serveur fait foi (protocole 6, message `season`), **V2.44.2** cœur (saisons en schéma 2 : vigne, neige au col, ralentissement, créatures), **V2.44.3** l'hiver dans Unity (glace de l'eau, neige des toits, rochers, sol et col, fondu du feuillage), **V2.44.4** vendanges (rangs en 3 états, modèles du lot C reliés).

### Étape 4 : le dessous

| Sous-lot | Contenu | Taille | Dépend de | Modèles à demander |
|---|---|---|---|---|
| **V2.45** | Étages : portes de dessous, zones non intérieures avec créatures, relèvement des portes et zones reliées (la reprise au dernier point sûr est faite au V2.28) | M | V2.26, V2.28 | aucun |
| **V2.46** | Grotte derrière la cascade | S | V2.31, V2.45 | kit de grotte (parois, stalagmites, rideau d'eau vu de dedans) |
| **V2.47** | Mines de la carrière (donjon décidé au sujet 27), **trois niveaux** d'après la coupe du [plan de la mine](cartes/plan_mine_jour.jpg) (et [nuit](cartes/plan_mine_nuit.jpg)) : galeries boisées, rivière souterraine, cristaux du Seuil au fond | L | V2.37, V2.45 | kit de galerie boisée, ascenseur de mine, effondrements, filons, rivière souterraine, cristaux du Seuil |
| **V2.48** | Crypte-donjon sous l'abbaye (Retenu) ; petite crypte sous la chapelle du Bourg (secret) | M | V2.35, V2.45 | kit de crypte (voûtes, sarcophages, ossuaire) |
| **V2.49** | Égouts du Bourg | M | V2.30, V2.45 | kit d'égout (voûtes, canaux, grilles, échelles) |
| **V2.50** | Galeries sous les pierres du Seuil | M | V2.39, V2.45 | kit de galeries cristallines, éclats géants |
| **V2.50.1** | **Grottes noyées du lac** (donjon décidé au sujet 27 : marée et plongée), sous les marais, entrée dans les roselières d'après le [plan des marais](cartes/plan_marais_ete.jpg) | M | V2.36, V2.45 | kit de grotte noyée (voûtes basses, bassins, passages sous l'eau, épaves et coffres engloutis) |

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

Les quatre questions de la première version sont tranchées (section 0 bis). Aucune question ouverte pour l'instant ; les suivantes viendront avec chaque sous-lot.
