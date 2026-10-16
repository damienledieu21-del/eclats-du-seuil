# Moteur de simulation (lots 5 et 6, correctif 6.1)

Moteur de combat déterministe, sans Unity, sans réseau et sans horloge système. Code : `src/EdS.Simulation/Engine`. Toutes les valeurs sont **provisoires**.

## Temps et file d'événements

- Horloge logique en ticks (`rules.json` → `combat.tickMs`, 50 ms). Les durées en ms sont converties par `TickRate` ; elles doivent être des multiples du tick (sinon le fichier est refusé).
- Un combat de `durationMs` dure `durationMs / tickMs` ticks, numérotés à partir de 0. Une commande à `atMs` s'exécute au tick `atMs / tickMs`.
- `EventQueue<T>` : tas binaire ordonné par (tick, phase, numéro d'insertion). Deux événements identiques en tick et en phase sortent toujours dans leur ordre d'insertion.

## Ordre intra-tick (A10)

| Phase | Contenu |
|---|---|
| `setup` | tick 0 seulement : une ligne `combatant` par combattant (statistiques calculées) |
| 1. `expirations` | fins de recharge (`cooldownReady`), fins de fenêtre de parade (`parryEnded`), fins d'état (`stateExpired` : Fragilité, rupture de garde) |
| 2. `commands` | commandes du tick et tours des assaillants, triés par initiative décroissante, puis identifiant de l'acteur (ordinal), puis ordre dans le scénario (le tour d'un assaillant avant ses commandes) ; validation puis application (`skillCast`, `guardStarted`, `guardStopped`, `parryStarted`, `attackStarted` ou `commandRejected`) |
| 3. `resolution` | impacts des actions lancées pendant ce tick, dans l'ordre de la phase 2 ; pour chaque cible : défense (parade, garde), dégâts, récompenses, états appliqués |
| 4. `periodic` | entretien de la garde (`guardUpkeep`, ou rupture `upkeep`) |
| 5. `regeneration` | régénération d'endurance (`enduranceRegenerated`), sauf pendant la garde ou la rupture |
| 6. `endOfTick` | morts (`died`, puis `stateRemoved` pour chaque état du mort), fin de combat, arrêt sur limite |

Conséquences testées (`TimingTests`, `OrderingTests`) :

