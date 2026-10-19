# Réseau — lot 10 (serveur autoritaire à deux joueurs)

Le lot 10 fait jouer **deux joueurs en coopération** dans le même combat, arbitré par un **serveur autoritaire** qui résout tout avec le même cœur que Unity. Un client n'envoie que des intentions ; il ne peut ni modifier le combat ni tricher sur ses ressources. Décisions de Damien (19 octobre 2026) : coopération (deux Bastions contre des assaillants) ; un joueur déconnecté laisse le combat continuer, son personnage inactif, garde relâchée, avec 30 secondes pour revenir.

Tout le réseau est dans `src/EdS.Net` (netstandard2.1, C# 9, sans dépendance), utilisable par Unity (hôte et client) et par un serveur dédié.

## Principe

Le cœur est déterministe : mêmes commandes, même combat, octet par octet.

1. Le **serveur** reçoit les intentions des joueurs (garde tenue, parade, compétence d'un emplacement, cibles), les range dans le prochain tick, les soumet au moteur (groupes tout ou rien, `CombatEngine.SubmitAll`), simule le tick et diffuse les **commandes admises** avec l'**empreinte** du journal à ce point.
2. Chaque **client** soumet exactement les mêmes commandes à son propre moteur, simule le même tick et compare l'empreinte. Une différence est une désynchronisation : le client demande l'historique et rejoue tout.
3. Une **reconnexion** est une resynchronisation : l'historique complet (en-tête et commandes) suffit à reconstruire l'état exact.

## Protocole, version 1 (sous-lot 10.1)

### Messages encadrés

TCP, sans chiffrement (réseau local). Chaque message : longueur sur 4 octets gros-boutiste, puis un objet JSON UTF-8. Une longueur nulle ou au-delà de la limite du sens de réception est une faute de protocole, détectée sur l'en-tête, **avant** de lire ou d'allouer le contenu : la connexion est fermée.

| Sens | Taille maximale | Raison |
|---|---|---|
| client → serveur | 8 Kio | intentions, présentation, build : petits messages ; un client ne peut pas faire allouer de gros tampons au serveur |
| serveur → client | 4 Mio | historique d'un combat (resynchronisation) |

Côté transport : files bornées (4 096 messages en attente de lecture, 4 096 en attente d'envoi), délai d'écriture de 10 s ; un pair qui inonde ou ne lit plus est déconnecté. Aucune exception ne sort des fils de lecture et d'écriture.

### Lecture stricte

Chaque message passe par l'analyseur strict du sous-lot 9.1 (`MessageCodec`) : type connu et du bon sens, champs connus, uniques et obligatoires, types JSON exacts, entiers sans décimale, nombres dans leurs bornes, identifiants de 1 à 128 caractères parmi `a-z A-Z 0-9 . _ -`, textes libres (noms, raisons) sans caractère de contrôle ni demi-paire de substitution ni espace en tête ou en fin, jetons de 32 chiffres hexadécimaux minuscules, empreintes `fnv1a64:` et 16 chiffres hexadécimaux, listes bornées (256 talents, 256 commandes par tick, 8 places). Un message refusé ferme la connexion. La lecture ne lève jamais d'exception sur des octets reçus.

### Messages

| Type | Sens | Champs | Rôle |
|---|---|---|---|
| `hello` | C → S | `protocol`, `name` (24 car.), `build` { `reference`, `talents`? } | entrée dans la salle, build de référence et rangs de talents éventuels |
| `reconnect` | C → S | `protocol`, `token` | retour d'un joueur déconnecté |
| `build` | C → S | `build` | changement de build en salle d'attente |
| `ready` | C → S | `ready` | prêt ou non |
| `input` | C → S | `guard`, `parry`, `slot` (0 à 4 ou null), `target`, `ally` (ou null) | intentions pour le prochain tick |
| `ping` | C → S | `clientTime` | mesure du temps aller-retour |
| `resyncRequest` | C → S | — | demande de l'historique |
| `bye` | C → S | — | départ volontaire |
| `welcome` | S → C | `protocol`, `seat`, `token`, `tickMs` | place attribuée (identifiant du personnage), jeton de reconnexion |
| `lobby` | S → C | `phase` (`lobby`, `combat`, `ended`), `seats` [{ `id`, `player`, `connected`, `ready`, `build` }] | état de la salle à chaque changement |
| `start` | S → C | `header` | ligne d'en-tête du journal : le client reconstruit le combat avec ses données, après les contrôles d'identité d'un rejeu |
| `tick` | S → C | `tick`, `commands` [{ `atMs`, `actor`, `action`, `skill`, `target` }], `hash` | commandes admises d'un tick et empreinte |
| `resync` | S → C | `header`, `ticks`, `history` [{ `tick`, `commands` }], `hash` | historique complet (ticks strictement croissants, inférieurs à `ticks`) |
| `rejected` | S → C | `reason` | intention refusée à l'admission (rien n'a été soumis) |
| `pong` | S → C | `clientTime` | réponse au ping |
| `end` | S → C | `tick`, `reason` | fin du combat |
| `error` | S → C | `reason` | refus définitif ; la connexion est ensuite fermée |

Exemple : `{"type":"input","guard":true,"parry":false,"slot":2,"target":"loup_a","ally":null}`.

## Serveur (sous-lot 10.2)

`CombatRoom` est une salle de combat autoritaire, animée par des appels à `Advance(secondes)` : aucun fil ni horloge dans la salle, ce qui la rend testable en temps virtuel. `RoomHost` l'anime en temps réel sur un fil, avec une écoute TCP ; c'est ce qu'utilisent le serveur dédié et l'hôte Unity.

- **Places** : une par personnage du scénario, dans l'ordre (`scenarios/coop/duo_meute.json` : `bastion` et `bastion2` contre deux loups et un hurleur). Un `hello` prend la première place libre ; la salle pleine, un protocole autre que 1 ou un build invalide sont refusés (`error`, puis fermeture). Le build demandé (référence et rangs de talents) est résolu et jugé par `BuildValidator` (`BuildChoices`) ; un build composé s'affiche « Personnalisé (Rempart) ».
- **Salle d'attente** : `build` change de build (prêt remis à faux), `ready` se déclare prêt. Le combat commence quand toutes les places sont prises, connectées et prêtes ; le serveur envoie à chacun l'en-tête du journal (`start`). On ne rejoint pas un combat commencé.
- **Combat** : à chaque tick (50 ms), pour chaque place dans l'ordre, les intentions deviennent des groupes de commandes (`IntentionPlanner`, partagé avec le pont temps réel du 9.2 : quitter la garde et lancer forment un groupe) soumis tout ou rien ; un groupe refusé à l'admission vaut un `rejected` au joueur et rien n'est soumis. Puis le moteur simule le tick et chaque joueur reçoit `tick` (commandes admises, empreinte FNV-1a sur 64 bits du journal à ce point, `HashingCombatLog`). Cibles : celle choisie par le joueur si c'est un ennemi vivant (un allié vivant pour un soutien), sinon le premier ennemi vivant (l'autre joueur pour un soutien, soi-même à défaut).
- **Déconnexion** (décision du 19/10/2026) : le combat continue ; la place reste réservée, le personnage est inactif (garde relâchée au tick suivant). Le joueur a 30 s pour revenir avec son jeton (`reconnect`) : il reçoit `welcome` puis l'historique complet (`resync`). Au-delà, la place est abandonnée. Un départ volontaire (`bye`) abandonne la place tout de suite. Si plus personne n'est présent ni attendu, le combat s'arrête.
- **Protections** : présentation dans les 5 s, 60 messages par seconde (rafale de 120), un historique par seconde au plus, 16 connexions au plus, jetons de 128 bits comparés en temps constant.
- **Fin** : `end` (dernier tick simulé, raison), retour en salle d'attente (`ended`) ; une nouvelle partie commence quand tous sont de nouveau prêts. Le journal rejouable du combat (scénario complété des commandes admises, simulé de nouveau) est disponible (`LastJournal`, `CombatEnded`) ; ses lignes de combat sont exactement celles que les clients ont vérifiées.

Serveur dédié (outil .NET, hors Unity) :

```bash
dotnet run --project src/EdS.Server -- --local                      # ce poste seulement, port 7777
dotnet run --project src/EdS.Server -- --port 7777 --journaux combats   # réseau local, journaux rejouables (eds replay)
```

## Réseau simulé

`SimulatedNetwork` remplace TCP dans les tests et les mesures : temps virtuel avancé par l'appelant, latence d'un sens et gigue par lien (tirage `Pcg32` : même graine, mêmes délais), ordre conservé dans chaque sens comme en TCP, fermeture vue après les messages déjà partis, coupure brutale (`Cut`) qui perd les messages en route, mêmes limites de taille. Aucun fil, aucune horloge réelle : les tests de réseau sont reproductibles.

## Limites

Voir [LIMITES_LOT_10.md](LIMITES_LOT_10.md).
