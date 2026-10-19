# Cadrage du jeu complet — registre des décisions

Source : document de Damien du 19 octobre 2026, « Réponses à Claude, Q1 à Q40 et vision consolidée », archivé sans modification dans [cadrage/2026-10-19_reponses_q01_q40.md](cadrage/2026-10-19_reponses_q01_q40.md). Les questions Q1 à Q40 sont celles du questionnaire de Claude du même jour. Ce registre reprend les choix de ce document, confrontés le 19 octobre 2026 au dépôt (`main` à `f5998c5` : moteur 0.14.0, journal au format 8, contenu du Bastion 0.5.0, protocole réseau 1).

**Rôle des documents.** La [feuille de route](FEUILLE_DE_ROUTE.md) reste la source de vérité du lot en cours et des règles du moteur. Ce registre est celle de la vision du jeu complet. Le [plan à partir du lot 11](PLAN_LOT_11.md) propose l'ordre des chantiers.

Un choix « Retenu » ici ne modifie aucune règle du moteur tant qu'un lot ne l'a pas réalisé. Les écarts avec l'existant sont listés en section 6.

## 1. Comment lire ce registre

| Statut | Sens |
|---|---|
| **Retenu** | Choix exprimé par Damien. |
| **Recommandation** | Réponse proposée (questionnaire ou cadrage), qui sert de base de travail. Elle n'est jamais présentée comme une décision de Damien : elle est confirmée ou corrigée avant le lot qui la réalise. |
| **Ouvert** | Chiffre, préférence ou règle encore à arbitrer ou à étudier. |

| Périmètre | Sens |
|---|---|
| **Tranche** | Première tranche jouable : les Vallons d'Aube (lots 11 à 14 proposés). |
| **Alpha** | Première alpha proposée à des testeurs extérieurs. C'est un jalon à définir (Q40). |
| **Final** | Le jeu à sa sortie. |

Les chiffres d'une tranche ou d'une alpha (24 places, niveaux 1 à 10…) sont **provisoires et configurables**. Ce ne sont jamais des plafonds du MMORPG. Ceux qui existent déjà sont des paramètres des données (`rules.json`) ; ceux à venir le seront aussi.

## 2. Direction générale (Retenu)

- **Le jeu.** Éclats du Seuil est un MMORPG original pour PC et mobile, principalement jouable seul, dans un monde multijoueur partagé. Combat en temps réel, progression, exploration, métiers, relations avec les PNJ et découvertes se complètent.
- **Les inspirations** : *The Exiled Heavy Knight* et *Satisfy / Overgeared*. Elles donnent une direction (identité des classes, compréhension des mécaniques, builds, artisanat, objets remarquables, secrets, trajectoires personnelles). Leurs personnages, leur intrigue et leurs règles ne sont pas repris.
- **L'univers.** La Rupture, le Seuil et les Éclats relient les systèmes du monde. La promesse : construire un personnage, trouver sa place dans l'univers, laisser une trace de ses aventures.
- **Direction artistique : fantasy stylisée.** Volumes doux et travaillés, matières peintes, couleurs expressives, silhouettes d'équipement reconnaissables, ambiance propre à chaque région (Vallons accueillants, forêt mystérieuse, profondeurs du Seuil étranges).
- **Deux vues au choix du joueur**, utilisables en exploration comme en combat :
  - **vue 1**, vue haute de trois quarts ;
  - **vue 2**, troisième personne montrant le personnage entier.

  La caméra ne change ni les portées, ni les collisions, ni les règles (détail en H14).

## 3. Registre des réponses Q1 à Q40

