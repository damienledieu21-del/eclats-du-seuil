# Données et validation (lots 3 à 6, correctifs 6.1 et 6.1.1, sous-lot 7.1)

## Fichier de règles

`data/v0/rules.json` centralise tous les paramètres de `StatRules`, `DamageRules` `ProgressionRules` (bloc `progression`, lot 4) et `CombatRules` (bloc `combat` : `tickMs`, `globalCooldownMs`, `endurance.*`, `limits.*` au lot 5 ; `guard.*`, `parry.*`, `rampartCharges.*`, `resonance.*`, `states.fragility.*` au lot 6). Toutes les valeurs provisoires du moteur y sont, aucune n'est codée en dur. `schemaVersion` vaut 3 depuis le lot 7.1 (voir « Versions de format »). Le schéma `data/schemas/rules.schema.json` sert à l'auto-complétion dans les éditeurs ; la validation qui fait foi est celle du programme.

```json
{
  "$schema": "../schemas/rules.schema.json",
  "schemaVersion": 3,                    // forme du fichier ; changement incompatible = +1, sans migration en pré-alpha
  "kind": "rules",                       // type de fichier attendu
  "contentVersion": "0.3.0-provisoire",  // version SemVer des valeurs
  "data": { "stats": { … }, "damage": { … }, "progression": { … }, "combat": { … } }
}
```

Tous les champs sont obligatoires : aucune valeur par défaut silencieuse. Décimaux à 4 décimales au plus, sans exposant ; entiers sans décimale.

Identifiants stables utilisés comme clés : attributs `force`, `dexterite`, `intelligence`, `vitalite`, `volonte`, `endurance` ; types de dégâts `physique`, `magique` ; arrondis `halfAwayFromZero`, `halfToEven`, `towardZero`, `floor`, `ceiling`.

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
sha256:9473e0ca90af517d9bf03807d7e08e4d06cafd781ba2ba3b094d7aadc29db5a1
```

(Elle a changé au correctif 6.1 avec la version de schéma 2, puis au lot 7.1 avec la version 3 et les cinq nouvelles limites ; aucun paramètre préexistant n'a changé.)

## Versions de format

| Fichier | `schemaVersion` | Historique |
|---|---|---|
| `rules.json` | 3 | 1 : lots 3 à 5 ; 2 : blocs `combat.guard`, `parry`, `rampartCharges`, `resonance`, `states` du lot 6 ; 3 : `combat.limits.maxCombatants`, `maxCommands`, `maxCommandsPerActorPerTick`, `maxPendingEvents`, `maxWorkPerTick` (lot 7.1) |
| classes | 2 | 1 : lots 4 et 5 ; 2 : `unblockable`, `rampartChargeCost`, `rampartChargeGain` obligatoires dans le bloc `combat` |
| scénarios | 1 | ajouts compatibles seulement (assaillant, `action`) : un scénario du lot 5 reste lisible |
| journal (`formatVersion`) | 2 | 2 : `instance` et `generation` sur les états, `stateRemoved` |

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
sha256:b7a3a07bb8584f6907d4ea44c4948dec7b31b5c910bba0dc3c70ba2571970ad2
```

Le bloc facultatif `combat` d'une compétence entre dans l'empreinte (`combat=none` s'il est absent). Depuis le lot 6, il porte aussi `unblockable`, `rampartChargeCost` et `rampartChargeGain` (obligatoires dans le bloc).

## Scénarios et jeu de données (lots 5 et 6)

`GameDataLoader.Load("data/v0")` charge `rules.json` et tous les `classes/*.json` en un `GameData` (règles, classes, identité : versions et empreintes). `ScenarioJsonReader` (forme) → `ScenarioDocument` → `ScenarioValidator` (en-tête, références de classe, build validé par `BuildValidator`, puis `CombatSetup.Validate`) → `CombatSetup`. L'identité retenue pour le journal ne couvre que les règles et les classes utilisées par le scénario. Depuis le lot 6, un scénario peut contenir un assaillant (`type: "assailant"`) et des commandes défensives (`action`). Format, exemples et rejeu : [MOTEUR.md](MOTEUR.md).

Dépendances (lot 6) : la description d'un combat (`CombatSetup`, combattants, commandes, identité des données) est passée d'`EdS.Simulation` à `EdS.Domain` (espace `EdS.Domain.Combat`). `EdS.Data` ne dépend donc plus que du domaine ; seuls les outils (`EdS.Data.Json`, `EdS.Cli`) référencent le moteur pour simuler et rejouer.

```
EdS.Domain ◄── EdS.Simulation ◄── EdS.Data.Json ──► EdS.Data ──► EdS.Domain
                                       ▲
                                    EdS.Cli
```
