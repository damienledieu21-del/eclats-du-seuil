# Limites du lot 11 (combat dans l'espace)

Plan du lot et décisions : [PLAN_LOT_11.md](PLAN_LOT_11.md), [CADRAGE.md](CADRAGE.md#5-décisions-du-20-octobre-2026-l11-d1-à-l11-d12).

## Sous-lot 11.1 : ennemis qui meurent

- **Pas encore de positions** : la mort est jugée sans distance ni placement, comme tout le combat jusqu'au moteur spatial (11.4).
- **Assaillants scriptés** : un assaillant mortel garde le comportement de l'entraînement (attaques à intervalle fixe, dans l'ordre). Il n'a ni puissance offensive propre (ses réactions infligent 0, comme avant), ni comportement dans l'espace (11.5).
- **Cycle de vie minimal** : vivant → mort, sans retour dans un même combat. Réapparition et réveil renforcé ne sont pas réalisés ; le contrat qui les rendra possibles est décrit dans [MOTEUR.md](MOTEUR.md#cycle-de-vie-des-assaillants-sous-lot-111). La mort ne donne encore rien : expérience, butin et registre des récompenses viendront au lot 12.
- **Mannequins** : toujours immortels (`dummy` n'accepte pas `health`). Un scénario qui veut une cible mortelle immobile prend un assaillant.
- **Valeurs provisoires** : les PV de la meute à vaincre (solo : 600, 600, 500 ; coopération : 900, 900, 700) sont réglés pour qu'un joueur qui se défend gagne en moins d'une minute et qu'un joueur qui ne se défend pas tombe. Ils sont mesurés avec des scripts simples (tests), pas encore avec Damien.
- **Fin de combat** : quand tous les ennemis sont morts, le combat s'arrête aussitôt (`teamDefeated`). Dans le monde partagé (lots 12 et 13), une rencontre ne terminera plus la simulation de la zone : ce sera une règle de la zone, pas du moteur.
- **Formats** : moteur 0.15.0, journal au format 9, scénarios en schéma 4. Les journaux du moteur 0.14.0 sont refusés au rejeu, sans migration (pré-alpha) ; leurs lignes d'événements sont reproduites par le nouveau moteur (tests de non-régression).

## Sous-lot 11.4a : positions, déplacement et décor

Détail du fonctionnement : [MOTEUR.md](MOTEUR.md#combat-positionné-sous-lot-114a).

- **Pas encore de portées** : dans un combat positionné, compétences et attaques touchent encore sans regarder la distance ni la ligne de vue. Portées, ciblage et refus `targetOutOfRange` et `noLineOfSight` : 11.4b. Attaques annoncées au sol et garde de face (arc de 180°, orientation en garde vers la cible) : 11.4c. L'orientation est déjà tenue (direction de marche) pour ces morceaux.
- **Combattants fixés au départ** : personne n'entre ni ne sort en cours de combat, et il n'y a pas encore d'instantané d'état : 11.4d.
- **Créatures sans comportement** : un assaillant ne marche que par des commandes `move` et `stop` écrites dans le scénario. Approcher, poursuivre, encercler : 11.5.
- **Un seul niveau** : pas de surfaces superposées ni de passages entre elles (pont, souterrain). La proposition retenue pour la première démonstration n'en a qu'une ; les surfaces viendront avec la zone des Vallons.
- **Garde-fou anti-piège local** : il protège un joueur coincé contre le décor par une créature (huit directions d'essai, un pas d'essai), pas un joueur enfermé dans un cul-de-sac fermé à distance. Une créature qui pousse peut aussi gêner un joueur sans l'enfermer (elle glisse autour de lui). L'essai du 11.6 dira si la règle A tient avec ce garde-fou, sinon la règle B.
- **Ordre de déplacement** : les combattants avancent un par un dans l'ordre de leurs identifiants ; à contact égal, le premier prend la place. C'est déterministe, pas simultané.
- **Petits glissements ignorés** : un pas qui n'avance pas d'un huitième de sa longueur est annulé (pas de tremblement) ; pousser presque de face contre un mur ne fait donc pas glisser, et un combattant peut s'arrêter jusqu'à un huitième de pas avant le contact.
- **Étourdissement** : il immobilise (blocage `movement`, décision de Damien du 20 octobre 2026 à 03 h 38) ; la commande `move` reste admise pendant l'étourdissement (l'intention est gardée) et le combattant repart à la fin. Aucune autre altération livrée ne bloque le déplacement.
- **Réseau** : le protocole du lot 10 ne transmet pas les déplacements (refus explicite à l'écriture et à la lecture). Le combat positionné en réseau, avec prédiction du déplacement local, est le 11.7.
- **Unity** : le prototype déplace encore son personnage lui-même, hors du cœur ; le déplacement simulé par le moteur arrive avec la scène du 11.6.
- **Volume** : au plus 64 obstacles de 16 sommets, coordonnées dans ±1000 m ; aucune partition de l'espace (chaque pas teste tous les obstacles et toutes les emprises, avec un écart rapide par boîte englobante). Suffisant pour la démonstration, à revoir pour une zone peuplée (lot 13).
- **Commandes** : un joystick qui change de direction à chaque tick produit une commande par tick ; `combat.limits.maxCommands` (20 000) borne un combat fermé de dix minutes. La zone (11.7, lot 13) n'aura pas cette limite globale.
- **Formats** : moteur 0.16.0, journal au format 10, scénarios en schéma 5, règles en schéma 10 (contenu 0.9.0). Un combat sans espace écrit les mêmes lignes qu'au moteur 0.15.0 (treize scénarios livrés comparés) ; un journal 0.15.0 est refusé au rejeu, sans migration.
