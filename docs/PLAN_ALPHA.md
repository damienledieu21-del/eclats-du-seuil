# Plan jusqu'à l'alpha — chantiers numérotés dans l'ordre

Plan de Claude du 31 octobre 2026, **relu par Damien** : ses réponses aux 5 questions (31 octobre 2026, vers 13 h 45) sont reprises dans le corps du plan ; une question reste ouverte. Il range dans l'ordre tout ce que le [périmètre](PERIMETRE.md) met avant l'ouverture de l'alpha fermée sur PC. Il ne change aucune décision : les choix viennent de la [feuille de route](FEUILLE_DE_ROUTE.md) (sujets 1 à 54), du [périmètre](PERIMETRE.md) (et de ses [10 points tranchés](PERIMETRE.md#points-tranchés-par-damien-31-octobre-2026-1-h-20)), du [plan des Vallons](PLAN_VALLONS_SOLO.md) et du [cadrage](CADRAGE.md). Ce qui demande un choix nouveau est dans les [questions pour Damien](#questions-pour-damien).

## En tête

**Comment lire ce plan.** Le plan est une liste de **90 chantiers**, numérotés de 1 à 90. Un chantier, c'est un sous-lot : une branche, une PR, de la taille du V1.1 ou du 12.3. Le numéro donne l'**ordre de départ** de l'agent principal. Pour le bloc 5, c'est décidé (réponse 1 de Damien, 31 octobre 2026) : pendant que l'agent principal fait le lot 13 (n° 31 à 45), le **second agent** prend les chantiers des classes (n° 46 à 69), dans leur ordre ; C0.1 à C0.3 peuvent commencer plus tôt (voir le tableau). Ailleurs, un second agent peut prendre en même temps un chantier plus loin dans la liste, quand la colonne « Parallèle possible » du [tableau](#tableau-récapitulatif) le permet. Chaque chantier a un identifiant (V1.x, V2.x, V3.x, V4.x, 13.x, C0.x à C4.x, P.x, B.x, M.x, G.x, A.x), un titre et une taille : **S** (petit, un ou deux jours), **M** (moyen), **L** (gros, souvent un format nouveau et beaucoup de tours de revue). Les tailles comparent les chantiers entre eux ; ce ne sont pas des dates. Sous chaque chantier, quelques lignes disent ce qu'il fait, ce dont il dépend, ce qui change de format ou de règles, et s'il y a un **essai de Damien** dans Unity. Une **décision d'argent de Damien** ou une **décision de Damien** est toujours écrite en clair.

