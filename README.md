# Éclats du Seuil — simulateur de combat

Simulateur de combat C# du futur MMORPG *Éclats du Seuil*, **indépendant de Unity**, sans réseau ni interface graphique.
Le plan complet est dans [PLAN_SIMULATEUR_V0_1.md](PLAN_SIMULATEUR_V0_1.md).

> Toutes les valeurs de jeu sont **provisoires et non équilibrées**.

## Structure

| Projet | Cible | Rôle |
|---|---|---|
| `src/EdS.Domain` | netstandard2.1, C# 9 | Définitions, virgule fixe (`Fixed`), conversion du temps (`TickRate`) — aucune dépendance |
| `src/EdS.Simulation` | netstandard2.1, C# 9 | Moteur de combat, RNG (`Pcg32`) — dépend du domaine seulement |
| `src/EdS.Data` | netstandard2.1, C# 9 | Chargement et validation des données JSON (lot 3) |
| `src/EdS.Cli` | net10.0 | Outil en ligne de commande |
| `tests/*` | net10.0 | xUnit + FsCheck (tests de propriétés) |

Les projets `netstandard2.1` sont limités à C# 9 et n'ont aucune dépendance externe : ils sont importables tels quels dans Unity (IL2CPP) et sur un serveur .NET.

## Commandes

```bash
dotnet build -warnaserror      # compilation, avertissements bloquants
dotnet test                    # tous les tests
dotnet test --filter Category=Numeric
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

Les autres propositions de la section 6 du plan sont des valeurs initiales configurables.

## Avancement

- [x] Lot 0 — socle (solution, projets, CI)
- [x] Lot 1 — virgule fixe, conversion du temps, RNG PCG32
- [ ] Lot 2 — attributs et formules
