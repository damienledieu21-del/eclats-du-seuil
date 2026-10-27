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

Appuyer sur **Play** (▶ en haut au centre). Depuis le sous-lot 12.8, le jeu s'ouvre sur **l'aventure dans les Vallons d'Aube** (section 4 bis) ; **N** passe aux séances d'entraînement, puis revient à l'aventure. La première séance est la **scène de démonstration des Vallons** (sous-lot 11.6) : elle se construit : un coin de campagne en formes simples, avec un moulin, une rivière et son pont, des arbres, un abri sous un toit et un passage étroit entre deux murs. Le Bastion (capsule bleue) part du sud-ouest ; une meute attend : trois loups derrière le moulin, un sanglier au-delà de la rivière, un archer derrière le passage étroit.

- **Le déplacement est simulé par le moteur** : le décor arrête le Bastion, il ne traverse pas les ennemis, un étourdissement l'immobilise ; les portées et la ligne de vue des compétences comptent (une compétence hors de portée ou sans vue est refusée, avec la raison à l'écran).
- **Un ennemi caché par le décor n'apparaît pas** tant que le Bastion ne le voit pas (même règle que la ligne de vue des compétences) : au départ, les loups sont cachés derrière le moulin ; le sanglier et l'archer se voient par-dessus la rivière, qui est basse (sous-lot 11.9). Il ne se cible pas non plus : Tab ne propose que des ennemis en vue. Une cible qui passe derrière le décor reste la cible **une seconde** (sous-lot 11.9 : un loup qui passe derrière un tronc ne fait plus changer de cible), puis elle est remplacée par le premier ennemi en vue, ou lâchée s'il n'y en a aucun (la garde ne se tourne alors vers personne). Une cible morte est remplacée aussitôt.
- Les ennemis attendent ; ils s'engagent quand on s'approche (ou qu'on les frappe), puis approchent, encerclent, gardent leurs distances (l'archer) ou chargent (le sanglier).
- **Les attaques annoncées sont dessinées au sol** : contour orange de la zone, remplissage rouge qui grandit jusqu'à l'impact. Sortir de la zone avant l'impact l'esquive ; un coup dirigé annoncé est un cercle sous sa cible.
- **Caméra** : la molette la règle en douceur de la **vue 1** (haute, de trois quarts) à la **vue 2** (derrière le personnage) ; **C** rappelle l'autre réglage (au départ : vue 1 ↔ vue 2) ; le réglage est gardé d'une partie à l'autre ; rien ne change de vue tout seul. En vue 2, reculer (S, ou le stick vers le bas) fait marcher le Bastion **à reculons** en regardant devant lui (sous-lot 11.9) ; en vue 1, il se tourne vers sa marche. Un mur, un arbre ou le toit qui cache le Bastion s'abaisse ou s'efface le temps qu'il passe.

Garde et parade sont nécessaires ; quand tous les ennemis sont morts, c'est la victoire ; si le Bastion tombe, c'est la défaite. Retour arrière pour recommencer, N pour les autres séances (meute à vaincre, entraînement, S1 à S5 : sans décor, le déplacement y reste visuel).

Les touches sont lues **par leur position** sur le clavier (Unity 6) : le tableau donne les lettres d'un clavier **AZERTY**. En QWERTY, Z Q S D deviennent W A S D, A / E deviennent Q / E, et M reste M.

| Touche | Action |
|---|---|
| Z Q S D ou flèches | se déplacer (simulé par le moteur dans les Vallons, visuel dans les séances sans décor) |
| clic droit (ou Maj) maintenu | garde : un coup est bloqué (endurance) |
| Espace | parade parfaite, juste avant l'impact |
| 1, 2, 3, 4 | compétences des emplacements |
| R | ultime (100 de Résonance) |
| Tab | ennemi suivant comme cible |
| C | rappel rapide de l'autre réglage de caméra (vue 1 ↔ vue 2, retenues par le [cadrage](CADRAGE.md#2-direction-générale-retenu)) |
| A / E | tourner la caméra d'un huitième de tour |
| molette | régler la caméra entre la vue 1 et la vue 2 (réglage gardé) ; bouton du milieu maintenu : tourner librement |
| T | écran des talents (combat en pause) ; T ou Échap pour le fermer |
| N | scénario suivant (Vallons, meute à vaincre, entraînement, S1 à S5), puis retour à l'aventure |
| F1 à F5 | build : Rempart, Contrecoup, Gardien, hybride, niveau 10 |
| Retour arrière | recommencer la séance |
| Échap | pause |
| H | masquer ou afficher l'aide |
| F8 | afficher ou cacher les commandes au doigt (essai au PC ; voir la section 7) |
| M | jeu en réseau à deux (voir la section 6) |

