# Feuille de route — Éclats du Seuil

Source de vérité pour les agents : le lot en cours, les décisions validées par Damien et la suite prévue. Les chiffres de gameplay sont des valeurs initiales **provisoires**, pas un équilibrage définitif. Toute modification d'une décision validée passe par Damien.

## Vision

MMORPG fantasy original PC/mobile, monde partagé et persistant à terme, **principalement jouable en solo** (décision du 20 octobre 2026, L11-D9 : **aucune voie essentielle de progression n'exige un groupe** ; des expéditions peuvent être réservées aux groupes ; voies essentielles : campagne principale, progression de classe, métiers, builds viables ; remplace « aucun groupe imposé pour la moindre activité »), combats dynamiques en temps réel, progression profonde, classes aux identités mécaniques très distinctes, trois arbres de talents combinables par classe, synergies, exploration, métiers, économie, coopération facultative, cross-play PC/mobile.

**Cadrage des 19 et 20 octobre 2026** (choix de Damien, détail et statuts dans [CADRAGE.md](CADRAGE.md)) :
- **Direction artistique** : fantasy stylisée (direction graphique). « Plutôt sombre » (vision précédente) décrit une ambiance et coexiste avec elle : Vallons accueillants, donjons inquiétants, Seuil étrange dans une même identité (L11-D10). Déclinaison proposée : volumes doux, matières peintes, couleurs expressives, ambiance propre à chaque région.
- **Deux vues au choix du joueur** : vue haute de trois quarts (vue 1) et troisième personne montrant le personnage entier (vue 2), en exploration comme en combat ; remplace « caméra non tranchée ».
- **Positions réelles en combat** (Q13).
- **Première région** : les Vallons d'Aube.

## Progression validée (configuration actuelle, provisoire)

Toujours en vigueur ; paramètres de `rules.json`. Le cadrage du 19 octobre 2026 laisse le plafond final ouvert (Q18) et prévoit une refonte avec les livres de build, les héritages et les maîtrises (H6, H7), proposée au chantier de l'alpha (plan, décision L11-D1) ; elle se fera avec Damien.

- Niveau maximal provisoire 30 ; 1 point de talent par niveau à partir du niveau 2, soit 29 points au niveau 30.
- Trois arbres librement combinables. Talent majeur : 18 points dans son arbre + prérequis, puis 1 point. Un seul majeur par build ; un build sans majeur est valide.
- Actives : 2 (niv. 1), 3 (niv. 5), 4 (niv. 10). Ultime : 1 à partir du niveau 10. Passifs équipables (distincts des talents) : 1 (niv. 1), 2 (niv. 5), 3 (niv. 20), 4 (niv. 30).
- Attributs et progression de long terme : chantier ultérieur. Aujourd'hui, les attributs sont écrits dans chaque build. Retenu le 20 octobre 2026 (Q20, L11-D10) : pour le personnage persistant, une base fixée par la classe et le niveau, modifiée par l'équipement et le build, sans répartition manuelle (lot 12) ; builds et scénarios d'essai peuvent garder des valeurs explicites, versionnées.

## Ordre des chantiers

`8.1 → 8.2 → 8.3 → 8.4 → revue PR 8a → 8.5 → 8.6 → 8.7 (lot 8 terminé le 18 octobre 2026) → prototype Unity 3D (lot 9 : 9.1 → 9.2 → 9.3 → 9.4, terminé le 19 octobre 2026) → serveur autoritaire à deux joueurs (lot 10 : 10.1 → 10.2 → 10.3 → 10.4 → 10.5 → 10.6, terminé le 19 octobre 2026) → tranche des Vallons d'Aube`

Ordre validé par Damien le 20 octobre 2026 (L11-D1, détail dans le [plan](PLAN_LOT_11.md#3-ordre-des-chantiers-validé-le-20-octobre-2026)) : `lot 11 combat dans l'espace (11.1 → 11.9, terminé le 23 octobre 2026) → lot 12 les Vallons en solo → lot 13 monde partagé et persistance → lot 14 interface mobile complète → première alpha → jeu complet`.

### Lot 8 — premier Bastion réellement jouable

Deux PR de gameplay (A11) : **8a moteur** (8.1 à 8.4, PR #3, #6, #7 et #8, fusionnées le 18 octobre 2026), puis **8b contenu et mesures** (8.5 à 8.7).

| Sous-lot | Contenu | État |
|---|---|---|
| 8.1 | X1 modificateurs permanents, X2 conditionnels (`whileGuarding`, `perRampartCharge`, `perResonanceStep`), règles effectives par combattant, plafonds | PR #3 (brouillon) ; décisions du 18/10 appliquées (moteur 0.11.0, schéma de règles 6) |
| 8.2 | X3 renforcements temporaires, X4 boucliers d'absorption (catégorie `shield`, un bouclier par source, consommation FIFO, absorbe aussi les DOT, fin par durée ou épuisement, champ `absorbed`), réduction de dégâts et modificateurs temporaires | PR empilée sur la PR #3 (brouillon) : catégories `buff` et `shield`, schéma de règles 7 et de classe 6, moteur 0.12.0, journal au format 6 ; aucun contenu livré |
| 8.3 | X5 ciblage `self` / `ally` / `allAllies`, X6 transfert de dégâts, X7 provocation, X9 rôle `ally` des déclencheurs | PR #7, empilée sur la PR #6 (brouillon) : compétences de soutien, catégories `taunt` et `protection`, règles en schéma 8, classes en 7, scénarios en 3, moteur 0.13.0, journal au format 7 ; aucun contenu livré ; deux tours de revue Codex traités |
| 8.4 | X8 Contrecoup consomme toutes les Charges, fiches complètes des compétences restantes et de l'ultime, L8-D2, aucun `notImplemented` restant | PR empilée sur la PR #7 (brouillon) : fiches complètes du Bastion, Charges dépensées et coefficient par Charge, `resonanceGain`, `guard.noUpkeep`, `combat.triggers.areaStrikeMinCooldownMs` (3 s), rupture de garde limitée aux contrôles et modificateurs ; règles en schéma 9 (contenu 0.7.0), classes en 8 (contenu 0.3.0), moteur 0.14.0, journal au format 8 |
| 8.5 | Contenu complet du Bastion (talents, intermédiaires, passifs), plus aucun `[TEST]`, builds de référence en données | PR #9, fusionnée le 18 octobre 2026 : talents, intermédiaires et passifs en données (modificateurs et déclencheurs existants, moteur inchangé), altérations `reactiveAegis`, `protectiveLink`, `oathTaunt`, fichier de builds de référence (`kind: "builds"`, schéma 1) ; règles au contenu 0.8.0, Bastion 0.4.0 |
| 8.6 | X10 compteurs de mesure, scénarios S1 à S5, commande CLI `report`, catégorie `Scenario` | PR #10, fusionnée le 18 octobre 2026 : compteurs hors journal (moteur 0.14.0 et journal inchangés), API de pilotage (`View`, `Submit`), pilote déterministe à trois profils, scénarios `scenarios/mesures`, `eds report` (texte, JSON `measureReport` 1, journaux rejouables), relevé initial dans [BUILDS.md](BUILDS.md#mesures-des-builds-sous-lot-86) ; deux tours de revue Codex |
| 8.7 | Passe d'équilibrage mesurée, surtout en données ; rapport de mesures et écarts | PR #11, fusionnée le 18 octobre 2026 : décision 8 (Provocation inflige 0,8), Bastion au contenu 0.5.0, tous les objectifs provisoires atteints ([RAPPORT_8_7.md](RAPPORT_8_7.md)) |

Ordre défensif attendu en 8.2 : garde ou parade selon leurs règles, puis bouclier, puis PV. Tests 8.2 attendus : bouclier épuisé pendant un impact, plusieurs boucliers, DOT successifs, mort d'un personnage protégé, réservation exacte, aucun dégât négatif ni effet partiel.

Règles 8.3 : transfert non récursif et non notifiable aux déclencheurs offensifs, conservation des dégâts à l'arrondi près, garde du protecteur seulement selon Serment, protection retirée à la mort du protecteur, allié mort ou hostile refusé, provocation d'un personnage refusée et journalisée (`notApplicable`), provocation la plus récente prioritaire, mort du provocateur gérée.

Tests 8.4 attendus : riposte automatique de zone avec recharge interne 2 950 ms refusée, 3 000 ms acceptée ; Charges insuffisantes ; cibles invalides ; interactions de ressources ; ultimes, boucliers et provocations.

### Lot 9 — prototype Unity 3D

Objectif (« Après le lot 8 », point 1) : importer le cœur C# sans le réécrire et jouer le Bastion dans Unity, graphismes provisoires. Le cœur reste la seule source de vérité du combat ; Unity affiche et transmet les commandes.

| Sous-lot | Contenu | État |
|---|---|---|
| 9.1 | Données portables : lecture JSON stricte sans dépendance (analyseur maison), `EdS.Data.Json` en netstandard2.1 / C# 9, utilisable par Unity et le futur serveur ; validation, empreintes et journaux inchangés ; mesures déplacées dans `EdS.Tools` (outils .NET) | PR #12, fusionnée le 18 octobre 2026 ; revue Codex : nombre de valeurs JSON borné pendant l'analyse et arbre compact (`InputLimits.MaxJsonValues`) |
| 9.2 | Pont temps réel (`EdS.Runtime`, netstandard2.1) : temps réel → ticks de 50 ms, saisie → commandes, vues pour l'affichage, session d'entraînement depuis les données | PR #13, fusionnée le 18 octobre 2026 : `RealtimeCombat`, `TrainingSession`, compteurs dans la vue du moteur (journal inchangé), catégorie de tests `Runtime` ; revue Codex : dernière demande de compétence du tick, garde et lancer admis ensemble (`CombatEngine.SubmitAll`), journal écrit en fin de partie (`WriteLog`) |
| 9.3 | Projet Unity (`unity/`) : paquet local du cœur, arène générée, personnage, caméras isométrique et troisième personne comparées, barres de ressources, garde et parade visibles, compétences, ennemis d'entraînement, altérations ; guide d'installation pour Windows | PR #14, fusionnée le 19 octobre 2026 : paquet `com.eclats.core` compilé par Unity depuis `src/`, sorties .NET dans `artifacts/`, jeu construit par le code (arène, caméras, interface IMGUI, télégraphie), rendu URP configuré à l'ouverture, guide [UNITY.md](UNITY.md) ; essayé par Damien le 18 octobre 2026 (Unity 6000.3.26f1) : le jeu se lance, Console sans erreur |
| 9.4 | Première interface de talents (builds de référence, répartition des points validée par `BuildValidator`) | PR #16, fusionnée le 19 octobre 2026 : `TalentEditor` (point par point, refus atomiques avec raison), `TrainingSession.WithCustomBuild`, écran des talents dans Unity (touche T) ; essayé par Damien dans Unity 6000.3.26f1, Console sans erreur |

**Décisions du 18 octobre 2026** (prototype, questions posées avant le 9.1) : Unity 6.3 LTS (suivi jusqu’en décembre 2027), licence Personal gratuite ; essais sur le poste de Damien sous Windows ; projet Unity dans ce dépôt (`unity/`) ; rendu URP. Unity ne pouvant pas tourner dans les sessions cloud, les essais dans l'éditeur sont faits par Damien ; tout ce qui ne dépend pas d'Unity est testé en CI.

**Décision du 18 octobre 2026** (essai du 9.3) : les deux caméras plaisent à Damien ; l'isométrique et la troisième personne sont toutes deux gardées, au choix du joueur (touche C).

### Lot 10 — serveur autoritaire à deux joueurs

Objectif (« Après le lot 8 », point 2) : deux joueurs dans le même combat, résolu par un serveur avec le même cœur ; commandes invalides refusées, déconnexion et reconnexion, latence, désynchronisation et rejeu. Pas d'infrastructure massive avant cette preuve. Détail : [RESEAU.md](RESEAU.md).

| Sous-lot | Contenu | État |
|---|---|---|
| 10.1 | Protocole (messages stricts et bornés, version 1), transport TCP sans dépendance, réseau simulé déterministe pour les tests | PR #18, fusionnée le 19 octobre 2026 : `EdS.Net` (netstandard2.1), `MessageCodec`, `Framing`, `TcpNetConnection`, `SimulatedNetwork`, catégorie de tests `Network` |
| 10.2 | Serveur : salle à deux places, horloge de 50 ms, intentions rangées au tick, refus motivés, limites de débit, déconnexion et reconnexion, journal rejouable ; serveur dédié | PR #19, fusionnée le 19 octobre 2026 : `CombatRoom`, `RoomHost`, `EdS.Server`, `IntentionPlanner` (partagé avec le pont temps réel), `HashingCombatLog`, `CombatLogReplayer.ReadSetup`, scénario `coop/duo_meute` |
| 10.3 | Client réseau : combat local rejoué depuis les commandes du serveur, contrôle d'empreinte, resynchronisation, reconnexion | PR #20, fusionnée le 19 octobre 2026 : `NetworkClient`, `NetworkCombat`, `ICombatClient`, `CombatDisplay` et `FeedbackTracker` (affichage partagé avec le solo) |
| 10.4 | Unity : héberger ou rejoindre, salle d'attente, deux Bastions, ciblage d'un allié, ping | PR #21, fusionnée le 19 octobre 2026 : `NetworkSession` (héberger, rejoindre, retour automatique), `BotPlayer` (équipier robot, aussi `EdS.Server --robot`), menu réseau, salle d'attente et combat à deux dans Unity (touche M) ; essayé par Damien le 19 octobre 2026 au soir (branche du 10.6) : tout fonctionne, touches corrigées pour l'AZERTY au 10.6 |
| 10.5 | Mesures : latence (0 à 200 ms), gigue, désynchronisations et coupures provoquées ; rapport, notamment sur la parade | PR #22, fusionnée le 19 octobre 2026 : `eds reseau` (`EdS.Tools.Network`), rapport [RAPPORT_10_5.md](RAPPORT_10_5.md) : aucune divergence, retour en 130 ms, parade à vue perdue dès 50 ms d'un sens, parade anticipée du ping tenue jusqu'à 200 ms ; options A à D pour la compensation |
| 10.6 | Parade en réseau, **option B** (décision ci-dessous) : télégraphie des assaillants avancée du temps aller-retour lissé, côté client | PR #23, fusionnée le 19 octobre 2026 : `NetworkClientOptions.TelegraphAdvance` (actif par défaut, plafond de 1 s), `NetworkCombat.TelegraphLeadSeconds`, bandeau Unity ; affichage seulement, serveur, moteur et protocole inchangés ; `eds reseau` mesure la parade « anticipée » sur cette télégraphie : 69 à 76 % de parades parfaites jusqu'à 200 ms d'un sens (0 % à vue dès 50 ms) ; robot réglé au moment du solo ; essayé par Damien le 19 octobre 2026 au soir : tout fonctionne ; lettres lues par position (Z Q S D, A / E et M en AZERTY, demande de Damien) |

**Décisions du 19 octobre 2026** (avant le 10.1) : **coopération** (deux Bastions contre des assaillants) ; un joueur déconnecté laisse le **combat continuer**, son personnage inactif (garde relâchée), avec 30 secondes pour se reconnecter. La compensation de latence de la parade sera décidée après les mesures du 10.5.

**Décision du 19 octobre 2026 à 11:28** (après le rapport du 10.5) : la parade en réseau est compensée par l'**option B**, une **télégraphie avancée du ping côté client** : la barre et le compte à rebours du prochain coup s'affichent en avance du temps aller-retour mesuré, et le joueur pare « à vue » sur cet affichage. Aucune règle ne change ; le serveur juge toujours la parade sur son tick (aucune triche possible). Options écartées pour l'instant : A (rien), C (rembobinage borné côté serveur, gardé pour un serveur sur Internet), D (fenêtre élargie en réseau).

### Lot 11 — combat dans l'espace (terminé le 23 octobre 2026)

Objectif : un combat où se placer compte, résolu par le serveur, lisible dans les deux vues, jouable à deux et au pouce. Détail, résultats observables et validations : [PLAN_LOT_11.md](PLAN_LOT_11.md#4-lot-11--combat-dans-lespace).

| Sous-lot | Contenu | État |
|---|---|---|
| 11.1 | Ennemis qui meurent : PV des assaillants, mort comme état de la simulation (plus d'action, cibles invalidées, un seul événement), cycle de vie prêt pour réapparition et réveil, victoire des joueurs | **Fusionné** (PR #25, 20 octobre 2026). Autorisé le 20 octobre 2026 (L11-D1) : `health` facultatif des assaillants (scénarios en schéma 4), tour d'un mort abandonné sans ligne, victoire des joueurs, moteur 0.15.0, journal au format 9, référence `reference-0.15.0-contenu-0.5.0` (corps des journaux livrés inchangé) ; scénarios `meute_a_vaincre` (solo) et `coop/meute_a_vaincre` (Unity) ; « Victoire » ou « Défaite » dans Unity ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md) ; essayé par Damien le 20 octobre 2026 à 01 h 48 : « très bien, le niveau est bien » |
| 11.2 | Architecture de référence du monde partagé (document, L11-D8) | **Fusionné** (PR #26, 20 octobre 2026). [ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md), une simulation déterministe par zone, entités qui entrent et sortent, instantané d'état, état filtré par zone d'intérêt et prédiction du joueur local, événements consommables à identité unique ; quatre points **retenus par Damien le 20 octobre 2026** (section 9) |
| 11.3 | Règles du combat dans l'espace (document, L11-D2 à L11-D7) : couverture du bouclier, contact joueur–créature, portées du Bastion | **Fusionné** (PR #27, 20 octobre 2026). [COMBAT_ESPACE.md](COMBAT_ESPACE.md), espace et surfaces, déplacement, collisions, ciblage mixte, attaques annoncées et défenses déclarées, formats prévus ; cinq propositions **retenues par Damien le 20 octobre 2026** (section 10), contact joueur–créature : règle A retenue à l'essai du 22 octobre 2026 |
| 11.4 | Moteur spatial, en quatre PR : 11.4a positions, déplacement et décor ; 11.4b portées et ciblage ; 11.4c attaques annoncées au sol et garde de face ; 11.4d combattants qui arrivent en cours de combat et instantané d'état | **Fusionné** (PR #28, #30, #31 et #32, 20 et 22 octobre 2026). 11.4a (PR #28) : combat positionné (`space`, places, `move` et `stop`), vitesse de base 5 m/s, garde à la moitié, gel qui ralentit, décor solide et glissement, joueurs qui se traversent, créatures qui s'évitent, contact A et B réglable (A livré, garde-fou anti-piège), étourdissement qui immobilise (décision du 20/10/2026 à 03 h 38), moteur 0.16.0, journal au format 10, scénarios en schéma 5, règles en schéma 10 ; scénario `espace_passage` (le Bastion tient un passage) ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-114a--positions-déplacement-et-décor). 11.4b (PR #30) : portées du Bastion retenues le 20/10/2026 (mêlée 2,5 m, Provocation et Barrière 15 m, Onde 5 m, Bastion absolu 15 m, Riposte précise 2,5 m, Jugement 5 m, Serment 15 m, lien protecteur 15 m), refus `targetOutOfRange` et `noLineOfSight` au lancement, groupes jugés à l'impact, moteur 0.17.0, journal au format 11, classes en schéma 9 (Bastion 0.6.0), règles en schéma 11 ; scénario `espace_portees` ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-114b--portées-et-ligne-de-vue). 11.4c (PR #31) : attaques annoncées au sol (coup dirigé, cône, cercle, bande, zone persistante), impact au tick près, annonces évitables, défenses déclarées par attaque, arc du bouclier de 180° et garde tournée vers la cible sélectionnée (retenus le 20/10/2026), moteur 0.18.0, journal au format 12, scénarios en schéma 6, règles en schéma 12 ; scénario `espace_annonces` ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-114c--attaques-annoncées-et-garde-de-face) ; trois questions posées à Damien dans la PR #31 (contrôle pendant une annonce, vitesse de rotation en garde, provocation et zones visées), tranchées à l'essai du 22 octobre 2026 (rapport 11.9). 11.4d (PR #32) : arrivées au début de leur tick (refusées si la place est prise), arrivée pilotée sans place réservée (`Join`), départs en fin de tick (`leave`), absents inertes, instantané d'état canonique relu sans rejouer le passé (aller-retour exact), moteur 0.19.0, journal au format 13, scénarios en schéma 7 ; scénario `espace_renfort` ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-114d--arrivées-départs-et-instantané-détat) |
| 11.5 | Ennemis dans l'espace | **Fusionné** (PR #33, 22 octobre 2026). Comportements en données (`behavior` : distance maximale et minimale, engagement, encerclement), cible déterministe (provocation, cible de scénario, sinon le plus proche), engagement par la meute, arrêt pendant l'annonce, charge le long d'une bande ; moteur 0.20.0, journal au format 14, scénarios en schéma 8, instantané au format 2 ; scénario `espace_meute` ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-115--comportements-des-ennemis) |
| 11.6 | Scène de démonstration Unity, vues 1 et 2 (H14) | **Fusionné** (PR #34, 22 octobre 2026), essayé par Damien le 22 octobre 2026 (rapport 11.9). Scène des Vallons construite par le code depuis `scenarios/vallons_demo.json` (moulin, rivière et pont, arbres, abri sous un toit, passage étroit), déplacement simulé par le moteur, ennemis cachés par le décor, annonces au sol, caméra réglable de la vue 1 à la vue 2 (molette, rappel par C, réglage gardé) ; mode d'emploi : [UNITY.md](UNITY.md#4-jouer) ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-116--scène-de-démonstration-des-vallons) |
| 11.7 | Démonstration réseau minimale : arrivée pendant un affrontement, retour à l'état courant, latence | **Fusionné** (PR #35, 22 octobre 2026). Protocole 2 (marche et sélection transmises, état courant `state` au lieu de l'historique), arrivée d'un joueur pendant le combat (`RoomOptions.JoinDuringCombat`, place libre cherchée autour de la sienne), retour et resynchronisation sans rien rejouer ni réattribuer, prédiction du déplacement local, Vallons à deux dans Unity (avec un robot qui arrive après 10 s), esquive mesurée de 0 à 200 ms (`eds reseau --esquive`) ; détail : [RESEAU.md](RESEAU.md#protocole-2-et-démonstration-réseau-sous-lot-117) ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-117--démonstration-réseau-minimale) |
| 11.8 | Premier essai tactile (simulateur de l'éditeur, Galaxy Note8 ; iPhone 13 si une chaîne iOS est disponible) | **Fusionné** (PR #36, 22 octobre 2026, et correctif #37, 23 octobre 2026), essayé par Damien sur le Note8 le 23 octobre 2026 (rapport 11.9). Stick du pouce gauche, boutons d'action, gestes de caméra (glisser, pincer entre les vues 1 et 2), données emportées dans le jeu construit (recopiées au premier lancement sur Android), réglages allégés sur téléphone ; guide pas à pas : [UNITY.md](UNITY.md#7-jouer-sur-téléphone-sous-lot-118) ; limites : [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-118--premier-essai-tactile) |
| 11.9 | Essai, rapport, choix du contact joueur–créature, réestimation des lots 12 à 14 | **Fusionné** (PR #38, #39 et #40, 23 octobre 2026 ; lot 11 terminé). Fiche d'essai : [ESSAI_LOT_11.md](ESSAI_LOT_11.md) ; essai fait par Damien le 22 octobre 2026 (PC, puis Galaxy Note8 après la PR #37). Suites en trois PR : 11.9a (#38) réglages du moteur (obstacle bas, bande qui repousse, étourdissement qui annule l'attaque, arrêt pendant l'annonce et meute réglables, marche à reculons, protocole 3) ; 11.9b (#39) réglages de Unity (recul en vue 2, cible perdue remplacée, scène de démarrage) ; 11.9c (#40) rapport et réestimation des lots 12 à 14. Rapport : [ESSAI_LOT_11.md](ESSAI_LOT_11.md#7-rapport-de-lessai-sous-lot-119), section 7 ; choix consignés dans le [registre](CADRAGE.md#5-bis-choix-de-lessai-du-22-octobre-2026-sous-lot-119) ; réestimation : [plan](PLAN_LOT_11.md#7-estimation), section 7 (lots 12 à 14 : environ 1 à 2 semaines chacun, iOS non estimable sans Mac) |

**Décisions du 20 octobre 2026** (L11-D1 à L11-D12, validées par Damien ; texte complet dans le [registre](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12)) :

- **D1 ordre** : positions d'abord, architecture avant le moteur spatial, démonstration réseau et essai tactile dès le lot 11 ; 11.1 autorisé.
- **D2 visée** : ciblage mixte, conditions spatiales vérifiées par le moteur.
- **D3 garde** : garde en marchant, ralentie, valeur réglée par essais ; couverture du bouclier explicite.
- **D4 esquive** : pas de roulade universelle au premier lot ; esquive par le déplacement.
- **D5 collisions** : ni blocage ni poussée passive entre joueurs ; règle explicite pour le contact joueur–créature.
- **D6 hauteur** : sol, pentes et étages reliés, pas de saut de combat.
- **D7 défenses** : déclarées attaque par attaque.
- **D8 architecture** : simulation de zone sous autorité du serveur, entrée depuis l'état courant.
- **D9 groupes** : voir la Vision.
- **D10 tranche** : recommandations du cadrage retenues.
- **D11 téléphones** : Galaxy Note8 et iPhone 13.
- **D12** : budget et horizon non fixés ; estimation à refaire après les premières démonstrations.

**Décisions du 20 octobre 2026 à 01 h 48** (Damien : « pour les 9 questions on garde tes avis », après l'essai du 11.1 : « le niveau est bien ») :

- **Architecture** ([ARCHITECTURE_MONDE.md](ARCHITECTURE_MONDE.md), section 9) : une simulation déterministe par zone sous autorité du serveur, les combats sur place en font partie, la zone tourne sur le poste en solo ; mises à jour par état filtré (zone d'intérêt) avec prédiction du joueur local, le rejeu intégral du lot 10 restant pour les combats fermés ; même modèle pour les donjons et expéditions ; TCP gardé pour la démonstration du 11.7, avec mesures.
- **Règles du combat dans l'espace** ([COMBAT_ESPACE.md](COMBAT_ESPACE.md), section 10) : garde et parade valent contre les coups venus des 180° de devant, de dos dégâts pleins ; en garde, le Bastion fait face à sa cible ; portées du Bastion (mêlée à 2,5 m, Onde de rupture à 5 m, Provocation et soutiens à 15 m, Riposte précise en mêlée seulement) ; cible, direction et point verrouillés au lancement ; valeurs de départ des essais (5 m/s, garde à la moitié, 300 ms de réaction, emprises de 0,5 à 1,2 m).

**Décision du 20 octobre 2026 à 03 h 38** (Damien : « oui », à la question de la PR #28) : **l'étourdissement immobilise**. Il bloque aussi le déplacement (blocage `movement` dans les données), en plus des compétences, de la garde, de la parade et des attaques.

### Lot 12 — les Vallons d'Aube en solo (terminé le 26 octobre 2026)

Objectif : parcourir les Vallons seul, du niveau 1 au niveau 10, avec la mission « Les traces sous le moulin » jusqu'à un chef de fin. Détail : [PLAN_LOT_11.md](PLAN_LOT_11.md#lot-12--les-vallons-daube-en-solo) ; le monde : [MONDE.md](MONDE.md) ; limites : [LIMITES_LOT_12.md](LIMITES_LOT_12.md). Lancé par Damien le 23 octobre 2026 (« Go 12 fait le max tout seul » ; fusion autorisée quand la CI et la revue Codex sont vertes).

| Sous-lot | Contenu | État |
|---|---|---|
| 12.1 | Monde : zone des Vallons, entités qui entrent et sortent, combats sur place, temps du monde, sauvegarde | **Fusionné** (PR #42, 23 octobre 2026). Projet `EdS.World` (session de zone, événements consommables à clé unique, rencontres, temps du monde provisoire de 2 h par journée, sauvegarde versionnée et atomique), mode zone du moteur (pas de fin quand une équipe tombe, jusqu'à 24 h), scénario `zones/vallons` ; moteur 0.22.0, journal au format 16, scénarios en schéma 10 |
| 12.2 | Personnage : expérience et niveaux 1 à 10, attributs par classe et niveau, sauvegarde | **Fusionné** (PR #43, 23 octobre 2026). Progression en données (`world/progression.json`, valeurs provisoires), personnage persistant (niveau tiré de l'expérience, attributs de la table, build validé à l'entrée), partie en solo qui donne l'expérience des créatures vaincues, sauvegarde en version 2 (la version 1 est mise à jour) |
| 12.3 | Objets, équipement, inventaire | **Fusionné** (PR #44, 24 octobre 2026). Catalogue en données (`world/items.json` : quatre emplacements dont le bouclier, inventaire de 24 places, raretés à six rangs, 15 objets, tables de butin ; valeurs provisoires), bonus d'équipement compté par le moteur, inventaire et équipement atomiques, butin tiré par la clé de la mort et attribué une seule fois ; moteur 0.23.0, journal au format 17, scénarios en schéma 11, sauvegarde en version 3 |
| 12.4 | Les Vallons en données, populations et réapparitions, laisse | **Fusionné** (PR #45, 24 octobre 2026). Zone du monde en données (`world/zones/vallons.json` : points sûrs, sept lieux, dix populations, passage du moulin), carte agrandie (village, moulin, pré des loups, ruines, carrière, petite zone du Seuil), réapparition sous une nouvelle identité, laisse (la créature trop distancée lâche et revient au calme chez elle), passage du moulin ouvert pour de bon ; valeurs provisoires |
| 12.5 | PNJ, dialogues et quêtes | **Fusionné** (PR #46, 24 octobre 2026). Textes par clés (`world/texts/fr.json`), histoire en données (`world/story.json` : trois PNJ, dialogues à conditions et effets, mission « Les traces sous le moulin » en sept étapes, découvertes), confiance des PNJ et conséquence observable (amulette d'Odile), état narratif personnel dans la sauvegarde (version 4) ; textes et valeurs provisoires |
| 12.6 | Soin et métiers | **Fusionné** (PR #47, 24 octobre 2026). Soin dans le moteur (commande `consume`, charges, recharge partagée), potions et remèdes emportés au combat, métiers en données (`world/crafting.json` : forge, herboristerie, minage ; recettes ; six nœuds de récolte), expérience de métier séparée, ferrure forgée pour la porte du moulin ; moteur 0.24.0, journal 18, scénarios 12, instantané 3, sauvegarde 5 ; valeurs provisoires |
| 12.7 | Chef de fin et récompenses | **Fusionné** (PR #48, 24 octobre 2026). Le Gardien du Seuil à deux formes (zone en schéma 2, bloc `boss`), première victoire récompensée une seule fois (clé du registre du monde, lame du Seuil), butin à chaque victoire, défaite sans perte et reprise au point sûr, étape « chef » de la mission ; valeurs provisoires |
| 12.8 | Unity : la zone jouable, inventaire, quêtes, dialogues | **Fusionné** (PR #49, 25 octobre 2026). Le jeu s'ouvre sur l'aventure dans les Vallons (`AdventureSession` : monde, sauvegarde locale, pont temps réel sur la session de zone, cible parmi les proches), PNJ, nœuds et points sûrs à l'écran, dialogues, inventaire et équipement, journal et découvertes, atelier, potions, halte et reprise après défaite, sauvegarde automatique, messages et écrans par clés de texte ; boutons au doigt ; valeurs provisoires ; essayé par Damien le 25 octobre 2026 (12.9) |
| 12.9 | Essai complet et rapport | **Fait** : suites fusionnées (PR #50 à #53, 25 et 26 octobre 2026). Essai de Damien des 25 et 26 octobre 2026 sur PC et sur le Note8, fiche de 33 points ; suites en quatre PR : 12.9a (#50) créatures et Gardien rééquilibrés, forge de Maëlle visible ; 12.9b (#51) compétences gagnées en montant de niveau ; 12.9c (#52) loups qu'on sème, mission détaillée, garde au doigt ; 12.9d (#53) la carte. Rapport : [ESSAI_LOT_12.md](ESSAI_LOT_12.md) |

### Essai de style (entre le lot 12 et le lot 13)

Décidé par Damien le 25 octobre 2026 : voir les Vallons avec des modèles et des sons gratuits (packs CC0) avant le lot 13. Le jeu choisit les modèles et les sons déposés par Damien dans `unity/Assets/Style` (non suivi par git), habille le Bastion, les loups, les sangliers, les arbres et les rochers, ajoute des plantes et des sons ; sans pack, rien ne change. Mode d'emploi : [UNITY.md](UNITY.md#7-bis-essai-de-style-packs-gratuits). État : machinerie en PR (#55) ; essai réaliste en PR empilée, à essayer par Damien.

**Décisions du 26 octobre 2026** (premier essai de style dans Unity) :

- **Style low poly écarté** : « bof j'aime pas trop je voulais quelque chose de vraiment réaliste ».
- **Direction : réalisme stylisé** : « un mélange de réaliste et de stylisé coloré », « lumineux, réaliste mais pas trop » (référence : The Elder Scrolls), et pour l'essai « le plus réaliste possible ». Fichiers réalistes gratuits : Poly Haven (CC0 : sols, plantes, rochers, bois mort, ciel) et Mixamo (le paladin et ses animations, compte Adobe de Damien).
- **Animaux** : on peut rester sur des modèles gratuits pour l'instant (« si c'est seulement les textures qui sont impactées et pas le reste ») ; le jeu ne change pas, seul l'aspect change.
- **Le soir, après l'essai** : PNJ, bâtiments et rivière à faire aussi ; personnages Mixamo pour les trois PNJ, l'archer, le rôdeur et le Gardien, textures Poly Haven pour les bâtiments (choix de Damien dans la conversation).
- **Caméra à la souris libre, « comme ESO »** (choix de Damien parmi trois propositions) : sans clic, hors des écrans et des menus ; Alt tenu rend le curseur. La garde reste au clic droit.

**Décisions du 25 octobre 2026** (essai du 12.9, rapport : [ESSAI_LOT_12.md](ESSAI_LOT_12.md#4-décisions-de-damien-pendant-lessai)) :

- **Compétences** gagnées en montant de niveau (Barrière d'acier au niveau 5, Onde de rupture et Bastion absolu au niveau 10, aux emplacements des règles) ; un écran de choix des compétences viendra ensuite (« La 2 pour l'instant et la 3 après »).
- **Gardien** : la forme éveillée est plus forte que la première.
- **Petit essai de style** (packs gratuits de modèles, d'animations et de sons) **après le 12.9, avant le lot 13** (« oui on fera un petit essai de style », « apres le 12.9 oui »).
- **Relecture indépendante avant Codex** à partir du lot 13 : une relecture par un agent neuf avant chaque demande de revue Codex, une seule par PR (« Ok on fera ça ») ; consignée dans [CLAUDE.md](../CLAUDE.md#méthode-pour-un-sous-lot).

## Décisions validées

**A1 à A12** : pas de soin pour le Gardien au lot 8 (boucliers, protections, transferts, provocations) ; trois talents intermédiaires de cinq rangs par arbre (noms de travail) ; Fureur contenue dépend de la Résonance détenue ; Charges conservées (Rempart) ou dépensées (Contrecoup) selon le build ; une ultime commune, Bastion absolu ; les boucliers absorbent les DOT ; un bouclier par source, FIFO ; dégâts transférés jamais retransférés ni déclencheurs de réactions, garde du protecteur seulement dans le cadre de Serment ; provocation sur un personnage refusée `notApplicable` et journalisée ; attributs identiques pour les builds de comparaison ; deux PR de gameplay 8a puis 8b ; builds de référence livrés en données.

Aussi : aucun soin, aucune deuxième classe, aucun système de positions, aucune interface Unity avant la fin du lot 8 ; pas de déclencheurs d'équipement avant le chantier des objets.

Ces limites étaient liées au lot 8. Le cadrage retient ensuite les positions réelles (Q13) et, le 20 octobre 2026 (L11-D10), des potions de soin limitées, sans soin propre au Bastion (Q7) ; il recommande une classe à distance (Q17). A1 (ci-dessus, décisions du lot 8) ne visait que l'arbre Gardien au lot 8 ; le résumé « pas de soin pour le Bastion » du questionnaire était trop large (écart E4 du [cadrage](CADRAGE.md#7-écarts-avec-lexistant)). Le ciblage sans position du moteur (A7 du plan du simulateur v0.1, [MOTEUR.md](MOTEUR.md#ciblage-a7), à ne pas confondre avec A7 ci-dessus) reste vrai pour le moteur 0.14.0 jusqu'au moteur spatial (sous-lot 11.4) ; au moteur 0.16.0 (11.4a), un combat positionné a des positions et des collisions ; au moteur 0.17.0 (11.4b), des portées et une ligne de vue ; au moteur 0.18.0 (11.4c), des attaques annoncées au sol et une garde de face.

**L8-D1 à L8-D6** : frappes de réaction sans critique ni Résonance automatique ; toute riposte automatique de zone a au moins 3 s de recharge interne (sauf configuration explicite de test) ; une réaction mixte s'exécute entièrement pendant la résolution ; pas d'annulation `staleSource` pour les effets autonomes ; pas de condition générale `requiresAbleToAct` (les déclencheurs autorisés réagissent malgré un étourdissement) ; déclencheurs d'équipement reportés au système d'objets.

**Plafonds 8.1** : réduction de garde 75 % ; réduction totale des dégâts subis 85 %, avant bouclier (paramètres de `rules.json` depuis la décision 2 ci-dessous).

**Décisions du 18 octobre 2026** (questions de fin du rapport 8.1) :

1. **Rempart éternel** : garde incassable (`guard.noUpkeepBreak`) avec réduction de 40 % à 0 endurance ; le raccourci de rupture par Charge est **retiré** de ce majeur.
2. Les plafonds de 75 % et 85 % deviennent des **paramètres de `rules.json`** (nouvelle version du schéma de règles).
3. **Blocage à coût nul** : comportement actuel conservé (il relance le délai de régénération).
4. Un `perRank` de **déclencheur** sur un talent à un seul rang est **refusé**, comme pour les modificateurs.

**Décisions du 18 octobre 2026** (interprétations du rapport 8.4, PR #8, toutes gardées par Damien) :

5. **Provocation** : les +5 de Résonance sont gagnés au lancement, même si la provocation est refusée (cible qui n'est pas un assaillant).
6. **Bastion absolu** : les 8 s valent pour ses trois effets ; aucune recharge propre, la Résonance (100) seule le limite ; garde sans entretien pour le Bastion seul, −30 % de dégâts subis pour lui et ses alliés.
7. **Contrecoup** : les Charges sont dépensées au lancement ; un bonus « par Charge détenue » ne compte pas sur son impact.

**Décision du 18 octobre 2026** (sous-lot 8.7, passe d'équilibrage) :

8. **Provocation** : inflige une frappe physique de 0,8 en plus de provoquer (objectif S1 : le Gardien n'avait que deux actives de dégâts ; écart de DPS ramené de 27,2 % à 10,5 %). Options écartées : Onde de rupture à la place de Provocation dans le build du Gardien, ou objectif S1 assoupli à 30 %. Rapport : [RAPPORT_8_7.md](RAPPORT_8_7.md).

## Catalogue du Bastion (valeurs initiales du plan)

Trois arbres : **Rempart** (garde, endurance, parade, Charges, survie), **Contrecoup** (ripostes, dégâts, Résonance), **Gardien** (boucliers, protection, transfert, soutien ; utile aussi en solo).

**Rempart** — Garde stable (3 rangs : réduction de garde +4 %, entretien −0,25 endurance/s par rang) ; Écho défensif (1 rang : un blocage rend 4 endurance ; avec recharge interne de 3 s, donne aussi une Charge) ; **Rempart éternel** (majeur, décision 1 ci-dessus). Intermédiaires : Peau de fer (dégâts subis −1,5 %/rang), Souffle du roc (endurance max +4 et régénération +1/s par rang), Poids du rempart (en garde, réduction additionnelle de 0,5 % par rang et par Charge).

**Contrecoup** — Riposte précise (3 rangs : une parade parfaite frappe l'attaquant, coefficient 0,5 + 0,25/rang, sans critique ni Résonance) ; Fureur contenue (2 rangs : bonus aux dégâts directs par tranche de 10 Résonance détenue, jusqu'à +20 % à 100 au rang 2) ; **Jugement du colosse** (majeur : une parade parfaite déclenche une frappe de zone 1,2, +10 Résonance, recharge interne ≥ 3 s). Intermédiaires : Main lourde (dégâts directs +2 %/rang, réactions comprises), Élan (récupération après parade ratée −50 ms/rang), Écho de guerre (+2 Résonance/rang après une parade parfaite).

**Gardien** — Égide réactive (3 rangs : parade parfaite ou blocage donne un bouclier de 3 % des PV max par rang, 4 s, recharge interne 2 s, renouvellement au plus fort) ; Lien protecteur (1 rang : Barrière d'acier protège aussi un allié lié, 25 % de ses dégâts transférés au Bastion) ; **Serment du gardien** (majeur : provoque l'assaillant qui frappe l'allié lié, 2 s, recharge interne 3 s, transfert porté à 40 %, dégâts transférés passant par la garde du Bastion). Intermédiaires : Vigilance (PV max +2 %/rang), Présence (provocations +0,2 s/rang), Rempart vivant (boucliers posés +4 %/rang).

**Passifs équipables** : Endurance du roc (endurance max +15), Bouclier lourd (coût par blocage −3), Réserve de Charges (Charges max +1), Œil du duelliste (fenêtre de parade +50 ms), Ferveur (Résonance par impact direct +1), Posture de fer (en garde, dégâts subis −5 %), Sentinelle (premier impact sur l'allié lié : +1 Charge, recharge 6 s), Ténacité (résistance aux altérations +100). Martèlement et Vengeance ne sont pas réintroduits.

**Compétences** — Frappe du rempart (1,2 ; 10 endurance ; 6 s ; ennemi unique) ; Heurt de bouclier (0,8 ; 15 ; 10 s ; étourdissement 1,5 s) ; Onde de rupture (0,5 ; 20 ; 12 s ; tous les ennemis ; Fragilité) ; Contrecoup (10 ; 8 s ; consomme toutes les Charges, au moins une ; 0,6 + 0,4 par Charge) ; Provocation (0,8 depuis le sous-lot 8.7, décision 8 ; 10 ; 12 s ; assaillant ; 3 s ; +5 Résonance) ; Barrière d'acier (20 ; 20 s ; bouclier de 15 % des PV max du Bastion, 6 s, sur soi ou un allié). **Ultime Bastion absolu** : 100 Résonance, 8 s, dégâts subis −30 % pour le Bastion et ses alliés, provoque tous les assaillants, garde sans entretien pendant l'effet.

## Builds et scénarios de référence (8.5 à 8.7)

Attributs communs niveau 30 : Force 30, Dextérité 10, Intelligence 6, Vitalité 30, Volonté 15, Endurance 20.

- **Rempart** : 19 Rempart / 8 Gardien / 2 Contrecoup, majeur Rempart éternel ; Frappe du rempart, Heurt de bouclier, Barrière d'acier, Onde de rupture ; Endurance du roc, Bouclier lourd, Réserve de Charges, Posture de fer.
- **Contrecoup** : 19 Contrecoup / 10 Rempart, majeur Jugement du colosse ; Frappe du rempart, Contrecoup, Onde de rupture, Heurt de bouclier ; Œil du duelliste, Ferveur, Endurance du roc, Ténacité.
- **Gardien** : 19 Gardien / 9 Rempart / 1 Contrecoup, majeur Serment du gardien ; Provocation, Barrière d'acier, Frappe du rempart, Heurt de bouclier ; Sentinelle, Posture de fer, Ténacité, Endurance du roc.
- Contrôles : hybride 10/10/9 sans majeur ; niveau 10 (9 points, 4 actives, ultime, 2 passifs).

Scénarios : **S1** mannequin 60 s (DPS brut) ; **S2** duel soutenu 120 s (assaillant toutes les 1,5 s, Fragilité périodique) ; **S3** meute 60 s (3 assaillants, zone et saignement) ; **S4** protection 90 s (Bastion, allié fragile, 2 assaillants) ; **S5** briseur de garde 90 s (attaques imparables régulières). Profils : débutant (aucune parade parfaite), standard (une attaque sur deux), expert (toutes les attaques éligibles).

Objectifs provisoires : S1 écart de DPS ≤ 15 % ; S2 DPS Contrecoup ≥ +25 % sur Rempart ; S2/S5 survie et dégâts évités du Rempart ≥ +25 % sur Contrecoup ; S4 allié protégé ≥ 1,5 fois plus longtemps avec Gardien ; tous scénarios < 50 % de chaque budget moteur. Chaque spécialisation excelle sur sa mesure, aucune ne domine partout ; les écarts sont mesurés et expliqués.

## Après le lot 8

1. **Prototype Unity 3D** (lot 9, ci-dessus) : importer le cœur C# sans le réécrire ; personnage, déplacements, caméra expérimentale (isométrique et troisième personne comparées), garde et parade visibles, compétences, ennemis d'entraînement, barres de ressources, altérations, première interface de talents. Graphismes provisoires.
2. **Serveur autoritaire à deux joueurs** : deux clients, le serveur résout le combat avec le même cœur, commandes invalides refusées, déconnexion/reconnexion, tests de latence, désynchronisation et rejeu. Pas d'infrastructure massive avant cette preuve.
3. **Vertical slice**, devenue la **tranche des Vallons d'Aube** (cadrage du 19 octobre 2026) : une zone, le Bastion jouable, quelques ennemis, une mission, progression, récompense, inventaire, récolte et fabrication (minerais et forge, plantes et remèdes), un deuxième joueur dans une région partagée. Avec les positions réelles en combat (Q13), elle se découpe en quatre lots (11 à 14), ordre validé le 20 octobre 2026 ; voir [PLAN_LOT_11.md](PLAN_LOT_11.md).
4. Ensuite : première alpha (périmètre à définir ; recommandations : trois classes, progression refondue, donjon, groupes, échanges), puis le jeu complet. Cet ordre, validé le 20 octobre 2026 (L11-D1), remplace la liste précédente (progression et équipement, monde, métiers, économie, classes, multijoueur étendu, persistance, interfaces, direction artistique, alpha) ; écart E9 du [cadrage](CADRAGE.md#7-écarts-avec-lexistant).
