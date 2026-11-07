# Résumé pour les agents — Éclats du Seuil

À lire **à la place** de toute la documentation. Ensuite, chercher seulement les passages utiles avec `grep` (voir la carte plus bas). Détail complet : [CLAUDE.md](../CLAUDE.md).

## 1. Le projet en 3 lignes

- *Éclats du Seuil* : MMORPG fantasy PC/mobile, surtout jouable en solo dans un monde partagé.
- Ce dépôt = le cœur C# du jeu (simulateur de combat déterministe), indépendant d'Unity, du réseau et de l'interface.
- Rôles : **Damien décide** (gameplay, dépenses, **toutes les fusions**) ; **Claude développe** (code, tests, docs, PR) ; **Codex relit** les PR (voir AGENTS.md). Une auto-revue Claude n'est jamais présentée comme une revue Codex.

## 2. Règles absolues

- Ne jamais fusionner une PR sans l'accord explicite de Damien pour cette PR précise. Jamais d'auto-merge, jamais de push sur `main`, jamais de `--force`, jamais de suppression de branche distante.
- Tout passe par une branche et une PR (brouillon tant qu'elle n'est pas prête). Ne jamais réécrire un commit poussé : corriger par un nouveau commit.
- Ne pas modifier `.github/workflows/` sans le signaler dans la PR. Aucun secret nulle part. Ne toucher à aucun autre dépôt.
- Les décisions de gameplay validées ne changent jamais en silence : poser la question à Damien dans la PR.
- Ne pas affirmer sans preuve : distinguer « code écrit », « tests locaux verts », « CI verte », « revu », « fusionné ».
- S'arrêter pour demander seulement pour : arbitrage de gameplay non couvert, dépense, rupture d'architecture, fusion.

## 3. Contrats techniques

- **Déterminisme** : tick logique de 50 ms, aucune horloge réelle, virgule fixe `Fixed` (échelle 10 000), RNG `Pcg32`, ordre d'événements stable, aucun état global mutable, journaux identiques octet par octet.
- **Projets portables** (`EdS.Domain`, `Simulation`, `Data`, `Data.Json`, `Runtime`, `Net`, `World`) : `netstandard2.1`, C# 9, **aucune dépendance externe**, pas d'API .NET récente (cela va dans `EdS.Tools` ou `EdS.Cli`).
- **Unity** compile `src/` directement : tout nouveau projet reçoit un `.asmdef` (mêmes références que le `.csproj`) ; aucune sortie de compilation dans `src/`. Scripts `unity/Assets` : C# 9, sans nullabilité, compilés en CI via `tests/EdS.Unity.CompileCheck`.
- **Arithmétique vérifiée** (`checked`, `IntMath`), pas de débordement silencieux, précontrôle numérique avant le premier tick.
- **Refus atomiques** : une opération refusée ne modifie rien ; budgets réservés avant toute mutation ; files et déclencheurs bornés.
- Définitions immuables, état mutable seulement dans le moteur, un seul validateur, erreurs avec chemin précis.
- **Formats versionnés** : tout changement (journal, schémas, empreinte) est versionné, documenté, testé. **Les journaux figés ne sont jamais modifiés** : on en fige de nouveaux.

## 4. Versions actuelles (vérifiées dans le code)

| Élément | Version | Où (grep) |
|---|---|---|
| Sauvegarde | v14 (v13 encore lue) | `SaveValidator.cs` (`CurrentSchemaVersion`) |
| Catalogue d'objets | v7 | `ItemValidator.cs` |
| Règles (contenu) | 0.16.0 (écrit « 0.16.0-provisoire ») | `data/v0/rules.json` (`contentVersion`) |
| Moteur | 0.28.0 | `CombatEngine.cs` (`Version`) |
| Journal | 22 | `CombatEngine.cs` (`LogFormatVersion`) |
| Scénarios (schéma) | 16 | `ScenarioValidator.cs` |
| Instantané (format) | 5 | `CombatEngine.Snapshot.cs` (`FormatVersion`) |
| Protocole réseau | 4 | `src/EdS.Net/Protocol.cs` |
| Zones (schéma) | v11 (v10, v9, v8 et v7 encore lues) | `Zones/ZoneValidator.cs` |
| Fabrication (schéma) | v3 | `Crafting/CraftingModel.cs` |
| Histoire (schéma) | v9 (v4 à v8 encore lues) | `Story/StoryValidator.cs` |
| Marchands (schéma) | v3 (v2 encore lue) | `Merchants/MerchantModel.cs` |

Référence figée : `tests/EdS.Data.Tests/Fixtures/reference-0.28.0-contenu-0.6.0-regles-0.16.0`.
À ne pas confondre : version du contenu des règles (0.16.0) et version de leur schéma (`DataFormat.RulesSchemaVersion`, actuellement 15).

## 5. Où chercher

