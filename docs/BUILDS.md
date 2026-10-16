# Classes, talents et builds (lot 4)

Le moteur est générique : aucune règle ne dépend d'une classe. Les effets de combat des talents et des compétences ne sont **pas** implémentés (lot 5 et suivants) ; ce lot ne décrit que leur structure et la validité d'un build.

## Règles de progression (`rules.json`, bloc `progression`)

| Paramètre | Valeur v0 | Sens |
|---|---|---|
| `maxLevel` | 30 | niveau maximum |
| `talentPoints.firstLevel` / `perLevel` | 2 / 1 | points = niveau − 1 (29 au niveau 30) |
| `majorTalents.threshold` | 18 | points déjà investis dans la branche avant d'acquérir son majeur |
| `majorTalents.max` | 1 | talents majeurs par build |
| `treesPerClass` | 3 | arbres par classe, librement combinables |
| `slots.activeUnlockLevels` | [1, 1, 5, 10] | 2 actives au niveau 1, 3 au 5, 4 au 10 |
| `slots.passiveUnlockLevels` | [1, 5, 20, 30] | 1/2/3/4 passifs équipables |
| `slots.ultimateUnlockLevel` | 10 | emplacement d'ultime dédié |

Un majeur coûte 1 point après les 18 : 19 points pour un majeur, il en reste 10 au niveau 30, donc un second est impossible même sans la limite explicite `max`, qui reste vérifiée.

## Fichier de classe (`data/v0/classes/bastion.json`)

```json
{
  "schemaVersion": 1, "kind": "class", "contentVersion": "0.1.0-provisoire",
  "data": {
    "id": "class.bastion", "name": "Bastion",
    "trees": [ { "id": "tree.bastion.rempart", "name": "Rempart", "talents": [
      { "id": "talent.bastion.rempart.echo_defensif", "name": "Écho défensif", "maxRank": 1, "major": false,
        "placeholder": false, "prerequisites": [ { "talent": "talent.bastion.rempart.garde_stable", "rank": 3 } ] } ] } ],
    "skills": [ { "id": "skill.bastion.bastion_absolu", "name": "Bastion absolu", "kind": "ultimate",
                  "minLevel": 10, "placeholder": false, "talentRequirements": [] } ]
  }
}
```

Identifiants stables préfixés (`class.`, `tree.`, `talent.`, `skill.`), minuscules, chiffres et `_` ; jamais renommés ni réutilisés. Tous les champs sont obligatoires.

Vérifications de cohérence (`ClassDefinition.Validate`) : identifiants bien formés et uniques, noms non vides, nombre d'arbres, rang maximum ≥ 1 (1 pour un majeur), prérequis connus, non dupliqués, non réflexifs, de rang dans [1, rang max], pas de majeur exigeant un majeur, pas de cycle, chaque majeur atteignable (assez de points hors majeurs et hors talents qui en dépendent), niveau minimal de compétence valide, ultime pas avant le niveau 10.

## Mini-arbre du Bastion (provisoire)

| Arbre | Talents réels | Prérequis |
|---|---|---|
| Rempart | Garde stable (3), Écho défensif (1), **Rempart éternel** (majeur) | Écho : Garde stable 3 ; majeur : Écho 1 |
| Contrecoup | Riposte précise (3), Fureur contenue (2), **Jugement du colosse** (majeur) | Fureur : Riposte 2 ; majeur : Fureur 2 |
| Gardien | Égide réactive (3), Lien protecteur (1), **Serment du gardien** (majeur) | Lien : Égide 3 ; majeur : Lien 1 |

Chaque arbre contient en plus trois talents `[TEST] Palier 1–3` (5 rangs, `placeholder: true`, sans effet) pour que la branche offre assez de points avant son majeur. Ils seront remplacés par de vrais talents.

Compétences : six actives (Frappe du rempart, Heurt de bouclier, Provocation, Contrecoup, Barrière d'acier, Onde de rupture, niveau 1 provisoire), l'ultime Bastion absolu (niveau 10) et quatre passifs `[TEST] Passif 1–4`. Les passifs équipables sont distincts des talents permanents.

Depuis le lot 5, une active peut porter un bloc `combat` (puissance et type de dégâts, coefficient, coût d'endurance, recharge en ms, ciblage `enemy` ou `allEnemies`, `provisional`). Un passif ne peut pas en avoir. Seules deux compétences sont implémentées, avec des valeurs **provisoires** :

| Compétence | Coefficient | Endurance | Recharge | Ciblage |
|---|---|---|---|---|
| Frappe du rempart | 1,20 | 10 | 6 s | ennemi |
| Heurt de bouclier | 0,80 | 15 | 10 s | ennemi |

Les autres actives sont équipables mais refusées au lancer (`skillNotImplemented`). Moteur : [MOTEUR.md](MOTEUR.md).

## Validation d'un build (`BuildValidator`)

Un build donne la classe, le niveau, les attributs (explicites), les rangs de talents, les actives (dans l'ordre des emplacements), l'ultime et les passifs. Toutes les erreurs sont rapportées, avec un code stable et l'élément concerné :

`WrongClass`, `LevelOutOfRange`, `UnknownTalent`, `RankOutOfRange`, `TooManyPoints`, `PrerequisiteNotMet`, `MajorThresholdNotMet`, `TooManyMajorTalents`, `UnknownSkill`, `WrongSlotKind`, `DuplicateSkill`, `TooManySkills`, `SkillLevelTooLow`, `SkillRequirementNotMet`, `UltimateLocked`.

La validation est statique (allocation finale) mais respecte l'ordre d'acquisition : les talents qui dépendent d'un majeur ne comptent pas dans son seuil de 18. Les points non dépensés sont autorisés.

Exemples testés (`tests/EdS.Data.Tests/BastionBuildExamples.cs`) : trois profils de niveau 30 (Rempart défensif, Contrecoup offensif, Gardien protecteur), un hybride sans majeur, un débutant de niveau 1, un niveau 10 avec ultime ; et quatorze builds invalides (deux majeurs, 17 points avant un majeur, prérequis de rang, rang trop élevé, niveau 31, trop de points, ultime au niveau 9, 3 actives au niveau 4, compétence en double, mauvais type d'emplacement, talent ou compétence inconnus, trop de passifs).
