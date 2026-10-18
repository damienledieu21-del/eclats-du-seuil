# Prototype Unity — guide pas à pas (Windows)

Le prototype (lot 9) se trouve dans le dossier `unity/` du dépôt. Unity compile lui-même le cœur du jeu depuis `src/` : rien d'autre à installer que Unity et GitHub Desktop, et aucune scène à préparer.

## 1. Installer (une seule fois)

1. **Unity Hub** : [unity.com/download](https://unity.com/download), version Windows (x64 sur un processeur Intel ou AMD).
2. Dans le Hub : se connecter (compte Unity gratuit), puis **Preferences → Licenses → Add → Get a free personal license**.
3. **Installs → Install Editor → Unity 6.3 (LTS)**, version `6000.3.26f1` (celle du projet) ou une autre `6000.3.x`. Modules : *Microsoft Visual Studio Community* et *Windows Build Support (IL2CPP)*. Prévoir 15 Go ; sur un SSD si possible.
4. **GitHub Desktop** : [desktop.github.com](https://desktop.github.com), se connecter avec son compte GitHub, puis **File → Clone repository → eclats-du-seuil**.

## 2. Récupérer la bonne version

Tant que les PR du lot 9 ne sont pas fusionnées, le prototype n'existe que sur sa branche :

1. Dans GitHub Desktop, en haut : **Current branch → feat/lot-9-3-unity** (onglet *Other branches* au besoin).
2. **Fetch origin** puis **Pull origin** pour prendre les dernières modifications.

Une fois les PR fusionnées, rester simplement sur `main` et faire **Pull origin**.

## 3. Ouvrir le projet

1. Unity Hub → **Projects → Add → Add project from disk**.
2. Choisir le dossier **`unity`** à l'intérieur du dépôt cloné (par exemple `Documents\GitHub\eclats-du-seuil\unity`), pas le dépôt lui-même.
3. Si le Hub signale une version manquante (une autre `6000.3.x` est installée), **ne pas l'installer** : cocher la version 6.3 installée, puis ouvrir et accepter le changement de version.
4. La première ouverture prend plusieurs minutes : Unity importe le moteur de rendu et compile le cœur du jeu.
5. À la fin, la console (onglet **Console**) doit afficher « Éclats du Seuil : rendu URP configuré ». Le projet n'a pas de scène : la scène vide proposée par Unity suffit.

## 4. Jouer

Appuyer sur **Play** (▶ en haut au centre). L'arène se construit, avec le Bastion (capsule bleue) face à l'assaillant d'entraînement.

| Touche | Action |
|---|---|
| Z Q S D ou flèches | se déplacer (déplacement visuel, voir les limites) |
| clic droit (ou Maj) maintenu | garde : un coup est bloqué (endurance) |
| Espace | parade parfaite, juste avant l'impact |
| 1, 2, 3, 4 | compétences des emplacements |
| R | ultime (100 de Résonance) |
| Tab | ennemi suivant comme cible |
| C | caméra isométrique ↔ troisième personne |
| A / E | tourner la caméra isométrique |
| molette | zoom ; bouton du milieu maintenu : tourner la caméra en troisième personne |
| N | scénario suivant (entraînement, S1 à S5) |
| F1 à F5 | build : Rempart, Contrecoup, Gardien, hybride, niveau 10 |
| Retour arrière | recommencer la séance |
| Échap | pause |
| H | masquer ou afficher l'aide |

À l'écran : barres de PV, d'endurance, de Résonance et de Charges en bas ; les compétences avec leur recharge ; au-dessus de chaque ennemi, sa barre de PV et une **barre de télégraphie** qui se remplit avant son coup (rouge vif : coup **imparable**, à encaisser sans garde ni parade) ; la fiche de la cible en haut à droite ; les textes de combat (dégâts, « Bloqué », « Parade parfaite ! »).

## 5. En cas de problème

- **Objets roses** : le rendu URP n'est pas configuré. Menu **Éclats du Seuil → Configurer le rendu URP**, puis relancer Play.
- **Cadre rouge « Impossible de charger le jeu »** : le dossier `unity` a été déplacé hors du dépôt. Il doit rester à côté de `data/` et `scenarios/`.
- **Erreurs rouges dans la Console** : faire une capture de la Console et l'envoyer à Claude.
- **Fichiers modifiés dans GitHub Desktop** après une ouverture (`ProjectVersion.txt` notamment) : c'est Unity qui les réécrit ; ne rien valider (*Commit*). Si un *Pull* refuse à cause d'eux : clic droit sur le fichier → *Discard changes*, puis *Pull*.
- Pour remettre le projet à zéro : fermer Unity et supprimer le dossier `unity/Library` (il est recréé à l'ouverture).

## Ce qui est provisoire

Graphismes en formes simples, interface IMGUI, déplacements sans effet sur le combat (le cœur n'a pas encore de positions), une seule touche par compétence. Détail : [LIMITES_LOT_9.md](LIMITES_LOT_9.md#sous-lot-93--projet-unity).