### A. Première région et tranche jouable

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q1 | Première région | **Vallons d'Aube** : village, chemins, rivière, moulin, carrière, ruines, manifestations du Seuil ; retours ultérieurs (secrets, ressources, quêtes de PNJ, poches de danger avancé) | Retenu | Géographie détaillée à concevoir, avec des trajets lisibles |
| Q2 | Mission de la tranche | **« Les traces sous le moulin »**, en cinq étapes : enquêter au village ; suivre les traces ; rouvrir le passage du moulin (récolte, fabrication, raccourci réel) ; comprendre la manifestation ; vaincre le chef et revenir (conséquence visible chez les PNJ) | Recommandation | Une réponse ou une action change une aide, une information ou un objectif. Présence d'une quête secrète et du réveil renforcé : à estimer |
| Q3 | Niveaux du premier parcours | **1 à 10** | Recommandation | « Les compétences prévues pour ce premier parcours » ; livres, héritages et découvertes en apportent ensuite. Plafond final : Q18 |
| Q4 | Équipement (final) | **Plusieurs emplacements, effets spéciaux, choix adaptés aux builds** ; plusieurs bons choix par tranche de niveau, même à rareté égale ; le **bouclier du Bastion est un vrai équipement** | Retenu | **12 emplacements** (deux mains, tête, torse, épaules, mains, jambes, pieds, ceinture, pendentif, deux anneaux) : Recommandation |
| Q4 | Équipement (tranche) | **Arme, main secondaire ou bouclier, armure, bijou** : modèle extensible, catalogue restreint | Recommandation | Adaptation de l'option B à quatre emplacements |
| Q5 | Inventaire | Tranche : **24 places, objets empilables** | Recommandation | Final : capacité évolutive, filtres, comparaison, stockage, objets de quête, commerce. Capacité finale (40 puis 80 évoqués) : Ouvert |
| Q6 | Récolte et fabrication | **Minerais et forge, plantes et remèdes**, une courte chaîne de chaque famille, utile à l'aventure ; minerais ordinaires distincts des Éclats ; **chaque métier a son expérience et ses niveaux**, séparés du niveau de combat | Retenu | — |
| Q7 | Potions et Bastion | **Potions de soin limitées**, recharge partagée entre consommables de soin. Le Bastion n'a pas de soin propre ; il peut utiliser les consommables universels et recevoir le soutien d'un allié | Recommandation | Compatible avec la règle historique A1 (écart E4). Valeurs, stock et recharge à équilibrer |
| Q8 | Défaite | **Retour au dernier point sûr** adapté, **sans perte** des acquis (niveaux, XP gagnée, équipement, livres, métiers, étapes de quête acquises) ; pas de perte punitive supplémentaire | Retenu | Le coût est le temps, la tentative et les consommables. En groupe : secours si la rencontre le permet ; réinitialisation quand tous ont échoué ou quitté la zone, sans effacer une victoire enregistrée |
| Q9 | Deuxième joueur | **Région partagée** : se voir, se déplacer et combattre ensemble, avec ses objectifs personnels | Retenu | Crédit des actions, aide et partage de quête explicites. Place dans la tranche : plan technique. Le lot 10 ne couvre pas le monde partagé (écart E5) |
| Q10 | Sauvegarde | Final : **comptes et progression sous autorité serveur**, données versionnées, migrations | Retenu | Tranche : fichier local possible (Recommandation) ; un personnage local n'a pas de valeur dans le monde public ; politique de remise à zéro à annoncer |
| Q11 | Entrée en combat | **Sur place, en temps réel**, sans changement d'écran ; un donjon peut avoir son entrée et son instance | Retenu | — |
| Q12 | Éléments de terrain | Final : **raccourcis, pistes, accès secondaires, passages souterrains, manifestations, interactions** | Retenu | Première démonstration : un raccourci réel et une manifestation compréhensible (Recommandation). Les accès essentiels ont une solution pour toutes les classes |

### B. Combat

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q13 | Positions | **Positions réelles** qui comptent : portée, placement, zones à éviter, mouvements utiles, contournement, interactions avec les lieux ; garde et parade restent importantes | Retenu | Calendrier : traiter ce socle tôt, avant de construire la tranche dessus (Recommandation). Règles et autorité à définir avant la représentation |
| Q14 | Rôles | **Chaque classe accomplit seule** la campagne et les voies principales ; protection, dégâts, contrôle et soutien perceptibles et utiles en coopération | Retenu | Contenus collectifs dédiés possibles (écart E1) |
| Q15 | Soin (jeu complet) | **Classe ou orientation de soutien** avec soin, protection, renforcement, gestion d'altérations ; voie solo satisfaisante | Recommandation | Ne remplace ni garde, ni parade, ni placement |

