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

## Réglages du sous-lot V1.8 (progression et équilibrage)

Chaque chiffre provisoire des chantiers V1.1 à V1.7, réglé ou gardé, avec sa raison. Les combats simulés par chapitre (`V18EquilibreTests`) vérifient l'ensemble.

**Zones en mélange** (zones en schéma 7, sujet 4) :
- Niveau d'une créature : `max(niveau minimum de son lieu, niveau du joueur − easeLevels)` ; `easeLevels` = **2** : deux niveaux sous le joueur, assez pour « se sentir puissant » sans que la zone devienne triviale (au niveau 25, une créature a environ 6 % de PV et 7 % de dégâts de moins qu'à niveau égal ; au niveau 10, environ 12 % et 18 %).
- Les chiffres des créatures du scénario sont ceux du niveau **1** (`baseLevel`, rééquilibrées pour un débutant à l'essai du 12.9), ceux des formes des chefs du niveau **5** (`bossBaseLevel` : le Gardien et le chef des bandits avaient été réglés pour un Bastion de niveau 10 *sans* l'équipement typique de son niveau ; avec lui, il faut les compter plus bas pour que le Gardien demande garde et parade au niveau 25).
- Par niveau de plus : **+12 % de PV** et **+30 % de dégâts bruts** (de la valeur écrite). Réglés par les combats simulés (`V18EquilibreTests`) : avec +8 % et +6 %, un Bastion typique gagnait tout sans se défendre dès le chapitre 2, car son équipement monte bien plus vite que ses attributs (arme de 10 à 70 de puissance, armure de 60 à plus de 900 : l'armure absorbe une grande part des dégâts bruts). Les dégâts doivent donc suivre l'armure ; les PV montent moins pour que les combats ne s'allongent pas trop.
- Résultat mesuré (Bastion typique, sans talent) : une créature seule se bat sans défense avec plus de la moitié des PV à la fin à chaque chapitre ; un groupe entame vraiment le Bastion qui ne se défend pas (il finit à 34 à 66 % de ses PV, et **tombe au chapitre 5**), alors qu'avec garde et parades il gagne toujours ; le **Gardien** au niveau 25 gagne contre un Bastion sans défense ni potion et perd contre un Bastion qui garde, pare et a cinq potions.
- Niveaux minimums des lieux des Vallons, par chapitre : 1 (village, moulin, rivière, pré nord, forge, bourg, champs), 6 (ruines, lac, vergers, hameau, carrière), 11 (forêt des Loups, plaine de l'est, tour de guet), 16 (hautes pierres, camp des rôdeurs), 21 (le Seuil, le Gardien).
- L'expérience d'une créature suit ses PV (1 point par tranche de 6 PV depuis V1.8b, 5 avant) : elle monte avec elle.

**Usure et forge** (catalogue en version 6, métiers en version 3 ; réponses de Damien du 31 octobre 2026) :
- `perFullHealth` **3** (au lieu de 10) : un combat difficile coûte environ une fois les PV maximaux (potions comprises) ; une pièce commune de niveau 1 perd 3 points sur 100 par combat difficile, soit **environ 33 combats difficiles**, dans la cible de 30 à 40.
- Résistance à l'usure : **+1 % par niveau requis** au-dessus du premier, et par rareté **0, 20, 40, 60, 80, 100 %** (commune à mythique) ; chaque pièce du combat perd `(dégâts × perFullHealth × 100 + reste) / (PV maximaux × (100 + résistance))` points, arrondi vers le bas, avec son propre reste gardé d'un combat à l'autre dans la session (pas de paliers d'arrondi). Une pièce commune de niveau 10 dure environ 36 combats difficiles, une rare de niveau 10 environ 43, une très rare de niveau 18 environ 52.
- **Un bouclier usé à zéro retombe sur la garde de base, jamais meilleure que neuve** (décision de Damien, déjà le cas en V1.4 : voir « Bouclier usé à zéro » ci-dessus).
- **Réparation** : de tout d'un coup (`Repair`) ou pièce par pièce (`RepairPiece`), même prix par pièce : `⌈valeur × 30 % × manque / solidité⌉`, au moins 5 cuivre (gardés : réparer une pièce à moitié usée coûte 15 % de sa valeur, moins que l'écart avec une neuve).
- **Vente d'une pièce usée** : `⌊prix plein × solidité restante / solidité pleine⌋`, au moins **1 cuivre** (décision de Damien) ; le rachat se fait au prix payé.
- **Recette ratée** : la moitié des matériaux rendue (gardé) et **un quart de l'expérience de la recette** (`failure.xpPercent` 25, au moins 1 ; décision de Damien).
- Chance de rater : **35 % par niveau manquant**, deux niveaux au plus, **−25 % pour trois frappes parfaites** (gardés : à un niveau, de 35 % à 10 % selon l'adresse ; à deux niveaux, 70 % à 45 %, une vraie prise de risque). **L'herboristerie garde le risque de rater au-dessus de son niveau, sans petit jeu** (décision de Damien : l'adresse n'y joue pas, 35 ou 70 %).
- Qualité : supérieure **+10 %**, chef-d'œuvre **+20 %** du bonus de base (gardés : un chef-d'œuvre vaut une pièce d'un ou deux niveaux plus haut, pas plus).

**Propriétés tirées** : nombre par rareté **0, 1, 2, 3, 4, 5** (commune à mythique, choix de Damien) ; échelle **100, 100, 110, 120, 135, 150 %** (gardée : de 100 à 150 %, la mythique vaut une fois et demie la commune).

**Boucliers** (règles, bloc `combat.shield`, gardés : changer les règles demanderait de nouvelles références figées sans raison de jeu) : part bloquée de **45 à 70 %** autour de la garde de base (60 %), coût d'un blocage de **4 à 20** d'endurance autour de 10, fenêtre de parade décalée de **50 ms** au plus. Les boucliers livrés vont de 50 % à 70 % : un grand pavois bloque plus mais coûte plus et pare plus serré (100 ms), une targe bloque moins mais pare plus large (200 ms).

**Prix** (marchands, gardés) : valeur d'un équipement ≈ base de rareté + environ 250 cuivre par niveau requis (commune de niveau 1 : 250 ; rare de niveau 4 : 1 200 ; rare de niveau 10 : 3 100 ; très rare de niveau 18 : 9 200 ; épique de niveau 19 : 16 400) ; revente à 25 % (30 % chez Bertille), sans profit au rachat.

**Guilde** (gardés) : rang **F à 100 points**, **E à 300**. Un contrat du rang G rapporte 10 à 15 points, une quête secondaire 20, une découverte 5 : F arrive au chapitre 2 (niveaux 6 à 10), E au chapitre 3 ou 4, quand l'épreuve E (le loup blanc, niveau 11 au moins, et le Borgne, niveau 16 au moins) devient jouable. Paiement : G 70 à 90 cuivre, F 200 à 400, E 600 à 800 ; **contrats pressés** environ **+40 à +60 %** d'argent et de points (à finir dans la journée du monde, sans pénalité).

**Loup blanc** : **35 % par nuit** (gardé). Une nuit revient toutes les 3 heures de jeu (journée de 3 heures, sujet 32) : le loup blanc apparaît en moyenne une fois toutes les 8 à 9 heures de jeu, rare sur les plus de 5 heures des Vallons ; une quête, l'épreuve E ou un contrat en cours le font venir à coup sûr.

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