- un temps de recharge qui se termine au tick T est disponible pour une commande du même tick T (l'expiration passe avant les commandes) ;
- toutes les commandes d'un tick sont validées avant toute résolution : un combattant tué au tick T peut encore agir au tick T ; il meurt en fin de tick ;
- l'ordre des lignes ne dépend que du scénario, jamais de l'ordre d'insertion en mémoire ni d'un dictionnaire.

### Actions simultanées et morts en fin de tick

- Toutes les commandes d'un tick sont validées et appliquées (phase 2) avant le premier impact (phase 3). Une garde ou une parade commandée au tick T protège donc contre un impact du même tick T, quelle que soit l'initiative de l'attaquant.
- Les impacts se résolvent dans l'ordre de la phase 2 (initiative, puis identifiant). Un état appliqué par un impact (Fragilité) vaut pour les impacts suivants du même tick, pas pour celui qui l'applique (`FragilityTests`).
- Les PV peuvent tomber à 0 pendant la phase 3 sans que le combattant meure : il reste ciblable et ses actions déjà lancées se résolvent. La mort est constatée en phase 6, dans l'ordre des identifiants ; la garde et la fenêtre de parade du mort s'arrêtent alors sans ligne supplémentaire, et ses états actifs sont retirés (`stateRemoved`, raison `death`, ordre ordinal des états) : leurs échéances deviennent périmées.
- Si toutes les équipes sauf une (ou toutes) sont mortes en phase 6, le combat se termine sur ce tick (`teamDefeated`, vainqueur ou `null`). Mannequins et assaillants ne meurent jamais.

## Commandes et refus atomiques

Une commande (`atMs`, `actor`, `skill`, `target` facultative) est d'abord entièrement validée ; l'état n'est modifié qu'ensuite. Une commande refusée ne consomme ni endurance, ni recharge, ni GCD, ni tirage aléatoire. Premier motif rencontré, dans cet ordre :

- compétence : `actorDead`, `actorCannotAct` (mannequin, assaillant), `skillNotEquipped`, `skillNotImplemented` (compétence sans bloc `combat`), `guardActive` (pas de compétence pendant la garde), `globalCooldown`, `cooldown`, puis ciblage : `targetRequired`, `targetNotEnemy`, `targetDead` (cible unique) ou `targetNotAllowed` (zone), puis `insufficientEndurance`, `insufficientResonance` (ultime), `insufficientRampartCharges` ;
- `guardStart` : `actorDead`, `actorCannotAct`, `guardBroken`, `guardAlreadyActive` ;
- `guardStop` : `actorDead`, `actorCannotAct`, `guardNotActive` ;
- `parry` : `actorDead`, `actorCannotAct`, `guardBroken`, `parryActive` (fenêtre déjà ouverte), `parryRecovery`.

Chaque refus est journalisé (`commandRejected` avec `action`, `skill`, `target`, `command`, `reason`). Garde et parade n'ont ni coût d'activation ni GCD.

## Rythme (A6)

Compétences instantanées. GCD de `combat.globalCooldownMs` (1000 ms) déclenché par chaque lancer ; recharge individuelle démarrant au lancer. Lancer au tick T avec une recharge de D ms : disponible à T + D / tickMs. Pas d'animation ni de temps d'incantation.

## Ciblage (A7)

Logique uniquement : pas de position, de distance ni de collision. `enemy` = une cible ennemie vivante désignée ; `allEnemies` = tous les ennemis vivants, dans l'ordre ordinal de leurs identifiants (et non dans l'ordre du scénario, comme l'indiquait à tort la version précédente de ce document). L'énumération `SkillTargeting` est le point d'extension pour un ciblage spatial futur.

## Endurance (A3)

Coût payé au lancer, maximum respecté. Les coûts de garde (entretien, blocage) sont des dépenses comme les autres. Régénération de `combat.endurance.regenPerSecond` (10/s) après `regenDelayMs` (1000 ms) sans dépense. Elle est calculée sur le total depuis la dernière dépense, pour éviter toute dérive d'arrondi :

```
ticksRégén = t − dernièreDépense − délaiTicks + 1   (si > 0)
gagné(t)   = min(max − enduranceAprèsDépense, ⌊régénParSeconde × ticksRégén × tickMs / 1000⌋)
```

Avec 50 ms : dépense au tick 20, rien jusqu'au tick 39, +0,5 cumulé au tick 40 (arrondi à 0), premier point au tick 41, puis +1 tous les 2 ticks. Pleine endurance : le compteur s'arrête.

Suspension (A1) : aucune régénération pendant la garde ni pendant la rupture. À la fin de l'une ou de l'autre (tick E), la régénération reprend dès qu'une seconde complète s'est écoulée depuis la dernière dépense, mais les ticks suspendus ne rapportent rien : le compteur repart de max(dernière dépense + délai, E). Exemples testés : dépense au tick 0, garde du tick 0 au tick 100, premier point au tick 101 ; blocage au tick 20, arrêt au tick 25, premier point au tick 41 ; rupture du tick 20 au tick 80, premier point au tick 81.

## Garde (A1)

Posture tenue de `guardStart` à `guardStop`. Pendant la garde :

- entretien de `combat.guard.upkeepPerSecond` (2/s), cumulé sur **tout le combat** (correctif 6.1, M01) : dû = ⌊ 2 × ticks gardés depuis le début du combat × 50 / 1000 ⌋, payé au fil des ticks, soit 1 point tous les 10 ticks gardés. Un tick est gardé si la garde est active en phase 4 : une garde prise puis quittée dans le même tick ne compte pas ; prise, quittée et reprise, elle compte une fois. Le coût ne dépend donc que du nombre de ticks réellement gardés, quelle que soit leur répartition (avant le correctif, le reliquat était perdu à chaque reprise : des gardes de 9 ticks ne coûtaient rien). S'il ne peut être payé, la garde est brisée (`upkeep`) et ce tick n'est pas compté : le reliquat reste dû à la garde suivante ;
- chaque impact direct bloquable coûte `costPerBlock` (10) puis est réduit de `damageReduction` (60 %) après armure (`blocked`, puis `damage` avec `defense: "guard"`). Si l'endurance manque, la garde est brisée **avant** la réduction : l'impact passe en entier (`defense: "guardBroken"`) ;
- lancer une compétence est refusé (`guardActive`) : il faut d'abord `guardStop`.

Rupture de garde : `breakDurationMs` (3000 ms), intervalle [tick de rupture, +60 ticks[. Ni garde ni parade (`guardBroken`), pas de régénération, aucune hausse des dégâts reçus. Une fenêtre de parade ouverte est interrompue (`parryEnded` `interrupted`). Fin journalisée par `stateExpired` (`guardBreak`) ; la garde peut reprendre au tick même de la fin.

## Parade parfaite (A2)

`parry` au tick P ouvre la fenêtre semi-ouverte [P, P + 3[ (150 ms à 50 ms par tick). Un impact résolu aux ticks P, P + 1 ou P + 2 est paré ; au tick P + 3 la fenêtre est déjà fermée (phase 1). Testé pour les décalages −1 à 3.

- Impact paré : −90 % après armure (`defense: "parry"`). Le premier impact paré de la tentative rapporte +1 Charge et +8 Résonance (`parrySucceeded`) ; les impacts suivants de la même fenêtre sont aussi réduits, sans nouvelle récompense.
- Fin de fenêtre (`parryEnded`) : `success`, ou `failed` sans aucune Charge ; un échec impose une récupération de 500 ms comptée depuis la fin de la fenêtre, [P + 3, P + 13[ (`parryRecovery`).
- Pendant une garde : la parade est permise et prioritaire. Un impact dans la fenêtre est seulement paré (−90 %), sans coût de blocage ni Résonance de blocage ; la garde reste active et reprend pour les impacts hors fenêtre. Jamais de double réduction.

Attaque **imparable** (`unblockable`) : ignore la garde et la parade (`defense: "unblockable"`), aucun coût ni récompense ; une fenêtre traversée par un seul impact imparable se termine en échec.

## Ressources (A4, A5)

Propres aux personnages, à zéro au début de chaque combat, remises à zéro à la fin (après le résumé, qui donne la valeur finale). Tout gain passe par `resourceGained` (`resource`, `source`, `amount` réellement gagné, `wasted` perdu au plafond, `value`). Un gain ne déclenche jamais un autre gain.

- **Charges de rempart** : 0 à 5, +1 par parade parfaite réussie, aucun gain passif ni décroissance, excédent perdu. Sources et consommateurs configurables par compétence (`rampartChargeGain`, `rampartChargeCost` dans le bloc `combat` ; refus `insufficientRampartCharges`).
- **Résonance** : 0 à 100, départ à 0 ; +3 par attaque directe réussie, **une fois par action** quel que soit le nombre de cibles (`directHit`) ; +8 par parade parfaite ; +2 par blocage normal (par défenseur et par impact bloqué). L'ultime coûte 100 (`insufficientResonance`) et une action qui dépense de la Résonance n'en rapporte pas (pas de génération récursive).
- « Réussie » : avec un ciblage logique et sans esquive, toute attaque qui touche au moins une cible réussit, même parée ou bloquée.

## États (A19)

| État | Effet | Durée | Réapplication |
|---|---|---|---|
| `fragility` | +10 % de dégâts directs reçus (catégorie « altération » de la formule) | 6 s, [application, +120 ticks[ | renouvelle la durée depuis la nouvelle application, sans cumul (`renewed: true`) |
| `guardBreak` | ni garde ni parade, pas de régénération | 3 s | impossible : la rupture empêche la garde qui pourrait se briser |

Journal : `stateApplied` (`combatant`, `state`, `instance`, `generation`, `untilTick` exclu, `renewed`), `stateExpired` (`combatant`, `state`, `instance`, `generation`) et `stateRemoved` (mêmes champs et `reason`). La Fragilité est appliquée après les dégâts de l'impact qui la porte.

### Contrat des états temporaires (correctif 6.1, M02)

- **Instance** : chaque application d'un état absent crée une instance au numéro unique dans le combat (1, 2, 3… dans l'ordre d'application, tous combattants confondus), jamais réutilisé.
- **Génération** : une réapplication d'un état non cumulable déjà actif garde l'instance, repousse l'échéance et incrémente la génération (`renewed: true`).
- **Échéance** : chaque application programme une expiration portant le couple (instance, génération). À l'échéance, l'expiration n'agit que si ce couple est encore celui de l'état actif ; sinon elle est périmée et ignorée sans aucune ligne ni transition. Un état non cumulable expire donc exactement une fois, quel que soit le nombre d'applications simultanées ou de renouvellements.
- **Même tick** : l'expiration (phase 1) passe avant une réapplication du même tick (phase 3), qui crée alors une nouvelle instance (`renewed: false`).
- **Retrait** : la mort retire les états (`stateRemoved`, `death`) ; leurs échéances deviennent périmées.
- **Pour le lot 7 (non implémenté)** : un état cumulable aura plusieurs instances actives par état, chacune avec sa propre échéance ; un état remplacé ou dissipé sera retiré par `stateRemoved` avec une autre raison (`replaced`, `dispelled`). Le couple (instance, génération) reste la seule clé de validité d'une échéance, et un événement programmé pour une instance retirée ne s'exécute jamais.

## Dégâts

Formule du lot 2 (`DamageCalculator`) : puissance du type de la compétence × coefficient, bonus (Fragilité de la cible : altération +10 %), critique (un tirage par coup, sur le flux PCG32 de l'attaquant), défense de la cible, puis réduction de garde ou de parade (lot 6), arrondi final. Exemple : 100 bruts sans armure → garde 40, parade 10 ; sous Fragilité 110, 44 et 11. Chaque combattant a son propre flux, dérivé de la graine du scénario et du hachage FNV-1a 64 de son identifiant : ajouter un combattant ne change pas les tirages des autres.

## Mannequin

PV infinis (`health: null`), armure et résistance magique configurables, ne riposte jamais (toute commande à son nom est refusée `actorCannotAct`). Les dégâts reçus sont comptés dans son résumé.

## Assaillant d'entraînement (A21)

PV infinis, défenses configurables, n'obéit à aucune commande. Il attaque à `firstAttackMs` puis toutes les `intervalMs` (référence : 2000), en jouant ses `attacks` dans l'ordre puis en boucle. Chaque attaque a des dégâts **bruts** (avant armure ; référence 100), un type de dégâts, un ciblage logique (`enemy` : sa `target` ; `allEnemies` : tous les ennemis vivants par identifiant), `unblockable` et `appliesFragility`. Jamais de critique, donc aucun tirage aléatoire. Journal : `attackStarted` (ou `attackSkipped` si sa cible est morte), puis les impacts.

## Limites

- `combat.limits.maxDurationMs` (600 000) : durée maximale d'un scénario ; plafond absolu du format 86 400 000.
- `combat.limits.maxEventsPerTick` (1000) : budget des **événements de simulation comptés** dans un tick (phases 1 à 6) ; au-delà, arrêt propre `simulationAborted` (`eventBudgetExceeded`). Ce n'est pas un plafond de lignes du journal : l'en-tête et les lignes `combatant` (initialisation), `simulationAborted` (arrêt), `combatantSummary` et `combatEnded` (finalisation) ne sont pas comptés. Elles restent bornées par le nombre de combattants (1 + N à l'initialisation, au plus 2 + N à la fin) ; leur enveloppe propre est fixée dans le contrat du lot 7.
- Précontrôle numérique **avant le premier tick** par `CombatSetup.Validate` (correctif 6.1, M04 ; complété au correctif 6.1.1, CA-M01), pour tous les types de combattants :
  - bornes des valeurs saisies : dégâts bruts d'un assaillant dans [0, 10⁹], armure et résistance magique des mannequins et assaillants dans [0, 10⁹], attributs d'un personnage dans [0, 10⁶], coefficient de compétence dans [0, 100], coûts et gains dans [0, 10⁶] ;
  - bornes des règles : constantes de défense dans ]0, 10⁹] ; toutes les durées (dont `globalCooldownMs` et `endurance.regenDelayMs` depuis 6.1.1, et les recharges de compétences) dans [0, 86 400 000] ms ;
  - valeurs **calculées** avec le même code que le moteur (`CombatantStats`) : statistiques de chaque combattant ; défense effective réelle de chaque participant, **personnages compris**, pour les deux types de dégâts, au plus 10⁹, et dénominateur constante × 10 000 + défense brute représentable ; pire impact de chaque attaque contre chaque défenseur ennemi réel ; pire impact sans défense (majorant de tous les autres) cumulé sur toute la durée au rythme maximal d'événements comptés.
  - Tout dépassement est refusé avec un diagnostic localisé (code `OutOfRange` pour les valeurs calculées, rattaché au combattant) ; rien n'est journalisé, et la CLI ne crée pas le fichier journal.
- Ce que le précontrôle garantit : les calculs **connus et recensés** du moteur à la date du correctif 6.1.1 restent représentables pour un combat validé. Il ne garantit pas qu'aucun débordement n'est possible : tout calcul ajouté plus tard (lot 7 et suivants) doit être recensé et borné à son tour. Tout dépassement arithmétique non anticipé pendant la simulation (`checked`, virgule fixe) arrête le combat proprement (`simulationAborted`, `overflow`) au lieu de planter ; un test vérifie ce filet de sécurité séparément.
- Échéances (cas C et D de CA-M01) : avec un tick de 1 ms, dernier tick possible (< 86 400 000) + délai en ticks (≤ 86 400 000) < 2 × 10⁸ ; produits de la régénération et de l'entretien de garde : (régénération ou coût par seconde ≤ 2³¹) × (ticks × durée du tick ≤ 86 400 000 ms) < 2 × 10¹⁷, loin de 2⁶³.
- Un combat interrompu (`aborted`) n'est pas un résultat d'équilibrage : `eds simulate` renvoie le code de sortie 2 et affiche un bandeau `DIAGNOSTIC` au-dessus des statistiques partielles.
- Natures des valeurs : les plafonds ci-dessus sont des **limites techniques** provisoires de pré-alpha, pas des paramètres d'équilibrage (qui sont dans `rules.json`) ni des **capacités mesurées** (seuls les trois scénarios de référence ont été simulés : au plus 7 événements comptés par tick). Détail : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#trois-natures-de-valeurs).
- Limites prévues pour le lot 7 : [LIMITES_LOT_7.md](LIMITES_LOT_7.md).

Fin de combat : `durationReached`, `teamDefeated` (une seule équipe vivante, ou aucune) ou `aborted`. Chaque fin écrit un `combatantSummary` par combattant puis `combatEnded`.

## Journal JSON Lines

Une ligne JSON par événement, UTF-8 sans BOM, séparateur `\n`, champs dans un ordre fixe, nombres en virgule fixe écrits exactement (`1.5`, jamais `1.5000001`). Écrit par `JsonLineWriter` (cœur, sans bibliothèque).

Première ligne, l'en-tête :

```json
{"type":"header","format":"eds-combat-log","formatVersion":2,"engineVersion":"0.6.1","tickMs":50,
 "data":{"rules":{"contentVersion":"…","fingerprint":"sha256:…"},"classes":[{"id":"class.bastion","contentVersion":"…","fingerprint":"sha256:…"}]},
 "scenario":{…scénario complet…}}
```

Puis les événements : `{"tick":0,"seq":2,"phase":"commands","event":"skillCast",…}`. `seq` est le numéro global de la ligne d'événement.

Événements : `combatant`, `cooldownReady`, `skillCast`, `commandRejected`, `guardStarted`, `guardStopped`, `guardUpkeep`, `guardBroken`, `blocked`, `parryStarted`, `parrySucceeded`, `parryEnded`, `attackStarted`, `attackSkipped`, `damage` (avec `fragile` et `defense` : `none`, `guard`, `guardBroken`, `parry`, `unblockable`), `resourceGained`, `stateApplied`, `stateExpired`, `stateRemoved`, `enduranceRegenerated`, `died`, `simulationAborted`, `combatantSummary` (avec blocages, parades, ruptures, Résonance et Charges), `combatEnded`. Version du moteur : `0.6.1`. Format du journal : 2 depuis le correctif 6.1 (`instance` et `generation` sur les états, `stateRemoved`) ; un journal au format 1 est refusé au rejeu, sans migration (pré-alpha).

## Rejeu

`eds replay journal.jsonl` (ou `CombatLogReplayer`) :

1. lit l'en-tête et vérifie format, version du moteur, tick, puis l'empreinte **et** la `contentVersion` des règles et de chaque classe utilisée face aux données actuelles. Toute différence = rejeu **refusé** avant simulation : un journal n'est pas réputé reproductible si les fichiers de configuration ont changé ;
2. reconstruit le scénario depuis l'en-tête, le rejoue et compare ligne par ligne. Première différence signalée avec son numéro de ligne ; journal tronqué ou ligne modifiée = `Diverged`.

## Scénarios

Fichier `kind: "scenario"` (schéma `data/schemas/scenario.schema.json`) : graine, durée, combattants (`character` avec classe et build complet, `dummy` avec défenses, `assailant` avec défenses, cible, rythme et attaques), commandes horodatées : `{"atMs", "actor", "skill", "target"}` pour une compétence, `{"atMs", "actor", "action"}` avec `guardStart`, `guardStop` ou `parry`. Le build est vérifié par le validateur de builds du lot 4.

Scénarios livrés : `mannequin.json` (lot 5), `entrainement_garde_parade.json` (30 s contre un assaillant : garde, 8 parades réussies et 1 ratée, Fragilité, attaques imparables) et `entrainement_rupture.json` (16 s : garde tenue jusqu'à la rupture, commandes refusées pendant la rupture, parades ensuite).

```bash
dotnet run --project src/EdS.Cli -- simulate scenarios/mannequin.json --out combat.jsonl
dotnet run --project src/EdS.Cli -- replay combat.jsonl
dotnet run --project src/EdS.Cli -- validate scenarios/mannequin.json
```

Résultat de référence du mannequin : 60 s, 16 compétences lancées, 2 refus voulus (Provocation non implémentée, Frappe en recharge), 441 dégâts, DPS 7,35, 48 de Résonance.
