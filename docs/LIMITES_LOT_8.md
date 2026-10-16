# Limites du lot 8 (sous-lot 8.1 : modificateurs)

Ce document complète [LIMITES_LOT_7.md](LIMITES_LOT_7.md) pour les modificateurs du sous-lot 8.1. Les bornes ci-dessous sont des **enveloppes techniques provisoires** (pré-alpha) ou des **plafonds de jeu** ; les valeurs d'équilibrage restent dans les données et sont provisoires.

## Plafonds de jeu

| Plafond | Valeur | Où | Effet |
|---|---|---|---|
| Réduction de garde (B1) | 75 % | `CombatRules.MaxGuardDamageReduction` | base de `rules.json` refusée au-delà ; valeur effective écrêtée |
| Réduction totale des dégâts subis (B2) | 85 % | `CombatRules.MaxTotalDamageReduction` | garde × `damageTaken`, avant l'absorption par un bouclier ; la parade (90 %) reste hors plafond |
| Fenêtre de parade | ⌊500 / tick⌋ × tick | `CombatantRules.MaxParryWindowMs` | valeur effective écrêtée, plancher 1 tick |
| Charges de rempart max | 10 | `CombatantRules.MaxRampartCharges` | valeur effective écrêtée |
| PV max | −90 % à +1 000 % | `CombatantRules.MinMaxHealthPercent`, `MaxMaxHealthPercent` | pourcentage résultant écrêté |

Quand une base des règles sort déjà d'un intervalle de jeu (règles de test), l'intervalle est élargi jusqu'à elle : sans modificateur, la valeur effective est toujours la base. Les deux plafonds B1 et B2 sont des constantes du moteur en 8.1 (schéma de règles 5 inchangé) ; pour que l'équilibrage du lot 8.7 reste « données seulement », ils deviendront des paramètres de `rules.json` à la prochaine version du schéma de règles.

## Écrêter ou refuser

- **Refus** (erreur de données, avec chemin, avant tout combat) : clé inconnue, condition non admise, doublon, valeur déclarée hors de sa borne au rang 1 ou au rang maximal, entier ou ms non entier, ms non multiple du tick (ou de la période d'un DOT), drapeau ≠ 1, `perRank` hors talent, `damageTaken` positif, modificateur sur une compétence active ou ultime, plus de 8 par porteur ou de 512 par classe. Pour une règle à plafond absolu, la borne d'une valeur déclarée est la largeur du plafond : un modificateur seul ne peut pas le dépasser.
- **Écrêtage** (jamais une erreur) : la somme de plusieurs sources légales. Une combinaison de choix valides ne rend pas un combat invalide ; la valeur appliquée est écrite dans la ligne `combatant` (`rules`).
- **Inertes** (acceptées, documentées) : `guard.reductionAtZeroEndurance` sans `guard.noUpkeepBreak` chez le même porteur ; `guard.breakShortenPerCharge` avec `guard.noUpkeepBreak` (aucune rupture liée à l'endurance ne reste à raccourcir).

## Absence de débordement

- **Sommes.** Au plus 512 modificateurs par classe ; |valeur| ≤ 8,64·10⁷ ms, soit 8,64·10¹¹ en brut : |Σ| < 4,5·10¹⁴ < 2⁶³. Addition entière exacte : l'ordre des sources est sans effet.
- **Conditionnels.** Charges ≤ max(base, 10) ≤ 2³¹ − 1, Résonance ≤ `resonance.max` ≤ 2³¹ − 1, |Σ conditionnel| ≤ 512 × 10 en valeur, soit 5,12·10⁷ en brut : |terme| < 1,2·10¹⁷.
- **Raccourci de rupture.** valeur ≤ 8,64·10⁷ ms × Charges ≤ 2³¹ − 1 : < 1,9·10¹⁷, calcul vérifié (`checked`).
- **Entretien et régénération.** taux brut ≤ 2,15·10¹³ × (ticks × durée du tick ≤ 8,64·10⁷) ≤ 1,86·10²¹ sur 128 bits (`IntMath.MulDiv`), quotient < 2·10¹⁴.
- **Durées.** Durée ajustée écrêtée à 86 400 000 ms avant la conversion en ticks : le produit par le facteur de résistance reste < 10¹².
- **Dégâts.** Le précontrôle numérique (`CombatSetup.ValidateNumericLimits`) calcule le pire impact avec le coefficient effectif et le bonus « talent » maximal de chaque attaquant ; `damageTaken` ne fait que réduire. Un dépassement est refusé (`NumericLimit`, rattaché au combattant) avant tout journal.

## Ce qui n'est pas couvert en 8.1

- Boucliers (`shield.potencyDealt`, X3, X4), transferts (`protection.redirectRatio`, X6) et provocation (`status.taunt.*`, X7) : sous-lots 8.2 et 8.3. Ces clés sont refusées comme inconnues tant que leur consommateur n'existe pas (une clé sans effet irait contre la validation stricte) ; `status.taunt.*` l'est au combat, `taunt` étant absent du catalogue.
- Conditions multiplicatives combinées (`perRampartCharge` avec `perResonanceStep`) : refusées, faute de contenu prévu et parce que leur mise à l'échelle quadratique serait difficile à équilibrer.
