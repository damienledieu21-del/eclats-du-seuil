# Classes, talents et builds (lot 4)

Le moteur est générique : aucune règle ne dépend d'une classe. Ce document décrit la structure des classes, la validité d'un build et, depuis le sous-lot 8.5, le contenu complet du Bastion et ses builds de référence. Depuis le lot 7.3, un talent ou une compétence peut porter des déclencheurs ; depuis le sous-lot 8.1, un talent ou une compétence passive peut porter des **modificateurs** (règles effectives du porteur, schéma de classe 5 ; 7 depuis le sous-lot 8.3) : voir [DONNEES.md](DONNEES.md#modificateurs-sous-lot-81). Depuis le sous-lot 8.5, tous les talents et passifs du Bastion livré en portent (ci-dessous).

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
  "schemaVersion": 8, "kind": "class", "contentVersion": "0.4.0-provisoire",
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

Identifiants stables préfixés (`class.`, `tree.`, `talent.`, `skill.`), minuscules, chiffres et `_` ; jamais renommés ni réutilisés. Tous les champs de l'exemple sont obligatoires ; `combat`, `triggers` (lot 7.3) et `modifiers` (sous-lot 8.1) sont facultatifs.

Vérifications de cohérence (`ClassDefinition.Validate`) : identifiants bien formés et uniques, noms non vides, nombre d'arbres, rang maximum dans [1, 100] (1 pour un majeur), prérequis connus, non dupliqués, non réflexifs, de rang dans [1, rang max], pas de majeur exigeant un majeur, pas de cycle, chaque majeur atteignable (assez de points hors majeurs et hors talents qui en dépendent), niveau minimal de compétence valide, ultime pas avant le niveau 10 ; depuis le sous-lot 8.1, modificateurs bien formés (clé connue, bornes au rang 1 et au rang maximal, conditions, doublons, au plus 8 par porteur et 512 par classe, aucun sur une active ou une ultime).

## Arbres du Bastion (provisoires, sous-lot 8.5)

Chaque arbre a trois talents notables (le troisième est le majeur) et trois intermédiaires de cinq rangs (A2, noms de travail), sans prérequis ; une branche offre donc 19 points avant son majeur (18 exigés). Plus aucun talent ni passif `[TEST]` (les paliers et passifs d'essai des lots 4 à 8.4 sont retirés ; leurs identifiants ne sont pas réutilisés). Valeurs **provisoires** du catalogue de la feuille de route, à équilibrer au sous-lot 8.7 ; « r » est le rang.

| Arbre | Talent (rangs) | Prérequis | Effet livré |
|---|---|---|---|
| Rempart | Garde stable (3) | — | réduction de garde +0,04 r, entretien −0,25 r endurance/s |
| | Écho défensif (1) | Garde stable 3 | un blocage rend 4 endurance ; un blocage donne aussi 1 Charge, au plus toutes les 3 s |
| | **Rempart éternel** (majeur) | Écho défensif 1 | garde incassable (`guard.noUpkeepBreak`), réduction de 40 % à 0 endurance (décision 1) |
| | Peau de fer (5) | — | dégâts subis −1,5 % r |
| | Souffle du roc (5) | — | endurance max +4 r, régénération +1 r/s |
| | Poids du rempart (5) | — | en garde, dégâts subis −0,5 % r par Charge détenue |
| Contrecoup | Riposte précise (3) | — | une parade parfaite frappe l'attaquant : coefficient 0,5 + 0,25 r (0,75 à 1,25), sans critique ni Résonance |
| | Fureur contenue (2) | Riposte précise 2 | dégâts infligés +1 % r par tranche de 10 Résonance détenue (+20 % à 100 au rang 2) |
| | **Jugement du colosse** (majeur) | Fureur contenue 2 | une parade parfaite frappe tous les ennemis (1,2) et rend 10 Résonance, au plus toutes les 3 s (L8-D2) |
| | Main lourde (5) | — | dégâts infligés +2 % r, réactions comprises |
| | Élan (5) | — | récupération après une parade ratée −50 ms r |
| | Écho de guerre (5) | — | une parade parfaite rend 2 r Résonance |
| Gardien | Égide réactive (3) | — | un blocage ou un impact paré donne un bouclier de 3 % r des PV max, 4 s (`reactiveAegis`), au plus toutes les 2 s, renouvelé au plus fort |
| | Lien protecteur (1) | Égide réactive 3 | la Barrière d'acier lancée sur un allié lui pose aussi une protection de 6 s (`protectiveLink`) : 25 % de ses dégâts transférés au Bastion |
| | **Serment du gardien** (majeur) | Lien protecteur 1 | un assaillant qui frappe un allié est provoqué 2 s (`oathTaunt`), au plus toutes les 3 s ; transfert porté à 40 % (+0,15) et passant par la garde du Bastion |
| | Vigilance (5) | — | PV max +2 % r |
| | Présence (5) | — | provocations de Provocation et de Serment +0,2 s r (pas celle de l'ultime) |
| | Rempart vivant (5) | — | boucliers posés +4 % r |

Compétences : six actives (Frappe du rempart, Heurt de bouclier, Provocation, Contrecoup, Barrière d'acier, Onde de rupture, niveau 1 provisoire), l'ultime Bastion absolu (niveau 10) et huit passifs équipables, distincts des talents permanents (tous de niveau 1) :

| Passif | Effet livré |
|---|---|
| Endurance du roc | endurance max +15 |
| Bouclier lourd | coût par blocage −3 |
| Réserve de Charges | Charges max +1 |
| Œil du duelliste | fenêtre de parade +50 ms |
| Ferveur | Résonance par impact direct +1 |
| Posture de fer | en garde, dégâts subis −5 % |
| Sentinelle | un impact subi par un allié donne 1 Charge, au plus toutes les 6 s |
| Ténacité | résistance aux altérations +100 |

Forme en données (modificateurs et déclencheurs) : [DONNEES.md](DONNEES.md#bastion-sous-lot-85) ; interprétations retenues : [LIMITES_LOT_8.md](LIMITES_LOT_8.md#sous-lot-85--interprétations-et-limites).

Depuis le lot 5, une active ou l'ultime peut porter un bloc `combat` (puissance et type de dégâts, coefficient, coût d'endurance, recharge en ms, ciblage, `provisional` ; au lot 6 : `unblockable`, `rampartChargeCost`, `rampartChargeGain` ; au sous-lot 7.2 : `applies` ; au sous-lot 8.3 : ciblages de soutien et compétences sans dégâts ; au sous-lot 8.4 : `rampartChargeSpendAll`, `coefficientPerRampartCharge`, `resonanceGain`). Un passif ne peut pas en avoir. Depuis le sous-lot 8.4, toutes les actives et l'ultime ont leur fiche complète, aux valeurs **provisoires** de la feuille de route :

| Compétence | Coefficient | Endurance | Recharge | Ciblage | Effets |
|---|---|---|---|---|---|
| Frappe du rempart | 1,20 | 10 | 6 s | ennemi | — |
| Heurt de bouclier | 0,80 | 15 | 10 s | ennemi | étourdissement 1,5 s (`stun`) |
| Onde de rupture | 0,50 | 20 | 12 s | tous les ennemis | Fragilité (`fragility`) |
| Contrecoup | 0,60 + 0,40 par Charge | 10 | 8 s | ennemi | dépense toutes les Charges, au moins une |
| Provocation | — | 10 | 12 s | ennemi (un assaillant) | provocation 3 s (`taunt`), +5 Résonance au lancement |
| Barrière d'acier | — | 20 | 20 s | allié (soi compris) | bouclier de 15 % des PV max du Bastion, 6 s (`steelBarrier`) |
| Bastion absolu (ultime) | — | 0 (100 de Résonance) | — | soi | 8 s : dégâts subis −30 % pour le Bastion et ses alliés, garde sans entretien pour le Bastion, provocation de tous les assaillants |

Toutes sont parables (aucune n'est imparable). Plus aucune compétence livrée n'est refusée au lancer `skillNotImplemented`. Moteur : [MOTEUR.md](MOTEUR.md#fiches-du-bastion-et-ripostes-de-zone-sous-lot-84).

## Validation d'un build (`BuildValidator`)

Un build donne la classe, le niveau, les attributs (explicites), les rangs de talents, les actives (dans l'ordre des emplacements), l'ultime et les passifs. Toutes les erreurs sont rapportées, avec un code stable et l'élément concerné :

`WrongClass`, `LevelOutOfRange`, `UnknownTalent`, `RankOutOfRange`, `TooManyPoints`, `PrerequisiteNotMet`, `MajorThresholdNotMet`, `TooManyMajorTalents`, `UnknownSkill`, `WrongSlotKind`, `DuplicateSkill`, `TooManySkills`, `SkillLevelTooLow`, `SkillRequirementNotMet`, `UltimateLocked`, `InvalidProgressionRules` et `AttributeOutOfRange` (correctif 6.1).

Entrées extrêmes (correctif 6.1, M03) : aucune valeur, même `int.MinValue` ou `int.MaxValue` (rangs, niveau, attributs, règles de progression), ne provoque d'exception. Les sommes de rangs sont calculées sur 64 bits ; des règles de progression incohérentes donnent un seul diagnostic `InvalidProgressionRules` ; les attributs sont plafonnés à 1 000 000. Bornes des règles de progression : `maxLevel` et `talentPoints.firstLevel` dans [1, 1000], `perLevel` dans [0, 1000], seuil et maximum de majeurs dans [0, 1 000 000], `treesPerClass` dans [1, 100], au plus 100 emplacements par liste.

La validation est statique (allocation finale) mais respecte l'ordre d'acquisition : les talents qui dépendent d'un majeur ne comptent pas dans son seuil de 18. Les points non dépensés sont autorisés.

Exemples testés (`tests/EdS.Data.Tests/BastionBuildExamples.cs`) : trois profils de niveau 30 (Rempart défensif, Contrecoup offensif, Gardien protecteur), un hybride sans majeur, un débutant de niveau 1, un niveau 10 avec ultime ; et dix-huit builds invalides (deux majeurs, 17 points avant un majeur, prérequis de rang, rang trop élevé, intermédiaire au rang 6, majeur sans son prérequis, niveau 31, trop de points, ultime au niveau 9, 3 actives au niveau 4, compétence en double, mauvais type d'emplacement, talent ou compétence inconnus, ancien palier ou passif d'essai, trop de passifs).

## Builds de référence (sous-lot 8.5, `data/v0/builds/bastion.json`)

Décision A12 : les builds de comparaison sont livrés en données, dans un fichier de type `builds` (schéma 1, voir [DONNEES.md](DONNEES.md#builds-de-référence-sous-lot-85)), validés par `BuildValidator` comme tout build. Attributs communs (A10), contrôle de niveau 10 compris : Force 30, Dextérité 10, Intelligence 6, Vitalité 30, Volonté 15, Endurance 20. Tous les points sont dépensés ; chaque build équipe l'ultime.

| Build | Catégorie | Points (Rempart / Contrecoup / Gardien) | Talents | Actives | Passifs |
|---|---|---|---|---|---|
| `build.bastion.rempart` | référence | 19 / 2 / 8 | Garde stable 3, Écho défensif, Peau de fer 5, Souffle du roc 4, Poids du rempart 5, **Rempart éternel** ; Égide réactive 3, Vigilance 5 ; Riposte précise 2 | Frappe du rempart, Heurt de bouclier, Barrière d'acier, Onde de rupture | Endurance du roc, Bouclier lourd, Réserve de Charges, Posture de fer |
| `build.bastion.contrecoup` | référence | 10 / 19 / 0 | Riposte précise 3, Fureur contenue 2, Main lourde 5, Écho de guerre 5, Élan 3, **Jugement du colosse** ; Garde stable 3, Écho défensif, Souffle du roc 5, Peau de fer 1 | Frappe du rempart, Contrecoup, Onde de rupture, Heurt de bouclier | Œil du duelliste, Ferveur, Endurance du roc, Ténacité |
| `build.bastion.gardien` | référence | 9 / 1 / 19 | Égide réactive 3, Lien protecteur, Vigilance 5, Présence 4, Rempart vivant 5, **Serment du gardien** ; Garde stable 3, Écho défensif, Peau de fer 5 ; Riposte précise 1 | Provocation, Barrière d'acier, Frappe du rempart, Heurt de bouclier | Sentinelle, Posture de fer, Ténacité, Endurance du roc |
| `build.bastion.hybride` | contrôle | 10 / 10 / 9, sans majeur | Garde stable 3, Écho défensif, Peau de fer 5, Souffle du roc 1 ; Riposte précise 3, Fureur contenue 2, Main lourde 5 ; Égide réactive 3, Lien protecteur, Vigilance 5 | Frappe du rempart, Contrecoup, Barrière d'acier, Onde de rupture | Endurance du roc, Posture de fer, Œil du duelliste, Ferveur |
| `build.bastion.niveau_10` | contrôle | 4 / 3 / 2 (9 points) | Garde stable 3, Écho défensif ; Riposte précise 3 ; Égide réactive 2 | Frappe du rempart, Heurt de bouclier, Contrecoup, Provocation | Endurance du roc, Posture de fer |

La feuille de route fixe la répartition par arbre, le majeur, les actives et les passifs des trois références ; le détail des intermédiaires et les deux contrôles sont des choix du sous-lot 8.5, provisoires comme le reste. Tests : `Lot85BuildDataTests` (conformité, combat d'entraînement et rejeu de chaque build, erreurs). Les scénarios S1 à S5 qui les comparent arrivent au sous-lot 8.6.

## Mesures des builds (sous-lot 8.6)

`eds report` joue chaque scénario de mesure (`scenarios/mesures`, voir [DONNEES.md](DONNEES.md#scénarios-de-mesure-et-rapport-sous-lot-86)) avec chaque build de la classe du personnage `bastion` et chaque profil de joueur, puis évalue les objectifs provisoires de la feuille de route. Un combat mesuré est un **combat ordinaire** : le build du personnage `bastion` est remplacé par le build mesuré, un pilote déterministe ajoute ses commandes tick par tick, puis le scénario complété est rejoué et le résultat comparé à celui du pilotage (`PilotedCombat`) ; son journal (`--logs`) se rejoue octet par octet avec `eds replay`.

| Scénario | Durée | Adversaires (valeurs du 8.6) |
|---|---|---|
| S1 `s1_mannequin` | 60 s | un mannequin sans armure (DPS brut) |
| S2 `s2_duel` | 120 s | un assaillant toutes les 1,5 s à partir de 1,5 s, 80 de dégâts bruts ; une attaque sur quatre pose Fragilité |
| S3 `s3_meute` | 60 s | trois assaillants : deux sur le Bastion (45, toutes les 2 s et 2,5 s, une attaque sur deux pose un saignement de coefficient 0,5), un hurleur de zone (30, toutes les 3 s) |
| S4 `s4_protection` | 90 s | un allié fragile (`allie`, niveau 10, 400 PV, sans compétence ni garde) visé par deux assaillants (40, toutes les 2 s et 2,5 s, le second alterne avec un saignement) |
| S5 `s5_briseur` | 90 s | un assaillant toutes les 1,5 s : deux coups bloquables de 60 puis un écrasement **imparable** de 90 |

**Profils** : débutant (aucune parade parfaite), standard (une attaque éligible sur deux : la première, la troisième…), expert (toutes). Une attaque éligible est une attaque bloquable d'un assaillant qui vise le Bastion ou frappe en zone, au tick où elle part ; plusieurs au même tick comptent pour une. Les profils ne diffèrent que par les parades.

**Pilote** (`CombatPilot`, mêmes règles pour tous les builds) : il lit l'état du début du tick (`CombatEngine.View`) ; il pare selon le profil ; il tient la garde tant qu'un assaillant le menace (sa prochaine attaque le vise ou frappe en zone), la baisse pour régénérer quand l'endurance ne paie plus un blocage et la relève à la moitié de l'endurance maximale (au moins deux blocages), sauf garde incassable (Rempart éternel), jamais baissée ; aucune garde face à un mannequin ou à des assaillants qui visent un autre. Compétences par priorité : l'ultime dès 100 de Résonance, puis les actives dans l'ordre des emplacements du build, prêtes et payables en gardant de quoi bloquer un coup (aucune compétence payante pendant une pause de garde) ; jamais au tick d'un impact qu'il bloquerait sans le parer (lancer fait quitter la garde, reprise au tick suivant). Cibles : pour une compétence de dégâts, l'assaillant qui le vise et frappe le plus tôt, sinon le premier ennemi ; pour la Provocation, un assaillant qui vise un allié (sinon elle attend) ; pour la Barrière d'acier, l'allié vivant le plus entamé, sinon soi ; Contrecoup seulement à 3 Charges (ou au maximum s'il est plus bas).

**Mesures** (personnage `bastion`, et l'allié de S4) : DPS (dégâts infligés par seconde de vie) ; PV perdus (PV max − PV restants) ; dégâts évités (atténués par la garde, la parade et les réductions de dégâts subis, absorbés par ses boucliers, transférés à un protecteur) ; survie (instant de la mort, sinon estimation durée × PV max / PV perdus, plafonnée à 10 fois la durée) ; survie de l'allié (même calcul) ; blocs, parades, ruptures, compétences, ultimes, refus ; pics de charge en pourcentage de chaque budget.

**Objectifs provisoires** (profil standard, builds de référence) : S1 écart de DPS (max − min) / max ≤ 15 % ; S2 DPS du Contrecoup ≥ 1,25 × Rempart ; S2 et S5 survie et dégâts évités du Rempart ≥ 1,25 × Contrecoup ; S4 survie de l'allié avec le Gardien ≥ 1,5 × la meilleure des deux autres références ; tous les combats (tous profils, tous builds) sous 50 % de chaque budget. Un objectif dont un combat manque est « non mesuré ».

**Relevé initial (8.6, avant équilibrage ; règles 0.8.0, Bastion 0.4.0), profil standard :**

| Scénario | Build | DPS | PV perdus / max | Évités | Survie (s) | Allié (s) | Blocs | Parades | Compétences | Ultimes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| S1 | Rempart | 20,65 | 0 / 440 | 0 | 600 | — | 0 | 0 | 24 | 0 |
| S1 | Contrecoup | 23,08 | 0 / 400 | 0 | 600 | — | 0 | 0 | 20 | 0 |
| S1 | Gardien | 16,8 | 0 / 440 | 0 | 600 | — | 0 | 0 | 19 | 0 |
| S1 | Hybride | 17,33 | 0 / 440 | 0 | 600 | — | 0 | 0 | 18 | 0 |
| S1 | Niveau 10 | 16,8 | 0 / 400 | 0 | 600 | — | 0 | 0 | 16 | 0 |
| S2 | Rempart | 23,46 | 177 / 440 | 6391 | 298,3 | — | 38 | 39 | 13 | 4 |
| S2 | Contrecoup | 89,36 | 400 / 400 | 1599 | † 39 | — | 10 | 12 | 16 | 3 |
| S2 | Gardien | 24,92 | 440 / 440 | 5111 | † 111 | — | 19 | 34 | 27 | 3 |
| S2 | Hybride | 57,01 | 440 / 440 | 4679 | † 90 | — | 15 | 30 | 30 | 3 |
| S2 | Niveau 10 | 46,58 | 400 / 400 | 2398 | † 57 | — | 11 | 17 | 18 | 2 |
| S3 | Rempart | 36,32 | 236 / 440 | 3714 | 111,9 | — | 42 | 28 | 11 | 3 |
| S3 | Contrecoup | 146,05 | 400 / 400 | 842 | † 21,5 | — | 9 | 9 | 10 | 2 |
| S3 | Gardien | 29,85 | 181 / 440 | 3266 | 145,9 | — | 20 | 27 | 18 | 2 |
| S3 | Hybride | 64,95 | 440 / 440 | 1593 | † 31,5 | — | 13 | 13 | 10 | 1 |
| S3 | Niveau 10 | 54,42 | 400 / 400 | 960 | † 21,5 | — | 9 | 10 | 8 | 1 |
| S4 | Rempart | 23,98 | 0 / 440 | 0 | 900 | † 12 | 0 | 0 | 37 | 0 |
| S4 | Contrecoup | 41,62 | 60 / 400 | 340 | 600 | † 11 | 5 | 4 | 35 | 2 |
| S4 | Gardien | 17,9 | 50 / 440 | 962 | 792 | † 19,5 | 10 | 11 | 29 | 1 |
| S4 | Hybride | 22,38 | 55 / 440 | 0 | 720 | † 13 | 0 | 0 | 28 | 0 |
| S4 | Niveau 10 | 27,43 | 140 / 400 | 580 | 257,1 | † 12 | 7 | 7 | 36 | 1 |
| S5 | Rempart | 22,5 | 440 / 440 | 1849 | † 50,5 | — | 11 | 11 | 10 | 1 |
| S5 | Contrecoup | 72,09 | 400 / 400 | 452 | † 22 | — | 4 | 4 | 11 | 1 |
| S5 | Gardien | 19,47 | 440 / 440 | 1627 | † 49 | — | 8 | 10 | 11 | 1 |
| S5 | Hybride | 44,8 | 440 / 440 | 1434 | † 40 | — | 7 | 9 | 13 | 1 |
| S5 | Niveau 10 | 44,32 | 400 / 400 | 586 | † 22 | — | 4 | 5 | 8 | 0 |

(† : mort à cet instant.) Objectifs : **S1 non atteint** (écart de 27,2 % : Rempart 20,65, Contrecoup 23,08, Gardien 16,8) ; S2 DPS +280,9 % ; S2 survie +664,9 % et dégâts évités +299,7 % ; S5 survie +129,5 % et dégâts évités +309,1 % ; S4 allié +62,5 % (19,5 s contre 12 s) ; pic de charge maximal 2,2 % (S3). Lecture : chaque spécialisation excelle sur sa mesure, mais le Contrecoup domine le DPS bien au-delà des +25 % (ripostes de parade) et meurt vite ; le Gardien et le Rempart sont en retrait sur le mannequin, où ni parade ni blocage ne rapporte rien. L'équilibrage est l'objet du sous-lot 8.7 ; le relevé complet des trois profils s'obtient avec `eds report`.