### C. Classes et progression

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q16 | Classes de la première alpha | **Trois classes complètes** (mêlée, distance, soutien ou spécialisation) | Recommandation | Nombre final : Ouvert. Une alpha élargie à 4 ou 5 classes est possible |
| Q17 | Deuxième classe | **Classe à distance**, de préférence un **mage lié au Seuil** | Recommandation | Nom et trois arbres à concevoir |
| Q18 | Niveau maximal | **30 n'est pas un plafond final validé** ; il reste la configuration actuelle | Ouvert | Fixer courbe d'XP, paliers, arbres, livres et héritages avant le chiffre |
| Q19 | Après le plafond | **Plusieurs progressions** : maîtrises, livres, héritages, connaissances, métiers, réputation, équipements, recherches, refuge ; Éclats encadrés (pas d'empilement illimité) | Retenu | — |
| Q20 | Attributs | **Base fixée par la classe et le niveau** ; personnalisation par les arbres, les deux livres actifs, l'héritage et l'équipement ; pas de répartition manuelle | Recommandation | Attributs du modèle conservés et réévalués (écart E6) |
| Q21 | Changer de build | **Gratuit, en lieu sûr, hors combat** ; configurations sauvegardées ; un livre retiré rend ses points ; les livres appris restent | Retenu | Ne réinitialise ni décisions narratives, ni relations, ni serments. Changement d'héritage : Ouvert |

### D. Monde

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q22 | Régions | Final : **cinq régions, une ville centrale, le Cœur du Seuil**. Noms de travail : Vallons d'Aube, Forêt des Murmures, Côte des Brumes, Crêtes de Fer, Plateau de Cendre, Havre d'Ambre (ville), Cœur du Seuil (expéditions avancées) | Recommandation | Tranche : les Vallons (Retenu). Trois régions : cible possible d'une alpha élargie. Une région n'est pas une bande de niveau unique |
| Q23 | Cœur du Seuil | **Expéditions principales faisables seul ou à plusieurs** ; expéditions collectives dédiées possibles, annoncées comme telles | Retenu | Réveil renforcé d'un boss solo : solution solo exigeante prévue |
| Q24 | Montures, voyages | **Montures et points de voyage**, destinations découvertes au préalable | Recommandation | Ne décide ni du vol libre ni de la navigation |
| Q25 | Logement | **Refuge personnel évolutif** (camp, maison, atelier, domaine) | Retenu | « Plus tard » est un ordre de réalisation |
| Q26 | Jour, nuit, météo | **Effets sur le jeu** (créatures, traces, ressources, passages, manifestations), règles apprenables | Retenu | Les occasions importantes reviennent ; pas d'heure réelle très restrictive. Le temps du monde découle des ticks du serveur (contrat de déterminisme) |

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
| Q31 | Taille des groupes | **Cinq joueurs** | Recommandation | Quatre évoqué auparavant ; aucune valeur validée. Compagnons comptés par une règle explicite ; capacité des instances selon les mesures |
| Q32 | Guildes | **Oui** : identité, projets, contrats, expéditions, installations, transformations régionales ; facultatives | Retenu | — |
| Q33 | Combat entre joueurs | **Volontaire, en arènes ou zones dédiées** ; progression principale en PvE ; pas de PvP permanent sur les routes ordinaires | Recommandation | Ses récompenses ne deviennent pas un passage obligé du PvE |
| Q34 | Boss de monde | **Événements publics coopératifs facultatifs**, liés à la région ; contribution utile comptée (pas seulement le dernier coup) | Retenu | Nombre de joueurs et modalités : à mesurer |

### G. Produit

| Q | Sujet | Choix | Statut | Notes |
|---|---|---|---|---|
| Q35 | Plateformes | Final : **PC et mobile**, même personnage et même progression | Retenu | Premiers essais sur PC, mobile pris en compte dès maintenant, **validation tactile tôt** (Recommandation) |
| Q36 | Modèle économique | Aucun modèle ni prix choisi | Ouvert | Aucun achat de puissance contre de l'argent réel (Recommandation). Les livres s'achètent en monnaie du jeu |
| Q37 | Langues | **Français et anglais** ; rédaction en français, textes préparés pour la traduction | Recommandation | Énigmes et jeux de mots adaptés à chaque langue |
| Q38 | Hébergement | Essais locaux, puis serveur adapté aux tests à distance et au jeu persistant | Recommandation | Budget, hébergeur, nombre de joueurs : Ouvert. Aucune dépense autorisée par ce cadrage. La machine d'un autre projet n'est pas disponible |
| Q39 | Graphismes | **Fantasy stylisée, vues 1 et 2** | Retenu | Méthode de production (formes simples, packs, artistes, IA) : Ouvert ; toute ressource payante est proposée avec son coût avant engagement |
| Q40 | Horizon de l'alpha | Aucune date ni disponibilité fournie | Ouvert | Claude propose jalons et estimation ([plan](PLAN_LOT_11.md#7-estimation)) |

## 4. Systèmes de la vision (H1 à H14)

| H | Système | Ce qui est retenu | Statut | À spécifier |
|---|---|---|---|---|
| H1 | PNJ avec mémoire | Personnalité, objectifs, relations, habitudes ; ils se souviennent des actes pertinents. Réponses et actions changent indices, confiance, objectifs, aide, alliances, accès aux secrets. Une réponse polie n'est pas toujours la meilleure ; certaines décisions ferment une possibilité | Retenu | États narratifs et réactions cohérentes, pas de simulation illimitée des conversations |
| H2 | Trois échelles de conséquences | **Parcours personnel**, **expédition d'un groupe**, **chapitre public d'une région** ; un joueur ne supprime pas seul un PNJ indispensable aux autres ; les transformations durables créent des aventures et laissent des traces | Retenu | Règle des choix partagés d'une expédition |
| H3 | Quêtes secrètes conditionnelles | Déclenchées par une combinaison d'**acquis durables** et de **circonstances temporaires**, puis une action finale ; voies alternatives ; indices ; journal des découvertes ; les occasions reviennent ; la première découverte donne du prestige sans rien confisquer | Retenu | Format des conditions ; récompenses variées (une quête secrète n'est pas forcément mythique) |
| H4 | Donjons, boss et réveil renforcé | Boss final garanti par donjon ; populations expliquées par les lieux. Le **réveil renforcé** (« Érance », nom provisoire) suit des conditions et des signes lisibles. Règle de récompense : première victoire enregistrée et récompensée une seule fois ; suite renforcée annoncée ; récompense supplémentaire si elle est vaincue ; première victoire gardée en cas d'échec ; ni défaite, ni redémarrage, ni reconnexion ne rendent le premier palier | Retenu | Un seul réveil par expédition (Recommandation) ; taux d'apparition et de butin : Ouvert |
| H5 | Équipement varié | Quatre notions distinctes : niveau requis, puissance de base, rareté, adéquation au build ; plusieurs boucliers de même niveau aux orientations différentes ; « rare » ne veut pas dire unique sur le serveur | Retenu | Éveil et reforge, par famille ou par objet |
| H6 | Deux livres de build actifs | **Deux livres actifs au plus par configuration**, sur l'ensemble des trois arbres (pas deux par arbre) ; une seconde copie ne cumule pas ; collection permanente ; achat en monnaie du jeu, ou trouvés en donjon, quête ou découverte ; prérequis visibles | Retenu | Place des modules dans l'arbre, coût en points, seuils des majeurs, sort de l'exemplaire physique |
| H7 | Arbres, héritages, maîtrises | **Trois arbres combinables** par classe (en place pour le Bastion) ; **héritages** gagnés par exploration, relations, recherche et épreuves ; **maîtrises** prouvées par des épreuves | Retenu | Une seule orientation d'héritage active (Recommandation). Seuils, ultimes et points à relire ; changement d'héritage : Ouvert |
| H8 | Combinaisons et terrain | Une compétence crée une situation qu'une autre exploite (marque, ouverture, déstabilisation, zone, obstacle), en solo et avec des alliés | Retenu | Coûts, durées et limites ; commandes lisibles sur mobile. Exemple de la marque après parade : Recommandation |
| H9 | Métiers et créations signées | XP, niveaux, techniques, recettes et spécialisations indépendants du combat ; recette connue, maîtrise, outillage et matériaux distincts ; fabrication triviale moins formatrice ; créations signées ; recherche, enseignement, procédés perdus | Retenu | Catalogue des familles (extraction minière, bûcheronnage, cueillette, pêche, forge et armurerie, cuir, textile, alchimie, cuisine, artisanat des Éclats) : Recommandation |
| H10 | Économie, commandes, caravanes | Commandes à contrat (biens conservés jusqu'à l'issue), échanges sans connexion simultanée, spécialités régionales, routes et caravanes, plusieurs voies d'approvisionnement pour le solo | Retenu | Échanges, liaison des objets, livraison, taxes, consommation : Ouvert |
| H11 | Géographie, Atlas, nature | Surface, profondeurs et échos du Seuil dans certaines régions (pas la carte trois fois) ; repères stables ; **Atlas de la Rupture** (connaissances, cartes, bestiaire, langues, matériaux, phénomènes) qui ouvre des actions ; populations mobiles avec indices ; grandes chasses préparées | Retenu | — |
| H12 | Factions, transformation des régions | Factions aux intérêts divisés, rangs gagnés par des actes, incompatibilités annoncées ; reconstruction collective (villes, routes, mines, phares, ateliers) ; chroniques | Retenu | Noms de factions : noms de travail |
| H13 | Refuge, compagnons, haut niveau | Refuge qui reflète le parcours, PNJ qui s'y installent ; compagnons avec histoire et fonctions ; plusieurs ambitions de haut niveau, aucune obligatoire | Retenu | Place des compagnons dans l'équipe et les combats |
| H14 | Direction artistique, deux vues | Fantasy stylisée ; vues 1 et 2 disponibles dans la même expérience ; démonstration jouable demandée avant de décliner lieux, animations et effets | Retenu | Fonctionnement commun proposé (Recommandation) : une caméra réglable entre les deux positions, rappel rapide, réglage conservé, **aucune bascule automatique** (entrée en combat, classe, boss), molette et rappel sur PC, geste et bouton sur mobile, visibilité adaptée sans voir à travers les parois ni révéler les secrets. Amplitudes, rotation, projection exacte : Ouvert |

