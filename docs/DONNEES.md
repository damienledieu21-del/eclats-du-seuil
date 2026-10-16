# Données et validation (lots 3 à 6, correctifs 6.1 et 6.1.1, sous-lots 7.1 et 7.2)

## Fichier de règles

`data/v0/rules.json` centralise tous les paramètres de `StatRules`, `DamageRules` `ProgressionRules` (bloc `progression`, lot 4) et `CombatRules` (bloc `combat` : `tickMs`, `globalCooldownMs`, `endurance.*`, `limits.*` au lot 5 ; `guard.*`, `parry.*`, `rampartCharges.*`, `resonance.*` au lot 6 ; `guard.breakStatus` au lot 7.2) et le catalogue des altérations (bloc `statuses` à la racine de `data`, lot 7.2). Toutes les valeurs provisoires du moteur y sont, aucune n'est codée en dur. `schemaVersion` vaut 4 depuis le lot 7.2 (voir « Versions de format »). Le schéma `data/schemas/rules.schema.json` sert à l'auto-complétion dans les éditeurs ; la validation qui fait foi est celle du programme.

```json
{
  "$schema": "../schemas/rules.schema.json",
  "schemaVersion": 4,                    // forme du fichier ; changement incompatible = +1, sans migration en pré-alpha
  "kind": "rules",                       // type de fichier attendu
  "contentVersion": "0.4.1-provisoire",  // version SemVer des valeurs
  "data": { "stats": { … }, "damage": { … }, "progression": { … }, "combat": { … }, "statuses": [ … ] }
}
```

Tous les champs sont obligatoires (sauf les champs facultatifs d'une altération, ci-dessous) : aucune valeur par défaut silencieuse. Décimaux à 4 décimales au plus, sans exposant ; entiers sans décimale.

Identifiants stables utilisés comme clés : attributs `force`, `dexterite`, `intelligence`, `vitalite`, `volonte`, `endurance` ; types de dégâts `physique`, `magique` ; arrondis `halfAwayFromZero`, `halfToEven`, `towardZero`, `floor`, `ceiling`.

## Altérations (sous-lot 7.2)

`data.statuses` est une liste **ordonnée** de définitions (au plus 64) : l'ordre fixe la priorité des motifs de refus (la première altération bloquante l'emporte) et entre dans l'empreinte. Champs obligatoires : `id`, `category` (`dot`, `control`, `modifier`, `immunity`), `durationMs`, `stacking` (`refresh`, `perSourceKeepStronger`, `keepLonger`, `ignore`), `resistance` (`none`, `defense`, `duration`). Champs facultatifs, à valeur neutre s'ils sont absents : `periodMs` et `damageType` (DOT), `blocks` (`skills`, `guard`, `parry`, `regeneration`, `assailantAttacks`), `interrupts` (`guard`, `parry`), `blockReason`, `damageTakenIncrease` (0 à 10), `movementSpeedModifier` (−1 à 1), `grantsOnExpiry`, `immuneTo`.

```json
{ "id": "stun", "category": "control", "durationMs": 1500, "stacking": "ignore", "resistance": "duration",
  "blocks": ["skills", "guard", "parry", "assailantAttacks"], "interrupts": ["guard", "parry"],
  "blockReason": "stunned", "grantsOnExpiry": "stunImmunity" }
```

Correctif 7.2 (contenu `0.4.1-provisoire`) : l'étourdissement passe de `keepLonger` à `ignore` ; un étourdissement actif refuse toute nouvelle application, ce qui garantit son expiration et l'immunité qui la suit.

Cohérence vérifiée par `StatusCatalog.Validate` (seule validation de gameplay), erreurs rattachées à `$.data.statuses[i].champ` : identifiants uniques ; durée positive, multiple du tick, au plus 24 h ; un DOT a une période multiple du tick qui divise sa durée, un type de dégâts, le cumul par source et la résistance `none` ou `defense`, et lui seul a période, type de dégâts, cumul par source ou résistance `defense` ; seul un contrôle a la résistance `duration` ; une altération qui bloque a un `blockReason` (et réciproquement) ; seule une immunité a `immuneTo`, qui désigne au moins une altération existante, sans doublon ni immunité ; un état accordé existe, n'est ni un DOT ni l'altération elle-même et n'accorde rien à son tour (profondeur 1). `combat.guard.breakStatus` désigne une altération existante, ni DOT ni immunité.

Applications (`applies`, dans le bloc `combat` d'une compétence et dans chaque attaque d'assaillant ; obligatoire, éventuellement vide) : `{"status": "bleed", "coefficient": 0.5}`. Le coefficient est obligatoire pour un DOT, interdit sinon, dans [0, 100] ; une altération citée deux fois, inconnue, une immunité ou l'altération de rupture sont refusées. `stats.statusResistanceConstant` (K, 1000) est dans ]0, 10⁹] ; `statusResistance` d'un mannequin ou d'un assaillant (facultatif, 0 par défaut) dans [0, 10⁹].

