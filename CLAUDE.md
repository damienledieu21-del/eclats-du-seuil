# Consignes pour les agents Claude — Éclats du Seuil

Ce fichier est lu au début de chaque session (interactive ou planifiée). Il est volontairement court : le détail est dans les documents cités.

## Le projet

*Éclats du Seuil* est un MMORPG fantasy PC/mobile, jouable surtout en solo dans un monde partagé. Ce dépôt contient le **cœur C# du jeu** (simulateur de combat déterministe), indépendant de Unity, du réseau et de toute interface. Le but n'est pas le simulateur le plus complexe possible : c'est un vrai jeu jouable. Le prototype Unity 3D (lot 9), le serveur autoritaire à deux joueurs (lot 10) et le combat dans l'espace (lot 11, terminé le 23 octobre 2026) sont faits. La vision du jeu complet (cadrage de Damien du 19 octobre 2026) est dans [docs/CADRAGE.md](docs/CADRAGE.md), l'ordre des chantiers (validé le 20 octobre 2026) dans [docs/PLAN_LOT_11.md](docs/PLAN_LOT_11.md).

- Directeur du jeu : **Damien** (« Dam »). Il décide du gameplay, des dépenses et de **toutes les fusions**.
- Claude : développement, tests, documentation, préparation des PR.
- Codex : revue indépendante des PR sur GitHub (règles dans [AGENTS.md](AGENTS.md)). Une auto-revue Claude n'est jamais présentée comme une revue Codex.