## 5. Chiffres provisoires et paramètres

| Valeur | Où | Statut | Périmètre |
|---|---|---|---|
| Niveau maximal 30, 1 point de talent par niveau dès le niveau 2 (29 points) | `rules.json`, `progression` | Configuration actuelle ; plafond final Ouvert (Q18) | Moteur actuel |
| Talent majeur : 18 points investis dans son arbre, puis 1 point ; un seul par build | `rules.json`, `majorTalents` | Validé pour la configuration actuelle ; à relire avec livres et héritages (H6, H7) | Moteur actuel |
| Actives 2, 3, 4 aux niveaux 1, 5, 10 ; ultime au niveau 10 ; passifs 1, 2, 3, 4 aux niveaux 1, 5, 20, 30 | `rules.json` | Validé pour la configuration actuelle | Moteur actuel |
| Niveaux du premier parcours : 1 à 10 | à créer | Recommandation (Q3) | Tranche |
| Inventaire : 24 places | à créer | Recommandation (Q5) | Tranche |
| Emplacements d'équipement : 4 (tranche), 12 (final) | à créer | Recommandation (Q4) | Tranche, Final |
| Raretés : 6 rangs, Secret à part | à créer | Retenu (Q30) | Final |
| Livres actifs : 2 par configuration | à créer | Retenu (H6) | Final |
| Héritage actif : 1 | à créer | Recommandation (H7) | Final |
| Groupe standard : 5 joueurs | à créer | Recommandation (Q31) | Final |
| Réveil renforcé : 1 par expédition | à créer | Recommandation (H4) | Final |
| Combattants par combat : 64 (borne 256) | `rules.json`, `combat.limits.maxCombatants` | Paramètre technique | Moteur actuel |
| Avance de la télégraphie en réseau : plafond 1 s | `NetworkClientOptions` | Paramètre (lot 10.6) | Réseau actuel |