À l'écran : barres de PV, d'endurance, de Résonance et de Charges en bas ; les compétences avec leur recharge ; au-dessus de chaque ennemi, sa barre de PV (« PV infinis » pour un ennemi d'entraînement) et une **barre de télégraphie** qui se remplit avant son coup ; un ennemi mort s'assombrit, n'attaque plus, et la cible passe au suivant (rouge vif : coup **imparable**, à encaisser sans garde ni parade) ; la fiche de la cible en haut à droite ; les textes de combat (dégâts, « Bloqué », « Parade parfaite ! »).

### Ce que tu vérifies dans les Vallons (sous-lot 11.6)

Les quatre vérifications de H14, à noter pour le rapport du 11.9 :

1. **Même combat dans les deux vues** : jouer en vue 1, puis en vue 2 ; la caméra ne change ni les portées, ni les collisions, ni les règles.
2. **Obstacles et passages** : passer le pont et le passage étroit, longer le moulin, entrer sous le toit ; se faire encercler par les loups près d'un arbre ou d'un mur (règle de contact A en place : le Bastion ne doit jamais rester coincé).
3. **Commande et stabilité** : le personnage répond tout de suite, s'arrête net, ne tremble pas contre un mur ; la caméra ne saute pas.
4. **Lisibilité et performances** : les annonces au sol se lisent dans les deux vues, un ennemi caché le reste vraiment, l'image reste fluide.

Note aussi ce qui gêne (vitesse, caméra, taille des zones) : ce sont des réglages.

## 4 bis. L'aventure dans les Vallons d'Aube (sous-lot 12.8)

Toute la zone en formes simples : le village, le moulin, la rivière, le pré des loups, les ruines, la carrière et la petite zone du Seuil ; les créatures réapparaissent, le Gardien du Seuil attend au cœur des pierres dressées. Les **PNJ** sont des capsules dorées (Odile au moulin, Maëlle à la forge du village, Gaspard au camp de la carrière), les **herbes** des boules vertes, les **filons** des cubes gris (cachés le temps de repousser), les **points sûrs** des disques bleus. Les noms s'affichent quand on s'approche ; une indication en bas dit ce qui est possible ici (« F : parler à Odile », « F : récolter… », « Entrée : faire halte »).

La partie est **sauvegardée toute seule** (quelques secondes après chaque changement, à chaque halte, en quittant le jeu ou en le passant en arrière-plan sur téléphone) ; on la retrouve au lancement suivant. Pour repartir de zéro, supprimer le dossier `sauvegardes` du dossier de l'application (Unity : `Application.persistentDataPath`).

| Touche | Action |
|---|---|
| F | parler au PNJ à portée (3 m), sinon récolter le nœud à portée |
| 1 à 9 (ou clic) | choisir une réponse du dialogue (parmi celles affichées) ; Échap ou le bouton Fermer (au doigt) le ferme |
| I | inventaire, équipement porté et métiers (porter, retirer) |
| J | journal de quête et découvertes |
| K | atelier : recettes, ce qu'il manque, fabriquer (à la forge de Maëlle, à l'ouest du village, juste au nord de la place : enclume grise, « K : forger » s'affiche quand on y est) |
| P | boire un objet de soin emporté (le plus petit d'abord) |
| L | carte en grand (noms des lieux, PNJ, points sûrs, objectif) ; L, Échap ou Fermer pour revenir. En petit, elle reste en haut à droite (12.9d) |
| Entrée | faire halte près d'un point sûr (aucun ennemi proche) : sauvegarde, PV pleins ; après une défaite : reprendre au dernier point sûr |
| F6 | sauvegarder maintenant |
| N | quitter l'aventure (sauvegardée) pour l'entraînement |

Les touches de combat sont celles de l'entraînement (garde, parade, compétences, Tab, caméra). La cible se choisit parmi les ennemis en vue à 20 m au plus, du plus proche au plus loin. Le monde **s'arrête** pendant un dialogue, un écran ouvert ou la pause (jeu en solo). Au doigt, un écran ou un dialogue ouvert masque les boutons tactiles du jeu : seuls ses propres boutons (dont Fermer) répondent.

**Niveau et équipement** gagnés ou changés en route prennent effet à la **halte** suivante (le combat en cours garde le build d'entrée). **Compétences gagnées** (12.9b) : Barrière d'acier au niveau 5 (touche 3, sur toi), Onde de rupture (touche 4) et l'ultime Bastion absolu (R) au niveau 10 ; le message du niveau annonce la compétence, la halte l'équipe et dit sa touche. La **porte du moulin**, une fois ferrée par Maëlle, se franchit aussi après une halte. Une **défaite** ne fait rien perdre : Entrée reprend au dernier point sûr approché, PV pleins.

Au doigt : **Agir** (F) et **Potion** au-dessus de la garde, **Sac**, **Journal** et **Atelier** en haut, **Halte** à la place de « Rejouer ». Au doigt, seul le bouton **Garde** lève la garde (12.9c : marcher en touchant Agir ne la lève plus).

**Carte** (12.9d, demande de Damien du 25 octobre 2026) : la petite carte des Vallons, nord en haut, est en haut à droite ; la fiche de la cible passe à sa gauche et, au doigt, la rangée Menu, Aide… descend sous elle. Elle montre le décor (rivière en bleu), toi (point jaune), l'**objectif de l'étape en cours** (étoile orange : le lieu à atteindre, le PNJ à qui parler, la créature à vaincre), les PNJ (blanc), les points sûrs (vert), la forge (gris), les filons et herbes (petits points) et les ennemis **en vue** seulement (rouge, violet pour le chef). Toucher la carte, ou L, l'ouvre en grand avec les noms ; le monde s'arrête pendant ce temps, comme pour les autres écrans.

Ce que tu vérifies (essai du 12.9) : la mission d'Odile de bout en bout (traces, ferrure forgée, pré des loups, manifestation, rôdeur, Gardien, retour) ; la récolte et la fabrication ; l'inventaire et l'équipement ; une défaite et la reprise ; quitter puis relancer le jeu (la partie revient) ; la lisibilité des écrans sur PC et sur le Note8.

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

**e. Les Vallons à deux, arrivée en plein combat (sous-lot 11.7)** : le menu propose aussi **Héberger les Vallons à deux** et **Héberger les Vallons, robot qui arrive après 10 s de combat**. Dans les Vallons, on marche (ZQSD) et le serveur résout tout : déplacements, cible, attaques annoncées. Le combat commence dès que les joueurs présents sont prêts ; l'autre joueur (ou le robot, 10 s après le début) entre **pendant** l'affrontement, à sa place ou juste à côté si elle est prise. Ce que tu vérifies :

1. **Seul avec le robot** : Play → **M** → **Héberger les Vallons, robot qui arrive après 10 s de combat** → **Je suis prêt**. Le bandeau sous le chrono compte les secondes avant le robot ; il apparaît en vert près de la place de départ, et le combat continue sans saccade.
2. **Ta marche** : ton personnage part dès que tu appuies et s'arrête dès que tu lâches, même avec du ping (sa place est prédite, puis corrigée en douceur si le serveur en décide autrement). Il ne traverse ni le décor ni les ennemis.
3. **À deux PC** (comme en **b**, mais avec **Héberger les Vallons à deux**) : l'un commence seul, l'autre rejoint quand il veut ; chacun voit l'autre marcher, les mêmes ennemis aux mêmes places, et un loup tué meurt chez les deux en même temps.
4. **Coupure** : couper le Wi-Fi d'un PC quelques secondes : au retour, la partie reprend à l'état courant (pas de longue attente).

Dans la salle d'attente : **F1 à F5** changent de build de référence, **T** ouvre les talents (« Jouer ce build » envoie le build composé au serveur, qui le vérifie). En combat, le temps aller-retour (ping) s'affiche sous le chrono, avec l'avance de la télégraphie : en réseau, la barre orange au-dessus d'un assaillant se remplit en avance du ping (sous-lot 10.6), il suffit donc de parer « à vue », comme en solo ; en réseau local, l'avance ne dépasse pas quelques millisecondes. Pas de pause en réseau. **Quitter la partie** ramène au menu ; **Revenir au jeu solo** reprend l'entraînement ; ouvert depuis l'aventure (M), il la laisse continuer telle quelle.

## 7. Jouer sur téléphone (sous-lot 11.8)

Premier essai tactile : la scène des Vallons au doigt, d'abord dans le **simulateur d'appareils** de l'éditeur, puis sur le **Galaxy Note8** (Android). L'iPhone 13 demande un Mac avec Xcode : pas pour cet essai.

**Les commandes au doigt** (téléphone tenu à l'horizontale) :

| Où | Geste | Action |
|---|---|---|
| moitié gauche | poser le pouce et le pousser | marcher (le stick apparaît là où le pouce se pose ; marche relative à la caméra) |
| en bas à droite | maintenir **Garde** | garde |
| en bas à droite | toucher **Parade** | parade parfaite |
| au-dessus | **1**, **2**, **3**, **4**, **Ultime** | compétences |
| au-dessus | **Cible**, **Vue** (et **Allié** en réseau) | ennemi suivant, rappel vue 1 ↔ vue 2, allié visé |
| moitié droite, hors des boutons | glisser un doigt | tourner la vue |
| moitié droite | écarter ou rapprocher deux doigts | régler la vue entre la 1 (haute) et la 2 (proche) |
| à droite, sous la fiche de la cible | **Menu**, **Aide** ; seul : **Pause**, **Rejouer**, **Scène** | menu réseau, aide, pause, recommencer, scène suivante |

**a. Dans l'éditeur, sans téléphone** : **Window → General → Device Simulator**. Dans l'onglet *Simulator* qui s'ouvre, choisir un téléphone (un Samsung Galaxy si la liste en propose un, sinon un téléphone Android récent), le tourner à l'horizontale (bouton de rotation), puis **Play** : la souris fait le doigt (un seul doigt : le pincement ne s'essaie que sur le vrai téléphone). Vérifier que les boutons ne cachent ni les barres ni la fiche de la cible. Au PC sans simulateur, **F8** affiche aussi les commandes (la souris fait un doigt).

**b. Installer sur le Note8** (une seule fois pour les étapes 1 à 3) :

1. **Unity Hub → Installs** → roue dentée de Unity 6.3 → **Add modules** → cocher **Android Build Support** avec **OpenJDK** et **Android SDK & NDK Tools** (environ 5 Go).
2. **Sur le Note8** : *Paramètres → À propos du téléphone → Informations sur le logiciel* → toucher **7 fois** *Numéro de version* (« Mode développeur activé ») ; puis *Paramètres → Options de développement* → activer **Débogage USB**. Brancher le téléphone au PC en USB et accepter « Autoriser le débogage USB » sur le téléphone.
3. **Dans Unity** : **File → Build Profiles → Android → Switch Platform** (long la première fois). Puis **Player Settings** (dans la même fenêtre) → *Other Settings* : **Scripting Backend : IL2CPP**, **Target Architectures : ARM64** coché, **Minimum API Level** : d'abord relever la version d'Android du Note8 (*Paramètres → À propos du téléphone → Informations sur le logiciel → Version d'Android* ; elle n'est pas connue d'avance, L11-D11), puis choisir une API minimale qui ne la dépasse pas : la valeur proposée par Unity si elle est au plus celle du téléphone, sinon celle du téléphone (Android 8.0 : *API level 26* ; 8.1 : *27* ; 9 : *28*). Une API minimale au-dessus de la version du téléphone donne un jeu impossible à installer. Note la version relevée dans la fiche d'essai. Si l'écran reste noir au premier essai : décocher *Auto Graphics API* et mettre **OpenGLES3** en premier.
4. **Build Profiles → Android** : *Run Device* → choisir le **Note8** (bouton *Refresh* s'il n'apparaît pas) → **Build And Run** → dossier **`unity/Build/Android`** (ignoré par GitHub Desktop), nom `eclats.apk`. Unity construit le jeu (plusieurs minutes la première fois), l'installe et le lance. Depuis le sous-lot 11.9, Unity ne demande plus d'enregistrer de scène : à l'ouverture du projet, la scène de démarrage **`Assets/EdS/Scenes/Jeu.unity`** est créée et inscrite en tête de la liste de construction (une construction a besoin d'une scène, même vide ; le jeu se construit lui-même au lancement ; ce fichier est ignoré par GitHub Desktop ; menu **Éclats du Seuil → Créer la scène de démarrage** pour la refaire). L'ancienne scène `Assets/Essai.unity`, si tu l'as gardée, peut rester ou être supprimée.
5. Au premier lancement : « Préparation des données du jeu… » (les données et scénarios du dépôt, emportés dans le jeu, sont recopiés sur le téléphone), puis les Vallons. Après une nouvelle construction dont les données ont changé, la copie est refaite à neuf au lancement suivant.

**c. Ce que tu vérifies sur le téléphone** (rapport au 11.9) :

1. **Installation et démarrage** : le jeu s'installe, démarre, garde l'écran allumé, tourne avec le téléphone (deux sens à l'horizontale).
2. **Marche au pouce** : départ, arrêt, changement de direction ; la marche suit bien la caméra.
3. **Boutons** : assez grands, bien placés pour les pouces ; garde maintenue et parade au bon moment.
4. **Geste entre les deux vues** : pincer pour passer de la vue 1 à la vue 2, glisser pour tourner ; rien ne change tout seul.
5. **Lisibilité et fluidité** : annonces au sol, barres, textes lisibles ; image fluide (ombres et anticrénelage coupés sur téléphone).
6. **En réseau** (si tu veux) : PC qui héberge les Vallons (section 6), téléphone sur le même Wi-Fi → **Menu** → adresse du PC → **Rejoindre**.

## 7 bis. Essai de style (packs gratuits)

Décision de Damien du 25 octobre 2026 : un petit essai de style après le 12.9, avant le lot 13, pour voir les Vallons avec de vrais modèles, tout en gratuit (licence CC0 : utilisables même dans un jeu vendu). Sans pack, le jeu garde ses formes simples : rien n'est obligatoire.

1. **Télécharger** : [Lowpoly Animated Knight](https://quaternius.itch.io/lowpoly-animated-knight) (le héros), [Animated Animals Low Poly](https://opengameart.org/content/animated-animales-low-poly) (le loup), [Lowpoly Animated Farm Animal Pack](https://opengameart.org/content/lowpoly-animated-farm-animal-pack) (le cochon, en attendant un sanglier), [Stylized Nature MegaKit](https://quaternius.itch.io/stylized-nature-megakit) (arbres, rochers, plantes ; version gratuite), et les sons [Impact Sounds](https://kenney.nl/assets/impact-sounds) et [RPG Audio](https://kenney.nl/assets/rpg-audio) de Kenney.
2. **Déposer** : dézipper chaque pack dans `unity/Assets/Style/` (un sous-dossier par pack, noms libres). Garder les dossiers **FBX** (et **Textures** s'il y en a) et les sons (`.ogg`) ; supprimer les dossiers **Blend** (Unity demanderait Blender), OBJ et glTF ne servent pas. Ce dossier n'est pas suivi par git : il ne part pas sur GitHub.
3. **Préparer** : dans Unity, menu **Éclats du Seuil → Préparer l'essai de style**. Il trouve les modèles et les sons par leur nom, règle l'import des modèles animés et écrit le catalogue `Assets/Style/Resources/StyleCatalog` ; une fenêtre dit ce qu'il a trouvé (héros, loup, sanglier, nombre d'arbres, de rochers, de plantes et de sons).
4. **Jouer** : Play. Le Bastion est un chevalier, les loups et les sangliers ont leur modèle, les arbres, les blocs de la carrière et les pierres du Seuil deviennent des arbres et des rochers, des plantes poussent sur le sol ; coups, blocages, parades, morts et potions ont un son. L'archer, le rôdeur, le Gardien et les PNJ gardent leur forme simple pour cet essai.
5. **Un modèle de travers** : dans `Assets/Style/Resources/StyleCatalog` (inspecteur), *Hero Yaw*, *Wolf Yaw* et *Boar Yaw* tournent le héros, le loup ou le sanglier (90, 180 ou −90) ; on peut aussi y glisser un autre modèle à la main.
6. **Sur le téléphone** : Build And Run comme d'habitude (section 7) ; les packs partent dans le jeu construit.

Limites des packs gratuits : le loup n'a que l'attente et la marche (il marche aussi quand il court), le cochon que l'attente (il glisse) ; sans animation de mort, ils se couchent sur le flanc. Le chevalier n'a pas d'épée en main ni de bouclier (le disque bleu de la garde reste). Les feuillages peuvent paraître pleins (matériaux importés sans transparence) : à régler si le style est gardé.

Ce que tu regardes : si le style low poly te plaît sur PC et sur le Note8, si on voit mieux qui est qui, si les animations suivent (attente, marche, course, attaque quand tu lances une compétence, mort), si les sons aident, et si l'image reste fluide sur le téléphone. Les modèles ne changent rien au combat : les emprises et les obstacles du moteur restent les mêmes ; les plantes n'arrêtent rien.

### Essai réaliste (26 octobre 2026)

Avis de Damien sur le premier essai : « bof j'aime pas trop je voulais quelque chose de vraiment réaliste ». Direction choisie : « un mélange de réaliste et de stylisé coloré », « lumineux, réaliste mais pas trop », « le plus réaliste possible ». Le même outil sert, avec des fichiers réalistes rangés par Claude dans `unity/Assets/Style` (toujours non suivi par git) :

- `Mixamo/` : le paladin avec épée et bouclier (*Paladin W/Prop J Nordstrom*, avec sa peau) et cinq animations sans peau, « FBX for Unity », 30 images par seconde : attente, marche et course sur place, coup d'épée (*Downward Slash*), mort (*Falling Back Death*). Mixamo est gratuit avec un compte Adobe ; ses personnages et animations s'emploient dans un jeu.
- `PolyHaven/` (licence CC0, textures en 1k) : le ciel (`Sky/`, *kloofendal 48d partly cloudy puresky*), les sols (`Ground/` : herbe, terre, roche, avec leur relief), des plantes (herbes, fougère, mousse, arbustes, ortie, ficaire, pissenlit), des rochers (dont des lots de plusieurs rochers) et du bois mort (souches, tronc couché).
- Les arbres restent ceux du MegaKit : Poly Haven n'en a pas d'assez légers pour un jeu (70 à 700 Mo par arbre).

1. **Préparer** : menu **Éclats du Seuil → Préparer l'essai de style** (relancer après chaque ajout de fichiers). En plus du catalogue, il règle les cartes de relief, crée les matériaux des modèles Poly Haven (avec la transparence des feuilles et de l'herbe), renomme les animations Mixamo d'après leur fichier, crée le ciel et le matériau du terrain dans `Assets/Style/Generated`, et active les effets d'image d'URP s'ils étaient coupés. La fenêtre de fin dit ce qu'il a trouvé (héros et nombre d'animations à part, sols, ciel, bois mort).
2. **Jouer** : Play. Le sol devient un terrain peint (herbe, terre sous les arbres et par taches, roche autour des blocs, des pierres et des murs) qui déborde de l'aire, une forêt (et du bois mort) pousse autour, le ciel, le soleil, l'ambiance, la brume et les couleurs de l'image changent, le Bastion est le paladin, animé.
3. **Régler pendant le jeu** : dans `Assets/Style/Resources/StyleCatalog` (inspecteur), partie *Lumière et couleurs* : couleur, force, hauteur et direction du soleil, couleurs d'ambiance, brume (début, fin, couleur), luminosité, contraste, couleurs plus vives, halo, bords assombris. Les changements se voient tout de suite dans l'éditeur (Play en cours) et sont gardés. Pour revenir aux réglages de l'essai : clic droit sur le titre du catalogue dans l'inspecteur → *Remettre la lumière et les couleurs par défaut* (le menu de préparation le fait aussi, une fois, quand Claude change ces réglages). Les textures de sol se changent aussi à la main (glisser une autre texture du dossier `Ground`). Le nombre d'arbres autour, de plantes, la largeur de la forêt et la taille des textures du sol comptent au prochain combat.

Premier essai (26 octobre 2026, vidéo de Damien) et corrections : le paladin était tout blanc (textures restées dans le fichier Mixamo : elles sont maintenant sorties dans `Mixamo/Textures` et posées sur son matériau) ; image trop claire et jaune (soleil, ambiance et exposition baissés, contraste relevé : réglages version 2) ; rangées de pissenlits et arbustes alignés (Poly Haven livre chaque modèle en lot de variantes avec leurs niveaux de détail : une seule variante est gardée, d'après les noms des pièces) ; arbres du MegaKit coupés à tort (la règle des lots ne vise plus que les fichiers Poly Haven).

Limites : les arbres restent dessinés (MegaKit) ; les loups et les sangliers restent ceux des packs low poly (choix de Damien : les animaux plus tard) ; sur le téléphone, la forêt est réduite au tiers, les plantes à la moitié et le halo coupé, mais l'essai réaliste n'est pas encore réglé pour le Note8 (à faire si le style est gardé) ; le terrain est plat comme le sol du moteur.

## 8. En cas de problème

- **Objets roses** : le rendu URP n'est pas configuré. Menu **Éclats du Seuil → Configurer le rendu URP**, puis relancer Play.
- **Cadre rouge « Impossible de charger le jeu »** : le dossier `unity` a été déplacé hors du dépôt. Il doit rester à côté de `data/` et `scenarios/`.
- **Erreurs rouges dans la Console** : faire une capture de la Console et l'envoyer à Claude.
- **Fichiers modifiés dans GitHub Desktop** après une ouverture (`ProjectVersion.txt` notamment) : c'est Unity qui les réécrit ; ne rien valider (*Commit*). Si un *Pull* refuse à cause d'eux : clic droit sur le fichier → *Discard changes*, puis *Pull*.
- Pour remettre le projet à zéro : fermer Unity et supprimer le dossier `unity/Library` (il est recréé à l'ouverture).
- **Réseau : « connexion impossible »** : vérifier l'adresse, que l'autre PC héberge bien (salle d'attente affichée) et que le pare-feu Windows autorise Unity (ou le jeu construit) sur les réseaux privés, port 7777.
- **Réseau : « désynchronisation persistante »** : ne devrait jamais arriver ; faire une capture et l'envoyer à Claude (c'est exactement ce que le lot 10 doit détecter).
- **Réseau : « protocole 1 non pris en charge »** (ou 2, ou 3) : les deux PC n'ont pas la même version du projet ; faire un *Pull* sur les deux (sous-lot 11.7 : protocole 2 ; sous-lot 11.9 : protocole 3).
- **Téléphone non proposé dans *Run Device*** : câble de données (pas seulement de charge), débogage USB activé, autorisation acceptée sur le téléphone ; débrancher, rebrancher, *Refresh*.
- **Téléphone : écran bloqué sur « Préparation des données du jeu… » avec un sol rose** (premier essai du 22 octobre 2026) : le jeu construit n'emportait pas son shader de couleur. Corrigé : chaque construction emporte un matériau modèle (`EdsBuildShaders`) ; faire un *Pull*, puis **Build And Run** à nouveau. Si un cadre rouge « Démarrage du jeu impossible » s'affiche, envoyer une photo à Claude.
- **Téléphone : « Impossible de préparer les données du jeu »** : le jeu a été construit sans ses données (projet Unity déplacé hors du dépôt) ; reconstruire depuis le projet du dépôt.

## Ce qui est provisoire

Graphismes en formes simples, interface IMGUI, une seule touche par compétence. Le déplacement n'est simulé que dans la scène des Vallons (les autres séances et la coopération en réseau n'ont pas de décor ; le réseau dans l'espace vient au 11.7). Détail : [LIMITES_LOT_9.md](LIMITES_LOT_9.md#sous-lot-93--projet-unity) et [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-116--scène-de-démonstration-des-vallons).
