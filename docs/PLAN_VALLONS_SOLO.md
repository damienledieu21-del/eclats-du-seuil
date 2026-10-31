# Lot « Vallons solo finis » — plan de la vague 1, « un vrai jeu »

Plan de Claude du 29 octobre 2026, écrit pendant la nuit à la demande de Damien, **à valider par lui**. Les décisions de gameplay viennent de la [feuille de route](FEUILLE_DE_ROUTE.md) (décisions du 29 octobre 2026, 1 h 56 à 4 h 40) et du [cadrage](CADRAGE.md). Ce qui n'y est pas est marqué « proposition » ; les vrais arbitrages sont regroupés dans la [section 6](#6-questions-pour-damien).

Chaque sous-lot suit la méthode habituelle de [CLAUDE.md](../CLAUDE.md) : une branche et une PR, tests, vérification complète identique à la CI, relecture par un agent neuf, revue Codex, fusion seulement avec l'accord de Damien.

## En bref (pour Damien)

À la fin de la vague 1, les Vallons deviennent **un vrai jeu en solo** :

- une **quête principale en cinq chapitres** à travers tous les lieux des Vallons agrandis, du moulin au Gardien du Seuil, et une **vingtaine de quêtes secondaires** de toutes sortes ;
- **l'équipement complet** : toutes les places du personnage, des boucliers qui changent la garde, des armes et des bijoux à effets, des ensembles, des objets aux propriétés tirées au hasard, l'usure et la réparation ;
- **la monnaie (cuivre, argent, or) et les marchands** du bourg, et le **voyage rapide** entre les points sûrs découverts ;
- la **Guilde des aventuriers** : ta carte, les rangs G, F et E, les contrats et les épreuves de promotion ;
- l'**écran des compétences** pour changer sa barre partout hors combat ;
- un **équilibrage en difficulté normale**, des niveaux au-delà de 10, et des zones qui suivent ton niveau ;
- **plus de 5 heures** de jeu pour tout faire (estimation, à mesurer pendant l'essai).

Dix sous-lots, chacun une PR, dans l'ordre de la [section 3](#3-les-dix-sous-lots). Les cinq questions de la [section 6](#6-questions-pour-damien) sont réglées (29 octobre 2026, 4 h 47 et 5 h 19). Les vagues 2 à 4 (monde vivant, aventures, vie à côté du combat) viennent après ([section 7](#7-après-la-vague-1)).

## 1. Point de départ

État du code au 29 octobre 2026 (branche de l'essai 9, sur les intérieurs et l'essai réaliste 8). Le détail est dans [MONDE.md](MONDE.md).

| Domaine | Ce qui existe | Ce que la vague 1 ajoute |
|---|---|---|
| **Personnage** | Niveaux 1 à 10 (`progression.json`), attributs par classe et niveau, compétences gagnées en montant (2 places au niveau 1, 3 au 5, 4 et l'ultime au 10), arbre des talents ouvrable dans l'aventure | Niveaux au-delà de 10 (les règles en permettent 30), places de la barre qui continuent de grandir, écran des compétences |
| **Objets** | 4 emplacements (arme, bouclier, torse, pendentif), 24 places d'inventaire, six raretés, bonus fixes, 10 équipements, tables de butin tirées par la clé de la mort | Les 18 places, boucliers à garde propre, effets, ensembles, propriétés tirées, qualité, usure, comparaison, beaucoup d'objets |
| **Économie** | Rien : ni or ni marchand | Or, marchands, prix d'achat et de vente, réparation |
| **Quêtes** | Une mission en huit étapes, trois PNJ, conditions et effets, objectifs `fact`, `reach`, `defeat`, `collect`, `passage`, plusieurs quêtes possibles à la fois | Journal par quêtes principales et secondaires, nouveaux objectifs et effets, PNJ des intérieurs qui parlent, cinq chapitres, une vingtaine de quêtes secondaires |
| **Ennemis** | Loups, sangliers, archers, rôdeurs, le Gardien à deux formes ; 27 créatures, plafond de 128 combattants | Bandits, chef des bandits, une bête élite ; niveaux par lieu et montée au niveau du joueur |
| **Monde** | Vallons de 240 × 170 m, bourg d'Aube, trois intérieurs, cinq points sûrs, récolte et fabrication (forge, herboristerie, minage) | Voyage rapide, Guilde des aventuriers au bourg, forge d'équipement |

## 2. Ce qu'on pourra faire

Un tour de jeu, du début à la fin de la vague :

1. On commence au village, niveau 1, avec une épée et un écu. Odile parle des traces sous le moulin : c'est le chapitre 1.
2. Au bourg, on vend sa première peau de loup, on achète une potion, on s'inscrit à la **Guilde des aventuriers** (rang G, carte d'aventurier) et on prend un premier contrat de chasse.
3. On équipe ce qu'on trouve : un casque tombé d'un bandit, des bottes « du Renard » avec une propriété tirée au hasard. L'écran compare avec ce qu'on porte. Un bouclier lourd bloque plus mais coûte plus d'endurance, un bouclier léger rend la parade plus facile.
4. La forge de Maëlle fabrique une partie des pièces. Une pièce bien réussie sort de qualité supérieure, ou en chef-d'œuvre signé.
5. Les combats usent l'équipement selon les dégâts reçus ; on le répare contre de l'or.
6. On découvre les points sûrs (bourg, ferme, hameau, camp, tour) et on y voyage d'un coup, hors combat.
7. Les chapitres mènent aux champs, au lac, au hameau attaqué, à la tour de guet, au camp des rôdeurs et des bandits, puis au Seuil et au Gardien. Les quêtes secondaires remplissent les trous.
8. On passe les épreuves de promotion de la guilde jusqu'au rang E, le plus haut des Vallons ; le rang D viendra avec la région suivante.

## 3. Les dix sous-lots

Ordre proposé : d'abord ce qui sert à tout le reste (or, équipement), puis les systèmes (compétences, quêtes, guilde), puis l'équilibrage, enfin le contenu écrit et l'essai. Les tailles sont relatives (S, M, L), pas des dates.

### V1.1 — Monnaie, marchands, voyage rapide (M)

- **Monnaie** : **cuivre, argent et or** (choix de Damien du 29 octobre 2026, 12 h 57 ; 100 cuivre font 1 argent, 100 argent font 1 or), gardée avec le personnage (sauvegarde) comme un seul nombre de pièces de cuivre, bornée, arithmétique vérifiée ; affichée « 3 or 45 argent 12 cuivre ». Gagnée par les quêtes, les contrats, la vente ; jamais par l'argent réel (aucun jeton, sujet 6).
- **Marchands** en données (`merchants`, nouveau fichier ou bloc de l'histoire) : PNJ, objets en vente, prix, part du prix de base rendue à la vente. Au bourg : armurier, herboriste, épicier. Le stock change chaque jour du monde (tirage déterministe sur la clé du jour) ; le stock tournant poussé vient avec la vague 2.
- **Achat, vente, rachat** : opérations atomiques (or et inventaire changent ensemble ou pas du tout), refus avec motif (or insuffisant, inventaire plein, objet porté ou réservé). Un objet vendu par erreur se rachète au même prix tant que la session dure.
- **Voyage rapide** : entre les points sûrs **découverts** (clé au registre du monde), hors combat (aucun ennemi engagé, règle de la halte à 12 m). Le passage reprend le chemin d'une porte : sauvegarde, puis nouvelle session au point d'arrivée. Gratuit dans la vague 1 (le prix éventuel est un réglage d'équilibrage).
- **Unity** : écran du marchand (acheter, vendre, racheter), or affiché dans l'inventaire, carte qui propose le voyage.
- **Formats** : sauvegarde (or, points sûrs découverts) et histoire ou données des marchands en nouvelle version ; schémas, `DONNEES.md`, tests de lecture et de refus.
- **État** (30 octobre 2026, en PR) : fait. Bourse en cuivre dans la sauvegarde (version 6), effet `giveMoney` de l'histoire (schéma 2 : 50 cuivre, 1 argent 50 cuivre et 5 argent dans la mission d'Odile), marchands en données (`merchants.json`, schéma 1 : Garin l'armurier, Sauge l'herboriste, Bertille l'épicière), stock du jour déterministe, achat, vente (plusieurs lots d'un coup) et rachat atomiques, points sûrs découverts au registre du monde et voyage rapide hors combat ; écran du marchand, bourse dans l'inventaire et voyage depuis la carte dans Unity. Prix, parts de revente et récompenses provisoires (questions à Damien dans la PR). Détail : [MONDE.md](MONDE.md#monnaie-marchands-et-voyage-rapide-sous-lot-v11).

### V1.2 — Équipement complet : places, boucliers, écran (L)

- **Places** : **18** (choix de Damien du 29 octobre 2026, 4 h 47, [CADRAGE.md](CADRAGE.md) Q4) : 16 pour le combat (arme, bouclier, tête, épaules, cape, torse, brassards, mains, ceinture, jambes, pieds, pendentif, deux anneaux, deux reliques à effet), l'**Éclat du Seuil** du personnage (il grandit avec l'histoire) et un **outil de métier** (utile hors combat). Le Bastion porte **toujours un bouclier** (refus de l'ôter sans en mettre un autre).
- **Boucliers qui changent la garde** : chaque bouclier porte ses valeurs de garde (part des dégâts bloquée, coût en endurance d'un blocage, fenêtre de parade un peu plus large ou plus étroite). Changement des règles et du moteur : versionné, avec un nouveau journal de référence figé (les anciens ne changent pas).
- **Catalogue** : quelques dizaines d'objets pour les niveaux 1 à 20, plusieurs bons choix par tranche de niveau à rareté égale (H5) ; niveau requis, puissance, rareté et adéquation au build restent quatre notions distinctes.
- **Inventaire** : 24 places gardées pour l'instant, avec des sacs plus tard (vague 4). Tri et filtres simples.
- **Unity** : écran du personnage avec toutes les places, **comparaison claire** (vert ou rouge, avec les chiffres) en survolant un objet. Visible sur le Bastion : l'arme, le bouclier et la cape d'abord (les pièces d'armure attendront des modèles en morceaux, sans achat sans l'accord de Damien).
- **État** (30 octobre 2026, en PR) : fait. 18 places (catalogue en version 3), bouclier du Bastion et Éclat du Seuil toujours portés, garde propre de chaque bouclier (règles 0.13.0, moteur 0.25.0, nouvelle référence figée), 73 équipements des niveaux 1 à 20 avec leurs prix, équipement de départ, tri et filtres, sauvegarde en version 7, écran aux 18 places avec comparaison, arme, bouclier et cape visibles (formes simples pour la cape et les armes autres que l'épée du paladin). Les effets, les ensembles et les propriétés tirées restent pour V1.3. Détail : [MONDE.md](MONDE.md#équipement-complet-sous-lot-v12).

### V1.3 — Effets d'équipement : armes, bijoux, ensembles, propriétés tirées (L)

- **Effets** des armes et des bijoux par les déclencheurs du moteur, dont l'origine `equipment` est réservée depuis le lot 8 (par exemple : « une parade parfaite donne une Charge de plus », « les coups sur un ennemi étourdi infligent 10 % de plus »). Un seul validateur, bornes, tests de propriétés.
- **Ensembles** de 2, 4 et 6 pièces (bonus à 2, 4 et 6 pièces portées).
- **Objets nommés faits à la main** (boss, quêtes, légendaires) et **reste du butin aux propriétés tirées au hasard**, dans des bornes par rareté et niveau (Q30 : la rareté n'est pas le hasard des caractéristiques). Tirage déterministe par la clé de la mort, comme le butin : le même événement donne toujours le même objet.
- **Formats** : catalogue des objets en nouvelle version (effets, ensembles, propriétés), instance d'objet dans l'inventaire et la sauvegarde (propriétés tirées, qualité, usure). Règles et empreinte versionnées, nouveaux journaux figés.
- **État** (31 octobre 2026, en PR) : fait. Effets d'équipement par les déclencheurs d'origine `equipment` et les modificateurs (un seul validateur, `EquipmentEffect.Check`, bornes `combat.equipment` des règles 0.14.0, condition `targetStunned`), moteur 0.26.0 et nouvelle référence figée ; catalogue en version 4 : trois objets nommés à effets (Lame du Seuil, Amulette de l'aube, Talisman du Seuil), deux ensembles (Tenue du veneur, 4 pièces ; Garde de la tour, 6 pièces), six propriétés tirées par rareté et niveau ; tirage par la clé de la mort ; instance d'objet dans l'inventaire et la sauvegarde (version 9 ; qualité et usure réservées à V1.4) ; fiche Unity avec propriétés, effets et ensembles. La rondache du chef des bandits n'existe pas (question pour Damien). Détail : [MONDE.md](MONDE.md#effets-déquipement-ensembles-et-propriétés-tirées-sous-lot-v13).

### V1.4 — Forge, qualité, usure (M)

- **Recettes d'équipement** à la forge de Maëlle pour une partie des pièces (armes, boucliers, pièces de métal) ; les autres viennent du butin, des quêtes et des marchands (Q29).
- **Qualité de fabrication** : normale, supérieure, chef-d'œuvre signé du nom du personnage. Décision du sujet des métiers : « des petits jeux, mais aussi une part de chance ». **Dès la vague 1** (choix de Damien, 5 h 19) : un premier petit jeu de forge (frapper le métal au bon moment, trois frappes) qui fait monter la chance, le reste tiré au hasard. Une recette au-dessus de son niveau peut **rater**, avec une partie des matériaux rendue.
- **Usure** : chaque combat use l'équipement porté **selon les dégâts reçus**. Une pièce usée à zéro ne donne plus ses bonus, sans se casser. **Réparation contre de l'or** chez l'armurier ou à la forge.
- L'amélioration de +1 à +10, les pierres à sertir et les runes viennent avec la forge poussée (vague 4).
- **État** (31 octobre 2026, en PR) : fait. Cinq recettes d'équipement à la forge de Maëlle (épée de garde, casque de fer, écu du garde, gantelets de fer, cotte de mailles), petit jeu de forge à trois frappes (notes envoyées au cœur, tirage `Pcg32` sur une clé stable recette, jour, compteur), qualité normale, supérieure ou chef-d'œuvre signé, recette jusqu'à deux niveaux au-dessus qui peut rater (moitié des matériaux rendue), usure à la fin de chaque combat selon les dégâts reçus, hors du moteur (aucune règle ni aucun journal changé), pièce usée à zéro sans bonus, réparation atomique à la forge et chez l'armurier ; catalogue des objets en version 5, métiers en version 2, sauvegarde en version 11. Détail : [MONDE.md](MONDE.md#forge-qualité-usure-sous-lot-v14).

### V1.5 — Écran des compétences (S)

- Changer ses compétences **partout hors combat** (décision de Damien), refusé tant qu'un ennemi est engagé ou à moins de 12 m ; pris en compte à l'entrée suivante, comme les talents (`AdventureSession.SetTalents`).
- **Barre qui grandit** (sujet 3) : aujourd'hui 2 places au niveau 1, 3 au 5, 4 et l'ultime au 10. Proposition : une 5e place au niveau 15 et une 6e au niveau 20, ce qui équipe les six actives du Bastion avant le Gardien (niveau 25). La 7e et la 8e viendront quand le Bastion aura plus de compétences (ascensions).
- **Unity** : écran avec les compétences apprises, glisser une compétence sur une place (au doigt aussi), description et recharge.
- **État** (29 octobre 2026, en PR) : changer ses compétences hors combat (un seul juge, effet à la halte, gardé dans la sauvegarde) et l'écran (touche B, onglet Compétences : K est l'atelier) sont faits. Les 5e et 6e places ne le sont pas : le pont temps réel et le protocole du réseau réservent l'emplacement 4 à l'ultime, et la barre de combat, les touches et les boutons tactiles ont quatre places ; à faire avec les niveaux au-delà de 10 (V1.8). Détail : [MONDE.md](MONDE.md#personnage-sous-lot-122).

### V1.6 — Quêtes : le moteur (M)

- **Journal** à deux onglets, principales et secondaires ; suivi d'une quête choisie, avec son objectif sur la carte (déjà fait pour l'étape en cours de la mission).
- **Nouveaux objectifs** : parler à un PNJ (`talk`), livrer un objet à un PNJ (`deliver`), vaincre une créature précise ou un nombre de créatures d'une sorte, atteindre un rang de guilde, avoir un objet équipé. **Nouveaux effets** : donner de l'or (`giveGold`), prendre de l'or, donner des points de guilde.
- **Récompenses au choix** (choisir un objet parmi deux ou trois) et quêtes qui s'ouvrent à un niveau ou après une autre.
- **PNJ des intérieurs** qui parlent (aubergiste, prêtre, habitant) : la limite « ni dialogue dans une pièce » des intérieurs saute.
- **Nouveaux ennemis** : bandits (épée, arc), chef des bandits (à formes, comme le Gardien), une bête élite (« le loup blanc »), avec leurs attaques annoncées et leur butin.
- **Formats** : histoire en nouvelle version (objectifs, effets, catégories de quête), validateur étendu, tests de chaque objectif et de chaque refus.
- **État** (31 octobre 2026, en PR) : fait, sauf le rang de guilde (objectif) et les points de guilde (effet), laissés à la guilde (V1.7) qui en fixera le format. Histoire en schéma 3 (catégories, conditions d'ouverture dont `levelAtLeast`, récompenses au choix, objectifs `talk`, `deliver`, `defeatFamily`, `equipped`, effet `takeMoney`, PNJ des intérieurs), sauvegarde en version 8 (quête suivie, récompense en attente ; la version 7 est celle de V1.2, relue sans quête suivie), zones en schéma 5 (autres chefs à formes, familles) ; journal à onglets, suivi, récompenses au choix et dialogue des PNJ d'intérieur dans Unity ; bandits, chef des bandits à deux formes et loup blanc par les données de zone seulement (aucune règle ni aucun journal de référence changés par V1.6 ; ils suivent les règles 0.13.0 de V1.2, sans bouclier) ; deux quêtes d'exemple à l'auberge et à la chapelle (textes à relire). L'effet « donner de l'or » s'appelle `giveMoney` (V1.1). Détail : [MONDE.md](MONDE.md#quêtes--le-moteur-sous-lot-v16).

### V1.7 — Guilde des aventuriers (M)

- **Lieu** : la guilde au bourg (un bâtiment existant, avec son intérieur), sa maîtresse de guilde.
- **Carte d'aventurier** : rang, points, contrats réussis, titres. Rangs **G, F et E** dans les Vallons (choix de Damien, 5 h 19) ; D, C, B, A, S, SS et SSS avec les régions suivantes.
- **Points** gagnés par les accomplissements (contrats, quêtes, créatures rares, découvertes). Assez de points ouvrent une **épreuve de promotion** (un combat ou une mission précise par rang).
- **Contrats** : chasses (vaincre tant de loups), livraisons (apporter des herbes, du minerai), primes (un bandit nommé). Tirés chaque jour du monde, de façon déterministe, parmi une liste en données ; un rang plus haut donne des contrats plus durs et mieux payés.
- **Les quatre avantages** décidés : contrats plus durs et mieux payés, accès à des lieux, **boutique de la guilde**, **titre**.
- **Formats** : données de la guilde (rangs, seuils, épreuves, contrats), sauvegarde (carte), textes par clés.
- **État** (31 octobre 2026, en PR) : fait. Données de la guilde en schéma 1 (`guild.json` : les dix rangs, G, F et E ouverts, seuils, deux épreuves, titres, douze contrats, porte des archives au rang F, points des accomplissements), histoire en schéma 4 (objectif `guildRank`, effet `giveGuildPoints`, condition `guildRankAtLeast`), marchands en schéma 2 (réserve de la guilde selon le rang), sauvegarde en version 10 (carte d'aventurier ; la version 9 est celle de V1.3) ; Maison de la Guilde et ses archives (intérieurs), Dame Brunehilde, Aubin l'intendant, Ysaline l'archiviste et le Borgne (bandit nommé, par les données de zone seulement) ; tableau de la guilde, onglet « Carte d'aventurier » du journal et titre affiché dans Unity. Textes et valeurs à relire et régler. Détail : [MONDE.md](MONDE.md#guilde-des-aventuriers-sous-lot-v17).

### V1.8 — Progression et équilibrage (M)

- **Niveaux au-delà de 10** : pas de plafond voulu par Damien ; les données vont jusqu'au niveau 30 que les règles permettent déjà, avec des valeurs provisoires pour 21 à 30. La difficulté suit les chapitres de la quête principale : **du niveau 1 au niveau 25** dans les Vallons (choix de Damien, 5 h 19 : « plus long »).
- **Zones en mélange** (sujet 4) : chaque lieu a un **niveau minimum** ; si le joueur est plus fort, ses créatures **montent à son niveau**, et un réglage les garde faciles pour se sentir puissant.
- **Difficulté normale** (« on meurt parfois si on joue mal ; le Gardien demande garde et parade ») : combats simulés par le moteur pour chaque chapitre, comme `Lot129BalanceTests`, avec un build et un équipement typiques du niveau.
- **Rythme rapide puis plus lent** (sujet 4) : courbe d'expérience réglée pour atteindre le niveau 5 vite, puis ralentir.
- **Durée** : plus de 5 heures pour tout faire. Estimation par le contenu (section 4), à mesurer pendant l'essai.

### V1.9 — Contenu : quête principale et quêtes secondaires (L)

- Les textes sont **proposés par Claude et relus par Damien** (décision du 29 octobre 2026), **chapitre par chapitre** (choix de Damien, 5 h 19) : une PR de textes par chapitre, pour les relire tranquillement.
- Squelette de la quête principale et liste des quêtes secondaires : [section 4](#4-la-quête-principale-et-les-quêtes-secondaires).
- Tout passe par les validateurs (lieux, PNJ, objets, textes) et des tests qui jouent chaque quête de bout en bout avec le moteur (pilote scripté), comme la mission d'aujourd'hui.

### V1.10 — Places des maisons et essai de la vague (S)

- **Places des maisons du bourg agrandies** pour les grandes maisons de Sketchfab (décision de l'essai de style, 6 à 8 m d'emprise), avec les contrôles des Vallons agrandis (routes, haltes, créatures).
- **Essai complet dans Unity** avec Damien : une partie de bout en bout, captures, avis, puis corrections.

Les autres points de l'essai de style sont déjà faits ou réglés : pas d'achat d'animal pour l'instant ; une porte soigne comme une halte (intérieurs, PR #67).

## 4. La quête principale et les quêtes secondaires

Proposition, à relire par Damien. Elle relie les Vallons à l'histoire d'Orsédia (sujet 2, « épique et sombre », un peu d'humour chez les PNJ du quotidien).

### Quête principale : « Les Éclats de l'Aube », cinq chapitres

| Chapitre | Niveaux (choix de Damien : 1 à 25) | Lieux | Ce qui se passe |
|---|---|---|---|
| 1. Les traces sous le moulin | 1 à 5 | Village, carrière, moulin, pré des loups | La mission d'aujourd'hui, jusqu'aux éclats du pré. Le premier Éclat fait **revivre un souvenir** de la Rupture (une courte scène, avant les souvenirs jouables de plus tard). Gaspard envoie au bourg. |
| 2. Le bourg d'Aube | 5 à 10 | Bourg, chapelle, auberge, guilde | Sœur Albe, de l'Ordre de l'Aube, reconnaît l'Éclat. Des vols d'éclats au bourg ; Renart, le marchand ambrien, en sait plus qu'il ne dit. Inscription à la guilde. |
| 3. Les champs et le hameau | 10 à 15 | Champs, lac, vergers, hameau de la carrière | Les bandits attaquent le hameau et volent les éclats pour quelqu'un. Défendre le hameau, suivre leur piste jusqu'au lac. |
| 4. La tour et le camp | 15 à 20 | Tour de guet, camp des rôdeurs | La tour ne répond plus. Au camp, le **chef des bandits** travaille pour les **Rassembleurs** ; il parle du « Sans-Repos ». Premier choix : l'épargner ou non (le choix reste écrit dans les faits ; ses suites viendront avec la vague 3). |
| 5. Le Seuil | 20 à 25 | Hautes pierres, petite zone du Seuil | La manifestation grandit, le rôdeur du Seuil, puis le **Gardien du Seuil** et sa forme éveillée. Retour chez Odile. Une voix de l'Envers murmure : « Celui-qui-Attend ». |

### Quêtes secondaires (une vingtaine)

Mélange voulu par Damien : courtes et longues, faciles et difficiles, en plusieurs étapes et directes.

| Quête | Donneur | Sorte | Lieu |
|---|---|---|---|
| Le pain du matin | Le boulanger du bourg | Courte, facile, livraison | Bourg, champs |
| Les sangliers des champs | Le fermier | Courte, chasse | Champs |
| La lettre au fond du lac | Une veuve du bourg | Moyenne, choix (rendre la lettre ou la garder) | Lac, bourg |
| L'apprentie de Maëlle | Maëlle | Moyenne, en étapes, tutoriel de la forge | Forge, carrière |
| Herbes pour la chapelle | Sœur Albe | Courte, récolte | Lac, vergers |
| La tour silencieuse | Le capitaine de la garde | Longue, en étapes, difficile | Tour de guet |
| Le loup blanc | Gaspard | Difficile, créature rare | Forêt des Loups |
| Les vergers pillés | La fermière des vergers | Moyenne, bandits | Vergers |
| Les pierres gravées | Le vieux Gaspard | Longue, exploration | Partout |
| Une dette à l'auberge | L'aubergiste | Courte, humour, or | Auberge |
| Le marchand qui en savait trop | Renart | Moyenne, choix | Bourg, hameau |
| Les cloches de la chapelle | Le prêtre | Courte, intérieur | Chapelle |
| Le puits qui murmure | Une enfant du bourg | Courte, mystère du Seuil | Place du bourg |
| La ferrure de rechange | Odile | Directe, fabrication | Moulin, forge |
| Les flèches perdues | Un archer de la garde | Courte, récolte sur des ennemis | Plaine de l'est |
| Le déserteur | Un rôdeur | Moyenne, choix (le livrer ou l'aider) | Camp |
| Les potions du hameau | L'herboriste | Directe, herboristerie | Hameau |
| Le pont des Vallons | Le meunier | Moyenne, en étapes | Rivière |
| La prime du Borgne | La guilde | Difficile, prime | Camp, bois |
| La dernière lanterne | L'habitant de la maison du bourg | Courte, intérieur | Maison du bourg |

**Durée estimée** (à mesurer pendant l'essai) : quête principale de 2 h 30 à 3 h, quêtes secondaires d'environ 2 h 30, guilde et contrats d'environ 1 heure, donc **6 heures et plus** pour tout faire.

## 5. Formats et contrats techniques

- **Versions** : catalogue des objets, histoire, progression, sauvegarde et règles passent chacun en nouvelle version, documentée dans `DONNEES.md` et testée (lecture, refus avec chemin précis, migration des sauvegardes). Une sauvegarde d'aujourd'hui doit se relire dans la nouvelle version.
- **Moteur** : les effets d'équipement (origine `equipment`) et la garde du bouclier changent les règles. Version de contenu et empreinte des règles montent ; **nouveaux journaux de référence figés**, les anciens restent intacts.
- **Déterminisme** : tous les tirages (propriétés des objets, qualité de fabrication, stock des marchands, contrats du jour) passent par `Pcg32`, initialisé par une clé stable (mort, recette et jour du monde, contrat). Aucune horloge réelle.
- **Atomicité** : achat, vente, réparation, fabrication, récompense et promotion changent tout ou rien. Or et quantités vérifiés (`checked`), bornés.
- **Projets** : tout reste en `netstandard2.1`, C# 9, sans dépendance. Chaque nouvelle API de Unity reçoit son bouchon dans `tests/EdS.Unity.CompileCheck`.
- **Tests** : unitaires, adversariaux et de propriétés pour chaque système ; quêtes jouées de bout en bout par un pilote ; combats d'équilibrage simulés ; une catégorie de tests par grand système si besoin (déclarée dans `ci.yml` et `scripts/test-category.sh`, changement signalé dans la PR).

## 6. Questions pour Damien

Toutes réglées le 29 octobre 2026 :

1. **La cape** (4 h 47) : **18 places** d'équipement, dont la cape, l'Éclat du Seuil et un outil de métier (V1.2).
2. **Niveaux des chapitres** (5 h 19) : **1 à 25** (« plus long ») : chapitres de 1 à 5, 5 à 10, 10 à 15, 15 à 20 et 20 à 25 ; le Gardien vers le niveau 25.
3. **Rangs de la guilde dans les Vallons** (5 h 19) : **G, F et E** ; le rang D vient avec la région suivante.
4. **Petit jeu de forge** (5 h 19) : **dès la vague 1**.
5. **Relecture des textes** (5 h 19) : **chapitre par chapitre**.

## 7. Après la vague 1

Découpage décidé (« Les Vallons en très grand », 29 octobre 2026) :

- **Vague 2, le monde vivant** : événements du monde (attaque du hameau, faille du Seuil), jour et nuit qui changent le monde, météo, villageois qui suivent leur journée, animaux paisibles, réputation (villageois, garde, rôdeurs, bandits), fêtes au bourg, marchands au stock changeant, Seuil qui grandit.
- **Vague 3, les aventures** : les trois donjons (crypte sous la chapelle, mines de la carrière, grottes noyées du lac) avec pièges, énigmes, chef et réveil renforcé ; le boss du monde « le Grand Solitaire » ; créatures rares nommées ; chasses avec pistage ; cartes au trésor ; énigmes du Seuil ; arène du camp ; choix qui restent (chef des bandits, hameau).
- **Vague 4, la vie à côté du combat** : cuisine et boulangerie (concours de pâtisserie), pêche et carnet, jeu de cartes à la taverne, tir à l'arc et courses, maison au bourg, compagnon, forge poussée (+1 à +10, sertissage, ensembles, arme légendaire), bestiaire, hauts faits et titres, livres à lire.

Les quatorze grands sujets du jeu complet (classes, histoire, combat, progression, multijoueur et chat, modèle économique, montures et voyages, identité, régions, jeu après l'histoire, compagnons, fêtes et saisons, âme du jeu, réglages et accessibilité) sont dans la feuille de route. Ils servent de cap : la vague 1 n'en prend que ce qu'il faut pour les Vallons en solo.

## 8. Ordre de travail

| Sous-lot | Taille | Dépend de | Essai par Damien |
|---|---|---|---|
| V1.1 Monnaie, marchands, voyage rapide | M | — | — |
| V1.2 Équipement complet | L | V1.1 (prix) | Oui, court (écran, boucliers) |
| V1.3 Effets d'équipement | L | V1.2 | — |
| V1.4 Forge, qualité, usure | M | V1.1, V1.3 | Oui, court (forge, réparation) |
| V1.5 Écran des compétences | S | — | — |
| V1.6 Quêtes : le moteur | M | V1.1 (or) | — |
| V1.7 Guilde des aventuriers | M | V1.6 | — |
| V1.8 Progression et équilibrage | M | V1.2 à V1.7 | — |
| V1.9 Contenu des quêtes | L | V1.6 à V1.8, relecture de Damien | Relecture des textes |
| V1.10 Places des maisons et essai | S | Tout | **Oui, la partie complète** |

Les branches s'empilent tant que la précédente n'est pas fusionnée. V1.5 peut se faire à tout moment. Avant de commencer : fusionner l'essai réaliste 8 (#65), les intérieurs (#67) et l'essai 9, puis faire l'essai des intérieurs et de la caméra dans Unity.

La suite, chantier par chantier jusqu'à l'alpha fermée sur PC (fin de la vague 1, vagues 2 à 4, lot 13, classes, préparation) : [PLAN_ALPHA.md](PLAN_ALPHA.md) (proposition à relire par Damien).
