# Essai de fin du lot 11 (sous-lot 11.9) — fiche remplie et rapport

Objet : [PLAN_LOT_11.md](PLAN_LOT_11.md), 11.9 : les quatre vérifications de H14, l'esquive en réseau, le choix du contact joueur–créature, la valeur du ralentissement en garde, puis la réestimation des lots 12 à 14. Cette fiche rassemble tout ce que l'essai doit trancher ; Damien coche et note, Claude en tire le rapport, les réglages et l'estimation révisée.

Statut de chaque choix : **retenu** quand Damien l'a décidé, **recommandation** sinon (aucune recommandation ci-dessous n'est une décision).

**Essai fait par Damien le 22 octobre 2026** : sur PC dans Unity 6.3 (fiche remplie de 20 h 46 à 21 h 07), puis sur le Galaxy Note8 après le correctif de la PR #37 (fiche du téléphone remplie à 1 h 43 le 23 octobre). Réponses relevées dans la fiche en ligne ; rapport et suites à la [section 7](#7-rapport-de-lessai-sous-lot-119).

## 1. Les quatre vérifications de H14 (scène des Vallons, [UNITY.md](UNITY.md#4-jouer))

| Vérification | Ce qu'on regarde | Résultat (OK / à revoir, note) |
|---|---|---|
| Même combat dans les deux vues | jouer une partie en vue 1, une en vue 2 : mêmes portées, mêmes collisions, mêmes esquives ; rien ne bascule tout seul | **OK** |
| Obstacles et passages | le pont, le passage étroit, l'abri : on passe où il faut, pas à travers ; un ennemi caché le reste | **OK** |
| Commande et stabilité | la marche répond, s'arrête net, ne tremble pas contre un mur ; pas de blocage ni d'erreur | **À revoir** : « en vue 2 la touche S (reculer) ne recule pas et fait des trucs bizarres ». Choix : le Bastion recule en regardant devant lui → fait au 11.9 (marche à reculons) |
| Lisibilité et performances | annonces au sol, barres, cible lisibles ; image fluide (PC, puis téléphone) | **OK** |

## 2. Réglages à choisir

| Réglage | Aujourd'hui | Options | Choix de Damien |
|---|---|---|---|
| Contact joueur–créature | **A** (volumes solides sans poussée, garde-fou contre le piège) | A, ou **B** (traversée freinée : on peut traverser une créature, en ralentissant fortement) | **Retenu : garder A (solide)** |
| Ralentissement en garde | moitié de la vitesse (`combat.movement.guardSpeedFactor` 0,5) | une autre fraction (0,3 à 0,8), ou pas de ralentissement | **Retenu : garder la moitié** |
| Angles et distances des vues | vue 1 : 52° à 17 m ; vue 2 : 16° à 6 m | plus haut, plus bas, plus près, plus loin | **Retenu : garder** |
| La rivière arrête la vue | oui (un seul genre d'obstacle dans le moteur) | garder ; ou un « obstacle bas » qui arrête la marche et laisse passer la vue (nouveau genre d'obstacle) | **Retenu : on voit par-dessus (obstacle bas)** → fait au 11.9 |

## 3. Esquive en réseau (sous-lot 11.7, `eds reseau --esquive`)