## Déclencheurs (sous-lot 7.3)

Un déclencheur est une donnée, jamais du code : `triggers` est une liste facultative (au plus 8) sur un **talent** ou une **compétence** de classe (passive, active ou ultime ; seuls les talents de rang ≥ 1 et les compétences équipées sont actifs), sur une **altération** du catalogue (`statuses[].triggers`, actif tant que l'instance existe) et sur un **assaillant** de scénario (Q4, pour les tests et les entraînements). L'équipement est réservé (Q9), non implémenté. Aucune donnée livrée n'en porte : les déclencheurs de gameplay restent dans les tests jusqu'au lot 8 (Q14).

```json
{ "on": "onBlock", "role": "source", "cooldownMs": 500, "filter": { "damageType": "physique" },
  "effects": [ { "type": "strike", "target": "eventTarget", "coefficient": 1, "powerType": "physique", "damageType": "magique", "unblockable": true },
               { "type": "applyStatus", "target": "eventTarget", "status": "bleed", "coefficient": { "base": 0.25, "perRank": 0.05 } } ] }
```

- `on` (obligatoire) : `onSkillCast`, `onHitDealt`, `onHitTaken`, `onBlock`, `onPerfectParry`, `onGuardBroken`, `onResourceGained`, `onStateApplied`, `onStateExpired`, `onStateRemoved`, `onDotTick`, `onDeath` (événements observables de §1.2 seulement ; aucun diagnostic n'est observable).
- `role` : `self`, `source` ou `target`, obligatoire quand le type en admet plusieurs (blocage, parade, rupture, états, DOT), interdit sinon ; `onDeath` n'admet que `source` (le tueur), `onHitTaken` que `target`.
- `filter` (facultatif) : `skills` (identifiants de compétences ou d'attaques d'assaillant), `damageType`, `defense` (liste parmi `none`, `guard`, `parry`, `unblockable`, `guardBroken`), `status` ou `category` (pas les deux), `resource` (`rampartCharges` ou `resonance`), `renewed` ; chaque critère doit avoir un sens pour `on`. Le filtre ne lit que l'instantané de l'événement.
- `cooldownMs` : recharge interne dans [0, 24 h], multiple du tick.
- `effects` : 1 à 4, exécutés dans l'ordre comme une seule réaction. `strike` : `target` (`eventSource`, `eventTarget`, `allEnemies`, jamais le porteur), `coefficient` (0 à 100), `powerType`, `damageType`, `unblockable` facultatif. `applyStatus` : `target` (les mêmes, ou `bearer`), `status` (mêmes règles que `applies` : coefficient obligatoire pour un DOT, ni immunité ni rupture de garde). `gainResource` : `resource` (`resonance`, `rampartCharges`, `endurance`), `amount` entier (au plus le coût maximal d'une compétence), toujours au porteur. `onDeath` ne vise pas `eventTarget` (le mort).
- Valeurs par rang : un nombre, ou `{ "base": x, "perRank": y }` pour un talent seulement, borné à chaque rang possible.

Validation unique (`TriggerDefinition.ValidateList` et `ValidateReferences`, appelées par les lecteurs de règles, de classes et de scénarios et par `CombatSetup.Validate`), erreurs localisées à `…triggers[i].champ`. Un **cycle statique** possible (un déclencheur dont les effets peuvent produire l'événement qu'il observe, directement ou par une chaîne de déclencheurs) est un **avertissement** du rapport, jamais une erreur (Q1) : la règle de cycle et la profondeur garantissent la terminaison. `combat.limits.maxTriggerDepth` (4, de 0 à 8) borne la profondeur. Comportement au combat : [MOTEUR.md](MOTEUR.md#déclencheurs-sous-lot-73).

## Architecture

```
rules.json ──► EdS.Data.Json (System.Text.Json, outils .NET uniquement)
                    │ forme : syntaxe, types, champs inconnus/en double, nombres
                    ▼
               RulesDocument ──► RulesValidator (EdS.Data, commun)
                                    │ en-tête, champs obligatoires, références
                                    ▼
                               StatRules / DamageRules / ProgressionRules / CombatRules .Validate (domaine)
                                    │ seule validation de gameplay
                                    ▼
                               LoadedRules (règles + versions + empreinte)
```

- `EdS.Data` (netstandard2.1, aucune dépendance) : modèle `RulesDocument`, catalogue `RulesCatalog` (unique description du format), validateur commun, empreinte. Un futur chargeur Unity remplira le même `RulesDocument` et appellera le même validateur.
- `EdS.Data.Json` (net10.0) : adaptateur System.Text.Json, sans réflexion.

## Empreinte

`sha256:…` calculée sur une écriture canonique (version de schéma, puis chaque paramètre dans l'ordre du catalogue). Indépendante des espaces, de l'ordre des clés et de l'écriture des nombres (`2` = `2.0`) ; ignore `contentVersion`. Valeur actuelle de `data/v0/rules.json` :

```
sha256:ee482e9a5ac947bfdb51398452985e848d3cee01b64a71f53a67ea7cf1c4cbb4
```

(Elle a changé au correctif 6.1 avec la version de schéma 2, au lot 7.1 avec la version 3 et les cinq nouvelles limites, puis au lot 7.2 avec la version 4, la constante de résistance, `breakStatus` et les altérations, écrites dans l'ordre du fichier avec chaque champ facultatif à sa valeur neutre ; au correctif 7.2, l’étourdissement passant en `ignore` ; enfin au lot 7.3 avec la version 5 et `combat.limits.maxTriggerDepth`. Une altération sans déclencheur n’ajoute rien au texte canonique.)

## Versions de format

| Fichier | `schemaVersion` | Historique |
|---|---|---|
| `rules.json` | 5 | 1 : lots 3 à 5 ; 2 : blocs `combat.guard`, `parry`, `rampartCharges`, `resonance`, `states` du lot 6 ; 3 : `combat.limits.maxCombatants`, `maxCommands`, `maxCommandsPerActorPerTick`, `maxPendingEvents`, `maxWorkPerTick` (lot 7.1) ; 4 : `statuses`, `stats.statusResistanceConstant`, `combat.guard.breakStatus` à la place de `breakDurationMs`, `combat.states` supprimé (lot 7.2) ; 5 : `combat.limits.maxTriggerDepth`, `statuses[].triggers` facultatif (lot 7.3) |
| classes | 4 | 1 : lots 4 et 5 ; 2 : `unblockable`, `rampartChargeCost`, `rampartChargeGain` obligatoires dans le bloc `combat` ; 3 : `applies` obligatoire dans le bloc `combat` (lot 7.2) ; 4 : `triggers` facultatif sur les talents et les compétences (lot 7.3) |
| scénarios | 2 | 1 : lots 5 et 6 (assaillant, `action`) ; 2 : `applies` à la place de `appliesFragility`, `statusResistance` facultatif des mannequins et assaillants (lot 7.2) |
| journal (`formatVersion`) | 4 | 2 : `instance` et `generation` sur les états, `stateRemoved` ; 3 : `category` et `source` sur les états, `applies`, `stateBlocked`, `dotDamage` (lot 7.2) ; 4 : provenance des effets, `eid`, `cause`, `root`, `depth`, `trigger` (lot 7.3, voir MOTEUR.md) |

Le lot 6 avait rendu les formats de règles et de classes incompatibles sans changer leur numéro ; le correctif 6.1 corrige cet oubli.

**Politique pré-alpha : aucune migration garantie.** Un fichier d'une version antérieure est refusé explicitement, par un seul message sur `$.schemaVersion` (`UnsupportedSchemaVersion`, « structure incompatible, refusée ; aucune migration n'est garantie »), sans analyse de son contenu : pas de cascade de champs manquants. Il se met à jour à la main en suivant ce document. Une version future est refusée de la même façon. Les migrateurs automatiques n'arriveront qu'avec un format stabilisé.

Contrat des chaînes (correctif 6.1) : `contentVersion` suit SemVer 2.0 strictement (chiffres ASCII, pas de zéro initial, y compris dans les identifiants numériques de pré-version, toute la chaîne : un saut de ligne final est refusé). Identifiants stables et noms de combattants : minuscules ASCII, chiffres et `_`, correspondance de toute la chaîne.

## Erreurs

Chaque erreur donne un chemin JSON, un code stable et, quand c'est possible, la ligne :

```
$.data.stats.base.maxHealt (ligne …) : champ inconnu ; champs possibles : physicalPower, … [UnknownField]
$.data.damage.defenseConstants.feu (ligne …) : type de dégâts inconnu « feu » ; valeurs possibles : physique, magique. [UnknownReference]
$.data.stats.critical.maxChanceBasisPoints : la chance critique maximale ne peut pas dépasser 10 000 points de base (100 %). [InvalidValue]
```

Bornes techniques des règles (correctif 6.1.1) : `damage.defenseConstants.*` dans ]0, 10⁹], `combat.globalCooldownMs` et `combat.endurance.regenDelayMs` dans [0, 86 400 000] ms, comme les autres durées. Une valeur **calculée** hors des limites numériques du moteur (défense effective d'un personnage, pire impact, cumul) est refusée à la validation du scénario avec le code `OutOfRange`, sur le chemin du combattant (`$.data.combatants[0]`). Voir [MOTEUR.md](MOTEUR.md#limites).

Limites de volume (sous-lot 7.1) : les cinq champs de `combat.limits` ajoutés en schéma 3 sont bornés par des plafonds techniques (`maxCombatants` [2, 256], `maxCommands` [0, 10⁶], `maxCommandsPerActorPerTick` [1, 64], `maxPendingEvents` [1, 10⁷], `maxWorkPerTick` [1, 10⁷]) et `maxEventsPerTick` ne peut pas dépasser `maxWorkPerTick`. Un scénario qui dépasse une limite configurée est refusé avec le code `LimitExceeded` : sur le tableau (`$.data.combatants`, `$.data.commands`, une seule erreur, sans analyser les éléments), sur `$.data.commands[i].atMs` pour la première commande en excès d'un acteur à un tick, ou sur `$.data.commands` si les événements initiaux dépassent la capacité de la file.

Limites des lecteurs (sous-lot 7.1, constantes techniques) : fichier de données ou texte JSON au plus 16 Mio (`FileTooLarge`, vérifié avant lecture ou avant encodage), profondeur JSON au plus 32 (`InvalidJson`), journal rejoué au plus 256 Mio. Un rapport conserve au plus 200 erreurs ; les suivantes sont comptées et résumées par une dernière ligne « … et N autres erreurs ». Détail : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#limites-des-lecteurs-en-place-sous-lot-71).

Codes : `FileUnreadable`, `DuplicateId`, `InvalidJson`, `WrongType`, `MissingField`, `UnknownField`, `DuplicateField`, `InvalidNumber`, `OutOfRange`, `UnknownReference`, `UnsupportedSchemaVersion`, `InvalidContentVersion`, `WrongKind`, `InvalidValue`, `LimitExceeded`, `FileTooLarge` (sous-lot 7.1). Toutes les erreurs sont rapportées en une passe, dans la limite de 200.

## Commande

```bash
dotnet run --project src/EdS.Cli -- validate                  # data/v0/rules.json
dotnet run --project src/EdS.Cli -- validate chemin/rules.json
dotnet run --project src/EdS.Cli -- validate data/v0/classes/bastion.json [--rules chemin/rules.json]
```

Le champ `kind` choisit le validateur. Un fichier de classe est vérifié avec la progression du `rules.json` indiqué (défaut : `data/v0/rules.json`).

## Fichiers de classe

Un fichier par classe (`data/v0/classes/<classe>.json`, `kind: "class"`), même en-tête que `rules.json`, schéma `data/schemas/class.schema.json`. Format et règles : [BUILDS.md](BUILDS.md). Même architecture : `ClassJsonReader` (forme) → `ClassDocument` → `ClassValidator` (en-tête, présence) → `ClassDefinition.Validate` (seule validation de gameplay). En-tête, empreinte et lecture JSON sont partagés (`HeaderValidator`, `ContentFingerprint`, `JsonInput`).

Empreinte d'une classe : SHA-256 de `eds-class` puis chaque valeur `chemin=valeur` dans l'ordre du fichier (l'ordre des arbres, talents et compétences fait partie du contenu ; les espaces et l'ordre des clés non ; `contentVersion` ignorée). Valeur actuelle de `bastion.json` :

```
sha256:5efb027f289b02e37ffdee6f9d41062acdce9bfd94f2921a9ba6432b3748a51f
```

Le bloc facultatif `combat` d'une compétence entre dans l'empreinte (`combat=none` s'il est absent). Depuis le lot 6, il porte aussi `unblockable`, `rampartChargeCost` et `rampartChargeGain`, et depuis le lot 7.2 `applies` (obligatoires dans le bloc).

## Scénarios et jeu de données (lots 5 et 6)

`GameDataLoader.Load("data/v0")` charge `rules.json` et tous les `classes/*.json` en un `GameData` (règles, classes, identité : versions et empreintes). `ScenarioJsonReader` (forme) → `ScenarioDocument` → `ScenarioValidator` (en-tête, références de classe, build validé par `BuildValidator`, puis `CombatSetup.Validate`) → `CombatSetup`. L'identité retenue pour le journal ne couvre que les règles et les classes utilisées par le scénario. Depuis le lot 6, un scénario peut contenir un assaillant (`type: "assailant"`) et des commandes défensives (`action`). Format, exemples et rejeu : [MOTEUR.md](MOTEUR.md).

Dépendances (lot 6) : la description d'un combat (`CombatSetup`, combattants, commandes, identité des données) est passée d'`EdS.Simulation` à `EdS.Domain` (espace `EdS.Domain.Combat`). `EdS.Data` ne dépend donc plus que du domaine ; seuls les outils (`EdS.Data.Json`, `EdS.Cli`) référencent le moteur pour simuler et rejouer.

```
EdS.Domain ◄── EdS.Simulation ◄── EdS.Data.Json ──► EdS.Data ──► EdS.Domain
                                       ▲
                                    EdS.Cli
```
