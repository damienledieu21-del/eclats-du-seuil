# Données et validation (lots 3 à 6, correctifs 6.1 et 6.1.1)

## Fichier de règles

`data/v0/rules.json` centralise tous les paramètres de `StatRules`, `DamageRules` `ProgressionRules` (bloc `progression`, lot 4) et `CombatRules` (bloc `combat` : `tickMs`, `globalCooldownMs`, `endurance.*`, `limits.*` au lot 5 ; `guard.*`, `parry.*`, `rampartCharges.*`, `resonance.*`, `states.fragility.*` au lot 6). Toutes les valeurs provisoires du moteur y sont, aucune n'est codée en dur. `schemaVersion` vaut 2 depuis le correctif 6.1 (voir « Versions de format »). Le schéma `data/schemas/rules.schema.json` sert à l'auto-complétion dans les éditeurs ; la validation qui fait foi est celle du programme.

```json
{
  "$schema": "../schemas/rules.schema.json",
  "schemaVersion": 2,                    // forme du fichier ; changement incompatible = +1, sans migration en pré-alpha
  "kind": "rules",                       // type de fichier attendu
  "contentVersion": "0.2.0-provisoire",  // version SemVer des valeurs
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
sha256:67d3894c9d1f66fee1863b16430d6b6a75b8dea3c3929780ac9e8c0099c8fd4b
```

(La valeur a changé au correctif 6.1 uniquement parce que la version de schéma, passée à 2, fait partie de l'écriture canonique ; aucun paramètre n'a changé.)

## Versions de format

| Fichier | `schemaVersion` | Historique |
|---|---|---|
| `rules.json` | 2 | 1 : lots 3 à 5 ; 2 : blocs `combat.guard`, `parry`, `rampartCharges`, `resonance`, `states` du lot 6 |
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

Codes : `FileUnreadable`, `DuplicateId`, `InvalidJson`, `WrongType`, `MissingField`, `UnknownField`, `DuplicateField`, `InvalidNumber`, `OutOfRange`, `UnknownReference`, `UnsupportedSchemaVersion`, `InvalidContentVersion`, `WrongKind`, `InvalidValue`. Toutes les erreurs sont rapportées en une passe.

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
