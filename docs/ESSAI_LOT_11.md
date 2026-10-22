# Essai de fin du lot 11 (sous-lot 11.9) — fiche à remplir

Objet : [PLAN_LOT_11.md](PLAN_LOT_11.md), 11.9 : les quatre vérifications de H14, l'esquive en réseau, le choix du contact joueur–créature, la valeur du ralentissement en garde, puis la réestimation des lots 12 à 14. Cette fiche rassemble tout ce que l'essai doit trancher ; Damien coche et note, Claude en tire le rapport, les réglages et l'estimation révisée.

Statut de chaque choix : **retenu** quand Damien l'a décidé, **recommandation** sinon (aucune recommandation ci-dessous n'est une décision).

## 1. Les quatre vérifications de H14 (scène des Vallons, [UNITY.md](UNITY.md#4-jouer))

| Vérification | Ce qu'on regarde | Résultat (OK / à revoir, note) |
|---|---|---|
| Même combat dans les deux vues | jouer une partie en vue 1, une en vue 2 : mêmes portées, mêmes collisions, mêmes esquives ; rien ne bascule tout seul | |
| Obstacles et passages | le pont, le passage étroit, l'abri : on passe où il faut, pas à travers ; un ennemi caché le reste | |
| Commande et stabilité | la marche répond, s'arrête net, ne tremble pas contre un mur ; pas de blocage ni d'erreur | |
| Lisibilité et performances | annonces au sol, barres, cible lisibles ; image fluide (PC, puis téléphone) | |

## 2. Réglages à choisir

| Réglage | Aujourd'hui | Options | Choix de Damien |
|---|---|---|---|
| Contact joueur–créature | **A** (volumes solides sans poussée, garde-fou contre le piège) | A, ou **B** (traversée freinée : on peut traverser une créature, en ralentissant fortement) | |
| Ralentissement en garde | moitié de la vitesse (`combat.movement.guardSpeedFactor` 0,5) | une autre fraction (0,3 à 0,8), ou pas de ralentissement | |
| Angles et distances des vues | vue 1 : 52° à 17 m ; vue 2 : 16° à 6 m | plus haut, plus bas, plus près, plus loin | |
| La rivière arrête la vue | oui (un seul genre d'obstacle dans le moteur) | garder ; ou un « obstacle bas » qui arrête la marche et laisse passer la vue (nouveau genre d'obstacle) | |

## 3. Esquive en réseau (sous-lot 11.7, `eds reseau --esquive`)

Mesure faite (cercle de 2 m annoncé 900 ms avant l'impact, sortir demande 0,5 s de marche) : un joueur parfait esquive jusqu'à 150 ms de latence d'un sens ; avec une réaction humaine de 250 ms, jusqu'à 50 ms. Détail : [RESEAU.md](RESEAU.md#esquive-mesurée-eds-reseau---esquive-60-s-par-ligne-39-impacts).

| Question | Options | Choix |
|---|---|---|
| Durée des annonces face à la latence | garder (bon jeu en réseau local et sur une bonne connexion) ; allonger les annonces des grosses zones ; réduire leur rayon | |
| Rattrapage côté serveur | aucun (l'impact se juge aux places du serveur, recommandation actuelle) ; ou remonter le temps du joueur (« lag compensation », plus permissif, plus complexe) | |
| Essai à deux postes | ressenti de l'esquive avec ton ping réel | |

## 4. Points de comportement laissés ouverts par les sous-lots

| Sous-lot | Point | Aujourd'hui | Choix |
|---|---|---|---|
| 11.4b | Jugement du colosse sans ennemi à 5 m | une parade parfaite sans ennemi à portée ne rapporte pas ses 10 points de Résonance (règle générale des réactions) | |
| 11.4c | Contrôle pendant une annonce | un étourdissement posé pendant l'annonce ne l'interrompt pas | |
| 11.4c | Garde tournée vers la cible | instantanée | instantanée, ou à vitesse de rotation bornée |
| 11.4c | Provocation et zones visées | une provocation détourne aussi les zones visées (cône, bande, cercle autour de la cible) : le Bastion qui provoque prend le souffle à la place de son allié | |
| 11.5 | Arrêt pendant l'annonce | un ennemi s'arrête de marcher pendant l'annonce de chacune de ses attaques, coups dirigés compris | |
| 11.5 | Engagement par la meute | un allié engagé entraîne les autres de proche en proche | |
| 11.5 | Charge | s'arrête au contact, sans repousser le joueur | |
| 11.5 | Retour au calme | aucun : un ennemi engagé le reste (pas de laisse ; règles de rencontre au lot 12) | |
| 11.6 | Cible perdue de vue | remplacée aussitôt par le premier ennemi en vue | aussitôt, ou après un court délai |

## 5. Téléphone (sous-lot 11.8, [UNITY.md](UNITY.md#7-jouer-sur-téléphone-sous-lot-118))

| Vérification | Résultat |
|---|---|
| Version d'Android du Note8 (Informations sur le logiciel) | |
| Installation et démarrage sur le Note8 | |
| Marche au pouce | |
| Boutons (taille, place) | |
| Geste entre les deux vues (pincer, glisser) | |
| Lisibilité et fluidité | |
| Réseau depuis le téléphone (facultatif) | |

## 6. Ensuite

Avec cette fiche remplie, Claude : applique les réglages choisis (valeurs de règles, nouveau contenu versionné si besoin), écrit le rapport de l'essai, puis **réestime les lots 12 à 14** ([PLAN_LOT_11.md](PLAN_LOT_11.md), section 7 : l'estimation indicative est à refaire après le 11.7 et le 11.8, L11-D12).