## 6. Écarts avec l'existant

Relevés le 19 octobre 2026 en confrontant le cadrage aux documents et au code. Chaque écart indique sa source et son traitement.

| N° | Constat (source) | Cadrage | Traitement |
|---|---|---|---|
| **E1** | Feuille de route, Vision : « principalement jouable en solo (**aucun groupe imposé pour la moindre activité**) » | Q14 et Q23 : des **contenus et expéditions collectifs dédiés** peuvent exister ; les voies essentielles de progression restent accessibles en solo | **Question pour Damien** (décision L11-D9 du plan) : remplacer l'ancienne phrase par « aucune voie essentielle de progression n'exige un groupe ; des contenus collectifs dédiés, annoncés comme tels, peuvent exister » ? |
| **E2** | Feuille de route, Vision : « fantasy stylisée **plutôt sombre** » ; « Caméra : **non tranchée** » (déjà complétée le 18 octobre : les deux caméras du prototype gardées au choix) | Q39 et H14 : fantasy stylisée aux ambiances propres à chaque région (Vallons accueillants) ; vues 1 et 2 retenues. Le cadrage précise que ces choix remplacent les mentions antérieures | **Appliqué** : Vision de la feuille de route mise à jour. La caméra « isométrique » du prototype (perspective plongeante à 50°, pas une projection orthographique) est la base de la vue 1, la troisième personne celle de la vue 2 |
| **E3** | `docs/MOTEUR.md`, « Ciblage (A7) » : « logique uniquement : pas de position, de distance ni de collision » ; `SkillCombatDefinition.cs` ; feuille de route : « aucun système de positions avant la fin du lot 8 ». Dans Unity, le déplacement n'est qu'affiché : le moteur ignore où sont les combattants | Q13 : positions réelles qui ont des conséquences | A7 reste vrai pour le moteur 0.14.0 ; Q13 le remplace comme direction. **Changement de moteur à réaliser** (lot 11 proposé) : nouveaux formats de journal, de scénario et de classe, nouveaux journaux de référence |
| **E4** | Feuille de route, **A1** : « pas de soin **pour le Gardien au lot 8** » et « aucun soin… avant la fin du lot 8 ». Le questionnaire (Q7) et la première version du plan (Q6) résumaient à tort « pas de soin pour le Bastion ». Le moteur n'a aucun effet de soin | Q7 : potions limitées ; le Bastion sans soin propre peut recevoir du soin | **Pas de contradiction** : A1 visait l'arbre Gardien et le lot 8. Résumé corrigé. Les potions (ou la classe de soutien, Q15) demanderont une catégorie d'effet « soin » dans le moteur |
| **E5** | Première version du plan : « le monde **fabrique un combat** (`CombatSetup`) par rencontre et lit le résultat ». `docs/RESEAU.md`, Principe : salle de combat à composition fixe (deux places), **chaque client rejoue tout le combat** à partir des commandes du serveur | Q9 (région partagée), Q11 (combat sur place), Q13 (positions), Q34 (boss de monde), Q8 (réinitialisation quand tous ont quitté la zone) : des joueurs rejoignent un combat en cours ; une région contient beaucoup de joueurs et de créatures | Le modèle du lot 10 convient aux combats fermés (instances, duels, essais), **pas à une région partagée**. **Décision d'architecture pour Damien** (L11-D8 du plan) : simulation par zone sur le serveur et réplication filtrée |
| **E6** | `CharacterBuild.Attributes` : les attributs font partie du build ; les builds livrés ont ceux du niveau 30 quel que soit leur niveau ; feuille de route : « **budget d'attributs** : chantier ultérieur » (qui suggérait des points à répartir) | Q20 : base fixée par la classe et le niveau, sans répartition manuelle | Si Q20 est confirmée : **table d'attributs par classe et niveau**, au lieu d'un budget à répartir ; format des builds à faire évoluer (attributs explicites gardés pour les scénarios d'essai). Formulation de la feuille de route ajustée |
| **E7** | Feuille de route, « Progression validée (ne change pas pendant le lot 8) » : 30 niveaux, 29 points, majeur à 18 + 1 | Q18 : 30 n'est pas un plafond final ; H6 et H7 : livres, héritages, maîtrises, budget de points à repenser | **Pas de contradiction** : la configuration actuelle reste en vigueur jusqu'à la refonte de la progression (chantier de l'alpha). Titre de la section précisé. Erreur de la première version du plan corrigée : au niveau 10, le majeur (niveau 20) et les 3ᵉ et 4ᵉ passifs (niveaux 20 et 30) manquent encore |
| **E8** | `DONNEES.md` : équipement « réservé, non implémenté » ; L8-D6 : déclencheurs d'équipement reportés au système d'objets ; `FORMULES.md` et `StatContributions` : trois puissances d'équipement déjà prévues dans les formules. Le bouclier du Bastion est implicite (garde, blocage, Heurt de bouclier, Bouclier lourd) | Q4 : effets spéciaux, bouclier réel | **Pas de contradiction** : le chantier des objets réalise L8-D6. Question pour plus tard : un Bastion **sans bouclier** (arme à deux mains) est-il possible ? |
| **E9** | Feuille de route, « Après le lot 8 », point 4 : progression et équipement, puis monde, métiers, économie, classes, multijoueur étendu, persistance, interfaces PC/mobile, direction artistique, alpha. La première version du plan gardait des positions seulement visuelles | Q13 (positions tôt), Q35 (validation tactile tôt), H14 (démonstration des deux vues avant la production) | **Nouvel ordre proposé** dans le [plan](PLAN_LOT_11.md#3-ordre-proposé-des-chantiers) (décision L11-D1) |
| **E10** | Les textes du jeu sont en français, écrits dans le code d'Unity (`Hud.cs`, `NetworkScreen.cs`…) et dans les noms des données | Q37 : français et anglais | **Pas de contradiction.** Textes du monde rangés par clés dès le lot 12 ; l'interface du prototype migrera avec la nouvelle interface (lot 14 proposé) |
| **E11** | Première version du plan : questions Q1 à Q10, numérotées autrement (Q7 défaite, Q8 deuxième joueur…) | Q1 à Q40 du questionnaire | **Appliqué** : numérotation Q1 à Q40 partout |
| **E12** | `CLAUDE.md` : « Après le lot 8 viennent le prototype Unity 3D, puis un serveur autoritaire » ; liste de lecture sans ce registre | — | **Appliqué** : `CLAUDE.md` et `AGENTS.md` renvoient à ce registre |

## 7. Points ouverts

**Informations à fournir par Damien**

- Budget : enveloppe ponctuelle pour ressources et outils ; plafond mensuel pour les services de test. Aucune dépense n'est autorisée tant que rien n'est fixé.
- Horizon (Q40) : date souhaitée pour les premiers testeurs, ou jalons, avec une disponibilité approximative.
- Matériel d'essai mobile : téléphone Android disponible ? Un iPhone demanderait un Mac et un compte de développeur Apple payant.

**Arbitrages de gameplay**

- Plafond final et rythme de progression (Q18) : concevoir les paliers avant le chiffre.
- Métiers : maîtrise de tous les métiers ou expertises avancées exclusives (Q27).
- Nombre de classes et de régions à chaque jalon (Q16, Q22) ; taille du groupe standard (Q31).
- Emplacements et capacité d'inventaire finaux (Q4, Q5).
- Points des livres et de l'héritage dans les seuils des arbres (H6, H7) ; changement d'héritage, portée des serments (Q21).
- Portée des marchés, transport des biens (Q28, H10).
- Caméra : transition, amplitudes, rotation, commandes PC et mobile, obstacles (H14).
- Règles du combat dans l'espace (visée, garde en mouvement, esquive, collisions, verticalité, attaques de zone) : décisions L11-D2 à L11-D7 du [plan](PLAN_LOT_11.md#8-décisions-nécessaires).
- Formule des contenus de groupe (E1).
- Bastion sans bouclier (E8).
- Nom du réveil renforcé : « Érance » est provisoire (le mot courant s'écrit « errance »).
- Modèle commercial et conditions de lancement (Q36).

**Arbitrage d'architecture** : simulation du monde partagé (E5, décision L11-D8).
