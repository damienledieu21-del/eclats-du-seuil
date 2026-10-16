# Moteur de simulation (lot 5)

Moteur de combat minimal, déterministe, sans Unity, sans réseau et sans horloge système. Code : `src/EdS.Simulation/Engine`. Toutes les valeurs sont **provisoires**.

## Temps et file d'événements

- Horloge logique en ticks (`rules.json` → `combat.tickMs`, 50 ms). Les durées en ms sont converties par `TickRate` ; elles doivent être des multiples du tick (sinon le fichier est refusé).
- Un combat de `durationMs` dure `durationMs / tickMs` ticks, numérotés à partir de 0. Une commande à `atMs` s'exécute au tick `atMs / tickMs`.
- `EventQueue<T>` : tas binaire ordonné par (tick, phase, numéro d'insertion). Deux événements identiques en tick et en phase sortent toujours dans leur ordre d'insertion.

## Ordre intra-tick (A10)

| Phase | Contenu |
|---|---|
| `setup` | tick 0 seulement : une ligne `combatant` par combattant (statistiques calculées) |
| 1. `expirations` | fins de temps de recharge (`cooldownReady`) |
| 2. `commands` | commandes du tick, triées par initiative décroissante, puis identifiant de l'acteur (ordinal), puis ordre dans le scénario ; validation puis paiement (`skillCast` ou `commandRejected`) |
| 3. `resolution` | dégâts des compétences lancées pendant ce tick, dans l'ordre des lancers (`damage`) |
| 4. `periodic` | réservée aux effets périodiques (aucun dans ce lot) |
| 5. `regeneration` | régénération d'endurance (`enduranceRegenerated`) |
| 6. `endOfTick` | morts (`died`), fin de combat, arrêt sur limite |

Conséquences testées (`TimingTests`, `OrderingTests`) :

- un temps de recharge qui se termine au tick T est disponible pour une commande du même tick T (l'expiration passe avant les commandes) ;
- toutes les commandes d'un tick sont validées avant toute résolution : un combattant tué au tick T peut encore agir au tick T ; il meurt en fin de tick ;
- l'ordre des lignes ne dépend que du scénario, jamais de l'ordre d'insertion en mémoire ni d'un dictionnaire.

## Commandes et refus atomiques

Une commande (`atMs`, `actor`, `skill`, `target` facultative) est d'abord entièrement validée ; l'état n'est modifié qu'ensuite. Une commande refusée ne consomme ni endurance, ni recharge, ni GCD, ni tirage aléatoire. Premier motif rencontré, dans cet ordre :

`actorDead`, `actorCannotAct` (mannequin), `skillNotEquipped`, `skillNotImplemented` (compétence sans bloc `combat`), `globalCooldown`, `cooldown`, puis ciblage : `targetRequired`, `targetNotEnemy`, `targetDead` (cible unique) ou `targetNotAllowed` (zone), et enfin `insufficientEndurance`.

## Rythme (A6)

Compétences instantanées. GCD de `combat.globalCooldownMs` (1000 ms) déclenché par chaque lancer ; recharge individuelle démarrant au lancer. Lancer au tick T avec une recharge de D ms : disponible à T + D / tickMs. Pas d'animation ni de temps d'incantation.

## Ciblage (A7)

Logique uniquement : pas de position, de distance ni de collision. `enemy` = une cible ennemie vivante désignée ; `allEnemies` = tous les ennemis vivants, dans l'ordre du scénario. L'énumération `SkillTargeting` est le point d'extension pour un ciblage spatial futur.

## Endurance (A3)

Coût payé au lancer, maximum respecté. Régénération de `combat.endurance.regenPerSecond` (10/s) après `regenDelayMs` (1000 ms) sans dépense. Elle est calculée sur le total depuis la dernière dépense, pour éviter toute dérive d'arrondi :

```
ticksRégén = t − dernièreDépense − délaiTicks + 1   (si > 0)
gagné(t)   = min(max − enduranceAprèsDépense, ⌊régénParSeconde × ticksRégén × tickMs / 1000⌋)
```

Avec 50 ms : dépense au tick 20, rien jusqu'au tick 39, +0,5 cumulé au tick 40 (arrondi à 0), premier point au tick 41, puis +1 tous les 2 ticks. Pleine endurance : le compteur s'arrête. Pas de « garde brisée » dans ce lot.

## Dégâts

Formule du lot 2 (`DamageCalculator`) : puissance du type de la compétence × coefficient, bonus, critique (un tirage par coup, sur le flux PCG32 de l'attaquant), défense de la cible. Chaque combattant a son propre flux, dérivé de la graine du scénario et du hachage FNV-1a 64 de son identifiant : ajouter un combattant ne change pas les tirages des autres.

## Mannequin

PV infinis (`health: null`), armure et résistance magique configurables, ne riposte jamais (toute commande à son nom est refusée `actorCannotAct`). Les dégâts reçus sont comptés dans son résumé.

## Limites

- `combat.limits.maxDurationMs` (600 000) : durée maximale d'un scénario ; plafond absolu du format 86 400 000.
- `combat.limits.maxEventsPerTick` (1000) : au-delà, arrêt propre `simulationAborted` (`eventBudgetExceeded`).
- Tout dépassement arithmétique (`checked`, virgule fixe) arrête le combat proprement (`simulationAborted`, `overflow`) au lieu de planter.

Fin de combat : `durationReached`, `teamDefeated` (une seule équipe vivante, ou aucune) ou `aborted`. Chaque fin écrit un `combatantSummary` par combattant puis `combatEnded`.

## Journal JSON Lines

Une ligne JSON par événement, UTF-8 sans BOM, séparateur `\n`, champs dans un ordre fixe, nombres en virgule fixe écrits exactement (`1.5`, jamais `1.5000001`). Écrit par `JsonLineWriter` (cœur, sans bibliothèque).

Première ligne, l'en-tête :

```json
{"type":"header","format":"eds-combat-log","formatVersion":1,"engineVersion":"0.5.0","tickMs":50,
 "data":{"rules":{"contentVersion":"…","fingerprint":"sha256:…"},"classes":[{"id":"class.bastion","contentVersion":"…","fingerprint":"sha256:…"}]},
 "scenario":{…scénario complet…}}
```

Puis les événements : `{"tick":0,"seq":2,"phase":"commands","event":"skillCast",…}`. `seq` est le numéro global de la ligne d'événement.

Événements : `combatant`, `cooldownReady`, `skillCast`, `commandRejected`, `damage`, `enduranceRegenerated`, `died`, `simulationAborted`, `combatantSummary`, `combatEnded`.

## Rejeu

`eds replay journal.jsonl` (ou `CombatLogReplayer`) :

1. lit l'en-tête et vérifie format, version du moteur, tick, puis l'empreinte **et** la `contentVersion` des règles et de chaque classe utilisée face aux données actuelles. Toute différence = rejeu **refusé** avant simulation : un journal n'est pas réputé reproductible si les fichiers de configuration ont changé ;
2. reconstruit le scénario depuis l'en-tête, le rejoue et compare ligne par ligne. Première différence signalée avec son numéro de ligne ; journal tronqué ou ligne modifiée = `Diverged`.

## Scénarios

Fichier `kind: "scenario"` (schéma `data/schemas/scenario.schema.json`, exemple `scenarios/mannequin.json`) : graine, durée, combattants (`character` avec classe et build complet, ou `dummy` avec défenses), commandes horodatées. Le build est vérifié par le validateur de builds du lot 4.

```bash
dotnet run --project src/EdS.Cli -- simulate scenarios/mannequin.json --out combat.jsonl
dotnet run --project src/EdS.Cli -- replay combat.jsonl
dotnet run --project src/EdS.Cli -- validate scenarios/mannequin.json
```

Résultat de référence : 60 s, 16 compétences lancées, 2 refus voulus (Provocation non implémentée, Frappe en recharge), 441 dégâts, DPS 7,35, 244 lignes.
