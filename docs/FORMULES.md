# Formules de combat v0.1 (lot 2)

> Valeurs **provisoires et non équilibrées**. Toutes sont réglables dans `data/v0/rules.json` (voir [DONNEES.md](DONNEES.md)).

## Statistiques dérivées (`DerivedStats.Calculate`)

`valeur = base + attribut × coefficient + somme des contributions (équipement, effets…)`

| Statistique | Base | Attribut | Plancher |
|---|---|---|---|
| Puissance physique | 0 | +2 par Force | 0 |
| Puissance agile | 0 | +2 par Dextérité | 0 |
| Puissance magique | 0 | +2 par Intelligence | 0 |
| Armure | 0 | — | brute conservée, effective ≥ 0 |
| Résistance magique | 0 | — | brute conservée, effective ≥ 0 |
| PV max | 100 | +10 par Vitalité | 0 |
| Résistance aux altérations | 0 | +2 par Volonté | 0 |
| Endurance max | 100 | +2 par Endurance | 0 |
| Chance de critique | 5 % | — | bornée à [0 %, 60 %] |
| Multiplicateur critique | ×1,5 | — | borné à [×1, ×2,5] |

L'équipement apporte trois puissances distinctes (physique, agile, magique) additionnées **avant** le coefficient de compétence. Depuis le sous-lot 12.3, l'équipement porté apporte aussi armure, résistance magique, PV max, endurance max et résistance aux altérations, en contributions plates (`GearBonus`, [MOTEUR.md](MOTEUR.md#bonus-déquipement-sous-lot-123)).

## Dégâts (`DamageCalculator.Compute`)

```
puissance         = puissance du type de la compétence (physique pour le Bastion)
× coefficient     de compétence (≥ 0)
× bonus           Π sur les catégories (compétence, talent, altération) de borne(1 + Σ bonus de la catégorie, 0, 10) ; la Fragilité de la cible est une altération de +0,1
× critique        multiplicateur critique de l'attaquant si le tirage réussit, sinon 1
× défense         constante / (constante + défense effective), armure ou résistance magique selon le type de dégâts
× (1 − réduction) garde (0,6) ou parade parfaite (0,9), jamais les deux ; 0 sinon (lot 6)
= arrondi final   au plus proche, moitié loin de zéro ; minimum 1 si le montant est > 0, 0 sinon
```

Garde (0,6) et parade (fenêtre de 150 ms) sont les valeurs de base des règles ; un bouclier porté les remplace (ci-dessous).

## Garde du bouclier (sous-lot V1.2)

Un bouclier porté apporte sa garde ; elle remplace les valeurs de base des règles pour son porteur, puis les modificateurs s'y ajoutent sous les mêmes plafonds qu'avant :

```
réduction de garde = borne(part du bouclier + Σ modificateurs guard.damageReduction, 0, combat.guard.maxDamageReduction = 0,75)
coût d'un blocage  = max(0, coût du bouclier + Σ modificateurs guard.costPerBlock)
fenêtre de parade  = borne(fenêtre du bouclier + Σ modificateurs parry.windowMs, 1 tick, 500 ms ramenés au multiple du tick)
sans bouclier      : les mêmes formules avec combat.guard.damageReduction (0,6), combat.guard.costPerBlock (10), combat.parry.windowMs (150 ms)
```

Bornes d'un bouclier (`combat.shield` des règles, [DONNEES.md](DONNEES.md#garde-des-boucliers-sous-lot-v12)) : part bloquée de 0,45 à 0,7, coût de 4 à 20, fenêtre de 100 à 200 ms. Boucliers livrés : l'écu de chêne (0,6 ; 10 ; 150 ms : exactement les valeurs des règles), les boucliers légers (0,5 ; 6 ; 200 ms, ou 0,55 ; 8 ; 200 ms) et les pavois (0,7 ; 14 à 16 ; 100 ms).

Exemple vérifiable (scénario `entrainement_bouclier`, coups de 100 dégâts physiques, armure 0, Bastion sans talent) : un coup gardé avec le pavois (0,7) fait 100 × (1 − 0,7) = **30** et coûte 14 d'endurance ; avec la rondache (0,5), **50** pour 6 d'endurance. Une parade lancée 150 ms avant le coup le pare avec la rondache (fenêtre de 200 ms, 4 ticks) mais pas avec le pavois (100 ms, 2 ticks).

## Effets d'équipement (sous-lot V1.3)

Les modificateurs d'un objet nommé ou d'un palier d'ensemble s'ajoutent à ceux des talents et des passifs, dans les mêmes formules (`damageDealt` dans la catégorie « talent » des bonus, `damageTaken` dans la réduction totale plafonnée à 85 %, `maxHealth` en pourcentage des PV maximaux, `endurance.regenPerSecond` à la régénération). Nouvelle condition **`targetStunned`** (équipement seulement, `damageDealt`) : le bonus ne compte que si la cible de l'impact porte une altération qui bloque ses compétences ou ses attaques d'assaillant (l'étourdissement), lue au calcul des dégâts.

Exemple vérifiable (scénario `entrainement_equipement`, Frappe du rempart sur un assaillant étourdi par le Heurt de bouclier, Bastion de niveau 10 sans talent) : **59** dégâts avec la Lame du Seuil (+10 % sur une cible étourdie), **56** sans ; deux coups bloqués de 100 bruts : **86** subis en tout avec la Garde de la tour à 2 pièces portées (−5 % en garde), **90** sans.

## Propriétés tirées (sous-lot V1.3)

Bornes d'une propriété `p` sur un objet de niveau requis `N` et de rareté `R` (échelle `s(R)` en pour cent, `rarities[].scalePercent` du catalogue) :

```
basse = max(1, ⌊(p.min + p.minPerLevel × (N − 1)) × s(R) / 100⌋)
haute = max(basse, ⌊(p.max + p.maxPerLevel × (N − 1)) × s(R) / 100⌋)
valeur tirée : uniforme dans [basse, haute] (Pcg32, rejet sans biais), propriétés distinctes, au nombre de count(R)
```

La rareté fixe le **nombre** de propriétés (commun 0, rare 1, très rare 2, épique 3, légendaire 3, mythique 4) et l'**échelle** (100 %, 100 %, 110 %, 120 %, 135 %, 150 %) ; le niveau fixe les bornes ; le hasard ne choisit qu'à l'intérieur (Q30). Exemple : Vigueur (PV maximaux : 5 à 10, +3 à +6 par niveau) sur les bottes du Renard (rares, niveau 6) : **20 à 40** ; sur la cape du Seuil (épique, niveau 19) : ⌊59 × 1,2⌋ = **70** à ⌊118 × 1,2⌋ = **141**.

Étapes intermédiaires à 4 décimales ; l'étape de défense est calculée exactement (produit-quotient 128 bits).

## Forge, qualité et usure (sous-lot V1.4)

Hors du moteur de combat (aucune règle ni aucun journal ne change). Valeurs livrées entre parenthèses, provisoires.

- **Adresse** : moyenne des notes des frappes du petit jeu (de 0 à 100 chacune, trois frappes), arrondie vers le bas.
- **Chance de rater** une recette à `gap` niveaux de métier au-dessus du sien (au plus `failure.maxLevels`, 2) : `gap × percentPerLevel − skillReduction × adresse / 100`, de 0 à 100 % (35 et 25 : 35 % à un niveau, 10 % avec des frappes parfaites ; 70 % à deux niveaux, 45 % avec des frappes parfaites). Sans petit jeu (recette sans qualité : l'herboristerie), l'adresse vaut 0. Ratée, la recette prend ses entrées et rend `⌊nombre × refundPercent / 100⌋` de chacune (50 %) ; rien n'est fabriqué ni appris.
- **Qualité** d'une recette à qualité réussie, `n` = niveaux du métier au-dessus du requis : chef-d'œuvre `M = min(100, base + perSkill × adresse / 100 + perLevel × n)` (0, 12, 2), qualité supérieure `min(100 − M, …)` de même (15, 45, 5), qualité normale pour le reste. Au niveau requis : 12 % de chef-d'œuvre et 60 % de qualité supérieure avec trois frappes parfaites ; 0 % et 15 % sans adresse.
- **Tirages** : `Pcg32` initialisé par l'empreinte FNV-1a 64 bits de la clé `<personnage>/craft/<recette>/day/<jour du monde>/<compteur des fabrications>`, flux `craft:<recette>` ; d'abord l'échec (`NextBounded(100) < chance`), puis la qualité (`r < M` : chef-d'œuvre ; `r < M + S` : supérieure), toujours dans cet ordre. Le compteur avance à chaque tentative admise (réussie ou ratée), gardé dans la sauvegarde.
- **Bonus de qualité** : pour chaque statistique non nulle du bonus de base `b`, `max(1, ⌊b × p / 100⌋)`, `p` = 10 % (supérieure) ou 20 % (chef-d'œuvre). Exemple : l'épée de garde (22 de puissance physique) donne 24 en qualité supérieure, 26 en chef-d'œuvre.
- **Usure** : à la fin d'un combat (le personnage n'est plus en combat, ou il est tombé), chaque pièce portée à l'entrée dans la zone qui s'use (où qu'elle soit maintenant, portée ou dans le sac) perd `⌊(dégâts reçus × perFullHealth + reste) / PV maximaux⌋` points de solidité (10 ; solidité 100), le reste gardé pour le combat suivant de la session (pas dans la sauvegarde). Les dégâts reçus sont ceux du compteur du moteur (`CombatantCounters.DamageTaken`, par différence d'un tick au suivant ; un soin ne les efface pas).
- **Bouclier usé à zéro** : garde de repli, pour chaque valeur la plus faible de la sienne et de celle des règles : part bloquée `min(sienne, combat.guard.damageReduction)`, coût d'un blocage `max(sien, combat.guard.costPerBlock)`, fenêtre de parade `min(sienne, combat.parry.windowMs)`, ramenées dans les bornes `combat.shield`.
- **Prix d'une réparation** (une pièce) : `⌈valeur × percent × manque / (100 × solidité)⌉`, au moins `minimum` (30 % ; 5 cuivre), `valeur` celle des marchands (0 sans valeur). Exemple : l'épée du milicien (300 cuivre) à 40 / 100 coûte 54 cuivre.

## Exemple vérifiable (test `Exemple_1_coup_critique_physique_detaille`)

Bastion Force 50 + arme +40, compétence ×1,2, bonus compétence +20 %, talent +10 % −5 %, altération +10 %, coup critique, cible armure 500 :

| Étape | Calcul | Résultat |
|---|---|---|
| Puissance | 50 × 2 + 40 | 140 |
| Coefficient | 140 × 1,2 | 168 |
| Bonus | 1,2 × 1,05 × 1,1 = 1,386 ; 168 × 1,386 | 232,848 |
| Critique | 232,848 × 1,5 | 349,272 |
| Armure | 349,272 × 1000 / 1500 | 232,848 |
| Arrondi | | **233** |

Sans critique : 155,232 → **155**.
