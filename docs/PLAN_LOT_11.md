# Plan à partir du lot 11 — ordre validé le 20 octobre 2026

Plan de Claude, troisième version. L'ordre et les règles du combat dans l'espace ont été **validés par Damien le 20 octobre 2026** (décisions L11-D1 à L11-D12, détail dans le [registre du cadrage](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12)). Les choix de gameplay restent les siens. Chaque sous-lot suit la méthode habituelle : branche, tests, CI, revue Codex, PR, fusion seulement avec son accord.

## 1. État réel du projet

Vérifié dans le code le 19 octobre 2026 (`main` à `f5998c5`).

| Domaine | Ce qui existe | Ce qui manque pour le cadrage |
|---|---|---|
| **Combat** | Moteur déterministe 0.14.0 (tick de 50 ms, virgule fixe, journal au format 8, rejeu octet par octet), 64 combattants par combat au plus (paramètre, borne 256). **Bastion complet** : trois arbres, six actives, l'ultime, huit passifs, garde, parade, Charges, Résonance, boucliers, provocation, transfert, altérations, déclencheurs | **Ennemis qui meurent** : les assaillants n'ont pas de PV et frappent à intervalles programmés (`FirstAttackMs`, `IntervalMs`), sans comportement. Les joueurs ne peuvent que perdre ou tenir jusqu'au temps imparti. **Aucun soin** (pas de catégorie d'effet) |
| **Positions** | Aucune (A7 du moteur, `MOTEUR.md`). Unity déplace le personnage et place les assaillants pour l'affichage seulement ; la garde couvre toutes les directions | Tout le socle : positions, déplacement, portée, formes, zones au sol, collisions, obstacles, étages |
| **Progression** | Règles de niveau dans `rules.json` (niveau maximal 30, points, déblocages, majeur à 18 points + 1), `BuildValidator`, `TalentEditor` (redistribution point par point), builds de référence aux niveaux 30 et 10 | Expérience, attributs par classe et niveau, livres, héritages, maîtrises |
| **Objets** | Les formules prévoient trois puissances d'équipement (`StatContributions`). L'origine `equipment` des déclencheurs est réservée | Objets, raretés, équipement, inventaire, butin, effets d'équipement |
| **Monde** | Rien | Zone, PNJ, dialogues, quêtes, récolte, fabrication, points sûrs, temps du monde |
| **Multijoueur** | Lot 10 : serveur autoritaire d'**un combat**, une place par personnage du scénario (deux dans la coopération livrée, huit au plus), composition fixe, pas d'entrée dans un combat commencé, TCP sans chiffrement pour le réseau local. Chaque client rejoue tout le combat et compare l'empreinte ; resynchronisation ; retour dans les 30 s ; robot ; mesures (`eds reseau`) ; télégraphie avancée du ping (option B) | Zone où l'on entre à tout moment, entrée par l'état courant, prédiction du déplacement, comptes, chiffrement, hébergement |
| **Persistance** | Aucune, à part les journaux de combat | Sauvegarde locale, puis sauvegarde serveur |
| **Unity** | Prototype jouable sur PC (Windows) : arène générée, interface IMGUI, combat solo et réseau, écran des talents, graphismes provisoires. **Deux caméras** : « isométrique » (perspective plongeante à 50°, recul de 8 à 30, quarts de tour), troisième personne (18°, recul de 3 à 14, souris) ; bascule par la touche C avec un court lissage (environ 0,3 s), sans réglage continu entre les deux | Vues 1 et 2 reliées par un réglage continu, visibilité près des obstacles, scène de terrain, commandes tactiles, versions Android et iOS |
| **Textes** | En français, écrits dans le code d'Unity et dans les noms des données | Textes par clés, préparés pour la traduction (Q37) |

## 2. Acquis réutilisables et ce qui change

L11-D8 retient une simulation de zone sous autorité du serveur, où l'on entre depuis l'état courant. Voici ce que devient chaque acquis du lot 10. Rien n'est réécrit par principe ; le modèle fermé n'est pas supposé suffire.