**Les règles de la méthode.** Elles ne changent pas ([CLAUDE.md](../CLAUDE.md#méthode-pour-un-sous-lot)) : **une PR par chantier**, en brouillon tant qu'elle n'est pas prête ; tests touchés verts en local, puis la vérification complète par la CI ; une **relecture indépendante** par un agent neuf, une seule par PR, avant la **revue Codex** (c'est la pratique décidée par Damien pour chaque PR depuis la vague 1 ; [CLAUDE.md](../CLAUDE.md#méthode-pour-un-sous-lot) la demande à partir du lot 13) ; la revue Codex sur chaque PR ; **fusion selon les accords de Damien** :

- **vagues 1 à 4** (n° 1 à 30) : chaque PR est fusionnée dès que sa CI est verte et que Codex n'a aucune remarque (vague 1 : sujet 18, 29 octobre 2026, 21 h 01 ; vagues 2 à 4 : réponse 4 du 31 octobre 2026) ;
- **lot 13 et classes** (n° 31 à 69) : fusion **PR par PR**, avec l'accord explicite de Damien à chaque fois (réponse 4) ;
- **blocs 6 à 8** (P, B, M, G, A ; n° 70 à 90) : **même règle que les vagues** : fusion dès que la CI est verte et que Codex n'a aucune remarque (réponse de Damien à la [question 6](#questions-pour-damien), 31 octobre 2026) ; les démarches qui engagent Damien (entreprise, marque, page Steam) restent à lui.

Ces accords sont notés dans la [feuille de route](FEUILLE_DE_ROUTE.md) (sujet 18 pour la vague 1) et dans ce plan (réponses de Damien, dans les [questions](#questions-pour-damien)). Un changement de format (sauvegarde, règles, histoire, protocole) est toujours versionné, documenté et testé, avec de nouveaux journaux de référence figés ; les anciens ne changent jamais. Deux agents ne travaillent jamais en même temps sur le même format.

**Ce qui est fait, ce qui est en cours.** Fusionnés : **V1.1** monnaie, marchands et voyage rapide (PR #72), **V1.5** écran des compétences (PR #71), les **tests plus rapides** (PR #73), les **réponses de Damien** aux chantiers V1.5 et V1.1 (PR #74 : réserve des compétences, place vide, marchands fermés la nuit, journée de 3 h), la feuille de route et le périmètre (PR #70), **V1.2** équipement complet (PR #75, fusionnée le 31 octobre 2026, `26c3e59`, sauvegarde v7). En cours : **V1.6** moteur des quêtes (PR #76, brouillon, passée en sauvegarde v8) et **V1.3** effets d'équipement (commencé, sauvegarde v9 prévue). Avant eux, les lots 8 à 12, l'essai de style, les intérieurs et la caméra sont faits.

**Les grands jalons et leurs dates estimées.** Ce sont des fourchettes honnêtes, **pas des promesses** (sujet 20 : objectifs souples, aucune date annoncée au public tant qu'elle n'est pas sûre). Elles comptent environ un chantier par jour, revues comprises, deux agents en parallèle quand c'est possible, les relectures et les essais de Damien, et les jours sans travail.

| Jalon | Chantier qui le marque | Fourchette |
|---|---|---|
| J1. Vague 1 finie, Vallons « vrai jeu » en solo | 12 (V1.10, essai complet) | fin novembre à mi-décembre 2026 |
| J2. Vallons solo finis (vagues 2 et part gardée des vagues 3 et 4) | 30 (V4.4, essai complet) | mi-janvier à fin février 2027 |
| J3. Monde partagé jouable sur Internet | 45 (13.15, essai complet à plusieurs) | mars à avril 2027 |
| J4. Cinq classes jouables, le Bastion à ~14 compétences | 69 (C4.4) | avril à mai 2027 |
| J5. Ouverture de l'alpha fermée sur PC | 90 (A.7) | **avril à juin 2027, mai-juin le plus probable** |

Comme dans le [calcul de charge](PERIMETRE.md#calcul-de-charge), **fin février 2027 est le plus tôt possible**, sans aucun retard : il ne faut pas y compter. Mars 2027 ne paraît pas tenable.

## Le compte des chantiers

| Bloc | Chantiers | Le périmètre en comptait |
|---|---|---|
| 1. Fin de la vague 1 (V1.2 à V1.10) | 12 (n° 1 à 12) | ~8 |
| 2. Vague 2, le monde vivant (V2.1 à V2.8) | 8 (n° 13 à 20) | ~8 |
| 3. Part des vagues 3 et 4 gardée avant le lot 13 (V3.1 à V3.6, V4.1 à V4.4) | 10 (n° 21 à 30) | ~10 |
| 4. Lot 13, monde partagé, avec l'éditeur interne (13.1 à 13.15) | 15 (n° 31 à 45) | ~12 (sans les guildes simples, comptées au bloc 7) + 2 pour l'éditeur |
| 5. Les quatre nouvelles classes et le Bastion à ~14 compétences (C0.1 à C0.8, C1 à C4) | 24 (n° 46 à 69) | ~20 + 2 (caractéristiques et refonte des arbres, comptées avec les premières minutes) |
| 6. Premières minutes, création du personnage, interface de l'alpha (P.1 à P.5) | 5 (n° 70 à 74) | ~4 (les ~6 moins les 2 passés au bloc 5) |
| 7. Livres de build, cheval simple, guildes simples (B.1 à B.4, M.1 à M.3, G.1 et G.2) | 9 (n° 75 à 83) | ~4 + ~3 + ~2 |
| 8. Préparation de l'alpha (A.1 à A.7) | 7 (n° 84 à 90) | ~4 (les ~6 moins les 2 de l'éditeur) |
| **Total** | **90** | **environ 80** (79 en additionnant les lignes) |

**Pourquoi 90 et pas 80.** Le total dépasse de 11 chantiers. Deux viennent d'une décision de Damien (le Bastion à ~14 compétences) ; les neuf autres n'ajoutent aucune décision : ils rendent visible un travail que le calcul de charge avait mis dans d'autres lignes ou pas compté :

- **+2** : le **Bastion passe aussi à environ 14 compétences** en alpha (réponse 2 de Damien, 31 octobre 2026) : ses compétences 7 à 14 (C0.7), puis Unity et son équilibrage (C0.8). Sa fiche est faite avec les quatre autres (C0.1).
- **+4** : le V1.9 devient **cinq PR de textes**, une par chapitre, puisque Damien relit chapitre par chapitre ([plan des Vallons](PLAN_VALLONS_SOLO.md#6-questions-pour-damien), question 5). Ce sont de petits chantiers.
- **+3** : l'interface de l'alpha (P.4), les touches et les sons (P.5) et le PC minimum (A.3) sont rangés « Alpha » dans le périmètre (sections 8 et 10) mais n'avaient pas de ligne dans le calcul.
- **+2** : la marque, le Discord et la page Steam (A.4 et A.5) sont surtout des **démarches de Damien**, guidées par Claude, avec peu de code.
- **+1** : l'essai à plusieurs sur Internet et la tenue de charge (13.15).
- **−1** : le souvenir, le réveil et la création tiennent en 3 chantiers (P.1 à P.3) au lieu des ~4 qui restent au calcul une fois les caractéristiques et la refonte des arbres passées au bloc des classes.

Sans les deux démarches, cela fait **88 sous-lots de code, environ 9 de plus que le calcul**. Avec deux agents, l'écart pèse un peu plus d'une semaine ; les deux chantiers du Bastion sont sur la voie du second agent (bloc 5), hors du chemin critique. La fourchette de l'alpha ne bouge pas, mais la marge de la voie des classes diminue (voir le [risque 2](#chemin-critique-et-risques)).

## Bloc 1 — Fin de la vague 1 (V1.2 à V1.10)

Le détail de chaque chantier est dans le [plan des Vallons, section 3](PLAN_VALLONS_SOLO.md#3-les-dix-sous-lots). Ici, seulement l'essentiel et l'ordre.

### 1. V1.2 — Équipement complet : places, boucliers, écran (L)

- **Fusionné** le 31 octobre 2026 (PR #75, `26c3e59`) : 18 places, Bastion toujours avec un bouclier, boucliers qui changent la garde, catalogue des niveaux 1 à 20, écran du personnage avec la comparaison claire.
- Dépend de V1.1 (fait).
- Formats : règles et moteur (garde du bouclier, nouveau journal figé), catalogue des objets, **sauvegarde v7**.
- Essai : oui, court (écran du personnage, boucliers).

### 2. V1.6 — Quêtes : le moteur (M)

- **En cours** (PR #76, brouillon) : journal à deux onglets, objectifs `talk` et `deliver`, effets d'or et de points de guilde, récompenses au choix, PNJ des intérieurs qui parlent, bandits, chef des bandits, loup blanc.
- Dépend de V1.1 (fait).
- Formats : histoire en nouvelle version, validateur étendu, **sauvegarde v8** (quête suivie, étape `reward`). La PR #76 visait d'abord la même version que la #75 ; elle est passée de v7 à v8 après la fusion de la #75 : la collision est réglée ainsi.

### 3. V1.3 — Effets d'équipement : armes, bijoux, ensembles, propriétés tirées (L)

- Effets par les déclencheurs d'origine `equipment`, ensembles de 2, 4 et 6 pièces, objets nommés, propriétés tirées au hasard par la clé de la mort.
- **Commencé.** Dépend de V1.2 (fait).
- Formats : catalogue des objets, instance d'objet dans la sauvegarde (**v9 prévue**, après la v8 du V1.6, pour ne pas refaire la collision de #75 et #76), règles et empreinte, nouveaux journaux figés.

### 4. V1.7 — Guilde des aventuriers (M)

- La guilde au bourg, carte d'aventurier, rangs G, F et E, points, épreuves de promotion, contrats du jour, boutique et titre.
- **En cours** (PR en brouillon, 31 octobre 2026) : fait ; histoire en schéma 4, marchands en schéma 2, **sauvegarde v10** (la v9 est celle de V1.3, PR #78).
- Dépend de V1.6.
- Formats : données de la guilde (nouveau fichier), sauvegarde (carte), textes par clés.

### 5. V1.4 — Forge, qualité, usure (M)

- Recettes d'équipement à la forge de Maëlle, qualité normale, supérieure ou chef-d'œuvre signé, petit jeu de forge à trois frappes, recette qui peut rater ; usure selon les dégâts reçus, réparation contre de l'or.
- Dépend de V1.1 et V1.3.
- Formats : fabrication, sauvegarde (qualité, usure).
- Essai : oui, court (forge, réparation).

### 6. V1.8 — Progression et équilibrage (M)

- Niveaux jusqu'à 25 dans les Vallons (plafond qui monte, sujet 54 : 25 en alpha), données jusqu'à 30 ; 5e et 6e places de la barre (réponses de Damien du 30 octobre 2026) ; zones en mélange avec niveau minimum ; difficulté normale vérifiée par des combats simulés pour chaque chapitre ; rythme rapide puis plus lent.
- Trois places de bonus (plat, élixir, huile), à côté des fioles (périmètre, section 5, sujet 36) ; les plats arriveront avec la cuisine, après l'ouverture de l'alpha (point 3). **Fait dans V1.8c** (en PR) : élixirs et huiles en recettes, place du plat prête et testée, sauvegarde v12 ; effet à la prochaine entrée dans une session ([MONDE.md](MONDE.md#les-trois-places-de-bonus-sous-lot-v18c)).
- Dépend de V1.2 à V1.7.
- Formats : progression, zones (niveau minimum), pont temps réel et **protocole réseau** (deux places de plus), sauvegarde (bonus en cours) et règles si besoin, nouveaux journaux figés.

### 7. V1.9.1 — Chapitre 1 « Les traces sous le moulin » et ses quêtes (S)

- La mission d'aujourd'hui reprise en chapitre 1 (niveaux 1 à 5), avec le court souvenir du premier Éclat ; quêtes secondaires proposées : « La ferrure de rechange », « Le pain du matin », « Les flèches perdues ».
- Avant les textes : le **guide d'écriture**, le **lexique d'Orsédia** et la **fiche de chaque PNJ important** (sujet 51, rangés « Vallons » dans le périmètre, section 2), documentation relue par Damien avec le chapitre 1.
- Dépend de V1.6 et V1.8 (comme le V1.9 du [plan des Vallons](PLAN_VALLONS_SOLO.md#8-ordre-de-travail) : « V1.6 à V1.8 »). Textes proposés par Claude ; chaque quête jouée de bout en bout par un pilote.
- Formats : histoire et textes (pas de nouvelle version attendue).
- **Relecture de Damien** : chapitre 1.
- **État** (5 novembre 2026, en PR) : fait ([MONDE.md](MONDE.md#chapitre-1--les-traces-sous-le-moulin--et-ses-quêtes-sous-lot-v191), [ECRITURE.md](ECRITURE.md)), sans changement de format ; textes à relire.

### 8. V1.9.2 — Chapitre 2 « Le bourg d'Aube » et ses quêtes (M)

- Sœur Albe, Renart, les vols d'éclats, l'inscription à la guilde (niveaux 5 à 10) ; quêtes proposées : « Une dette à l'auberge », « Les cloches de la chapelle », « La dernière lanterne », « Le puits qui murmure », « L'apprentie de Maëlle », « Herbes pour la chapelle ».
- Dépend de V1.7 (guilde), V1.4 (forge pour l'apprentie), V1.8 (niveaux) et V1.9.1.
- **Relecture de Damien** : chapitre 2 (avec les noms et répliques des marchands, comme décidé le 30 octobre 2026).
- **État** (5 novembre 2026, en PR) : fait ([MONDE.md](MONDE.md#chapitre-2--le-bourg-daube--et-ses-quêtes-sous-lot-v192), [ECRITURE.md](ECRITURE.md)), sans changement de format ; textes à relire.

### 9. V1.9.3 — Chapitre 3 « Les champs et le hameau » et ses quêtes (M)

- Attaque du hameau par les bandits, piste jusqu'au lac (niveaux 10 à 15) ; quêtes proposées : « Les sangliers des champs », « La lettre au fond du lac », « Les vergers pillés », « Les potions du hameau », « Le pont des Vallons ».
- Dépend de V1.8 (niveaux) et V1.9.2.
- **Relecture de Damien** : chapitre 3.
- **État** (5 novembre 2026, en PR) : fait ([MONDE.md](MONDE.md#chapitre-3--les-champs-et-le-hameau--et-ses-quêtes-sous-lot-v193), [ECRITURE.md](ECRITURE.md)), sans changement de format (contenus seulement, dont trois bandits de plus dans la zone des Vallons), avec l'étal de Renart ; le tutoriel de Colombe à qualité attend une question à Damien ; textes à relire.

### 10. V1.9.4 — Chapitre 4 « La tour et le camp » et ses quêtes (M)

- La tour qui ne répond plus, le chef des bandits et les Rassembleurs, le premier choix (l'épargner ou non, écrit dans les faits) (niveaux 15 à 20) ; quêtes proposées : « La tour silencieuse », « Le déserteur », « La prime du Borgne », « Le marchand qui en savait trop ».
- Dépend de V1.9.3.
- **Relecture de Damien** : chapitre 4.
- **État** (5 novembre 2026, en PR) : fait ([MONDE.md](MONDE.md#chapitre-4--la-tour-et-le-camp--et-ses-quêtes-sous-lot-v194), [ECRITURE.md](ECRITURE.md)), sans changement de format (histoire, textes et objets seulement ; aucune créature ajoutée) ; le premier choix (épargner le chef des bandits ou non) est écrit dans les faits ; textes et questions à relire.

### 11. V1.9.5 — Chapitre 5 « Le Seuil » et ses quêtes (M)

- Le rôdeur du Seuil, le Gardien et sa forme éveillée, le retour chez Odile, la voix de l'Envers (niveaux 20 à 25) ; quêtes proposées : « Le loup blanc », « Les pierres gravées ».
- Dépend de V1.9.4.
- **Relecture de Damien** : chapitre 5. La répartition des vingt quêtes entre les chapitres est une proposition ; la liste est celle du [plan des Vallons](PLAN_VALLONS_SOLO.md#4-la-quête-principale-et-les-quêtes-secondaires).
- **État** (5 novembre 2026, en PR) : fait ([MONDE.md](MONDE.md#chapitre-5--le-seuil--et-ses-quêtes-sous-lot-v195), [ECRITURE.md](ECRITURE.md)), sans changement de format (sauvegarde v12 : l'avancement de l'ancienne mission est converti à la lecture) ; la fin de l'ancienne mission (manifestation, rôdeur, Gardien, retour chez Odile) est passée du chapitre 1 au chapitre 5 ; « Le loup blanc » (le loup blanc de « La bête blanche » qui revient) et « Les pierres gravées » ; un lieu « cœur du Seuil » met le Gardien au niveau 23 au moins ; aucune créature ajoutée. **Avec lui, le V1.9 est fini** : les cinq chapitres et les vingt quêtes secondaires du plan des Vallons (en plus de celles d'avant : les peaux pour l'auberge, la bête blanche, les registres de l'archiviste et les épreuves de la guilde), textes et questions à relire.

### 11 bis. V1.9.6 — Chapitre 1 enrichi : l'ouverture, le souvenir jouable, Croc-Gris, les indices (M)

- Demande de Damien du 5 novembre 2026, avant son essai complet sous Unity : un chapitre 1 « Les traces sous le moulin » beaucoup plus riche, six idées choisies. **PR A1** (cœur et données) : les trois premières et les indices du mystère ; **PR A2** : quatre ou cinq quêtes de plus et le village vivant ; **PR B** : la présentation sous Unity.
- **État** (5 novembre 2026, PR A1 en brouillon) : fait ([MONDE.md](MONDE.md#chapitre-1-enrichi--louverture-le-souvenir-jouable-croc-gris-sous-lot-v196), [ECRITURE.md](ECRITURE.md)) : l'ouverture « Le réveil » (tutoriel : marcher, frapper, se garder, parer, boire), le souvenir de la Rupture en scène jouée (une petite zone où l'on est Galeran, texte de repli gardé), Croc-Gris (chef de la meute du pré, deux formes), trois indices ; **histoire en schéma 6** (objectif `practice`, scènes), sauvegarde v12 inchangée.
- **V1.9.9** (« chapitre 1 au max, le contenu », PR en brouillon) : Brunehilde quand Bérenger envoie le joueur, la poussière d'écho (butin des échos, vente, remède de l'aube), le village qui se souvient (Croc-Gris, garde de nuit, chat, puits), le second temps fort du souvenir (les Voix de la Rupture) : [MONDE.md](MONDE.md#chapitre-1-au-max-le-contenu-sous-lot-v199).
- **PR B** (sous-lot V1.9.8, en brouillon) : la présentation sous Unity (réveil, bulles du tutoriel, souvenir joué avec son ambiance, Croc-Gris, silhouette, pierre, collier, « Quête passée ») ; à vérifier à l'écran par Damien ([UNITY.md](UNITY.md#chapitre-1--louverture-et-le-souvenir-jouable-sous-lot-v198-5-novembre-2026)).

### 12. V1.10 — Places des maisons et essai de la vague 1 (S)

- Places des maisons du bourg agrandies (6 à 8 m), contrôles des Vallons agrandis.
- Outils d'essai (périmètre, section 13, sujet 18) : **sauvegarde du dossier des ressources** Unity et bouton **« Préparer et lancer l'essai »**, prêts pour la partie complète.
- Dépend de tout le bloc 1.
- Formats : zone des Vallons.
- Essai : **la partie complète** (vague 1, de bout en bout, captures, avis, puis corrections). Premiers testeurs amis possibles (sujet 18 : testeurs dès la vague 1).
- **État** (5 novembre 2026, PR en brouillon) : maisons du bourg de toutes tailles (petites, moyennes et grandes, choix de Damien ; `V110BourgTests`) ; murs transparents à 25 % retenus par Damien ; les cinq défauts de l'essai 10 sous Unity corrigés (intérieur sombre, poutres, murs en fondu, tache blanche du feu, fumée noire de la forge : [UNITY.md](UNITY.md#essai-v110--maisons-du-bourg-agrandies-et-les-5-défauts-de-lessai-10-5-novembre-2026)) ; liste de vérification de l'essai : [ESSAI_V1.10.md](ESSAI_V1.10.md). V1.10.1 : menus « Sauvegarder le dossier des ressources » et « Préparer et lancer l'essai » ajoutés (code écrit, compilation CI ; à essayer sous Unity) et tests de conversion des sauvegardes aux étapes `enquete`, `traces`, `passage` et au suivi de quête : **V1.10 complète côté code, reste l'essai de Damien**. Suites notées : une réplique de Brunehilde à l'inscription à la guilde quand Bérenger y envoie le joueur (choix de Damien du 6 novembre 2026 ; **faite au sous-lot V1.9.9**, PR en brouillon : [MONDE.md](MONDE.md#chapitre-1-au-max-le-contenu-sous-lot-v199)) ; réponse aux indices de la vieille porte (à résoudre plus tard) ; compagnon d'affichage (Pain-d'Épice), après l'essai (choix de Damien du 6 novembre 2026, PR #100).

## Bloc 2 — Vague 2, le monde vivant (V2.1 à V2.8)

Contenu décidé le 29 octobre 2026 (« Vallons plus vivants » et « Les Vallons en très grand », [plan des Vallons, section 7](PLAN_VALLONS_SOLO.md#7-après-la-vague-1)). Les identifiants V2.x sont proposés ici.

**Nom des sous-lots** (relecture de la PR #113, 7 novembre 2026) : le lot de recherche de chemin (PR #113, accord de Damien du 7 novembre 2026) s'appelle **« V2.8 bis — Recherche de chemin »** dans ce plan, pour ne pas se confondre avec le chantier n° 20 « V2.8 — Fêtes, stock changeant, essai » ; ses commits et sa documentation (MONDE, MOTEUR) le nomment « V2.8 ». Aucun autre identifiant n'est renommé.

### 13. V2.1 — Jour et nuit qui changent le monde (M)

- La journée de 3 h et la nuit de 22 h à 5 h (déjà dans le jeu, PR #74) changent le monde : lanternes allumées, villageois rentrés, créatures de nuit, nuit bleue et lisible (sujets 32 et 49).
- Dépend de V1.10.
- Formats : zone (populations selon l'heure).
- Essai : oui, court (la nuit).
- **État** (6 novembre 2026, PR en brouillon) : zones en schéma 8 (schéma 7 encore lu) ; sept loups de la nuit et quatre échos de 22 h à 5 h, sangliers qui dorment la nuit, huit villageois devant leur porte la nuit (toujours joignables) ; nuit bleue et lisible, lanternes, réverbères, fenêtres allumées, feux follets, hiboux sous Unity ; menu « Essai de la nuit » ([MONDE.md](MONDE.md#jour-et-nuit-qui-changent-le-monde-sous-lot-v21), [UNITY.md](UNITY.md#essai-v21--jour-et-nuit-qui-changent-le-monde)). Reste l'essai de Damien sous Unity.

### 14. V2.2 — Météo (S)

- Pluie, brume du matin, tirées de façon déterministe par le jour du monde ; d'abord pour l'ambiance, sans changer les règles du combat.
- Dépend de V2.1.
- Formats : temps du monde (sauvegarde du monde si besoin).
- **État** (6 novembre 2026, PR en brouillon) : temps recalculé du jour du monde et de la zone (aucun changement de sauvegarde, version 12) : clair, nuageux, brume du matin, pluie fine, averse, orage rare (éclairs et tonnerre pour l'ambiance), tranches de 3 h du monde fondues en douceur ; histoire en schéma 8 (condition `weather`, schéma 7 encore lu), seize répliques chez onze villageois ; pluie, éclaboussures, flaques, brume, ciel couvert, éclairs et tonnerre (synthèse sans fichier) sous Unity ; menu « Essai de la météo » ; populations inchangées (plus d'échos par brume : avec V2.4, réponse de Damien du 6 novembre 2026) ([MONDE.md](MONDE.md#météo-sous-lot-v22), [UNITY.md](UNITY.md#essai-v22--météo)). Reste l'essai de Damien sous Unity.

### 14 bis. V2.2.1 — Compagnon d'affichage : Pain-d'Épice (S)

- Décision de Damien du 6 novembre 2026 (PR #100) : le chat de Rosalie suit le joueur tant que l'objet est au sac ; ni combat ni sauvegarde.
- Formats : aucun.
- **État** (6 novembre 2026, PR en brouillon) : mécanisme générique « compagnon lié à un objet » (`Companions`, `CompanionFollower`, testés) ; chat en formes simples qui suit le chemin du joueur, s'assoit, fait sa toilette, ronronne, se couche, part en rapetissant ; menu « Essai du compagnon » ([MONDE.md](MONDE.md#compagnon-daffichage--pain-dépice-sous-lot-v221), [ESSAI_V1.10.md](ESSAI_V1.10.md#7-v221--pain-dépice-suit-le-joueur-compagnon-daffichage)). Reste l'essai de Damien sous Unity.

### 15. V2.3 — Villageois qui suivent leur journée (M)

- Emploi du temps des PNJ importants (travail, taverne, maison), qui se retrouvent au bon endroit ; dialogues qui en tiennent compte.
- Dépend de V2.1 et V1.6.
- Formats : histoire (PNJ en nouvelle version).
- **État** (6 novembre 2026, PR en brouillon) : histoire en schéma 9 (emplois du temps `schedule` des PNJ, abri sous l'averse, condition `activity`, répliques `lines` d'un nœud selon le moment ; schémas 4 à 8 encore lus), sauvegarde inchangée (version 12) ; vingt-sept PNJ ont leur journée (dont ceux des intérieurs, dans leur pièce ; réponses de Damien du 6 novembre 2026) (Firmin au fournil la nuit, au marché à midi, à la sieste l'après-midi, à la taverne le soir…), soixante-deux répliques du moment, la boutique d'un marchand parti dit où il est ; passants du bourg (un sur trois par course) qui vont au puits, rentrent manger ou viennent devant l'auberge ; sous Unity, activité sous le nom des PNJ et menu « Essai de la journée du village » ([MONDE.md](MONDE.md#villageois-qui-suivent-leur-journée-sous-lot-v23), [UNITY.md](UNITY.md#essai-v23--la-journée-du-village)). Reste l'essai de Damien sous Unity.

### 15 bis. V2.3.1 — PNJ qui changent de lieu (M)

- Accord de Damien du 7 novembre 2026 : lever la limite « un PNJ vit dans une seule zone » (réponse du 6 novembre : la taverne sur la terrasse pour l'alpha, « plus tard une salle pleine le soir » ; Rosalie ne pouvait pas sortir).
- Formats : histoire en schéma 10 (zone d'un créneau) ; sauvegarde inchangée.
- **État** (7 novembre 2026, PR en brouillon) : un créneau peut placer le PNJ dans une autre zone (intérieur ↔ dehors), validé au chemin précis (zone inconnue, porte manquante) ; le cœur suit le PNJ (parler, livrer, commercer, repères de quête), il finit la conversation avant de partir ; contenu 0.19.0 : la salle de l'auberge se remplit le soir et à midi, Rosalie sort (chapelle, puits, place), Mathurin fume sur la terrasse, Frère Anselme fait sa tournée ; sous Unity, le PNJ marche jusqu'à la porte, disparaît et réapparaît de l'autre côté ; menu « Essai des PNJ qui changent de lieu » ([MONDE.md](MONDE.md#pnj-qui-changent-de-lieu-sous-lot-v231), [ESSAI_V1.10.md](ESSAI_V1.10.md#13-les-pnj-qui-changent-de-lieu-v231)).

### 16. V2.4 — Animaux paisibles et bêtes qui se battent (M)

- Animaux paisibles (cerfs, lièvres), loups et sangliers qui se battent entre eux, premier écosystème des Vallons.
- Dépend de V1.10.
- Formats : **règles et moteur** (créatures hostiles entre elles), zone, nouveaux journaux figés.
- **Suite de V2.2** (réponse de Damien du 6 novembre 2026) : plus d'échos et de loups par brume, avec une zone en schéma 9 (populations selon le temps), sans gêner les quêtes ni l'étoile des chasses.
- **État** (6 novembre 2026, PR en brouillon) : quatre équipes de créatures avec des **trêves** (la meute et le gibier se battent ; bandits et échos à part), coups réduits de moitié entre bêtes, une bête ne court qu'après une bête qu'elle voit ; **six cerfs** (aube et jour, abrités sous l'averse) et **quatre lièvres** (crépuscule et nuit) qui **fuient** en harde ; **trois loups gris et deux échos de la brume** (zones en schéma 9, `weather`) ; une bête tuée par une autre ne donne rien ; formes simples sous Unity et menu « Essai des animaux ». Moteur 0.28.0, journal 22, scénarios 16, règles 15 (0.16.0), instantané 5, nouvelle référence figée ; sauvegarde inchangée (12) ([MONDE.md](MONDE.md#animaux-paisibles-et-bêtes-qui-se-battent-sous-lot-v24)). Reste l'essai de Damien sous Unity.

### 16 bis. V2.4.1 — Bêtes qui se promènent (S)

- Réponse de Damien du 6 novembre 2026 (question 8 de la PR #104) : « un petit lot du moteur, errance déterministe ». Validé par Damien le 7 novembre 2026.
- Dépend de V2.4.
- Formats : moteur, journal, règles, scénarios, instantané ; nouvelles références figées.
- **État** (7 novembre 2026, PR en brouillon) : moteur 0.29.0, journal au format 23, scénarios en schéma 17, règles en schéma 16 (contenu 0.17.0), instantané au format 6 ; errance sans état ni tirage du générateur du combat ; cerfs qui broutent en harde derrière leur chef, lièvres qui sautillent par à-coups, loups qui patrouillent en file sur un circuit, sangliers qui fouillent ; retour dans l'aire, calme après 15 s et retour chez soi ; ni bagarre sans raison, ni vie de session consommée, ni bête qui vient d'elle-même sur un point sûr ou un PNJ ; créatures des événements sans errance ; têtes qui broutent sous Unity ; menu « Essai des bêtes qui se promènent » ([MONDE.md](MONDE.md#bêtes-qui-se-promènent-sous-lot-v241)). Reste l'essai de Damien sous Unity.

### 17. V2.5 — Événements du monde : attaque du hameau, faille du Seuil (L)

- Événements déclenchés par le temps du monde, de façon déterministe, avec une récompense donnée une seule fois par personnage (registre).
- Dépend de V2.1 et V1.9.3 (le hameau).
- Formats : monde (nouveau bloc d'événements), sauvegarde (registre).
- Essai : oui, court (une attaque du hameau).
- **État** (6 novembre 2026, PR en brouillon) : zones en schéma 10 (bloc `events`, schémas 9, 8 et 7 encore lus), sauvegarde v13 (registre des événements du personnage, v12 encore lue) ; moteur, règles, journal et scénarios inchangés (nouveau journal figé du scénario `evenement_hameau`). **L'attaque du hameau** (un soir sur deux environ, bandits ou loups, annonce 1 h 30 avant, cloche, villageois au refuge, récompense de la première défense donnée une seule fois, remerciements de Josse et d'Aubine, fumée et débris une journée après un pillage), **la faille du Seuil** (aux ruines, au lac ou dans le pré, trois vagues d'échos, à refermer avec la touche d'action), **la chasse de l'aube** (deux loups contre les sangliers des champs, sans mort ni registre) ; menu « Essai des événements » ([MONDE.md](MONDE.md#événements-du-monde-sous-lot-v25), [ESSAI_V1.10.md](ESSAI_V1.10.md#10-les-événements-du-monde-v25)). Reste l'essai de Damien sous Unity.

### 18. V2.6 — Le Seuil qui grandit (M)

- Des zones se corrompent au fil du temps, des failles à refermer, des couleurs qui se fanent près des failles (sujet 49).
- Dépend de V2.5.
- Formats : monde, sauvegarde (état des failles).
- **État** (7 novembre 2026, PR en brouillon) : zones en schéma 11 (cicatrice d'une faille, échos rôdeurs ; schéma 10 encore lu), sauvegarde v14 (cicatrices ; v13 encore lue) ; moteur, règles, journal et scénarios inchangés. Une faille restée ouverte (là ou pendant l'absence) laisse une **cicatrice** à sa place : palier 1, un de plus tous les 2 jours du monde jusqu'au 4, rayon de 8 à 20 m ; **couleurs fanées et brume froide** autour (Unity), échos de la faille plus forts (+15 % par palier), **échos rôdeurs** à partir du palier 2 (un par palier, de retour 2 h du monde après leur mort, hors de la réserve des quêtes) ; une faille refermée à sa place la fait **reculer** d'un palier et arrête les autres de grandir ; un **éclat de Seuil** posé en son cœur la **purifie** (200 points) ; Sauge, Sœur Albe et Firmin en parlent ; menu « Essai du Seuil qui grandit » ([MONDE.md](MONDE.md#le-seuil-qui-grandit-sous-lot-v26), [ESSAI_V1.10.md](ESSAI_V1.10.md#11-le-seuil-qui-grandit-v26)). Reste l'essai de Damien sous Unity.

### 18 bis. V2.6.1 — Suites du Seuil (S)

- Propagation d'une cicatrice au lieu voisin et quête de Sœur Albe pour les purifier ; borne de 16 répliques par PNJ montée (réponses de Damien du 7 novembre 2026 aux questions 5 et 6 de la PR #110 ; accord du 7 novembre : « fais tout et cherche le mieux possible »).
- Dépend de V2.6.
- Formats : zones (schéma 12), histoire (schéma 11) ; sauvegarde v14 inchangée.
- **État** (7 novembre 2026, PR en brouillon) : une cicatrice au palier 4 depuis 2 jours du monde fait naître une cicatrice chez sa voisine (ruines vers le lac puis le pré ; lac et pré vers les ruines), recalculé du temps du monde, borné ; quête « Les cicatrices du Seuil » (purifier une cicatrice, revenir voir Sœur Albe, en purifier deux autres ; chapelet de l'Aube) ; Sœur Albe veille sur le parvis de 17 h à 18 h ; Gaspard, Odile, Aldric et le capitaine en parlent ; menus d'essai ([MONDE.md](MONDE.md#suites-du-seuil-sous-lot-v261), [ESSAI_V1.10.md](ESSAI_V1.10.md#14-les-suites-du-seuil-v261)). Reste l'essai de Damien sous Unity.

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
- Clé `character/skill/<compétence>` (compétence déjà donnée) : elle porte l'**identifiant du personnage**, ou cet état passe dans le personnage avec une nouvelle version de la sauvegarde ; à faire ici, avant que deux personnages partagent un monde (13.5), comme le demande [MONDE.md](MONDE.md) (limite relevée par la relecture de la PR #74).
- Dépend de 13.1.
- Formats : **sauvegarde** (passage du fichier local à la base, migration testée, clé des compétences données).

### 34. 13.4 — Comptes et connexion (M)

- Compte Éclats du Seuil (e-mail et mot de passe), une place de personnage par classe, données au strict minimum, serveurs en Europe, confidentialité courte (sujets 23 et 43).
- Dépend de 13.3.
- Formats : base (comptes), protocole (connexion).

### 35. 13.5 — Le solo sur le serveur (L)

- Unity devient un client seulement ; les sauvegardes locales d'aujourd'hui sont reprises sur le serveur ; l'hébergement local reste pour les prototypes et les essais.
- Bonus (plat, élixir, huile) **gardés à la mort et arrêtés hors ligne**, règle du serveur (périmètre, section 5, rangée « Alpha »).
- **Forge sur le serveur** (relecture indépendante de la PR #80, V1.4) : aujourd'hui le tirage de la forge se prévoit (clé connue du client) et se relance par une petite fabrication ou en rechargeant une sauvegarde, et le client note lui-même les frappes du petit jeu. Au lot 13 : un **secret du serveur** entre dans la graine du tirage, et le **serveur tient le temps du petit jeu** (il donne le tick de départ et la course du curseur, le client n'envoie que les ticks de ses frappes, le serveur les note).
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

## Bloc 5 — Les quatre nouvelles classes et le Bastion à ~14 compétences (C0.1 à C4.4)

Les cinq classes de l'alpha (sujet 22) : le **Bastion** (déjà là), le **Liturge de l'Aube** (soin), le **Veilleur** (distance), l'**Ombrelame** (corps à corps agile) et le **Tisse-Seuil** (magie de contrôle). Chaque classe : ressource propre, 3 arbres de talents, rôle principal et secondaire, rythme, signature et silhouette, environ 14 compétences en alpha, **Bastion compris** (décision de Damien du 31 octobre 2026, réponse 2), finit seule l'histoire. Ce bloc se fait **pendant le lot 13, avec un second agent** (décision de Damien du 31 octobre 2026, réponse 1). L'ordre des classes est décidé le même jour (réponse 5) : **Liturge de l'Aube, Veilleur, Ombrelame, puis Tisse-Seuil**. Les classes ont besoin d'abord d'un socle commun (C0) ; le Bastion passe à ~14 compétences juste après ce socle (C0.7 et C0.8, place proposée par Claude) : le socle est ainsi éprouvé sur une classe connue avant les nouvelles.

### 46. C0.1 — Fiches des quatre classes et du Bastion (S)

- Une fiche par nouvelle classe : ressource et mécanique, armes, 3 arbres, ~14 compétences avec leurs annonces et leurs défenses (L11-D7), rôle principal et secondaire, rythme, signature. La **fiche du Bastion** ajoute à ce qu'il a déjà ses compétences 7 à 14 (réponse 2). La roue des classes attend le lancement (périmètre, section 1).
- Ne dépend de rien : peut commencer dès maintenant, documentation seulement.
- **Relecture de Damien** : les cinq fiches, avant tout code de classe.

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

- Chaque classe affronte tous les boss des milliers de fois sur le moteur déterministe ; cible de 5 % par rôle (sujet 37). Les combats entre classes (arène) et la roue des classes attendent le lancement (périmètre, section 1).
- Dépend de C0.4.
- Formats : aucun (outil dans `EdS.Tools`, rapport).

### 52. C0.7 — Bastion : compétences 7 à 14 (L)

- Le Bastion passe de 6 compétences et son ultime à environ 14 compétences en alpha (décision de Damien du 31 octobre 2026, réponse 2) : les compétences 7 à 14 de sa fiche (C0.1), avec leurs annonces et leurs défenses (L11-D7), en données, et dans le moteur si l'une l'exige.
- Dépend de C0.1 (fiche du Bastion), C0.4 et C0.5. Passe avant C1.1 : une seule classe change le moteur à la fois.
- Formats : classe du Bastion en données, moteur et règles si besoin, nouveaux journaux figés (les anciens ne changent jamais).

### 53. C0.8 — Bastion : dans Unity et équilibrage (M)

- Animations, effets, sons et symboles des nouvelles compétences sur le modèle actuel du Bastion ; builds de référence revus avec les arbres refondus (C0.3) ; banc d'essai (C0.6), réglages en données.
- Dépend de C0.7, C0.6 et C0.3. Si une animation gratuite manque : **décision d'argent de Damien** (fiche d'achat).
- Formats : classe du Bastion en données, Unity.
- Essai : oui, court (le Bastion à ~14 compétences).

### 54 à 57. C1 — Liturge de l'Aube (soin et soutien)

- **54. C1.1 — Mécanique et compétences (L)** : la Ferveur (prières qui se chargent, lumière qui soigne et brûle), ~14 compétences. Dépend de C0.4, C0.5 et C0.7 (une seule classe change le moteur à la fois). Formats : classe en données, moteur si la Ferveur l'exige, nouveaux journaux figés.
- **55. C1.2 — Arbres, passifs et builds de référence (M)** : arbres Grâce, Bénédiction et le troisième de la fiche, passifs, builds de référence. Dépend de C1.1 et C0.3. Formats : classe en données.
- **56. C1.3 — Dans Unity : modèle, animations, signature (M)** : modèle et animations gratuits (Mixamo, packs), couleur, son et symbole, silhouette. Dépend de C1.1. Si aucun modèle gratuit ne convient : **décision d'argent de Damien** (fiche d'achat).
- **57. C1.4 — Relevé à distance, équilibrage et essai (S)** : le relevé à distance du Liturge, sur la règle du relevé du 13.7 ; banc d'essai, réglages en données. Dépend de C1.2, C1.3, C0.6 et 13.7 (relevé). Formats : classe en données. Essai : oui, court.

### 58 à 61. C2 — Veilleur (distance et exploration)

- **58. C2.1 — Mécanique et compétences (L)** : la Vigilance qui monte à l'immobilité, pièges de lumière, arc long et arc court, ~14 compétences. Dépend de C0.4 et C1.1 (une seule classe change le moteur à la fois). Formats : classe, moteur si besoin, nouveaux journaux figés.
- **59. C2.2 — Arbres, passifs et builds de référence (M)** : Guet, Traque, Lanterne. Dépend de C2.1 et C0.3.
- **60. C2.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C2.1. Même règle pour un achat éventuel : **décision d'argent de Damien**.
- **61. C2.4 — Équilibrage et essai (S)** : dépend de C2.2 et C2.3. Essai : oui, court.

### 62 à 65. C3 — Ombrelame (corps à corps et contrôle)

- **62. C3.1 — Mécanique et compétences (L)** : se fondre dans l'ombre, frapper dans le dos, poser des marques, dagues et lames jumelles, rythme en rafales, ~14 compétences. Dépend de C0.4 et C2.1. Formats : classe, moteur si besoin, nouveaux journaux figés.
- **63. C3.2 — Arbres, passifs et builds de référence (M)** : Lame, Voile, Poison. Dépend de C3.1 et C0.3.
- **64. C3.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C3.1. Achat éventuel : **décision d'argent de Damien**.
- **65. C3.4 — Équilibrage et essai (S)** : dépend de C3.2 et C3.3. Essai : oui, court.

### 66 à 69. C4 — Tisse-Seuil (contrôle et dégâts)

- **66. C4.1 — Mécanique et compétences (L)** : fils tissés entre les ennemis, tirés pour les lier, bâton et orbe, ~14 compétences. Dépend de C0.4 et C3.1. Formats : classe, moteur si besoin, nouveaux journaux figés.
- **67. C4.2 — Arbres, passifs et builds de référence (M)** : Trame, Nœud, Rupture. Dépend de C4.1 et C0.3.
- **68. C4.3 — Dans Unity : modèle, animations, signature (M)** : dépend de C4.1. Achat éventuel : **décision d'argent de Damien**.
- **69. C4.4 — Équilibrage des cinq classes et essai (S)** : banc d'essai sur les cinq classes ensemble (jalon J4). Dépend de C4.2, C4.3 et des quatre autres équilibrages (C0.8 pour le Bastion, C1.4, C2.4, C3.4). Essai : oui, court (les cinq classes l'une après l'autre).

## Bloc 6 — Premières minutes, création du personnage, interface de l'alpha (P.1 à P.5)

Ordre décidé (sujet 21) : souvenir de la Rupture en version courte (point 5), réveil au village, création du personnage, tutoriel dans l'histoire. Les identifiants P.x sont proposés ici.

### 70. P.1 — Souvenir de la Rupture, version courte (M)

- Quelques minutes dans la peau du jeune Galeran : le Seuil éclate, la lune se fend, la main tendue vers un enfant, l'écran blanc. La grande mise en scène attend le lancement.
- Dépend de V4.4 (zone et outils de scène) ; modèles gratuits.
- Formats : histoire, une petite zone à part.
- **Relecture de Damien** : le texte du souvenir. Essai : oui, court.

### 71. P.2 — Réveil et tutoriel dans l'histoire (M)

- Réveil dans le pré près du moulin, Odile, une chose apprise à la fois (marcher, parler, garde, parade face à un loup, première compétence, récolte, forge) ; premier combat **moins de 2 minutes** après le réveil, première vraie récompense **en moins de 10 minutes** ; indications courtes qui disparaissent.
- Dépend de P.1 et V1.9.1.
- Formats : histoire, textes.
- Essai : oui, court (les dix premières minutes).

### 72. P.3 — Création du personnage, version simple (M)

- Au réveil : classe choisie en voyant chaque classe en action, visage simple, nom dit à Odile ; peuple vallonien seulement (périmètre, section 5).
- Dépend de P.2, 13.4 (une place par classe) et des cinq classes jouables (C4.4, jalon J4).
- Formats : sauvegarde et base (apparence), Unity.
- Essai : oui, court (créer un personnage de chaque classe).

### 73. P.4 — Interface de l'alpha : lisibilité (M)

- Grand livre à onglets, chiffres de dégâts, vie des ennemis avec leur annonce, fil des événements, info-bulle qui compare, mini-carte, suivi des quêtes, signes au-dessus des têtes discrets et réglables, barre de consommables à part (périmètre, sections 5 et 8).
- Dépend de 13.7 (lisibilité à plusieurs).
- Formats : Unity seulement.

### 74. P.5 — Touches, réglages et sons (M)

- Toutes les touches changeables (AZERTY et QWERTY), garde maintenue ou en bascule, réglages enregistrés avec le compte ; sons-signaux en combat (un son par attaque, tintement de la parade), son en 3D au casque, volumes séparés.
- Dépend de 13.4 et P.4.
- Formats : réglages du compte (base), Unity.
- Essai : oui, court (avec P.4).

## Bloc 7 — Livres de build, cheval simple, guildes simples

Versions simples décidées pour l'alpha (points 4, 6 et 8). Les identifiants B, M et G sont proposés ici.

### 75. B.1 — Livres de build : les règles (L)

- Deux livres actifs au plus par configuration, sur les trois arbres ; une seconde copie ne cumule pas ; collection permanente ; prérequis visibles ; aucun livre n'est le seul moyen d'éviter les attaques ordinaires (H6, L11-D4).
- Dépend de C0.3 (refonte des arbres).
- Formats : règles et classes, sauvegarde (collection), nouveaux journaux figés.
- **Décision de Damien** : la place des livres dans les arbres et leur coût en points (points ouverts H6 du cadrage), proposés dans la PR.

### 76. B.2 — Livres des cinq classes (M)

- Une première série de livres pour le Bastion et les quatre nouvelles classes.
- Dépend de B.1, des C*.2 et de C0.8 (le Bastion à ~14 compétences).
- Formats : classes en données.

### 77. B.3 — Trouver et acheter les livres (M)

- Achat en monnaie du jeu, livres trouvés en donjon, en quête ou par une découverte (H6).
- Dépend de B.2, V3.1 et V1.6.
- Formats : marchands, butin, histoire.

### 78. B.4 — Écran des livres (S)

- Ranger, activer et comparer ses livres dans le grand livre à onglets.
- Dépend de B.3 et P.4.
- Essai : oui, court (groupé avec M.3 et G.2).

### 79. M.1 — Cheval : les règles (M)

- Un seul cheval simple : monter et descendre hors combat, vitesse au galop, on descend pour combattre.
- Dépend de 13.5 (le cheval vit sur le serveur).
- Formats : règles du monde, sauvegarde (monture possédée), protocole.

### 80. M.2 — Cheval : modèle et animations (M)

- Modèle gratuit, animations de marche, trot, galop, montée et descente.
- Dépend de M.1. Si aucun cheval gratuit ne convient : **décision d'argent de Damien** (fiche d'achat).

### 81. M.3 — Quête de la première monture (S)

- Le cheval gagné par une quête à la fin du chapitre 1 (sujet 7).
- Dépend de M.2 et V1.9.1.
- Formats : histoire.
- **Relecture de Damien** : le texte de la quête. Essai : oui, court (groupé).

### 82. G.1 — Guildes simples : créer, inviter (M)

- Créer une guilde, inviter, quitter, renvoyer ; une guilde principale par personnage (sujet 40).
- Dépend de 13.7.
- Formats : serveur et base, protocole.

### 83. G.2 — Canal de guilde et écran (S)

- Canal de guilde dans le chat, liste des membres.
- Dépend de G.1 et 13.8.
- Essai : oui, court (groupé).

## Bloc 8 — Préparation de l'alpha (A.1 à A.7)

### 84. A.1 — Carnet d'essai et bouton d'aide (M)

- Une touche : capture et remarque, envoyées à Claude (sujet 18) ; bouton d'aide dans le jeu, bug envoyé avec le journal du combat, rejoué au coup près (sujets 16 et 25).
- Dépend de 13.5.
- Formats : serveur (réception), Unity.

### 85. A.2 — Statistiques sans nom (M)

- Statistiques pseudonymisées, actives par défaut, désactivables, expliquées clairement : difficulté, parcours, économie (dont les sorties d'or), technique (sujet 25). Règle de la CNIL à vérifier ; sinon accord demandé au joueur.
- Dépend de 13.5.
- Formats : serveur, réglages du compte.

### 86. A.3 — PC minimum et fluidité (M)

- PC minimum de 2018, 60 images par seconde sur le PC conseillé ; ce qui compte au combat ne disparaît jamais ; rapport de lenteur (sujet 42).
- Dépend de C4.3 (toutes les classes à l'écran) et 13.15.
- Formats : réglages graphiques (Unity).

### 87. A.4 — Marque, nom de domaine, réseaux, Discord, entreprise (S, démarche)

- **Décision d'argent de Damien** : marque « Éclats du Seuil » à l'INPI **avant de montrer le jeu** (avant la première vidéo publique) ; 190 € pour une classe, 230 € pour deux, tarifs à vérifier sur inpi.fr ; Claude vérifie d'abord que le nom est libre (sujet 23).
- Damien réserve lui-même, guidé par Claude : l'adresse e-mail du jeu, le nom de domaine (petite dépense, prix vérifié au moment), YouTube, TikTok, Instagram, **Discord** (sujet 17).
- Entreprise : rien à créer avant l'alpha, puisqu'il n'y a pas de vente ; un premier rendez-vous avec un comptable ou la CCI pendant l'alpha, pour que l'entreprise soit prête **avant la première vente** (sujet 54).
- Ne dépend de rien : **peut se faire dès maintenant**, hors du chemin des chantiers.

### 88. A.5 — Page Steam (S, démarche)

- **Préparer** la page avant l'ouverture de l'alpha (J5) : textes, captures et vidéo tirées du jeu, logo, la déclaration honnête de l'IA, le questionnaire de classification propre à Steam.
- La page **s'ouvre pendant l'alpha**, avec de vraies images du jeu (décision de Damien du 31 octobre 2026, réponse 3). Le [périmètre](PERIMETRE.md) (section 12) range au lancement les bêtas et la démo au Next Fest ; seule la page avance à l'alpha. Frais de Steam : **décision d'argent de Damien**, au moment de l'ouvrir.
- Dépend de A.4 (la marque avant de montrer le jeu), de C4.4 (n° 69 : les cinq classes à l'écran, de vraies images) et du registre de l'origine de chaque contenu (pour la déclaration de l'IA, voir [Hors chantiers](#hors-chantiers)).

### 89. A.6 — Testeurs « Pionnier de l'Alpha » (S)

- Programme de testeurs (titre « Pionnier de l'Alpha », nom au monument des Fondateurs), testeurs adultes seulement, invitations, comptes, salon des testeurs sur le Discord, fiche d'essai.
- Dépend de A.4 (Discord), 13.4 et 13.14.
- **Décision de Damien** : le choix des testeurs et leur nombre.

### 90. A.7 — Répétition générale et ouverture de l'alpha (S)

- Critères de stabilité vérifiés (aucune perte de personnage, sauvegardes en double, reconnexion), dernières corrections, ouverture.
- Dépend de tout le reste.
- Essai : **la partie complète** (de la création du personnage au Gardien, avec un groupe).
- **Décision de Damien** : l'ouverture de l'alpha (jalon J5).

## Hors chantiers

Travail rangé avant l'alpha dans le périmètre, fait à côté des chantiers, sans PR de code à lui :

- **Bible artistique illustrée, charte graphique et logo fixe** (périmètre, section 9, sujets 49, 18 et 13) : documentation et images, juste après ce plan ; le logo fixe sert à la page Steam (n° 88).
- **Registre de l'origine de chaque contenu** (Claude, Damien, gratuit, acheté), à côté de `CREDITS.md` (périmètre, section 12) : tenu dès maintenant, complet avant la page Steam (n° 88), qui en a besoin pour la déclaration de l'IA.
- **Vidéos du journal de développement** (périmètre, section 12, sujet 17) : après la marque à l'INPI (n° 87), chaque publication avec l'accord de Damien.

Les autres éléments relevés sont rattachés à un chantier : guide d'écriture, lexique et fiches des PNJ au n° 7 ; trois places de bonus au n° 6 ; bonus gardés à la mort et arrêtés hors ligne au n° 35 ; sauvegarde du dossier des ressources et bouton « Préparer et lancer l'essai » au n° 12.

## Tableau récapitulatif

**Parallèle possible** : chantiers qui peuvent tourner en même temps avec deux agents, sans se gêner. La règle : deux chantiers ne changent jamais en même temps le même format (sauvegarde, règles et moteur, histoire, protocole, zone) ; les textes, l'outillage, la documentation et Unity peuvent avancer à côté d'un changement de moteur. Aujourd'hui, « monde » et « zone » sont le même fichier (`data/v0/world/zones/vallons.json`) : un chantier qui touche l'un ne tourne pas à côté d'un chantier qui touche l'autre. La démarche A.4 peut se faire à tout moment ; A.5 se prépare dès que les cinq classes sont à l'écran (n° 69) et la page s'ouvre pendant l'alpha.

| N° | Chantier | Taille | Dépend de | Essai | Parallèle possible |
|---|---|---|---|---|---|
| 1 | V1.2 Équipement complet (PR #75, fusionnée) | L | V1.1 | Oui, court | — |
| 2 | V1.6 Quêtes : le moteur (PR #76, sauvegarde v8) | M | V1.1 | — | — |
| 3 | V1.3 Effets d'équipement (sauvegarde v9) | L | 1 | — | 46 |
| 4 | V1.7 Guilde des aventuriers | M | 2 | — | 46 |
| 5 | V1.4 Forge, qualité, usure | M | V1.1, 3 | Oui, court | 46 |
| 6 | V1.8 Progression et équilibrage | M | 1 à 5 | — | 46 |
| 7 | V1.9.1 Chapitre 1 | S | 2, 6 | Relecture | 46 |
| 8 | V1.9.2 Chapitre 2 | M | 4, 5, 6, 7 | Relecture | 46 |
| 9 | V1.9.3 Chapitre 3 | M | 6, 8 | Relecture | 46 |
| 10 | V1.9.4 Chapitre 4 | M | 9 | Relecture | 46, 87 |
| 11 | V1.9.5 Chapitre 5 | M | 10 | Relecture | 46, 87 |
| 11 bis | V1.9.6 Chapitre 1 enrichi (A1, puis A2 et B) | M | 11 | Relecture, puis essai | 46 |
| 12 | V1.10 Places des maisons et essai | S | 1 à 11 | **Partie complète** | 46 |
| 13 | V2.1 Jour et nuit | M | 12 | Oui, court | 46 |
| 14 | V2.2 Météo | S | 13 | — | 15, 16 |
| 15 | V2.3 Villageois et leur journée | M | 13, 2 | — | 14, 16 |
| 16 | V2.4 Animaux et bêtes qui se battent | M | 12 | — | 14, 15, 19 |
| 17 | V2.5 Événements du monde | L | 13, 9 | Oui, court | 46 |
| 18 | V2.6 Le Seuil qui grandit | M | 17 | — | 46 |
| 18 bis | V2.6.1 Suites du Seuil | S | 18 | Oui, court (§ 14) | 46 |
| 19 | V2.7 Réputation | M | 2 | — | 16 |
| 20 | V2.8 Fêtes, stock changeant, essai | M | 13 à 19 | Oui, court | 21 |
| 21 | V3.1 Donjons : moteur et crypte | L | 12, 13 | Oui, court | 20, 46 |
| 22 | V3.2 Mines de la carrière | M | 21 | — | 26, 27, 28, 29 |
| 23 | V3.3 Grottes noyées du lac | M | 21 | — | 26, 27, 28, 29 |
| 24 | V3.4 Grand Solitaire, créatures rares | M | 4, 17 | Oui, court | 27, 29 |
| 25 | V3.5 Exploration récompensée | M | 2 | — | 46 |
| 26 | V3.6 Arène et choix qui restent | M | 10, 17 | — | 22, 23 |
| 27 | V4.1 Pêche et carnet | M | 5 | — | 22, 23, 24 |
| 28 | V4.2 Forge poussée | L | 3, 5 | — | 22, 23 |
| 29 | V4.3 Compagnon | M | 4 | — | 22, 23, 24 |
| 30 | V4.4 Bestiaire, hauts faits, essai | M | 21 à 29 | **Partie complète** | 46 |
| 31 | 13.1 Serveur de zone | L | 30, 11.7 | — | 47, 48 |
| 32 | 13.2 Client du monde partagé | L | 31 | Oui, court | 33, 47, 48 |
| 33 | 13.3 Persistance sur le serveur | L | 31 | — | 32, 49, 50 |
| 34 | 13.4 Comptes et connexion | M | 33 | — | 49, 51 |
| 35 | 13.5 Le solo sur le serveur | L | 32, 33, 34 | — | 51, 52, 54 |
| 36 | 13.6 Récompenses partagées | M | 35 | — | 52, 53, 54, 55 |
| 37 | 13.7 Groupes, amis, relever | L | 35 | Oui, court | 53, 55, 56 |
| 38 | 13.8 Chat et modération de base | M | 34 | — | 56, 57, 58 |
| 39 | 13.9 Marché simple | M | 35 | — | 58, 59 |
| 40 | 13.10 Événements et donjons à plusieurs | M | 36, 37, 17, 21, 24 | — | 59, 60 |
| 41 | 13.11 Éditeur (1) quêtes et dialogues | L | 2 | Oui, court | 60, 61, 62 |
| 42 | 13.12 Éditeur (2) créatures, « Essayer » | M | 41 | — | 62, 63 |
| 43 | 13.13 Sanctions et outils | S | 38 | — | 63, 64 |
| 44 | 13.14 Hébergement de test | M | 33 | — | tous les C* |
| 45 | 13.15 Essai sur Internet, charge | S | 31 à 44 | **Partie complète** | 64, 65 |
| 46 | C0.1 Fiches des quatre classes et du Bastion | S | — | Relecture | tous |
| 47 | C0.2 Caractéristiques | L | 6 | — | 31, 32 |
| 48 | C0.3 Refonte des arbres | M | 47 | — | 31, 32 |
| 49 | C0.4 Socle du moteur multi-classes | L | 46 | — | 33, 34 |
| 50 | C0.5 Roulade, animations au moteur | M | 49 | Oui, court | 33 |
| 51 | C0.6 Banc d'essai d'équilibrage | M | 49 | — | 34, 35, 52, 54 |
| 52 | C0.7 Bastion : compétences 7 à 14 | L | 46, 49, 50 | — | 35, 36, 51 |
| 53 | C0.8 Bastion : Unity et équilibrage | M | 52, 51, 48 | Oui, court | 36, 37, 54 |
| 54 | C1.1 Liturge : mécanique | L | 49, 50, 52 | — | 35, 36, 51, 53 |
| 55 | C1.2 Liturge : arbres | M | 54, 48 | — | 56, 36, 37 |
| 56 | C1.3 Liturge : Unity | M | 54 | — | 55, 58, 37, 38 |
| 57 | C1.4 Liturge : relevé à distance, équilibrage | S | 55, 56, 51, 37 | Oui, court | 58, 38 |
| 58 | C2.1 Veilleur : mécanique | L | 49, 54 | — | 56, 57, 38, 39 |
| 59 | C2.2 Veilleur : arbres | M | 58, 48 | — | 60, 39, 40 |
| 60 | C2.3 Veilleur : Unity | M | 58 | — | 59, 62, 40, 41 |
| 61 | C2.4 Veilleur : équilibrage | S | 59, 60 | Oui, court | 62, 41 |
| 62 | C3.1 Ombrelame : mécanique | L | 49, 58 | — | 60, 61, 41, 42 |
| 63 | C3.2 Ombrelame : arbres | M | 62, 48 | — | 64, 42, 43 |
| 64 | C3.3 Ombrelame : Unity | M | 62 | — | 63, 66, 43, 45 |
| 65 | C3.4 Ombrelame : équilibrage | S | 63, 64 | Oui, court | 66, 45 |
| 66 | C4.1 Tisse-Seuil : mécanique | L | 49, 62 | — | 64, 65, 70 |
| 67 | C4.2 Tisse-Seuil : arbres | M | 66, 48 | — | 68, 70, 71 |
| 68 | C4.3 Tisse-Seuil : Unity | M | 66 | — | 67, 71 |
| 69 | C4.4 Équilibrage des cinq classes | S | 53, 57, 61, 65, 67, 68 | Oui, court | 73 |
| 70 | P.1 Souvenir de la Rupture, court | M | 30 | Relecture, oui, court | 66, 67, 75 |
| 71 | P.2 Réveil et tutoriel | M | 70, 7 | Oui, court | 67, 68, 75 |
| 72 | P.3 Création du personnage | M | 71, 34, 69 | Oui, court | 73, 82 |
| 73 | P.4 Interface de l'alpha | M | 37 | — | 69, 72, 75, 79 |
| 74 | P.5 Touches, réglages, sons | M | 34, 73 | Oui, court | 76, 80 |
| 75 | B.1 Livres : les règles | L | 48 | — | 70, 71, 73 |
| 76 | B.2 Livres des cinq classes | M | 75, 53, 55, 59, 63, 67 | — | 74, 79 |
| 77 | B.3 Trouver et acheter les livres | M | 76, 21, 2 | — | 80, 82 |
| 78 | B.4 Écran des livres | S | 77, 73 | Oui, court (groupé) | 81, 83 |
| 79 | M.1 Cheval : règles | M | 35 | — | 73, 76 |
| 80 | M.2 Cheval : modèle et animations | M | 79 | — | 74, 77 |
| 81 | M.3 Quête de la première monture | S | 80, 7 | Relecture, oui, court (groupé) | 78, 82 |
| 82 | G.1 Guildes simples | M | 37 | — | 72, 77, 81 |
| 83 | G.2 Canal de guilde | S | 82, 38 | Oui, court (groupé) | 78, 84 |
| 84 | A.1 Carnet d'essai, bouton d'aide | M | 35 | — | 83, 85, 86, 89 |
| 85 | A.2 Statistiques sans nom | M | 35 | — | 84, 86, 89 |
| 86 | A.3 PC minimum et fluidité | M | 68, 45 | — | 84, 85, 89 |
| 87 | A.4 Marque, réseaux, Discord, entreprise | S | — | — | tous |
| 88 | A.5 Page Steam | S | 87, 69 | — | tous ceux après 69 |
| 89 | A.6 Testeurs « Pionnier de l'Alpha » | S | 87, 34, 44 | — | 84, 85, 86 |
| 90 | A.7 Répétition générale, ouverture | S | tous | **Partie complète** | — |

## Chemin critique et risques

**Le chemin critique** (ce qui ne peut pas se faire à côté) : fin de la vague 1 → vague 2 → part gardée des vagues 3 et 4 → lot 13 → premières minutes et création du personnage → livres, cheval, guildes → préparation → ouverture. Les **classes ne sont pas sur ce chemin** : le second agent les fait pendant le lot 13 (décision de Damien du 31 octobre 2026, réponse 1). C'est le plus gros gain de temps du plan : la plupart des 24 chantiers du bloc 5 sortent du chemin, soit environ un mois de gagné.

**Les cinq plus gros risques :**

1. **Le lot 13 et ses formats nouveaux.** Serveur de zone, base de données, solo sur le serveur et migration des sauvegardes sont des choix difficiles à défaire. Les PR à format nouveau demandent beaucoup de tours de revue (une trentaine pour le 11.4d). C'est le bloc le plus incertain : 4 à 8 semaines (sujet 20).
2. **Les quatre classes et le Bastion à ~14 compétences.** Chaque nouvelle classe demande au moteur une mécanique que le Bastion n'a pas (soin, distance, furtivité, contrôle), ~14 compétences, trois arbres, des animations soumises au moteur et un équilibrage à 5 % près ; le Bastion gagne aussi 8 compétences (de 7 à 14). Il faut aussi trouver des modèles et des animations gratuits qui tiennent le style « entre ESO et Fable ». Le bloc compte 24 chantiers sur la voie du second agent, plus que les 15 du lot 13 : sa marge est faible, et la création du personnage (n° 72) l'attend.
3. **Le contenu écrit et les relectures.** Cinq chapitres, une vingtaine de quêtes, cinq fiches de classes (Bastion compris), le souvenir et la quête du cheval passent par Damien. Une relecture qui attend arrête les chapitres suivants, qui s'empilent.
4. **Les formats de sauvegarde et de règles en cascade.** Presque chaque chantier des Vallons touche la sauvegarde ou les règles ; les branches s'empilent et deux agents peuvent se gêner. Puis le lot 13 déplace toutes les sauvegardes sur le serveur : une erreur à ce moment ferait perdre des personnages. Plus tard, la création du personnage (n° 72) et les guildes simples (n° 82) touchent toutes deux la **base de données** : elles ne tournent en parallèle que si leurs migrations de base sont séparées (tables différentes, versions dans l'ordre) ; sinon, l'une attend l'autre.
5. **Les essais dans Unity et le PC minimum.** Unity ne tourne pas dans le cloud : chaque essai attend le PC de Damien. Le style réaliste (packs, ombres, effets) doit tenir 60 images par seconde sur le PC conseillé et rester jouable sur un PC de 2018, et le réglage peut arriver tard (A.3).

**Ce qui ferait gagner du temps :** les classes pendant le lot 13 avec un second agent (décidé) ; les cinq fiches de classes relues tôt (décembre) ; les essais courts groupés dans une seule séance (B.4, M.3, G.2) ; l'éditeur interne (13.11) prêt avant les quêtes de l'alpha ; des relectures de Damien rendues sous 24 à 48 heures ; aucune idée nouvelle ajoutée à l'alpha (elles vont au lancement). Le périmètre le dit aussi : reporter une version simple (les livres de build d'abord) rendrait du temps, mais Damien ne l'a pas choisi.

**Ce qui ferait perdre du temps :** une PR à format nouveau qui enchaîne les tours de revue ; une idée « au max » ajoutée à l'alpha en route ; un modèle de classe ou de cheval introuvable en gratuit ; un hébergement qui tarde (décision ou mise en service) ; les fêtes de fin d'année ; deux agents sur le même format.

## Où Damien intervient

Dans l'ordre, avec le moment estimé (mêmes fourchettes que les jalons).

| Quand | Quoi | Sorte |
|---|---|---|
| 31 octobre 2026 (**fait**) | Relire ce plan et répondre aux [questions](#questions-pour-damien) 1 à 5 (réponses vers 13 h 45) | Décision de Damien |
| Novembre 2026 | V1.2 : écran du personnage et boucliers (n° 1) | Essai court |
| Novembre 2026 | Chapitre 1 et ses quêtes (n° 7) | Relecture |
| Novembre 2026 | V1.4 : forge et réparation (n° 5) | Essai court |
| Novembre à début décembre 2026 | Chapitres 2 à 5 et leurs quêtes, un par un (n° 8 à 11) | Relectures |
| Dès que possible, avant la première vidéo publique | Marque à l'INPI, 190 € pour une classe, 230 € pour deux, tarifs à vérifier (n° 87) ; e-mail du jeu, nom de domaine, réseaux et Discord créés par Damien | **Décision d'argent de Damien** |
| Fin novembre à mi-décembre 2026 | Partie complète de la vague 1 (n° 12), premiers testeurs amis | Essai complet |
| Décembre 2026 | Fiches des quatre classes et du Bastion (n° 46) | Relecture |
| Décembre 2026 à janvier 2027 | La nuit (n° 13), une attaque du hameau (n° 17), la vague 2 (n° 20) | Essais courts |
| Janvier 2027 | La crypte (n° 21), le Grand Solitaire (n° 24) | Essais courts |
| Mi-janvier à fin février 2027 | Partie complète des Vallons solo finis (n° 30) | Essai complet |
| Février 2027 | Serveur de test loué au mois et plafond de dépenses mensuel (n° 44) | **Décision d'argent de Damien** |
| Février à mars 2027 | Refonte des arbres et chiffres proposés (n° 48) | Décision de Damien |
| Février à mars 2027 | Roulade (n° 50), réseau à 150 ms (n° 32), groupe à deux (n° 37), écrire une quête dans l'éditeur (n° 41) | Essais courts |
| Mars à mai 2027 | Si un modèle ou une animation de classe ne se trouve pas en gratuit (n° 53, 56, 60, 64, 68) | **Décision d'argent de Damien**, au cas par cas |
| Mars à avril 2027 | Partie complète à plusieurs sur Internet (n° 45) | Essai complet |
| Mars à mai 2027 | Le Bastion à ~14 compétences (n° 53), chaque nouvelle classe (n° 57, 61, 65), puis les cinq ensemble (n° 69) | Essais courts |
| Avril 2027 | Texte du souvenir de la Rupture (n° 70), les dix premières minutes (n° 71), la création (n° 72), les réglages (n° 74) | Relecture, essais courts |
| Avril 2027 | Place et coût des livres dans les arbres (n° 75) | Décision de Damien |
| Avril à mai 2027 | Si aucun cheval gratuit ne convient (n° 80) | **Décision d'argent de Damien** |
| Avril à mai 2027 | Texte de la quête du cheval (n° 81) ; livres, cheval et guilde en une séance (n° 78, 81, 83) | Relecture, essai court groupé |
| Mai 2027 | Choix et nombre des testeurs (n° 89) | Décision de Damien |
| Mai à juin 2027 | Répétition générale et ouverture de l'alpha (n° 90) | Essai complet, décision de Damien |
| Pendant l'alpha (après J5) | Ouverture de la page Steam, avec de vraies images, et ses frais (n° 88) | **Décision d'argent de Damien** |
| Pendant l'alpha | Premier rendez-vous avec un comptable ou la CCI, avant toute vente (n° 87) | Décision de Damien |

Et **l'accord de fusion**, PR par PR, pour chaque PR du lot 13 et des classes (n° 31 à 69, réponse 4). Les PR des vagues 1 à 4 (n° 1 à 30) et des blocs 6 à 8 (n° 70 à 90, réponse à la [question 6](#questions-pour-damien)) sont fusionnées dès que leur CI est verte et que Codex n'a aucune remarque.

## Questions pour Damien

**Réponses de Damien (31 octobre 2026, vers 13 h 45)** : 1. les quatre classes **pendant le lot 13**, avec un second agent ; 2. le **Bastion passe aussi à environ 14 compétences** en alpha ; 3. la **page Steam s'ouvre pendant l'alpha**, avec de vraies images du jeu (frais de Steam : décision d'argent de Damien) ; 4. l'accord de fusion « CI verte et Codex sans remarque » **vaut aussi pour les vagues 2 à 4** ; **le lot 13 et les classes se fusionnent PR par PR**, avec l'accord explicite de Damien à chaque fois ; 5. ordre des classes : **Liturge de l'Aube, Veilleur, Ombrelame, puis Tisse-Seuil**.

**Question ouverte :**

6. **Les fusions des blocs 6 à 8.** Réponse de Damien (31 octobre 2026) : **même règle que les vagues** ; les premières minutes, les livres de build, le cheval, les guildes simples et la préparation de l'alpha (P, B, M, G, A ; n° 70 à 90) sont fusionnés dès que la CI est verte et que Codex n'a aucune remarque. Les démarches qui engagent Damien (entreprise, marque, page Steam) restent à lui.

Questions posées le 31 octobre 2026 (gardées pour mémoire, réglées par les réponses ci-dessus) :

1. **Les quatre classes : pendant le lot 13 ou après ?** Proposition de Claude : **pendant**, avec un second agent. Le moteur des classes et le serveur touchent des formats différents, et les classes sortent ainsi du chemin critique (environ un mois de gagné). Après le lot 13, c'est plus simple à suivre, mais l'alpha glisse vers juin ou après.
2. **Combien de compétences en alpha ?** Le périmètre propose environ 14 par classe (chiffre de Claude, pas encore une décision). Le plan le prend pour les quatre nouvelles classes. Le **Bastion** passe-t-il aussi à environ 14 (deux ou trois chantiers de plus), ou garde-t-il ses 6 compétences et son ultime pour l'alpha ?
3. **La page Steam : quand ?** Le sujet 17 dit « page Steam tôt » (pour la liste de souhaits) ; le périmètre la range au lancement. Proposition : la préparer pendant l'alpha (n° 86 à l'époque, n° 88 aujourd'hui) et l'ouvrir quand des vidéos montrent le jeu, après la marque. Frais de Steam à décider au moment de l'ouvrir.
4. **Les fusions après la vague 1.** L'accord « fusion dès la CI verte et Codex sans remarque » vaut pour la vague 1. Damien l'étend-il aux vagues 2 à 4 ? Et pour le lot 13 et les classes, qui changent des formats difficiles à défaire, garde-t-il un accord PR par PR ?
5. **L'ordre des classes.** Proposition : Liturge de l'Aube d'abord (le soin sert aux groupes et au relevé en donjon), puis Veilleur, Ombrelame et Tisse-Seuil (du plus simple au plus nouveau pour le moteur). Un autre ordre ?
