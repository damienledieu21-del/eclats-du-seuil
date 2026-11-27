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
| Q25 | Logement | **Refuge personnel évolutif** (camp, maison, atelier, domaine) ; maison du Bourg achetée puis agrandie (Damien, 20 novembre 2026, § 7 quaterdecies) | Retenu | « Plus tard » est un ordre de réalisation |
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
| **L11-D4. Esquive** | **Pas de roulade universelle** au premier lot. L'esquive réelle se fait par le déplacement hors d'une zone ou d'une trajectoire ; annonces et temps de sortie des attaques ordinaires permettent d'y répondre avec le déplacement de base. Dashs, bonds et autres mouvements pourront ensuite servir l'identité des classes et des livres ; aucun livre ne doit devenir obligatoire pour obtenir l'unique mouvement qui évite les attaques ordinaires ; **remplacé le 14 novembre 2026** par une vraie esquive, voir la décision ci-dessous |
| **L11-D5. Collisions** | Pas de règle générale « tout le monde se pousse ». Entre joueurs : ni blocage ni poussée passive (personne ne pousse un autre dans une zone dangereuse ni ne lui ferme un passage). Entre créatures : séparation douce ou évitement, sans chaîne de poussées. **Joueur et créature : règle distincte et explicite** (passage, volumes, chevauchements), jamais laissée à une physique générique ; Claude la précise avant de figer cette partie du moteur, puis un essai (plusieurs ennemis, un angle, une entrée étroite) fait choisir le compromis. Décor solide. Déplacements forcés seulement par des attaques et capacités identifiables |
| **L11-D6. Hauteur et saut** | Déplacements au sol, **pentes et étages reliés** ; pas de saut générique de combat au premier lot. Une première démonstration peut n'avoir qu'un niveau. Dès qu'un pont ou plusieurs étages existent, la même position sur la carte ne veut dire ni même surface ni portée. Connexions praticables et obstacles pertinents pour les coups et les zones à définir ; traversées d'exploration et mobilité propre à certaines classes restent possibles |
| **L11-D7. Défenses par attaque** | La forme d'une attaque et sa défense autorisée sont **deux informations distinctes**. Chaque attaque déclare : annonce, origine ou orientation si pertinente, conditions d'évitement, garde possible ou non, parade parfaite possible ou non. Convention de la démonstration : coups directs → garde et parade parfaite selon les règles existantes ; souffle ou onde annoncée → garde seulement si la compétence le prévoit, pas de parade parfaite par défaut ; zone persistante (poison, sol brûlant) → en sortir, la garde seule ne neutralise pas l'exposition ; attaque imparable → retour visuel explicite et réponse par le déplacement. Pas d'interdiction universelle liée au seul mot « zone » ; toutes ces attaques ne sont pas à produire au lot 11 |
| **L11-D8. Architecture** | **Simulation de zone sous autorité du serveur**, principes fixés avant le moteur des positions. Le serveur détient l'état de référence (positions, attaques, dégâts, morts, puis butin, XP, inventaires) ; le client transmet les intentions, affiche, et peut anticiper présentations ou mouvements avec correction. Le noyau déterministe reste utile, mais **l'entrée dans une zone part d'un état courant suivi de mises à jour** : ni rejeu de tout le passé, ni place réservée d'avance. Une zone est une unité logique de simulation, pas une machine louée ; éprouvée localement, indépendamment du choix d'un hébergeur. **Démonstration minimale avant la production des Vallons** : serveur local et deux clients dans un petit espace ; déplacements et attaque spatiale résolus par le serveur ; arrivée du deuxième joueur pendant un affrontement ; mort d'un ennemi cohérente ; reconnexion à l'état courant sans réattribuer d'événements déjà consommés ; essai avec latence artificielle. Ce qui est réutilisé du lot 10 et ce qui change est explicité ([plan, section 2](PLAN_LOT_11.md#2-acquis-réutilisables-et-ce-qui-change)) |
| **L11-D9. Groupes** | Option B (section 2) |
| **L11-D10. Tranche** | Recommandations de la tranche retenues (Q2, Q3, Q4, Q5, Q6, Q7, Q10 tranche, Q11, Q12 tranche, Q20 : section 3). Ton : « plutôt sombre » et « fantasy stylisée » coexistent (section 2). **Soin** : A1 concernait l'arbre Gardien pendant le lot 8, ce qui n'interdit pas toute source de soin pour le Bastion ; l'effet de soin est planifié avant de présenter les remèdes comme fonctionnels. **Attributs** : voir Q20. Quêtes secrètes, deux livres, héritages, réveils renforcés et métiers avancés restent dans la vision ; leur absence d'une première démonstration correspond à une étape identifiée du plan |
| **L11-D11. Appareils** | **Samsung Galaxy Note8** et **iPhone 13**. Versions d'Android et d'iOS installées à relever (et référence exacte du Note8 en cas d'incompatibilité), sans les déduire des dernières versions disponibles. Note8 : d'abord installation et démarrage, puis commandes tactiles, lisibilité, deux vues, réglages graphiques allégés, sans le tenir pour représentatif de tous les Android. iPhone 13 : appareil de référence des premiers essais iOS. Accès à un Mac ou à une chaîne de compilation iOS à vérifier ; un essai Android ne valide pas iOS. Le simulateur d'appareils de l'éditeur aide pour les dispositions, mais un essai sur appareil réel est nécessaire. Le matériel minimal pris en charge n'est pas fixé par ces appareils |
| **L11-D12. Budget, horizon, estimation** | Budget et horizon non fixés ; aucun achat décidé. Les 6 à 13 semaines étaient la somme des durées indicatives des lots 11 à 14, ni une estimation indépendante ni une date. Les hypothèses de disponibilité (essais sous 24 heures) sont à confirmer. Réévaluation après la première démonstration du combat dans l'espace en réseau et les premiers essais tactiles ; tant que positions et zone partagée ne sont pas éprouvées, le délai de l'alpha n'est pas attribué surtout au contenu et aux graphismes. Périmètre de l'alpha à définir : testeurs, boucle jouable, persistance, plateformes, contenu, critères de stabilité |

**Décision de Damien du 14 novembre 2026 (21 h 03 et 21 h 05), qui revient sur L11-D4 — Retenu.** Une **vraie esquive**, choix « AAAA » :

- **a. Mouvement** : un bond rapide d'environ 3 m dans la direction du joystick, ou vers l'arrière si le joystick est au repos (pas de roulade).
- **b. Invincibilité** : une courte fenêtre au début du bond, environ 0,3 s (6 ticks).
- **c. Coût** : de l'endurance, plus une petite recharge d'environ 1 s, pour qu'on ne puisse pas l'enchaîner sans fin.
- **d. Lien avec la parade** : la parade et l'esquive restent deux choix différents. La parade récompense le timing ; l'esquive sert à se replacer, et elle évite aussi un coup imparable.

Valeurs dans les règles (`combat.dodge`) : 3 m, 300 ms d'invulnérabilité, 20 d'endurance, 1 s de recharge ; durée du bond 150 ms (choix technique de Claude, « un bond rapide »). Les créatures n'esquivent pas (aucune donnée ne le prévoit). Réalisation : moteur 0.32.0, [MOTEUR.md](MOTEUR.md#esquive-décision-de-damien-du-14-novembre-2026), [COMBAT_ESPACE.md](COMBAT_ESPACE.md#2-déplacement). La règle de conception de L11-D4 (une attaque ordinaire évitable laisse le temps d'en sortir au pas de base) reste vérifiée par le validateur : l'esquive s'ajoute à la marche, elle ne la remplace pas.

**Réponses de Damien du 14 novembre 2026 (vers 23 h 50), conformes aux recommandations — Retenu** :

1. **Esquive en garde** : on quitte la garde, on bondit, puis la garde reprend si le bouton est tenu (le moteur refuse `guardActive` ; l'interface quitte la garde dans le même groupe).
2. **Incantations longues futures** : l'esquive les interrompt (à réaliser avec la première incantation longue ; rien à coder aujourd'hui, les compétences du Bastion sont instantanées).
3. **Altération déjà posée** (poison, saignement) : elle continue pendant l'invulnérabilité.
4. **Touche PC** : Ctrl gauche.

**Décision de Damien du 14 novembre 2026 (vers 23 h 58) — Retenu** : un coup esquivé ne donne aucune Résonance à l'attaquant.

Les valeurs (bond de 150 ms, 20 d'endurance) restent à confirmer à l'essai.

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

## 7 bis. Compagnons et boss du monde (Retenu)

Décisions de Damien rapportées par la session principale ; elles précisent le sujet 11 « Compagnons » de la [feuille de route](FEUILLE_DE_ROUTE.md) (en solo, un compagnon, puis deux plus tard dans l'histoire).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Deuxième place de compagnon | Elle s'ouvre **par l'histoire**, à la **fin du chapitre 1 des Vallons**, après le Grand Solitaire ou le chef des bandits, par une **quête de lien au feu de camp** : le premier compagnon présente quelqu'un, ou ramène Cendre assez grand. Un **niveau minimum doux, d'environ 12**, évite qu'elle s'ouvre trop tôt (valeur exacte à équilibrer). | Retenu (Damien, 14 novembre 2026, 23 h 43 : « Ok on fait ça », sur la recommandation de Claude) |
| Compagnons pendant le Grand Solitaire (boss du monde, de 5 à 100 joueurs) | Chaque joueur garde **un seul compagnon au combat** ; le second **attend au point de repli** de l'événement. La proposition « tous au repli » n'est pas retenue. Source : dossier ChatGPT « Grand Solitaire et compagnons », p. 34. | Retenu (Damien, 14 novembre 2026, 23 h 45) |
| Attaque à deux et jauge de lien | Les coups de l'attaque à deux **ne rechargent pas** la jauge de lien (elle est dépensée au départ et ne se remplit que par les actions coordonnées ordinaires). | Retenu (Damien, 15 novembre 2026, vers 9 h 45) |
| Mercenaire renvoyé puis réembauché | Il **garde son estime et son trait** ; seule sa **jauge de lien revient à 0**. | Retenu (Damien, 15 novembre 2026, vers 9 h 45) |
| Ronce, l'ours | Ramené au niveau des autres compagnons de son rôle : **gros protecteur, mais pas supérieur à tout** (valeurs exactes à équilibrer à l'essai). | Retenu (Damien, 15 novembre 2026, vers 9 h 45) |

## 7 ter. Saisons (Retenu)

Décisions de Damien du 15 novembre 2026, 2 h 27, rapportées par la session principale, sur proposition du dossier ChatGPT « Quatre saisons » (AssetLab, `Saisons/`). Elles précisent le sujet 12 « Fêtes et saisons » de la [feuille de route](FEUILLE_DE_ROUTE.md) (saisons du monde comme dans la vraie vie).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Dates des saisons | Les saisons changent le **1er mars, le 1er juin, le 1er septembre et le 1er décembre**, à l'heure de Paris (saisons météorologiques, hémisphère Nord), avec une **transition visuelle de 7 jours**. Le **serveur fait foi**. | Retenu (Damien, 15 novembre 2026, 2 h 27) |
| Glace du lac | Praticable **du 8 décembre à la fin février inclus**, après une première semaine où on la voit se former sans pouvoir y marcher. | Retenu (Damien, 15 novembre 2026, 2 h 27) |
| Fonte du lac gelé | L'accès à la glace **se ferme un peu avant la fonte** ; les joueurs encore dessus sont **ramenés sur la berge sans rien perdre**. À coder dès que le passage existe sur la carte ; pour l'instant, la règle est consignée. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Récolte de saison | On garde la **repousse rapide** des récoltes de saison : **10 min** au printemps (primevère argentée, sauge de pluie), **2 min** en automne (cèpes, trompettes). | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Échos dans la brume | On garde le taux : **40 % en automne, 15 % sinon, 2 Échos au plus** par volume de brume. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Prix de saison | Les prix de saison **suivent la vraie saison** (date fournie par l'hôte ou le serveur). | Retenu (Damien, 15 novembre 2026, 9 h 45) |

### Saisons comme dans la vraie vie, V2.44 (décisions de Damien du 20 novembre 2026)

Rapportées par la session principale. Elles précisent le sujet 12 « Fêtes et saisons » et le chantier V2.44 ([PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md), [MONDE.md](MONDE.md#saisons-comme-dans-la-vraie-vie-v244-décisions-de-damien-du-20-novembre-2026)).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Neige et déplacement | La neige **ralentit le déplacement hors des chemins** ; sur les chemins, aucun effet. **Hors combat seulement**. **−10 %, à cheval comme à pied** (réponse de Damien du 20 novembre 2026, PR #229). Jamais dans un intérieur. | Retenu (Damien, 20 novembre 2026) |
| Dates de la neige et de la vigne | Neige en plaine du **15 décembre au 28 février**, au col du **1er novembre au 15 avril** ; vigne verte au **1er avril**, mûre au **1er septembre**, vendangée au **1er octobre** (réponse de Damien du 20 novembre 2026, PR #229). | Retenu (Damien, 20 novembre 2026) |
| Plafond et loups de l'hiver | On garde **128 combattants** par session, avec **2 loups de l'hiver** (réponse de Damien du 20 novembre 2026, PR #229). | Retenu (Damien, 20 novembre 2026) |
| Prix du raisin | **15 cuivres** chez les marchands (réponse de Damien du 20 novembre 2026, PR #229). | Retenu (Damien, 20 novembre 2026) |
| Neige du col à l'image | La neige du col s'affiche **à partir de 220 m** (réponse de Damien du 20 novembre 2026, PR #232). | Retenu (Damien, 20 novembre 2026) |
| Glace à l'image | **Seul le lac gèle** : la glace ne s'affiche que là où elle est praticable (la bande du lac gelé) ; les rivières restent libres (réponse de Damien du 20 novembre 2026, PR #232). | Retenu (Damien, 20 novembre 2026) |
| Vendanges | Une **récolte de saison** : on cueille du **raisin en septembre**, avec une **repousse comme les cèpes**. Un événement au château viendra plus tard. | Retenu (Damien, 20 novembre 2026) |
| Créatures de saison | La saison change les créatures, **avec le bestiaire existant** : les ours hibernent, plus de loups l'hiver, etc. | Retenu (Damien, 20 novembre 2026) |
| Toits enneigés | Par **shader**, sur **tous les bâtiments**, sans aucun crédit dépensé. | Retenu (Damien, 20 novembre 2026) |
| Date en multijoueur | Le **serveur fait foi** (déjà retenu le 15 novembre) : depuis le protocole 6 (V2.44.1), il envoie sa date à chaque joueur, qui n'utilise plus l'horloge de son appareil en partie en réseau. | Retenu (Damien, 15 novembre 2026), appliqué le 20 novembre 2026 |

## 7 undecies. Lot C : intérieurs de métier et forge de Maëlle (Retenu)

Décisions de Damien du 16 novembre 2026, vers 21 h 40, rapportées par la session principale (modèles Tripo du lot C, [UNITY.md](UNITY.md#lot-c--modèles-tripo-16-novembre-2026)).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Intérieurs de métier | Créer les intérieurs de la **forge**, de la **grange**, du **moulin**, de la **tour de guet**, d'une **maison paysanne**, et la **cuisine** et les **chambres** de l'auberge, meublés avec les modèles du lot C. | Retenu (Damien, 16 novembre 2026, vers 21 h 40) ; réalisé dans la PR empilée sur #201 ([MONDE.md](MONDE.md#intérieurs-de-métier-lot-c-décisions-du-16-novembre-2026)) |
| Forge de Maëlle | **Toutes les pièces de forge** du lot C (soufflet, bac de trempe, meule, râtelier d'armes, présentoir de fers, tas de charbon, établi, outils) autour de la forge **en plein air**, sans bloquer le passage. | Retenu (Damien, 16 novembre 2026, vers 21 h 40) ; même PR |
| Plantes et filons | Plantes à récolter **grossies 2 fois** et **trois tailles de filons** : gardées. | Retenu (Damien, 16 novembre 2026, vers 21 h 40) ; PR #196 |
| Meubles des nouveaux lieux (I01 à I21) | La PR #202 **reste en brouillon** ; chaque meuble sera placé quand son lieu entrera dans les données. | Retenu (Damien, 16 novembre 2026, vers 21 h 40)  **Option A retenue (Damien, 21 novembre 2026)** : les 12 meubles posés dans les intérieurs des lieux reconstruits (V2.56.2) sont gardés ; les 10 autres attendent leur lieu. |
| Porte de la forge couverte | Sur la **maison de Maëlle** (`maison3`) : le hameau a désormais cette porte (seule exception à « le hameau reste sans porte »). | Retenu (Damien, 17 novembre 2026, vers 0 h 10) |
| Maison paysanne | La **maison d'Achille** (`maison9`, champs du sud). | Retenu (Damien, 17 novembre 2026, vers 0 h 10) |
| Habitants des intérieurs de métier | Des habitants **vivent dans ces pièces** : emplois du temps adaptés en gardant leurs horaires (Maëlle à sa forge couverte et dans la cour, la meunière au moulin, Achille chez lui ; l'aubergiste et le personnel en cuisine, les voyageurs de passage dans les chambres la nuit). | Retenu (Damien, 17 novembre 2026, vers 0 h 10) ; réalisé pour Maëlle (forge couverte 16 h à 19 h 30), Odile (salle du moulin l'après-midi) et Achille (repas de midi chez lui) ; le matin et la nuit restent ceux d'avant (règles de l'histoire : place du début du jeu le matin, devant sa porte la nuit). Cuisine et chambres : impossible tant qu'un PNJ ne passe qu'une porte depuis les Vallons (`StoryValidator.SlotZone`), et aucun voyageur de passage n'a d'emploi du temps : question posée dans la PR #209 |
| Auvent de la forge | L'auvent (C_OV_A01) **au-dessus de la forge en plein air** de Maëlle, sans gêner ni les pièces ni le passage. | Retenu (Damien, 17 novembre 2026, vers 0 h 10) ; PR #209 (`LotCModels.ForgeAwning`, testé) |
| Plans des intérieurs de métier | Recalés sur les **fiches de ChatGPT** (`AssetLab/Interieurs`) : dimensions des pièces et meubles aux cotes ; l'auberge suit ses trois niveaux (chambres, logement de Mathurin et dortoir, combles). | Réalisé (Claude, 17 novembre 2026) d'après les fiches ; écarts notés dans [MONDE.md](MONDE.md#intérieurs-de-métier-lot-c-décisions-du-16-novembre-2026) |
| PNJ dans une pièce derrière une pièce | La règle de l'histoire accepte une pièce reliée par porte à une pièce elle-même reliée aux Vallons : **deux portes au plus** (`StoryValidator.MaxSlotDepth`), chaque porte avec sa porte de retour, jamais par la pièce d'une scène ; refus « porte manquante » au-delà. L'affichage mène le PNJ à la première porte du chemin. | Retenu (Damien, 17 novembre 2026, vers 2 h) ; PR empilée sur #209 |
| Aubergiste en cuisine | Mathurin en cuisine aux heures du service, 11 h 30 à 12 h 30 et 17 h 30 à 18 h ; ses autres horaires ne changent pas. L'auberge n'a pas d'autre personnel dans les données. | Retenu (Damien, 17 novembre 2026, vers 2 h) ; même PR |
| Voyageurs de passage | Deux PNJ simples, sans quête : un convoyeur et une voyageuse, arrivés le soir à la taverne, la nuit dans les chambres (leur zone), repartis le matin sur la route d'Ambre. | Retenu (Damien, 17 novembre 2026, vers 2 h) ; même PR |
| Achille dort chez lui | Sa zone devient sa maison (`zone.chaumiere`) : il y dort et y prend son repas de midi ; ses journées aux champs et ses soirées à la taverne ne changent pas, ni ses quêtes. Seule exception à « le matin, chacun à sa place du début du jeu », avec les voyageurs. Aucune sauvegarde à migrer : la place d'un PNJ ne se sauvegarde pas. | Retenu (Damien, 17 novembre 2026, vers 2 h) ; même PR |
| Odile le matin | Elle **reste dehors le matin**, sans exception (ses quêtes du moulin commencent là). | Retenu (Damien, 17 novembre 2026) ; PR #212 |
| Personnel de l'auberge | Deux employés, avec prénom, horaires et quelques répliques : **Perrine, la serveuse** (en salle, en cuisine aux repas avec Mathurin, la nuit à l'auberge) et **Huguet, le garçon d'écurie** (à l'écurie devant l'auberge, son repas de midi en cuisine, la nuit dans la paille). | Retenu (Damien, 17 novembre 2026) ; PR #212 |
| Voyageurs nommés | Un prénom (**Baudouin**, convoyeur ; **Aveline**, voyageuse), une **tenue de voyage** (cape et sac, toute l'année) et une réplique sur leur route. | Retenu (Damien, 17 novembre 2026) ; PR #212 |

## 7 quinquies. L'âme du jeu (Retenu)

Décision de Damien du 15 novembre 2026, vers 9 h 50, rapportée par la session principale. Elle précise le sujet 13 « L'âme du jeu » de la [feuille de route](FEUILLE_DE_ROUTE.md).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Devise du jeu | **« Ce qui est brisé peut encore briller. »**, choisie parmi les six devises du dossier ChatGPT « L'âme du jeu ». | Retenu (Damien, 15 novembre 2026, 9 h 50) |

## 7 quater. Grande carte des Vallons (Retenu)

Décisions de Damien du 15 novembre 2026, vers 9 h 45, puis vers 17 h (monture, point de vue du sommet, Forêt des Murmures) et vers 21 h 15 (brouillard), rapportées par la session principale. Elles précisent le [plan de la grande carte](PLAN_GRANDE_CARTE.md) (§ 3 et § 4.4).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Éboulement et gués de la vallée | Ils s'ouvrent **par une quête à la fin du chapitre 1**, comme la porte du moulin. | Retenu (Damien, 15 novembre 2026, vers 9 h 45) ; quête minimale « Les passages de la vallée » au V2.28c (4) ([MONDE.md](MONDE.md)) |
| Niveaux des lieux de la région | Ils restent **sous le Gardien du Seuil** (23, fin du chapitre 5) : toute la région entre **1 et 22**, dans un ordre cohérent avec l'existant (camp des rôdeurs 16, Borgne 17). | Retenu (Damien, 15 novembre 2026, vers 9 h 45) ; appliqué dans la PR #170 ([MONDE.md](MONDE.md)) |
| Monture : coup reçu en selle | Être frappé en selle **ne fait pas descendre**. | Retenu (Damien, 15 novembre 2026, vers 17 h) ; c'était déjà la règle du moteur 0.34.0 ([MOTEUR.md](MOTEUR.md#monture-sous-lot-v228d-2)) |
| Monture : temps de montée | **Monter prend 1 seconde.** Règle d'un coup reçu pendant la montée, laissée à Claude (« la plus simple ») : **un coup ne l'annule pas** ; il faut être à l'arrêt, et marcher ou agir (garde, parade, esquive, compétence) l'annule ; au bout de la seconde, le moteur juge comme avant (ennemi à moins de 12 m : refus). | Retenu (Damien, 15 novembre 2026, vers 17 h) ; règle du coup : choix de Claude ; réalisé au V2.28d (2b) ([DONNEES.md](DONNEES.md#monture-dans-la-partie-solo-sous-lot-v228d-2b)) |
| Point de vue du sommet | À la **première visite** : **200 XP**, une **entrée au carnet**, et le **brouillard de carte dissipé** autour. | Retenu (Damien, 15 novembre 2026, vers 17 h) ; à réaliser au V2.29 (2) |
| Forêt des Murmures | **Au nord-ouest**, comme sur la carte d'Orsédia retenue (déjà aligné : pont des Murmures à l'ouest côté nord, [PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md#40-bis-les-vallons-dans-orsédia-retenu)). | Retenu (Damien, 15 novembre 2026, vers 17 h) |
| Brouillard de carte | **Sous le brouillard, tout est caché** : noms de lieux, PNJ, points sûrs, portes (et créatures) ne s'affichent que sur les cases vues ; le joueur et l'objectif de la quête suivie restent. | Retenu (Damien, 15 novembre 2026, vers 21 h 15) ; réalisé au V2.29 (2) ([DONNEES.md](DONNEES.md#brouillard-de-carte-et-point-de-vue-du-sommet-sous-lot-v229-2)) |
| Point de vue du sommet : brouillard | Monter au point de vue **lève le brouillard sur toute la région** (remplace « autour », ligne du 15 novembre 2026 ci-dessus). | Retenu (Damien, 16 novembre 2026) ; sous-lot V2.29.1 ([DONNEES.md](DONNEES.md#brouillard-de-carte-et-point-de-vue-du-sommet-sous-lot-v229-2)) |
| Monture : quête du cheval | Une **vraie petite quête** pour obtenir la monture, au lieu de l'avoir directement à la fin du chapitre 1. | Retenu (Damien, 16 novembre 2026) ; sous-lot à venir, après le sommet ([PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md#0-ter-décisions-de-damien-du-16-novembre-2026-retenu)) |
| Porte nord du Bourg | Une **porte nord**, reliée au chemin du pont des Murmures. | Retenu (Damien, 16 novembre 2026) ; réalisée au V2.40 (PR), avec la passerelle des Ronces ouverte à la fin du chapitre 1 (Damien, 18 novembre 2026) ([MONDE.md](MONDE.md)) |
| Bourg déménagé : emplacement | Le bloc au nord-ouest de la place du marché est **gardé** ; ajustement possible après le test dans Unity. | Retenu (Damien, 16 novembre 2026, vers 10 h) ; V2.30.1 |
| Panneaux du village au Bourg | Des **panneaux aux croisements** entre le village et le Bourg, dès maintenant. | Retenu (Damien, 16 novembre 2026, vers 10 h) ; réalisé au V2.30.1 ([MONDE.md](MONDE.md)) |
| Quête du cheval : détails | **Achille, Brume et les rives du lac** confirmés ; **plus tard** : ramener un vrai cheval à la longe jusqu'à la ferme (modèle 3D et animations). | Retenu (Damien, 16 novembre 2026, vers 10 h) ; la suite est à faire avec le modèle du cheval (V2.28d (2c)) |
| Grand Solitaire et Bourg déménagé | Le boss du monde attaque **devant la porte de l'Est** du Bourg fortifié et **n'entre pas dans les murs**. | Retenu (Damien, 16 novembre 2026, vers 16 h 30) ; réalisé au V2.30.1 ([MONDE.md](MONDE.md)) |
| Bourg : ouvertures des portes | Ouvertures des données **resserrées sur les passages** des modèles : est 4,2 m, ouest 6 m, poterne 3 m. | Retenu (Damien, 16 novembre 2026, vers 21 h 30) ; réalisé au V2.30.5 ([MONDE.md](MONDE.md)) |
| Bourg : fossé | **Fossé creusé**, infranchissable, autour de la palissade (obstacles, modèles F11 à F15, talus), franchi seulement par les **ponts** devant les portes. | Retenu (Damien, 16 novembre 2026, vers 21 h 30) ; réalisé au V2.30.5 |
| Bourg : données alignées sur les modèles | Clocher sur celui de l'église, écuries portées à 24 m, maison notable H09 et boutiques P13 à P18 placées (route d'Ambre). | Retenu (Damien, 16 novembre 2026, vers 21 h 30) ; réalisé au V2.30.5 |
| Bourg : façades vivantes | Fenêtres allumées la nuit et lanternes de porte sur les bâtiments habillés ; pièces murales (lanternes, auvents, linge, bannières, râtelier) à leur hauteur de pose ; kit H13-H20 s'il s'adapte. | Retenu (Damien, 16 novembre 2026, vers 21 h 30) ; réalisé au V2.30.5, sauf le kit H13-H20 (non adapté) |
| Bourg : rôle des nouveaux bâtiments | Les 7 nouveaux bâtiments (H09, boutiques P13 à P18) reçoivent un rôle : un marchand et ses PNJ chacun, métier, horaires et marchandises d'après le dossier du Bourg, sans invention. | Retenu (Damien, 16 novembre 2026, vers 23 h 20) ; réalisé au V2.30.5 pour les six boutiques (choix prudents à confirmer : le dossier ne donne que les métiers) ; H09 : un habitant sans boutique (question posée) |
| Bourg : marchands des nouvelles boutiques | Un vrai prénom, une petite personnalité et quelques répliques (salut, achat, au revoir, une ou deux rumeurs du Bourg) pour les six marchands, dans le ton des PNJ existants, sans quête nouvelle. | Retenu (Damien, 17 novembre 2026, vers 2 h 15) ; réalisé au V2.30.5 |
| Bourg : maison notable (H09) | H09 reste un habitant : une famille du Bourg avec ses horaires, sans boutique. | Retenu (Damien, 17 novembre 2026, vers 2 h 15) ; réalisé au V2.30.5 (Aymon et Ermeline Vaudry) |
| Lieux de la grande carte : créatures | Les lieux sont **peuplés dès leur sous-lot** avec le bestiaire existant (meute de la tanière, ours, etc.). | Retenu (Damien, 16 novembre 2026, vers 23 h) ; V2.31 et suivants ([MONDE.md](MONDE.md)) |
| Pont suspendu de la vallée | Un **vrai troisième passage** sur la rivière, ailleurs que sur le gué nord, relié par des sentiers ; il s'ouvre avec les gués (quête « Les passages de la vallée »). | Retenu (Damien, 16 novembre 2026, vers 23 h) ; ouverture avec les gués : choix de Claude ; V2.31 ([MONDE.md](MONDE.md)) |
| Lieux de la grande carte : nuit | Le pont suspendu s'ouvre **avec les gués** (gardé) ; les lieux ont aussi des **créatures de nuit** du bestiaire de nuit existant (loups de la nuit, échos). | Retenu (Damien, 17 novembre 2026, vers 1 h 10) ; V2.33 et suivants ([MONDE.md](MONDE.md)) |
| Points sûrs et passerelle des Ronces | Points sûrs **devant l'entrée de la mine**, **devant le fort** et **plus bas sur la route du col** (là où la pente le permet) ; la **passerelle des Ronces** s'ouvre à la **fin du chapitre 1**, avec la quête qui le termine. | Retenu (Damien, 18 novembre 2026) ; mine et fort au V2.33 à V2.39, col et passerelle au V2.40 ([MONDE.md](MONDE.md)) |

## 7 sexies. Grand Solitaire, 2e partie (Retenu)

Décisions de Damien du 15 novembre 2026, vers 9 h 45, rapportées par la session principale. Elles règlent les valeurs de la première partie (PR #171) avant le boss dans le moteur ; le détail est dans [MONDE.md](MONDE.md#le-grand-solitaire-boss-du-monde-première-partie--règles-et-données).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Planification | **Fenêtres de 6 h**, **une chance sur deux** par fenêtre. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Mode solo | Tant que le monde n'est pas partagé, **sa vie compte pour 1 joueur** (base + par joueur). | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Apparence | **Donnée à la première victoire**, en attendant sa quête de mémoire. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Emplacements tous occupés | **Il apparaît quand même**, à l'emplacement le moins mauvais (le plus loin des joueurs). | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Vie du boss | Les PV passent à l'**échelle 1/100** : 2 000 + 400 par joueur, soit **2 400 PV en solo**, pour un combat d'environ **5 à 8 minutes**. | Retenu (Damien, 15 novembre 2026) |

## 7 septies. Fêtes aux vraies dates (Retenu)

Décisions de Damien du 15 novembre 2026, vers 9 h 45, rapportées par la session principale (PR #174). Elles précisent le sujet 12 « Fêtes et saisons » de la [feuille de route](FEUILLE_DE_ROUTE.md) ; le détail est dans [MONDE.md](MONDE.md#fêtes-aux-vraies-dates-histoire-en-schéma-14).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Noms des trois fêtes existantes | **Semailles** (l'ancienne foire de printemps), **Fête des Moissons**, **Veillée des Contes** (l'ancienne veillée de l'Aube). La réplique « la nuit la plus longue » est retouchée (la veillée tombe en novembre). | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Dates des douze fêtes | Toutes les dates proposées sont gardées : Lanternes 1-7 janvier, Liens 10-16 février, Carnaval 1-10 mars, Semailles 10-23 avril, Artisans 1-10 mai, Feux du Seuil 20-30 juin, Tournoi 10-23 juillet, Étoiles 8-17 août, Moissons 15-28 septembre, Échos 25 octobre-3 novembre, Contes 15-28 novembre, Givre 18-31 décembre. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Concours d'une fête datée | **Un seul concours récompensé par fête et par personnage** (par occurrence annuelle de la fête), au lieu d'un par jour du monde. | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Fêtes sans contenu | Les neuf fêtes dont le contenu reste à écrire **n'affichent que leur nom** (fêtes du calendrier seulement). | Retenu (Damien, 15 novembre 2026, 9 h 45) |
| Fêtes sans contenu (suite) | Le choix du 15 novembre (les neuf fêtes n'affichent que leur nom) est **remplacé par le chantier des fêtes** : les douze fêtes ont leurs activités, leur monnaie, leur marchand et leurs récompenses ([§ 7 undecies](#7-undecies-les-12-fêtes-dorsédia-retenu)). | Retenu (Damien, 18 novembre 2026) |
| Fête datée et marché | Quand leurs heures se croisent, **la fête datée passe avant le marché** ; le marché de la semaine se tient quand même. | Retenu (Damien, 15 novembre 2026, 9 h 45) |

## 7 octies. Pêche : appâts (Retenu)

Décision de Damien du 15 novembre 2026, vers 14 h 25, rapportée par la session principale (relecture indépendante de la PR #181, revenu illimité de la pêche).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Appâts | La pêche demande des **appâts** : chaque lancer en consomme **un**, acheté chez un marchand ou ramassé (vers, mouches…). Sans appât, le lancer est **refusé** (refus atomique, avec un message). | Retenu (Damien, 15 novembre 2026, 14 h 25) |
| Prix et sources | Vers de terre 2 cuivre et mouches de mai 3 cuivre à l'épicerie du bourg ; vers à ramasser au gué du moulin, mouches au bord du lac (métier de récolte « pêche »). | Recommandation de Claude (PR #181), valeurs provisoires |

## 7 nonies. Cuisine et boulangerie (Retenu)

Réponses de Damien du 15 novembre 2026, vers 15 h 30, rapportées par la session principale (questions de la PR #183).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Où cuisiner | Au **four de la boulangerie de Firmin** pour les pains et les pâtisseries, et au **feu de camp** pour les poissons et les plats simples. | Retenu (Damien, 15 novembre 2026, 15 h 30) ; répartition des recettes : Claude (pains, viennoiseries, pâtisseries au four ; friture, truite meunière, quenelles, matelote au feu de camp de Gaspard) |
| Concours de pâtisserie | Le joueur présente **sa** couronne des moissons ou **sa** tarte renversée à Firmin ; la **qualité** du plat (normale, supérieure, chef-d'œuvre) décide du prix ; **un concours par fête et par an**. | Retenu (Damien, 15 novembre 2026, 15 h 30) ; fait dans la 2e partie (concours à la Fête des Moissons ; prix : recommandation de Claude, voir [MONDE.md](MONDE.md#concours-de-pâtisserie-cuisine-2)) |
| Recettes | Les **treize recettes** sont gardées telles quelles. | Retenu (Damien, 15 novembre 2026, 15 h 30) |
| Valeurs des plats et prix des ingrédients | Valeurs provisoires de Claude, à ajuster à l'essai. | Recommandation (question non posée) |

| Concours des Semailles | Un concours de pâtisserie **aussi à la fête des Semailles** (recette de printemps cohérente avec les données : la **fouace des Semailles**), en plus des Moissons. **Prix confirmés** : couronne 150 / 300 / 600 cuivre, tarte 80 / 150 / 300 ; la fouace garde le barème de la couronne. La pâtisserie présentée reste un **trophée**, pas utilisable en combat. | Retenu (Damien, 16 novembre 2026) ; fait (branche `v2-31-mises-cartes`) |
| Deux concours à la même fête | Aux Semailles, le **tir d'Aldric et le concours de pâtisserie** se font tous les deux (ils ne se ferment plus l'un l'autre) ; même règle aux Moissons (tourte et pâtisserie). Tarte : pas aux Semailles, fouace seulement. | Retenu (Damien, 16 novembre 2026, vers 2 h 20) ; fait (histoire en schéma 16, piste `lane`) |

## 7 decies. Cartes à la taverne (Retenu)

Décisions de Damien du 15 novembre 2026, vers 18 h 45, rapportées par la session principale (PR #187, vague 4 « la vie à côté du combat »). Détail : [MONDE.md](MONDE.md#cartes-à-la-taverne-vague-4-3e-partie).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Règles | Le jeu des **Trois Feux**, tel que proposé : 27 cartes (soleil, lune, brume ; 1 à 9), cinq cartes et cinq manches, **+3** à la couleur qui l'emporte (soleil sur brume, brume sur lune, lune sur soleil), le plus de manches gagne ; PNJ prudent, hardi ou rusé. | Retenu (Damien, 15 novembre 2026, 18 h 45) |
| Mises | **5, 10 ou 25 cuivre**, rendues **doublées** si l'on gagne ; **au plus 3 parties par PNJ et par jour**. | Retenu (Damien, 15 novembre 2026, 18 h 45) |
| Récompense rare | Après **10 victoires** : un **jeu de cartes rare** (objet décoratif) et le titre **« As des Trois Feux »**. | Retenu (Damien, 15 novembre 2026, 18 h 45) |
| Adversaires | **Mathurin** et les PNJ présents à la taverne le soir. | Retenu (Damien, 15 novembre 2026, 18 h 45, sur la proposition de Claude) |
| Mises, confirmées | Mises de **5, 10 ou 25 cuivre** ; **3 parties par PNJ et par jour** ; jeu de cartes rare et titre **« As des Trois Feux »** après **10 victoires** ; la **partie en cours est enregistrée dans la sauvegarde** (v25). | Retenu (Damien, 16 novembre 2026) ; fait (branche `v2-31-mises-cartes`) |
| Partie nulle, plafond de gains | Partie nulle : **chacun récupère sa mise**. **Plafond de gains à la taverne : 150 cuivre par jour**, compté sur les **gains seulement** (les pertes ne redonnent pas de marge). | Retenu (Damien, 16 novembre 2026 ; gains seulement : vers 2 h 20) ; fait (branche `v2-31-mises-cartes`) |
| Donne à chaque assise | Un **compteur d'assises persistant** dans la sauvegarde (v25) : la donne change à chaque fois qu'on s'assoit. | Retenu (Damien, 16 novembre 2026, vers 2 h 20) ; fait |
| Où et quand on joue | Les joueurs de cartes sont **à l'auberge de 18 h à minuit** (d'après leur emploi du temps) ; mises proposées dans leur dialogue, écran de partie Unity. | Retenu (Damien, 16 novembre 2026, vers 14 h ; la PR #193 proposait 19 h à 22 h) ; fait (branche `v2-32-cartes-cote-jeu`) |
| Titre actif | Le joueur choisit **un titre affiché sous son nom** parmi ceux obtenus (titres de la guilde et « As des Trois Feux »), depuis la carte d'aventurier ; sauvegarde **v26** (`cards.shown`). | Retenu (Damien, 16 novembre 2026, vers 14 h) ; fait (branche `v2-32-cartes-cote-jeu`) |

## 7 undecies. Les 12 fêtes d'Orsédia (Retenu)

Décisions de Damien du 17 novembre 2026, rapportées par la session principale, sur le dossier « Les 12 fêtes d'Orsédia » (AssetLab, `Fetes/`, 108 références). Elles précisent le sujet 12 « Fêtes et saisons » de la [feuille de route](FEUILLE_DE_ROUTE.md) ; le détail est dans [MONDE.md](MONDE.md#les-12-fêtes-dorsédia).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Monnaie de fête | **Chaque fête a sa monnaie propre** (12 monnaies). Elle est **liée au personnage**, **gardée d'une année sur l'autre**, **ni échangeable entre joueurs ni convertible en cuivre**. Elle se gagne **aux activités de la fête** et se dépense **chez le marchand de fête**. | Retenu (Damien, 17 novembre 2026) |
| Récompense annuelle | **Une récompense annuelle par fête et par personnage**, jamais perdue pour toujours : une récompense ratée **se rattrape à la fête suivante** (l'année d'après), **achetable à prix plein** en monnaie de fête. | Retenu (Damien, 17 novembre 2026) |
| Nature des récompenses | **Cosmétiques seulement, sans aucune statistique.** Chaque fête gagnée donne **en plus un titre** (mécanisme des titres existant). | Retenu (Damien, 17 novembre 2026) |
| Raretés | Les **récompenses** sont de rareté **Rare**, les **costumes Communs**. | Retenu (Damien, 17 novembre 2026) |
| Fenêtres | Déjà retenu le 15 novembre (§ 7 septies) : **vraies dates (7 à 14 jours)** ; sans date fournie, **repli sur le calendrier du jeu**. | Retenu (Damien, 15 et 17 novembre 2026) |
| Économie des fêtes | Validée telle que proposée : **plafond 999** pièces par fête, le gain au-delà **perdu mais annoncé** (l'activité compte quand même) ; prix : **décoration 40 / 60 / 90** (jusqu'à 1 m, jusqu'à 2,5 m, au-delà), **costume 120**, **plat 5**, **récompense rattrapée 150** ; gains : **concours 30** (une fois par édition), **activité 10** (une fois par jour de fête). | Retenu (Damien, 18 novembre 2026) |
| Gagner une fête | **3 activités différentes** dans l'édition pour Semailles et Moissons, **2** pour les autres fêtes. | Retenu (Damien, 18 novembre 2026) |
| Rattrapage | On ne rattrape que **l'édition précédente**, et **sans le titre** (seule une fête gagnée le donne). | Retenu (Damien, 18 novembre 2026) |
| Marchand de fête | **Un marchand dédié**, pas le colporteur : un **étal de fête sur la place**, avec **son propre PNJ forain**, dont le **décor change chaque mois** (sous-lot F3). | Retenu (Damien, 18 novembre 2026) |
| Forain et décor de l'étal | **Maître Florentin**, le forain des fêtes, gardé (nom et place sur la place du bourg) ; le **décor automatique** de l'étal (les décorations de la fête qui tiennent sur un étal, jusqu'à 2,5 m) est gardé. | Retenu (Damien, 19 novembre 2026) |
| Titres, raretés, jours, heures, invités | Les propositions de Claude sont gardées : noms des titres (Porte-Lanterne, Cœur des Liens, Visage du Carnaval, Plume de Griffon, Main habile, Âme de la Ronde, Laurier des Lices, Attrape-Lune, Dernière Gerbe, Voix des Échos, Passe-Conte, Étoile du Givre), **décorations et plats Communs**, jours de repli, heures et invités des fêtes. | Proposition validée par défaut (Damien, 18 novembre 2026), à revoir en jouant |
| Interface des fêtes (F4) | L'étal du forain et les activités des invités passent par des **réponses du dialogue** (« [Fête] … »), comme les mises des Trois Feux : **gardé**. La **bourse** n'affiche que la monnaie de la fête en cours ; **toutes les monnaies** restent visibles à l'étal : **gardé**. | Retenu (Damien, 19 novembre 2026) |
| Décor du mois (F5) | En plus de la place du village et de la place du marché du Bourg : **devant l'auberge**, au **parvis de la chapelle** et aux **portes du Bourg**, mêmes règles de placement. | Retenu (Damien, 19 novembre 2026) |
| Maison du joueur | **Oui**, en **chantier à part**, après les lieux reconstruits ; les décorations de fête achetées y seront posées. Chantier V2.53 ouvert le 20 novembre 2026 (§ 7 quaterdecies). | Retenu (Damien, 19 novembre 2026), en cours |
| Réconfort d'un plat de fête (F6) | Un **vrai petit confort hors combat pendant 60 minutes de jeu** (marche un peu plus rapide hors combat, ou endurance qui revient plus vite hors combat), **aucune statistique de combat** ; déterministe et testé côté cœur ; hors sauvegarde si le format devait changer (perdu au rechargement). Valeur : **+10 % de vitesse de marche hors combat**, avec le changement du moteur (moteur 0.35.0, journal 29, instantané 10, nouvelle référence figée ; journaux historiques jamais modifiés), fait au sous-lot F7. | Retenu (Damien, 19 novembre 2026) |
| Accessoire de fête des habitants | **Un accessoire par fête** (masque, couronne…) : **oui, plus tard**, dans un chantier à part avec les costumes en 3D. **Option B retenue** : les habitants en fête tiennent **le plat de la fête** en main ; la récompense n'est pas portée, pour qu'elle reste rare ; exception : le **masque du Carnaval**, vrai accessoire de costume, reste porté (V2.56.3). | Retenu (Damien, 19 et 21 novembre 2026) ; V2.56.3 en PR |

## 7 duodecies. Pâtisseries de Firmin (Retenu)

Décisions de Damien du 19 novembre 2026, rapportées par la session principale, sur le dossier « Les pains et pâtisseries » (AssetLab, `Firmin/`, 16 modèles) et la PR #217 ; détail dans [MONDE.md](MONDE.md#pâtisseries-de-firmin-19-novembre-2026).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Niveaux de réussite au concours | **Trois niveaux** montrés, selon la qualité de la pièce de la main du joueur : normale → **raté** (N1), supérieure → **correct** (N2), chef-d'œuvre → **maîtrisé** (N3). | Retenu (Damien, 19 novembre 2026) |
| Pièce achetée à Firmin | Montrée **maîtrisée** (N3). | Retenu (Damien, 19 novembre 2026) |
| Recettes | **On suit le dossier** : la tarte renversée se fait sur **pâte brisée** (objet et recette nouveaux), le levain chef est un **levain de blé** ; la pâte feuilletée reste pour les chaussons. | Retenu (Damien, 19 novembre 2026) |
| Calendrier de l'étal | Celui proposé par Claude : levain, pâtes brisée et feuilletée tous les jours ; tarte renversée le **dimanche** ; fouace les **samedis et dimanches de printemps** ; couronne les **samedis et dimanches d'automne**. | Retenu (Damien, 19 novembre 2026) |
| Pâtisserie tenue en main | **Plus tard**, avec les objets portés du héros. **Option A retenue** : celle qu'on mange (2,5 s), sinon la meilleure de ses pièces hors combat (V2.56.1). | Retenu (Damien, 19 et 21 novembre 2026) ; V2.56.1 en PR |

## 7 terdecies. Lieux reconstruits, V2.43 (Retenu)

Réponses de Damien du 19 novembre 2026, rapportées par la session principale ([PLAN_GRANDE_CARTE.md](PLAN_GRANDE_CARTE.md), V2.43 ; [MONDE.md](MONDE.md#lieux-reconstruits-sous-lot-v243)).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Lieux reconstruisibles | On commence par **5 lieux** : le **hameau** de la carrière, le **pont effondré** de la route de l'Est, le **moulin à foulon**, la **tour de signal** du sommet et **une aile de l'abbaye** (le dortoir). Le format doit permettre d'en ajouter d'autres plus tard (H12 complet : villes, routes, mines, phares, ateliers). | Retenu (Damien, 19 novembre 2026) |
| Avancement d'un chantier | Par **paliers de contributions variées** : or, matériaux de métier, combats et exploration autour du lieu. La contribution de **tous les joueurs** viendra au lot 13 avec l'état de région. | Retenu (Damien, 19 novembre 2026) |
| Perte d'une reconstruction | Une reconstruction **n'est jamais perdue** (ni bandits, ni corruption, ni Envers). | Retenu (Damien, 19 novembre 2026) |
| Envers des douze lieux (V2.52) | Niveaux dans l'**ordre de la surface, de 24 à 28** ; reprise **toujours à l'autel de l'Aube** ; coffres **fouillés une seule fois** ([MONDE.md](MONDE.md#lenvers-des-vallons--le-contenu-des-douze-lieux-chantier-v252a)). | Retenu (Damien, 27 novembre 2026, recommandations de la PR #262) |
| Ce qu'apporte un lieu reconstruit | Un **décor** et des **PNJ**, des **services** (marchand, artisan, point sûr, voyage) et **son intérieur**. | Retenu (Damien, 19 novembre 2026) |
| Bois et pierre | On ajoute le **bois** et la **pierre** (bruts) et les **planches** et **pierres taillées** (travaillées, recettes dans les métiers existants) ; récolte du bois en forêt et de la pierre à une carrière, sur des nœuds de récolte comme ceux qui existent ; les paliers des chantiers s'en servent en plus des matériaux existants. | Retenu (Damien, 19 novembre 2026) ; V2.43.2a |
| Métiers du bois et de la pierre | Deux **nouveaux métiers** : **bûcheron** (récolte du bois, planches) et **tailleur de pierre** (récolte de la pierre, pierres taillées), chacun avec sa progression, comme les métiers existants. Quantités validées : **3 bois → 2 planches**, **3 pierres → 1 pierre taillée**, nœuds qui repoussent en **60 s**. | Retenu (Damien, 19 novembre 2026, réponses à la PR #222) |
| Ateliers faciles | La taille de la pierre se fait **aussi à la petite carrière** (niveau 6) ; les planches se scient **aussi** dans un lieu facile près du village (Claude a proposé le **moulin du village**, niveau 1) ; la cabane du bûcheron et les gradins gardent un second atelier. | Retenu (Damien, 19 novembre 2026, relecture de la PR #222) |
| Montants et services des 5 lieux | Les paliers proposés par Claude (V2.43.2b, tableau de [MONDE](MONDE.md#lieux-reconstruits-sous-lot-v243)) et tous les services (réparation chez le charron et la foulonnière, points sûrs au pont et à la tour, boutiques du charron, de la foulonnière et du frère hospitalier) sont **validés tels quels**. | Retenu (Damien, 19 novembre 2026, réponses à la PR #224) |
| Modèles des lieux reconstruits | **Jusqu'à 330 crédits Tripo** pour les ~11 modèles des lieux (échafaudages, pont, palan, tour de signal, dortoir, roue et maillets du foulon) ; génération lancée par la session principale (`AssetLab/Lieux_Reconstruits/tripo_lieux.py`), jamais par un agent. | Retenu (Damien, 20 novembre 2026, PR #225) |
| Intérieurs des lieux reconstruits | Hameau : **atelier du charron** (fiche I11) ; pont : **relais** façon maison du péager (I17) ; moulin à foulon : sur le modèle du **moulin du village** ; tour de signal : **salle de garde** ; abbaye : **dortoir** avec les lits du lot C. Chaque **porte s'ouvre une fois le lieu reconstruit**. | Retenu (Damien, 20 novembre 2026) ; V2.43.4 |

## 7 quaterdecies. Maison du joueur, V2.53 (Retenu)

Réponses de Damien du 20 novembre 2026, rapportées par la session principale ([MONDE.md](MONDE.md#maison-du-joueur-sous-lot-v253)). Elles précisent, pour ce chantier, Q25 (refuge personnel évolutif) et les sujets 8 et 50 de la [feuille de route](FEUILLE_DE_ROUTE.md) (chambre à soi puis terrain dans un quartier) : la maison de ce chantier est **une seule maison dans le Bourg** ; le terrain dans un quartier avec des voisins reste à venir (lot 13 et au-delà).

| Sujet | Ce qui est retenu | Statut |
|---|---|---|
| Achat et agrandissement | La maison s'**achète une fois en or**, puis s'**agrandit par paliers** (or, planches, pierre taillée), avec le système des chantiers (paliers, apports plafonnés, refus atomiques). | Retenu (Damien, 20 novembre 2026) |
| Combien, où | **Une seule maison**, dans le **Bourg fortifié**. | Retenu (Damien, 20 novembre 2026) |
| Pose des meubles | **Pose libre au sol**, grille de **0,25 m**, rotation par **quarts de tour** ; la pose au mur viendra plus tard. | Retenu (Damien, 20 novembre 2026) |
| À quoi elle sert | **À tout** : décor, **coffre**, **atelier** (cuisine ou établi) plus lent qu'en ville, **point sûr à soi**, **lit** (bonus de repos, aucune statistique de combat sauf règle de repos existante). | Retenu (Damien, 20 novembre 2026) |
| Touches et commandes tactiles de la maison | **N** : la maison (panneau « Chez soi », ou rentrer chez soi depuis un point sûr) ; **H** reste l'aide ; **R** : l'ultime en combat, et tourne le meuble en mode décoration (commandes de combat suspendues pendant la pose) ; on **quitte l'aventure par le menu de la pause** (« Quitter l'aventure »), plus par N ni par le bouton « Scène », remplacé par le bouton « Maison » ; **une touche, un seul usage par contexte** (exploration, décoration, panneau, dialogue), testé ; au doigt tout de suite : bouton « Maison », et en mode décoration un toucher pour poser, Tourner et Annuler. | Retenu (Damien, 20 novembre 2026) |
| Montants (prix, paliers, bonus du lit, vitesse de l'atelier) | Propositions de Claude (PR #230) **validées** : maison à **8 000 cuivre** ; paliers grande salle (3 000 cuivre, 20 planches, 6 pierres taillées), seconde pièce (4 000, 30, 12), grande maison (6 000, 40, 20) ; **lit : 120 minutes** de réconfort ; meubles (2 à 6 matériaux, revendus 110 à 400 cuivre). L'**attente de l'atelier** (30 s de jeu) **n'est pas sauvegardée** : la sauvegarde ne change pas. | Retenu (Damien, 20 novembre 2026) |

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
