# Rapport de mesures du sous-lot 10.5 — jeu en réseau

Mesures obtenues avec `eds reseau` (sous-lot 10.5) : la salle (`CombatRoom`) et deux clients (`NetworkClient`) du lot 10 jouent le scénario de coopération `coop/duo_meute` (deux Bastions, Rempart et Gardien, contre deux loups et un hurleur, 60 s) sur le **réseau simulé** : latence d'un sens identique pour les deux joueurs, gigue tirée par `Pcg32`, coupures provoquées. Tout est en temps virtuel : mêmes réglages, mêmes résultats (vérifié par les tests `Lot105NetworkMeasureTests`). Moteur 0.14.0, journal au format 8, données inchangés.

```bash
dotnet run --project src/EdS.Cli -- reseau              # 3 graines par réglage (environ 20 s)
dotnet run --project src/EdS.Cli -- reseau --graines 5
dotnet run --project src/EdS.Cli -- reseau --scenario scenarios/mesures/s4_protection.json   # tout scénario à deux personnages Bastion
```

## Méthode

- **Joueurs scriptés**, identiques pour les deux places : ils voient ce que montre leur client (l'état reçu du serveur, compte à rebours interpolé entre deux ticks, à la précision d'une image de 10 ms). Garde levée quand un coup qui les vise arrive dans moins de 0,8 s ; compétences sinon, une à la fois en attendant d'en voir le lancer ; **parade** sur chaque coup parable qui les vise.
- **Moment de la parade** :
  - **à vue** : 50 ms avant le coup affiché, à ± 25 ms près (imprécision tirée au hasard, graine de la partie). C'est le bon moment en solo : la parade s'applique au tick en préparation et couvre les coups des trois ticks suivants (fenêtre de 150 ms), elle réussit donc si on la presse entre 100 ms avant le coup affiché et 50 ms après.
  - **anticipée** : le même moment avancé du temps aller-retour que le client mesure (ping). C'est exactement ce que ferait, pour un joueur qui pare à vue, une **télégraphie affichée en avance du ping** (option B ci-dessous).
- **Mesures** : parades pressées ; parfaites et ratées selon le **serveur** (résultat du combat) ; commandes refusées par le moteur (parade pendant la récupération, compétence déjà lancée…) ; délai entre la pression et le tick du serveur qui l'applique ; retard de l'affichage sur le serveur ; temps aller-retour mesuré ; divergences ; empreintes finales des clients comparées au journal du serveur.
- **Réussite** = parfaites / pressées. Elle n'atteint pas 100 % même sans latence : une partie des pressions tombe sur un coup interrompu (assaillant étourdi ou mort), une fenêtre déjà ouverte ou une récupération. Le bon repère est la ligne à 0 ms (213 parades parfaites sur 3 parties).

## Parades selon la latence

