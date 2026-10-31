# Plan jusqu'à l'alpha — chantiers numérotés dans l'ordre

Plan de Claude du 31 octobre 2026, **proposition à relire par Damien**. Il range dans l'ordre tout ce que le [périmètre](PERIMETRE.md) met avant l'ouverture de l'alpha fermée sur PC. Il ne change aucune décision : les choix viennent de la [feuille de route](FEUILLE_DE_ROUTE.md) (sujets 1 à 54), du [périmètre](PERIMETRE.md) (et de ses [10 points tranchés](PERIMETRE.md#points-tranchés-par-damien-31-octobre-2026-1-h-20)), du [plan des Vallons](PLAN_VALLONS_SOLO.md) et du [cadrage](CADRAGE.md). Ce qui demande un choix nouveau est dans les [questions pour Damien](#questions-pour-damien).

## En tête

**Comment lire ce plan.** Le plan est une liste de **88 chantiers**, numérotés de 1 à 88. Un chantier, c'est un sous-lot : une branche, une PR, de la taille du V1.1 ou du 12.3. Le numéro donne l'**ordre de départ** de l'agent principal. Un second agent peut prendre en même temps un chantier plus loin dans la liste, quand la colonne « Parallèle possible » du [tableau](#tableau-récapitulatif) le permet. Chaque chantier a un identifiant (V1.x, V2.x, V3.x, V4.x, 13.x, C0.x à C4.x, P.x, B.x, M.x, G.x, A.x), un titre et une taille : **S** (petit, un ou deux jours), **M** (moyen), **L** (gros, souvent un format nouveau et beaucoup de tours de revue). Les tailles comparent les chantiers entre eux ; ce ne sont pas des dates. Sous chaque chantier, quelques lignes disent ce qu'il fait, ce dont il dépend, ce qui change de format ou de règles, et s'il y a un **essai de Damien** dans Unity. Une **décision d'argent de Damien** ou une **décision de Damien** est toujours écrite en clair.

