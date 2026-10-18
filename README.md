# Éclats du Seuil — simulateur de combat

Simulateur de combat C# du futur MMORPG *Éclats du Seuil*, **indépendant de Unity**, sans réseau ni interface graphique.
Le plan complet est dans [PLAN_SIMULATEUR_V0_1.md](PLAN_SIMULATEUR_V0_1.md).

> Toutes les valeurs de jeu sont **provisoires et non équilibrées**.

## Structure

| Projet | Cible | Rôle |
|---|---|---|
| `src/EdS.Domain` | netstandard2.1, C# 9 | Virgule fixe (`Fixed`), temps (`TickRate`), attributs et statistiques (`Stats`), formule de dégâts, règles et description d'un combat (`Combat`), classes, talents et validateur de builds (`Builds`) — aucune dépendance |
| `src/EdS.Simulation` | netstandard2.1, C# 9 | Moteur de combat déterministe (`Engine` : horloge logique, file d'événements, commandes, garde, parade, ressources, états, assaillant, journal JSON Lines, rejeu), RNG (`Pcg32`), tirage critique — dépend du domaine seulement |
| `src/EdS.Data` | netstandard2.1, C# 9 | Modèle de données, catalogue du format, validateur commun, empreinte, scénarios — dépend du domaine seulement |
| `src/EdS.Data.Json` | net10.0 | Adaptateur System.Text.Json, chargement et rejeu, réservé aux outils .NET |
| `src/EdS.Cli` | net10.0 | Outil en ligne de commande (`rng`, `validate`, `simulate`, `replay`) |
| `data/` | JSON | `v0/rules.json` (paramètres d'équilibrage et de progression), `v0/classes/bastion.json` (arbres, talents, compétences), `schemas/` |
| `scenarios/` | JSON | Scénarios de combat (`mannequin.json`, `entrainement_garde_parade.json`, `entrainement_rupture.json`, `entrainement_alterations.json`, `entrainement_declencheurs.json`) |
| `tests/*` | net10.0 | xUnit + FsCheck (tests de propriétés) |

Les projets `netstandard2.1` sont limités à C# 9 et n'ont aucune dépendance externe : ils sont importables tels quels dans Unity (IL2CPP) et sur un serveur .NET.

## Commandes

```bash
dotnet build -warnaserror      # compilation, avertissements bloquants
dotnet test                    # tous les tests
dotnet test --filter Category=Numeric
dotnet test --filter Category=Formula
dotnet test --filter Category=Data
dotnet test --filter Category=Build
dotnet test --filter Category=Engine
dotnet test --filter Category=Resources   # garde, parade, ressources, états (lot 6)
dotnet test --filter Category=Regression  # non-régression du correctif 6.1 (M01 à M05)
dotnet test --filter Category=Limits      # admission, budgets et capacités (sous-lot 7.1)
dotnet test --filter Category=Status      # altérations : DOT, contrôles, immunités, résistances (sous-lot 7.2)
dotnet test --filter Category=Trigger     # déclencheurs, provenance, cycles, profondeur, report (sous-lot 7.3)
dotnet test --filter Category=Modifier    # modificateurs X1 et X2, règles effectives, plafonds (sous-lot 8.1)
dotnet test --filter Category=Shield      # renforcements et boucliers (sous-lot 8.2)
dotnet test --filter Category=Support     # soutien, provocation, protection, rôle allié (sous-lot 8.3)
dotnet test --filter Category=Bastion     # fiches du Bastion, Contrecoup, ripostes de zone (sous-lot 8.4)
scripts/test-category.sh -c Release Numeric Formula Data Build Engine Resources Regression Limits Status Trigger Modifier Shield Support Bastion   # échoue si une catégorie est inconnue, vide, ou absente d'un projet qui la déclare
dotnet run --project src/EdS.Cli -- validate
dotnet run --project src/EdS.Cli -- validate data/v0/classes/bastion.json
dotnet run --project src/EdS.Cli -- rng --seed 42 --count 6
dotnet run --project src/EdS.Cli -- simulate scenarios/mannequin.json --out combat.jsonl
dotnet run --project src/EdS.Cli -- simulate scenarios/entrainement_garde_parade.json
dotnet run --project src/EdS.Cli -- replay combat.jsonl
```

Prérequis : SDK .NET 10 (voir `global.json`).

## Décisions validées (revue du plan v0.1)

1. Tick initial de 50 ms ; conversion ms → ticks isolée dans `TickRate` pour pouvoir essayer 20 ms.
2. Parade parfaite provisoire : fenêtre de 150 ms, −90 % des dégâts éligibles, +1 Charge de rempart.
3. Limites de déclencheurs conservées ; toute suppression est journalisée explicitement.
4. Compatibilité Unity/IL2CPP de chaque bibliothèque vérifiée avant intégration dans un projet `netstandard2.1`.
5. Puissance d'équipement prévue dans les statistiques offensives, configurable, jamais codée en dur.
6. Un seul talent majeur par build au niveau 30 (voulu) ; les builds sans talent majeur restent valides.

Arbitrages du lot 2 : base offensive 0 et +2 puissance par Force/Dextérité/Intelligence (D1) ; trois puissances d'équipement ajoutées avant le coefficient (D2) ; armure et résistance magique indépendantes, base 0, constante 1000 chacune (D3) ; bonus additifs par catégorie (compétence, talent, altération) et multipliés entre catégories, jamais négatifs (D4). Détail et exemple chiffré : [docs/FORMULES.md](docs/FORMULES.md).

Arbitrages du lot 3 : PV = 100 + 10 × Vitalité + contributions (E1) ; cœur indépendant de toute bibliothèque de sérialisation, System.Text.Json dans un adaptateur réservé aux outils .NET, un seul validateur de gameplay (E2) ; `rules.json` versionné avec empreinte stable (E3). Détail : [docs/DONNEES.md](docs/DONNEES.md).

Arbitrages du lot 4 : moteur de talents générique, mini-arbre Bastion avec talents de test « [TEST] » pour atteindre les seuils (F1) ; 1 point par rang, prérequis de rang (F2) ; points non dépensés autorisés (F3) ; pas de compétence en double, niveau minimal et prérequis de talents par compétence, emplacement d'ultime au niveau 10 (F4) ; attributs fournis par le build (F5) ; un fichier par classe, versionné, avec empreinte (F6). Détail : [docs/BUILDS.md](docs/BUILDS.md).

Arbitrages du lot 5 : endurance +10/s après 1 s sans dépense, plafonnée, refus atomique si coût impayable, pas de garde brisée (A3) ; compétences instantanées, GCD 1 s configurable, recharge au lancer (A6) ; ciblage logique sans positions, zones dans un ordre déterministe (A7) ; ordre intra-tick expirations → commandes (initiative puis identifiant) → résolution → périodiques → régénération → morts et fin (A10) ; Frappe du rempart 1,20 / 10 / 6 s et Heurt de bouclier 0,80 / 15 / 10 s provisoires ; mannequin à PV infinis. Détail : [docs/MOTEUR.md](docs/MOTEUR.md).

Arbitrages du lot 6 : garde en posture (`guardStart`/`guardStop`), −60 % après armure, 2 endurance/s et 10 par impact bloqué, rupture de 3 s si l'endurance manque, régénération suspendue (A1) ; parade parfaite horodatée, fenêtre [P, P + 3 ticks[, −90 %, +1 Charge, récupération de 500 ms après échec, prioritaire sur la garde sans cumul (A2) ; Charges de rempart 0 à 5, sans gain passif ni décroissance, remises à zéro en fin de combat (A4) ; Résonance 0 à 100, +3 par attaque réussie (une fois par action), +8 par parade, +2 par blocage, ultime à 100, sans génération récursive (A5) ; Fragilité +10 % pendant 6 s renouvelée sans cumul, rupture de garde de 3 s (A19) ; assaillant d'entraînement à PV infinis, 100 bruts toutes les 2 s, sans critique, attaques bloquables ou imparables (A21). Détail : [docs/MOTEUR.md](docs/MOTEUR.md).

Correctif 6.1 (stabilisation, avant le lot 7) : entretien de garde cumulé sur tout le combat, identité et génération des états temporaires, validateurs sans dépassement, limites numériques vérifiées avant le premier tick, collections figées, formats de règles et de classes en version 2 sans migration garantie (pré-alpha), moteur 0.6.1 et journal au format 2. Contrat de limites du lot 7 : [docs/LIMITES_LOT_7.md](docs/LIMITES_LOT_7.md).

Correctif 6.1.1 (contre-audit CA-M01) : précontrôle numérique étendu aux défenses réelles de tous les participants, personnages compris, et aux deux types de dégâts ; constantes de défense bornées à 10⁹ ; recharge globale et délai de régénération bornés à 86 400 000 ms. Ces plafonds sont des enveloppes techniques provisoires de pré-alpha, distinctes des paramètres d'équilibrage et des capacités mesurées. Aucun changement de journal pour les scénarios de référence.

Sous-lot 7.2 (altérations) : catalogue d'altérations en données (`statuses` de `rules.json`, schéma 4) ; brûlure et saignement, DOT de 6 s toutes les 2 s, réduits par la résistance magique ou l'armure relue à chaque tick, sans critique, renouvelés par source à la plus forte puissance en gardant leur cadence ; étourdissement de 1,5 s qui bloque les actions et interrompt garde et parade, suivi de 3 s d'immunité ; gel de 4 s (−30 % de déplacement enregistré, ne bloque rien) ; résistance aux altérations qui réduit la durée des contrôles (K = 1000) ; Fragilité et rupture de garde migrées sans changement de comportement ; moteur 0.8.0, journal au format 3. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#altérations-a12-à-a15-a19-a23--sous-lot-72).

Correctif 7.2 (arbitrages après revue du sous-lot 7.2) : étourdissement en cumul `ignore` (refus `active`, immunité toujours accordée à l'expiration sauf décès) ; DOT à cadence d'origine avec fin effective alignée sur les ticks et journalisée (`untilTick`, `nextTick`) ; une parade parfaite empêche les altérations de l'impact paré (`parried`), pas la garde normale ni les attaques imparables ; budgets inchangés (1000 événements par tick, 100 000 en file) et contrôle prudent de saturation de la file avant le premier tick ; résistance K = 1000 et +2 par point de Volonté conservées ; DOT maintenus après la mort de leur source. Provenance causale des altérations pour le sous-lot 7.3 : [docs/LIMITES_LOT_7.md](docs/LIMITES_LOT_7.md#provenance-causale-des-altérations-préparation-du-sous-lot-73-correctif-72). Moteur 0.8.1, journal au format 3.

Sous-lot 7.3 (déclencheurs et synergies) : déclencheurs en données sur les talents, compétences équipées, altérations et assaillants de scénario (équipement réservé) ; provenance de chaque effet au journal (`eid`, `cause`, `root`, `depth`, `trigger`, format 4) ; notification à l'émission dans un ordre total, recharge interne, règle de cycle par identité de déclencheur, profondeur 4 (plafond 8), suppressions journalisées (`triggerSuppressed`) ; réactions réservées entières avant toute mutation (frappe sans critique, application, gain de ressource jamais notifié) ; report au tick suivant (`deferredFromTick`, `deferredDropped`) et annulations journalisées (`reactionCancelled`) ; majorant de file non garanti dès qu'un déclencheur existe. Gameplay des quatre scénarios livrés inchangé. Moteur 0.9.0, journal au format 4. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#déclencheurs-sous-lot-73).

Correctif 7.3.1 (audit Astra de la PR #2) : la réservation crédite le retrait de l'événement traité (un combat au majorant garanti ne s'arrête plus sur la file) ; précontrôle numérique des DOT par leur formule périodique réelle, compteurs préparés et vérifiés avant mutation ; empreintes au contrat canonique 2, sans sentinelle `none` ; cycles statiques en temps linéaire et plafonds globaux de déclencheurs (512 par classe, 4 096 par combat). Moteur 0.9.1 ; les journaux 0.9.0 sont refusés au rejeu. Cinquième scénario de référence, `entrainement_declencheurs.json`, le seul avec des déclencheurs : journal figé, simulé, comparé octet par octet et rejoué en CI. Détail : [docs/LIMITES_LOT_7.md](docs/LIMITES_LOT_7.md#correctif-731-audit-astra-de-la-pr-2).

Sous-lot 8.1 (modificateurs, moteur seulement) : modificateurs permanents (X1) et conditionnels (X2 : `whileGuarding`, `perRampartCharge`, `perResonanceStep`) sur les talents et les compétences passives, en données (schéma de classe 5), validés strictement avec chemins ; règles effectives par combattant partagées par la validation et le moteur ; réduction de garde plafonnée à 75 % et réduction totale des dégâts subis à 85 % (avant boucliers) ; précontrôle numérique et majorant de file sur les valeurs effectives ; valeurs effectives au journal (`rules` sur `combatant`). Aucun contenu du Bastion n'est encore modifié : gameplay des cinq scénarios livrés inchangé. Moteur 0.10.0, journal au format 5. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#modificateurs-sous-lot-81), [docs/DONNEES.md](docs/DONNEES.md#modificateurs-sous-lot-81), [docs/LIMITES_LOT_8.md](docs/LIMITES_LOT_8.md).

Décisions du 18/10/2026 (questions du rapport 8.1) : plafonds de réduction dans `rules.json` (`combat.guard.maxDamageReduction` 0,75, `combat.damageTaken.maxReduction` 0,85 ; schéma de règles 6, contenu 0.6.0) ; un `perRank` de déclencheur sur un talent à un seul rang est refusé ; Rempart éternel sans raccourci de rupture (contenu 8.5) ; blocage à coût nul inchangé. Moteur 0.11.0 (journal identique hors en-tête).

Sous-lot 8.2 (renforcements et boucliers, moteur seulement) : catégorie d'altération `buff` qui porte des modificateurs temporaires (dégâts infligés, dégâts subis sous le plafond total, entretien de garde) et catégorie `shield` (un bouclier par source, valeur = PV max de la source × coefficient × (1 + `shield.potencyDealt`), renouvelé au plus fort, absorbe dégâts directs et DOT après la défense et avant les PV, dans l'ordre de création, retiré épuisé en fin de tick) ; entretien de garde cumulé au taux du moment. Aucune donnée livrée n'en déclare : gameplay des cinq scénarios inchangé. Schéma de règles 7, schéma de classe 6 (les fichiers 6 et 5 sont refusés sans migration), moteur 0.12.0, journal au format 6. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#renforcements-et-boucliers-sous-lot-82), [docs/DONNEES.md](docs/DONNEES.md#renforcements-et-boucliers-sous-lot-82), [docs/LIMITES_LOT_8.md](docs/LIMITES_LOT_8.md#sous-lot-82--interprétations-et-limites).

Sous-lot 8.3 (soutien, moteur seulement) : compétences de soutien sans dégâts et ciblages `self`, `ally`, `allAllies` (X5) ; applications à cible propre (`applies[].target`) ; provocation (`taunt`, X7) : un assaillant provoqué vise la source de sa provocation la plus récente ; protection (`protection`, X6) : une part des dégâts d'un allié transférée à son protecteur, jamais retransférée ni notifiée, par sa garde seulement avec `protection.redirectThroughGuard` ; provocations et protections retirées à la mort de leur source ; rôle `ally` des déclencheurs (X9). Aucune donnée livrée n'en déclare : gameplay des cinq scénarios inchangé. Règles en schéma 8, classes en 7, scénarios en 3 (versions antérieures refusées sans migration), moteur 0.13.0, journal au format 7. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#provocation-protection-et-rôle-allié-sous-lot-83), [docs/LIMITES_LOT_8.md](docs/LIMITES_LOT_8.md#sous-lot-83--interprétations-et-limites).

Sous-lot 8.4 (fiches du Bastion) : toutes les compétences du Bastion ont leur fiche complète (feuille de route), plus aucune n'est refusée `skillNotImplemented` : Heurt de bouclier étourdit, Onde de rupture fragilise tous les ennemis, Contrecoup dépense toutes les Charges (0,6 + 0,4 par Charge, X8), Provocation provoque 3 s et rapporte 5 de Résonance, Barrière d'acier pose un bouclier de 15 % des PV max, et l'ultime Bastion absolu (100 de Résonance) renforce le Bastion et ses alliés (−30 % de dégâts subis), lui donne une garde sans entretien (`guard.noUpkeep`) et provoque tous les assaillants pendant 8 s. Toute riposte automatique de zone a au moins 3 s de recharge interne (L8-D2, `combat.triggers.areaStrikeMinCooldownMs`). Le gameplay des scénarios livrés change (étourdissement, Provocation lancée) ; le moteur reproduit l'ancien sur le contenu historique (fixture `content-0.2.0`). Règles en schéma 9 (contenu 0.7.0), classes en 8 (contenu 0.3.0), moteur 0.14.0, journal au format 8. Détail : [docs/MOTEUR.md](docs/MOTEUR.md#fiches-du-bastion-et-ripostes-de-zone-sous-lot-84), [docs/BUILDS.md](docs/BUILDS.md), [docs/LIMITES_LOT_8.md](docs/LIMITES_LOT_8.md#sous-lot-84--interprétations-et-limites).

Les autres propositions de la section 6 du plan sont des valeurs initiales configurables.

## Avancement

- [x] Lot 0 — socle (solution, projets, CI)
- [x] Lot 1 — virgule fixe, conversion du temps, RNG PCG32
- [x] Lot 2 — attributs, statistiques dérivées, défenses, bonus, critiques, formule de dégâts
- [x] Lot 3 — données JSON, validation structurelle et sémantique, rules.json
- [x] Lot 4 — classes, talents, compétences équipables, validateur de builds
- [x] Lot 5 — moteur de simulation minimal (horloge, commandes, recharges, endurance, dégâts directs, journal, rejeu)
- [x] Lot 6 — ressources du Bastion, garde, parade parfaite, états défensifs, assaillant d'entraînement
- [x] Correctif 6.1 — stabilisation (entretien de garde, identité des états, dépassements, immuabilité, formats v2), en attente de contre-audit
- [x] Lot 7 — limites et budgets (7.1), altérations (7.2), déclencheurs (7.3), correctifs 7.2 et 7.3.1 (PR #2 fusionnée)
- [ ] Lot 8 — en cours : sous-lot 8.1 (modificateurs X1 et X2, moteur 0.10.0 puis 0.11.0), sous-lot 8.2 (renforcements et boucliers, moteur 0.12.0) sous-lot 8.3 (soutien, provocation, protection, moteur 0.13.0) et sous-lot 8.4 (fiches du Bastion, moteur 0.14.0) en revue
