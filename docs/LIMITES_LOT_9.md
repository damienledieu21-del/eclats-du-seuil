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
- **Séance d'entraînement** : durée maximale des règles (10 min) ; à la fin, le client relance une séance identique (même graine). Les commandes du scénario sont retirées ; ses adversaires et sa graine sont gardés.
