# Lot 11 — vertical slice : plan proposé (en attente des décisions de Damien)

Proposition de Claude du 19 octobre 2026, après la fusion du lot 10. **Rien n'est décidé ici** : les choix de gameplay reviennent à Damien (section « Questions »). Une fois tranchés, ils passent dans la [feuille de route](FEUILLE_DE_ROUTE.md) et le lot démarre par le premier sous-lot.

## Objectif

Feuille de route, « Après le lot 8 », point 3 : **une zone, le Bastion jouable, quelques ennemis, une mission, progression, récompense, inventaire, une activité de récolte et fabrication, interactions avec un deuxième joueur.** C'est la première fois que le jeu sort de l'arène : on se déplace dans un lieu, on y fait quelque chose, et on en garde le résultat.

## Point de départ

Ce qui existe et sert tel quel :

- le **moteur de combat** déterministe (lots 5 à 8) et le **Bastion** complet (trois arbres, actives, ultime, passifs, builds de référence) ;
- le **prototype Unity** (lot 9) : arène, deux caméras, interface de combat, écran des talents ;
- le **réseau** (lot 10) : serveur autoritaire, combat à deux, reconnexion, mesures ; la parade en réseau (option B) en revue.

Ce qui manque, vérifié dans le code :

| Manque | Détail |
|---|---|
| **Ennemis qui meurent** | Les assaillants n'ont pas de points de vie (`AssailantSetup` sans PV, `Health` nul) : ce sont des mannequins qui frappent. Un combat ne se gagne donc pas, il dure jusqu'au temps imparti. Il faut des PV, la mort d'un assaillant et la victoire des joueurs : changement du moteur, du schéma des scénarios et du journal (nouvelles versions, nouveaux journaux de référence). |
| **Progression par niveau** | Les règles de niveau (points de talent, actives, ultime, passifs) existent et sont vérifiées par `BuildValidator` ; des builds valides existent aux niveaux 30 et 10 (`build.bastion.niveau_10`), et un débutant de niveau 1 dans les tests. Mais tous ont les **attributs du niveau 30** : il manque le **budget d'attributs par niveau** (« chantier ultérieur » dans la feuille de route) et l'**expérience** qui fait monter de niveau. |
| **Monde** | Aucune notion de zone, de déplacement hors combat, de rencontre, de mission, d'objet, d'inventaire, de récolte, de fabrication ni de sauvegarde. Le moteur n'a pas de positions (décision du lot 8) : ce qui se passe hors combat est à construire à côté. |
| **Deuxième joueur hors combat** | Le réseau ne partage que les combats (salle d'attente puis combat) ; personne ne voit l'autre se déplacer. |

## Principe proposé

Garder intact ce qui marche et ajouter une **couche « monde »** à côté du moteur de combat :

- un nouveau projet **`EdS.World`** (netstandard2.1, C# 9, sans dépendance, comme les autres), qui gère hors combat : personnage (niveau, expérience, inventaire), zone et rencontres, mission, récolte, fabrication, récompenses. Déterministe et testé sans Unity, comme le combat ;
- pour chaque rencontre, le monde **fabrique un combat** (`CombatSetup`) avec les ennemis rencontrés, le moteur le joue comme aujourd'hui, et le monde **lit le résultat** (victoire, ennemis tués) pour donner expérience et butin. Le moteur reste seul juge du combat ;
- de nouvelles **données** (ennemis, butin, objets, recettes, zone, mission) dans `data/`, avec schémas versionnés et le validateur commun ;
- une **sauvegarde** du personnage dans un fichier versionné (local pour la slice ; un serveur de comptes viendra plus tard) ;
- **Unity** affiche la zone et transmet les intentions, comme pour le combat.

## Découpage proposé

| Sous-lot | Contenu | Taille |
|---|---|---|
| 11.1 | **Ennemis qui meurent** : PV des assaillants (données), mort, victoire des joueurs ; nouvelles versions du schéma et du journal, nouveaux journaux de référence ; Unity montre la barre de vie et la mort | moyenne |
| 11.2 | **Ennemis et butin en données** : catalogue d'ennemis (au lieu de les écrire dans chaque scénario), tables de butin, objets ; validation | moyenne |
| 11.3 | **Personnage qui progresse** (`EdS.World`) : niveau, expérience, attributs par niveau, déblocages (actives, passifs, ultime, points de talent) ; sauvegarde versionnée | moyenne |
| 11.4 | **Zone, rencontres, récompenses** : zone en données (points d'intérêt, groupes d'ennemis, réapparition), rencontre → combat → expérience et butin, défaite → retour au camp ; inventaire | grande |
| 11.5 | **Récolte et fabrication** : nœuds de récolte, temps, réapparition ; recettes ; objets fabriqués utiles (selon la question 6) | moyenne |
| 11.6 | **Mission** : étapes (tuer, récolter, fabriquer, rapporter), suivi, récompense finale | moyenne |
| 11.7 | **Unity** : la zone jouable (terrain simple, déplacements, ennemis visibles, nœuds, personnage de mission, inventaire, fabrication, montée de niveau et talents) | grande |
| 11.8 | **Deuxième joueur** : selon la question 8 | moyenne à grande |
| 11.9 | **Essai complet et mesures** : parcours de la mission du début à la fin, durée, difficulté, rapport et réglages | petite |

Chaque sous-lot suit la méthode habituelle (branche, tests, CI, revue Codex, PR). 11.1 sert dans tous les cas ; l'ordre des suivants peut changer.

## Questions pour Damien

Pour chaque question : les options, puis mon avis. Réponds simplement par la lettre, ou corrige.

**Q1. La zone.** Quel lieu et quelle ambiance ? Taille visée ?
- A. Une lisière de forêt sombre avec des ruines (loups, bandits), environ 5 minutes pour la traverser.
- B. Un col de montagne avec une mine d'éclats (créatures de pierre).
- C. Autre idée à toi.
- *Avis* : A, petite (5 min), pour réutiliser loups et hurleur déjà faits.

**Q2. La mission.** Une seule mission en 3 à 5 étapes. Proposition : « Rallumer le Seuil » : chasser la meute qui rôde (tuer 6 loups), récolter 5 éclats bruts, faire forger une pièce d'équipement au camp, vaincre le chef de meute (mini-boss), revenir au camp.
- A. Celle-ci.
- B. Une autre histoire (dis-moi l'idée, je la découpe).
- *Avis* : A ; elle touche toutes les briques demandées (combat, récolte, fabrication, récompense).

**Q3. La progression dans la slice.** Jusqu'à quel niveau ?
- A. Du niveau 1 au niveau 5 (2 puis 3 actives, 4 points de talent) : court, simple.
- B. Du niveau 1 au niveau 10 (4 actives et l'ultime au niveau 10) : on voit tout le Bastion se construire, environ 45 minutes de jeu.
- *Avis* : B. Il faut aussi décider du **budget d'attributs par niveau** (aujourd'hui, tous les builds, même celui de niveau 10, ont les attributs prévus pour le niveau 30) et de la courbe d'expérience : je proposerai une courbe simple, à valider.

**Q4. L'équipement.**
- A. Pas d'équipement : la récompense est l'expérience et des objets de fabrication.
- B. Trois emplacements (arme, armure, bijou) qui ne donnent que des bonus de caractéristiques (pas d'effets spéciaux, comme décidé : « pas de déclencheurs d'équipement avant le chantier des objets »).
- *Avis* : B ; sans équipement, la fabrication n'a pas de but.

**Q5. L'inventaire.**
- A. Simple : 24 places, objets empilables, pas de poids, pas d'échange.
- B. Avec échange entre les deux joueurs.
- *Avis* : A pour commencer ; l'échange ira avec la question 8.

**Q6. Récolte et fabrication.** Quelle activité ?
- A. Récolte d'éclats (minerai) et forge simple : 3 à 4 recettes d'équipement (question 4).
- B. Cueillette de plantes et potions. Attention : une potion de soin donne du soin, que le Bastion n'a pas (décision A1, soin exclu au lot 8) : c'est une décision à part.
- *Avis* : A, sans potion de soin pour l'instant.

**Q7. La défaite.** Que se passe-t-il quand le Bastion tombe ?
- A. Retour au camp, sans perte (la rencontre réapparaît).
- B. Retour au camp avec une petite pénalité (par exemple perte d'une partie de l'expérience du niveau).
- *Avis* : A pour la slice.

**Q8. Le deuxième joueur.** Jusqu'où ?
- A. Chacun dans sa propre zone ; on se retrouve pour des combats à deux (le lot 10 tel quel), et la mission avance pour les deux.
- B. **Zone partagée** : on se voit se déplacer, on combat ensemble les mêmes ennemis, chacun a sa mission et son butin. Le serveur devient aussi le serveur de la zone.
- *Avis* : B, mais en dernier (11.8), une fois la slice jouable en solo : c'est le cœur d'un MMO, et le lot 10 en a posé les bases. A serait plus rapide mais ne montre pas le « monde partagé ».

**Q9. La sauvegarde.**
- A. Fichier local sur le PC (simple, sans compte).
- B. Sur le serveur, avec un compte.
- *Avis* : A pour la slice ; le serveur de comptes est un chantier à part (persistance).

**Q10. L'entrée en combat.**
- A. Sans transition : quand on approche d'un groupe d'ennemis, le combat démarre sur place, avec ces ennemis (positions seulement visuelles, comme aujourd'hui).
- B. Avec transition : on entre dans une arène séparée.
- *Avis* : A ; c'est plus vivant, et le prototype fait déjà ainsi dans l'arène.

## Ce qui est fait sans attendre

Rien n'est codé tant que ces questions ne sont pas tranchées, sauf accord de Damien pour démarrer 11.1 (ennemis qui meurent), utile dans tous les cas. Les valeurs (PV des ennemis, expérience, attributs par niveau, butin) resteront **provisoires** et réglées par mesures, comme au lot 8.
