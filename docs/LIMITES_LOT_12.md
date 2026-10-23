# Limites connues du lot 12 (les Vallons d'Aube en solo)

## Sous-lot 12.1 : le monde (zone, temps du monde, sauvegardes)

Détail : [MONDE.md](MONDE.md).

- **Pas encore de réapparition** : une créature tuée reste morte jusqu'à la fin de la session de zone ; la session suivante repart du scénario de zone (toutes les créatures vivantes). Populations et réapparitions : 12.4.
- **Pas encore de laisse ni de réinitialisation** : les rencontres sont reconnues (`Encounters`) mais rien n'en découle encore (12.4 : un ennemi assez distancé abandonne).
- **Session de zone bornée** : une session dure au plus 24 h de jeu (une heure pour les Vallons) ; au-delà, une nouvelle session commence depuis la sauvegarde. Le combat en cours n'est pas gardé d'une session à l'autre : seuls le temps du monde et le registre des événements le sont (le personnage : 12.2).
- **Sauvegarde minimale** : zone, temps du monde, sessions, événements consommés (le personnage s'y ajoute au 12.2, l'inventaire et les quêtes ensuite).
- **Écriture atomique** : sur une plateforme sans remplacement atomique, la copie de secours est faite avant que la sauvegarde soit retirée ; une coupure entre les deux laisse la copie, reprise à la lecture. Le comportement réel sur le Galaxy Note8 ne s'essaie qu'avec Unity (12.8).
- **Temps du monde provisoire** : journée de 2 h de jeu, début à 8 h ; pas encore d'effet du jour et de la nuit sur le jeu.
- **Unity** : `EdS.World` est compilé par Unity (assemblage du jeu), mais aucun écran ne s'en sert encore (12.8).
- **Formats** : moteur 0.22.0, journal au format 16, scénarios en schéma 10, sauvegarde en version 1 (2 au 12.2) ; instantané (format 2), règles (schéma 12) et classes (schéma 9) inchangés. Un combat fermé écrit les mêmes lignes qu'au moteur 0.21.0 ; un journal 0.21.0 est refusé au rejeu, sans migration.

## Sous-lot 12.2 : le personnage (expérience, niveaux, attributs, sauvegarde)

Détail : [MONDE.md](MONDE.md#personnage-sous-lot-122).

- **Valeurs provisoires** (question à Damien) : courbe d'expérience (1 620 points pour le niveau 10), expérience des créatures (PV maximaux ÷ 20), table d'attributs du Bastion par niveau. Tout se règle dans `data/v0/world/progression.json`, sans toucher au code ; l'équilibrage se fera à l'essai (12.9), avec les quêtes qui donneront aussi de l'expérience (12.5).
- **Un seul joueur crédité** : l'expérience va au personnage s'il est présent et vivant quand la créature tombe, quel que soit celui qui l'a frappée. Le partage entre joueurs viendra avec le serveur de zone (lot 13).
- **Niveau gagné en combat** : effet à la prochaine entrée en zone ; pas de montée de niveau en plein combat, ni de soin ou de PV supplémentaires immédiats.
- **Talents et compétences** : gardés dans la sauvegarde et validés à l'entrée ; aucun écran ne les change encore (12.8). Un build devenu invalide (données changées) est refusé à l'entrée avec son motif, sans être corrigé automatiquement.
- **Une seule classe jouable** (le Bastion) : la progression d'une autre classe s'ajoutera dans le même fichier.
- **Formats** : sauvegarde en version 2 (la version 1 est mise à jour à la lecture), progression en schéma 1 ; moteur, journal, scénarios et règles inchangés par le 12.2.
