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