| Sujet | Fichier | Exemple |
|---|---|---|
| Lot en cours, décisions validées | `docs/FEUILLE_DE_ROUTE.md` | `grep -ni "lot 13" docs/FEUILLE_DE_ROUTE.md` |
| Vision, choix du jeu complet | `docs/CADRAGE.md`, `docs/cadrage/` | `grep -n "Q18" docs/CADRAGE.md` |
| Ordre des chantiers | `docs/PLAN_LOT_11.md`, `docs/PLAN_ALPHA.md` | `grep -n "L11-D1" docs/PLAN_LOT_11.md` |
| Moteur de combat | `docs/MOTEUR.md`, `src/EdS.Simulation` | `grep -rn "LogFormatVersion" src` |
| Formats et données | `docs/DONNEES.md`, `src/EdS.Data*`, `data/` | `grep -n "schéma" docs/DONNEES.md` |
| Builds, classes | `docs/BUILDS.md` | `grep -n "talent" docs/BUILDS.md` |
| Formules | `docs/FORMULES.md` | `grep -rn "Fixed" src/EdS.Domain` |
| Textes, ton, PNJ | `docs/ECRITURE.md` | `grep -n "Gaspard" docs/ECRITURE.md` |
| Monde, zones | `docs/MONDE.md`, `docs/ARCHITECTURE_MONDE.md`, `src/EdS.World` | `grep -rn "ZoneValidator" src` |
| Combat dans l'espace | `docs/COMBAT_ESPACE.md` | `grep -n "portée" docs/COMBAT_ESPACE.md` |
| Réseau, serveur | `docs/RESEAU.md`, `src/EdS.Net`, `src/EdS.Server` | `grep -n "Version" src/EdS.Net/Protocol.cs` |
| Unity | `docs/UNITY.md`, `tests/EdS.Unity.CompileCheck` | `find src -name "*.asmdef"` |
| Limites connues d'un lot | `docs/LIMITES_LOT_*.md` | `grep -ni "limite" docs/LIMITES_LOT_12.md` |
| Périmètre, crédits, builds CI | `docs/PERIMETRE.md`, `docs/CREDITS.md` | `grep -n "^#" docs/PERIMETRE.md` |
| Tests | `tests/EdS.{Domain,Data,Simulation}.Tests` | `grep -rn "\[Trait(\"Category\"" tests` |
| Catégories de tests | `scripts/test-category.sh` | `grep -n "World" scripts/test-category.sh .github/workflows/ci.yml` |
| Scénarios | `scenarios/` (dont `zones/`, `coop/`, `mesures/`) | `ls scenarios/zones` |
| CI | `.github/workflows/ci.yml` | `grep -n "name:" .github/workflows/ci.yml` |
| Outils, ligne de commande | `src/EdS.Tools`, `src/EdS.Cli` | `grep -rn "simulate" src/EdS.Cli` |

## 6. Vérification avant push et CI

Avant chaque push : **une seule vérification**, limitée à ce qui est touché, sorties filtrées.

```bash
set -o pipefail   # sans lui, un tube rend le code de tail, et un échec passerait pour réussi
dotnet restore 2>&1 | tail -3
dotnet build --no-restore -c Release -warnaserror 2>&1 | tail -15
dotnet test --no-build -c Release --filter "<classes ou catégories touchées>" 2>&1 | tail -8
# + scénario de référence (simulate, cmp avec le journal figé, replay) si règle, donnée ou moteur change
```

- Si `dotnet` manque : voir la section « Vérification » de CLAUDE.md.
- La vérification complète (toutes les étapes de `ci.yml`, catégories, scénario de référence, `test_ci_provenance.py`) est faite par la **CI** sur chaque commit poussé ; la structure exacte des travaux (Release, Debug, parallélisme) : voir `ci.yml`.
- Check à exiger : **`build-test` vert** sur la tête de la PR, plus une revue Codex sans remarque sur cette même tête. Une CI en échec se corrige par un nouveau commit.
- `gh pr` ne marche pas en session cloud : utiliser `gh api` (REST), par exemple `gh api repos/{owner}/{repo}/commits/{sha}/check-runs`.
- Message de commit : `Lot 8.2 (n/m) : …`. Rapport de PR : SHA, ce qui est fait, tests (chiffres), limites, questions pour Damien.

## 7. Méthode économe (choix de Damien du 4 novembre 2026, en plus de CLAUDE.md)

- Lire avec `offset`/`limit` ; jamais un gros doc en entier.
- Filtrer toutes les sorties (`grep`, `tail`, `head`, `cut -c1-200`).
- Une seule vérification complète ; ne pas relancer ce que la CI refait.
- Partir de la carte ci-dessus, puis `grep -n` ciblé, puis lecture de quelques lignes autour.
- Rapport final d'environ 15 lignes : ce qui est fait, SHA, tests, limites, questions.