Mesure faite (cercle de 2 m annoncé 900 ms avant l'impact, sortir demande 0,5 s de marche) : un joueur parfait esquive jusqu'à 150 ms de latence d'un sens ; avec une réaction humaine de 250 ms, jusqu'à 50 ms. Détail : [RESEAU.md](RESEAU.md#esquive-mesurée-eds-reseau---esquive-60-s-par-ligne-39-impacts).

| Question | Options | Choix |
|---|---|---|
| Durée des annonces face à la latence | garder (bon jeu en réseau local et sur une bonne connexion) ; allonger les annonces des grosses zones ; réduire leur rayon | Reporté (« pas compris » ; section reprise plus tard, choix de Damien) |
| Rattrapage côté serveur | aucun (l'impact se juge aux places du serveur, recommandation actuelle) ; ou remonter le temps du joueur (« lag compensation », plus permissif, plus complexe) | Reporté (idem) ; recommandation inchangée : aucun |
| Essai à deux postes | ressenti de l'esquive avec ton ping réel | Reporté (idem) |

## 4. Points de comportement laissés ouverts par les sous-lots

| Sous-lot | Point | Aujourd'hui | Choix |
|---|---|---|---|
| 11.4b | Jugement du colosse sans ennemi à 5 m | une parade parfaite sans ennemi à portée ne rapporte pas ses 10 points de Résonance (règle générale des réactions) | **Retenu : garder** (pas de Résonance si aucun ennemi à moins de 5 m n'est blessé) |
| 11.4c | Contrôle pendant une annonce | un étourdissement posé pendant l'annonce ne l'interrompt pas | **Retenu : l'étourdissement l'annule** → fait au 11.9 |
| 11.4c | Garde tournée vers la cible | instantanée | **Retenu : garder instantanée** |
| 11.4c | Provocation et zones visées | une provocation détourne aussi les zones visées (cône, bande, cercle autour de la cible) : le Bastion qui provoque prend le souffle à la place de son allié | **Retenu : garder** |
| 11.5 | Arrêt pendant l'annonce | un ennemi s'arrête de marcher pendant l'annonce de chacune de ses attaques, coups dirigés compris | **Retenu : « ça dépend des attaques et du type d'ennemi »** → réglable par attaque au 11.9 (`holdPosition`) ; l'archer des Vallons continue à reculer pendant sa pluie |
| 11.5 | Engagement par la meute | un allié engagé entraîne les autres de proche en proche | **Retenu : « il faut les deux, ça doit dépendre des situations »** → réglable par ennemi au 11.9 (`pack`) ; le sanglier et l'archer des Vallons ne suivent plus les loups |
| 11.5 | Charge | s'arrête au contact, sans repousser le joueur | **Retenu : il te repousse** → fait au 11.9 (`push`, 2 m pour le sanglier) |
| 11.5 | Retour au calme | aucun : un ennemi engagé le reste (pas de laisse ; règles de rencontre au lot 12) | **Retenu : ajouter une laisse au lot 12** ; « si on avance plus vite que l'ennemi, il faut suffisamment s'éloigner pour que l'ennemi lâche » |
| 11.6 | Cible perdue de vue | remplacée aussitôt par le premier ennemi en vue | **Retenu : après un court délai** → une seconde au 11.9 |

## 5. Téléphone (sous-lot 11.8, [UNITY.md](UNITY.md#7-jouer-sur-téléphone-sous-lot-118))

| Vérification | Résultat |
|---|---|
| Version d'Android du Note8 (Informations sur le logiciel) | **Android 9** |
| Installation et démarrage sur le Note8 | **OK** (après la PR #37 : le premier jeu construit restait bloqué, faute de shader emporté) |
| Marche au pouce | **OK** |
| Boutons (taille, place) | **OK** |
| Geste entre les deux vues (pincer, glisser) | **OK** |
| Lisibilité et fluidité | **OK** |
| Réseau depuis le téléphone (facultatif) | Pas essayé |

## 6. Ensuite (fait)

Avec cette fiche remplie, Claude : applique les réglages choisis (valeurs de règles, nouveau contenu versionné si besoin), écrit le rapport de l'essai, puis **réestime les lots 12 à 14** ([PLAN_LOT_11.md](PLAN_LOT_11.md), section 7 : l'estimation indicative est à refaire après le 11.7 et le 11.8, L11-D12).

## 7. Rapport de l'essai (sous-lot 11.9)

**Bilan.** Les quatre vérifications de H14 passent, sauf la commande en vue 2 (recul) ; le jeu tourne sur le Note8 (Android 9) après un correctif (PR #37) ; les réglages de départ du combat dans l'espace sont gardés (contact A, garde à la moitié de la vitesse, vues, garde tournée instantanément, provocation, jugement du colosse). Damien a demandé sept changements, tous faits au 11.9 (code écrit et tests locaux verts ; état de la revue et de la fusion : voir les PR) :

| Demande | Fait | Où |
|---|---|---|
| Reculer en vue 2 en regardant devant soi | marche à reculons (`backward` d'une commande `move`) ; Unity l'envoie en vue 2 quand on recule ; protocole réseau 3 | 11.9a (moteur), 11.9b (Unity) |
| Rivière qui laisse voir par-dessus | obstacle bas (`low`) : arrête la marche, pas la vue | 11.9a |
| Étourdissement qui annule l'attaque annoncée | un assaillant encore étourdi à l'impact perd son attaque (`attackCancelled`) | 11.9a |
| Charge qui repousse | bande qui repousse (`push`), 2 m pour le sanglier des Vallons | 11.9a |
| Arrêt pendant l'annonce selon l'attaque et l'ennemi | `holdPosition` par attaque (l'archer recule pendant sa pluie) | 11.9a |
| Meute selon la situation | `pack` par ennemi (sanglier et archer hors meute) | 11.9a |
| Cible perdue remplacée après un court délai | une seconde (`TargetKeeper`, solo et réseau) | 11.9b |
| Unity ne demande plus d'enregistrer une scène | scène de démarrage créée par l'éditeur | 11.9b |

**Reporté.** L'esquive en réseau (section 3 : durée des annonces, rattrapage côté serveur, essai à deux postes) sera reprise plus tard (choix de Damien) ; la recommandation reste « aucun rattrapage ». La laisse (retour au calme d'un ennemi distancé) va au lot 12, avec les règles de rencontre. iPhone 13 : toujours sans chaîne iOS (Mac et Xcode).

**À essayer par Damien** (prochain essai, sans urgence) : le recul en vue 2, la rivière basse (le sanglier et l'archer sont visibles dès le départ), la charge qui repousse, la cible gardée une seconde derrière un arbre, et la construction Android sans question de scène.

**Réestimation des lots 12 à 14** : [PLAN_LOT_11.md](PLAN_LOT_11.md#7-estimation), section 7 (révisée le 23 octobre 2026).
