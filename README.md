# Éclats du Seuil — simulateur de combat

Simulateur de combat C# du futur MMORPG *Éclats du Seuil*, **indépendant de Unity**, sans réseau ni interface graphique.
Le plan complet est dans [PLAN_SIMULATEUR_V0_1.md](PLAN_SIMULATEUR_V0_1.md).

> Toutes les valeurs de jeu sont **provisoires et non équilibrées**.

## Structure

| Projet | Cible | Rôle |
|---|---|---|
| `src/EdS.Domain` | netstandard2.1, C# 9 | Virgule fixe (`Fixed`), temps (`TickRate`), attributs et statistiques (`Stats`), formule de dégâts (`Combat`), classes, talents et validateur de builds (`Builds`) — aucune dépendance |
| `src/EdS.Simulation` | netstandard2.1, C# 9 | Moteur de combat, RNG (`Pcg32`), tirage critique — dépend du domaine seulement |
| `src/EdS.Data` | netstandard2.1, C# 9 | Modèle de données, catalogue du format, validateur commun, empreinte — aucune dépendance |
| `src/EdS.Data.Json` | net10.0 | Adaptateur System.Text.Json, réservé aux outils .NET |
| `src/EdS.Cli` | net10.0 | Outil en ligne de commande (`rng`, `validate`) |
| `data/` | JSON | `v0/rules.json` (paramètres d'équilibrage et de progression), `v0/classes/bastion.json` (arbres, talents, compétences), `schemas/` |
| `tests/*` | net10.0 | xUnit + FsCheck (tests de propriétés) |

Les projets `netstandard2.1` sont limités à C# 9 et n'ont aucune dépendance externe : ils sont importables tels quels dans Unity (IL2CPP) et sur un serveur .NET.

## Commandes

```bash
dotnet build -warnaserror      # compilation, avertissements bloquants
dotnet test                    # tous les tests
dotnet test --filter Category=Numeric
dotnet test --filter Category=Formula
dotnet test --filter Category=Data
dotnet run --project src/EdS.Cli -- validate
dotnet run --project src/EdS.Cli -- validate data/v0/classes/bastion.json
dotnet run --project src/EdS.Cli -- rng --seed 42 --count 6
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

Les autres propositions de la section 6 du plan sont des valeurs initiales configurables.

## Avancement

- [x] Lot 0 — socle (solution, projets, CI)
- [x] Lot 1 — virgule fixe, conversion du temps, RNG PCG32
- [x] Lot 2 — attributs, statistiques dérivées, défenses, bonus, critiques, formule de dégâts
- [x] Lot 3 — données JSON, validation structurelle et sémantique, rules.json
- [x] Lot 4 — classes, talents, compétences équipables, validateur de builds
