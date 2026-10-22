# Données et validation (lots 3 à 6, correctifs 6.1 et 6.1.1, sous-lots 7.1 à 7.3, sous-lots 8.1 à 8.5, sous-lots 11.4a à 11.4d, sous-lot 11.5)

## Fichier de règles

`data/v0/rules.json` centralise tous les paramètres de `StatRules`, `DamageRules` `ProgressionRules` (bloc `progression`, lot 4) et `CombatRules` (bloc `combat` : `tickMs`, `globalCooldownMs`, `endurance.*`, `limits.*` au lot 5 ; `guard.*`, `parry.*`, `rampartCharges.*`, `resonance.*` au lot 6 ; `guard.breakStatus` au lot 7.2 ; `triggers.areaStrikeMinCooldownMs` au sous-lot 8.4 ; `movement.*` et `contact.*` au sous-lot 11.4a, voir ci-dessous) et le catalogue des altérations (bloc `statuses` à la racine de `data`, lot 7.2). Toutes les valeurs provisoires du moteur y sont, aucune n'est codée en dur. `schemaVersion` vaut 4 depuis le lot 7.2 (voir « Versions de format »). Le schéma `data/schemas/rules.schema.json` sert à l'auto-complétion dans les éditeurs ; la validation qui fait foi est celle du programme.

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

## Déplacement et contact (sous-lot 11.4a)

Bloc `combat` de `rules.json`, schéma 10, contenu 0.9.0 (arc du bouclier et annonces : schéma 12, contenu 0.11.0, sous-lot 11.4c) ; ne servent qu'aux combats positionnés ([MOTEUR.md](MOTEUR.md#combat-positionné-sous-lot-114a)) :

| Paramètre | Valeur livrée | Sens et bornes |
|---|---|---|
| `combat.movement.speed` | 5 | vitesse de marche de base, m/s, de 0,1 à 20 ; valeur de départ retenue le 20/10/2026 |
| `combat.movement.guardSpeedFactor` | 0,5 | facteur de vitesse en garde, de 0 à 1 ; valeur de départ retenue, fixée à l'essai 11.9 (L11-D3) |
| `combat.contact.playerCreature` | `solid` | contact joueur–créature : `solid` (règle A, volumes solides sans poussée, garde-fou anti-piège) ou `slowedCrossing` (règle B, traversée freinée) ; point de départ, choix de Damien après l'essai du 11.6 (L11-D5) |
| `combat.contact.crossingSpeedFactor` | 0,3 | règle B : facteur de vitesse d'un joueur qui chevauche une créature, dans ]0, 1] ; proposition de départ (« ralentit fortement ») |
| `combat.guard.arcDegrees` | 180 | sous-lot 11.4c (L11-D3, retenu) : arc frontal du bouclier, en degrés, de 1 à 360 ; garde et parade parfaite ne valent que contre un coup venu de cet arc |
| `combat.telegraph.minReactionMs` | 300 | sous-lot 11.4c : temps de réaction minimal d'une annonce (ms, 0 à 10 000), valeur de départ retenue, réglée au 11.9 |
| `combat.contact.largeCreatureRadius` | 1 | rayon (m) à partir duquel une créature reste solide pour les joueurs dans les deux règles, de 0,1 à 5 ; proposition de départ (un chef fait 1,2 m) |

Le `movementSpeedModifier` des altérations (gel −0,3) s'applique désormais à la vitesse d'un combattant positionné. Nouveau blocage `movement` (`blocks`) : le combattant ne marche pas tant que l'altération dure, quels que soient ses bonus de vitesse ; son intention reste et il repart à la fin. **L'étourdissement l'a** : décision de Damien du 20 octobre 2026 à 03 h 38 (« oui » à la question de la PR #28).

## Portées (sous-lot 11.4b)

Ne servent qu'aux combats positionnés ([MOTEUR.md](MOTEUR.md#portées-et-ligne-de-vue-sous-lot-114b)) ; sans espace, elles sont ignorées. Toutes facultatives (absentes : sans limite), en mètres, de 0,1 à 100 (borne technique), 4 décimales au plus. Le validateur refuse une portée sans objet, au chemin du champ.

| Champ | Fichier (schéma) | Sens |
|---|---|---|
| `combat.range` | classes (9) | compétence `enemy` ou `ally` : distance maximale entre les bords des emprises, au lancement, avec la ligne de vue ; `allEnemies` ou `allAllies` : rayon du cercle centré sur le lanceur, à l'impact ; interdite pour `self` |
| `combat.applies[].range` | classes (9) | seulement avec `target` `allAllies` ou `allEnemies` : rayon autour de l'acteur |
| `triggers[].effects[].range` | classes (9) | `strike` ou `applyStatus` vers `eventSource` ou `eventTarget` (bords des emprises du porteur et de la cible) ou `allEnemies` (rayon autour du porteur) ; jamais pour `bearer` ni `gainResource` |
| `statuses[].range` | règles (11) | protection seulement : le protecteur doit être à cette distance du protégé (bords) pour qu'un impact ou un tick de DOT soit transféré |

La portée n'est lue **ni** dans les déclencheurs des règles **ni** dans les scénarios (attaques et déclencheurs d'assaillant) : champ inconnu, comme avant (formes des attaques d'assaillant au 11.4c). Valeurs livrées (Bastion 0.6.0, règles 0.10.0), retenues par Damien le 20 octobre 2026 ([COMBAT_ESPACE.md](COMBAT_ESPACE.md#7-le-bastion-dans-lespace)) :

| Donnée | Portée |
|---|---|
| Frappe du rempart, Heurt de bouclier, Contrecoup | 2,5 m (mêlée) |
| Provocation, Barrière d'acier | 15 m |
| Onde de rupture | rayon 5 m |
| Bastion absolu | alliés à 15 m (`bastionAbsolute`), assaillants à 15 m (`bastionAbsoluteTaunt`) ; la garde sur soi sans rayon |
| Riposte précise | attaquant à 2,5 m |
| Jugement du colosse | frappe dans un rayon de 5 m (le gain de Résonance suit la règle du premier effet : sans ennemi dans le rayon, la réaction est annulée) |
| Serment du gardien | assaillant à 15 m du Bastion |
| Lien protecteur (`protectiveLink`, règles) | 15 m |

## Altérations (sous-lot 7.2)

`data.statuses` est une liste **ordonnée** de définitions (au plus 64) : l'ordre fixe la priorité des motifs de refus (la première altération bloquante l'emporte) et entre dans l'empreinte. Champs obligatoires : `id`, `category` (`dot`, `control`, `modifier`, `immunity` ; `buff` et `shield` depuis le sous-lot 8.2, voir ci-dessous), `durationMs`, `stacking` (`refresh`, `perSourceKeepStronger`, `keepLonger`, `ignore`), `resistance` (`none`, `defense`, `duration`). Champs facultatifs, à valeur neutre s'ils sont absents : `periodMs` et `damageType` (DOT), `blocks` (`skills`, `guard`, `parry`, `regeneration`, `assailantAttacks` ; `movement` depuis le sous-lot 11.4a), `interrupts` (`guard`, `parry`), `blockReason`, `damageTakenIncrease` (0 à 10), `movementSpeedModifier` (−1 à 1), `grantsOnExpiry`, `immuneTo`, `triggers` (lot 7.3), `modifiers` (sous-lot 8.2, renforcements seulement).

```json
{ "id": "stun", "category": "control", "durationMs": 1500, "stacking": "ignore", "resistance": "duration",
  "blocks": ["skills", "guard", "parry", "assailantAttacks", "movement"], "interrupts": ["guard", "parry"],
  "blockReason": "stunned", "grantsOnExpiry": "stunImmunity" }
```

Correctif 7.2 (contenu `0.4.1-provisoire`) : l'étourdissement passe de `keepLonger` à `ignore` ; un étourdissement actif refuse toute nouvelle application, ce qui garantit son expiration et l'immunité qui la suit.

