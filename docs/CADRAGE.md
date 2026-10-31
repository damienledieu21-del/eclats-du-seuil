# Cadrage du jeu complet — registre des décisions

**Sources**, archivées sans modification dans [cadrage/](cadrage/) :

- le questionnaire de Claude du 19 octobre 2026 ([2026-10-19_questionnaire_q01_q40.md](cadrage/2026-10-19_questionnaire_q01_q40.md)) ;
- le document de Damien « Réponses à Claude, Q1 à Q40 et vision consolidée » :
  - version du 19 octobre ([2026-10-19_reponses_q01_q40.md](cadrage/2026-10-19_reponses_q01_q40.md)),
  - mise à jour du 20 octobre ([2026-10-20_reponses_q01_q40_v2.md](cadrage/2026-10-20_reponses_q01_q40_v2.md)) : activités de groupe, téléphones d'essai ;
- les réponses de Damien aux décisions L11-D1 à L11-D12 du plan ([2026-10-20_reponses_l11_d1_d12.md](cadrage/2026-10-20_reponses_l11_d1_d12.md)). Le document les présente comme des « réponses conseillées » ; **Damien les a toutes validées** dans la conversation le 20 octobre 2026 vers 0 h 30 (« Oui, toutes »).

Ce registre en reprend les choix, confrontés au dépôt (`main` à `f5998c5` : moteur 0.14.0, journal au format 8, contenu du Bastion 0.5.0, protocole réseau 1).

**Rôle des documents.** La [feuille de route](FEUILLE_DE_ROUTE.md) reste la source de vérité du lot en cours et des règles du moteur. Ce registre est celle de la vision du jeu complet. Le [plan à partir du lot 11](PLAN_LOT_11.md) détaille l'ordre des chantiers.

Un choix « Retenu » ne modifie aucune règle du moteur tant qu'un lot ne l'a pas réalisé. Les écarts avec l'existant sont listés en section 7.

## 1. Comment lire ce registre

| Statut | Sens |
|---|---|
| **Retenu** | Choix exprimé ou validé par Damien. « Retenu (20/10, L11-Dn) » : validé avec la décision Dn du 20 octobre. |
| **Recommandation** | Réponse proposée (questionnaire ou cadrage), qui sert de base de travail. Elle n'est jamais présentée comme une décision de Damien : elle est confirmée ou corrigée avant le lot qui la réalise. |
| **Ouvert** | Chiffre, préférence ou règle encore à arbitrer ou à étudier. |

| Périmètre | Sens |
|---|---|
| **Tranche** | Première tranche jouable : les Vallons d'Aube (lots 11 à 14). |
| **Alpha** | Première alpha proposée à des testeurs extérieurs. C'est un jalon à définir : testeurs, boucle jouable, persistance, plateformes, contenu, critères de stabilité (Q40, L11-D12). |
| **Final** | Le jeu à sa sortie. |

Les chiffres d'une tranche ou d'une alpha (24 places, niveaux 1 à 10…) sont **provisoires et configurables**. Ce ne sont jamais des plafonds du MMORPG. Ceux qui existent déjà sont des paramètres des données (`rules.json`) ; ceux à venir le seront aussi.

## 2. Direction générale (Retenu)

- **Le jeu.** Éclats du Seuil est un MMORPG original pour PC et mobile, principalement jouable seul, dans un monde multijoueur partagé. Combat en temps réel, progression, exploration, métiers, relations avec les PNJ et découvertes se complètent.
- **Solo et groupes** (20 octobre, L11-D9) : **aucune voie essentielle de progression n'exige un groupe** ; des expéditions peuvent être réservées aux groupes.
  - Voies essentielles : campagne principale, progression de classe, métiers, construction de builds viables.
  - Les livres, héritages et recettes déterminants pour ces voies ont un accès solo ou une autre voie prévue.
  - Les défis collectifs peuvent avoir leurs titres, cosmétiques et marques de prestige, sans promesse de récompenses identiques pour tous les formats.
  - Cette règle remplace « aucun groupe imposé pour la moindre activité ».
- **Les inspirations** : *The Exiled Heavy Knight* et *Satisfy / Overgeared*. Elles donnent une direction (identité des classes, compréhension des mécaniques, builds, artisanat, objets remarquables, secrets, trajectoires personnelles). Leurs personnages, leur intrigue et leurs règles ne sont pas repris comme un cahier des charges canonique.
- **L'univers.** La Rupture, le Seuil et les Éclats relient les systèmes du monde. La promesse : construire un personnage, trouver sa place dans l'univers, laisser une trace de ses aventures.
- **Direction artistique : fantasy stylisée.** (Damien, 31 octobre 2026 : remplacée par « entre ESO et Fable », voir le sujet 49 de la [feuille de route](FEUILLE_DE_ROUTE.md).)
  - « Fantasy stylisée » décrit la direction graphique ; « plutôt sombre » (ancienne vision) décrivait une ambiance. Les deux ne s'opposent pas : Vallons accueillants, donjons inquiétants et Seuil étrange dans une même identité (L11-D10).
  - *Déclinaison proposée (Recommandation)* : volumes doux et travaillés, matières peintes, couleurs expressives, silhouettes d'équipement reconnaissables, ambiance propre à chaque région. Le degré de détail est à affiner à partir de la planche des Vallons.
- **Deux vues au choix du joueur**, utilisables en exploration comme en combat :
  - **vue 1**, vue haute de trois quarts ;
  - **vue 2**, troisième personne montrant le personnage entier.

  La caméra ne change ni les portées, ni les collisions, ni les règles (détail en H14).

## 3. Registre des réponses Q1 à Q40

