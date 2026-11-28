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
| Sauvegarde | v33 (v32 et antérieures encore lues ; la source dans `character.corruption`, défi de la source, V2.42.3 ; v32 : autels purifiés `character.corruption`, corruption qui s'étend et recule, V2.42.1 ; v31 : coffres repris au fort `events[].recovered`, quête de reprise, V2.41.3 ; v30 : jour du dernier échec `events[].lostDay`, contrecoup d'un pillage sur ses jours, revue Codex de la PR #240, V2.41.1 ; v29 : maison du joueur `character.home`, V2.53.1, décisions de Damien du 20 novembre 2026 ; v28 : lieux reconstruits `character.rebuilds`, V2.43.1, décisions de Damien du 19 novembre 2026 ; v27 : fêtes d'Orsédia `character.feasts` : portefeuille par fête, récompenses, rattrapage, titre de fête, décisions de Damien du 17 novembre 2026 ; v26 : titre actif `character.cards.shown`, décision de Damien du 16 novembre 2026 ; v25 : cartes à la taverne `character.cards` : graine, victoires, compteurs du jour, partie en cours, mises du 16 novembre 2026 ; v24 : marque du pâtissier `signedBy` à toute qualité, concours de pâtisserie ; v23 : cases explorées `character.explored`, V2.29 (2) ; v22 : monture `character.mount`, V2.28d (2b) ; v21 : pêche et carnet `character.fishing`, vague 4 ; v20 : troupe des compagnons `character.companions`, compagnons ; v19 : point de reprise `resume` : dernier point sûr approché et son secteur, V2.28a ; v18 : registre purgé des clés de mort, `sectors`, V2.26 ; v17 : aspect du héros `character.look`, créateur de personnage, V2.16 ; v16 : stock des marchands `character.stock`, concours `story.festivals`, V2.10) | `SaveValidator.cs` (`CurrentSchemaVersion`) |
| Catalogue d'objets | v8 (`craftQuality` : pâtisserie de concours) | `ItemValidator.cs` |
| Règles (contenu) | 0.18.0 (écrit « 0.18.0-provisoire » ; esquive `combat.dodge`) | `data/v0/rules.json` (`contentVersion`) |
| Moteur | 0.36.0 (marche dans la neige, V2.44.2 ; 0.35.0 : réconfort d'un plat de fête, sous-lot F7 ; 0.34.0 : monture ; 0.33.0 : compagnons) | `CombatEngine.cs` (`Version`) |
| Journal | 30 (neige ; 29 : réconfort ; 28 : monture) | `CombatEngine.cs` (`LogFormatVersion`) |
| Scénarios (schéma) | 24 (actions `snow` et `snowEnd`, neige ; 23 : actions `comfort` et `comfortEnd`, réconfort ; 22 : `mount`, `carried.mounted`, actions `mount` et `dismount`, monture ; 21 : `companion`, actions `order` et `duo`, compagnons ; 20 : action `dodge`, esquive ; 19 : aire d'une zone à région jusqu'à 4 096 obstacles, V2.28c ; 18 : `carried`, V2.26) | `ScenarioValidator.cs` |
| Instantané (format) | 11 (`snowbound`, neige ; 10 : `comforted`, réconfort ; 9 : monture) | `CombatEngine.Snapshot.cs` (`FormatVersion`) |
| Protocole réseau | 6 (message `season` : date du serveur, V2.44.1, décision du 20 novembre 2026 ; 5 : esquive) | `src/EdS.Net/Protocol.cs` |
| Zones (schéma) | v18 (v17 à v7 encore lues ; secrets `secrets` (coffres et stèles de lore, fouillés une fois, clé `<zone>/secret/<id>`), V2.52a ; l'Envers : étage `envers` et reflet `mirror` (`of`, `scenario`, `remove`), aire composée par `ZoneValidator.Reflect`, failles au même point, V2.51a ; eau des portes `water` (`tide`, `diveMs`) : marée et plongée des grottes noyées, `EdS.World/Tides.cs`, V2.50.1 ; étage `floor` (surface, dessous), genre de porte `kind`, bornes des portes relevées (64, 512 et 32 par case ; 256 zones reliées), V2.45.1 ; territoire des populations, V2.26 ; région : cases de 128 m et secteurs, V2.25.3) | `Zones/ZoneValidator.cs`, `Zones/RegionGrid.cs` |
| Fabrication (schéma) | v4 (`seasons` facultatif sur un nœud : nœuds de saison, saisons du 15 novembre 2026) | `Crafting/CraftingModel.cs` |
| Histoire (schéma) | v16 (v4 à v15 encore lues ; `lane` sur `contestOpen` et `contest` : piste de concours, deux concours à la même fête, décision du 16 novembre 2026, en v16 ; `hasSigned` et `present`, concours de pâtisserie, en v15 ; fêtes aux vraies dates en v14 ; fêtes en v13, V2.10 ; réputation en v12, V2.7 ; 32 répliques par dialogue depuis v11, 16 avant) | `Story/StoryValidator.cs` |
| Compagnons (catalogue) | 1 | `Allies/CompanionModel.cs` (`CompanionValidator`) |
| Lieux reconstruits (schéma) | v2 (v1 encore lue ; portes des intérieurs `rebuilt.doors`, V2.43.4 ; chantiers par paliers, V2.43.1 ; faits `fact.rebuild.<lieu>.started`, `.<palier>`, `.done`) | `Rebuilds/RebuildModel.cs` (`CurrentSchemaVersion`), `EdS.World/Rebuilds.cs` |
| Maison du joueur (schéma) | v1 (achat, paliers, meubles sur la grille de 0,25 m, coffre ; `EdS.World/Home.cs`, `WorldSession.Home.cs`) | `Homes/HomeModel.cs` (`HomeValidator.CurrentSchemaVersion`) |
| Saisons (schéma) | v2 (v1 encore lue ; vigne, neige en plaine et au col, créatures de saison, V2.44.2, décisions de Damien du 20 novembre 2026 ; v1 : Échos de la brume, lac gelé, île ; calendrier `EdS.Domain/Time/SeasonCalendar.cs`) | `Seasons/SeasonModel.cs` (`CurrentSchemaVersion`) |
| Boss du monde (schéma) | v2 (Grand Solitaire : scénario des formes, une forme par phase ; v1 encore lue ; `EdS.World/WorldBoss.cs`, `WorldSession.WorldBoss.cs`) | `WorldBosses/WorldBossModel.cs` (`CurrentSchemaVersion`) |
| Fêtes d'Orsédia (schéma) | v2 (v1 encore lue ; `stall`, étal du forain, en v2 ; 108 références, monnaies, titres, activités, marchand de fête ; décisions de Damien du 17 novembre 2026, sous-lots F1 à F3 ; moteur : `EdS.World/WorldSession.Feasts.cs`, `Feasts.cs`) | `Feasts/FeastModel.cs` (`FeastValidator.CurrentSchemaVersion`) |
| Caravanes (schéma) | v2 (v1 encore lue ; quête de reprise au fort `recovery`, V2.41.3 ; v1 : caravanes et attaques des bandits, V2.41.1 ; l'attaque devient un événement de la zone, sorte `Caravan`, sans changer le schéma des zones) | `Caravans/CaravanModel.cs` (`CaravanValidator.CurrentSchemaVersion`), `EdS.World/Caravans.cs` |
| Corruption (schéma) | v2 (v1 encore lue ; défi de la source `challenge`, V2.42.3, non sauvegardé : une partie rechargée en plein défi fait disparaître le gardien, à relancer avec F ; v1 : corruption qui s'étend et recule, V2.42.1 : source, terres, autels, règles ; état recalculé du temps du monde et des autels purifiés) | `Corruption/CorruptionModel.cs` (`CorruptionValidator.CurrentSchemaVersion`), `EdS.World/Corruption.cs` |
| Marchands (schéma) | v7 (v2 à v6 encore lues ; jours et saisons d'une offre permanente, pâtisseries de Firmin ; boutiques à horaires, fournées, grand jour de marché, V2.14 ; stock vivant, prix, contrecoups, étal ambulant, V2.10) | `Merchants/MerchantModel.cs`, `Merchants/ShopRules.cs` |

Référence figée : `tests/EdS.Data.Tests/Fixtures/reference-0.36.0-contenu-0.6.0-regles-0.18.0` (neige : `scenarios/espace_neige.json` ; la précédente, `reference-0.35.0-…`, réconfort : `scenarios/espace_reconfort.json` ; la précédente, `reference-0.34.0-…`, monture : `scenarios/espace_monture.json`, `EdS.Simulation/Engine/CombatEngine.Mount.cs` ; la précédente, `reference-0.33.0-…`, compagnons : `scenarios/espace_compagnon.json`, `EdS.Simulation/Engine/CombatEngine.Companions.cs` ; la précédente, `reference-0.32.0-…-regles-0.18.0`, esquive, reste figée).
À ne pas confondre : version du contenu des règles (0.18.0) et version de leur schéma (`DataFormat.RulesSchemaVersion`, actuellement 17).

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
| Grande carte (4 × 3 km, cases, étages) | `docs/PLAN_GRANDE_CARTE.md` | `grep -n "V2.26" docs/PLAN_GRANDE_CARTE.md` |
| Combat dans l'espace | `docs/COMBAT_ESPACE.md` | `grep -n "portée" docs/COMBAT_ESPACE.md` |
| Réseau, serveur | `docs/RESEAU.md`, `src/EdS.Net`, `src/EdS.Server` | `grep -n "Version" src/EdS.Net/Protocol.cs` |
| Unity | `docs/UNITY.md`, `tests/EdS.Unity.CompileCheck` | `find src -name "*.asmdef"` |
| Performances (audit, Profiler, F9) | `docs/PERFS.md`, `src/EdS.Runtime/PerfRules.cs` | `grep -n "PC modeste" docs/PERFS.md` |
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
