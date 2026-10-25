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

## Sous-lot 12.5 : PNJ, dialogues et quêtes

Détail : [MONDE.md](MONDE.md#pnj-dialogues-et-quêtes-sous-lot-125).

- **Textes provisoires** (question à Damien) : dialogues, noms, journal et découvertes sont une première écriture, à relire par Damien ; tout est dans `data/v0/world/texts/fr.json`. Pas encore d'anglais.
- **Mission sans chef** : l'étape « vaincre le chef » viendra au 12.7, entre le rôdeur et le retour chez Odile. Pas de quête secrète ni de réveil renforcé dans la tranche pour l'instant (Q2 : à une étape identifiée).
- **Une seule conséquence de PNJ** dans la mission (la confiance d'Odile : indice de Maëlle et amulette au retour). Le format en permet d'autres sans code.
- **Minerai sur les sangliers** en attendant les nœuds de récolte et la forge (fait au 12.6 : le joueur forge la ferrure).
- **Portée personnelle seulement** (H2) : rien de commun à plusieurs joueurs avant le lot 13.
- **Passage ouvert à la session suivante** (comme au 12.4) : l'étape « pré des loups » se fait après une reprise.
- **Pas d'écran** : dialogues et journal s'affichent au 12.8.
- **Formats** : sauvegarde en version 4 (histoire ; les versions 1 à 3 sont mises à jour), histoire et textes en schéma 1 ; moteur et journal inchangés.

## Sous-lot 12.6 : soin et métiers

Détail : [MONDE.md](MONDE.md#soin-et-métiers-sous-lot-126), moteur : [MOTEUR.md](MOTEUR.md#soin-sous-lot-126).

- **Valeurs provisoires** (question à Damien) : soins (300 et 700 PV), recharge partagée de 20 s, une pile emportée par objet, courbe d'expérience des métiers, recettes, repousse des nœuds (1 min), réduction de l'expérience triviale. Tout est dans les données.
- **Pas de régénération des PV** hors des objets de soin ; une session suivante repart PV pleins.
- **Soin réservé aux objets** : pas encore de compétence de soin de classe (le format du moteur s'y prête).
- **Réseau** : la commande `consume` n'est pas encore transmise par le protocole du lot 10 (monde partagé : lot 13).
- **Nœuds par session** : un nœud récolté repousse dans la session ; une nouvelle session les remet tous en place.
- **Formats** : moteur 0.24.0, journal au format 18, scénarios en schéma 12, instantané au format 3, catalogue des objets en version 2, sauvegarde en version 5 (les versions 1 à 4 sont mises à jour), métiers en schéma 1. Sans objet de soin, un combat écrit les mêmes lignes qu'au moteur 0.23.0 ; un journal 0.23.0 est refusé au rejeu, sans migration.

## Sous-lot 12.7 : chef de fin et récompenses

Détail : [MONDE.md](MONDE.md#chef-de-fin-et-récompenses-sous-lot-127).

- **Valeurs provisoires** (question à Damien) : PV, attaques et places des deux formes, lame du Seuil (arme épique, niveau 8), butin du Gardien. Tout est dans `scenarios/zones/vallons_chef.json` et `data/v0/world/`.
- **Pas de quête secrète ni de réveil renforcé** (Q2, H4) : hors de la tranche pour l'instant ; le format du registre (clé par victoire) permettra une clé de plus pour une suite renforcée.
- **Phases par formes successives** : chaque phase est un nouveau combattant (nouvelle identité, PV pleins). Changer d'attaques au même combattant à un seuil de PV demanderait de toucher au moteur : à faire si l'essai le demande.
- **Chef présent à chaque session** : vaincu, il revient à la session suivante (butin à chaque victoire, récompense une seule fois). Pas encore de délai de réapparition dans la session.
- **Défaite annoncée, pas encore rejouée** : la session signale la défaite ; c'est Unity (12.8) qui enchaînera la reprise (sauvegarde puis nouvelle session au point sûr).
- **Portée personnelle** : la victoire est une clé du registre de la sauvegarde du joueur ; le partage d'une victoire de groupe viendra au lot 13.
- **Formats** : zone en schéma 2 (bloc `boss`) ; moteur, journal, scénarios, instantané et sauvegarde inchangés (la victoire est une clé de `consumed`).

## Sous-lot 12.8 : Unity, la zone jouable

Détail : [MONDE.md](MONDE.md#laventure-jouable-sous-lot-128), touches : [UNITY.md](UNITY.md#4-bis-laventure-dans-les-vallons-daube-sous-lot-128).

- **Pas essayé dans Unity ici** : les scripts sont compilés en CI contre les bouchons de l'API (`EdS.Unity.CompileCheck`) et le cœur de l'aventure (`AdventureSession`) est testé ; l'affichage, les écrans et le téléphone s'essaient au 12.9.
- **Formes simples et écrans IMGUI provisoires** : PNJ, nœuds et points sûrs en primitives ; inventaire, journal, atelier et dialogue en panneaux simples ; pas de carte, pas d'icônes.
- **Valeurs provisoires** (question à Damien) : portée de choix de la cible (20 m), halte (à 5 m d'un point sûr, sans ennemi à 12 m), sauvegarde automatique (5 s), barres des créatures jusqu'à 25 m.
- **Le monde s'arrête** pendant un dialogue, un écran ou la pause (solo). Au lot 13 (monde partagé), il continuera.
- **Changements à la halte** : niveau, équipement et passage ouvert prennent effet à la halte suivante (nouvelle session) ; pas encore de changement d'équipement en plein combat.
- **Dernier point sûr non sauvegardé** : au lancement, la partie reprend à l'entrée de la zone (la sauvegarde ne garde pas la position).
- **Talents** : l'écran des talents reste celui de l'entraînement ; le personnage de l'aventure garde ceux de sa sauvegarde.
- **Noms d'objets et de lieux** dans leurs fichiers de données, pas encore par clés (Q37 : à faire avec l'anglais).
- **Formats** : textes enrichis des clés `text.ui.…` (schéma inchangé) ; moteur, journal, scénarios et sauvegarde inchangés. `EdS.Runtime` dépend désormais de `EdS.World` (projet et définition d'assemblage).
