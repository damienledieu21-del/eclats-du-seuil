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