| Acquis | Réutilisé | Ce qui change |
|---|---|---|
| **Noyau déterministe** (moteur, tick, virgule fixe, journal, rejeu) | Autorité du serveur, tests de propriétés, journaux de référence, rejeu côté serveur pour enquêter | Le moteur reçoit des positions, et des combattants qui arrivent et partent en cours de route |
| **Protocole** (messages encadrés, lecture stricte, limites) | Conservé tel quel | Nouvelle version : entrée dans une zone, état courant, mises à jour, intentions de déplacement |
| **Transport TCP et réseau simulé** | Conservés, y compris la latence et la gigue artificielles des tests | — |
| **Salle de combat** (`CombatRoom`) | Gardée pour les combats fermés (instances, duels, essais) | Pour le monde : une zone où l'on entre et d'où l'on sort à tout moment, sans place réservée |
| **Client** (`NetworkCombat`) | Contrôle d'empreinte, gardé pour les tests ; reconstruction à un tick confirmé, base de la correction | Entrée par un **état courant** suivi de mises à jour, sans rejeu du passé. **Prédiction** du déplacement local avec correction, qui demande des instantanés pour ne rejouer que les derniers ticks |
| **Retour après une coupure** (jeton, 30 s) | Conservé | Retour à l'état courant. Événements identifiés (une mort, une récompense) pour ne jamais les réattribuer |
| **Temps aller-retour et télégraphie avancée** (option B) | Conservés | Étendus aux zones au sol (instant d'impact affiché en avance) |
| **`IntentionPlanner`, `CombatDisplay`** | Conservés, partagés entre le solo et le réseau | Étendus au déplacement et aux positions |
| **Robot, `eds reseau`** | Conservés | Nouveaux cas : arrivée pendant un affrontement, latence sur le déplacement et l'esquive |

Autres acquis :

- **Équipement** : `StatContributions` pour les bonus, l'origine `equipment` pour les effets.
- **Données** : validateur unique, schémas versionnés, refus avec chemin précis.
- **Caméras** : les deux caméras du prototype sont les bases des vues 1 et 2.
- **Mesures** : `eds report`, pilotes scriptés.

## 3. Ordre des chantiers (validé le 20 octobre 2026)

**Les positions d'abord**, avec trois ajustements (L11-D1) :

1. L'architecture de référence est fixée **avant** le moteur des positions.
2. Une **démonstration réseau minimale** a lieu dès le lot 11.
3. Un **premier essai tactile** a lieu dès le lot 11.

La simulation de zone tourne sur le serveur ; en solo, le jeu héberge la zone sur le poste, comme l'hôte du lot 10.

| Jalon | Lot | Objectif | Résultat observable | Périmètre |
|---|---|---|---|---|
| 1 | **11** | **Combat dans l'espace** | Dans une petite scène des Vallons : tuer des ennemis, sortir d'une zone annoncée pour l'éviter, jouer dans les deux vues, à deux en réseau (le second arrive pendant le combat), sur PC puis sur le Note8 | Tranche |
| 2 | **12** | **Les Vallons d'Aube en solo** | Mission « Les traces sous le moulin » de bout en bout, niveaux 1 à 10, équipement, récolte et fabrication, chef, sauvegarde locale | Tranche |
| 3 | **13** | **Monde partagé et persistance** | Deux joueurs ou plus dans les Vallons : se voir, combattre ensemble, chacun sa mission ; comptes de test et sauvegarde sur le serveur | Tranche |
| 4 | **14** | **Interface mobile complète** | La tranche jouable sur le Note8 et l'iPhone 13, même personnage sur PC et mobile, interface qui remplace l'IMGUI du prototype | Tranche |
| 5 | **15 et suivants** | **Première alpha** | Périmètre à définir (L11-D12) ; recommandations du cadrage : trois classes, progression refondue, donjon avec boss et réveil renforcé, groupes, échanges | Alpha |
| 6 | — | **Jeu complet** | Ville, cinq régions, Cœur du Seuil, économie, guildes, factions, boss de monde, refuge… | Final |

## 4. Lot 11 — combat dans l'espace

Objectif : un combat où se placer compte, résolu par le serveur, lisible dans les deux vues, jouable à deux et au pouce. La garde et la parade restent au cœur du Bastion.

| Sous-lot | Contenu | Résultat observable | Taille |
|---|---|---|---|
| 11.1 | **Ennemis qui meurent** (autorisé, L11-D1) : PV des assaillants en données (sans PV, un assaillant reste un mannequin, ce qui garde les scénarios actuels). La mort est un **état de la simulation** : plus d'attaque, cibles invalidées, un seul événement de mort. **Cycle de vie** explicite, prêt pour la réapparition et le réveil renforcé sans les réaliser. Victoire des joueurs quand tous les ennemis sont morts. Formats versionnés, nouveaux journaux de référence ; barre de vie et mort dans Unity, en solo comme en réseau | On tue la meute de la coopération dans Unity et le combat se gagne ; le journal le rejoue octet par octet | moyenne |
| 11.2 | **Architecture de référence** (document, L11-D8, [ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md)) : zone et entités, autorité, entrée par état courant, contenu des mises à jour (état ou commandes), zone d'intérêt, prédiction et correction, identifiants des événements, retour après une coupure, ce qui reste du lot 10 | Document que Damien relit et valide (choix difficile à défaire) | petite |
| 11.3 | **Règles du combat dans l'espace** (document, [COMBAT_ESPACE.md](COMBAT_ESPACE.md)) appliquant L11-D2 à L11-D7. Contenu : unités, plan au sol (la démonstration n'a qu'un niveau), vitesses, verrouillage de la cible et de la direction, défenses déclarées par attaque. Propositions à valider : **couverture directionnelle du bouclier**, **règle du contact joueur–créature** (section 4.1), portées et rayons du Bastion | Document et propositions validés par Damien | petite |
| 11.4 | **Moteur spatial** : positions en virgule fixe ; déplacement par intentions à vitesse bornée, ralenti en garde ; obstacles du décor ; contact selon 11.3 ; ciblage mixte (cible, direction, point au sol) vérifié au moment défini ; zones annoncées résolues au tick d'impact ; défenses par attaque ; combattants qui arrivent et partent. Propriétés et frontières au tick près | Scénarios positionnés en ligne de commande : une sortie de zone évite le coup, au tick près | grande |
| 11.5 | **Ennemis dans l'espace** : comportements en données (approcher, poursuivre, garder une distance, encercler, attaque de zone annoncée, charge) ; choix de cible déterministe | Scénario où la meute approche, encercle et annonce ses zones ; relevé `eds report` | moyenne |
| 11.6 | **Scène de démonstration dans Unity (H14)** : coin des Vallons en formes simples (moulin, pont, arbres, un toit, un passage étroit) ; Bastion ; plusieurs ennemis ; attaque annoncée au sol ; obstacle qui masque. Caméra réglable entre les vues 1 et 2 (molette, rappel rapide, réglage gardé, aucune bascule automatique) ; visibilité adaptée sans voir à travers les parois ; déplacement simulé par le moteur | Damien joue la scène en solo dans les deux vues | grande |
| 11.7 | **Démonstration réseau minimale** (L11-D8) : serveur local et deux clients dans la scène ; déplacements et attaque spatiale résolus par le serveur ; **arrivée du second joueur pendant un affrontement** ; mort d'un ennemi cohérente ; **retour à l'état courant** sans réattribuer d'événement ; prédiction du déplacement local ; essais avec latence artificielle (`eds reseau` : esquive de 0 à 200 ms d'un sens) | Deux postes, ou un poste et le robot : le second rejoint un combat en cours ; mesures | grande |
| 11.8 | **Premier essai tactile** : dispositions vérifiées dans le simulateur d'appareils de l'éditeur ; version Android installée sur le **Note8** (installation et démarrage d'abord, puis déplacement au pouce, boutons, geste entre les deux vues, réglages graphiques allégés) ; iPhone 13 si une chaîne de compilation iOS est disponible. Guide pas à pas dans `UNITY.md` | Damien joue la scène sur son téléphone ; limites notées | moyenne |
| 11.9 | **Essai et rapport** : les quatre vérifications de H14 (même combat dans les deux vues, obstacles et passages, commande et stabilité, lisibilité et performances) ; esquive en réseau ; choix du contact joueur–créature ; valeur du ralentissement en garde ; **réestimation** des lots 12 à 14 | Rapport et réglages ; estimation révisée | petite |

11.2 et 11.3 précèdent le moteur spatial. 11.1 n'en dépend pas : son cycle de vie est conçu pour une zone où l'on entre et sort.

### 4.1 Contact joueur–créature : proposition à éprouver (L11-D5)

Chaque combattant a une emprise au sol circulaire, de rayon fixé par sa taille. Le décor est solide. Les joueurs ne se bloquent pas et ne se poussent pas entre eux. Les créatures s'évitent (séparation douce, sans chaîne de poussées).

Pour le contact entre un joueur et une créature, deux règles seront essayées dans la scène du 11.6 (plusieurs ennemis, un angle, une entrée étroite) :

- **Règle A, « volumes solides sans poussée »** : ni le joueur ni la créature n'entre dans l'emprise de l'autre ; un déplacement qui la rencontre glisse le long du contour, sans rien pousser.
  - Pour : le Bastion peut tenir un passage étroit.
  - Contre : une créature peut coincer un joueur dans un angle. Garde-fou : une créature ne termine pas un déplacement qui fermerait le dernier passage libre d'un joueur.
- **Règle B, « traversée freinée »** : le chevauchement est permis, mais traverser une créature ralentit fortement le joueur ; personne n'est jamais bloqué.
  - Pour : aucun piège possible.
  - Contre : tenir un passage n'a plus de sens physique.

Dans les deux cas, les grandes créatures (chefs) restent solides et aucun déplacement forcé ne vient d'un simple contact : seulement d'attaques et de capacités identifiables (repousser, attirer).

*Avis* : A, si l'essai montre que le garde-fou suffit ; B sinon. Le choix final revient à Damien après l'essai (11.9). **Retenu le 22 octobre 2026 : A** (« garder : solide »).

## 5. Lots 12 à 14, alpha, jeu complet

### Lot 12 — les Vallons d'Aube en solo

| Sous-lot | Contenu | Taille |
|---|---|---|
| 12.1 | **Monde** (`EdS.World`, netstandard2.1, sans dépendance), sur l'architecture du 11.2 : la zone des Vallons, entités qui entrent et sortent, combats sur place, temps du monde tiré des ticks (Q26), formats de sauvegarde | moyenne |
| 12.2 | **Personnage** : expérience et niveaux 1 à 10 (courbe provisoire), attributs par classe et niveau (Q20), déblocages ; sauvegarde locale versionnée, écriture atomique, migrations | moyenne |
| 12.3 | **Objets, équipement, inventaire** : catalogue (niveau requis, puissance, rareté à six rangs, propriétés), quatre emplacements dont le bouclier, 24 places empilables, butin attribué une seule fois ; un premier effet spécial d'équipement si le temps le permet | grande |
| 12.4 | **Les Vallons en données** : village, rivière, moulin, carrière, ruines, petite zone du Seuil ; points sûrs ; populations et réapparitions ; raccourci du moulin (état gardé) ; manifestation ; nœuds de récolte ; **laisse** des ennemis (retour au calme d'un ennemi assez distancé, retenu à l'essai du 22 octobre 2026) | moyenne |
| 12.5 | **PNJ, dialogues et quêtes** : états narratifs (H1), conditions durables et temporaires (H3), journal des découvertes, portée personnelle des choix (H2) ; mission « Les traces sous le moulin » | grande |
| 12.6 | **Soin et métiers** : catégorie d'effet « soin » dans le moteur (avant les remèdes), potions limitées à recharge partagée ; minerai et forge, plantes et remèdes, XP de métier séparée | moyenne |
| 12.7 | **Chef de fin et récompenses** : boss à phases, défaite sans perte (retour au point sûr), registre des récompenses attribuées une seule fois ; quête secrète et réveil renforcé seulement s'ils entrent dans la tranche (Q2), sinon à une étape identifiée de l'alpha | moyenne |
| 12.8 | **Unity** : la zone jouable en formes simples, inventaire, équipement, journal de quête, dialogues, fabrication ; textes par clés (Q37) | grande |
| 12.9 | **Essai complet et rapport** : durée, difficulté, compréhension de la mission, réglages | petite |

### Lot 13 — monde partagé et persistance

À partir de la démonstration du 11.7 :

- **Serveur de zone** pour les Vallons : la simulation du lot 12 tourne sur le serveur ; les joueurs entrent et sortent.
- **Réplication** filtrée par zone d'intérêt, prédiction du joueur local.
- **Règles à plusieurs** : crédit des actions, aide, partage de quête, réinitialisation des rencontres quand tous sont partis (Q8), portée des choix (H2).
- **Comptes de test** et sauvegarde sur le serveur (Q10), chiffrement des échanges.
- **Hébergement d'essai** : sur le réseau local, rien à payer ; sur Internet, c'est une dépense que Damien décide.
- **Mesures** : joueurs par zone, bande passante, rapport.

### Lot 14 — interface mobile complète

Commandes tactiles complètes, nouvelle interface pensée pour le PC et le mobile (remplace l'IMGUI), performances mesurées sur le Note8 et l'iPhone 13, même personnage et même serveur sur PC et mobile.

### Première alpha (lots 15 et suivants)

Périmètre à définir avec Damien (L11-D12) : testeurs, boucle jouable, persistance, plateformes, contenu, critères de stabilité. Recommandations du cadrage :

- **Classes** : deuxième et troisième classes (mage du Seuil proposé, Q17).
- **Progression refondue** : livres (deux actifs), héritages, maîtrises, budget de points, plafond (Q18, H6, H7).
- **Donjon** : boss garanti et réveil renforcé (H4) ; groupes (taille Q31).
- **Régions** : les Vallons et d'autres selon le chiffrage ; trois régions relèvent d'une alpha élargie (Q16, Q22).
- **Le reste** : échanges directs et commandes, refuge de base, textes en français et en anglais, production artistique (méthode et budget, Q39), outils de retour des testeurs (Q40).

### Jeu complet

Havre d'Ambre et les cinq régions, Cœur du Seuil, marchés régionaux et caravanes, guildes, factions et transformations des régions, montures et voyages, boss de monde, combat entre joueurs volontaire, refuge complet, compagnons, Atlas de la Rupture, jour, nuit et météo, modèle économique.

## 6. Validations ciblées et conception transverse

### Règles sensibles et leur preuve

| Règle | Preuve prévue | Lot |
|---|---|---|
| **Mort unique** | Un ennemi mort n'agit plus, n'est plus une cible valide, et sa mort ne produit qu'un événement, même s'il est touché plusieurs fois au même tick ; propriétés sur combats aléatoires | 11.1 |
| **Portée et esquive réelles** | Hors de portée ou derrière un obstacle, la compétence est refusée. Un combattant sorti de la zone au tick d'impact n'est pas touché, resté dedans il l'est (frontière exacte, au tick près). Propriétés sur positions aléatoires, journaux de référence ; en réseau, mesure de l'esquive de 0 à 200 ms | 11.4, 11.7 |
| **Défenses par attaque** | Pour chaque attaque, seules ses défenses déclarées s'appliquent ; la garde seule ne neutralise pas une zone persistante ; un coup reçu hors de la couverture du bouclier n'est ni gardé ni paré | 11.4 |
| **Caméra sans effet sur les règles** | Le même combat scripté joué dans les deux vues donne le même journal, octet par octet ; essai de Damien | 11.6, 11.9 |
| **Entrée par l'état courant** | Un client qui arrive pendant un affrontement, ou qui revient après une coupure, a le même état que le serveur sans rejouer le passé, et aucun événement déjà consommé n'est réattribué | 11.7, 13 |
| **Conservation après défaite** | Propriété : après toute défaite, niveaux, XP, objets, livres, métiers et étapes acquises sont inchangés ; seuls les consommables utilisés manquent | 12.2, 12.7 |
| **Récompenses attribuées une fois** | Registre à clés uniques (personnage, rencontre, palier). Ni défaite, ni redémarrage, ni reconnexion, ni message en double, ni sauvegarde rechargée ne rendent deux fois le premier palier. La suite renforcée n'est récompensée que vaincue | 12.7, 13 |
| **Conditions des secrets** | Évaluateur de conditions : acquis durables gardés, circonstances temporaires vérifiées au déclenchement ; chaque combinaison testée, dans n'importe quel ordre ; retour clair dans le journal | 12.5 |
| **Deux livres et budget** | Le validateur des builds refuse un troisième livre ; une copie ne cumule pas ; les points reviennent au retrait ; le budget n'est jamais dépassé | alpha |
| **XP de métier indépendante** | Fabriquer ne donne pas d'XP de combat, combattre pas d'XP de métier ; une fabrication triviale forme moins | 12.6 |
| **Portée des choix narratifs** | Chaque choix a sa portée (personnelle, expédition, publique). Deux joueurs aux choix différents dans la même zone voient chacun la leur ; un PNJ indispensable n'est jamais supprimé pour les autres | 12.5, 13 |
| **Sauvegarde et reprise** | Sauvegarder puis charger redonne exactement le même état. Une coupure pendant l'écriture laisse l'ancienne sauvegarde intacte. Versions et migrations sont testées ; un fichier corrompu est refusé avec un message clair | 12.2, 13 |

### Sujets de conception, avec leurs critères (proposés, à valider par les essais)

- **Interface PC et mobile** (premier essai au 11.8, interface complète au lot 14) :
  - lisible en 1920 × 1080 et sur l'écran du Note8 ;
  - cibles tactiles d'au moins 9 mm ;
  - actions de combat atteignables au pouce ;
  - aucune information indispensable portée par la seule couleur.
- **Animations** : chaque coup ennemi est annoncé par une animation et une zone lisibles dans les deux vues. Les animations suivent les ticks et ne décident d'aucun résultat (le moteur décide).
- **Sons** : un son distinct pour le blocage, la parade parfaite, la parade ratée et la rupture de garde ; une alerte sonore pour un coup dangereux hors du champ.
- **Retours visuels** : les effets laissent voir le terrain, les adversaires et les annonces (H14). Rareté, réveil renforcé et « hors de portée » sont reconnaissables.

## 7. Estimation

Indicative, non engagée. **Révisée le 23 octobre 2026**, après la démonstration réseau (11.7), le premier essai tactile (11.8) et l'essai du 11.9 (L11-D12). La première estimation (20 octobre) est gardée plus bas pour comparaison.

**Ce que le lot 11 a montré.**

- **Rythme** : les neuf sous-lots du lot 11 ont été fusionnés du 20 octobre (1 h 48) au 23 octobre (1 h 46), soit environ trois jours au lieu des deux à quatre semaines estimées. Le temps s'est surtout passé en revues Codex : la PR du 11.4d (instantané d'état) en a demandé une trentaine de tours, celles du 11.5 une vingtaine ; les PR sans format nouveau sont passées en quelques tours.
- **Essais** : Damien a essayé le 11.6, le 11.7 et le 11.8 le jour même (hypothèse « essais sous 24 heures » confirmée pour ce lot). Ce qui a coûté du temps, ce sont les installations (Android Build Support, place sur le disque, pare-feu, scène à enregistrer) et un défaut propre au jeu construit (shader non emporté, PR #37).
- **Risques levés** : positions et zone partagée éprouvées en petit (moteur spatial, entrée par l'état courant, arrivée en plein combat, deux postes simulés, Note8). Restent neufs : sauvegarde persistante, contenu en quantité (quêtes, objets, PNJ), interface qui remplace l'IMGUI, hébergement sur Internet (dépense), iOS (pas de Mac).

**Hypothèses** : le rythme du lot 11 (sous-lots fusionnés en quelques heures, revues comprises), sauf pour les sous-lots qui introduisent un format persistant ou de nombreuses règles (comptés comme le 11.4d) ; essais de Damien sous 24 heures ; formes simples jusqu'à l'alpha ; aucune dépense.

| Lot | Sous-lots | Dont grands | Durée indicative révisée | Avant (20 octobre) | Incertitude |
|---|---|---|---|---|---|
| 11 | 9 (+ correctif #37 et suites du 11.9) | 3 | fait en environ 3 jours | 2 à 4 semaines | — |
| 12 | 9 | 3 (objets, quêtes, Unity) | 1 à 2 semaines | 2 à 4 semaines | moyenne : beaucoup de règles de jeu à faire valider (quêtes, objets, laisse), contenu en données ; la sauvegarde est le premier format persistant |
| 13 | environ 6 | 2 (serveur de zone, persistance sur le serveur) | 1 à 2 semaines en réseau local | 2 à 4 semaines | moyenne : l'architecture est éprouvée (11.7) ; comptes, chiffrement et hébergement sur Internet restent neufs, et l'hébergement est une dépense à décider |
| 14 | environ 4 | 2 (nouvelle interface, mesures sur appareils) | 1 à 2 semaines pour Android | 1 à 3 semaines | moyenne pour Android (commandes au doigt déjà essayées sur le Note8) ; **iOS non estimable** sans Mac ni chaîne de compilation |
| Alpha | à chiffrer | — | non estimée | non estimée | périmètre à définir (L11-D12) ; contenu et production artistique y pèsent désormais plus que la technique |

Ces durées s'additionnent si les lots se suivent (environ 3 à 6 semaines pour les lots 12 à 14, contre 6 à 11 avant) ; leur somme n'est ni une estimation indépendante ni une date de livraison. Elles ne comptent ni les dépenses (hébergement, appareils, Mac), ni le temps de décision de Damien sur les règles de jeu, qui devient le facteur principal au lot 12.

## 8. Décisions prises et points restants

**Décisions du 20 octobre 2026** : L11-D1 à L11-D12, toutes retenues ([registre, section 5](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12)). Elles autorisent le 11.1 et fixent l'ordre ci-dessus.

**À proposer par Claude, puis à valider** :

- **Architecture de référence** (11.2) et **règles du combat dans l'espace** (11.3) : validées par Damien le 20 octobre 2026 à 01 h 48 ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md), [COMBAT_ESPACE.md](COMBAT_ESPACE.md)).
- **Contact joueur–créature** (section 4.1) : **A retenu** par Damien à l'essai du 22 octobre 2026 ([rapport 11.9](ESSAI_LOT_11.md#7-rapport-de-lessai-sous-lot-119)).
- **Ralentissement en garde** : **la moitié de la vitesse retenue** à l'essai du 22 octobre 2026.

**Informations attendues de Damien**, sans bloquer les sous-lots 11.1 à 11.7 :

- versions d'Android (Note8 : **Android 9**, relevé le 22 octobre 2026) et d'iOS (iPhone 13) installées ;
- accès à un Mac ou à une chaîne de compilation iOS ;
- disponibilité pour les essais ;
- plus tard, budget et horizon.

Aucune dépense n'est engagée sans son accord.
