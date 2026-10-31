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

Étapes intermédiaires à 4 décimales ; l'étape de défense est calculée exactement (produit-quotient 128 bits).

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