Cohérence vérifiée par `StatusCatalog.Validate` (seule validation de gameplay), erreurs rattachées à `$.data.statuses[i].champ` : identifiants uniques ; durée positive, multiple du tick, au plus 24 h ; un DOT a une période multiple du tick qui divise sa durée, un type de dégâts, le cumul par source et la résistance `none` ou `defense`, et lui seul a période, type de dégâts, cumul par source ou résistance `defense` ; seul un contrôle a la résistance `duration` ; une altération qui bloque a un `blockReason` (et réciproquement) ; seule une immunité a `immuneTo`, qui désigne au moins une altération existante, sans doublon ni immunité ; un état accordé existe, n'est ni un DOT ni l'altération elle-même et n'accorde rien à son tour (profondeur 1). `combat.guard.breakStatus` désigne une altération existante, ni DOT ni immunité.

Applications (`applies`, dans le bloc `combat` d'une compétence et dans chaque attaque d'assaillant ; obligatoire, éventuellement vide) : `{"status": "bleed", "coefficient": 0.5}`. Sous-lot 8.3 : `target` facultatif (`self`, `allAllies`, `allEnemies`, relatifs à l'acteur) — l'application est alors faite une fois, après les impacts, à ces cibles, au lieu de chaque cible de l'action. Le coefficient est obligatoire pour un DOT et pour un bouclier (sous-lot 8.2 : fraction des PV max de la source), interdit sinon, dans [0, 100] ; une altération citée deux fois, inconnue, une immunité ou l'altération de rupture sont refusées. `stats.statusResistanceConstant` (K, 1000) est dans ]0, 10⁹] ; `statusResistance` d'un mannequin ou d'un assaillant (facultatif, 0 par défaut) dans [0, 10⁹].

## Déclencheurs (sous-lot 7.3)

