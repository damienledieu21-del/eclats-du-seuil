# Limites connues du lot 12 (les Vallons d'Aube en solo)

## Sous-lot 12.1 : le monde (zone, temps du monde, sauvegardes)

Détail : [MONDE.md](MONDE.md).

- **Pas encore de réapparition** : une créature tuée reste morte jusqu'à la fin de la session de zone ; la session suivante repart du scénario de zone (toutes les créatures vivantes). Populations et réapparitions : 12.4.
- **Pas encore de laisse ni de réinitialisation** : les rencontres sont reconnues (`Encounters`) mais rien n'en découle encore (12.4 : un ennemi assez distancé abandonne).
- **Session de zone bornée** : une session dure au plus 24 h de jeu (une heure pour les Vallons) ; au-delà, une nouvelle session commence depuis la sauvegarde. Le combat en cours n'est pas gardé d'une session à l'autre : seuls le temps du monde et le registre des événements le sont (le personnage : 12.2).
- **Sauvegarde minimale** : zone, temps du monde, sessions, événements consommés. Le personnage, l'inventaire et les quêtes s'y ajouteront ; une sauvegarde d'une autre version est refusée (pas de migration avant les premiers testeurs).
- **Écriture atomique** : sur une plateforme sans remplacement atomique, la copie de secours est faite avant que la sauvegarde soit retirée ; une coupure entre les deux laisse la copie, reprise à la lecture. Le comportement réel sur le Galaxy Note8 ne s'essaie qu'avec Unity (12.8).
- **Temps du monde provisoire** : journée de 2 h de jeu, début à 8 h ; pas encore d'effet du jour et de la nuit sur le jeu.
- **Unity** : `EdS.World` est compilé par Unity (assemblage du jeu), mais aucun écran ne s'en sert encore (12.8).
- **Formats** : moteur 0.22.0, journal au format 16, scénarios en schéma 10, sauvegarde en version 1 ; instantané (format 2), règles (schéma 12) et classes (schéma 9) inchangés. Un combat fermé écrit les mêmes lignes qu'au moteur 0.21.0 ; un journal 0.21.0 est refusé au rejeu, sans migration.
