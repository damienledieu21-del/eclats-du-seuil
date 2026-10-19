# Prototype Unity — guide pas à pas (Windows)

Le prototype (lot 9) se trouve dans le dossier `unity/` du dépôt. Unity compile lui-même le cœur du jeu depuis `src/` : rien d'autre à installer que Unity et GitHub Desktop, et aucune scène à préparer.

## 1. Installer (une seule fois)

1. **Unity Hub** : [unity.com/download](https://unity.com/download), version Windows (x64 sur un processeur Intel ou AMD).
2. Dans le Hub : se connecter (compte Unity gratuit), puis **Preferences → Licenses → Add → Get a free personal license**.
3. **Installs → Install Editor → Unity 6.3 (LTS)**, version `6000.3.26f1` (celle du projet) ou une autre `6000.3.x`. Modules : *Microsoft Visual Studio Community* et *Windows Build Support (IL2CPP)*. Prévoir 15 Go ; sur un SSD si possible.
4. **GitHub Desktop** : [desktop.github.com](https://desktop.github.com), se connecter avec son compte GitHub, puis **File → Clone repository → eclats-du-seuil**.

## 2. Récupérer la bonne version

Le prototype est sur `main` depuis la fin du lot 9 (19 octobre 2026) :

1. Dans GitHub Desktop, en haut : **Current branch → main**.
2. **Fetch origin** puis **Pull origin** pour prendre les dernières modifications.

Pour essayer une PR avant sa fusion, choisir sa branche de la même façon (onglet *Other branches*), puis revenir sur `main` une fois la PR fusionnée.

## 3. Ouvrir le projet

1. Unity Hub → **Projects → Add → Add project from disk**.
2. Choisir le dossier **`unity`** à l'intérieur du dépôt cloné (par exemple `Documents\GitHub\eclats-du-seuil\unity`), pas le dépôt lui-même.
3. Si le Hub signale une version manquante (une autre `6000.3.x` est installée), **ne pas l'installer** : cocher la version 6.3 installée, puis ouvrir et accepter le changement de version.
4. La première ouverture prend plusieurs minutes : Unity importe le moteur de rendu et compile le cœur du jeu.
5. À la fin, la console (onglet **Console**) doit afficher « Éclats du Seuil : rendu URP configuré ». Le projet n'a pas de scène : la scène vide proposée par Unity suffit.

## 4. Jouer

Appuyer sur **Play** (▶ en haut au centre). L'arène se construit, avec le Bastion (capsule bleue) face à la **meute à vaincre** (sous-lot 11.1) : deux loups et un hurleur qui ont des PV. Garde et parade sont nécessaires ; quand les trois sont morts, c'est la victoire (« Victoire : tous les ennemis sont vaincus ! ») ; si le Bastion tombe, c'est la défaite. Retour arrière pour recommencer, N pour les autres séances.

Les touches sont lues **par leur position** sur le clavier (Unity 6) : le tableau donne les lettres d'un clavier **AZERTY**. En QWERTY, Z Q S D deviennent W A S D, A / E deviennent Q / E, et M reste M.

| Touche | Action |
|---|---|
| Z Q S D ou flèches | se déplacer (déplacement visuel, voir les limites) |
| clic droit (ou Maj) maintenu | garde : un coup est bloqué (endurance) |
| Espace | parade parfaite, juste avant l'impact |
| 1, 2, 3, 4 | compétences des emplacements |
| R | ultime (100 de Résonance) |
| Tab | ennemi suivant comme cible |
| C | caméra isométrique ↔ troisième personne (bases des vues 1 et 2 retenues par le [cadrage](CADRAGE.md#2-direction-générale-retenu)) |
| A / E | tourner la caméra isométrique |
| molette | zoom ; bouton du milieu maintenu : tourner la caméra en troisième personne |
| T | écran des talents (combat en pause) ; T ou Échap pour le fermer |
| N | scénario suivant (meute à vaincre, entraînement, S1 à S5) |
| F1 à F5 | build : Rempart, Contrecoup, Gardien, hybride, niveau 10 |
| Retour arrière | recommencer la séance |
| Échap | pause |
| H | masquer ou afficher l'aide |
| M | jeu en réseau à deux (voir la section 6) |

À l'écran : barres de PV, d'endurance, de Résonance et de Charges en bas ; les compétences avec leur recharge ; au-dessus de chaque ennemi, sa barre de PV (« PV infinis » pour un ennemi d'entraînement) et une **barre de télégraphie** qui se remplit avant son coup ; un ennemi mort s'assombrit, n'attaque plus, et la cible passe au suivant (rouge vif : coup **imparable**, à encaisser sans garde ni parade) ; la fiche de la cible en haut à droite ; les textes de combat (dégâts, « Bloqué », « Parade parfaite ! »).

## 5. Talents

Touche **T** : le combat se met en pause et l'écran des talents s'ouvre, avec les trois arbres du Bastion (Rempart, Contrecoup, Gardien).

- **+** et **−** ajoutent ou retirent un point. Un bouton grisé signale un point refusé ; un clic dessus affiche la raison (plus de point, prérequis, seuil de 18 points du talent majeur ★, un seul majeur par build, talent dont un autre dépend).
- **Partir de** : charge un build de référence (Rempart, Contrecoup, Gardien, hybride, niveau 10) ; **Tout retirer** repart de zéro.
- **Jouer ce build** relance la séance avec le build composé (« Personnalisé ») ; il reste actif en changeant de scénario (N). F1 à F5 reviennent à un build de référence.

Le niveau et les compétences équipées sont ceux du build de départ ; le build composé n'est pas encore sauvegardé (il disparaît à l'arrêt du jeu).

## 6. Jouer à deux en réseau (lot 10)

Touche **M** : le menu du réseau s'ouvre. On y tape son nom, puis on choisit une façon de jouer. La partie est une **coopération** : deux Bastions contre deux loups et un hurleur. Depuis le sous-lot 11.1, la meute peut mourir (scénario `coop/meute_a_vaincre`, 90 s) : quand elle est vaincue, c'est la victoire des deux joueurs. Le serveur mène le combat ; si un joueur perd la connexion, le combat continue, son personnage baisse la garde, et le jeu revient tout seul dans la partie s'il retrouve le réseau dans les 30 s.

**a. Essayer seul, avec un équipier robot** (le plus simple pour un premier essai) :

1. Play, puis **M** → **Héberger avec un équipier robot**.
2. La salle d'attente montre les deux places ; le robot est déjà prêt. Clic sur **Je suis prêt** : le combat commence.
3. Le robot joue simplement (garde quand un coup arrive, parade, compétences, Barrière d'acier sur toi). **G** choisit l'allié visé par tes soutiens (cercle vert).

**b. À deux PC sur le même réseau (box, Wi-Fi)** :

1. PC qui héberge : Play → **M** → **Héberger la partie**. Windows peut demander d'autoriser Unity sur le réseau : accepter pour les **réseaux privés**. La salle d'attente affiche l'adresse à donner (par exemple `192.168.1.20`).
2. Autre PC (même projet ouvert dans Unity, même branche) : Play → **M** → taper cette adresse → **Rejoindre**.
3. Chacun clique sur **Je suis prêt**.

**c. Sur un seul PC, deux fenêtres** : Unity n'ouvre pas deux fois le même projet ; on construit donc le jeu une fois.

1. **File → Build Profiles → Windows → Build**, et choisir le dossier **`unity/Build`** (il est ignoré par GitHub Desktop). Le jeu construit retrouve tout seul les données du dépôt.
2. Dans l'éditeur : Play → **M** → **Héberger la partie**.
3. Lancer le jeu construit (`unity/Build/…exe`) → **M** → adresse `127.0.0.1` → **Rejoindre**.

**d. Avec le serveur dédié** (outil .NET, pour les curieux) : `dotnet run --project src/EdS.Server -- --local --robot` dans un terminal ouvert dans le dépôt, puis dans Unity **M** → adresse `127.0.0.1` → **Rejoindre**.

Dans la salle d'attente : **F1 à F5** changent de build de référence, **T** ouvre les talents (« Jouer ce build » envoie le build composé au serveur, qui le vérifie). En combat, le temps aller-retour (ping) s'affiche sous le chrono, avec l'avance de la télégraphie : en réseau, la barre orange au-dessus d'un assaillant se remplit en avance du ping (sous-lot 10.6), il suffit donc de parer « à vue », comme en solo ; en réseau local, l'avance ne dépasse pas quelques millisecondes. Pas de pause en réseau. **Quitter la partie** ramène au menu ; **Revenir au jeu solo** reprend l'entraînement.

## 7. En cas de problème

- **Objets roses** : le rendu URP n'est pas configuré. Menu **Éclats du Seuil → Configurer le rendu URP**, puis relancer Play.
- **Cadre rouge « Impossible de charger le jeu »** : le dossier `unity` a été déplacé hors du dépôt. Il doit rester à côté de `data/` et `scenarios/`.
- **Erreurs rouges dans la Console** : faire une capture de la Console et l'envoyer à Claude.
- **Fichiers modifiés dans GitHub Desktop** après une ouverture (`ProjectVersion.txt` notamment) : c'est Unity qui les réécrit ; ne rien valider (*Commit*). Si un *Pull* refuse à cause d'eux : clic droit sur le fichier → *Discard changes*, puis *Pull*.
- Pour remettre le projet à zéro : fermer Unity et supprimer le dossier `unity/Library` (il est recréé à l'ouverture).
- **Réseau : « connexion impossible »** : vérifier l'adresse, que l'autre PC héberge bien (salle d'attente affichée) et que le pare-feu Windows autorise Unity (ou le jeu construit) sur les réseaux privés, port 7777.
- **Réseau : « désynchronisation persistante »** : ne devrait jamais arriver ; faire une capture et l'envoyer à Claude (c'est exactement ce que le lot 10 doit détecter).

## Ce qui est provisoire

Graphismes en formes simples, interface IMGUI, déplacements sans effet sur le combat (le cœur n'a pas encore de positions), une seule touche par compétence. Détail : [LIMITES_LOT_9.md](LIMITES_LOT_9.md#sous-lot-93--projet-unity).