Un déclencheur est une donnée, jamais du code : `triggers` est une liste facultative (au plus 8) sur un **talent** ou une **compétence** de classe (passive, active ou ultime ; seuls les talents de rang ≥ 1 et les compétences équipées sont actifs), sur une **altération** du catalogue (`statuses[].triggers`, actif tant que l'instance existe) et sur un **assaillant** de scénario (Q4, pour les tests et les entraînements). L'équipement est réservé (Q9), non implémenté. Depuis le sous-lot 8.5, des talents et un passif du Bastion livré en portent (voir « Bastion (sous-lot 8.5) ») ; jusque-là, aucune donnée livrée n'en portait (Q14).

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
- Valeurs par rang : un nombre, ou `{ "base": x, "perRank": y }` pour un talent à plusieurs rangs seulement, borné à chaque rang possible. Décision du 18/10/2026 : un perRank non nul sur un talent à un seul rang est refusé (il serait sans effet), comme pour les modificateurs.

Validation unique (`TriggerDefinition.ValidateList` et `ValidateReferences`, appelées par les lecteurs de règles, de classes et de scénarios et par `CombatSetup.Validate`), erreurs localisées à `…triggers[i].champ`. Un **cycle statique** possible (un déclencheur dont les effets peuvent produire l'événement qu'il observe, directement ou par une chaîne de déclencheurs) est un **avertissement** du rapport, jamais une erreur (Q1) : la règle de cycle et la profondeur garantissent la terminaison. `combat.limits.maxTriggerDepth` (4, de 0 à 8) borne la profondeur. Comportement au combat : [MOTEUR.md](MOTEUR.md#déclencheurs-sous-lot-73).

### Renforcements et boucliers (sous-lot 8.2)

Deux catégories s'ajoutent au catalogue (schéma de règles 7, puis 8 au sous-lot 8.3 ; un fichier antérieur est refusé sans migration), sans aucune définition livrée au sous-lot 8.2 (le bouclier et les renforcements du Bastion le sont au sous-lot 8.4, voir plus bas).

- **Renforcement** (`category: "buff"`) : porte `modifiers`, liste obligatoire (1 à 8) de modificateurs actifs tant que l'instance existe sur son porteur. Clés admises : `damageDealt` (catégorie de bonus « altération », comme la Fragilité), `damageTaken` (réduction, valeur négative, sous le plafond total `combat.damageTaken.maxReduction`), `guard.upkeepPerSecond` et, depuis le sous-lot 8.4 (schéma de règles 9), `guard.noUpkeep` (drapeau, `add` = 1 : garde sans entretien tant que le renforcement dure). Chaque élément a `key` et `add` (un nombre, ou `{ "base": x }` : `perRank` non nul refusé, une altération n'a pas de rang), sans `conditions` (refusées : le modificateur vaut tant que le renforcement est actif). Mêmes contrôles de bornes, de doublon et de valeur nulle que pour un passif. Cumul : `refresh`, `keepLonger` ou `ignore` (le cumul par source est refusé : une instance au plus par définition et par porteur).
- **Bouclier** (`category: "shield"`) : cumul `perSourceKeepStronger` obligatoire (un bouclier par source, renouvelé à la plus forte valeur), résistance `none`, sans `modifiers`. Sa valeur est fixée à l'application : ⌊ PV max de la source × coefficient × (1 + `shield.potencyDealt` de la source) ⌋. Le coefficient d'application est donc obligatoire (`applies[].coefficient` ou `coefficient` d'un effet `applyStatus` de déclencheur) ; une source sans PV max (mannequin, assaillant) pose un bouclier vide, refusé au combat (`stateBlocked`, `empty`). Un bouclier ne peut pas être accordé à l'expiration d'une autre altération (`grantsOnExpiry`) : aucune application ne fixerait sa valeur.
- Les autres catégories n'ont pas de `modifiers` (une liste vide équivaut à une liste absente).

```json
{ "id": "fortifie", "category": "buff", "durationMs": 8000, "stacking": "refresh", "resistance": "none",
  "modifiers": [ { "key": "damageTaken", "add": -0.3 } ] },
{ "id": "egide", "category": "shield", "durationMs": 4000, "stacking": "perSourceKeepStronger", "resistance": "none" }
```

Erreurs rattachées à `$.data.statuses[i].modifiers` (renforcement sans modificateur, modificateurs hors renforcement), `…modifiers[k].key` (clé inconnue ou non admise, doublon), `…modifiers[k].add` (bornes, `perRank`, valeur nulle, `damageTaken` positif), `…modifiers[k].conditions` et `$.data.statuses[i].stacking` (bouclier sans cumul par source, renforcement avec). Forme canonique : après les déclencheurs de l'altération, `modifiers.count` puis chaque modificateur comme pour une classe ; rien sans modificateur. Le schéma JSON (`rules.schema.json`) reprend les clés admises et l'absence de conditions ; `Lot82StatusDataTests` vérifie qu'il décrit exactement les champs lus, les catégories du domaine et les clés admises.

### Provocations et protections (sous-lot 8.3)

Deux catégories de plus (schéma de règles 8), aucune livrée au sous-lot 8.3 (provocations du Bastion livrées au sous-lot 8.4) : `taunt` (provocation d'un assaillant) et `protection` (transfert d'une part des dégâts d'un allié à son protecteur). Toutes deux se cumulent par source (`perSourceKeepStronger` obligatoire), ne sont jamais accordées à l'expiration d'une autre altération (elles dépendent d'une source vivante) et sont retirées à la mort de leur source. Une provocation a la résistance `none` ou `duration` (sa durée se réduit comme celle d'un contrôle). Une protection exige `redirectRatio` dans ]0, 1] (part des dégâts transférée ; champ refusé sur toute autre catégorie) et a la résistance `none`.

```json
{ "id": "provocation", "category": "taunt", "durationMs": 3000, "stacking": "perSourceKeepStronger", "resistance": "none" },
{ "id": "lienProtecteur", "category": "protection", "durationMs": 6000, "stacking": "perSourceKeepStronger", "resistance": "none", "redirectRatio": 0.25 }
```

Forme canonique : `redirectRatio` écrit pour chaque altération (0 hors protection), après ses modificateurs. Comportement au combat : [MOTEUR.md](MOTEUR.md#provocation-protection-et-rôle-allié-sous-lot-83).

### Bastion (sous-lot 8.4)

Les fiches complètes des compétences du Bastion (feuille de route) sont livrées : contenu des règles 0.7.0 (schéma 9) et de la classe 0.3.0 (schéma 8), toujours provisoires. Altérations ajoutées à la fin du catalogue :

| Identifiant | Catégorie | Durée | Utilisée par |
|---|---|---|---|
| `taunt` | provocation | 3 s | Provocation |
| `steelBarrier` | bouclier (cumul par source) | 6 s | Barrière d'acier (coefficient 0,15 : 15 % des PV max du lanceur) |
| `bastionAbsolute` | renforcement, `damageTaken` −0,3 | 8 s | Bastion absolu, sur le Bastion et ses alliés |
| `bastionAbsoluteGuard` | renforcement, `guard.noUpkeep` | 8 s | Bastion absolu, sur le Bastion |
| `bastionAbsoluteTaunt` | provocation | 8 s | Bastion absolu, sur tous les ennemis (les assaillants seulement l'acceptent) |

Champs facultatifs du bloc `combat` d'une compétence (classes, schéma 8) :

- `rampartChargeSpendAll` (booléen) : vrai, le lancement dépense toutes les Charges détenues, au moins `rampartChargeCost` (exigées) ; absent ou faux, exactement `rampartChargeCost`.
- `coefficientPerRampartCharge` (décimal dans [0, 100]) : ajouté au coefficient par Charge dépensée (Contrecoup : `coefficient` 0,6, `rampartChargeCost` 1, `rampartChargeSpendAll` vrai, 0,4). Refusé sans dégâts directs ou sans dépense de Charges possible (sans effet).
- `resonanceGain` (entier ≥ 0) : Résonance gagnée au lancement (Provocation : 5), refusée sur une ultime (elle dépense la Résonance).

Règles (schéma 9) : `combat.triggers.areaStrikeMinCooldownMs` (obligatoire, ms, multiple du tick, [0, 24 h], 3 000 livrés) : recharge interne minimale d'un déclencheur dont un effet `strike` vise `allEnemies` (L8-D2) ; un déclencheur en dessous est refusé à la validation du scénario, sur son `cooldownMs`. `combat.guard.breakStatus` doit désigner un contrôle ou un modificateur (revue Codex de la PR #7).

### Bastion (sous-lot 8.5)

Contenu complet du Bastion : règles au contenu 0.8.0 et classe au contenu 0.4.0, **schémas inchangés** (9 et 8 : aucun champ nouveau, seulement des modificateurs et des déclencheurs des sous-lots 7.3 à 8.4). Plus aucun talent ni passif `[TEST]` : les paliers et passifs d'essai sont retirés, remplacés par les neuf intermédiaires de cinq rangs et les huit passifs du catalogue (effets : [BUILDS.md](BUILDS.md#arbres-du-bastion-provisoires-sous-lot-85)). Altérations ajoutées à la fin du catalogue :

| Identifiant | Catégorie | Durée | Utilisée par |
|---|---|---|---|
| `reactiveAegis` | bouclier (cumul par source) | 4 s | Égide réactive (coefficient 0,03 par rang, sur le porteur) |
| `protectiveLink` | protection, `redirectRatio` 0,25 | 6 s (celle de la Barrière d'acier) | Lien protecteur, sur l'allié qui reçoit la Barrière |
| `oathTaunt` | provocation | 2 s | Serment du gardien, sur l'assaillant qui frappe un allié |

Forme des effets (extraits de `bastion.json`) :

```json
{ "id": "talent.bastion.rempart.echo_defensif", "triggers": [
    { "on": "onBlock", "role": "target", "effects": [ { "type": "gainResource", "resource": "endurance", "amount": 4 } ] },
    { "on": "onBlock", "role": "target", "cooldownMs": 3000, "effects": [ { "type": "gainResource", "resource": "rampartCharges", "amount": 1 } ] } ] }
{ "id": "talent.bastion.gardien.egide_reactive", "triggers": [
    { "on": "onHitTaken", "filter": { "defense": ["guard", "parry"] }, "cooldownMs": 2000,
      "effects": [ { "type": "applyStatus", "target": "bearer", "status": "reactiveAegis", "coefficient": { "base": 0.03, "perRank": 0.03 } } ] } ] }
{ "id": "talent.bastion.gardien.lien_protecteur", "triggers": [
    { "on": "onSkillCast", "filter": { "skills": ["skill.bastion.barriere_d_acier"] },
      "effects": [ { "type": "applyStatus", "target": "eventTarget", "status": "protectiveLink" } ] } ] }
{ "id": "talent.bastion.rempart.poids_du_rempart",
  "modifiers": [ { "key": "damageTaken", "add": { "base": -0.005, "perRank": -0.005 }, "conditions": ["whileGuarding", "perRampartCharge"] } ] }
```

Riposte précise, Écho de guerre et Jugement du colosse réagissent à `onPerfectParry` (rôle `target` : le Bastion qui pare) ; Serment du gardien et Sentinelle à `onHitTaken` avec le rôle `ally` (un allié vivant frappé) ; Rempart éternel, Fureur contenue, Peau de fer, Souffle du roc, Main lourde, Élan, Vigilance, Présence, Rempart vivant et sept passifs ne portent que des modificateurs. La classe livrée a un avertissement de cycle statique (Riposte précise et Jugement du colosse : face à un autre Bastion qui pare, une frappe de réaction pourrait être parée à son tour) ; c'est un avertissement, jamais une erreur (Q1). Empreintes : règles `sha256:03f73faca72b574c972b117e29235f072528b6a5929b268c01842ec9fca1f83a`, classe `sha256:4a719f82fa129497a4a4cd84152546998a6c919fab8a43d7817cfa7f253313a3`.

Les tests écrits avant le 8.5 qui modifient la classe (forme, empreintes, plafonds) ou mesurent un combat sans déclencheur s'appuient sur le contenu figé du 8.4 (`tests/EdS.Data.Tests/Fixtures/content-0.3.0` : classe et scénarios) ; le contenu livré a ses propres tests (`Lot85ContentDataTests`, `Lot85TalentCombatTests`).

## Builds de référence (sous-lot 8.5)

Un fichier par classe (`data/v0/builds/<classe>.json`, `kind: "builds"`, schéma 1, `data/schemas/builds.schema.json`), même en-tête que les autres fichiers de données. Décision A12 : les builds de comparaison sont des données, pas du code de test.

```json
{
  "schemaVersion": 1, "kind": "builds", "contentVersion": "0.1.0-provisoire",
  "data": { "class": "class.bastion", "builds": [
    { "id": "build.bastion.rempart", "name": "Rempart", "category": "reference",
      "build": { "level": 30, "attributes": { "force": 30, "dexterite": 10, "intelligence": 6, "vitalite": 30, "volonte": 15, "endurance": 20 },
                 "talents": { "talent.bastion.rempart.garde_stable": 3, … }, "activeSkills": [ … ], "ultimate": "skill.bastion.bastion_absolu",
                 "passiveSkills": [ … ] } } ] }
}
```

- `class` (obligatoire) : une classe chargée du même dossier.
- `builds` (obligatoire) : 1 à 64 builds ; chacun a `id` (identifiant stable `build.<classe>.<nom>`, de la classe du fichier, unique dans toutes les données), `name` (non vide), `category` (`reference` : build de spécialisation comparé ; `control` : build de contrôle) et `build`, de la même forme que le build d'un personnage de scénario (contrôle de forme partagé, `BuildShape`).
- Validation : `BuildSetJsonReader` (forme) → `BuildSetDocument` → `BuildSetValidator` (en-tête, présence, identifiants, catégories, forme du build) → `BuildValidator` (règles du build, contre la classe et la progression des règles). Une violation du build est rattachée au champ fautif d'après l'élément qu'elle désigne (revue Codex de la PR #9) : `$.data.builds[i].build.talents.<id>` pour un talent (inconnu, rang, prérequis, seuil du majeur), `.talents` pour le total des points ou des majeurs, `.activeSkills[n]`, `.passiveSkills[n]` ou `.ultimate` pour un emplacement, `.activeSkills` ou `.passiveSkills` pour leur nombre, `.level`, `.attributes.<attribut>` ; le code du validateur est dans le message (`UnknownReference` pour un talent ou une compétence inconnus, `InvalidValue` sinon). Dans un scénario, une violation du build reste rattachée à `combatants[i].build` (`CombatSetup.Validate`), comme depuis le lot 5.
- Chargement : `GameDataLoader.Load` lit `builds/*.json` après les classes (ordre des noms de fichiers) ; un fichier refusé, ou un identifiant déjà défini par un autre fichier (`DuplicateId`), fait échouer le chargement comme une classe refusée. `GameData.BuildSets` et `GameData.TryGetBuild` les exposent. Les builds n'entrent **pas** dans l'identité des données d'un combat : un scénario recopie le build de chaque personnage, et le journal reste inchangé.
- Pas d'empreinte pour ce fichier : il n'est lu par aucun combat (une référence aux builds depuis un scénario pourra venir avec les scénarios S1 à S5, sous-lot 8.6).

Contenu livré et choix des points : [BUILDS.md](BUILDS.md#builds-de-référence-sous-lot-85-datav0buildsbastionjson).

## Scénarios de mesure et rapport (sous-lot 8.6)

Les scénarios de mesure sont des scénarios ordinaires (`kind: "scenario"`, schéma 3, puis 4 depuis le sous-lot 11.1) rangés dans `scenarios/mesures` et nommés `s<n>_<nom>.json` (identifiant « S<n> ») ; un autre nom est ignoré par `eds report`, et deux fichiers au même numéro (`s1_a.json`, `s1_b.json`) sont refusés. Chacun a un personnage `bastion` (son build du fichier n'est qu'une valeur par défaut, celui du Rempart, remplacée à chaque mesure) et aucune commande (le pilote les ajoute). Contenu : [BUILDS.md](BUILDS.md#mesures-des-builds-sous-lot-86). Aucun format de scénario n'est ajouté : un scénario ne référence toujours pas un build par son identifiant.

```bash
dotnet run --project src/EdS.Cli -- report                                   # S1 à S5 × 5 builds × 3 profils, texte
dotnet run --project src/EdS.Cli -- report --profile standard --build build.bastion.rempart --build build.bastion.contrecoup
dotnet run --project src/EdS.Cli -- report --json > rapport.json            # rapport JSON reproductible octet par octet
dotnet run --project src/EdS.Cli -- report --logs journaux --strict          # journaux rejouables ; code 3 si un objectif échoue
```

Options : dossier (défaut `scenarios/mesures`), `--data` (défaut `data/v0`), `--build` et `--profile` répétables (défaut : tous les builds de la classe du personnage `bastion`, dans l'ordre du fichier ; `beginner`, `standard`, `expert`), `--json`, `--logs <dossier>` (journal de chaque combat, `<scénario>__<build>__<profil>.jsonl`, rejouable avec `eds replay`), `--strict`. Rapport texte aux fins de ligne `\n` sur toutes les plateformes. Codes de sortie : 0, 1 (usage, données ou scénario refusés, numéro de scénario en double), 2 (un combat interrompu par une limite du moteur : mesures de diagnostic), 3 (avec `--strict`, au moins un objectif non atteint).

Rapport JSON (`kind: "measureReport"`, `formatVersion` 1) : `engineVersion`, `data` (contenu et empreinte des règles, de chaque classe), `objectiveProfile`, `scenarios` (`id`, `name`, `durationMs`, `seed`), `builds`, `results` (par combat : `scenario`, `build`, `profile`, `durationMs`, `aborted`, `end`, `damageDealt`, `dps`, `healthLost`, `maxHealth`, `avoided`, `mitigated`, `absorbed`, `transferred`, `redirected`, `died`, `survivalSeconds`, `allyDied` et `allySurvivalSeconds` — `null` sans allié —, `blocks`, `perfectParries`, `guardBreaks`, `skillsCast`, `ultimates`, `commandsRejected`, `peaksPercent` `work`, `events`, `queue`) et `objectives` (`id`, `description`, `passed` — `null` si non mesuré —, `detail`). Propriétés dans un ordre fixe, nombres décimaux invariants, fin de ligne finale.

## Modificateurs (sous-lot 8.1)

Un modificateur change une règle de combat ou une statistique **du porteur** : `modifiers` est une liste facultative (au plus 8) sur un **talent** (actif dès le rang 1, valeur au rang du build) ou une **compétence passive** équipée (rang 1, sans `perRank`). Une compétence active ou ultime n'en porte pas (refus). Au plus 512 modificateurs par classe, vérifiés avec les autres plafonds globaux. Depuis le sous-lot 8.5, les talents et passifs du Bastion livré en portent (« Bastion (sous-lot 8.5) »).

```json
"modifiers": [
  { "key": "guard.damageReduction", "add": { "base": 0.02, "perRank": 0.02 } },
  { "key": "damageTaken", "add": -0.02, "conditions": ["whileGuarding", "perRampartCharge"] },
  { "key": "skill.bastion.frappe_du_rempart.cooldownMs", "add": -500 }
]
```

- `key` (obligatoire), liste fermée :

| Clé | Nature | Valeur déclarée (rang 1 et rang max) | Valeur résultante |
|---|---|---|---|
| `guard.damageReduction` | fraction | ±1 (classe) ; ±plafond des règles (0,75), vérifié au combat | [0 ; `combat.guard.maxDamageReduction`] |
| `guard.upkeepPerSecond` | décimal par seconde | ±10⁶ | [0 ; 2³¹ − 1] |
| `guard.costPerBlock` | entier | ±10⁶ | [0 ; 2³¹ − 1] |
| `guard.noUpkeepBreak` | drapeau, `add` = 1 exactement | — | — |
| `guard.reductionAtZeroEndurance` | fraction | ±1 (classe) ; ±plafond des règles (0,75), vérifié au combat | [0 ; réduction de garde effective] |
| `guard.breakShortenPerCharge` | ms | ±86 400 000 | [0 ; 24 h] |
| `parry.windowMs` | ms | ±500 | [1 tick ; ⌊500 / tick⌋ × tick] |
| `parry.failureRecoveryMs` | ms | ±86 400 000 | [0 ; 24 h] |
| `rampartCharges.max` | entier | ±10 | [0 ; 10] |
| `resonance.perDirectHit` | entier | ±10⁶ | [0 ; 2³¹ − 1] |
| `endurance.max` | entier, plat | ±10⁶ | ≥ 0 |
| `endurance.regenPerSecond` | décimal par seconde | ±10⁶ | [0 ; 2³¹ − 1] |
| `maxHealth` | fraction du PV max calculé | ±10 | pourcentage résultant [−0,9 ; 10] |
| `statusResistance` | décimal, plat | ±10⁶ | ≥ 0 |
| `damageDealt` | fraction, conditionnelle | ±10 | catégorie de bonus « talent » |
| `damageTaken` | fraction ≤ 0, conditionnelle | [−1 ; 0] | [−1 ; 0] |
| `status.<id>.durationDealt` | ms | ±86 400 000 | durée [1 tick ; 24 h] |
| `status.<id>.durationTaken` | ms | ±86 400 000 | durée [1 tick ; 24 h] |
| `<id de compétence>.coefficient` | fraction | ±100 | [0 ; 100] |
| `<id de compétence>.cooldownMs` | ms | ±86 400 000 | [0 ; 24 h] |
| `shield.potencyDealt` | fraction (sous-lot 8.2) | ±10 | ≥ −1 en effet : facteur (1 + puissance) ramené à 0 au plus bas |
| `protection.redirectRatio` | fraction (sous-lot 8.3) | ±1 | somme dans [−1, 1] ; part effective d'une protection posée (définition + somme) dans [0, 1] |
| `protection.redirectThroughGuard` | drapeau, `add` = 1 exactement (sous-lot 8.3) | — | les dégâts transférés au porteur passent par sa garde (réduction seulement) quand il garde |
| `guard.noUpkeep` | drapeau, `add` = 1 exactement (sous-lot 8.4) | — | garde sans entretien : coût par seconde effectif nul (permanent, ou tant qu'un renforcement qui le porte est actif) |

  `<id>` est un identifiant d'altération du catalogue (`status.stun.durationTaken`) ; une clé de compétence est l'identifiant complet d'une compétence de la classe qui se lance (bloc `combat`), suivi du suffixe (`skill.bastion.frappe_du_rempart.coefficient`). `durationDealt` est refusé pour une immunité ou l'altération de rupture de garde (jamais appliquées directement).
- `add` (obligatoire) : un nombre, ou `{ "base": x, "perRank": y }` pour un talent à plusieurs rangs (valeur au rang r : base + (r − 1) × perRank), bornée au rang 1 et au rang maximal. Une valeur nulle à chaque rang (sans effet) est refusée, comme un perRank non nul sur un talent à un seul rang. Entiers et ms : base et perRank entiers ; ms : multiples du tick (et de la période pour une altération à dégâts périodiques), vérifiés au combat. Pour une règle à plafond absolu, la borne d'une valeur déclarée est la largeur de ce plafond : un modificateur seul qui le dépasse est une erreur de données ; seules les combinaisons de plusieurs sources sont **écrêtées** (jamais refusées).
- `conditions` (facultatif, X2), seulement sur `damageDealt` et `damageTaken` : `whileGuarding` (porteur en garde), `perRampartCharge` (× Charges détenues), `perResonanceStep` (× ⌊Résonance / 10⌋) ; combinables, mais au plus une condition multiplicative (`perRampartCharge` et `perResonanceStep` ensemble refusés). Liste vide = absente.
- Une même clé avec le même ensemble de conditions deux fois chez un même porteur : `DuplicateId`. La même clé sur plusieurs porteurs s'additionne.

Validation : forme (`ModifierShape`, lecteur JSON), puis `ModifierDefinition.ValidateList` dans `ClassDefinition.Validate` (clés, conditions, doublons, bornes, entiers, drapeau, compétence référencée), puis `ModifierDefinition.ValidateReferences` dans `CombatSetup.Validate` (altération du catalogue, multiples du tick et de la période). Au plus une erreur par valeur. Erreurs de la classe localisées à `…modifiers` (porteur non admis, plus de 8 modificateurs), `…modifiers[i]` (élément qui n'est pas un objet), `…modifiers[i].<champ>` (champ inconnu), `…modifiers[i].key`, `.add`, `.add.base`, `.add.perRank` ou `.add.<champ>` (forme de la valeur : champ absent, de type faux ou inconnu), `.conditions` ou `.conditions[k]` (condition inconnue, répétée ou de type faux), et à `skills` pour le plafond de 512 par classe ; au combat, les erreurs de références sont rattachées à `combatants[i].class`, chemin interne dans le message, comme pour les déclencheurs (voir [MOTEUR.md](MOTEUR.md#modificateurs-sous-lot-81)). Forme canonique : après les déclencheurs du talent ou de la compétence, `modifiers.count`, puis pour chacun `key`, `add` (`base` ou `base+perRank/rank`) et la liste `conditions`, écrite dans l'ordre fixe `whileGuarding`, `perRampartCharge`, `perResonanceStep` quel que soit l'ordre déclaré (un ensemble : même empreinte) ; l'ordre des modificateurs, lui, fait partie du contenu. Une liste vide n'écrit rien (même empreinte qu'une liste absente). Le schéma JSON reprend les règles exprimables sans les règles du jeu (passive seulement et sans perRank, conditions de `damageDealt` et `damageTaken`, une seule multiplicative, drapeau à 1, `damageTaken` négatif, jamais nul) et n'est jamais plus strict que le programme : `Lot81SchemaConformanceTests` le vérifie sur plus de 13 000 fichiers dérivés de bastion.json (chaque clé, valeur, porteur dont le talent majeur, ensemble de conditions et liste type). Comportement au combat : [MOTEUR.md](MOTEUR.md#modificateurs-sous-lot-81).

## Architecture

```
rules.json ──► EdS.Data.Json (analyseur JSON strict maison, netstandard2.1 : outils .NET, Unity, serveur)
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

- `EdS.Data` (netstandard2.1, aucune dépendance) : modèle `RulesDocument`, catalogue `RulesCatalog` (unique description du format), validateur commun, empreinte.
- `EdS.Data.Json` (netstandard2.1, C# 9, aucune dépendance depuis le sous-lot 9.1) : lecture de la forme, sans réflexion, sur un analyseur JSON strict maison (`EdS.Data.Json.Text`) qui remplace System.Text.Json : RFC 8259 sans extension (ni commentaire, ni virgule finale, ni NaN, ni marque d'ordre dans un texte ; un fichier lu par `JsonInput.ReadFile` est débarrassé de la sienne), profondeur bornée (`InputLimits.MaxJsonDepth`), nombre de valeurs borné pendant l'analyse (`InputLimits.MaxJsonValues`, voir les limites des lecteurs ci-dessous), arbre compact (aucun objet alloué par valeur, tableaux à la taille exacte), UTF-8 valide, texte brut des nombres conservé, propriétés et doublons dans l'ordre du texte, ligne de chaque nom de propriété. Il est comparé à System.Text.Json sur tous les fichiers livrés et figés, sur des valeurs aléatoires et sur des textes altérés (`Lot91JsonTextTests`) : même arbre, jamais plus laxiste ; il refuse en plus les paires de substitution UTF-16 incomplètes (`\ud83d` seul), que System.Text.Json admet à l'analyse. Les messages d'erreur de syntaxe (`InvalidJson`) sont en français ; codes, chemins et lignes inchangés. Unity et le futur serveur lisent donc les données exactement comme la CLI : même validation, mêmes empreintes.
- `EdS.Tools` (net10.0) : outils réservés à .NET, aujourd'hui les mesures des builds (`EdS.Tools.Measures`, sous-lot 8.6, rapport JSON écrit avec System.Text.Json).

## Empreinte

`sha256:…` calculée sur une écriture canonique (version du contrat canonique, version de schéma, puis chaque paramètre dans l'ordre du catalogue). Indépendante des espaces, de l'ordre des clés et de l'écriture des nombres (`2` = `2.0`) ; ignore `contentVersion`. Valeur actuelle de `data/v0/rules.json` :

```
sha256:3b09a16910f7bf926bd122f0a594f28138d593a3035378b21f34c8ec0e2ffb97
```

**Contrat canonique 2 (correctif 7.3.1, A73-M02).** Une ligne par champ : `chemin=valeur` pour une valeur présente (barre oblique inverse, retour à la ligne et retour chariot échappés), `chemin?` pour un champ facultatif absent, et pour une liste `chemin.count=n` puis `chemin[i]=élément`. Aucune valeur textuelle ne sert plus de sentinelle : au contrat 1 (lots 1 à 7.3), un champ absent s'écrivait `none` (ou une chaîne vide dans les classes) et une liste était jointe par des virgules, si bien qu'un `filter.status` ou un `grantsOnExpiry` absent avait la même empreinte que la valeur `none`, identifiant d'altération valide (deux collisions démontrées par l'audit). Le texte haché commence par `eds-rules` ou `eds-class` puis `canonical=2`. Écrit par `CanonicalWriter` (`EdS.Data`) pour les règles, les altérations, les déclencheurs et les classes. **Compatibilité** : toutes les empreintes changent ; un journal antérieur (moteur 0.9.0, contrat 1) est refusé au rejeu avant toute simulation (moteur et empreintes différents), sans migration ; les quatre journaux de référence 0.9.0, figés dans `tests/EdS.Data.Tests/Fixtures/logs-0.9.0`, ne diffèrent des actuels que par `engineVersion` et les empreintes de l'en-tête.

(Elle a changé au correctif 6.1 avec la version de schéma 2, au lot 7.1 avec la version 3 et les cinq nouvelles limites, puis au lot 7.2 avec la version 4, la constante de résistance, `breakStatus` et les altérations, écrites dans l'ordre du fichier avec chaque champ facultatif à sa valeur neutre ; au correctif 7.2, l’étourdissement passant en `ignore` ; au lot 7.3 avec la version 5 et `combat.limits.maxTriggerDepth` ; au correctif 7.3.1 avec le contrat canonique 2 ; au lot 8 avec la version 6 et les plafonds `combat.guard.maxDamageReduction` et `combat.damageTaken.maxReduction` ; aux sous-lots 8.2 et 8.3 avec les versions 7 et 8 ; au sous-lot 8.4 avec la version 9, `combat.triggers.areaStrikeMinCooldownMs` et les cinq altérations du Bastion ; au sous-lot 8.5 avec les trois altérations des talents du Gardien, schéma inchangé ; enfin au sous-lot 11.4a avec la version 10, `combat.movement`, `combat.contact` et le blocage `movement` de l'étourdissement (contenu 0.9.0 ; empreinte précédente `sha256:03f73fac…`) ; puis au sous-lot 11.4b avec la version 11 et `statuses[].range` écrit pour chaque altération (absente : `range?` ; lien protecteur : 15), contenu 0.10.0 (empreinte précédente `sha256:c7b8d5af…`) ; puis au sous-lot 11.4c avec la version 12, `combat.guard.arcDegrees` et `combat.telegraph.minReactionMs`, contenu 0.11.0 (empreinte précédente `sha256:28f021f4…`). Une altération sans déclencheur n’ajoute rien au texte canonique.)

## Versions de format

| Fichier | `schemaVersion` | Historique |
|---|---|---|
| `rules.json` | 12 | 1 : lots 3 à 5 ; 2 : blocs `combat.guard`, `parry`, `rampartCharges`, `resonance`, `states` du lot 6 ; 3 : `combat.limits.maxCombatants`, `maxCommands`, `maxCommandsPerActorPerTick`, `maxPendingEvents`, `maxWorkPerTick` (lot 7.1) ; 4 : `statuses`, `stats.statusResistanceConstant`, `combat.guard.breakStatus` à la place de `breakDurationMs`, `combat.states` supprimé (lot 7.2) ; 5 : `combat.limits.maxTriggerDepth`, `statuses[].triggers` facultatif (lot 7.3) ; 6 : `combat.guard.maxDamageReduction` (0,75) et `combat.damageTaken.maxReduction` (0,85), plafonds de réduction auparavant constantes du moteur (lot 8, décision du 18/10/2026) ; 7 : catégories `buff` et `shield` et `statuses[].modifiers` (sous-lot 8.2) ; 8 : catégories `taunt` et `protection`, `statuses[].redirectRatio`, rôle `ally` des déclencheurs d'altération (sous-lot 8.3) ; 9 : `combat.triggers.areaStrikeMinCooldownMs` (L8-D2, 3 000 ms livrés), clé `guard.noUpkeep` admise sur un renforcement (sous-lot 8.4) ; 10 : `combat.movement.speed`, `combat.movement.guardSpeedFactor`, `combat.contact.playerCreature`, `combat.contact.crossingSpeedFactor`, `combat.contact.largeCreatureRadius` (sous-lot 11.4a) ; 11 : `statuses[].range` facultatif d'une protection (sous-lot 11.4b) ; 12 : `combat.guard.arcDegrees` et `combat.telegraph.minReactionMs` (sous-lot 11.4c) |
| classes | 9 | 1 : lots 4 et 5 ; 2 : `unblockable`, `rampartChargeCost`, `rampartChargeGain` obligatoires dans le bloc `combat` ; 3 : `applies` obligatoire dans le bloc `combat` (lot 7.2) ; 4 : `triggers` facultatif sur les talents et les compétences (lot 7.3) ; 5 : `modifiers` facultatif sur les talents et les compétences passives (sous-lot 8.1) ; 6 : clé de modificateur `shield.potencyDealt` (sous-lot 8.2) ; 7 : ciblages `self`, `ally`, `allAllies`, compétences sans dégâts (`powerType`, `damageType`, `coefficient` tous absents), `applies[].target`, rôle `ally` des déclencheurs, clés `protection.redirectRatio` et `protection.redirectThroughGuard` (sous-lot 8.3) ; 8 : `rampartChargeSpendAll`, `coefficientPerRampartCharge` et `resonanceGain` facultatifs dans le bloc `combat`, clé `guard.noUpkeep` (sous-lot 8.4) ; 9 : `combat.range`, `combat.applies[].range` et `triggers[].effects[].range` facultatifs (sous-lot 11.4b) |
| builds de référence | 1 | 1 : `data.class`, `data.builds[]` (`id`, `name`, `category`, `build`) (sous-lot 8.5) |
| scénarios | 8 | 1 : lots 5 et 6 (assaillant, `action`) ; 2 : `applies` à la place de `appliesFragility`, `statusResistance` facultatif des mannequins et assaillants (lot 7.2) ; 3 : `applies[].target` des attaques d'assaillant, rôle `ally` de leurs déclencheurs, ciblage d'attaque limité à `enemy` et `allEnemies` (sous-lot 8.3) ; 4 : `health` facultatif sur un assaillant, entier de 1 à 10⁹ (PV d'un assaillant mortel ; absent : PV infinis), refusé sur un personnage ou un mannequin (sous-lot 11.1) ; 5 : bloc `space` facultatif (`bounds`, `obstacles`) ; avec lui, `position` et `radius` obligatoires, `facing` facultatif, `speed` facultatif sur un assaillant ; actions `move` (avec `direction`) et `stop` (sous-lot 11.4a) ; 6 : forme, annonce, dimensions et défenses des attaques d'assaillant (`shape`, `telegraphMs`, `range`, `radius`, `angle`, `width`, `center`, `persistentMs`, `tickMs`, `guardable`, `parryable`), action `select` (sous-lot 11.4c) ; 7 : `arrivalMs` facultatif sur un combattant, action `leave` (sous-lot 11.4d) ; 8 : `behavior` facultatif sur un assaillant, `charge` facultatif sur une attaque (sous-lot 11.5) |
| rapport de mesures (`formatVersion`) | 1 | 1 : `kind: "measureReport"`, résultats par combat et objectifs (sous-lot 8.6, `eds report --json`) |
| journal (`formatVersion`) | 14 | 2 : `instance` et `generation` sur les états, `stateRemoved` ; 3 : `category` et `source` sur les états, `applies`, `stateBlocked`, `dotDamage` (lot 7.2) ; 4 : provenance des effets, `eid`, `cause`, `root`, `depth`, `trigger` (lot 7.3, voir MOTEUR.md) ; 5 : `statusResistance` et `rules` (règles effectives) sur `combatant`, `rampartChargesSpent` sur `guardBroken` (sous-lot 8.1) ; 6 : `absorbed` et `absorbedBy` sur `damage` et `dotDamage`, `absorb` sur le `stateApplied` d'un bouclier, raisons `depleted` (`stateRemoved`) et `empty` (`stateBlocked`), `damageAbsorbed` sur `combatantSummary`, `shield.potencyDealt` dans `rules` (sous-lot 8.2, voir MOTEUR.md) ; 7 : `transferred` sur `damage` et `dotDamage`, ligne `damageTransferred`, `redirectRatio` sur le `stateApplied` d'une protection, raisons `notApplicable` et `sourceDied`, `damageTransferred` et `damageRedirected` au bilan, clés `protection.*` dans `rules` (sous-lot 8.3) ; 8 : `guard.noUpkeep` dans `rules`, `rampartChargeCost` de `skillCast` = Charges réellement dépensées (sous-lot 8.4) ; 9 : `health` facultatif sur un assaillant de l'en-tête, ses PV dans sa ligne `combatant`, sa mort (sous-lot 11.1) ; 10 : combat positionné, `space` et places dans l'en-tête, `moveStarted`, `moveStopped`, `moved`, position et orientation sur `combatant`, position finale sur `combatantSummary` (sous-lot 11.4a) ; 11 : raisons `targetOutOfRange` et `noLineOfSight` (`commandRejected`) et `outOfRange` (`reactionCancelled`), portées d'un combat positionné (sous-lot 11.4b) ; 12 : formes des attaques dans l'en-tête et sur `attackStarted`, `attackLanded`, `attackCancelled`, `targetSelected`, défense `behind`, raisons `targetOutOfRange` et `noLineOfSight` de `attackSkipped`, commandes `select` (sous-lot 11.4c) ; 13 : `arrivalMs` des combattants et commandes `leave` dans l'en-tête, `combatantJoined`, `arrivalRefused`, `combatantLeft`, `leaveCancelled`, raisons `actorAbsent`, `targetAbsent`, `bearerAbsent`, `alreadyLeaving`, `left`, `sourceLeft`, présence au bilan (sous-lot 11.4d) ; 14 : `behavior` des assaillants et `charge` des attaques dans l'en-tête, `engaged`, `charged`, `chargeBlocked`, raisons `notEngaged` et `noTarget` (sous-lot 11.5) |
| instantané d'état (`snapshot`) | 2 | 1 : en-tête (versions, empreinte de l'en-tête du combat complété, tick, compteurs, file), une ligne par combattant, une par élément de la file ; entiers seulement, lignes JSON canoniques, empreinte `sha256:` (sous-lot 11.4d, [MOTEUR.md](MOTEUR.md#arrivées-départs-et-instantané-détat-sous-lot-114d)) ; 2 : `engaged`, `engagedTick`, `announcedToEnd`, `struck` et `taunted` obligatoires sur chaque combattant (engagement et son tick, annonce jusqu'à la fin du combat, coup reçu, provocation reçue ; sous-lot 11.5) |

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

Limites des lecteurs (sous-lot 7.1, constantes techniques) : fichier de données ou texte JSON au plus 16 Mio (`FileTooLarge`, vérifié avant lecture ou avant encodage), profondeur JSON au plus 32 (`InvalidJson`), document JSON d'au plus 1 048 576 valeurs (sous-lot 9.1, revue de la PR #12 : objets, tableaux, textes, nombres et littéraux, racine comprise ; `LimitExceeded` sur `$` avec la ligne, compté avant la création de chaque valeur : un texte valide de 16 Mio de « 0, » est refusé après 2 Mio lus, 32 Mio alloués au lieu de 700, alors que les fichiers livrés en ont moins de 1 000 et qu'un scénario de 20 000 commandes en a environ 120 000), journal rejoué au plus 256 Mio. Un rapport conserve au plus 200 erreurs ; les suivantes sont comptées et résumées par une dernière ligne « … et N autres erreurs ». Détail : [LIMITES_LOT_7.md](LIMITES_LOT_7.md#limites-des-lecteurs-en-place-sous-lot-71).

Codes : `FileUnreadable`, `DuplicateId`, `InvalidJson`, `WrongType`, `MissingField`, `UnknownField`, `DuplicateField`, `InvalidNumber`, `OutOfRange`, `UnknownReference`, `UnsupportedSchemaVersion`, `InvalidContentVersion`, `WrongKind`, `InvalidValue`, `LimitExceeded`, `FileTooLarge` (sous-lot 7.1). Toutes les erreurs sont rapportées en une passe, dans la limite de 200.

## Commande

```bash
dotnet run --project src/EdS.Cli -- validate                  # data/v0/rules.json
dotnet run --project src/EdS.Cli -- validate chemin/rules.json
dotnet run --project src/EdS.Cli -- validate data/v0/classes/bastion.json [--rules chemin/rules.json]
dotnet run --project src/EdS.Cli -- validate data/v0/builds/bastion.json   # sous-lot 8.5
```

Le champ `kind` choisit le validateur. Un fichier de classe est vérifié avec la progression du `rules.json` indiqué (défaut : `data/v0/rules.json`) ; un fichier de builds, avec les règles et les classes de `data/v0` (sans ses builds), et chaque build est résumé (points par arbre, majeur, emplacements).

## Fichiers de classe

Un fichier par classe (`data/v0/classes/<classe>.json`, `kind: "class"`), même en-tête que `rules.json`, schéma `data/schemas/class.schema.json`. Format et règles : [BUILDS.md](BUILDS.md). Même architecture : `ClassJsonReader` (forme) → `ClassDocument` → `ClassValidator` (en-tête, présence) → `ClassDefinition.Validate` (seule validation de gameplay). En-tête, empreinte et lecture JSON sont partagés (`HeaderValidator`, `ContentFingerprint`, `JsonInput`).

Empreinte d'une classe : SHA-256 de `eds-class`, `canonical=2`, puis chaque valeur (contrat canonique ci-dessus) dans l'ordre du fichier (l'ordre des arbres, talents et compétences fait partie du contenu ; les espaces et l'ordre des clés non ; `contentVersion` ignorée). Valeur actuelle de `bastion.json` :

```
sha256:5224f8119fd2f729d41bfc3bfc7ef9d5859f90f871b6ccf0f85ad32110690876
```

Sous-lot 11.4b : schéma 9, contenu 0.6.0 (portées) ; `combat.range` écrit après `combat.provisional`, `applies[].range` après `applies[].target`, `effects[].range` en fin d'effet (absents : `?`). Contenu 0.5.0 figé dans `tests/EdS.Data.Tests/Fixtures/content-0.5.0` (au schéma 9 : `sha256:1d46aafe…` ; `sha256:51f673a6…` au schéma 8) ; les contenus figés 0.2.0, 0.3.0 et 0.4.0 sont passés au schéma 9 sans autre changement (0.4.0 : `sha256:4d981022…`).

Sous-lot 8.7 : contenu 0.5.0, la Provocation porte `powerType`, `damageType` et `coefficient` (0,8) ; schéma 8 inchangé. Empreinte du contenu 0.4.0 (sous-lot 8.5, figé dans `tests/EdS.Data.Tests/Fixtures/content-0.4.0`) : `sha256:4a719f82fa129497a4a4cd84152546998a6c919fab8a43d7817cfa7f253313a3`.

(Sous-lot 8.1 : la ligne `schemaVersion=5` change l'empreinte ; le contenu de `bastion.json` est inchangé et ne porte aucun modificateur.) Sous-lot 8.3 : schéma 7 ; `applies[].target` écrit pour chaque application (absent : `target?`) et le coefficient d'une compétence de soutien écrit comme absent. Sous-lot 8.4 : schéma 8, contenu 0.3.0 (fiches complètes) ; `rampartChargeSpendAll`, `coefficientPerRampartCharge` et `resonanceGain` écrits après `rampartChargeGain` (absents : `?`). Sous-lot 8.5 : contenu 0.4.0 (talents, intermédiaires et passifs), schéma inchangé ; l'empreinte du contenu figé du 8.4 reste `sha256:6921f5e1…`.

Le bloc facultatif `combat` d'une compétence entre dans l'empreinte (`combat?` s'il est absent ; `combat=none` au contrat 1). Correctif 7.3.1 : une classe compte au plus 256 talents, 256 compétences et 512 déclencheurs, vérifiés avant tout autre contrôle et avant l'analyse statique des cycles (`LimitExceeded`). Depuis le lot 6, il porte aussi `unblockable`, `rampartChargeCost` et `rampartChargeGain`, et depuis le lot 7.2 `applies` (obligatoires dans le bloc).

## Scénarios et jeu de données (lots 5 et 6)

`GameDataLoader.Load("data/v0")` charge `rules.json`, tous les `classes/*.json` et, depuis le sous-lot 8.5, tous les `builds/*.json` en un `GameData` (règles, classes, builds de référence, identité : versions et empreintes des règles et des classes). `ScenarioJsonReader` (forme) → `ScenarioDocument` → `ScenarioValidator` (en-tête, références de classe, build validé par `BuildValidator`, puis `CombatSetup.Validate`) → `CombatSetup`. L'identité retenue pour le journal ne couvre que les règles et les classes utilisées par le scénario. Depuis le lot 6, un scénario peut contenir un assaillant (`type: "assailant"`) et des commandes défensives (`action`). Format, exemples et rejeu : [MOTEUR.md](MOTEUR.md).

**Combat positionné (schéma 5, sous-lot 11.4a).** Un scénario peut porter un bloc `space` ; alors chaque combattant a une place de départ, et seulement dans ce cas :

```json
"space": {
  "bounds": { "min": [-10, -6], "max": [10, 6] },
  "obstacles": [ { "id": "rocher", "points": [[-4.5, -0.2], [-3.5, -0.2], [-3.5, 2], [-4.5, 2]] } ]
},
"combatants": [ { "id": "bastion", "…": "…", "position": [-6, 0.5], "facing": [1, 0], "radius": 0.5 },
                { "id": "loup", "type": "assailant", "…": "…", "position": [8, 0.4], "radius": 0.6, "speed": 6 } ],
"commands": [ { "atMs": 0, "actor": "bastion", "action": "move", "direction": [1, 0] },
              { "atMs": 2200, "actor": "bastion", "action": "stop" } ]
```

Un point s'écrit `[x, z]` en mètres (x vers l'est, z vers le nord), dans ±1000 m. `obstacles` est facultatif (au plus 64 polygones strictement convexes de 3 à 16 sommets, dans un sens ou dans l'autre, identifiants uniques) ; `position` et `radius` (0,1 à 5 m) sont obligatoires, `facing` facultatif ([0, 1] par défaut, composantes dans [−1, 1]), `speed` (0,1 à 20 m/s) réservé aux assaillants. L'emprise de départ tient dans l'aire et ne chevauche ni un obstacle ni un combattant qui lui est solide. `move` exige `direction` (composantes dans [−1, 1], non toutes deux nulles) ; `stop` et les autres actions n'en ont pas ; un déplacement sans `space` est refusé. Chaque erreur a son chemin (`$.data.combatants[1].position`, `$.data.space.obstacles[0].points`, `$.data.commands[0].direction`…).

**Attaques annoncées (schéma 6, sous-lot 11.4c).** Dans un combat positionné, une attaque d'assaillant (`targeting: enemy`) peut avoir une forme ; tous les champs sont facultatifs et seuls ceux de sa forme sont admis ([MOTEUR.md](MOTEUR.md#attaques-annoncées-et-garde-de-face-sous-lot-114c)) :

```json
{ "id": "ecrasement", "rawDamage": 120, "damageType": "physique", "targeting": "enemy", "unblockable": false, "applies": [],
  "shape": "circle", "telegraphMs": 900, "radius": 2 },
{ "id": "souffle", "…": "…", "shape": "cone", "telegraphMs": 1100, "range": 8, "angle": 90, "guardable": true },
{ "id": "coup", "…": "…", "shape": "target", "range": 2.5 }
```

| Champ | Formes | Sens et bornes |
|---|---|---|
| `shape` | — | `target`, `cone`, `circle`, `line`, `persistent` ; absente : attaque historique (la cible ou tous les ennemis, sans géométrie) |
| `telegraphMs` | toutes | durée de l'annonce (ms, multiple du tick, 0 par défaut) ; une zone doit laisser `combat.telegraph.minReactionMs` + le temps d'en sortir au pas de base |
| `range` | `target` (facultatif), `cone`, `line` | portée (bords des emprises) ou longueur, 0,1 à 100 m |
| `radius` | `circle`, `persistent` | rayon, 0,1 à 100 m |
| `angle` | `cone` | ouverture, 1° à 180° |
| `width` | `line` | largeur, 0,1 à 100 m |
| `center` | `circle`, `persistent` | `target` (défaut) ou `self` |
| `persistentMs`, `tickMs` | `persistent` | durée de la zone et cadence de ses impacts (ms, multiples du tick, `tickMs` ≤ `persistentMs`) |
| `guardable`, `parryable` | toutes, attaques historiques comprises | défenses admises ; défauts et interdits : voir MOTEUR.md (jamais de parade contre une zone, jamais de garde contre une zone persistante ni contre une attaque imparable) |

Action `select` (combat positionné) : `{ "atMs": 0, "actor": "bastion", "action": "select", "target": "ogre" }`, sans `target` pour effacer la sélection ; elle ne prend pas de compétence. Le protocole du lot 10 ne la transmet pas.

**Arrivées et départs (schéma 7, sous-lot 11.4d).** Un combattant peut porter `arrivalMs` (entier, multiple du tick, dans [0, durée[) : il arrive en cours de combat, au début de ce tick ; absent : présent dès le départ. Sa place (combat positionné) est contrôlée comme celle des autres pour l'aire et le décor, mais pas contre les autres combattants : c'est le moteur qui refuse l'arrivée si une emprise solide l'occupe à ce moment-là. Action `leave` (avec ou sans espace) : `{ "atMs": 5000, "actor": "renfort", "action": "leave" }`, sans compétence ni cible, effective en fin de tick ([MOTEUR.md](MOTEUR.md#arrivées-départs-et-instantané-détat-sous-lot-114d)). Le protocole du lot 10 ne la transmet pas. Scénario livré : `scenarios/espace_renfort.json`.

**Comportements et charge (schéma 8, sous-lot 11.5).** Un assaillant d'un combat positionné peut porter `behavior` : `{ "maxRange": 0.5, "aggroRange": 8, "encircle": true }` (loup), `{ "maxRange": 9, "minRange": 6, "aggroRange": 14 }` (archer). `maxRange` obligatoire, `minRange` (0 par défaut, au plus `maxRange`), distances dans [0, 100] m entre les bords ; `aggroRange` dans ]0, 100] m, absente : engagé d'emblée ; `encircle` faux par défaut. Avec un comportement, `target` devient facultative, omise ou nulle (l'ennemi prend le plus proche) et le scénario ne lui donne ni `move` ni `stop`. Une bande peut être une charge : `"shape": "line", …, "charge": true` (seulement une bande). Fonctionnement : [MOTEUR.md](MOTEUR.md#comportements-des-ennemis-sous-lot-115). Scénario livré : `scenarios/espace_meute.json`.

Dépendances (lot 6) : la description d'un combat (`CombatSetup`, combattants, commandes, identité des données) est passée d'`EdS.Simulation` à `EdS.Domain` (espace `EdS.Domain.Combat`). `EdS.Data` ne dépend donc plus que du domaine ; seuls `EdS.Data.Json` (rejeu) et les outils (`EdS.Tools`, `EdS.Cli`) référencent le moteur pour simuler et rejouer.

```
EdS.Domain ◄── EdS.Simulation ◄── EdS.Data.Json ──► EdS.Data ──► EdS.Domain
                     ▲                  ▲
                     └──── EdS.Tools ───┘      (netstandard2.1 à gauche ; EdS.Tools et EdS.Cli en net10.0)
                              ▲
                           EdS.Cli
```
