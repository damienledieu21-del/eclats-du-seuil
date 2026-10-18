# Limites du lot 9 (prototype Unity 3D)

## Sous-lot 9.1 : données portables

Voir [DONNEES.md](DONNEES.md#architecture). L'analyseur JSON maison est comparé à System.Text.Json (même arbre, jamais plus laxiste) ; il refuse en plus les paires de substitution UTF-16 incomplètes et écrit ses messages de syntaxe en français. `EdS.Cli` garde System.Text.Json pour repérer le `kind` d'un fichier dans `validate`.

## Sous-lot 9.2 : pont temps réel

- **Aucune position** : le cœur n'a pas de système de positions (feuille de route, lot 8) ; dans le prototype, les déplacements du personnage sont visuels et toutes les attaques portent quelle que soit la distance.
- **Temps du client** : il décide seulement quand un tick est simulé. Au-delà de 5 ticks dus en une image (250 ms), le surplus est abandonné : une image très lente ralentit le combat au lieu de le faire rattraper d'un coup. Le combat ne dépend jamais de la cadence d'images (rejeu identique du scénario complété).
- **Pré-contrôle indicatif** : la raison d'indisponibilité d'une compétence (recharge, ressources, contrôle, cible) est calculée par le pont pour l'affichage ; le coût d'endurance affiché est celui de la fiche. Le moteur reste seul juge : une commande soumise peut encore être refusée (`Rejected`).
- **Intentions par tick** : une seule compétence par tick (la dernière demandée avant le tick) ; garde tenue reprise au tick qui suit un lancer, comme le pilote de mesure ; une parade demandée part au tick suivant (le joueur pare « juste avant » l'impact annoncé par la télégraphie).
- **Soutien en solo** : une compétence de soutien d'un allié (Barrière d'acier) vise le joueur lui-même ; le choix d'un allié viendra avec un scénario à plusieurs personnages.
- **Retours** : tirés de la différence des compteurs d'un tick à l'autre (pas du journal) : un retour ne dit pas quelle attaque a porté ni de quelle compétence vient un dégât ; au plus 256 retours en attente si le client ne les lit pas.
- **Journal** : aucun pendant la partie ; celui d'une partie terminée est écrit après coup depuis le combat complété (`WriteLog`), qui se rejoue octet par octet. Une partie abandonnée en cours n'a pas de journal.
- **Séance d'entraînement** : durée maximale des règles (10 min) ; à la fin, le client relance une séance identique (même graine). Les commandes du scénario sont retirées ; ses adversaires et sa graine sont gardés.

## Sous-lot 9.3 : projet Unity

Guide d'installation et touches : [UNITY.md](UNITY.md).

- **Non essayé dans Unity par Claude** : les sessions de développement n'ont pas d'éditeur Unity. La CI compile les scripts du jeu en C# 9 contre des bouchons de l'API Unity (`tests/EdS.Unity.CompileCheck`) et vérifie la cohérence du paquet (`Lot93UnityPackageTests`) ; un écart avec la vraie API d'Unity ne se voit qu'à l'ouverture dans l'éditeur (essai de Damien).
- **Cœur compilé par Unity depuis `src/`** : paquet local `com.eclats.core` (`unity/Packages/manifest.json` → `file:../../src`), une définition d'assemblage par projet (`noEngineReferences`, références identiques aux `.csproj`, transitives comprises), `csc.rsp` pour les annotations de nullabilité, `InternalsVisibleTo` du domaine vers le moteur repris en source sous `UNITY_5_3_OR_NEWER`. `EdS.Tools` et `EdS.Cli` (.NET récent) sont exclus par une contrainte de définition jamais satisfaite. Les sorties de compilation .NET vont dans `artifacts/` (`UseArtifactsOutput`) pour que Unity ne trouve ni `.cs` générés ni `.dll` dans `src/`. Unity écrit ses `.meta` dans `src/` : ignorés par git (aucun script du cœur n'est référencé par GUID).
- **Projet minimal** : seuls `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt` (6000.3.0f1, le Hub propose la version installée) et `Assets/EdS` sont suivis ; Unity génère le reste. Aucune scène : `EdsBootstrap` construit le jeu au lancement dans la scène ouverte.
- **Rendu URP** : configuré automatiquement à la première ouverture (`EdsProjectSetup` crée un asset URP et son renderer dans `Assets/EdS/Settings`, non suivis) ; en cas d'échec, le rendu intégré reste actif et les matériaux passent au shader « Standard ». Aucun post-traitement.
- **Saisie** : ancien gestionnaire d'entrées d'Unity (`Input`), touches pensées pour un clavier AZERTY (ZQSD, A/E). Le paquet Input System n'est pas utilisé.
- **Interface** : IMGUI (`OnGUI`), provisoire ; écran virtuel de 1080 pixels de haut.
- **Arène** : formes simples, positions visuelles (voir 9.2 : aucun système de positions dans le cœur) ; les ennemis sont placés en arc devant le joueur, un allié à ses côtés ; déplacement bloqué pendant un étourdissement.
- **Données** : lues dans le dépôt (`data/v0`, `scenarios/`) depuis le dossier parent de `unity/` ; une version compilée du jeu (hors éditeur) demandera de les embarquer (chantier ultérieur).
- **Scénarios jouables** : l'entraînement garde et parade et les scénarios de mesure S1 à S5 (touche N) ; builds de référence (F1 à F5).