Lire avant de travailler : [README.md](README.md), [docs/FEUILLE_DE_ROUTE.md](docs/FEUILLE_DE_ROUTE.md) (lot en cours, décisions validées, catalogue du Bastion), [docs/CADRAGE.md](docs/CADRAGE.md) (choix du jeu complet, avec leur statut : retenu, recommandation, ouvert ; une recommandation n'est jamais présentée comme une décision de Damien), puis les documents du domaine touché ([docs/MOTEUR.md](docs/MOTEUR.md), [docs/DONNEES.md](docs/DONNEES.md), [docs/BUILDS.md](docs/BUILDS.md), [docs/FORMULES.md](docs/FORMULES.md), [docs/MONDE.md](docs/MONDE.md), `docs/LIMITES_LOT_*.md`).

## Règles absolues

1. **Ne jamais fusionner une PR** sans une autorisation explicite de Damien, donnée dans la conversation pour cette PR précise ; une autorisation ne vaut pas pour les suivantes. Ne jamais activer l'auto-merge, ne jamais pousser sur `main`, ne jamais forcer un push (`--force`), ne jamais supprimer une branche distante. Toute modification passe par une branche et une PR (en brouillon tant qu'elle n'est pas prête).
2. Ne jamais réécrire un commit déjà poussé. Corriger par un nouveau commit.
3. Ne pas modifier `.github/workflows/` sans le signaler clairement dans la PR (la PR #4 modifie aussi `ci.yml`).
4. Aucun secret dans le code, les journaux, les PR ou les rapports. Ne toucher à aucun autre dépôt.
5. Les décisions de gameplay validées (feuille de route) ne changent jamais silencieusement. Une proposition de changement se fait dans la PR, avec la question posée à Damien.
6. Ne pas affirmer ce qui n'est pas prouvé : distinguer « code écrit », « tests locaux verts », « CI verte », « revu », « fusionné ».

## Quand s'arrêter et demander

S'arrêter et poser la question à Damien (dans la PR et dans le rapport) seulement pour : un vrai arbitrage de gameplay non couvert par la feuille de route, une dépense, une rupture d'architecture difficile à défaire, ou une fusion. Pour tout le reste, avancer sans demander de confirmation.

## Contrats techniques non négociables

- Déterminisme : tick logique (50 ms), aucune horloge réelle, virgule fixe `Fixed` (échelle 10 000), RNG `Pcg32`, ordre d'événements stable, aucun état global mutable, journaux reproductibles octet par octet.
- Projets `EdS.Domain`, `EdS.Simulation`, `EdS.Data`, `EdS.Data.Json` (depuis le sous-lot 9.1), `EdS.Runtime` (9.2), `EdS.Net` (lot 10), `EdS.World` (12.1) : `netstandard2.1`, C# 9, **aucune dépendance externe** (compatibilité Unity/IL2CPP). Pas d'API .NET plus récente dans ces projets ; ce qui a besoin de .NET récent va dans `EdS.Tools` ou `EdS.Cli`.
- Unity compile les projets portables directement depuis `src/` (paquet `com.eclats.core`, sous-lot 9.3) : tout nouveau projet de `src/` reçoit sa définition d'assemblage (`.asmdef`, références identiques au `.csproj`, ou contrainte d'exclusion pour un outil .NET) ; aucune sortie de compilation dans `src/` (`UseArtifactsOutput`). Les scripts de `unity/Assets` sont en C# 9, sans annotation de nullabilité, et compilés en CI contre `tests/EdS.Unity.CompileCheck`.
- Arithmétique vérifiée (`checked`, `IntMath`), aucun débordement silencieux, précontrôle numérique avant le premier tick.
- Refus atomiques : une opération refusée ne modifie rien. Réservation exacte des budgets avant mutation. Files et déclencheurs bornés.
- Définitions immuables, état mutable dans le moteur seulement. Un seul validateur, erreurs avec chemin précis.
- Tout changement de format (journal, schéma de règles ou de classes, empreinte) est versionné, documenté et testé. Les journaux de référence historiques ne sont jamais modifiés : on en fige de nouveaux.

## Vérification (identique à la CI)

Installer .NET si `dotnet` est absent :

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
```

Puis, avant chaque push, toutes les étapes de `.github/workflows/ci.yml` :

```bash
dotnet restore
dotnet build --no-restore -c Release -warnaserror
dotnet test --no-build -c Release
bash scripts/test-category.sh -c Release <catégories listées dans ci.yml>
# scénario de référence : simulate, cmp avec le journal figé, replay (voir ci.yml)
dotnet build --no-restore -c Debug -warnaserror
dotnet test --no-build -c Debug
python3 -m unittest discover -s scripts -p 'test_ci_provenance.py' -v   # générateur du rapport CI non signé (PR #4)
```

Un push n'a lieu que si tout est vert localement. Après le push, vérifier le check `build-test` de GitHub (`gh api repos/{owner}/{repo}/commits/{sha}/check-runs`). `gh pr` ne fonctionne pas dans les sessions cloud : utiliser `gh api` (REST).

## Méthode pour un sous-lot

1. Lire la feuille de route, la documentation et le code concernés ; confirmer les contrats existants.
2. Implémenter seulement les mécanismes du sous-lot, par commits cohérents aux messages explicites (`Lot 8.2 (n/m) : …`).
3. Tests unitaires, adversariaux et de propriétés ; frontières numériques ; non-régression contre les journaux figés ; catégorie de tests dédiée si besoin (déclarée dans `ci.yml` et `scripts/test-category.sh`).
4. Mettre à jour schémas, `docs/` et README (section décisions et avancement).
5. Vérification complète, push, PR en brouillon, CI verte. À partir du lot 13 (décision de Damien du 25 octobre 2026) : avant chaque demande de revue Codex, une relecture par un agent indépendant (neuf, sans le contexte de la session, à qui l'on donne le code et non ses conclusions), une seule par PR ; ses remarques fondées sont traitées avant la demande. Elle n'est jamais présentée comme une revue Codex.
6. Rapport dans la description de la PR : SHA, ce qui est fait, tests reproduits (chiffres), limites connues, questions pour Damien.

## Branches et PR

- Une branche par sous-lot, empilée sur la précédente tant que celle-ci n'est pas fusionnée (la PR vise alors la branche précédente, pas `main`).
- Avant d'ajouter des commits à une branche, vérifier qu'aucune autre session n'y travaille (derniers commits, PR ouverte, date).
- Remarques de revue (Codex ou Damien) : les traiter en priorité, au plus deux tours de corrections par PR, puis rendre la main à Damien avec un résumé.