**Les règles de la méthode.** Elles ne changent pas ([CLAUDE.md](../CLAUDE.md#méthode-pour-un-sous-lot)) : **une PR par chantier**, en brouillon tant qu'elle n'est pas prête ; tests touchés verts en local, puis la vérification complète par la CI ; une **relecture indépendante** par un agent neuf, une seule par PR, avant la **revue Codex** ; la revue Codex sur chaque PR ; **fusion selon les accords de Damien**. Pour la vague 1, Damien a autorisé la fusion de chaque PR dès que sa CI est verte et que Codex n'a plus de remarque (sujet 18, 29 octobre 2026, 21 h 01). Pour tout le reste, il faut son accord pour chaque PR (voir la [question 4](#questions-pour-damien)). Un changement de format (sauvegarde, règles, histoire, protocole) est toujours versionné, documenté et testé, avec de nouveaux journaux de référence figés ; les anciens ne changent jamais. Deux agents ne travaillent jamais en même temps sur le même format.

**Ce qui est fait, ce qui est en cours.** Fusionnés : **V1.1** monnaie, marchands et voyage rapide (PR #72), **V1.5** écran des compétences (PR #71), les **tests plus rapides** (PR #73), les **réponses de Damien** aux chantiers V1.5 et V1.1 (PR #74 : réserve des compétences, place vide, marchands fermés la nuit, journée de 3 h), la feuille de route et le périmètre (PR #70). En cours, en brouillon : **V1.2** équipement complet (PR #75) et **V1.6** moteur des quêtes (PR #76). Avant eux, les lots 8 à 12, l'essai de style, les intérieurs et la caméra sont faits.

**Les grands jalons et leurs dates estimées.** Ce sont des fourchettes honnêtes, **pas des promesses** (sujet 20 : objectifs souples, aucune date annoncée au public tant qu'elle n'est pas sûre). Elles comptent environ un chantier par jour, revues comprises, deux agents en parallèle quand c'est possible, les relectures et les essais de Damien, et les jours sans travail.

| Jalon | Chantier qui le marque | Fourchette |
|---|---|---|
| J1. Vague 1 finie, Vallons « vrai jeu » en solo | 12 (V1.10, essai complet) | fin novembre à mi-décembre 2026 |
| J2. Vallons solo finis (vagues 2 et part gardée des vagues 3 et 4) | 30 (V4.4, essai complet) | mi-janvier à fin février 2027 |
| J3. Monde partagé jouable sur Internet | 45 (13.15, essai complet à plusieurs) | mars à avril 2027 |
| J4. Cinq classes jouables | 67 (C4.4) | avril à mai 2027 |
| J5. Ouverture de l'alpha fermée sur PC | 88 (A.7) | **avril à juin 2027, mai-juin le plus probable** |

Comme dans le [calcul de charge](PERIMETRE.md#calcul-de-charge), **fin février 2027 est le plus tôt possible**, sans aucun retard : il ne faut pas y compter. Mars 2027 ne paraît pas tenable.

## Le compte des chantiers

| Bloc | Chantiers | Le périmètre en comptait |
|---|---|---|
| 1. Fin de la vague 1 (V1.2 à V1.10) | 12 (n° 1 à 12) | ~8 |
| 2. Vague 2, le monde vivant (V2.1 à V2.8) | 8 (n° 13 à 20) | ~8 |
| 3. Part des vagues 3 et 4 gardée avant le lot 13 (V3.1 à V3.6, V4.1 à V4.4) | 10 (n° 21 à 30) | ~10 |
| 4. Lot 13, monde partagé, avec l'éditeur interne (13.1 à 13.15) | 15 (n° 31 à 45) | ~14 (dont les guildes simples) + 2 pour l'éditeur |
| 5. Les quatre nouvelles classes (C0.1 à C0.6, C1 à C4) | 22 (n° 46 à 67) | ~20 + 2 (caractéristiques et refonte des arbres, comptées avec les premières minutes) |
| 6. Premières minutes, création du personnage, interface de l'alpha (P.1 à P.5) | 5 (n° 68 à 72) | ~4 (les ~6 moins les 2 passés au bloc 5) |
| 7. Livres de build, cheval simple, guildes simples (B.1 à B.4, M.1 à M.3, G.1 et G.2) | 9 (n° 73 à 81) | ~4 + ~3 + ~2 |
| 8. Préparation de l'alpha (A.1 à A.7) | 7 (n° 82 à 88) | ~4 (les ~6 moins les 2 de l'éditeur) |
| **Total** | **88** | **environ 80** (79 en additionnant les lignes) |

**Pourquoi 88 et pas 80.** Le total dépasse de 9 chantiers. Aucun n'ajoute une décision ; ils rendent visible un travail que le calcul de charge avait mis dans d'autres lignes ou pas compté :

- **+4** : le V1.9 devient **cinq PR de textes**, une par chapitre, puisque Damien relit chapitre par chapitre ([plan des Vallons](PLAN_VALLONS_SOLO.md#6-questions-pour-damien), question 5). Ce sont de petits chantiers.
- **+3** : l'interface de l'alpha (P.4), les touches et les sons (P.5) et le PC minimum (A.3) sont rangés « Alpha » dans le périmètre (sections 8 et 10) mais n'avaient pas de ligne dans le calcul.
- **+2** : la marque, le Discord et la page Steam (A.4 et A.5) sont surtout des **démarches de Damien**, guidées par Claude, avec peu de code.
- **+1** : l'essai à plusieurs sur Internet et la tenue de charge (13.15).
- **−1** : le souvenir, le réveil et la création tiennent en 3 chantiers (P.1 à P.3) au lieu des ~4 qui restent au calcul une fois les caractéristiques et la refonte des arbres passées au bloc des classes.

Sans les deux démarches, cela fait **86 sous-lots de code, environ 7 de plus que le calcul**. Avec deux agents, l'écart pèse environ une semaine ; la fourchette de l'alpha ne bouge pas.

## Bloc 1 — Fin de la vague 1 (V1.2 à V1.10)

Le détail de chaque chantier est dans le [plan des Vallons, section 3](PLAN_VALLONS_SOLO.md#3-les-dix-sous-lots). Ici, seulement l'essentiel et l'ordre.

### 1. V1.2 — Équipement complet : places, boucliers, écran (L)

- **En cours** (PR #75, brouillon) : 18 places, Bastion toujours avec un bouclier, boucliers qui changent la garde, catalogue des niveaux 1 à 20, écran du personnage avec la comparaison claire.
- Dépend de V1.1 (fait).
- Formats : règles et moteur (garde du bouclier, nouveau journal figé), catalogue des objets, sauvegarde.
- Essai : oui, court (écran du personnage, boucliers).

### 2. V1.6 — Quêtes : le moteur (M)

- **En cours** (PR #76, brouillon) : journal à deux onglets, objectifs `talk` et `deliver`, effets d'or et de points de guilde, récompenses au choix, PNJ des intérieurs qui parlent, bandits, chef des bandits, loup blanc.
- Dépend de V1.1 (fait).
- Formats : histoire en nouvelle version, validateur étendu.

### 3. V1.3 — Effets d'équipement : armes, bijoux, ensembles, propriétés tirées (L)

- Effets par les déclencheurs d'origine `equipment`, ensembles de 2, 4 et 6 pièces, objets nommés, propriétés tirées au hasard par la clé de la mort.
- Dépend de V1.2.
- Formats : catalogue des objets, instance d'objet dans la sauvegarde, règles et empreinte, nouveaux journaux figés.

### 4. V1.7 — Guilde des aventuriers (M)

- La guilde au bourg, carte d'aventurier, rangs G, F et E, points, épreuves de promotion, contrats du jour, boutique et titre.
- Dépend de V1.6.
- Formats : données de la guilde (nouveau fichier), sauvegarde (carte), textes par clés.

### 5. V1.4 — Forge, qualité, usure (M)

- Recettes d'équipement à la forge de Maëlle, qualité normale, supérieure ou chef-d'œuvre signé, petit jeu de forge à trois frappes, recette qui peut rater ; usure selon les dégâts reçus, réparation contre de l'or.
- Dépend de V1.1 et V1.3.
- Formats : fabrication, sauvegarde (qualité, usure).
- Essai : oui, court (forge, réparation).

### 6. V1.8 — Progression et équilibrage (M)

- Niveaux jusqu'à 25 dans les Vallons (plafond qui monte, sujet 54 : 25 en alpha), données jusqu'à 30 ; 5e et 6e places de la barre (réponses de Damien du 30 octobre 2026) ; zones en mélange avec niveau minimum ; difficulté normale vérifiée par des combats simulés pour chaque chapitre ; rythme rapide puis plus lent.
- Dépend de V1.2 à V1.7.
- Formats : progression, zones (niveau minimum), pont temps réel et **protocole réseau** (deux places de plus), règles si besoin, nouveaux journaux figés.

### 7. V1.9.1 — Chapitre 1 « Les traces sous le moulin » et ses quêtes (S)

- La mission d'aujourd'hui reprise en chapitre 1 (niveaux 1 à 5), avec le court souvenir du premier Éclat ; quêtes secondaires proposées : « La ferrure de rechange », « Le pain du matin », « Les flèches perdues ».
- Dépend de V1.6. Textes proposés par Claude ; chaque quête jouée de bout en bout par un pilote.
- Formats : histoire et textes (pas de nouvelle version attendue).
- **Relecture de Damien** : chapitre 1.

### 8. V1.9.2 — Chapitre 2 « Le bourg d'Aube » et ses quêtes (M)

- Sœur Albe, Renart, les vols d'éclats, l'inscription à la guilde (niveaux 5 à 10) ; quêtes proposées : « Une dette à l'auberge », « Les cloches de la chapelle », « La dernière lanterne », « Le puits qui murmure », « L'apprentie de Maëlle », « Herbes pour la chapelle ».
- Dépend de V1.7 (guilde), V1.4 (forge pour l'apprentie) et V1.9.1.
- **Relecture de Damien** : chapitre 2 (avec les noms et répliques des marchands, comme décidé le 30 octobre 2026).

### 9. V1.9.3 — Chapitre 3 « Les champs et le hameau » et ses quêtes (M)

- Attaque du hameau par les bandits, piste jusqu'au lac (niveaux 10 à 15) ; quêtes proposées : « Les sangliers des champs », « La lettre au fond du lac », « Les vergers pillés », « Les potions du hameau », « Le pont des Vallons ».
- Dépend de V1.8 (niveaux) et V1.9.2.
- **Relecture de Damien** : chapitre 3.

### 10. V1.9.4 — Chapitre 4 « La tour et le camp » et ses quêtes (M)

- La tour qui ne répond plus, le chef des bandits et les Rassembleurs, le premier choix (l'épargner ou non, écrit dans les faits) (niveaux 15 à 20) ; quêtes proposées : « La tour silencieuse », « Le déserteur », « La prime du Borgne », « Le marchand qui en savait trop ».
- Dépend de V1.9.3.
- **Relecture de Damien** : chapitre 4.

### 11. V1.9.5 — Chapitre 5 « Le Seuil » et ses quêtes (M)

- Le rôdeur du Seuil, le Gardien et sa forme éveillée, le retour chez Odile, la voix de l'Envers (niveaux 20 à 25) ; quêtes proposées : « Le loup blanc », « Les pierres gravées ».
- Dépend de V1.9.4.
- **Relecture de Damien** : chapitre 5. La répartition des vingt quêtes entre les chapitres est une proposition ; la liste est celle du [plan des Vallons](PLAN_VALLONS_SOLO.md#4-la-quête-principale-et-les-quêtes-secondaires).

### 12. V1.10 — Places des maisons et essai de la vague 1 (S)

- Places des maisons du bourg agrandies (6 à 8 m), contrôles des Vallons agrandis.
- Dépend de tout le bloc 1.
- Formats : zone des Vallons.
- Essai : **la partie complète** (vague 1, de bout en bout, captures, avis, puis corrections). Premiers testeurs amis possibles (sujet 18 : testeurs dès la vague 1).

## Bloc 2 — Vague 2, le monde vivant (V2.1 à V2.8)

Contenu décidé le 29 octobre 2026 (« Vallons plus vivants » et « Les Vallons en très grand », [plan des Vallons, section 7](PLAN_VALLONS_SOLO.md#7-après-la-vague-1)). Les identifiants V2.x sont proposés ici.

### 13. V2.1 — Jour et nuit qui changent le monde (M)

- La journée de 3 h et la nuit de 22 h à 5 h (déjà dans le jeu, PR #74) changent le monde : lanternes allumées, villageois rentrés, créatures de nuit, nuit bleue et lisible (sujets 32 et 49).
- Dépend de V1.10.
- Formats : zone (populations selon l'heure).
- Essai : oui, court (la nuit).

### 14. V2.2 — Météo (S)

- Pluie, brume du matin, tirées de façon déterministe par le jour du monde ; d'abord pour l'ambiance, sans changer les règles du combat.
- Dépend de V2.1.
- Formats : temps du monde (sauvegarde du monde si besoin).

### 15. V2.3 — Villageois qui suivent leur journée (M)

- Emploi du temps des PNJ importants (travail, taverne, maison), qui se retrouvent au bon endroit ; dialogues qui en tiennent compte.
- Dépend de V2.1 et V1.6.
- Formats : histoire (PNJ en nouvelle version).

### 16. V2.4 — Animaux paisibles et bêtes qui se battent (M)

- Animaux paisibles (cerfs, lièvres), loups et sangliers qui se battent entre eux, premier écosystème des Vallons.
- Dépend de V1.10.
- Formats : **règles et moteur** (créatures hostiles entre elles), zone, nouveaux journaux figés.

### 17. V2.5 — Événements du monde : attaque du hameau, faille du Seuil (L)

- Événements déclenchés par le temps du monde, de façon déterministe, avec une récompense donnée une seule fois par personnage (registre).
- Dépend de V2.1 et V1.9.3 (le hameau).
- Formats : monde (nouveau bloc d'événements), sauvegarde (registre).
- Essai : oui, court (une attaque du hameau).

### 18. V2.6 — Le Seuil qui grandit (M)

- Des zones se corrompent au fil du temps, des failles à refermer, des couleurs qui se fanent près des failles (sujet 49).
- Dépend de V2.5.
- Formats : monde, sauvegarde (état des failles).

### 19. V2.7 — Réputation (M)

- Réputation auprès des villageois, de la garde, des rôdeurs et des bandits ; conditions des dialogues et des quêtes qui la lisent.
- Dépend de V1.6.
- Formats : sauvegarde, histoire (conditions et effets).

### 20. V2.8 — Fêtes au bourg, marchands au stock changeant, essai de la vague 2 (M)

- Fêtes au bourg, stock tournant des marchands (au-delà du stock du jour du V1.1).
- Dépend de V2.1 à V2.7.
- Formats : marchands, histoire.
- Essai : oui, court (la vague 2 entière, en une séance).

## Bloc 3 — Part des vagues 3 et 4 gardée avant le lot 13

Ce qui reste avant le lot 13 après le point 3 des [points tranchés](PERIMETRE.md#points-tranchés-par-damien-31-octobre-2026-1-h-20). Les identifiants sont proposés ici.

### 21. V3.1 — Donjons : le moteur, et la crypte sous la chapelle (L)

- Instance de donjon (une copie pour soi, même modèle que la région, [architecture](ARCHITECTURE_MONDE.md#7-zones-instances-déplacements-entre-zones)), pièges, énigmes, secrets, chef, réveil renforcé ; la crypte et son fantôme de nuit.
- Dépend de V1.10 et V2.1 (la nuit).
- Formats : zone en nouvelle version (instances, pièges, énigmes), sauvegarde (registre des premières victoires).
- Essai : oui, court (la crypte).

### 22. V3.2 — Mines de la carrière (M)

- Deuxième donjon, au moins 8 sortes de monstres (sujet 28), chef et réveil renforcé.
- Dépend de V3.1.
- Formats : données seulement (zone, créatures), si V3.1 a tout prévu.

### 23. V3.3 — Grottes noyées du lac (M)

- Troisième donjon, au moins 8 sortes de monstres, chef et réveil renforcé.
- Dépend de V3.1.
- Formats : données seulement.

### 24. V3.4 — Le Grand Solitaire et les créatures rares nommées (M)

- Boss du monde des Vallons (en solo d'abord, ouvert à tous au 13.10) ; créatures rares nommées avec butin spécial, liées aux contrats de la guilde.
- Dépend de V1.7 et V2.5.
- Formats : zone (boss du monde), butin.
- Essai : oui, court (le Grand Solitaire).

### 25. V3.5 — Exploration récompensée (M)

- Trésors, pierres gravées, cartes au trésor, énigmes du Seuil, chasses avec pistage ; la carte peinte qui se dessine en explorant.
- Dépend de V1.6.
- Formats : histoire et zone (découvertes), sauvegarde.

### 26. V3.6 — Arène du camp et choix qui restent (M)

- Arène au camp des rôdeurs ; suites des premiers choix (chef des bandits épargné ou non, hameau reconstruit ou en ruine).
- Dépend de V1.9.4 (le choix) et V2.5 (l'attaque du hameau).
- Formats : histoire, sauvegarde.

### 27. V4.1 — Pêche et carnet (M)

- Pêche au lac avec un petit jeu, carnet des prises.
- Dépend de V1.4.
- Formats : fabrication et récolte, sauvegarde (carnet).

### 28. V4.2 — Forge poussée (L)

- Amélioration de +1 à +10, pierres à sertir, runes, arme légendaire.
- Dépend de V1.3 et V1.4.
- Formats : catalogue des objets, instance d'objet, sauvegarde, règles si les runes passent par le moteur, nouveaux journaux figés.

### 29. V4.3 — Compagnon (M)

- Un compagnon dans les Vallons : un chien ou un mercenaire de la guilde (périmètre, section 2), piloté par l'ordinateur comme le robot du lot 10.
- Dépend de V1.7.
- Formats : moteur (allié piloté), sauvegarde, nouveaux journaux figés.

### 30. V4.4 — Bestiaire, hauts faits, titres et livres ; essai des Vallons solo finis (M)

- Bestiaire, hauts faits des Vallons, titres, trophées de boss, quelques livres à lire, sacs simples.
- Dépend de tout le bloc 3.
- Formats : sauvegarde, textes.
- Essai : **la partie complète** (les Vallons solo finis, jalon J2).

**Après l'ouverture de l'alpha, hors total** (point 3) : cuisine et boulangerie, concours de pâtisserie, jeu de cartes à la taverne, tir à l'arc et courses, maison au bourg (environ 10 chantiers, ajoutés pendant l'alpha).

## Bloc 4 — Lot 13, monde partagé (13.1 à 13.15)

But : se voir, se déplacer et combattre ensemble dans les Vallons, **le solo tournant sur le serveur** (sujet 54), selon l'[architecture retenue](ARCHITECTURE_MONDE.md#9-retenu-par-damien-le-20-octobre-2026) (état filtré par zone d'intérêt, prédiction du joueur local). La technique des serveurs (base de données, connexion, marché) est réglée par Claude ; ce qui coûte est présenté à Damien. L'éditeur interne se fait **pendant** ce lot (sujet 54).

### 31. 13.1 — Serveur de zone (L)

- Un processus serveur héberge la zone des Vallons ; joueurs qui entrent et sortent ; état filtré envoyé à chacun selon sa zone d'intérêt (option B).
- Dépend de V4.4 (sujet 20 : les vagues des Vallons, puis le lot 13) et de la démonstration du 11.7.
- Formats : **protocole réseau** en nouvelle version, nouveau projet serveur.

### 32. 13.2 — Client du monde partagé (L)

- Prédiction du déplacement local, autres joueurs lissés, annonces des coups avancées du ping ; **jouable à 150 ms**, reconnexion automatique, esquive en réseau (reportée le 22 octobre 2026, reprise ici).
- Dépend de 13.1.
- Formats : protocole, Unity (client).
- Essai : oui, court (deux PC, latence ajoutée).

### 33. 13.3 — Persistance sur le serveur (L)

- Base de données pour les personnages et le registre, sauvegardes **toutes les heures, en double** (sujet 24) ; reprise exacte après un arrêt.
- Dépend de 13.1.
- Formats : **sauvegarde** (passage du fichier local à la base, migration testée).

### 34. 13.4 — Comptes et connexion (M)

- Compte Éclats du Seuil (e-mail et mot de passe), une place de personnage par classe, données au strict minimum, serveurs en Europe, confidentialité courte (sujets 23 et 43).
- Dépend de 13.3.
- Formats : base (comptes), protocole (connexion).

### 35. 13.5 — Le solo sur le serveur (L)

- Unity devient un client seulement ; les sauvegardes locales d'aujourd'hui sont reprises sur le serveur ; l'hébergement local reste pour les prototypes et les essais.
- Dépend de 13.2, 13.3 et 13.4.
- Formats : sauvegarde (migration), Unity.

### 36. 13.6 — Récompenses et ressources partagées (M)

- Récompense selon la participation (le soigneur gagne comme le frappeur, sujet 39), ressources partagées entre joueurs, communes qui réapparaissent vite, sorties d'or mesurables.
- Dépend de 13.5.
- Formats : registre, zone.

### 37. 13.7 — Groupes, amis, relever un allié (L)

- Groupes de 2 à 6, liste d'amis, invitations, **étapes de quête partagées** en groupe, relever un allié (tout le monde sur la carte, soigneurs seulement en donjon), relevés limités pendant un boss.
- Dépend de 13.5.
- Formats : protocole, serveur, règles (relevé), histoire (étapes partagées), nouveaux journaux figés.
- Essai : oui, court (groupe à deux).

### 38. 13.8 — Chat et modération de base (M)

- Canaux près de toi, région, groupe, messages privés ; charte au premier lancement, filtre, bloquer, signaler.
- Dépend de 13.4.
- Formats : protocole, serveur.

### 39. 13.9 — Marché simple (M)

- Échanges directs par la fenêtre officielle, un tableau de ventes ; tout s'échange sauf les légendaires et mythiques liés ; échanges atomiques.
- Dépend de 13.5.
- Formats : serveur et base (objets, or).

### 40. 13.10 — Événements ouverts et donjons à plusieurs (M)

- Attaque du hameau, faille et Grand Solitaire ouverts à tous ceux qui sont là ; donjons des Vallons seul (avec compagnon) ou de 2 à 6.
- Dépend de 13.6, 13.7, V2.5, V3.1 et V3.4.
- Formats : zone (instances de groupe), serveur.

### 41. 13.11 — Éditeur interne (1) : quêtes et dialogues (L)

- Quêtes posées avec des boutons (PNJ dans une liste, étapes « aller à », « parler à », « ramener »), dialogues en bulles reliées par des flèches ; Damien corrige une quête, une réplique ou un prix sans toucher au code (sujet 54).
- Dépend de V1.6 (format de l'histoire).
- Formats : aucun nouveau ; l'éditeur lit et écrit l'histoire avec le validateur existant.
- Essai : oui, court (Damien écrit une petite quête).

### 42. 13.12 — Éditeur interne (2) : créatures, bouton « Essayer », vérification (M)

- Fiches de créatures avec le bouton « Essayer » qui lance le combat ; vérification qui signale une quête impossible à finir avant de jouer.
- Dépend de 13.11.
- Formats : aucun nouveau.

### 43. 13.13 — Sanctions et outils de Damien (S)

- Sanctions graduées appliquées à la main (avertissement, suspension, bannissement), avertissement sur les liens, outils pour Damien et Claude, modérateurs de l'alpha.
- Dépend de 13.8.
- Formats : serveur, base.

### 44. 13.14 — Hébergement de test (M)

- **Décision d'argent de Damien** : un petit serveur de test en Europe, loué au mois (OVH, Hetzner ou autre ; prix vérifiés et proposés au moment de payer, sujet 24).
- **Décision d'argent de Damien** : le plafond de dépenses mensuel, avec une alerte (sujet 54).
- Mise en service, sauvegardes en trois copies dont une hors ligne, plan de secours, tableau de bord du projet.
- Dépend de 13.3.

### 45. 13.15 — Essai à plusieurs sur Internet et tenue de charge (S)

- Mesures de charge avec des robots, rapport de lenteur, corrections.
- Dépend de 13.1 à 13.14.
- Essai : **la partie complète**, à plusieurs sur Internet (Damien et un testeur ami ; jalon J3).

## Bloc 5 — Les quatre nouvelles classes (C0.1 à C4.4)

Les cinq classes de l'alpha (sujet 22) : le **Bastion** (déjà là), le **Liturge de l'Aube** (soin), le **Veilleur** (distance), l'**Ombrelame** (corps à corps agile) et le **Tisse-Seuil** (magie de contrôle). Chaque classe : ressource propre, 3 arbres de talents, rôle principal et secondaire, rythme, signature et silhouette, environ 14 compétences en alpha (chiffre proposé par Claude, [question 2](#questions-pour-damien)), finit seule l'histoire. Proposition : faire ce bloc **pendant le lot 13, avec un second agent** ([question 1](#questions-pour-damien)). L'ordre des classes est aussi une proposition ([question 5](#questions-pour-damien)). Les classes ont besoin d'abord d'un socle commun (C0).

### 46. C0.1 — Fiches des quatre classes (S)

- Une fiche par classe : ressource et mécanique, armes, 3 arbres, ~14 compétences avec leurs annonces et leurs défenses (L11-D7), rôle principal et secondaire, rythme, signature, place dans la roue des classes (sujet 37).
- Dépend de rien : peut commencer dès maintenant, documentation seulement.
- **Relecture de Damien** : les quatre fiches, avant tout code de classe.

### 47. C0.2 — Caractéristiques (L)

- 7 caractéristiques (avec la Constitution), 2 points à placer par niveau en plus de la base de la classe, effet exact de chaque point, rendements décroissants, défenses (sujet 34).
- Dépend de V1.8.
- Formats : **sauvegarde** (points placés), **règles et classes** en nouvelle version, empreinte, nouveaux journaux figés.

### 48. C0.3 — Refonte des arbres et maître de classe (M)

- Refonte des arbres proposée pour l'alpha (L11-D1), talents et points changés chez le maître de classe (gratuit au début, puis en or), plafond de niveau 25.
- Dépend de C0.2.
- Formats : règles et classes, sauvegarde, nouveaux journaux figés.
- **Décision de Damien** : la refonte est proposée dans la PR, avec ses chiffres, et se fait avec lui (feuille de route, « Progression validée »).

### 49. C0.4 — Socle du moteur pour plusieurs classes (L)

- Ce que les quatre classes demandent au moteur et que le Bastion n'a pas : ressource de classe générique, soins directs, projectiles et attaques à distance, sorts lancés, marques, fils et contrôle.
- Dépend de C0.1.
- Formats : **moteur et règles**, schéma des classes en nouvelle version, nouveaux journaux figés.

### 50. C0.5 — Roulade et animations soumises au moteur (M)

- Roulade qui coûte de l'endurance (sujet 3, rangée « Alpha ») ; le moteur décide du moment de l'impact et de l'annulation d'un coup, l'animation suit (sujet 54).
- Dépend de C0.4.
- Formats : moteur et règles, protocole (nouvelle commande), Unity, nouveaux journaux figés.
- Essai : oui, court (roulade du Bastion).

### 51. C0.6 — Banc d'essai d'équilibrage (M)

- Chaque classe affronte tous les boss et les autres classes des milliers de fois sur le moteur déterministe ; cible de 5 % par rôle (sujet 37).
- Dépend de C0.4.
- Formats : aucun (outil dans `EdS.Tools`, rapport).

### 52 à 55. C1 — Liturge de l'Aube (soin et soutien)

- **52. C1.1 — Mécanique et compétences (L)** : la Ferveur (prières qui se chargent, lumière qui soigne et brûle), ~14 compétences, relevé à distance. Dépend de C0.4 et C0.5. Formats : classe en données, moteur si la Ferveur l'exige, nouveaux journaux figés.
- **53. C1.2 — Arbres, passifs et builds de référence (M)** : arbres Grâce, Bénédiction et le troisième de la fiche, passifs, builds de référence. Dépend de C1.1 et C0.3. Formats : classe en données.
- **54. C1.3 — Dans Unity : modèle, animations, signature (M)** : modèle et animations gratuits (Mixamo, packs), couleur, son et symbole, silhouette. Dépend de C1.1. Si aucun modèle gratuit ne convient : **décision d'argent de Damien** (fiche d'achat).
- **55. C1.4 — Équilibrage et essai (S)** : banc d'essai, réglages en données. Dépend de C1.2, C1.3 et C0.6. Essai : oui, court.

### 56 à 59. C2 — Veilleur (distance et exploration)

- **56. C2.1 — Mécanique et compétences (L)** : la Vigilance qui monte à l'immobilité, pièges de lumière, arc long et arc court, ~14 compétences. Dépend de C0.4 et C1.1 (une seule classe change le moteur à la fois). Formats : classe, moteur si besoin, nouveaux journaux figés.
- **57. C2.2 — Arbres, passifs et builds de référence (M)** : Guet, Traque, Lanterne. Dépend de C2.1 et C0.3.
- **58. C2.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C2.1. Même règle pour un achat éventuel : **décision d'argent de Damien**.
- **59. C2.4 — Équilibrage et essai (S)** : dépend de C2.2 et C2.3. Essai : oui, court.

### 60 à 63. C3 — Ombrelame (corps à corps et contrôle)

- **60. C3.1 — Mécanique et compétences (L)** : se fondre dans l'ombre, frapper dans le dos, poser des marques, dagues et lames jumelles, rythme en rafales, ~14 compétences. Dépend de C0.4 et C2.1. Formats : classe, moteur si besoin, nouveaux journaux figés.
- **61. C3.2 — Arbres, passifs et builds de référence (M)** : Lame, Voile, Poison. Dépend de C3.1 et C0.3.
- **62. C3.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C3.1. Achat éventuel : **décision d'argent de Damien**.
- **63. C3.4 — Équilibrage et essai (S)** : dépend de C3.2 et C3.3. Essai : oui, court.

### 64 à 67. C4 — Tisse-Seuil (contrôle et dégâts)

- **64. C4.1 — Mécanique et compétences (L)** : fils tissés entre les ennemis, tirés pour les lier, bâton et orbe, ~14 compétences. Dépend de C0.4 et C3.1. Formats : classe, moteur si besoin, nouveaux journaux figés.
- **65. C4.2 — Arbres, passifs et builds de référence (M)** : Trame, Nœud, Rupture. Dépend de C4.1 et C0.3.
- **66. C4.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C4.1. Achat éventuel : **décision d'argent de Damien**.
- **67. C4.4 — Équilibrage des cinq classes et essai (S)** : banc d'essai sur les cinq classes ensemble (jalon J4). Dépend de C4.2, C4.3 et des quatre autres équilibrages. Essai : oui, court (les cinq classes l'une après l'autre).

## Bloc 6 — Premières minutes, création du personnage, interface de l'alpha (P.1 à P.5)

Ordre décidé (sujet 21) : souvenir de la Rupture en version courte (point 5), réveil au village, création du personnage, tutoriel dans l'histoire. Les identifiants P.x sont proposés ici.

### 68. P.1 — Souvenir de la Rupture, version courte (M)

- Quelques minutes dans la peau du jeune Galeran : le Seuil éclate, la lune se fend, la main tendue vers un enfant, l'écran blanc. La grande mise en scène attend le lancement.
- Dépend de V4.4 (zone et outils de scène) ; modèles gratuits.
- Formats : histoire, une petite zone à part.
- **Relecture de Damien** : le texte du souvenir. Essai : oui, court.

### 69. P.2 — Réveil et tutoriel dans l'histoire (M)

- Réveil dans le pré près du moulin, Odile, une chose apprise à la fois (marcher, parler, garde, parade face à un loup, première compétence, récolte, forge) ; premier combat **moins de 2 minutes** après le réveil, première vraie récompense **en moins de 10 minutes** ; indications courtes qui disparaissent.
- Dépend de P.1 et V1.9.1.
- Formats : histoire, textes.
- Essai : oui, court (les dix premières minutes).

### 70. P.3 — Création du personnage, version simple (M)

- Au réveil : classe choisie en voyant chaque classe en action, visage simple, nom dit à Odile ; peuple vallonien seulement (périmètre, section 5).
- Dépend de P.2, 13.4 (une place par classe) et des cinq classes jouables (C4.3).
- Formats : sauvegarde et base (apparence), Unity.
- Essai : oui, court (créer un personnage de chaque classe).

### 71. P.4 — Interface de l'alpha : lisibilité (M)

- Grand livre à onglets, chiffres de dégâts, vie des ennemis avec leur annonce, fil des événements, info-bulle qui compare, mini-carte, suivi des quêtes, signes au-dessus des têtes discrets et réglables, barre de consommables à part (périmètre, sections 5 et 8).
- Dépend de 13.7 (lisibilité à plusieurs).
- Formats : Unity seulement.

### 72. P.5 — Touches, réglages et sons (M)

- Toutes les touches changeables (AZERTY et QWERTY), garde maintenue ou en bascule, réglages enregistrés avec le compte ; sons-signaux en combat (un son par attaque, tintement de la parade), son en 3D au casque, volumes séparés.
- Dépend de 13.4 et P.4.
- Formats : réglages du compte (base), Unity.
- Essai : oui, court (avec P.4).

## Bloc 7 — Livres de build, cheval simple, guildes simples

Versions simples décidées pour l'alpha (points 4, 6 et 8). Les identifiants B, M et G sont proposés ici.

### 73. B.1 — Livres de build : les règles (L)

- Deux livres actifs au plus par configuration, sur les trois arbres ; une seconde copie ne cumule pas ; collection permanente ; prérequis visibles ; aucun livre n'est le seul moyen d'éviter les attaques ordinaires (H6, L11-D4).
- Dépend de C0.3 (refonte des arbres).
- Formats : règles et classes, sauvegarde (collection), nouveaux journaux figés.
- **Décision de Damien** : la place des livres dans les arbres et leur coût en points (points ouverts H6 du cadrage), proposés dans la PR.

### 74. B.2 — Livres des cinq classes (M)

- Une première série de livres pour le Bastion et les quatre nouvelles classes.
- Dépend de B.1 et des C*.2.
- Formats : classes en données.

### 75. B.3 — Trouver et acheter les livres (M)

- Achat en monnaie du jeu, livres trouvés en donjon, en quête ou par une découverte (H6).
- Dépend de B.2, V3.1 et V1.6.
- Formats : marchands, butin, histoire.

### 76. B.4 — Écran des livres (S)

- Ranger, activer et comparer ses livres dans le grand livre à onglets.
- Dépend de B.3 et P.4.
- Essai : oui, court (groupé avec M.3 et G.2).

### 77. M.1 — Cheval : les règles (M)

- Un seul cheval simple : monter et descendre hors combat, vitesse au galop, on descend pour combattre.
- Dépend de 13.5 (le cheval vit sur le serveur).
- Formats : règles du monde, sauvegarde (monture possédée), protocole.

### 78. M.2 — Cheval : modèle et animations (M)

- Modèle gratuit, animations de marche, trot, galop, montée et descente.
- Dépend de M.1. Si aucun cheval gratuit ne convient : **décision d'argent de Damien** (fiche d'achat).

### 79. M.3 — Quête de la première monture (S)

- Le cheval gagné par une quête à la fin du chapitre 1 (sujet 7).
- Dépend de M.2 et V1.9.1.
- Formats : histoire.
- **Relecture de Damien** : le texte de la quête. Essai : oui, court (groupé).

### 80. G.1 — Guildes simples : créer, inviter (M)

- Créer une guilde, inviter, quitter, renvoyer ; une guilde principale par personnage (sujet 40).
- Dépend de 13.7.
- Formats : serveur et base, protocole.

### 81. G.2 — Canal de guilde et écran (S)

- Canal de guilde dans le chat, liste des membres.
- Dépend de G.1 et 13.8.
- Essai : oui, court (groupé).

## Bloc 8 — Préparation de l'alpha (A.1 à A.7)

### 82. A.1 — Carnet d'essai et bouton d'aide (M)

- Une touche : capture et remarque, envoyées à Claude (sujet 18) ; bouton d'aide dans le jeu, bug envoyé avec le journal du combat, rejoué au coup près (sujets 16 et 25).
- Dépend de 13.5.
- Formats : serveur (réception), Unity.

### 83. A.2 — Statistiques sans nom (M)

- Statistiques pseudonymisées, actives par défaut, désactivables, expliquées clairement : difficulté, parcours, économie (dont les sorties d'or), technique (sujet 25). Règle de la CNIL à vérifier ; sinon accord demandé au joueur.
- Dépend de 13.5.
- Formats : serveur, réglages du compte.

### 84. A.3 — PC minimum et fluidité (M)

- PC minimum de 2018, 60 images par seconde sur le PC conseillé ; ce qui compte au combat ne disparaît jamais ; rapport de lenteur (sujet 42).
- Dépend de C4.3 (toutes les classes à l'écran) et 13.15.
- Formats : réglages graphiques (Unity).

### 85. A.4 — Marque, nom de domaine, réseaux, Discord, entreprise (S, démarche)

- **Décision d'argent de Damien** : marque « Éclats du Seuil » à l'INPI **avant de montrer le jeu** (avant la première vidéo publique) ; environ 230 € pour deux catégories, à confirmer sur inpi.fr ; Claude vérifie d'abord que le nom est libre (sujet 23).
- Damien réserve lui-même, guidé par Claude : l'adresse e-mail du jeu, le nom de domaine (petite dépense, prix vérifié au moment), YouTube, TikTok, Instagram, **Discord** (sujet 17).
- Entreprise : rien à créer avant l'alpha, puisqu'il n'y a pas de vente ; un premier rendez-vous avec un comptable ou la CCI pendant l'alpha, pour que l'entreprise soit prête **avant la première vente** (sujet 54).
- Dépend de rien : **peut se faire dès maintenant**, hors du chemin des chantiers.

### 86. A.5 — Page Steam (S, démarche)

- Préparer la page (textes, captures, logo), la déclaration honnête de l'IA, le questionnaire de classification propre à Steam.
- La page Steam est rangée « Lancement » dans le [périmètre](PERIMETRE.md) (section 12), et le sujet 17 dit « page Steam tôt » : le moment de l'ouvrir est la [question 3](#questions-pour-damien). Frais de Steam : **décision d'argent de Damien**.
- Dépend de A.4 (la marque avant de montrer le jeu).

### 87. A.6 — Testeurs « Pionnier de l'Alpha » (S)

- Programme de testeurs (titre « Pionnier de l'Alpha », nom au monument des Fondateurs), testeurs adultes seulement, invitations, comptes, salon des testeurs sur le Discord, fiche d'essai.
- Dépend de A.4 (Discord), 13.4 et 13.14.
- **Décision de Damien** : le choix des testeurs et leur nombre.

### 88. A.7 — Répétition générale et ouverture de l'alpha (S)

- Critères de stabilité vérifiés (aucune perte de personnage, sauvegardes en double, reconnexion), dernières corrections, ouverture.
- Dépend de tout le reste.
- Essai : **la partie complète** (de la création du personnage au Gardien, avec un groupe).
- **Décision de Damien** : l'ouverture de l'alpha (jalon J5).

## Tableau récapitulatif

**Parallèle possible** : chantiers qui peuvent tourner en même temps avec deux agents, sans se gêner. La règle : deux chantiers ne changent jamais en même temps le même format (sauvegarde, règles et moteur, histoire, protocole, zone) ; les textes, l'outillage, la documentation et Unity peuvent avancer à côté d'un changement de moteur. Les démarches A.4 et A.5 peuvent se faire à tout moment.

| N° | Chantier | Taille | Dépend de | Essai | Parallèle possible |
|---|---|---|---|---|---|
| 1 | V1.2 Équipement complet (PR #75) | L | V1.1 | Oui, court | 2 (déjà en parallèle) |
| 2 | V1.6 Quêtes : le moteur (PR #76) | M | V1.1 | — | 1 |
| 3 | V1.3 Effets d'équipement | L | 1 | — | 7, 46 |
| 4 | V1.7 Guilde des aventuriers | M | 2 | — | 7, 46 |
| 5 | V1.4 Forge, qualité, usure | M | V1.1, 3 | Oui, court | 8 |
| 6 | V1.8 Progression et équilibrage | M | 1 à 5 | — | 8, 46 |
| 7 | V1.9.1 Chapitre 1 | S | 2 | Relecture | 3, 4 |
| 8 | V1.9.2 Chapitre 2 | M | 4, 5, 7 | Relecture | 5, 6 |
| 9 | V1.9.3 Chapitre 3 | M | 6, 8 | Relecture | 46 |
| 10 | V1.9.4 Chapitre 4 | M | 9 | Relecture | 46, 85 |
| 11 | V1.9.5 Chapitre 5 | M | 10 | Relecture | 46, 85 |
| 12 | V1.10 Places des maisons et essai | S | 1 à 11 | **Partie complète** | 46 |
| 13 | V2.1 Jour et nuit | M | 12 | Oui, court | 16, 46 |
| 14 | V2.2 Météo | S | 13 | — | 15, 16 |
| 15 | V2.3 Villageois et leur journée | M | 13, 2 | — | 14, 16 |
| 16 | V2.4 Animaux et bêtes qui se battent | M | 12 | — | 14, 15, 19 |
| 17 | V2.5 Événements du monde | L | 13, 9 | Oui, court | 16, 46 |
| 18 | V2.6 Le Seuil qui grandit | M | 17 | — | 16, 46 |
| 19 | V2.7 Réputation | M | 2 | — | 16 |
| 20 | V2.8 Fêtes, stock changeant, essai | M | 13 à 19 | Oui, court | 21 |
| 21 | V3.1 Donjons : moteur et crypte | L | 12, 13 | Oui, court | 20, 46 |
| 22 | V3.2 Mines de la carrière | M | 21 | — | 27, 28, 29 |
| 23 | V3.3 Grottes noyées du lac | M | 21 | — | 27, 28, 29 |
| 24 | V3.4 Grand Solitaire, créatures rares | M | 4, 17 | Oui, court | 27, 29 |
| 25 | V3.5 Exploration récompensée | M | 2 | — | 22, 23 |
| 26 | V3.6 Arène et choix qui restent | M | 10, 17 | — | 22, 23 |
| 27 | V4.1 Pêche et carnet | M | 5 | — | 22, 23, 24 |
| 28 | V4.2 Forge poussée | L | 3, 5 | — | 22, 23 |
| 29 | V4.3 Compagnon | M | 4 | — | 22, 23, 24 |
| 30 | V4.4 Bestiaire, hauts faits, essai | M | 21 à 29 | **Partie complète** | 31 (préparation), 46 |
| 31 | 13.1 Serveur de zone | L | 30, 11.7 | — | 47, 48 |
| 32 | 13.2 Client du monde partagé | L | 31 | Oui, court | 33, 47, 48 |
| 33 | 13.3 Persistance sur le serveur | L | 31 | — | 32, 49, 50 |
| 34 | 13.4 Comptes et connexion | M | 33 | — | 49, 50, 51 |
| 35 | 13.5 Le solo sur le serveur | L | 32, 33, 34 | — | 51, 52 |
| 36 | 13.6 Récompenses partagées | M | 35 | — | 52, 53 |
| 37 | 13.7 Groupes, amis, relever | L | 35 | Oui, court | 53, 54 |
| 38 | 13.8 Chat et modération de base | M | 34 | — | 54, 55, 56 |
| 39 | 13.9 Marché simple | M | 35 | — | 56, 57 |
| 40 | 13.10 Événements et donjons à plusieurs | M | 36, 37, 17, 21, 24 | — | 57, 58 |
| 41 | 13.11 Éditeur (1) quêtes et dialogues | L | 2 | Oui, court | 58, 59, 60 |
| 42 | 13.12 Éditeur (2) créatures, « Essayer » | M | 41 | — | 60, 61 |
| 43 | 13.13 Sanctions et outils | S | 38 | — | 61, 62 |
| 44 | 13.14 Hébergement de test | M | 33 | — | tous les C* |
| 45 | 13.15 Essai sur Internet, charge | S | 31 à 44 | **Partie complète** | 62, 63 |
| 46 | C0.1 Fiches des quatre classes | S | — | Relecture | tous |
| 47 | C0.2 Caractéristiques | L | 6 | — | 31, 32 |
| 48 | C0.3 Refonte des arbres | M | 47 | — | 31, 32 |
| 49 | C0.4 Socle du moteur multi-classes | L | 46 | — | 33, 34 |
| 50 | C0.5 Roulade, animations au moteur | M | 49 | Oui, court | 33, 34 |
| 51 | C0.6 Banc d'essai d'équilibrage | M | 49 | — | 34, 35, 52 |
| 52 | C1.1 Liturge : mécanique | L | 49, 50 | — | 35, 36, 51 |
| 53 | C1.2 Liturge : arbres | M | 52, 48 | — | 54, 36, 37 |
| 54 | C1.3 Liturge : Unity | M | 52 | — | 53, 56, 38 |
| 55 | C1.4 Liturge : équilibrage | S | 53, 54, 51 | Oui, court | 56, 38 |
| 56 | C2.1 Veilleur : mécanique | L | 49, 52 | — | 54, 55, 38, 39 |
| 57 | C2.2 Veilleur : arbres | M | 56, 48 | — | 58, 39, 40 |
| 58 | C2.3 Veilleur : Unity | M | 56 | — | 57, 60, 40, 41 |
| 59 | C2.4 Veilleur : équilibrage | S | 57, 58 | Oui, court | 60, 41 |
| 60 | C3.1 Ombrelame : mécanique | L | 49, 56 | — | 58, 59, 41, 42 |
| 61 | C3.2 Ombrelame : arbres | M | 60, 48 | — | 62, 42, 43 |
| 62 | C3.3 Ombrelame : Unity | M | 60 | — | 61, 64, 43, 45 |
| 63 | C3.4 Ombrelame : équilibrage | S | 61, 62 | Oui, court | 64, 45 |
| 64 | C4.1 Tisse-Seuil : mécanique | L | 49, 60 | — | 62, 63, 68 |
| 65 | C4.2 Tisse-Seuil : arbres | M | 64, 48 | — | 66, 68, 69 |
| 66 | C4.3 Tisse-Seuil : Unity | M | 64 | — | 65, 69 |
| 67 | C4.4 Équilibrage des cinq classes | S | 55, 59, 63, 65, 66 | Oui, court | 71, 73 |
| 68 | P.1 Souvenir de la Rupture, court | M | 30 | Relecture, oui, court | 64, 65, 73 |
| 69 | P.2 Réveil et tutoriel | M | 68, 7 | Oui, court | 65, 66, 73 |
| 70 | P.3 Création du personnage | M | 69, 34, 66 | Oui, court | 71, 80 |
| 71 | P.4 Interface de l'alpha | M | 37 | — | 67, 70, 73, 77 |
| 72 | P.5 Touches, réglages, sons | M | 34, 71 | Oui, court | 74, 78 |
| 73 | B.1 Livres : les règles | L | 48 | — | 68, 69, 71 |
| 74 | B.2 Livres des cinq classes | M | 73, 53, 57, 61, 65 | — | 72, 77 |
| 75 | B.3 Trouver et acheter les livres | M | 74, 21, 2 | — | 78, 80 |
| 76 | B.4 Écran des livres | S | 75, 71 | Oui, court (groupé) | 79, 81 |
| 77 | M.1 Cheval : règles | M | 35 | — | 71, 74 |
| 78 | M.2 Cheval : modèle et animations | M | 77 | — | 72, 75 |
| 79 | M.3 Quête de la première monture | S | 78, 7 | Relecture, oui, court (groupé) | 76, 80 |
| 80 | G.1 Guildes simples | M | 37 | — | 70, 75, 79 |
| 81 | G.2 Canal de guilde | S | 80, 38 | Oui, court (groupé) | 76, 82 |
| 82 | A.1 Carnet d'essai, bouton d'aide | M | 35 | — | 81, 83, 84 |
| 83 | A.2 Statistiques sans nom | M | 35 | — | 82, 84 |
| 84 | A.3 PC minimum et fluidité | M | 66, 45 | — | 82, 83 |
| 85 | A.4 Marque, réseaux, Discord, entreprise | S | — | — | tous |
| 86 | A.5 Page Steam | S | 85 | — | tous |
| 87 | A.6 Testeurs « Pionnier de l'Alpha » | S | 85, 34, 44 | — | 82, 83, 84 |
| 88 | A.7 Répétition générale, ouverture | S | tous | **Partie complète** | — |

## Chemin critique et risques

**Le chemin critique** (ce qui ne peut pas se faire à côté) : fin de la vague 1 → vague 2 → part gardée des vagues 3 et 4 → lot 13 → premières minutes et création du personnage → livres, cheval, guildes → préparation → ouverture. Les **quatre classes ne sont pas sur ce chemin** si le second agent les fait pendant le lot 13 : c'est le plus gros gain de temps du plan (environ 20 chantiers retirés du chemin). Sans cela, la fourchette glisse d'environ un mois.

**Les cinq plus gros risques :**

1. **Le lot 13 et ses formats nouveaux.** Serveur de zone, base de données, solo sur le serveur et migration des sauvegardes sont des choix difficiles à défaire. Les PR à format nouveau demandent beaucoup de tours de revue (une trentaine pour le 11.4d). C'est le bloc le plus incertain : 4 à 8 semaines (sujet 20).
2. **Les quatre classes.** Chacune demande au moteur une mécanique que le Bastion n'a pas (soin, distance, furtivité, contrôle), ~14 compétences, trois arbres, des animations soumises au moteur et un équilibrage à 5 % près. Il faut aussi trouver des modèles et des animations gratuits qui tiennent le style « entre ESO et Fable ».
3. **Le contenu écrit et les relectures.** Cinq chapitres, une vingtaine de quêtes, quatre fiches de classes, le souvenir et la quête du cheval passent par Damien. Une relecture qui attend arrête les chapitres suivants, qui s'empilent.
4. **Les formats de sauvegarde et de règles en cascade.** Presque chaque chantier des Vallons touche la sauvegarde ou les règles ; les branches s'empilent et deux agents peuvent se gêner. Puis le lot 13 déplace toutes les sauvegardes sur le serveur : une erreur à ce moment ferait perdre des personnages.
5. **Les essais dans Unity et le PC minimum.** Unity ne tourne pas dans le cloud : chaque essai attend le PC de Damien. Le style réaliste (packs, ombres, effets) doit tenir 60 images par seconde sur le PC conseillé et rester jouable sur un PC de 2018, et le réglage peut arriver tard (A.3).

**Ce qui ferait gagner du temps :** les classes pendant le lot 13 avec un second agent ; les quatre fiches de classes relues tôt (décembre) ; les essais courts groupés dans une seule séance (B.4, M.3, G.2) ; l'éditeur interne (13.11) prêt avant les quêtes de l'alpha ; des relectures de Damien rendues sous 24 à 48 heures ; aucune idée nouvelle ajoutée à l'alpha (elles vont au lancement). Le périmètre le dit aussi : reporter une version simple (les livres de build d'abord) rendrait du temps, mais Damien ne l'a pas choisi.

**Ce qui ferait perdre du temps :** une PR à format nouveau qui enchaîne les tours de revue ; une idée « au max » ajoutée à l'alpha en route ; un modèle de classe ou de cheval introuvable en gratuit ; un hébergement qui tarde (décision ou mise en service) ; les fêtes de fin d'année ; deux agents sur le même format.

## Où Damien intervient

Dans l'ordre, avec le moment estimé (mêmes fourchettes que les jalons).

| Quand | Quoi | Sorte |
|---|---|---|
| Début novembre 2026 | Relire ce plan et répondre aux [questions](#questions-pour-damien) | Décision de Damien |
| Novembre 2026 | V1.2 : écran du personnage et boucliers (n° 1) | Essai court |
| Novembre 2026 | Chapitre 1 et ses quêtes (n° 7) | Relecture |
| Novembre 2026 | V1.4 : forge et réparation (n° 5) | Essai court |
| Novembre à début décembre 2026 | Chapitres 2 à 5 et leurs quêtes, un par un (n° 8 à 11) | Relectures |
| Dès que possible, avant la première vidéo publique | Marque à l'INPI, environ 230 € à confirmer (n° 85) ; e-mail du jeu, nom de domaine, réseaux et Discord créés par Damien | **Décision d'argent de Damien** |
| Fin novembre à mi-décembre 2026 | Partie complète de la vague 1 (n° 12), premiers testeurs amis | Essai complet |
| Décembre 2026 | Fiches des quatre classes (n° 46) | Relecture |
| Décembre 2026 à janvier 2027 | La nuit (n° 13), une attaque du hameau (n° 17), la vague 2 (n° 20) | Essais courts |
| Janvier 2027 | La crypte (n° 21), le Grand Solitaire (n° 24) | Essais courts |
| Mi-janvier à fin février 2027 | Partie complète des Vallons solo finis (n° 30) | Essai complet |
| Février 2027 | Serveur de test loué au mois et plafond de dépenses mensuel (n° 44) | **Décision d'argent de Damien** |
| Février à mars 2027 | Refonte des arbres et chiffres proposés (n° 48) | Décision de Damien |
| Février à mars 2027 | Roulade (n° 50), réseau à 150 ms (n° 32), groupe à deux (n° 37), écrire une quête dans l'éditeur (n° 41) | Essais courts |
| Mars à mai 2027 | Si un modèle de classe ne se trouve pas en gratuit (n° 54, 58, 62, 66) | **Décision d'argent de Damien**, au cas par cas |
| Mars à avril 2027 | Partie complète à plusieurs sur Internet (n° 45) | Essai complet |
| Mars à mai 2027 | Chaque nouvelle classe (n° 55, 59, 63), puis les cinq ensemble (n° 67) | Essais courts |
| Avril 2027 | Texte du souvenir de la Rupture (n° 68), les dix premières minutes (n° 69), la création (n° 70), les réglages (n° 72) | Relecture, essais courts |
| Avril 2027 | Place et coût des livres dans les arbres (n° 73) | Décision de Damien |
| Selon la question 3 | Ouverture de la page Steam et ses frais (n° 86) | **Décision d'argent de Damien** |
| Avril à mai 2027 | Si aucun cheval gratuit ne convient (n° 78) | **Décision d'argent de Damien** |
| Avril à mai 2027 | Texte de la quête du cheval (n° 79) ; livres, cheval et guilde en une séance (n° 76, 79, 81) | Relecture, essai court groupé |
| Mai 2027 | Choix et nombre des testeurs (n° 87) | Décision de Damien |
| Mai à juin 2027 | Répétition générale et ouverture de l'alpha (n° 88) | Essai complet, décision de Damien |
| Pendant l'alpha | Premier rendez-vous avec un comptable ou la CCI, avant toute vente (n° 85) | Décision de Damien |

Et pour chaque PR hors de la vague 1 : **l'accord de fusion**, PR par PR, tant que la [question 4](#questions-pour-damien) n'est pas réglée.

## Questions pour Damien

**Réponses de Damien (31 octobre 2026, vers 13 h 45)** : 1. les quatre classes **pendant le lot 13**, avec un second agent ; 2. le **Bastion passe aussi à environ 14 compétences** en alpha ; 3. la **page Steam s'ouvre pendant l'alpha**, avec de vraies images du jeu (frais de Steam : décision d'argent de Damien) ; 4. l'accord de fusion « CI verte et Codex sans remarque » **vaut aussi pour les vagues 2 à 4** ; **le lot 13 et les classes se fusionnent PR par PR**, avec l'accord explicite de Damien à chaque fois ; 5. ordre des classes : **Liturge de l'Aube, Veilleur, Ombrelame, puis Tisse-Seuil**.

Questions posées (gardées pour mémoire) :

1. **Les quatre classes : pendant le lot 13 ou après ?** Proposition de Claude : **pendant**, avec un second agent. Le moteur des classes et le serveur touchent des formats différents, et les classes sortent ainsi du chemin critique (environ un mois de gagné). Après le lot 13, c'est plus simple à suivre, mais l'alpha glisse vers juin ou après.
2. **Combien de compétences en alpha ?** Le périmètre propose environ 14 par classe (chiffre de Claude, pas encore une décision). Le plan le prend pour les quatre nouvelles classes. Le **Bastion** passe-t-il aussi à environ 14 (deux ou trois chantiers de plus), ou garde-t-il ses 6 compétences et son ultime pour l'alpha ?
3. **La page Steam : quand ?** Le sujet 17 dit « page Steam tôt » (pour la liste de souhaits) ; le périmètre la range au lancement. Proposition : la préparer pendant l'alpha (n° 86) et l'ouvrir quand des vidéos montrent le jeu, après la marque. Frais de Steam à décider au moment de l'ouvrir.
4. **Les fusions après la vague 1.** L'accord « fusion dès la CI verte et Codex sans remarque » vaut pour la vague 1. Damien l'étend-il aux vagues 2 à 4 ? Et pour le lot 13 et les classes, qui changent des formats difficiles à défaire, garde-t-il un accord PR par PR ?
5. **L'ordre des classes.** Proposition : Liturge de l'Aube d'abord (le soin sert aux groupes et au relevé en donjon), puis Veilleur, Ombrelame et Tisse-Seuil (du plus simple au plus nouveau pour le moteur). Un autre ordre ?
