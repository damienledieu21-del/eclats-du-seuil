# Limites connues du lot 12 (les Vallons d'Aube en solo)

## Sous-lot 12.1 : le monde (zone, temps du monde, sauvegardes)

Détail : [MONDE.md](MONDE.md).

- **Pas encore de réapparition** : une créature tuée reste morte jusqu'à la fin de la session de zone ; la session suivante repart du scénario de zone (toutes les créatures vivantes). Populations et réapparitions : 12.4 (fait).
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
- **Formats** : sauvegarde en version 2 (la version 1 est mise à jour à la lecture ; version 3 au 12.3), progression en schéma 1 ; moteur, journal, scénarios et règles inchangés par le 12.2.

## Sous-lot 12.3 : objets, équipement, inventaire

Détail : [MONDE.md](MONDE.md#objets-équipement-inventaire-sous-lot-123), moteur : [MOTEUR.md](MOTEUR.md#bonus-déquipement-sous-lot-123).

- **Valeurs provisoires** (questions à Damien) : les quatre emplacements de la tranche (arme, bouclier, torse, pendentif), les objets, leurs bonus et niveaux requis, les tables de butin et leurs chances. Tout est dans `data/v0/world/items.json`.
- **Butin perdu quand l'inventaire est plein** (proposition, question à Damien) : un objet tombé qui ne tient pas en entier n'est pas rangé, et il est signalé (`Kept` faux). Autres choix possibles : le laisser au sol un moment, ou le garder dans une réserve.
- **Pas encore d'effet spécial d'équipement** : les objets ne donnent que des bonus aux statistiques. Les déclencheurs d'équipement (L8-D6) et un premier objet remarquable viendront ensuite.
- **Le bouclier ne change pas encore la garde** : la garde et le blocage du Bastion restent ceux de sa classe, même sans bouclier porté ; le bouclier n'apporte que ses bonus. Un Bastion sans bouclier (E8) reste une question ouverte.
- **Pas de vente, d'achat, d'échange ni de destruction volontaire** : l'économie viendra plus tard ; `Remove` sert à la fabrication (12.6).
- **Chances de butin par créature** : la table de chaque créature de zone est donnée à l'ouverture de la partie ; les Vallons en données (12.4) l'associeront aux populations.
- **Formats** : moteur 0.23.0, journal au format 17, scénarios en schéma 11, sauvegarde en version 3 (les versions 1 et 2 sont mises à jour), catalogue des objets en schéma 1. Un combat sans équipement écrit les mêmes lignes qu'au moteur 0.22.0 ; un journal 0.22.0 est refusé au rejeu, sans migration.

## Sous-lot 12.4 : les Vallons en données (lieux, populations, laisse, passage du moulin)

Détail : [MONDE.md](MONDE.md#les-vallons-en-données-sous-lot-124).

- **Carte provisoire** (question à Damien) : disposition, tailles, créatures, délais de réapparition et laisses sont des premières valeurs, à régler à l'essai (12.9). Tout est dans `scenarios/zones/vallons.json` et `data/v0/world/zones/vallons.json`.
- **Laisse par téléportation** : la créature qui lâche disparaît et une vie calme et soignée réapparaît chez elle, au lieu de rentrer en marchant. Un retour à pied demanderait de toucher au moteur (désengagement) : à faire si l'essai le demande.
- **Laisse mesurée à la cible** (l'écart entre la créature et le joueur qu'elle poursuit), pas à la distance de son point de départ : c'est ce que Damien a décrit à l'essai.
- **Plafond de 64 combattants par session** : environ 53 réapparitions ou retours au calme par session d'une heure ; au-delà, la population attend la session suivante.
- **Passage ouvert à la session suivante** : l'espace d'une session ne change pas en route ; Unity (12.8) enchaînera une nouvelle session (sauvegarde puis reprise) quand le passage s'ouvre.
- **Nœuds de récolte** : avec les métiers (12.6). **Manifestation** : avec les quêtes (12.5) et le chef (12.7).
- **Format** : zone en schéma 1 ; scénario de zone agrandi (même schéma 11) ; moteur, journal, sauvegarde inchangés par le 12.4.
