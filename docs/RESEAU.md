# Réseau — lot 10 (serveur autoritaire à deux joueurs)

Le lot 10 fait jouer **deux joueurs en coopération** dans le même combat, arbitré par un **serveur autoritaire** qui résout tout avec le même cœur que Unity. Un client n'envoie que des intentions ; il ne peut ni modifier le combat ni tricher sur ses ressources. Décisions de Damien (19 octobre 2026) : coopération (deux Bastions contre des assaillants) ; un joueur déconnecté laisse le combat continuer, son personnage inactif, garde relâchée, avec 30 secondes pour revenir.

Tout le réseau est dans `src/EdS.Net` (netstandard2.1, C# 9, sans dépendance), utilisable par Unity (hôte et client) et par un serveur dédié.

## Principe

Le cœur est déterministe : mêmes commandes, même combat, octet par octet.

1. Le **serveur** reçoit les intentions des joueurs (garde tenue, parade, compétence d'un emplacement, cibles), les range dans le prochain tick, les soumet au moteur (groupes tout ou rien, `CombatEngine.SubmitAll`), simule le tick et diffuse les **commandes admises** avec l'**empreinte** du journal à ce point.
2. Chaque **client** soumet exactement les mêmes commandes à son propre moteur, simule le même tick et compare l'empreinte. Une différence est une désynchronisation : le client demande l'historique et rejoue tout.
3. Une **reconnexion** est une resynchronisation : l'historique complet (en-tête et commandes) suffit à reconstruire l'état exact.

**Depuis le sous-lot 11.7 (protocole 2)** : une resynchronisation, un retour ou une arrivée en plein combat passent par l'**état courant** (instantané du moteur, sous-lot 11.4d) au lieu de l'historique : rien n'est rejoué. Les déplacements et la sélection de cible passent par le serveur. Détail : [protocole 2 et démonstration réseau](#protocole-2-et-démonstration-réseau-sous-lot-117).

## Protocole, version 1 (sous-lot 10.1)

### Messages encadrés

TCP, sans chiffrement (réseau local). Chaque message : longueur sur 4 octets gros-boutiste, puis un objet JSON UTF-8. Une longueur nulle ou au-delà de la limite du sens de réception est une faute de protocole, détectée sur l'en-tête, **avant** de lire ou d'allouer le contenu : la connexion est fermée.

| Sens | Taille maximale | Raison |
|---|---|---|
| client → serveur | 8 Kio | intentions, présentation, build : petits messages ; un client ne peut pas faire allouer de gros tampons au serveur |
| serveur → client | 4 Mio | historique d'un combat (resynchronisation) |

Côté transport : files bornées en messages et en octets (4 096 messages et 64 Mio en attente de lecture, autant en attente d'envoi), délai d'écriture de 10 s ; un pair qui inonde ou ne lit plus est déconnecté. L'écoute range au plus 64 connexions pas encore prises en charge ; son arrêt et l'enregistrement d'une connexion sont exclusifs : une connexion arrivée pendant l'arrêt est fermée, aucune n'est rendue après. Aucune exception ne sort des fils de lecture, d'écriture et d'écoute.

### Lecture stricte

Chaque message passe par l'analyseur strict du sous-lot 9.1 (`MessageCodec`) : type connu et du bon sens, champs connus, uniques et obligatoires, types JSON exacts, entiers sans décimale, nombres dans leurs bornes, identifiants de 1 à 128 caractères parmi `a-z A-Z 0-9 . _ -`, textes libres (noms, raisons) sans caractère de contrôle ni demi-paire de substitution ni espace en tête ou en fin, jetons de 32 chiffres hexadécimaux minuscules, empreintes `fnv1a64:` et 16 chiffres hexadécimaux, listes bornées (256 talents, 256 commandes par tick, 8 places). Un message refusé ferme la connexion. La lecture ne lève jamais d'exception sur des octets reçus.

### Messages

| Type | Sens | Champs | Rôle |
|---|---|---|---|
| `hello` | C → S | `protocol`, `name` (24 car.), `build` { `reference`, `talents`? } | entrée dans la salle, build de référence et rangs de talents éventuels |
| `reconnect` | C → S | `protocol`, `token` | retour d'un joueur déconnecté |
| `build` | C → S | `build` | changement de build en salle d'attente |
| `ready` | C → S | `ready` | prêt ou non |
| `input` | C → S | `guard`, `parry`, `slot` (0 à 4 ou null), `target`, `ally` (ou null) ; protocole 2 : `heading` (0 à 63 ou null) | intentions pour le prochain tick |
| `ping` | C → S | `clientTime` | mesure du temps aller-retour |
| `resyncRequest` | C → S | — | demande de l'historique (protocole 2 : de l'état courant) |
| `bye` | C → S | — | départ volontaire |
| `welcome` | S → C | `protocol`, `seat`, `token`, `tickMs` | place attribuée (identifiant du personnage), jeton de reconnexion |
| `lobby` | S → C | `phase` (`lobby`, `combat`, `ended`), `seats` [{ `id`, `player`, `connected`, `ready`, `build` }] | état de la salle à chaque changement |
| `start` | S → C | `header` | ligne d'en-tête du journal : le client reconstruit le combat avec ses données, après les contrôles d'identité d'un rejeu |
| `tick` | S → C | `tick`, `commands` [{ `atMs`, `actor`, `action`, `skill`, `target` }], `hash` | commandes admises d'un tick et empreinte |
| `resync` | S → C | `header`, `ticks`, `history` [{ `tick`, `commands` }], `hash` | historique complet (ticks strictement croissants, inférieurs à `ticks`) ; **protocole 1 seulement**, remplacé par `state` |
| `rejected` | S → C | `reason` | intention refusée à l'admission (rien n'a été soumis) |
| `pong` | S → C | `clientTime` | réponse au ping |
| `end` | S → C | `tick`, `reason` | fin du combat |
| `error` | S → C | `reason` | refus définitif ; la connexion est ensuite fermée |

Exemple : `{"type":"input","guard":true,"parry":false,"slot":2,"target":"loup_a","ally":null}`.

## Serveur (sous-lot 10.2)

`CombatRoom` est une salle de combat autoritaire, animée par des appels à `Advance(secondes)` : aucun fil ni horloge dans la salle, ce qui la rend testable en temps virtuel. `RoomHost` l'anime en temps réel sur un fil, avec une écoute TCP ; c'est ce qu'utilisent le serveur dédié et l'hôte Unity.

- **Places** : une par personnage du scénario, dans l'ordre (`scenarios/coop/duo_meute.json` : `bastion` et `bastion2` contre deux loups et un hurleur). Un `hello` prend la première place libre ; la salle pleine, un autre protocole que le sien (1 au lot 10, 2 depuis le sous-lot 11.7) ou un build invalide sont refusés (`error`, puis fermeture). Le build demandé (référence et rangs de talents) est résolu et jugé par `BuildValidator` (`BuildChoices`) ; un build composé s'affiche « Personnalisé (Rempart) ».
- **Salle d'attente** : `build` change de build (prêt remis à faux), `ready` se déclare prêt. Le combat commence quand toutes les places sont prises, connectées et prêtes ; le serveur envoie à chacun l'en-tête du journal (`start`). On ne rejoint pas un combat commencé.
- **Combat** : à chaque tick (50 ms), pour chaque place dans l'ordre, les intentions deviennent des groupes de commandes (`IntentionPlanner`, partagé avec le pont temps réel du 9.2 : quitter la garde et lancer forment un groupe) soumis tout ou rien ; un groupe refusé à l'admission vaut un `rejected` au joueur et rien n'est soumis. Une compétence visiblement indisponible (recharge, ressources, contrôle, aucune cible : `IntentionPlanner.Unavailable`, le même contrôle que le pont temps réel) n'est pas planifiée : refus motivé, et la garde n'est pas quittée, comme en solo. Puis le moteur simule le tick et chaque joueur reçoit `tick` (commandes admises, empreinte FNV-1a sur 64 bits du journal à ce point, `HashingCombatLog`). Cibles : celle choisie par le joueur si c'est un ennemi vivant (un allié vivant pour un soutien), sinon le premier ennemi vivant (l'autre joueur pour un soutien, soi-même à défaut).
- **Déconnexion** (décision du 19/10/2026) : le combat continue ; la place reste réservée, le personnage est inactif (garde relâchée au tick suivant). Le joueur a 30 s pour revenir avec son jeton (`reconnect`) : il reçoit `welcome` puis l'historique complet (`resync`). Au-delà, la place est abandonnée. Un départ volontaire (`bye`) abandonne la place tout de suite. Si plus personne n'est présent ni attendu, le combat s'arrête.
- **Protections** : présentation dans les 5 s, 60 messages par seconde (rafale de 120), un historique par seconde au plus, 16 connexions au plus, jetons de 128 bits comparés en temps constant. Les délais (présentation, retour en 30 s) sont appliqués au nouvel instant avant de lire les messages en attente : un message lu après l'échéance est refusé. Avant de commencer, la salle calcule un majorant de la taille de l'historique (plafond de commandes des règles, identifiants les plus longs) ; s'il dépasse les 4 Mio d'un message du serveur, le combat est refusé avec la raison plutôt que de couper un joueur à son retour (avec les règles livrées, il en est loin).
- **Fin** : `end` (dernier tick simulé, raison), retour en salle d'attente (`ended`) ; une nouvelle partie commence quand tous sont de nouveau prêts. Le journal rejouable du combat (scénario complété des commandes admises, simulé de nouveau) est disponible (`LastJournal`, `CombatEnded`) ; ses lignes de combat sont exactement celles que les clients ont vérifiées. Le serveur dédié l'écrit avec `JournalFolder` (`combat-001.jsonl`, `combat-002.jsonl`…) : jamais d'écrasement, un serveur relancé sur le même dossier continue la numérotation, une erreur d'écriture est signalée sans arrêter la salle.
- **Arrêt de l'hôte** : `RoomHost.Dispose` arrête la boucle et l'écoute, puis ferme les connexions des joueurs avec la raison (`error` « serveur arrêté. ») ; aucune connexion ni fil de lecture ne reste ouvert.

Serveur dédié (outil .NET, hors Unity) :

```bash
dotnet run --project src/EdS.Server -- --local                      # ce poste seulement, port 7777
dotnet run --project src/EdS.Server -- --port 7777 --journaux combats   # réseau local, journaux rejouables (eds replay)
dotnet run --project src/EdS.Server -- --local --robot              # avec un équipier robot (sous-lot 10.4)
```

## Client (sous-lot 10.3)

`NetworkClient` est le client du serveur, animé comme la salle par `Advance(secondes)` (Unity l'appelle à chaque image ; les tests en temps virtuel). Il ne fait aucune prédiction : le serveur est seul juge, le client rejoue ses décisions.

- **Salle d'attente** : `hello` à la construction (nom, build) ; `Status` passe de `Connecting` à `Lobby` à l'accueil (`SeatId`, `Token`, `TickMs`). `SetBuild`, `SetReady`, `Leave`. Les messages du joueur sont vérifiés avant l'envoi par le même lecteur strict que le serveur : une erreur de l'appelant est une exception, pas une déconnexion. `SetBuild` envoie une demande à la fois (`PendingBuild`), suivie d'une mesure du temps aller-retour : le serveur répond au build (salle mise à jour ou refus) avant de répondre à la mesure, si bien qu'à cette réponse le build entre dans `AcceptedBuild` sauf si un refus est arrivé entre-temps (par exemple une demande partie en salle d'attente mais reçue par le serveur après le début du combat). Unity repart toujours de `AcceptedBuild` (revue Codex de la PR #21). Une demande encore sans réponse à une coupure est oubliée : un combat commencé avant cette réponse l'a forcément refusée (un build accepté remet « prêt » à faux), et une mesure envoyée après le retour ne doit pas la valider.
- **Combat** (`Combat`, `NetworkCombat`) : lu dans l'en-tête de journal reçu (`start`, `CombatLogReplayer.ReadSetup`), le moteur local rejoue chaque `tick` du serveur (commandes admises soumises tout ou rien, puis le tick) et compare l'empreinte de son journal (`HashingCombatLog`) à celle du serveur. L'affichage montre le dernier tick reçu ; la durée du combat est celle du scénario (60 s pour le duo, `--duration` pour une autre).
- **Divergence** : empreinte différente, tick manquant ou commande du serveur refusée par le moteur local (aucune exception ne remonte) : le client demande l'historique (`resyncRequest`), ignore les ticks reçus entre-temps (ils sont dans l'historique) et reconstruit le combat depuis son début. L'empreinte est vérifiée avant de publier quoi que ce soit du tick : un tick refusé ne produit ni retour ni changement de cible ou d'allié, et tant que l'historique est attendu, compétences et choix de cible sont ignorés (l'état affiché vient d'un tick refusé). Un historique qui ne retrouve pas l'empreinte est redemandé (une fois par seconde, ce que le serveur accepte) ; après trois historiques sans accord, le client abandonne (« désynchronisation persistante »). `Desyncs`, `Resyncs`, `LastDesync` le comptent pour les mesures du 10.5. La **fin** annoncée par le serveur n'est acceptée telle quelle que si le combat local concorde (aucun historique en attente, moteur local terminé au même tick) ; sinon, comme le serveur ne répond plus aux demandes d'historique après la fin, le client le dit (`EndProblem`, `Error` « fin du combat non vérifiée ») et ne montre pas de résultat local (`Result` nul) ; il reste dans la salle.
- **Intentions** : `NetworkCombat` a la même interface que le combat solo (`ICombatClient`, dans `EdS.Runtime`) : `SetGuard`, `Parry`, `UseSlot`, `NextTarget`, `SelectTarget`, plus `NextAlly` / `SelectAlly` (soutien sur l'autre joueur). Un emplacement vide ou visiblement indisponible donne un retour sans rien envoyer. Les changements sont regroupés : un message `input` toutes les 25 ms au plus (garde et cibles à jour, parade et compétence gardées jusqu'à l'envoi), bien sous la limite de 60 messages par seconde du serveur.
- **Affichage partagé** : `CombatDisplay` (état du joueur, autres combattants) et `FeedbackTracker` (retours tirés des compteurs du moteur) sont communs au solo et au réseau ; les refus du serveur (`rejected`) s'affichent comme des retours.
- **Temps aller-retour** : un `ping` par seconde ; `RttMs` (dernier) et `SmoothedRttMs` (moyenne mobile, 1/8 par mesure).
- **Coupure** pendant un combat : `Status` = `Disconnected`, jeton gardé, `DisconnectedSeconds` et `CanReconnect` pour l'affichage. `Reconnect(connexion)` reprend la même place : `welcome`, puis l'historique complet ; le combat reprend au tick du serveur (la garde a été relâchée pendant l'absence). `Reconnect` n'est accepté que depuis `Disconnected` : une seconde demande par-dessus une demande en cours serait refusée par le serveur (jeton encore attaché à l'ancienne connexion) ; une tentative dont la connexion se ferme ramène à `Disconnected`, d'où l'on recommence. Hors combat, une coupure arrête le client (`Failed`), comme un refus du serveur (`error`) ou un message illisible.

## Jeu en réseau dans Unity (sous-lot 10.4)

- **`NetworkSession`** (`EdS.Net`) : une partie vue du jeu, sans détail de transport. `HostGame` crée la salle (`CombatRoom`), l'écoute TCP (port 7777, tout le réseau local) et son hôte temps réel (`RoomHost`) dans le processus du jeu, puis la rejoint par le poste lui-même ; `Join` rejoint une adresse. Les connexions s'ouvrent sans bloquer l'image (délai de 10 s) ; une coupure pendant un combat fait revenir automatiquement avec le jeton, une tentative toutes les 2 s tant que le serveur garde la place (30 s). Les échecs se lisent dans `Error`, sans exception.
- **`BotPlayer`** : équipier robot pour essayer la coopération seul (hôte Unity « avec un équipier robot », serveur dédié `--robot`). C'est un client ordinaire (mêmes messages, mêmes limites) : prêt dans la salle d'attente, garde levée quand un coup arrive (0,8 s), parade juste avant un coup parable (0,1 s affiché, 0,15 s avant le sous-lot 10.6), compétences dès qu'elles sont prêtes, soutiens sur l'autre joueur. Déterministe, sans hasard ; ce n'est pas une intelligence de jeu.
- **Unity** (`EdsGame`, `NetworkScreen`) : touche M (menu : nom, héberger, héberger avec un robot, rejoindre « hôte » ou « hôte:port », revenir au solo) ; salle d'attente (places, builds, prêts, adresse à donner, F1 à F5 et talents envoyés au serveur, erreur de la session ou du robot) ; combat affiché par le même écran que le solo (`ICombatClient`), l'autre Bastion en allié (capsule verte, cercle vert sur l'allié visé, G pour changer) ; temps aller-retour sous le chrono ; bandeau de coupure et de retour (la garde tenue avant la coupure n'est pas renvoyée au retour : la touche est relue). Pas de pause en réseau. Noms, builds et raisons venus du réseau sont affichés sans texte enrichi (leurs chevrons ne forment pas de balise). Le jeu construit (`unity/Build/`) retrouve les données du dépôt dans ses dossiers parents. Guide : [UNITY.md](UNITY.md#6-jouer-à-deux-en-réseau-lot-10).

## Mesures (sous-lot 10.5)

`eds reseau` (`EdS.Tools.Network`) fait jouer la coopération à deux joueurs scriptés sur le réseau simulé, de 0 à 200 ms de latence d'un sens, avec gigue et coupures, et écrit un rapport Markdown reproductible. Résultats et options pour la parade : [RAPPORT_10_5.md](RAPPORT_10_5.md). En bref : aucune divergence ; retour après coupure en 130 ms ; la parade « à vue » ne tient que jusqu'à 25 ms d'un sens, la parade anticipée du ping tient jusqu'à 200 ms (sauf gigue forte). Damien a choisi l'option B (décision du 19 octobre 2026 à 11:28), faite au sous-lot 10.6 (ci-dessous).

## Télégraphie avancée (sous-lot 10.6, option B de la parade)

L'affichage a environ une latence de retard sur le serveur, et une parade met une latence à l'atteindre : une parade pressée « à vue » arrive donc un aller-retour trop tard (parade perdue dès 50 ms d'un sens, rapport du 10.5). Le client avance la **télégraphie** des assaillants (`OtherState.NextAttackSeconds`, barre au-dessus de l'assaillant et « Prochain coup dans … » de la fiche de la cible) du **temps aller-retour lissé** (`NetworkCombat.TelegraphLeadSeconds`, jamais moins de 0) : le joueur qui pare au moment appris en solo sur cet affichage atteint le serveur au bon tick.

- Réglages (`NetworkClientOptions`) : `TelegraphAdvance` (vrai par défaut) ; `MaxTelegraphLeadSeconds` (1 s par défaut, de 0 à 10 s ; une valeur hors bornes est refusée). Avant la première mesure du temps aller-retour, l'avance est nulle ; au retour après une coupure, la mesure repart de zéro (la nouvelle connexion n'a pas forcément la latence de l'ancienne).
- **Compte à rebours continu** (revue Codex de la PR #23) : la télégraphie s'écoule aussi pendant le tick en cours (`TickProgress`), en solo comme en réseau ; la barre au-dessus des assaillants avance donc en continu au lieu de sauter de 50 ms en 50 ms. En réseau, l'écoulement est plafonné à un tick si le suivant tarde.
- **Affichage seulement** : `CombatDisplay.Others(engine, joueur, avance, écoulé)` ; le moteur local, les empreintes, le protocole (version 1), le serveur et les règles sont inchangés. En solo, l'avance vaut 0.
- L'équipier robot (`BotPlayer`) pare sur le même affichage : il en profite aussi. Son seuil de parade passe de 0,15 à 0,1 s (le moment qui réussit en solo) : avec 0,15 s, il pressait trop tôt et ne réussissait qu'à la faveur du retard du réseau. Réseau simulé, 100 ms d'un sens : environ 37 parades parfaites pour 13 ratées avec l'avance, aucune parfaite pour 44 ratées sans.
- Unity : le bandeau du combat en réseau affiche l'avance (« télégraphie avancée de … ms ») à côté du temps aller-retour.
- Mesures : `eds reseau` mesure la parade « anticipée » avec cette télégraphie (le joueur scripté pare à vue, le client avance l'affichage) et la parade « à vue » sans elle ([RAPPORT_10_5.md](RAPPORT_10_5.md)).

## Protocole 2 et démonstration réseau (sous-lot 11.7)

Objet : [PLAN_LOT_11.md](PLAN_LOT_11.md), 11.7 (L11-D8). Le protocole passe à la **version 2** ; un client ou un serveur du protocole 1 est refusé à la présentation (« protocole 1 non pris en charge »).

| Changement | Détail |
|---|---|
| `input.heading` | cap de marche voulu, de 0 à 63 (un des 64 caps de `MoveInput`, sous-lot 11.6), ou null : arrêt. Champ obligatoire. |
| `tick.commands` | toutes les actions du moteur : `move` (avec `direction` [x, z], composantes dans [−1, 1] non toutes deux nulles), `stop`, `select` (avec `target`, ou null : sélection vidée), `leave`, en plus de la garde, de la parade et des compétences. |
| `state` (remplace `resync`) | `header` (en-tête du combat **complété** jusqu'à ce tick : arrivées comprises), `snapshot` (instantané du moteur, lignes JSON canoniques), `hash` (empreinte du journal du serveur à ce point). |

- **Marche et sélection par le serveur** : la salle tient, pour chaque place, la marche voulue (`MovementIntent`, partagé avec le pont solo) ; `move`, `stop` et `select` ne partent au moteur que quand la marche ou la sélection du moteur diffère de celle voulue (revue Codex de la PR #34 : comparée à l'état du moteur, pas au dernier envoi), dans la place laissée par le plafond de commandes du tick. Un joueur seul et un joueur en réseau produisent exactement les mêmes commandes. La cible d'un joueur est celle qu'il a choisie si elle est présente et vivante ; sinon le premier ennemi présent, vivant et en vue de lui (règle de ligne de vue du moteur) ; sinon aucune (sélection vidée).
- **État courant au lieu de l'historique** : à une divergence, un retour après une coupure ou une arrivée, le serveur envoie `state`. Le client reconstruit le combat complété, reprend le moteur à l'instantané (`CombatEngine.Restore`, contrôlé par le moteur) et repart de l'empreinte reçue (`HashingCombatLog` amorcé par elle) ; les retours affichés repartent de là : **aucun événement passé n'est rejoué ni réattribué**. L'empreinte reçue ne se vérifie qu'au tick suivant ; après trois demandes sans tick accepté, le client abandonne (« désynchronisation persistante après 3 demandes d'état courant »). L'en-tête de début reste celui du client (l'empreinte finale se compare comme avant).
- **Arrivée pendant l'affrontement** (`RoomOptions.JoinDuringCombat`) : le combat commence dès que les joueurs présents sont prêts ; les places libres restent hors du combat de départ. Un joueur qui arrive pendant le combat entre au tick courant (`CombatEngine.Join`) à la place de son personnage dans le scénario si elle est libre (aucune emprise présente et vivante, ni arrivée déjà en file à ce tick : revue Codex de la PR #35), sinon à la plus proche place libre essayée (anneaux de 1,5 m, 3 m et 4,5 m, huit directions, dans l'aire et hors du décor). Tout le monde reçoit alors l'état courant, qui contient le nouveau venu. Le majorant de taille d'un état compte les personnages des places libres. Revue Codex de la PR #35 (3e tour) : à l'arrivée, il est revérifié avec le build choisi par l'arrivant, avant de toucher au moteur ; un build qui le ferait dépasser est refusé (« entrée dans le combat refusée : avec ce build… »). 4e tour : le majorant compte aussi l'instantané courant (au début, celui de départ), et l'état réel qui suivrait l'arrivée est mesuré sur une copie du moteur avant de toucher au vrai ; la croissance de l'instantané en cours de combat (file d'événements) reste contrôlée à l'envoi, qui ferme la connexion au-delà de la limite.
- **Prédiction du déplacement local** (`MovementPrediction`, affichage seulement) : quand le serveur applique déjà la marche voulue, le personnage est montré à la place du dernier tick avancée du temps écoulé dans le tick et du temps aller-retour lissé (ce dernier compté au plus 0,5 s) ; quand la saisie vient de changer, la place montrée part de la dernière et suit la saisie aussitôt (à l'arrêt, elle ne bouge plus) ; si le serveur ne suit pas en 1 s, la prédiction est abandonnée. Elle s'arrête au bord de l'aire et avant le décor ; Unity la rejoint en douceur.
- **Unity** : deux entrées du menu réseau, « Héberger les Vallons à deux » (`scenarios/coop/vallons_duo.json`, l'autre joueur peut arriver en plein combat) et « Héberger les Vallons, robot qui arrive après 10 s de combat » (essai seul d'une arrivée ; le délai part du début du combat, `NetworkSession.BotArrivesIn`). Quand un état courant ajoute un combattant (`NetworkCombat.RosterVersion`), l'arène est repeuplée : l'arrivant a son corps chez l'autre joueur. Mode d'emploi : [UNITY.md](UNITY.md).
- **Esquive selon la latence** (`eds reseau --esquive`, scénario `coop/esquive_duo`) : deux géants écrasent tour à tour la place de leur cible (cercle de 2 m annoncé 900 ms avant) ; le joueur scripté sort du cercle dès qu'il voit l'annonce (après 0 ou 250 ms de réaction), le serveur juge l'impact. Résultats ci-dessous.

### Esquive mesurée (`eds reseau --esquive`, 60 s par ligne, 39 impacts)

| Latence d'un sens | Réaction 0 ms | Réaction 250 ms |
|---:|---:|---:|
| 0 ms | 100 % | 100 % |
| 50 ms | 100 % | 100 % |
| 100 ms | 100 % | 28 % |
| 150 ms | 97 % | 26 % |
| 200 ms | 33 % | 18 % |

Lecture : sortir d'un cercle de 2 m demande 0,5 s de marche ; sur les 900 ms de l'annonce, il reste environ 400 ms pour la réaction, l'aller-retour et le tick. Un joueur parfait esquive jusqu'à 150 ms d'un sens ; avec une réaction humaine de 250 ms, jusqu'à 50 ms (le réseau local et une bonne connexion). Au-delà, il faut des annonces plus longues ou des zones plus petites : c'est une donnée de réglage des attaques (`telegraphMs`, rayon), à trancher avec l'essai (11.9). Les empreintes des deux clients sont identiques à celles du serveur dans toutes les mesures, sans divergence.

## Réseau simulé

`SimulatedNetwork` remplace TCP dans les tests et les mesures : temps virtuel avancé par l'appelant, latence d'un sens et gigue par lien (tirage `Pcg32` : même graine, mêmes délais), ordre conservé dans chaque sens comme en TCP, fermeture vue après les messages déjà partis, coupure brutale (`Cut`) qui perd les messages en route, écoute arrêtée qui coupe les connexions pas encore acceptées et libère son adresse, 64 connexions au plus en attente d'acceptation, mêmes limites que TCP (taille des messages ; 4 096 messages et 64 Mio non lus au plus ; 4 096 messages et 64 Mio en route par émetteur, au-delà l'émetteur est fermé « file d'envoi pleine »). Aucun fil, aucune horloge réelle : les tests de réseau sont reproductibles.

## Limites

Voir [LIMITES_LOT_10.md](LIMITES_LOT_10.md).
