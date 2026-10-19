# Limites du lot 10 (serveur autoritaire à deux joueurs)

## Sous-lot 10.1 : protocole et transport

- **Réseau local seulement** : TCP sans chiffrement ni authentification forte. Le jeton de reconnexion (128 bits aléatoires) circule en clair. Un serveur ouvert sur Internet demandera TLS et des comptes (chantier ultérieur, avec une dépense d'hébergement à décider).
- **TCP** : ordre et intégrité garantis, mais un message perdu retarde les suivants (blocage en tête de file). Suffisant pour 20 ticks par seconde et deux joueurs ; un transport UDP pourra venir plus tard si les mesures du 10.5 le justifient.
- **Deux fils par connexion** (lecture, écriture) : adapté à quelques joueurs, pas à des milliers.
- **Réseau simulé** : latence et gigue, coupures ; pas de perte de paquets isolée ni de débit limité (TCP les masque en retards, que la gigue représente).
- **Limites de taille** : 8 Kio par message de client, 4 Mio par message du serveur. Un historique de combat plus gros (plafond de commandes relevé très au-delà des 20 000 livrées) ne pourrait pas être envoyé : la connexion serait fermée, sans effet sur le combat du serveur.