### A. Première région et tranche jouable

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q1 | Première région | **Vallons d'Aube** : village, chemins, rivière, moulin, carrière, ruines, manifestations du Seuil ; retours ultérieurs (secrets, ressources, quêtes de PNJ, poches de danger avancé) | Retenu | Géographie détaillée à concevoir, avec des trajets lisibles |
| Q2 | Mission de la tranche | **« Les traces sous le moulin »**, en cinq étapes : enquêter au village ; suivre les traces ; rouvrir le passage du moulin (récolte, fabrication, raccourci réel) ; comprendre la manifestation ; vaincre le chef et revenir. Avec un chef final et au moins une conséquence observable liée à un PNJ | Retenu (20/10, L11-D10) | Une réponse ou une action change une aide, une information ou un objectif. Quête secrète et réveil renforcé : présence à estimer, chacun à une étape identifiée du plan |
| Q3 | Niveaux du premier parcours | **1 à 10**, sans figer le plafond du jeu final | Retenu (20/10, L11-D10) | « Les compétences prévues pour ce premier parcours » ; livres, héritages et découvertes en apportent ensuite. Plafond final : Q18 |
| Q4 | Équipement (final) | **Plusieurs emplacements, effets spéciaux, choix adaptés aux builds** ; plusieurs bons choix par tranche de niveau, même à rareté égale ; le **bouclier du Bastion est un vrai équipement** ; **18 places** (Damien, 29 octobre 2026, 4 h 47 : « encore plus de places ») : 16 pour le combat (arme, bouclier, tête, épaules, **cape**, torse, **brassards**, mains, ceinture, jambes, pieds, pendentif, deux anneaux, **deux reliques** à effet), une place pour l'**Éclat du Seuil** du personnage, qui grandit avec l'histoire, et un **outil de métier** | Retenu (29 octobre 2026 ; remplace la recommandation de 12 emplacements, qui n'avait pas de cape) | — |
| Q4 | Équipement (tranche) | **Arme, main secondaire ou bouclier, armure, bijou** : modèle extensible, catalogue restreint | Retenu (20/10, L11-D10) | — |
| Q5 | Inventaire | Tranche : **24 places, objets empilables** | Retenu (20/10, L11-D10) | Final : capacité évolutive, filtres, comparaison, stockage, objets de quête, commerce. Capacité finale (40 puis 80 évoqués) : Ouvert |
| Q6 | Récolte et fabrication | **Minerais et forge, plantes et remèdes** ; minerais ordinaires distincts des Éclats ; **chaque métier a son expérience et ses niveaux**, séparés du niveau de combat | Retenu | Tranche : une courte chaîne de chaque famille, utile à l'aventure (Retenu, 20/10, L11-D10) |
| Q7 | Potions et Bastion | **Potions de soin limitées et récupération encadrée**, recharge partagée entre consommables de soin. Le Bastion n'a pas de soin propre ; il peut utiliser les consommables universels et recevoir le soutien d'un allié | Retenu (20/10, L11-D10) | A1 du lot 8 visait l'arbre Gardien pendant le lot 8 (écart E4). L'effet de soin est à planifier dans le moteur avant de présenter les remèdes du lot 12 comme fonctionnels. Valeurs, stock et recharge à équilibrer |
| Q8 | Défaite | **Retour au dernier point sûr** adapté, **sans perte** des acquis (niveaux, XP gagnée, équipement, livres, métiers, étapes de quête acquises) ; pas de perte punitive supplémentaire | Retenu | **Exception JcJ voulue** : en tombant dans une région sauvage, une partie du butin récolté tombe dans un sac à reprendre (jamais l'équipement ; [feuille de route](FEUILLE_DE_ROUTE.md), sujet 38). Le coût est le temps, la tentative et les consommables. En groupe : secours si la rencontre le permet ; réinitialisation quand tous ont échoué ou quitté la zone, sans effacer une victoire enregistrée |
| Q9 | Deuxième joueur | **Région partagée** : se voir, se déplacer et combattre ensemble, avec ses objectifs personnels | Retenu | Crédit des actions, aide et partage de quête explicites. Le lot 10 ne couvre pas le monde partagé (écart E5, L11-D8) |
| Q10 | Sauvegarde | Final : **comptes et progression sous autorité serveur**, données versionnées, migrations | Retenu (implicite : la source écrit « Jeu final : B » sans le mot « retenu » ; à confirmer) | Tranche : sauvegarde adaptée aux essais, fichier local (Retenu, 20/10, L11-D10) ; un personnage local n'a pas de valeur dans le monde public ; politique de remise à zéro à annoncer |
| Q11 | Entrée en combat | **Sur place, en temps réel**, sans changement d'écran ; un donjon peut avoir son entrée et son instance | Retenu | — |
| Q12 | Éléments de terrain | Final : **raccourcis, pistes, accès secondaires, passages souterrains, manifestations, interactions** | Retenu (implicite : la source écrit « Jeu final : A » ; à confirmer) | Tranche : un raccourci utile et une manifestation du Seuil (Retenu, 20/10, L11-D10). Les accès essentiels ont une solution pour toutes les classes |

### B. Combat

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q13 | Positions | **Positions réelles** qui comptent : portée, placement, zones à éviter, mouvements utiles, contournement, interactions avec les lieux ; garde et parade restent importantes | Retenu | Réalisées d'abord (lot 11, L11-D1). Règles : L11-D2 à L11-D7 (section 5) |
| Q14 | Rôles | **Chaque classe accomplit seule** la campagne et les voies principales ; protection, dégâts, contrôle et soutien perceptibles et utiles en coopération | Retenu | Expéditions réservées aux groupes autorisées (20/10, L11-D9) : un joueur solo ne termine pas forcément toutes les rencontres de groupe |
| Q15 | Soin (jeu complet) | **Classe ou orientation de soutien** avec soin, protection, renforcement, gestion d'altérations ; voie solo satisfaisante | Recommandation | Ne remplace ni garde, ni parade, ni placement |

### C. Classes et progression

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q16 | Classes de la première alpha | **Trois classes complètes** (mêlée, distance, soutien ou spécialisation ; Damien, 29 octobre 2026 : **5 classes pour l'alpha**, voir le sujet 22 de la [feuille de route](FEUILLE_DE_ROUTE.md)) | Recommandation | Nombre final : Ouvert. Une alpha élargie à 4 ou 5 classes est possible |
| Q17 | Deuxième classe | **Classe à distance**, de préférence un **mage lié au Seuil** | Recommandation | Nom et trois arbres à concevoir |
| Q18 | Niveau maximal | **30 n'est pas un plafond final validé** ; il reste la configuration actuelle (Damien, 31 octobre 2026 : plafond qui monte, par exemple 25 dans les Vallons, relevé à chaque extension ; voir le sujet 54 de la [feuille de route](FEUILLE_DE_ROUTE.md)) | Ouvert | Fixer courbe d'XP, paliers, arbres, livres et héritages avant le chiffre |
| Q19 | Après le plafond | **Plusieurs progressions** : maîtrises, livres, héritages, connaissances, métiers, réputation, équipements, recherches, refuge ; Éclats encadrés (pas d'empilement illimité) | Retenu | — |
| Q20 | Attributs | **Base fixée par la classe et le niveau**, modifiée par l'équipement et le build ; personnalisation par les arbres, les deux livres actifs et l'héritage ; **plus 2 points à placer par niveau** (redistribuables chez le maître de classe ; Damien, 30 octobre 2026 : remplace « pas de répartition manuelle ») | Retenu (20/10, L11-D10 ; points à placer : 30 octobre 2026) | Un build peut garder un état résolu, et un scénario d'essai des valeurs explicites : besoin de simulation distinct de la source des caractéristiques du personnage persistant. Migration versionnée si nécessaire ; scénarios existants reproductibles selon leur version de règles (écart E6) |
| Q21 | Changer de build | Compétences de la barre : **partout hors combat** ; talents : **chez le maître de classe, gratuit les premières fois, puis payant en or** (Damien, 29 octobre 2026, à la question posée sur l'écart : remplace « gratuit, en lieu sûr, hors combat ») ; configurations sauvegardées ; un livre retiré rend ses points ; les livres appris restent | Retenu (29 octobre 2026) | Ne réinitialise ni décisions narratives, ni relations, ni serments. Prix en or et nombre de changements gratuits : à régler à l'équilibrage. Changement d'héritage : Ouvert |

### D. Monde

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q22 | Régions | Final : **cinq régions, une ville centrale, le Cœur du Seuil**. Noms de travail : Vallons d'Aube, Forêt des Murmures, Côte des Brumes, Crêtes de Fer, Plateau de Cendre, Havre d'Ambre (ville), Cœur du Seuil (expéditions avancées) | Recommandation | Tranche : les Vallons (Retenu). Trois régions : cible possible d'une alpha élargie. Une région n'est pas une bande de niveau unique |
| Q23 | Cœur du Seuil | **Expéditions principales faisables seul ou à plusieurs** ; des expéditions **exclusivement collectives** sont possibles, annoncées comme telles | Retenu (collectives exclusives : 20/10, L11-D9) | Pas d'obligation de groupe indirecte pour les voies essentielles. Réveil renforcé d'un boss solo : solution solo exigeante prévue |
| Q24 | Montures, voyages | **Montures et points de voyage**, destinations découvertes au préalable | Recommandation | Ne décide ni du vol libre ni de la navigation |
| Q25 | Logement | **Refuge personnel évolutif** (camp, maison, atelier, domaine) | Retenu | « Plus tard » est un ordre de réalisation |
| Q26 | Jour, nuit, météo | **Effets sur le jeu** (créatures, traces, ressources, passages, manifestations), règles apprenables | Retenu | Les occasions importantes reviennent ; pas d'heure réelle très restrictive. *Note technique de Claude* : le temps du monde découlera des ticks du serveur (contrat de déterminisme) |

### E. Métiers et économie

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q27 | Métiers | **Expérience et niveaux propres à chaque métier** | Retenu | Tous les métiers découvrables sur un personnage (Recommandation) ; expertises avancées exclusives : Ouvert. La limite de deux livres ne concerne que le combat |
| Q28 | Marché | **Échanges, commandes artisanales, spécialités régionales, activités commerciales** | Retenu | Marchés régionaux avec une recherche commune, échanges directs et commandes (Recommandation) ; marché global : alternative ouverte. Rien ne se déduit du seul mot « hôtel des ventes » |
| Q29 | Meilleur équipement | **Fabrication, butin et grandes quêtes se complètent** ; la fabrication atteint les plus hautes raretés ; créations signées, commandes | Retenu | — |
| Q30 | Caractéristiques des objets | Échelle **Commun → Rare → Très rare → Épique → Légendaire → Mythique** ; **Secret** est une origine, pas un rang | Retenu | Identité définie et variations bornées (Recommandation). La rareté n'est pas le hasard des caractéristiques |

### F. Multijoueur

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q31 | Taille des groupes | **De 2 à 6 joueurs au plus** (« au vu du nombre de classes différentes ») ; **raids de 10 à 20 joueurs**, réservés aux groupes et annoncés comme tels | Retenu (Damien, 29 octobre 2026 ; remplace la recommandation de cinq) | Compagnons comptés par une règle explicite ; capacité des instances selon les mesures |
| Q32 | Guildes | **Oui** : identité, projets, contrats, expéditions, installations, transformations régionales ; facultatives | Retenu | — |
| Q33 | Combat entre joueurs | **Toujours volontaire** : duels, arènes classées, zones de guerre (Lumière contre Ombre) et quelques **régions sauvages** où l'on peut s'attaquer librement, avec un meilleur butin, où l'on n'entre que si on le veut ; progression principale en PvE ; pas de PvP sur les routes ordinaires | Retenu (Damien, 29 octobre 2026) | Ses récompenses ne deviennent pas un passage obligé du PvE |
| Q34 | Boss de monde | **Événements publics coopératifs facultatifs**, liés à la région ; contribution utile comptée (pas seulement le dernier coup) | Retenu | Nombre de joueurs et modalités : à mesurer |

### G. Produit

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q35 | Plateformes | Final : **PC et mobile**, même personnage et même progression (Damien, 31 octobre 2026 : lancement sur PC, téléphone quelques mois après dans le même monde, voir le [tri](PERIMETRE.md#points-tranchés-par-damien-31-octobre-2026-1-h-20), point 1) ; mobile pris en compte dès la conception (commandes, caméra, lisibilité, performances), **validation tactile assez tôt** | Retenu | Premiers essais sur PC (Recommandation) ; premier essai tactile pendant le lot 11 (L11-D1). Téléphones : **Samsung Galaxy Note8** (Android) et **iPhone 13** (20/10, L11-D11), versions des systèmes à relever |
| Q36 | Modèle économique | **Gratuit**, avec une **boutique** (apparences, passe de saison, confort) et un **abonnement facultatif de confort** ; **extensions gratuites** pour l'instant (à revoir « dans très longtemps ») ; **aucun jeton** d'or contre de l'argent réel | Retenu (Damien, 29 octobre 2026) | Aucun achat de puissance contre de l'argent réel. Prix : Ouvert. Les livres s'achètent en monnaie du jeu |
| Q37 | Langues | **Français et anglais** ; rédaction en français, textes préparés pour la traduction | Recommandation | Énigmes et jeux de mots adaptés à chaque langue |
| Q38 | Hébergement | Essais locaux, puis serveur adapté aux tests à distance et au jeu persistant | Recommandation | Budget, hébergeur, nombre de joueurs : Ouvert (nombre de joueurs : environ 2 000 à 5 000 par serveur, choix de Damien du 29 octobre 2026, voir le sujet 24 de la [feuille de route](FEUILLE_DE_ROUTE.md)). Aucune dépense autorisée par ce cadrage. La machine d'un autre projet n'est pas présumée disponible |
| Q39 | Graphismes | **Fantasy stylisée, vues 1 et 2** (Damien, 31 octobre 2026 : la direction artistique devient « entre ESO et Fable », voir le sujet 49 de la [feuille de route](FEUILLE_DE_ROUTE.md)) | Retenu | Méthode de production (formes simples, packs, artistes, IA) : Ouvert ; toute ressource payante est proposée avec son coût avant engagement |
| Q40 | Horizon de l'alpha | Aucune date ni disponibilité fournie | Ouvert | Jalons et estimation : [plan](PLAN_LOT_11.md#7-estimation), à réévaluer après les premières démonstrations (L11-D12) |

## 4. Systèmes de la vision (H1 à H14)

La source ne donne pas de statut d'ensemble à cette partie (« systèmes essentiels absents ou sous-représentés dans le questionnaire »). Seuls H3 (« exigence explicite ») et H14 (« retenue ») en ont un. Ici, « Retenu » veut dire : **direction décrite par Damien comme système essentiel**. Les détails que la source présente comme des propositions sont marqués « Recommandation ». Les systèmes absents d'une première démonstration restent dans la vision ; leur arrivée correspond à une étape identifiée du plan (L11-D10).

| H | Système | Ce qui est retenu | Statut | À spécifier |
|---|---|---|---|---|
| H1 | PNJ avec mémoire | Personnalité, objectifs, relations, habitudes ; ils se souviennent des actes pertinents. Réponses et actions changent indices, confiance, objectifs, aide, alliances, accès aux secrets. Une réponse polie n'est pas toujours la meilleure ; certaines décisions ferment une possibilité | Retenu | États narratifs et réactions cohérentes, pas de simulation illimitée des conversations |
| H2 | Trois échelles de conséquences | **Parcours personnel**, **expédition d'un groupe**, **chapitre public d'une région** ; un joueur ne supprime pas seul un PNJ indispensable aux autres ; les transformations durables créent des aventures et laissent des traces | Retenu | Règle des choix partagés d'une expédition |
| H3 | Quêtes secrètes conditionnelles | Déclenchées par une combinaison d'**acquis durables** et de **circonstances temporaires**, puis une action finale ; voies alternatives ; indices ; journal des découvertes ; les occasions reviennent ; la première découverte donne du prestige sans rien confisquer | Retenu | Format des conditions ; récompenses variées (une quête secrète n'est pas forcément mythique) |
| H4 | Donjons, boss et réveil renforcé | Boss final garanti par donjon ; populations expliquées par les lieux. Le **réveil renforcé** (« Érance », nom provisoire) suit des conditions et des signes lisibles. Règle de récompense (« recommandée et retenue dans sa direction ») : première victoire enregistrée et récompensée une seule fois ; suite renforcée annoncée ; récompense supplémentaire si elle est vaincue ; première victoire gardée en cas d'échec ; ni défaite, ni redémarrage, ni reconnexion ne rendent le premier palier | Retenu | Un seul réveil par expédition (Recommandation) ; taux d'apparition et de butin : Ouvert. Le cycle de vie des ennemis du 11.1 doit permettre réapparition et réveil (L11-D1) |
| H5 | Équipement varié | Quatre notions distinctes : niveau requis, puissance de base, rareté, adéquation au build ; plusieurs boucliers de même niveau aux orientations différentes ; « rare » ne veut pas dire unique sur le serveur | Retenu | Éveil et reforge, par famille ou par objet |
| H6 | Deux livres de build actifs | **Deux livres actifs au plus par configuration**, sur l'ensemble des trois arbres (pas deux par arbre) ; une seconde copie ne cumule pas ; collection permanente ; achat en monnaie du jeu, ou trouvés en donjon, quête ou découverte ; prérequis visibles | Retenu | Place des modules dans l'arbre, coût en points, seuils des majeurs, sort de l'exemplaire physique. Aucun livre ne doit être le seul moyen d'éviter les attaques ordinaires (L11-D4) |
| H7 | Arbres, héritages, maîtrises | **Trois arbres combinables** par classe (en place pour le Bastion) ; **héritages** gagnés par exploration, relations, recherche et épreuves ; **maîtrises** prouvées par des épreuves | Retenu | Une seule orientation d'héritage active (Recommandation). Seuils, ultimes et points à relire ; changement d'héritage : Ouvert |
| H8 | Combinaisons et terrain | Une compétence crée une situation qu'une autre exploite (marque, ouverture, déstabilisation, zone, obstacle), en solo et avec des alliés | Retenu | Coûts, durées et limites ; commandes lisibles sur mobile. Exemple de la marque après parade : Recommandation |
| H9 | Métiers et créations signées | XP, niveaux, techniques, recettes et spécialisations indépendants du combat ; recette connue, maîtrise, outillage et matériaux distincts ; fabrication triviale moins formatrice ; créations signées ; recherche, enseignement, procédés perdus | Retenu | Catalogue des familles (extraction minière, bûcheronnage, cueillette, pêche, forge et armurerie, cuir, textile, alchimie, cuisine, artisanat des Éclats) : Recommandation |
| H10 | Économie, commandes, caravanes | Commandes à contrat (biens conservés jusqu'à l'issue), échanges sans connexion simultanée, spécialités régionales, routes et caravanes, plusieurs voies d'approvisionnement pour le solo | Retenu | Échanges, liaison des objets, livraison, taxes, consommation : Ouvert |
| H11 | Géographie, Atlas, nature | Surface, profondeurs et échos du Seuil dans certaines régions (pas la carte trois fois) ; repères stables ; **Atlas de la Rupture** (connaissances, cartes, bestiaire, langues, matériaux, phénomènes) qui ouvre des actions ; populations mobiles avec indices ; grandes chasses préparées | Retenu | — |
| H12 | Factions, transformation des régions | Factions aux intérêts divisés, rangs gagnés par des actes, incompatibilités annoncées ; reconstruction collective (villes, routes, mines, phares, ateliers) ; chroniques | Retenu | Noms de factions : noms de travail |
| H13 | Refuge, compagnons, haut niveau | Refuge qui reflète le parcours, PNJ qui s'y installent ; plusieurs ambitions de haut niveau (combat, exploration, mystères, artisanat, commerce, héritage, contribution) ; le joueur n'a pas à poursuivre toutes les carrières pour avoir un personnage efficace | Retenu | Compagnons travaillés **possibles** (« peuvent participer ») : leur place dans l'équipe et les combats est à définir |
| H14 | Direction artistique, deux vues | Fantasy stylisée (Damien, 31 octobre 2026 : remplacée par « entre ESO et Fable », voir le sujet 49 de la [feuille de route](FEUILLE_DE_ROUTE.md)) ; vues 1 et 2 disponibles dans la même expérience ; démonstration jouable demandée avant de décliner lieux, animations et effets | Retenu | Fonctionnement commun proposé (Recommandation) : une caméra réglable entre les deux positions, rappel rapide, réglage conservé, **aucune bascule automatique** (entrée en combat, classe, boss), molette et rappel sur PC, geste et bouton sur mobile, visibilité adaptée sans voir à travers les parois ni révéler les secrets. Amplitudes, rotation, projection exacte : Ouvert |

## 5. Décisions du 20 octobre 2026 (L11-D1 à L11-D12)

Toutes **Retenu** : validées par Damien le 20 octobre 2026, dans les termes de [son document](cadrage/2026-10-20_reponses_l11_d1_d12.md). Les propositions de la première version du plan qui n'y figurent pas (par exemple une garde à la moitié de la vitesse) ne sont pas validées.

| Décision | Ce qui est décidé |
|---|---|
| **L11-D1. Ordre** | Positions réelles d'abord : lot 11 combat dans l'espace, lot 12 les Vallons, lot 13 monde partagé, lot 14 interface mobile complète. Trois ajustements : (1) l'architecture de référence (D8) est fixée **avant** le moteur des positions ; (2) le lot 11 contient une **première démonstration réseau minimale** compatible avec elle, le lot 13 gardant la région partagée complète et les comptes de test ; (3) un **premier essai tactile** a lieu pendant le lot 11, le lot 14 gardant l'interface mobile complète. Estimations à refaire après décomposition. **Le 11.1 est autorisé** : la mort est un état de la simulation (arrêt des actions de l'ennemi, cibles invalidées quand il le faut, événement de mort cohérent), pas une simple disparition à l'écran ; transitions de cycle de vie compatibles avec de futures réapparitions et réveils renforcés, sans développer tout le système ; une même mort ne produit jamais plusieurs attributions de récompense |
| **L11-D2. Visée** | **Ciblage mixte** : cible sélectionnée pour les attaques simples dirigées ; orientation du personnage pour les cônes ; point au sol pour certaines zones. Le moteur valide les conditions spatiales au moment défini par la compétence (portée, surface ou étage, cible valide, obstacles pertinents) : une cible sélectionnée ne garantit pas de toucher à n'importe quelle distance ni à travers un mur. Orientation et surface d'une attaque correspondent à son annonce ; le moment où direction ou cible se verrouillent est précisé. La caméra ne change rien |
| **L11-D3. Garde en mouvement** | Oui, **déplacement ralenti en garde** pour le Bastion. Le ralentissement se règle par essais, sans pourcentage fixé d'avance ; confort vérifié dans les deux vues, avec le temps nécessaire pour quitter une zone dangereuse. Orientation, coûts et interactions avec l'endurance, l'attaque et la parade restent cohérents avec les règles existantes. La **couverture directionnelle du bouclier** est décrite explicitement, sans protection à 360° implicite |
| **L11-D4. Esquive** | **Pas de roulade universelle** au premier lot. L'esquive réelle se fait par le déplacement hors d'une zone ou d'une trajectoire ; annonces et temps de sortie des attaques ordinaires permettent d'y répondre avec le déplacement de base. Dashs, bonds et autres mouvements pourront ensuite servir l'identité des classes et des livres ; aucun livre ne doit devenir obligatoire pour obtenir l'unique mouvement qui évite les attaques ordinaires |
| **L11-D5. Collisions** | Pas de règle générale « tout le monde se pousse ». Entre joueurs : ni blocage ni poussée passive (personne ne pousse un autre dans une zone dangereuse ni ne lui ferme un passage). Entre créatures : séparation douce ou évitement, sans chaîne de poussées. **Joueur et créature : règle distincte et explicite** (passage, volumes, chevauchements), jamais laissée à une physique générique ; Claude la précise avant de figer cette partie du moteur, puis un essai (plusieurs ennemis, un angle, une entrée étroite) fait choisir le compromis. Décor solide. Déplacements forcés seulement par des attaques et capacités identifiables |
| **L11-D6. Hauteur et saut** | Déplacements au sol, **pentes et étages reliés** ; pas de saut générique de combat au premier lot. Une première démonstration peut n'avoir qu'un niveau. Dès qu'un pont ou plusieurs étages existent, la même position sur la carte ne veut dire ni même surface ni portée. Connexions praticables et obstacles pertinents pour les coups et les zones à définir ; traversées d'exploration et mobilité propre à certaines classes restent possibles |
| **L11-D7. Défenses par attaque** | La forme d'une attaque et sa défense autorisée sont **deux informations distinctes**. Chaque attaque déclare : annonce, origine ou orientation si pertinente, conditions d'évitement, garde possible ou non, parade parfaite possible ou non. Convention de la démonstration : coups directs → garde et parade parfaite selon les règles existantes ; souffle ou onde annoncée → garde seulement si la compétence le prévoit, pas de parade parfaite par défaut ; zone persistante (poison, sol brûlant) → en sortir, la garde seule ne neutralise pas l'exposition ; attaque imparable → retour visuel explicite et réponse par le déplacement. Pas d'interdiction universelle liée au seul mot « zone » ; toutes ces attaques ne sont pas à produire au lot 11 |
| **L11-D8. Architecture** | **Simulation de zone sous autorité du serveur**, principes fixés avant le moteur des positions. Le serveur détient l'état de référence (positions, attaques, dégâts, morts, puis butin, XP, inventaires) ; le client transmet les intentions, affiche, et peut anticiper présentations ou mouvements avec correction. Le noyau déterministe reste utile, mais **l'entrée dans une zone part d'un état courant suivi de mises à jour** : ni rejeu de tout le passé, ni place réservée d'avance. Une zone est une unité logique de simulation, pas une machine louée ; éprouvée localement, indépendamment du choix d'un hébergeur. **Démonstration minimale avant la production des Vallons** : serveur local et deux clients dans un petit espace ; déplacements et attaque spatiale résolus par le serveur ; arrivée du deuxième joueur pendant un affrontement ; mort d'un ennemi cohérente ; reconnexion à l'état courant sans réattribuer d'événements déjà consommés ; essai avec latence artificielle. Ce qui est réutilisé du lot 10 et ce qui change est explicité ([plan, section 2](PLAN_LOT_11.md#2-acquis-réutilisables-et-ce-qui-change)) |
| **L11-D9. Groupes** | Option B (section 2) |
| **L11-D10. Tranche** | Recommandations de la tranche retenues (Q2, Q3, Q4, Q5, Q6, Q7, Q10 tranche, Q11, Q12 tranche, Q20 : section 3). Ton : « plutôt sombre » et « fantasy stylisée » coexistent (section 2). **Soin** : A1 concernait l'arbre Gardien pendant le lot 8, ce qui n'interdit pas toute source de soin pour le Bastion ; l'effet de soin est planifié avant de présenter les remèdes comme fonctionnels. **Attributs** : voir Q20. Quêtes secrètes, deux livres, héritages, réveils renforcés et métiers avancés restent dans la vision ; leur absence d'une première démonstration correspond à une étape identifiée du plan |
| **L11-D11. Appareils** | **Samsung Galaxy Note8** et **iPhone 13**. Versions d'Android et d'iOS installées à relever (et référence exacte du Note8 en cas d'incompatibilité), sans les déduire des dernières versions disponibles. Note8 : d'abord installation et démarrage, puis commandes tactiles, lisibilité, deux vues, réglages graphiques allégés, sans le tenir pour représentatif de tous les Android. iPhone 13 : appareil de référence des premiers essais iOS. Accès à un Mac ou à une chaîne de compilation iOS à vérifier ; un essai Android ne valide pas iOS. Le simulateur d'appareils de l'éditeur aide pour les dispositions, mais un essai sur appareil réel est nécessaire. Le matériel minimal pris en charge n'est pas fixé par ces appareils |
| **L11-D12. Budget, horizon, estimation** | Budget et horizon non fixés ; aucun achat décidé. Les 6 à 13 semaines étaient la somme des durées indicatives des lots 11 à 14, ni une estimation indépendante ni une date. Les hypothèses de disponibilité (essais sous 24 heures) sont à confirmer. Réévaluation après la première démonstration du combat dans l'espace en réseau et les premiers essais tactiles ; tant que positions et zone partagée ne sont pas éprouvées, le délai de l'alpha n'est pas attribué surtout au contenu et aux graphismes. Périmètre de l'alpha à définir : testeurs, boucle jouable, persistance, plateformes, contenu, critères de stabilité |

## 5 bis. Choix de l'essai du 22 octobre 2026 (sous-lot 11.9)

**Retenu** : choix de Damien dans la fiche d'essai du lot 11 ([ESSAI_LOT_11.md](ESSAI_LOT_11.md), rapport à la section 7).

| Point | Ce qui est décidé |
|---|---|
| Contact joueur–créature (L11-D5) | **Règle A** : volumes solides sans poussée, avec le garde-fou contre le piège |
| Ralentissement en garde (L11-D3) | **la moitié de la vitesse** (`combat.movement.guardSpeedFactor` 0,5) |
| Vues 1 et 2 (H14) | angles et distances gardés (vue 1 : 52° à 17 m ; vue 2 : 16° à 6 m) ; en vue 2, le Bastion recule en regardant devant lui |
| Rivière des Vallons | on voit par-dessus : « obstacle bas », qui arrête la marche mais pas la vue |
| Garde tournée vers la cible | instantanée |
| Provocation et zones visées | gardé : la provocation détourne aussi les zones visées |
| Jugement du colosse | gardé : pas de Résonance si aucun ennemi à moins de 5 m n'est blessé |
| Contrôle pendant une annonce | l'étourdissement annule l'attaque annoncée |
| Arrêt pendant l'annonce, meute | « ça dépend des attaques et du type d'ennemi » : réglables par attaque et par ennemi |
| Charge | elle repousse le joueur |
| Retour au calme | une laisse au lot 12 : un ennemi assez distancé lâche |
| Cible perdue de vue | remplacée après un court délai (une seconde) |
| Esquive en réseau | reportée (reprise plus tard) |

## 6. Chiffres provisoires et paramètres

| Valeur | Où | Statut | Périmètre |
|---|---|---|---|
| Niveau maximal 30, 1 point de talent par niveau dès le niveau 2 (29 points) | `rules.json`, `progression` | Configuration actuelle ; plafond final Ouvert (Q18) | Moteur actuel |
| Talent majeur : 18 points investis dans son arbre, puis 1 point ; un seul par build | `rules.json`, `majorTalents` | Validé pour la configuration actuelle ; à relire avec livres et héritages (H6, H7) | Moteur actuel |
| Actives 2, 3, 4 aux niveaux 1, 5, 10 ; ultime au niveau 10 ; passifs 1, 2, 3, 4 aux niveaux 1, 5, 20, 30 (remplacé au sous-lot V1.8 par la décision de Damien : 4 actives et l'ultime au niveau 1, 5e au 10, 6e au 20) | `rules.json` | Validé pour la configuration actuelle | Moteur actuel |
| Niveaux du premier parcours : 1 à 10 | à créer | Retenu (Q3, L11-D10) | Tranche |
| Inventaire : 24 places | à créer | Retenu (Q5, L11-D10) | Tranche |
| Emplacements d'équipement : 4 (tranche), 12 (final) | à créer | Tranche : Retenu (L11-D10) ; final : Recommandation (Q4) | Tranche, Final |
| Raretés : 6 rangs, Secret à part | à créer | Retenu (Q30) | Final |
| Livres actifs : 2 par configuration | à créer | Retenu (H6) | Final |
| Héritage actif : 1 | à créer | Recommandation (H7) | Final |
| Groupes de 2 à 6 joueurs ; raids de 10 à 20 | à créer | Retenu (Q31, Damien, 29 octobre 2026 ; remplace la recommandation de 5) | Final |
| Réveil renforcé : 1 par expédition | à créer | Recommandation (H4) | Final |
| Ralentissement en garde : moitié de la vitesse | `rules.json`, `combat.movement.guardSpeedFactor` (0,5) | Retenu (L11-D3, essai du 22 octobre 2026) | Tranche |
| Combattants par combat : 128 (borne 256 ; 64 jusqu'au 29 octobre 2026) | `rules.json`, `combat.limits.maxCombatants` | Paramètre technique | Moteur actuel |
| Avance de la télégraphie en réseau : plafond 1 s | `NetworkClientOptions` | Paramètre (lot 10.6) | Réseau actuel |

## 7. Écarts avec l'existant

Relevés le 19 octobre 2026 en confrontant le cadrage aux documents et au code, mis à jour le 20 octobre après les décisions L11-D1 à L11-D12. Chaque écart indique sa source et son traitement.

| N° | Constat (source) | Cadrage | Traitement |
|---|---|---|---|
| **E1** | Feuille de route, Vision : « principalement jouable en solo (**aucun groupe imposé pour la moindre activité**) » | Q14 et Q23 : des **contenus et expéditions collectifs dédiés** peuvent exister ; les voies essentielles de progression restent accessibles en solo | **Résolu le 20 octobre (L11-D9)** : la formule « aucune voie essentielle n'exige un groupe » remplace l'ancienne ; Vision de la feuille de route mise à jour |
| **E2** | Feuille de route, Vision : « fantasy stylisée **plutôt sombre** (base créative, pas une charte) » ; « Caméra : **non tranchée** » (déjà complétée le 18 octobre : les deux caméras du prototype gardées au choix) | Q39 et H14 : fantasy stylisée et vues 1 et 2 retenues ; « ces choix remplacent les précédentes mentions d'un style ou d'un type de caméra encore indécis ». La déclinaison (couleurs expressives, Vallons accueillants…) est seulement **proposée** | **Caméra : appliqué.** La caméra « isométrique » du prototype (perspective plongeante à 50°, pas une projection orthographique) est la base de la vue 1, la troisième personne celle de la vue 2. **Ton : résolu le 20 octobre (L11-D10)** : « plutôt sombre » décrit une ambiance, « fantasy stylisée » une direction graphique ; ils coexistent |
| **E3** | `docs/MOTEUR.md`, « Ciblage (A7) » (A7 du plan du simulateur v0.1, à ne pas confondre avec A7 du lot 8) : « logique uniquement : pas de position, de distance ni de collision » ; `SkillCombatDefinition.cs` ; feuille de route : « aucun système de positions avant la fin du lot 8 ». Dans Unity, le déplacement n'est qu'affiché : le moteur ignore où sont les combattants | Q13 : positions réelles qui ont des conséquences | A7 du moteur reste vrai pour le moteur 0.14.0 ; Q13 le remplace comme direction. **Changement de moteur au lot 11** : nouveaux formats de journal, de scénario et de classe, nouveaux journaux de référence |
| **E4** | Feuille de route, **A1 du lot 8** : « pas de soin **pour le Gardien au lot 8** » et « aucun soin… avant la fin du lot 8 ». Le [questionnaire](cadrage/2026-10-19_questionnaire_q01_q40.md) (Q7 : « ta règle actuelle dit « pas de soin pour le Bastion » ») et la première version du plan (Q6) résumaient trop largement. Le moteur n'a aucun effet de soin | Q7 : potions limitées ; le Bastion sans soin propre peut recevoir du soin | **Pas de contradiction ; portée consignée le 20 octobre (L11-D10).** Les potions (ou la classe de soutien, Q15) demandent une catégorie d'effet « soin » dans le moteur, planifiée avant les remèdes du lot 12 |
| **E5** | Première version du plan : « le monde **fabrique un combat** (`CombatSetup`) par rencontre et lit le résultat ». `docs/RESEAU.md` : salle de combat à composition fixe, une place par personnage du scénario, sans entrée dans un combat commencé (section « Serveur (sous-lot 10.2) ») ; **chaque client rejoue tout le combat** à partir des commandes du serveur (section « Principe ») | Q9 (région partagée), Q11 (combat sur place), Q13 (positions), Q34 (boss de monde), Q8 (réinitialisation quand tous ont quitté la zone) : des joueurs rejoignent un combat en cours ; une région contient beaucoup de joueurs et de créatures | **Résolu le 20 octobre (L11-D8)** : simulation de zone sous autorité du serveur, entrée depuis l'état courant. Le modèle du lot 10 reste valable pour les combats fermés (instances, duels, essais) |
| **E6** | `CharacterBuild.Attributes` : les attributs font partie du build ; les builds livrés ont ceux du niveau 30 quel que soit leur niveau ; feuille de route : « **budget d'attributs** : chantier ultérieur » (qui suggérait des points à répartir) | Q20 : base fixée par la classe et le niveau, modifiée par l'équipement et le build, sans répartition manuelle (depuis le 30 octobre 2026 : 2 points à placer par niveau, voir Q20 et le sujet 34 de la [feuille de route](FEUILLE_DE_ROUTE.md)) | **Retenu le 20 octobre (L11-D10)** : table d'attributs par classe et niveau pour le personnage persistant (lot 12) ; les builds et scénarios d'essai peuvent garder des valeurs explicites, versionnées, reproductibles |
| **E7** | Feuille de route, « Progression validée (ne change pas pendant le lot 8) » : 30 niveaux, 29 points, majeur à 18 + 1 | Q18 : 30 n'est pas un plafond final ; H6 et H7 : livres, héritages, maîtrises, budget de points à repenser | **Pas de contradiction** : la configuration actuelle reste en vigueur jusqu'à la refonte de la progression (chantier de l'alpha). Erreur de la première version du plan corrigée : au niveau 10, le majeur (niveau 20) et les 3ᵉ et 4ᵉ passifs (niveaux 20 et 30) manquent encore |
| **E8** | `DONNEES.md` : équipement « réservé, non implémenté » ; L8-D6 : déclencheurs d'équipement reportés au système d'objets ; `FORMULES.md` et `StatContributions` : trois puissances d'équipement déjà prévues dans les formules. Le bouclier du Bastion est implicite (garde, blocage, Heurt de bouclier, Bouclier lourd), et sa garde couvre aujourd'hui toutes les directions faute de positions | Q4 : effets spéciaux, bouclier réel ; L11-D3 : couverture directionnelle du bouclier explicite | **Pas de contradiction** : le chantier des objets réalise L8-D6 ; la couverture du bouclier est proposée au 11.3. Question pour plus tard : un Bastion **sans bouclier** (arme à deux mains) est-il possible ? |
| **E9** | Feuille de route, « Après le lot 8 », point 4 : progression et équipement, puis monde, métiers, économie, classes, multijoueur étendu, persistance, interfaces PC/mobile, direction artistique, alpha. La première version du plan gardait des positions seulement visuelles | Q13 (positions tôt), Q35 (validation tactile tôt), H14 (démonstration des deux vues avant la production) | **Résolu le 20 octobre (L11-D1)** : nouvel ordre, détaillé dans le [plan](PLAN_LOT_11.md#3-ordre-des-chantiers-validé-le-20-octobre-2026) |
| **E10** | Les textes du jeu sont en français, écrits dans le code d'Unity (`Hud.cs`, `NetworkScreen.cs`…) et dans les noms des données | Q37 : français et anglais | **Pas de contradiction.** Textes du monde rangés par clés dès le lot 12 ; l'interface du prototype migrera avec la nouvelle interface (lot 14) |
| **E11** | Première version du plan : questions Q1 à Q10, numérotées autrement (Q7 défaite, Q8 deuxième joueur…) | Q1 à Q40 du questionnaire | **Appliqué** : numérotation Q1 à Q40 partout |
| **E12** | `CLAUDE.md` : « Après le lot 8 viennent le prototype Unity 3D, puis un serveur autoritaire » ; liste de lecture sans ce registre | — | **Appliqué** : `CLAUDE.md` et `AGENTS.md` renvoient à ce registre |

## 8. Points ouverts

**Informations à fournir par Damien**

- Budget : enveloppe ponctuelle pour ressources et outils ; plafond mensuel pour les services de test. Aucune dépense n'est autorisée tant que rien n'est fixé.
- Horizon (Q40) : date souhaitée pour les premiers testeurs, ou jalons, avec une disponibilité approximative ; confirmation des hypothèses de disponibilité (essais sous 24 heures).
- Téléphones : version d'iOS (iPhone 13) installée ; accès à un Mac ou à une chaîne de compilation iOS. (Note8 : Android 9, relevé le 22 octobre 2026.)

**À proposer par Claude, puis à valider par un essai ou par Damien**

- Esquive en réseau : durée des annonces face à la latence, rattrapage côté serveur, essai à deux postes (reporté à la demande de Damien, essai du 22 octobre 2026).

Retenus le 22 octobre 2026 (essai du 11.9) : le contact joueur–créature (règle A) et le ralentissement en garde (la moitié de la vitesse), [section 5 bis](#5-bis-choix-de-lessai-du-22-octobre-2026-sous-lot-119). Retenus le 20 octobre 2026 à 01 h 48 : la couverture du bouclier (180° de devant), l'orientation en garde vers la cible, le verrouillage au lancement, les portées du Bastion ([COMBAT_ESPACE.md](COMBAT_ESPACE.md#10-retenu-par-damien-le-20-octobre-2026)) et les quatre principes de l'architecture ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md#9-retenu-par-damien-le-20-octobre-2026)).

**Arbitrages de gameplay**

- Plafond final et rythme de progression (Q18) : concevoir les paliers avant le chiffre.
- Métiers : maîtrise de tous les métiers ou expertises avancées exclusives (Q27).
- Nombre de classes et de régions à chaque jalon (Q16, Q22) ; taille du groupe standard (Q31) ; périmètre de l'alpha (L11-D12).
- Emplacements et capacité d'inventaire finaux (Q4, Q5).
- Points des livres et de l'héritage dans les seuils des arbres (H6, H7) ; changement d'héritage, portée des serments (Q21).
- Portée des marchés, transport des biens (Q28, H10).
- Caméra : transition, amplitudes, rotation, commandes PC et mobile, obstacles (H14).
- Bastion sans bouclier (E8).
- Nom du réveil renforcé : « Érance » est provisoire (le mot courant s'écrit « errance »).
- Modèle commercial et conditions de lancement (Q36).
- Statut final de Q10 (comptes sous autorité serveur) et Q12 (tous les éléments de terrain), écrits « Jeu final » sans le mot « retenu ».
