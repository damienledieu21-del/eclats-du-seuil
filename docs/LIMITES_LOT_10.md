# Limites du lot 10 (serveur autoritaire à deux joueurs)

## Sous-lot 10.1 : protocole et transport

- **Réseau local seulement** : TCP sans chiffrement ni authentification forte. Le jeton de reconnexion (128 bits aléatoires) circule en clair. Un serveur ouvert sur Internet demandera TLS et des comptes (chantier ultérieur, avec une dépense d'hébergement à décider).
- **TCP** : ordre et intégrité garantis, mais un message perdu retarde les suivants (blocage en tête de file). Suffisant pour 20 ticks par seconde et deux joueurs ; un transport UDP pourra venir plus tard si les mesures du 10.5 le justifient.
- **Deux fils par connexion** (lecture, écriture) : adapté à quelques joueurs, pas à des milliers.
- **Réseau simulé** : latence et gigue, coupures ; pas de perte de paquets isolée ni de débit limité (TCP les masque en retards, que la gigue représente).
- **Limites de taille** : 8 Kio par message de client, 4 Mio par message du serveur. Un historique de combat plus gros (plafond de commandes relevé très au-delà des 20 000 livrées, identifiants très longs) ne pourrait pas être envoyé : depuis la revue de la PR #19, un tel combat est refusé avant de commencer (pas de découpage de l'historique en plusieurs messages).

## Sous-lot 10.2 : serveur

- **Une salle par serveur**, places dans l'ordre des personnages du scénario ; le choix de la place n'est pas possible.
- **Pas de jonction en cours de combat** : seul un joueur déconnecté revient, avec son jeton, dans les 30 s.
- **Personnage abandonné** : il reste dans le combat, inactif, et peut être vaincu (le combat ne le retire pas).
- **Noms non vérifiés** : deux joueurs peuvent porter le même nom ; pas de comptes avant un serveur public.
- **Retard du serveur** : au plus 5 ticks rattrapés par passage ; un serveur trop lent ralentit le combat pour tous (comme le pont temps réel).
- **Historique en mémoire** : borné par le plafond de commandes du combat (20 000 dans les règles livrées).
- **Serveur dédié** : demande le SDK .NET 10 ; l'hôte Unity (10.4) n'en aura pas besoin.

## Sous-lot 10.3 : client

- **Pas de prédiction** : l'affichage montre le dernier tick reçu, en retard d'une latence aller sur le serveur ; une action du joueur prend effet après la latence aller, au tick suivant du serveur (50 ms au plus), et se voit après la latence retour. La parade est la plus exposée : mesures et options au 10.5, décision de Damien ensuite.
- **Reconstruction complète** après une divergence ou un retour : le combat est rejoué depuis son début (au plus la durée du combat, 1 200 ticks pour le duo) ; rapide en pratique, mais proportionnel au temps écoulé.
- **Retour manuel** : le client ne rouvre pas la connexion lui-même (il ne connaît pas le transport) ; l'appelant fournit la nouvelle connexion (Unity le fera au 10.4).
- **Client à appeler régulièrement** : un client qui ne lit plus (jeu figé plusieurs minutes) dépasse les 4 096 messages en attente et sa connexion est coupée ; il peut alors revenir pendant un combat.
- **Confiance dans le serveur** : un serveur fautif ne fait pas planter le client (lecture stricte, moteur protégé), mais le client montre ce qu'il dit. L'empreinte FNV-1a détecte les divergences accidentelles, pas une falsification.

## Sous-lot 10.4 : Unity

- **Pas essayé dans l'éditeur par Claude** : Unity ne tourne pas dans les sessions cloud. Les scripts compilent en CI contre les bouchons de l'API Unity (`EdS.Unity.CompileCheck`) et tout ce qui ne dépend pas d'Unity (`NetworkSession`, `BotPlayer`, arrêt de l'hôte) est testé, TCP réel compris ; l'essai dans Unity est celui de Damien.
- **Réseau local seulement** : pas de passage par Internet (box, NAT), pas de découverte automatique : on tape l'adresse affichée par l'hôte. Port d'hébergement fixe (7777) ; Windows demande d'autoriser Unity sur les réseaux privés.
- **Équipier robot sommaire** : il réagit à la télégraphie et lance ce qui est prêt, sans tactique.
- **Un seul scénario en réseau** (`coop/duo_meute`) ; place attribuée dans l'ordre d'arrivée (le robot peut prendre la première).
- **Pas de pause** en réseau (le serveur mène le combat) ; talents et builds se changent en salle d'attente seulement ; le build composé n'est pas sauvegardé.
- **Positions toujours visuelles** : l'allié est dessiné à côté du joueur ; chacun voit son personnage au centre.
