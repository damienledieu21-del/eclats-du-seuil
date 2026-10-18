# Rapport de mesures du sous-lot 8.7 — passe d'équilibrage

Mesures obtenues avec `eds report` (sous-lot 8.6 : pilote déterministe, scénarios `scenarios/mesures`, protocole dans [BUILDS.md](BUILDS.md#mesures-des-builds-sous-lot-86)). Moteur 0.14.0 et journal au format 8 inchangés ; règles au contenu 0.8.0 (inchangées) ; **Bastion au contenu 0.5.0**. Toutes les valeurs restent provisoires.

## Changement

Un seul changement de données, **décidé par Damien le 18 octobre 2026** (question posée pendant le sous-lot) : la **Provocation inflige une frappe physique de 0,8** (puissance physique, dégâts physiques) en plus de provoquer ; coût, recharge, durée de provocation et +5 Résonance inchangés. Le pilote lance une provocation qui inflige des dégâts sur l'ennemi désigné quand aucun assaillant ne vise un allié (auparavant, sans dégâts, elle attendait).

Pourquoi : le seul objectif manqué au 8.6 était S1 (écart de DPS de 27,2 % pour 15 % admis). Deux des quatre actives du Gardien (Provocation, Barrière d'acier) n'infligeaient rien : sur un mannequin, son DPS se réduisait à Frappe du rempart et Heurt de bouclier (16,8 = 60 de puissance × (1,2 / 6 s + 0,8 / 10 s)). Retoucher les seules valeurs partagées ne suffisait pas : Frappe 1,6, Heurt 1,2, Onde 0,3 et Main lourde à 1 %/rang ensemble laissaient encore 17,3 % (le bonus multiplicatif du Contrecoup ne se dilue pas). Deux autres options ont été présentées : équiper Onde de rupture à la place de Provocation dans le build du Gardien (écart 11,7 %, allié S4 +58 %), ou assouplir l'objectif S1 à 30 %.

## Objectifs provisoires (profil standard)

| Objectif | Avant (contenu 0.4.0) | Après (contenu 0.5.0) |
|---|---|---|
| S1 écart de DPS ≤ 15 % | **non atteint** : 27,21 % (Rempart 20,65, Contrecoup 23,08, Gardien 16,8) | **atteint** : 10,53 % (Rempart 20,65, Contrecoup 23,08, Gardien 21,2) |
| S2 DPS Contrecoup ≥ +25 % sur Rempart | atteint : +280,9 % (89,36 contre 23,46) | inchangé |
| S2 survie Rempart ≥ +25 % sur Contrecoup | atteint : +664,9 % (298,3 s contre 39 s) | inchangé |
| S2 dégâts évités Rempart ≥ +25 % | atteint : +299,7 % (6 391 contre 1 599) | inchangé |
| S5 survie Rempart ≥ +25 % | atteint : +129,5 % (50,5 s contre 22 s) | inchangé |
| S5 dégâts évités Rempart ≥ +25 % | atteint : +309,1 % (1 849 contre 452) | inchangé |
| S4 allié ≥ 1,5 fois plus longtemps avec Gardien | atteint : +62,5 % (19,5 s contre 12 s) | inchangé |
| Tous les combats < 50 % de chaque budget | atteint : pic 2,2 % (S3) | inchangé |

Seuls le Gardien et le contrôle de niveau 10 équipent la Provocation : les mesures du Rempart, du Contrecoup et de l'hybride sont identiques avant et après. Avec le Gardien plus fort sur le mannequin, c'est le Rempart qui ferme désormais la marche en S1.

## Mesures après équilibrage (profil standard)

| Scénario | Build | DPS | PV perdus / max | Évités | Survie (s) | Allié (s) | Blocs | Parades | Compétences | Ultimes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| S1 | Rempart | 20,65 | 0 / 440 | 0 | 600 | — | 0 | 0 | 24 | 0 |
| S1 | Contrecoup | 23,08 | 0 / 400 | 0 | 600 | — | 0 | 0 | 20 | 0 |
| S1 | Gardien | 21,2 | 0 / 440 | 0 | 600 | — | 0 | 0 | 24 | 0 |
| S1 | Hybride | 17,33 | 0 / 440 | 0 | 600 | — | 0 | 0 | 18 | 0 |
| S1 | Niveau 10 | 21,4 | 0 / 400 | 0 | 600 | — | 0 | 0 | 21 | 0 |
| S2 | Rempart | 23,46 | 177 / 440 | 6391 | 298,3 | — | 38 | 39 | 13 | 4 |
| S2 | Contrecoup | 89,36 | 400 / 400 | 1599 | † 39 | — | 10 | 12 | 16 | 3 |
| S2 | Gardien | 27,28 | 440 / 440 | 4638 | † 103,5 | — | 15 | 31 | 31 | 3 |
| S2 | Hybride | 57,01 | 440 / 440 | 4679 | † 90 | — | 15 | 30 | 30 | 3 |
| S2 | Niveau 10 | 46,89 | 400 / 400 | 2383 | † 57 | — | 11 | 17 | 19 | 2 |
| S3 | Rempart | 36,32 | 236 / 440 | 3714 | 111,9 | — | 42 | 28 | 11 | 3 |
| S3 | Contrecoup | 146,05 | 400 / 400 | 842 | † 21,5 | — | 9 | 9 | 10 | 2 |
| S3 | Gardien | 33,5 | 379 / 440 | 3091 | 69,7 | — | 17 | 26 | 20 | 3 |
| S3 | Hybride | 64,95 | 440 / 440 | 1593 | † 31,5 | — | 13 | 13 | 10 | 1 |
| S3 | Niveau 10 | 52,96 | 400 / 400 | 1007 | † 23 | — | 9 | 10 | 8 | 1 |
| S4 | Rempart | 23,98 | 0 / 440 | 0 | 900 | † 12 | 0 | 0 | 37 | 0 |
| S4 | Contrecoup | 41,62 | 60 / 400 | 340 | 600 | † 11 | 5 | 4 | 35 | 2 |
| S4 | Gardien | 22,17 | 80 / 440 | 952 | 495 | † 19,5 | 10 | 11 | 31 | 2 |
| S4 | Hybride | 22,38 | 55 / 440 | 0 | 720 | † 13 | 0 | 0 | 28 | 0 |
| S4 | Niveau 10 | 31,63 | 140 / 400 | 540 | 257,1 | † 12 | 6 | 7 | 37 | 2 |
| S5 | Rempart | 22,5 | 440 / 440 | 1849 | † 50,5 | — | 11 | 11 | 10 | 1 |
| S5 | Contrecoup | 72,09 | 400 / 400 | 452 | † 22 | — | 4 | 4 | 11 | 1 |
| S5 | Gardien | 26,48 | 440 / 440 | 1419 | † 46 | — | 7 | 10 | 16 | 1 |
| S5 | Hybride | 44,8 | 440 / 440 | 1434 | † 40 | — | 7 | 9 | 13 | 1 |
| S5 | Niveau 10 | 45,41 | 400 / 400 | 582 | † 22 | — | 4 | 5 | 9 | 0 |

(† : mort à cet instant ; survie d'un Bastion vivant à la fin : estimation durée × PV max / PV perdus, plafonnée à 10 fois la durée.)

## Écarts entre avant et après (tous profils)

Combats dont une mesure principale change ; tous les autres sont identiques.

| Profil | Scénario | Build | DPS | Survie (s) | Évités | Allié (s) |
|---|---|---|---:|---:|---:|---:|
| débutant | S1 | Gardien | 16,8 → 21,2 | 600 → 600 | 0 → 0 | = |
| standard | S1 | Gardien | 16,8 → 21,2 | 600 → 600 | 0 → 0 | = |
| expert | S1 | Gardien | 16,8 → 21,2 | 600 → 600 | 0 → 0 | = |
| débutant | S1 | Niveau 10 | 16,8 → 21,4 | 600 → 600 | 0 → 0 | = |
| standard | S1 | Niveau 10 | 16,8 → 21,4 | 600 → 600 | 0 → 0 | = |
| expert | S1 | Niveau 10 | 16,8 → 21,4 | 600 → 600 | 0 → 0 | = |
| débutant | S2 | Gardien | 13,71 → 12,95 | † 31,5 → † 31,5 | 1047 → 1238 | = |
| standard | S2 | Gardien | 24,92 → 27,28 | † 111 → † 103,5 | 5111 → 4638 | = |
| expert | S2 | Gardien | 38,15 → 41,8 | 1200 → 1200 | 5920 → 5744 | = |
| débutant | S2 | Niveau 10 | 18,67 → 22,29 | † 22,5 → † 21 | 668 → 585 | = |
| standard | S2 | Niveau 10 | 46,58 → 46,89 | † 57 → † 57 | 2398 → 2383 | = |
| expert | S2 | Niveau 10 | 65,95 → 63,4 | 1200 → 1200 | 5904 → 5904 | = |
| débutant | S3 | Gardien | 11,29 → 14,12 | † 17 → † 17 | 723 → 667 | = |
| standard | S3 | Gardien | 30,45 → 33,5 | † 53 → 69,7 | 2692 → 3091 | = |
| expert | S3 | Gardien | 50,15 → 54,45 | 600 → 600 | 2850 → 2955 | = |
| débutant | S3 | Niveau 10 | 18,75 → 24,86 | † 16 → † 14 | 633 → 539 | = |
| standard | S3 | Niveau 10 | 62,42 → 52,96 | † 31 → † 23 | 1355 → 1007 | = |
| expert | S3 | Niveau 10 | 91,7 → 88,95 | 600 → 600 | 2920 → 2980 | = |
| débutant | S4 | Gardien | 12,8 → 17,6 | 135,2 → 262,3 | 791 → 673 | = |
| standard | S4 | Gardien | 17,9 → 22,17 | 792 → 495 | 962 → 952 | = |
| expert | S4 | Gardien | 29,17 → 33,2 | 900 → 900 | 1452 → 1532 | = |
| débutant | S4 | Niveau 10 | 20,07 → 25 | 159,3 → 128,6 | 634 → 460 | = |
| standard | S4 | Niveau 10 | 27,43 → 31,63 | 257,1 → 257,1 | 580 → 540 | = |
| expert | S4 | Niveau 10 | 42 → 44,97 | 900 → 900 | 808 → 808 | = |
| débutant | S5 | Gardien | 11,77 → 15,51 | † 26,5 → † 32,5 | 669 → 834 | = |
| standard | S5 | Gardien | 19,47 → 26,48 | † 49 → † 46 | 1627 → 1419 | = |
| expert | S5 | Gardien | 28,46 → 30,63 | † 82 → † 76 | 3126 → 2824 | = |
| débutant | S5 | Niveau 10 | 22,36 → 27,16 | † 22 → † 19 | 444 → 372 | = |
| standard | S5 | Niveau 10 | 44,32 → 45,41 | † 22 → † 22 | 586 → 582 | = |
| expert | S5 | Niveau 10 | 54,51 → 58,55 | † 35,5 → † 31 | 1062 → 948 | = |

## Lecture des écarts

- **S1 (mannequin)** : la Provocation rapporte 4,4 de DPS au Gardien (0,8 × 60 / 12 s, plus quelques critiques) ; elle se lance sur le mannequin (provocation refusée `notApplicable`, +5 Résonance). Les trois références tiennent dans 10,5 % ; le Contrecoup reste en tête (Main lourde, Fureur contenue).
- **Le Contrecoup domine le DPS partout et meurt le premier en S2, S3 et S5** : ses ripostes de parade (Riposte précise 1,25, Jugement du colosse 1,2 de zone) font l'essentiel de ses dégâts en S2, S3 et S5 (+281 % en S2), mais sa garde plus faible et l'absence de boucliers le tuent tôt (39 s en S2, 21,5 s en S3). Sur la durée de vie, ses dégâts totaux restent du même ordre que ceux du Rempart (S2 : 3 485 contre 2 815). L'objectif ne fixe qu'un minimum (+25 %) : l'écart n'a pas été réduit.
- **Le Rempart survit** : garde incassable, Peau de fer et Poids du rempart ; en S2, il est encore vivant à 120 s (estimation 298 s). En S5, les écrasements imparables l'usent quand même (50,5 s), mais deux fois moins vite que le Contrecoup.
- **Le Gardien protège** : en S4, l'allié tient 19,5 s contre 12 s avec les deux autres (Serment du gardien et la Barrière d'acier liée y font l'essentiel ; la Provocation ne change pas ce chiffre). En S3, la Provocation, désormais lancée pour ses dégâts sur un loup qui le vise déjà, coûte de l'endurance et fait quitter la garde un tick toutes les 12 s ; le Gardien y survit pourtant un peu mieux (vivant à 60 s, 69,7 s estimées, contre une mort à 53 s) ; l'effet net d'un lancer de plus sur la garde, l'endurance et la Résonance n'est pas décomposé ici. Le Rempart reste le plus endurant de la meute (111,9 s).
- **Charge** : 2,2 % au plus d'un budget (S3), très loin des 50 %.

## Limites

- Une seule passe, un seul changement : la marge de S4 (1,63 pour 1,5 exigé) est la plus étroite ; les autres objectifs ont de larges marges.
- Objectifs mesurés au seul profil standard ; aux profils débutant et expert, les rapports diffèrent (tableau complet : `eds report`).
- Les choix du pilote (garde, priorités, cibles) pèsent sur les chiffres ; ils sont identiques pour tous les builds ([LIMITES_LOT_8.md](LIMITES_LOT_8.md#sous-lot-86--interprétations-et-limites)).
- Le relevé du 8.6 reste reproductible avec le Bastion figé du 8.5 (`tests/EdS.Data.Tests/Fixtures/content-0.4.0`, `Lot87BalanceTests`).