Deux joueurs, 3 graines par réglage (une graine change les délais tirés et l'imprécision des joueurs) :

| Parade | Latence (un sens) | Pressées | Parfaites | Réussite | Ratées | Refusées | Délai d'action moyen | p95 | Retard d'affichage | Aller-retour mesuré | Désync. | Empreintes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| à vue | 0 ms | 277 | 213 | 76,9 % | 3 | 34 | 30 ms | 50 ms | 10 ms | 20 ms | 0 | égales |
| à vue | 25 + 0 à 5 ms | 267 | 201 | 75,3 % | 3 | 42 | 51 ms | 70 ms | 30 ms | 60 ms | 0 | égales |
| à vue | 50 + 0 à 10 ms | 199 | 0 | 0,0 % | 138 | 47 | 79 ms | 100 ms | 60 ms | 120 ms | 0 | égales |
| à vue | 75 + 0 à 15 ms | 199 | 0 | 0,0 % | 137 | 48 | 105 ms | 130 ms | 87 ms | 172 ms | 0 | égales |
| à vue | 100 + 0 à 20 ms | 203 | 0 | 0,0 % | 140 | 54 | 133 ms | 160 ms | 115 ms | 230 ms | 0 | égales |
| à vue | 150 + 0 à 30 ms | 207 | 0 | 0,0 % | 140 | 60 | 184 ms | 220 ms | 170 ms | 341 ms | 0 | égales |
| à vue | 200 + 0 à 40 ms | 205 | 8 | 3,9 % | 141 | 48 | 242 ms | 280 ms | 225 ms | 449 ms | 0 | égales |
| à vue | 100 + 0 à 60 ms | 201 | 0 | 0,0 % | 137 | 52 | 153 ms | 190 ms | 135 ms | 274 ms | 0 | égales |
| à vue | 200 + 0 à 120 ms | 201 | 18 | 9,0 % | 141 | 32 | 284 ms | 350 ms | 270 ms | 553 ms | 0 | égales |
| anticipée | 0 ms | 277 | 213 | 76,9 % | 3 | 45 | 27 ms | 50 ms | 10 ms | 20 ms | 0 | égales |
| anticipée | 25 + 0 à 5 ms | 267 | 201 | 75,3 % | 3 | 51 | 46 ms | 70 ms | 30 ms | 60 ms | 0 | égales |
| anticipée | 50 + 0 à 10 ms | 267 | 201 | 75,3 % | 0 | 55 | 75 ms | 100 ms | 60 ms | 120 ms | 0 | égales |
| anticipée | 75 + 0 à 15 ms | 271 | 221 | 81,5 % | 10 | 27 | 103 ms | 130 ms | 87 ms | 175 ms | 0 | égales |
| anticipée | 100 + 0 à 20 ms | 275 | 226 | 82,2 % | 10 | 24 | 131 ms | 160 ms | 115 ms | 231 ms | 0 | égales |
| anticipée | 150 + 0 à 30 ms | 278 | 211 | 75,9 % | 15 | 37 | 186 ms | 220 ms | 170 ms | 341 ms | 0 | égales |
| anticipée | 200 + 0 à 40 ms | 283 | 212 | 74,9 % | 28 | 26 | 244 ms | 280 ms | 225 ms | 446 ms | 0 | égales |
| anticipée | 100 + 0 à 60 ms | 266 | 214 | 80,5 % | 17 | 22 | 152 ms | 190 ms | 135 ms | 274 ms | 0 | égales |
| anticipée | 200 + 0 à 120 ms | 265 | 148 | 55,8 % | 57 | 42 | 286 ms | 350 ms | 270 ms | 554 ms | 0 | égales |

## Coupures

Le premier joueur perd sa connexion à 5 s de combat (latence 50 + 0 à 10 ms, parade anticipée) et revient après la durée indiquée ; le serveur garde sa place 30 s :

| Coupure | Retour | Reprise après la nouvelle connexion | Resynchronisations | Désync. | Fin du combat | Empreintes |
|---:|---|---:|---:|---:|---|---|
| 1 s | réussi | 130 ms | 1 | 0 | DurationReached, 60,0 s | égales |
| 3 s | réussi | 130 ms | 1 | 0 | DurationReached, 60,0 s | égales |
| 10 s | réussi | 130 ms | 1 | 0 | DurationReached, 60,0 s | égales |
| 25 s | réussi | 130 ms | 1 | 0 | DurationReached, 60,0 s | égales |
| 35 s | refusé : refusé par le serveur : jeton inconnu ou expiré. | — | 0 | 0 | TeamDefeated, 54,0 s | égales |

## Ce que montrent les mesures

1. **Aucune divergence** dans les 59 parties mesurées : à chaque tick, les clients retrouvent l'empreinte du serveur, gigue forte et coupures comprises. Le retour après une coupure prend 130 ms (une connexion, l'accueil et l'historique complet) et rejoue tout le combat à l'identique ; au-delà de 30 s, la place est perdue comme décidé le 19 octobre.
2. **Délais** : l'affichage a environ une latence de retard sur le serveur, et une action s'applique après la latence et le tick suivant (25 ms en moyenne). Une parade « à vue » arrive donc au serveur environ un aller-retour plus tard qu'en solo.
3. **La parade à vue s'effondre d'un coup** : intacte jusqu'à 25 ms d'un sens (50 ms de ping), **nulle dès 50 ms d'un sens (100 ms de ping)**. La fenêtre de 150 ms laisse peu de marge, et le joueur pare alors systématiquement trop tard (les parades deviennent des parades ratées, avec leur récupération de 500 ms).
4. **La parade anticipée du ping tient** : 75 à 82 % jusqu'à 200 ms d'un sens (400 ms de ping), autant qu'à 0 ms. Elle se dégrade seulement quand la **gigue** est forte (56 % à 200 + 0 à 120 ms) : le ping mesuré ne prédit plus le délai du prochain message.
5. **Repères** : en réseau local (Wi-Fi, box), la latence d'un sens reste sous quelques millisecondes ; sur Internet en France, un serveur proche donne souvent 10 à 30 ms d'un sens, un serveur lointain ou une connexion mobile 50 à 150 ms. La coopération en réseau local du lot 10 n'est donc pas gênée ; un serveur sur Internet le serait.

## Options pour la parade (décision de Damien)

La décision du 19 octobre réserve ce choix à Damien après ces mesures. Rien n'est changé dans ce sous-lot.

| Option | Principe | Pour | Contre |
|---|---|---|---|
| **A. Ne rien compenser** | La parade se joue comme en solo ; au-delà d'environ 25 ms d'un sens, c'est au joueur d'anticiper | Aucun travail ; règles identiques en solo et en réseau | Parade quasi impossible dès 100 ms de ping sur Internet |
| **B. Télégraphie avancée du ping** (côté client) | La barre de télégraphie et le compte à rebours s'affichent en avance du temps aller-retour mesuré ; le joueur pare « à vue » sur cet affichage | Mesuré ici (« anticipée ») : 75 à 82 % jusqu'à 200 ms d'un sens ; aucune règle changée, aucune triche possible (le serveur juge toujours sur son tick) ; peu de travail (affichage seulement) | Sensible à la gigue forte ; l'affichage montre le coup un peu avant qu'il ne tombe chez l'autre joueur |
| **C. Rembobinage borné** (côté serveur) | Le client joint à sa parade le tick qu'il voyait ; le serveur la juge comme si elle était arrivée jusqu'à, par exemple, 150 ms plus tôt | Le plus juste pour le joueur, même avec de la gigue | Changement du moteur et du journal (parade rétroactive, nouvelle version) ; triche bornée possible (annoncer un tick ancien pour parer après coup) ; plus de travail |
| **D. Fenêtre élargie en réseau** | La fenêtre de parade passe de 150 ms à 150 ms + le ping (plafonné) | Simple | Change l'équilibrage (parade plus facile en réseau qu'en solo, Œil du duelliste dévalué) ; un faux ping élargit la fenêtre |

Avis de Claude, à titre indicatif : **B** d'abord (gain mesuré, aucune règle touchée, travail limité à l'affichage du client), en gardant **C** pour le jour où un serveur sur Internet sera ouvert à des connexions moins stables. **A** suffit tant que l'on joue en réseau local.

**Question pour Damien** : quelle option pour la parade en réseau (A, B, C, D, ou une autre) ?

## Limites des mesures

- Joueurs scriptés, plus réguliers qu'un humain (imprécision de ± 25 ms seulement) : les pourcentages sont des ordres de grandeur, la forme des courbes compte plus que les chiffres.
- Réseau simulé : latence et gigue par message, ordre conservé comme en TCP ; ni perte isolée ni débit limité (TCP les transforme en retards, que la gigue représente).
- Un seul scénario (coopération contre la meute) et deux builds (Rempart et Gardien) ; les parties durent 60 s.
- Le moment de parade « anticipée » suppose un ping bien mesuré (moyenne lissée du client) ; avec une gigue forte, il se trompe d'autant.
