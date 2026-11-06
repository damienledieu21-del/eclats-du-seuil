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
- **Caméra** (façon World of Warcraft depuis l'essai réaliste 9) : elle tourne autour du personnage ; la souris l'oriente et l'incline, la molette la rapproche (jusqu'à l'épaule) ou l'éloigne (24 m) ; elle ne tourne jamais toute seule ; **C** la remet derrière le personnage ; distance, inclinaison et réglages sont gardés d'une partie à l'autre. Caméra basse (derrière le personnage), reculer (S, ou le stick vers le bas) fait marcher le Bastion **à reculons** en regardant devant lui (sous-lot 11.9) ; caméra haute, il se tourne vers sa marche. Un mur, un arbre ou le toit qui cache le Bastion s'abaisse ou s'efface le temps qu'il passe.

Garde et parade sont nécessaires ; quand tous les ennemis sont morts, c'est la victoire ; si le Bastion tombe, c'est la défaite. Retour arrière pour recommencer, N pour les autres séances (meute à vaincre, entraînement, S1 à S5 : sans décor, le déplacement y reste visuel).

Les touches sont lues **par leur position** sur le clavier (Unity 6) : le tableau donne les lettres d'un clavier **AZERTY**. En QWERTY, Z Q S D deviennent W A S D, A / E deviennent Q / E, et M reste M.

| Touche | Action |
|---|---|
| Z Q S D ou flèches | se déplacer (simulé par le moteur dans les Vallons, visuel dans les séances sans décor) |
| clic droit (ou Maj) maintenu | garde : un coup est bloqué (endurance) |
| Espace | parade parfaite, juste avant l'impact |
| 1 à 6 | compétences des places actives (sous-lot V1.8 ; les touches 7 et 8 ne servent pas encore : aucune règle n'ouvre la 7e ni la 8e place) |
| R | ultime (100 de Résonance) |
| Tab | ennemi suivant comme cible |
| C | remettre la caméra derrière le personnage |
| A / E | tourner la caméra (en douceur, tant qu'on tient) |
| souris (sans clic) | **caméra à la souris libre** (choix de Damien du 26 octobre 2026, comme ESO) : gauche-droite tourne la vue, haut-bas l'incline ; curseur caché hors des écrans et des menus. Réglage de la pause : **clic gauche maintenu** à la place, comme World of Warcraft (le curseur reste alors visible) |
| Alt maintenu | rend le curseur (pour cliquer) ; **Échap** le rend aussi (pour arrêter le jeu dans l'éditeur), un clic dans le jeu le reprend |
| molette | rapprocher ou éloigner la caméra (1,6 à 24 m ; 8 m au plus dans une maison) ; bouton du milieu maintenu (curseur rendu) : tourner librement |
| T | écran des talents (combat en pause) ; T ou Échap pour le fermer |
| N | scénario suivant (Vallons, meute à vaincre, entraînement, S1 à S5), puis retour à l'aventure |
| F1 à F5 | build : Rempart, Contrecoup, Gardien, hybride, niveau 10 |
| Retour arrière | recommencer la séance |
| Échap | pause et **réglages** (souris libre ou clic maintenu, haut-bas inversé, sensibilité de la souris) |
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
| F | parler au PNJ à portée (3 m) — à un marchand : ouvrir sa boutique (V1.1) —, sinon récolter le nœud à portée |
| 1 à 9 (ou clic) | choisir une réponse du dialogue (parmi celles affichées) ; Échap ou le bouton Fermer (au doigt) le ferme |
| I | personnage : les 18 places d'équipement (V1.2), le sac avec ses filtres et son rangement, la comparaison au survol, les métiers (porter, retirer, échanger) |
| J | journal de quête et découvertes : onglets **Principales** et **Secondaires**, bouton **Suivre** (la quête suivie est sous la petite carte et son objectif sur la carte), boutons des **récompenses au choix** (V1.6) |
| K | atelier : recettes, ce qu'il manque, fabriquer (à la forge de Maëlle, à l'ouest du village, juste au nord de la place : enclume grise, « K : forger » s'affiche quand on y est) |
| P | boire un objet de soin emporté (le plus petit d'abord) |
| L | carte en grand (noms des lieux, PNJ, points sûrs, objectif) ; L, Échap ou Fermer pour revenir. En petit, elle reste en haut à droite (12.9d) |
| B | écran des compétences (sous-lot V1.5) : la barre de ta prochaine halte, les compétences apprises et à venir ; glisser une compétence sur une place, hors combat seulement (voir plus bas). K reste l'atelier |
| Entrée | faire halte près d'un point sûr (aucun ennemi proche) : sauvegarde, PV pleins ; après une défaite : reprendre au dernier point sûr |
| F6 | sauvegarder maintenant |
| N | quitter l'aventure (sauvegardée) pour l'entraînement |

Les touches de combat sont celles de l'entraînement (garde, parade, compétences, Tab, caméra). La cible se choisit parmi les ennemis en vue à 20 m au plus, du plus proche au plus loin. Le monde **s'arrête** pendant un dialogue, un écran ouvert ou la pause (jeu en solo). Au doigt, un écran ou un dialogue ouvert masque les boutons tactiles du jeu : seuls ses propres boutons (dont Fermer) répondent.

**Niveau et équipement** gagnés ou changés en route prennent effet à la **halte** suivante (le combat en cours garde le build d'entrée). **Compétences gagnées** (12.9b, barre de V1.8) : Provocation au niveau 3 (touche 3), Barrière d'acier au niveau 5 (touche 4, sur toi), Onde de rupture (touche 5, la 5e place s'ouvre avec elle) et l'ultime Bastion absolu (R, place ouverte dès le niveau 1) au niveau 10 ; Contrecoup (niveau 7) va dans la réserve. Le message du niveau annonce la compétence, la halte l'équipe et dit sa touche. Une ancienne sauvegarde garde sa barre : une compétence déjà connue avant V1.8 n'est pas placée d'office (elle reste dans la réserve). La **porte du moulin**, une fois ferrée par Maëlle, se franchit aussi après une halte. Une **défaite** ne fait rien perdre : Entrée reprend au dernier point sûr approché, PV pleins.

**Compétences** (sous-lot V1.5, décision de Damien : « partout hors combat ») : B, ou l'onglet **Compétences** du panneau (au doigt : **Sac**, puis l'onglet). À gauche, la barre telle qu'elle sera à ta prochaine halte : les places actives en éclats (touches 1 à 6), l'ultime (R), les passives en nœuds ; une place fermée dit le niveau qui l'ouvre (sous-lot V1.8 : 4 actives et l'ultime dès le niveau 1, 5e au niveau 10, 6e au niveau 20 ; passives aux niveaux 1, 5, 20 et 30). À droite, les compétences apprises (leur touche, ou « Réserve ») et celles à apprendre plus haut. **Glisse** une compétence sur une place (souris ou doigt), ou **touche-la puis touche la place** ; deux compétences de la barre s'échangent ainsi (un glissé qui revient sur sa place ne fait rien). La fiche du bas dit ce que fait la compétence (coût, recharge, portée, effet) ; une compétence à apprendre se survole ou se touche aussi. Refusé (motif sous la barre) tant qu'un ennemi est à moins de 12 m ou qu'un ennemi engagé te vise, même de loin, pendant une conversation ou si tu es tombé ; une place fermée ou une compétence pas encore apprise aussi ; à la souris, le **clic droit** sur une place la vide (réponse de Damien du 30 octobre 2026 : une place peut rester vide ; la halte ne la remplit plus). Une action qui ne change rien ne dit rien. Ce qui est gardé prend effet à la **halte** suivante (le combat en cours garde sa barre). Depuis V1.8, **Provocation** (niveau 3) prend la 3e place d'elle-même ; **Contrecoup** (niveau 7) arrive dans la **réserve** (barre pleine) : à toi de le placer, par exemple dans la 6e place au niveau 20. Si la boutique ferme pendant qu'elle est ouverte (la nuit qui tombe), le cœur rend ses offres non achetables (motif `Closed`) : leur bouton « Acheter » se grise. Une seule passive est apprise pour 2 places à partir du niveau 5 : la 2e affiche « Rien à placer pour l'instant ».

Au doigt : **Agir** (F) et **Potion** au-dessus de la garde, **Sac**, **Journal** et **Atelier** en haut, **Halte** à la place de « Rejouer ». Au doigt, seul le bouton **Garde** lève la garde (12.9c : marcher en touchant Agir ne la lève plus).

**Carte** (12.9d, demande de Damien du 25 octobre 2026) : la petite carte des Vallons, nord en haut, est en haut à droite ; la fiche de la cible passe à sa gauche et, au doigt, la rangée Menu, Aide… descend sous elle. Elle montre le décor (rivière en bleu), toi (point jaune), l'**objectif de l'étape en cours** (étoile orange : le lieu à atteindre, le PNJ à qui parler, la créature à vaincre), les PNJ (blanc), les points sûrs (vert), la forge (gris), les filons et herbes (petits points) et les ennemis **en vue** seulement (rouge, violet pour le chef). Toucher la carte, ou L, l'ouvre en grand avec les noms ; le monde s'arrête pendant ce temps, comme pour les autres écrans.

Ce que tu vérifies pour l'écran des compétences (V1.5) : B l'ouvre et le ferme ; glisser une compétence sur une autre place les échange (souris, puis au doigt sur le Note8) ; toucher une compétence puis une place fait pareil (un seul échange, un seul message ; un doigt qui glisse et se lève sur la même place ne fait rien, le choix reste) ; une place fermée affiche son niveau et refuse ; près d'un loup, le refus « Pas en plein combat » s'affiche ; après une halte, les touches 1 à 6 lancent les compétences dans le nouvel ordre ; quitter et relancer le jeu garde l'ordre choisi.

Ce que tu vérifies (essai du 12.9) : la mission d'Odile de bout en bout (traces, ferrure forgée, pré des loups, manifestation, rôdeur, Gardien, retour) ; la récolte et la fabrication ; l'inventaire et l'équipement ; une défaite et la reprise ; quitter puis relancer le jeu (la partie revient) ; la lisibilité des écrans sur PC et sur le Note8.

**Monnaie, marchands et voyage rapide** (sous-lot V1.1 ; règles : [MONDE.md](MONDE.md#monnaie-marchands-et-voyage-rapide-sous-lot-v11)) :

- **Bourse** : dans l'écran du personnage (I), à droite du titre du sac : « Bourse · 3 or 45 argent 12 cuivre ». Les quêtes et la vente en donnent ; le bruit des pièces sonne à chaque gain, achat ou rachat.
- **Marchands du bourg** (capsules dorées tant qu'ils n'ont pas de personnage Mixamo : dossiers `Mixamo/Armurier`, `Mixamo/Herboriste`, `Mixamo/Epiciere`) : Garin l'armurier au sud de la place, Sauge l'herboriste près de l'étal du sud, Bertille l'épicière près de l'étal du nord. « F : commercer avec… » s'affiche à 3 m ; **F** ouvre la boutique (le monde s'arrête, comme pour les autres écrans ; Échap ou I, J, K, L la ferment). Boutique fermée à cette heure : le message le dit et le dialogue du marchand s'ouvre à la place.
- **La boutique** : à gauche, ce qui est en vente aujourd'hui (prix, bouton **Acheter**, grisé quand l'or manque ou que le sac est plein ; le prix passe au rouge sans assez d'or) ; à droite, ton **sac** : **clic** vend un objet, **clic droit** toute la pile (au doigt : un toucher, un objet) ; la fiche au survol dit le prix de revente, « Ne se vend pas » pour un objet de quête ; un objet porté ou une potion emportée au combat ne se vendent pas (le message dit pourquoi) ; sur une pile de potions dont une partie est emportée, seule la part libre se vend. Plus de neuf offres : des boutons **<** et **>** autour de « 1 / 2 » changent de page. En dessous, **Rachat** : ce que tu as vendu depuis le lancement du jeu, au même prix (clic : racheter). En bas, la bourse et **Parler** (le dialogue du marchand). Chaque achat, vente ou refus s'écrit dans les messages.
- **Voyage rapide** : la carte (L) a une colonne **Voyage rapide** à gauche : un bouton par point sûr découvert (« Tu es ici » là où tu es). Un point sûr se découvre en passant à 5 m (« Point sûr découvert : … ») ; celui où tu commences l'est déjà. Hors combat seulement (aucun ennemi à 12 m, aucun ennemi qui te vise) : sinon les boutons sont grisés et la raison s'affiche sous eux. Le point où tu es reste grisé (« Tu es ici » : fais une halte). Plus de sept points sûrs : boutons **<** et **>** sous la liste. Le voyage sauvegarde, fait un fondu au noir et t'amène au point sûr, PV pleins, comme une halte. Gratuit.

Ce que tu vérifies (V1.1) : la bourse dans l'écran du personnage ; la boutique de chaque marchand (acheter une potion chez Sauge, vendre une peau de loup chez Bertille, la racheter) ; un achat refusé sans assez d'or ; le stock de Garin qui change d'un jour du monde à l'autre (2 h de jeu à l'époque, 3 h depuis le 30 octobre 2026) ; la découverte de la place du bourg, puis le voyage rapide vers la place du village depuis la carte, et le refus près d'un loup ; quitter et relancer le jeu (bourse et points sûrs gardés, rachats oubliés).

### Équipement complet (sous-lot V1.2)

Règles : [MONDE.md](MONDE.md#équipement-complet-sous-lot-v12). Aucune touche nouvelle : tout se fait dans l'écran du personnage (**I**, ou **Sac** au doigt).

- **Les 18 places** autour du sceau du Bastion : à gauche la tête, les épaules, la cape, le torse, les brassards et les mains ; à droite la ceinture, les jambes, les pieds, le cou et les deux anneaux ; sur le sceau l'arme, le bouclier, les deux reliques, l'**Éclat du Seuil** au centre et l'**outil de métier** en bas. Chaque case dit sa place et l'objet porté (couleur de sa rareté).
- **Porter** : clic sur un objet du sac (un anneau va au premier doigt libre). **Retirer** : clic sur l'objet porté ; le bouclier du Bastion et l'Éclat ne se retirent pas (« Cette place reste toujours occupée… ») : on en porte un autre à la place. **Échanger** les deux anneaux ou les deux reliques : clic droit sur l'un. En plein combat, tout est refusé (« En plein combat : change ton équipement hors combat. »). L'effet vient à ta prochaine halte.
- **Comparaison** : au survol d'un objet, la fiche montre son niveau requis, l'arbre auquel il convient et ce qu'il remplacerait, puis chaque valeur « porté → nouveau » avec l'écart **en vert** (mieux) ou **en rouge** (moins bien) ; pour un bouclier : part bloquée en garde, endurance par blocage, fenêtre de parade. Elle marche aussi dans la boutique de Garin.
- **Sac** : sous le sac, les filtres **Tout**, **Équipement**, **À ma portée** (ce que ton niveau permet), **Soins**, **Matériaux**, et le bouton **Ranger** (par sorte, puis par rareté, puis par niveau, à chaque clic).
- **Sur le Bastion** se voient l'arme, le bouclier et la cape. Avec le paladin de Mixamo, son épée et son bouclier (dans le modèle) prennent la teinte de la rareté ; une masse, une hache ou un marteau cache l'épée et prend une **forme simple** dans la main droite ; la **cape** est une **forme simple** dans le dos (aucun modèle de cape dans les packs déposés, aucun achat sans l'accord de Damien). Sans modèle, arme et bouclier sont aussi des formes simples. Les pièces d'armure ne se voient pas : elles attendront des modèles en morceaux.

Sous-lot V1.3 (effets d'équipement, [MONDE.md](MONDE.md#effets-déquipement-ensembles-et-propriétés-tirées-sous-lot-v13)) : la **fiche** d'un objet du sac ou porté compte ses **propriétés tirées** dans les écarts, puis montre ses détails : chaque propriété (« Vigueur : +24 pv maximaux »), son **effet** (en vert s'il s'ajoute à ce que tu portes, en rouge pour l'effet de l'objet remplacé, perdu), son **ensemble** (« Ensemble « Garde de la tour » : 3 → 4/6 pièces ») et chaque palier (vert s'il s'ouvre, rouge s'il se ferme, atténué tant qu'il ne vaut pas). Vendre un objet aux propriétés tirées vend celui-là, et le racheter le rend avec ses propriétés.

Ce que tu vérifies (V1.3) : survoler la Lame du Seuil ou l'Amulette de l'aube (effet en vert), une pièce de la Tenue du veneur (pièces portées sur le total), un objet tombé d'une créature (une propriété tirée) ; porter l'amulette, faire halte, réussir une parade parfaite (une Charge de plus) ; relire les textes des effets et des ensembles.

Sous-lot V1.4 (forge, qualité, usure, [MONDE.md](MONDE.md#forge-qualité-usure-sous-lot-v14)) : dans l'**atelier** (K), le bouton Fabriquer d'une recette de forge à qualité ouvre le **petit jeu de forge** : un curseur va et vient sur une barre, la cible dorée au milieu ; trois frappes (**Espace**, clic sur la barre, ou bouton **Frapper**, au doigt aussi) ; les notes et les chances (chef-d'œuvre, supérieure, rater) s'affichent ; à la troisième frappe, le cœur forge la pièce et le message dit sa qualité. Une recette au-dessus de ton niveau dit son risque de rater sur sa carte. La **fiche** d'un objet montre sa qualité (« Chef-d'œuvre, signé Bastion ») et sa solidité quand elle est entamée ; un objet porté usé à zéro a un bord rouge. Bouton **Réparer · prix** en bas de l'atelier quand tu es à la forge, et dans la boutique de l'armurier.

Ce que tu vérifies (V1.4) : récolter du minerai, forger une ferrure puis une épée de garde (le petit jeu, sa qualité) ; tenter le casque de fer à un niveau de forge trop bas (risque affiché, échec possible avec la moitié des matériaux rendue) ; combattre jusqu'à voir la solidité baisser dans la fiche, puis réparer à la forge et chez l'armurier.

Ce que tu vérifies (V1.2) : une nouvelle partie commence avec l'Éclat, l'épée et l'écu portés ; une ancienne partie reçoit l'Éclat (« Équipement mis à jour ») ; acheter la targe chez Garin, comparer (part bloquée en rouge, endurance et parade en vert), la porter, faire halte, puis essayer une parade un peu tôt ; le bouclier ne se retire pas, mais s'échange ; deux anneaux et leur échange au clic droit ; les filtres et Ranger ; la cape visible dans le dos (forme simple), et à quel point la teinte de l'épée et du bouclier du paladin se voit.

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
| au-dessus | **Cible**, **Vue** (et **Allié** en réseau) | ennemi suivant, caméra remise derrière le personnage, allié visé |
| moitié droite, hors des boutons | glisser un doigt | tourner la vue (gauche-droite) et l'incliner (haut-bas) |
| moitié droite | écarter ou rapprocher deux doigts | rapprocher ou éloigner la caméra |
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
4. **Gestes de la caméra** : pincer pour rapprocher ou éloigner, glisser pour tourner et incliner ; rien ne tourne tout seul.
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

**Demandes de Damien du 26 octobre 2026 au soir** (« les loups et les buissons sont tout blancs aussi, les PNJ, les bâtiments et la rivière sont à faire aussi, j'aimerais aussi qu'on puisse bouger la caméra avec la souris ») :

- **Personnages** : un dossier par personnage dans `Mixamo/` (nom du dossier = celui du PNJ ou de l'ennemi, sans accent) : `Odile` (*Peasant Girl*), `Maelle` (*Maria J J Ong*), `Gaspard` (*Knight D Pelegrini*), `Archer` (*Erika Archer* avec arc), `Rodeur` (*Nightshade J Friedrich*), `Gardien` (*Warrok W Kurniawan*, les deux formes du Gardien). Dans chaque dossier, le fichier avec la peau et les animations, nommées d'après leur mouvement (« Archer Idle », « Archer Walk », « Archer Death »). Les PNJ respirent sur place ; l'archer, le rôdeur et le Gardien marchent, courent et meurent. Hauteur et rotation de chacun se règlent dans le catalogue (*Characters*).
- **Bâtiments** (`PolyHaven/Surfaces`, CC0) : murs en pierre (*rustic stone wall*), moulin en crépi (*plastered stone wall*) avec un toit de tuiles (*clay roof tiles*) et des ailes en planches (*weathered brown planks*), piliers et pont en planches, toit des piliers en tuiles, murets du bord en pavés (*cobblestone floor*) ; textures à l'échelle du monde (taille d'une répétition réglable dans *Surfaces*).
- **Rivière** : eau faite par le code (couleur réglable, surface lisse qui reflète, petites vagues qui glissent).
- **Loups et sangliers** : leurs packs n'ont pas de texture ; ils prennent une couleur (*Wolf Color*, *Boar Color*). **Buissons blancs** : il manquait des textures du MegaKit (ajoutées depuis le zip de Damien) ; le menu réimporte les modèles dont un matériau est resté sans texture.
- **Caméra à la souris libre** (choix de Damien : « comme ESO ») : voir le tableau des commandes ci-dessus.

**Demandes de Damien du 27 octobre 2026** (« la forge est à faire, les minerais et l'herbe de l'Aube aussi, le pont, les bâtiments, l'eau ne sont pas du tout réalistes, Odile est tournée du mauvais côté, les espèces de buissons tout rouges je ne sais pas si c'est normal, le mur à côté de l'archer ne me plaît pas, peut-être mettre quelque chose d'autre ») et ses choix :

- **Eau** : vraie eau, shader `EdS/Eau` du jeu (`Assets/EdS/Game/Eau.shader`) : transparente, elle reflète le ciel du catalogue d'autant plus qu'on la regarde de biais, porte deux couches de vagues qui avancent et renvoie l'éclat du soleil. Le lit de la rivière est creusé dans le terrain (1 m, berges en pente douce, terre mouillée sur les bords), la surface est 30 cm sous le sol où l'on marche, et la rivière coule aussi sous le pont. Ses réglages (couleurs, taille et force des vagues, opacité, reflet) se changent dans `Assets/Style/Generated/Eau` ; la couleur de base reste *Water Color* du catalogue. Rien ne change au combat : la rivière arrête toujours la marche, pas la vue.
- **Pont** : un tronçon du ponton en bois de Poly Haven (*modular wooden pier*), tablier au ras du sol, pieux dans le lit.
- **Mur près de l'archer** (choix de Damien : « rondins faits par moi ») : les murs et la porte du moulin deviennent une palissade de rondins taillés en pointe (deux rangs décalés, hauteurs variées), écorce de Poly Haven (*bark brown 02*). Les haies, jusque-là des blocs de pierre, deviennent des haies d'arbustes de Poly Haven. Emprises du moteur inchangées.
- **Forge de Maëlle** : enclume de fer sur son billot (faite par le code), marteau couché dessus (*cross pein hammer*), foyer de pierre (*stone fire pit*) avec des braises et une lueur qui vacille, tonneau (*barrel 03*) et caisse (*wooden crate 02*).
- **Herbe de l'Aube** : une pervenche de Poly Haven (*periwinkle plant*), plus grande que nature. **Filons de fer** : un rocher réaliste veiné d'éclats de minerai rouille.
- **PNJ** : au repos, chacun regarde le point sûr le plus proche (d'où l'on arrive) ; il se tourne vers toi quand tu passes à moins de 8 m.
- **Buissons rouges** : c'était un buisson d'automne du MegaKit, mêlé aux plantes de Poly Haven ; seules les plantes réalistes restent quand il y en a.
- **Herbe trop jaune** : l'herbe du sol prend une teinte plus verte (*Grass Tint* du catalogue, blanc pour la couleur d'origine).
- **Moulin** (choix de Damien : le moulin blanc A de Sketchfab, *Windmill* de Tim0, licence CC-BY : l'auteur est cité dans les [crédits](CREDITS.md)) : déposé dans `Sketchfab/Windmill` (fichier *Windmill anim+LOD*, ses textures et la transparence des toiles). Il est livré avec sa version simplifiée posée à côté : seul le moulin détaillé est gardé. Sa tour prend la largeur de l'emprise du moulin, ses ailes tournent. S'il regarde de travers, sa rotation se règle dans le catalogue (*Props* → *Mill* → *Yaw*, par exemple 90, 180 ou −90) ; sans lui, l'ancien moulin reste.
- Correctif : une deuxième préparation ne remplace plus les matières des personnages de Mixamo par une seule texture.

Objets téléchargés par Claude chez Damien (Poly Haven, CC0, 1k) : `PolyHaven/barrel_03`, `cross_pein_hammer`, `modular_wooden_pier`, `periwinkle_plant`, `stone_fire_pit`, `wooden_crate_02`, et `PolyHaven/Surfaces/bark_brown_02` ; de Sketchfab (compte de Damien, CC BY) : `Sketchfab/Windmill`. Le fort en pierre (*modular fort 01*, 44,7 Mo), téléchargé par erreur (Claude le croyait en bois), est resté dans le zip de `Téléchargements` et n'est pas déposé dans le projet.

Limites de l'essai 3 (en partie levées par l'essai 4 ci-dessous) : les arbres restent dessinés (MegaKit) ; les loups et les sangliers restent ceux des packs low poly (choix de Damien : les animaux plus tard) ; sur le téléphone, la forêt est réduite au tiers, les plantes à la moitié, les arbustes des haies espacés, le relief du terrain plus grossier et le halo coupé, mais l'essai réaliste n'est pas encore réglé pour le Note8 (à faire si le style est gardé) ; hors du lit de la rivière, le terrain est plat comme le sol du moteur ; l'eau n'a pas d'écume ni de reflets des objets (seulement du ciel).

### Essai réaliste 4 (27 octobre 2026)

Demande de Damien : « je veux vraiment le plus réaliste possible dès maintenant, on ne pourrait pas faire 2 packs de textures différents, 1 pour le PC et 1 pour le téléphone ? », puis « je veux vraiment le max du réalisme ! ». Téléchargement accepté (« Oui, tout », environ 391 Mo), rangé par Claude dans `unity/Assets/Style` (toujours non suivi par git) :

- `PolyHaven2k/` (CC0, en 2k au lieu de 1k) : les sols (`Ground/`, 12 textures), les matières des bâtiments et l'écorce (`Surfaces/`, 12 textures), et les 26 modèles déjà employés (plantes, rochers, bois mort, objets de la forge, ponton, pervenche, arbustes des haies).
- `PolyHaven2k/Sky/` : le même ciel en 8k (75 Mo). **À déposer par Damien** (trop gros pour le transfert de Claude, limité à 30 Mo par fichier) : dans `Téléchargements`, clic droit sur `polyhaven-ciel-8k.zip` → *Extraire tout…* → destination `D:\eclats-du-seuil\unity\Assets\Style`. Sans lui, le ciel 1k reste.
- `Sketchfab/Arbres/` (CC BY, de *evolveduk*, cité dans les [crédits](CREDITS.md)) : quatre vrais arbres, chêne (`Oak`), pin (`Pine`), épicéa (`Spruce`) et bouleau (`Birch`), avec leurs textures d'écorce et de feuillage (couleur, relief, transparence).

Les anciens fichiers 1k peuvent rester : quand un fichier de Poly Haven est là en plusieurs résolutions, seule la plus haute compte.

1. **Préparer** : menu **Éclats du Seuil → Préparer l'essai de style**. La première fois, Unity réimporte toutes les textures (plusieurs minutes). La fenêtre de fin dit en plus si l'herbe en brins et le rendu du téléphone sont prêts, et combien de pièces de chaque arbre ont été habillées (« Arbre oak.fbx : 2 pièces habillées »).
2. **Jouer** : Play.

Ce qui change :

- **Arbres** : les quatre arbres de Sketchfab remplacent ceux du MegaKit (forêt et arbres de l'aire). Chaque arbre est préparé une fois dans `Assets/Style/Generated` (« oak (arbre) ») : écorce et feuillage reçoivent chacun leur matériau, les feuilles sont découpées et ne se dégarnissent pas au loin. Hauteur réglable dans le catalogue (*Tree Height*, 11 m ; les arbres de l'aire en font les quatre cinquièmes) ; emprises du moteur inchangées.
- **Vent** : arbres, arbustes, plantes et herbe de l'Aube bougent au vent (shader `EdS/Feuillage` du jeu : balancement qui croît avec la hauteur, feuilles qui frémissent, lumière du soleil qui traverse les feuilles à contre-jour), les arbres moins que les herbes. Les ombres bougent avec eux.
- **Herbe en brins** : de vraies touffes d'herbe (11 brins courbés, couleur du pied à la pointe, quelques brins secs) poussent sur le sol en herbe, environ 4 par m² (*Grass Density*), hors des obstacles, de la terre et de la rivière, dessinées jusqu'à 45 m de la caméra (*Grass Distance*). Couleurs et vent se règlent dans `Assets/Style/Generated/Herbe`. Shader `EdS/Herbe` du jeu.
- **Reflets** : la rivière reflète maintenant les berges, les arbres, la palissade et le moulin (une sonde de reflets prise une fois au début du combat), plus le ciel.
- **Rendu de l'ordinateur** : ombres fines sur 90 m en quatre cascades (carte 4096), ombres douces, bords lissés (MSAA ×4), occlusion ambiante de l'écran (les creux et les pieds des objets s'assombrissent), lumières des feux par pixel. Le menu règle le rendu URP du projet.
- **Deux packs de textures, un seul dépôt** (réponse à la demande de Damien) : chaque texture est gardée une fois, en pleine taille pour le PC (2k, le ciel 8k) ; Unity la réduit lui-même pour Android à la construction (1k, le ciel 2k), sans second téléchargement. Le téléphone a aussi son rendu plus léger (`Assets/Style/Generated/EdS_URP_Telephone` : ombres sur 35 m, deux cascades, sans ombres douces ni occlusion), choisi par le jeu au lancement sur Android ; l'herbe y est cinq fois moins dense, en touffes de 6 brins, et dessinée à moitié moins loin.

Ce que tu regardes : si les arbres et l'herbe font vrai de près comme de loin, si le vent est naturel (ni trop, ni trop peu : *Wind* dans les matériaux de `Generated`), si l'eau reflète bien ses berges, et si l'image reste fluide sur ton PC.

Limites : la sonde de reflets est prise une fois (un personnage qui bouge n'apparaît pas dans l'eau) ; l'herbe en brins ne pousse que sur l'aire du combat (le terrain qui déborde garde sa texture d'herbe) et ne projette pas d'ombre ; les arbres de Sketchfab sont des fichiers anciens (FBX 6.1 de Tree It) : si Unity n'arrivait pas à les lire, les arbres du MegaKit resteraient (la fenêtre de fin dit combien de pièces de chaque arbre ont été habillées) ; le rendu du téléphone n'est pas encore essayé sur le Note8.

### Essai réaliste 5 (27 octobre 2026)

Avis de Damien sur l'essai 4 (vidéo du 27 octobre 2026 au matin) : « c'est déjà beaucoup mieux dans le global », puis : « il faudrait texturer le bâtiment », « l'herbe pourrait être encore mieux », « le gros bloc en brique, je ne sais pas trop à quoi il sert », « le pont, on pourrait l'améliorer », « le mur de feuilles derrière les loups, je ne suis pas fan », « je voudrais qu'on améliore complètement l'esthétisme de l'interface ». Ses choix : muret de pierres sèches, pont cintré fait par Claude, interface façon Diablo 4 (des icônes au lieu de texte), police et icônes gratuites.

Rien à télécharger de plus : la police et les icônes viennent avec la branche (`unity/Assets/EdS/Interface`). Préparer comme d'habitude (menu **Éclats du Seuil → Préparer l'essai de style**), puis Play.

Ce qui change :

- **Abri à piliers** (près de la forge) : une vraie charpente faite par le code : poteaux de bois équarri sur des socles de pierre, sablières, entraits, aisseliers, poinçons, faîtage et chevrons, toit à deux pans couvert de tuiles posées en rangées parallèles au faîtage, avec une faîtière ronde et des débords tout autour. Le toit s'efface quand tu passes dessous.
- **« Gros bloc en brique »** : c'était le *front de taille* de la carrière (la paroi d'où l'on tire les blocs). Il devient une paroi de gros rochers du catalogue, un second rang plus petit au pied et quelques blocs tombés, tous dans son emprise.
- **Mur de feuilles** → **muret de pierres sèches** (ton choix) : assises de pierres irrégulières en quinconce, plus large en bas qu'en haut, pierres traversantes aux deux bouts, couronnement de pierres posées de chant ; pierre brute du sol (roche moussue de Poly Haven). Il s'abaisse comme un mur quand il te cache.
- **Pont** → **pont cintré en bois** fait par le code (ton choix) : planches posées une à une (largeurs et jeux variés), trois longerons qui suivent le cintre, poteaux et deux lisses de rondins de chaque côté, culées de pierres sur les rives. Le tablier monte de 35 cm au milieu, et les personnages montent avec lui (affichage seulement : le combat ne change pas).
- **Herbe** : brins plus fins et plus nombreux (16 par touffe, 8 sur téléphone), plaques d'herbe plus haute et plus rase, plaques plus sèches, coins fleuris (blanc, jaune, violet, bleu), léger reflet du soleil, pied moins sombre ; au loin, l'herbe s'abaisse en douceur au lieu d'apparaître d'un coup. Les nouvelles couleurs de `Assets/Style/Generated/Herbe` sont posées une fois par le menu ; tes réglages faits ensuite sont gardés.
- **Interface façon Diablo 4** (ton choix) :
  - globe de **vie** à gauche et d'**endurance** à droite (le liquide monte et descend), valeur au milieu ;
  - **compétences en icônes** entre les deux : touche dans le coin (1 à 6, R pour l'ultime ; sous-lot V1.8 : six éclats, trois de chaque côté du sceau, l'ultime au bout à droite, chaque éclat à sa place fixe), recharge en arc avec le temps restant, coût d'endurance en bas (rouge s'il en manque), ultime qui luit quand elle est prête ;
  - **Résonance** en barre fine et **Charges** en losanges au-dessus des compétences ; **garde** en icône de bouclier au-dessus du globe de vie (argent quand tu gardes, or pendant la parade) ;
  - cadres d'or sur les panneaux (aide, cible, carte, écrans de l'aventure, du réseau et des talents), boutons et champs assortis, police **Cinzel** pour les titres, les noms et les chiffres ;
  - sur téléphone, le tout est plus petit et plus serré quand les commandes tactiles sont affichées.

Ce que tu regardes : l'abri et ses tuiles, le muret, le pont (traverse-le), la paroi de la carrière, l'herbe de près et de loin, et l'interface pendant un combat (recharges, globes, garde).

Limites : les icônes ne couvrent que les compétences du Bastion (une icône d'épée pour les autres) ; la raison d'une compétence indisponible n'est plus écrite sous la case (la case s'assombrit, et le coût passe au rouge s'il manque d'endurance) ; l'interface n'est pas encore essayée sur le Note8 ; la sonde de reflets de l'eau est prise une fois.

### Essai réaliste 6 (27 octobre 2026)

Interface « Fragments du Seuil », étape 1.

Avis de Damien sur l'essai 5 (vidéo du 27 octobre 2026, 21 h 20) : « Le bouclier se modélise comme ça quand je l'active c'est à corriger », « je voudrais que la mini Map soit améliorée là ça fait vraiment schéma », « l'interface est mieux mais c'est pas ce que je préfère pareil pour l'inventaire, l'aide tout ces petits détails », puis (22 h 45) « Les attaques aussi des ennemis sont à changer c'est à dire la charge rouge du sanglier ou les rond des autres personnages ». Après plusieurs séries de maquettes (« quelque chose de plus futuriste », « qui fait vraiment plus jeux vidéo », « je veux vraiment qu'on crée quelque chose de nouveau »), il a choisi une identité créée pour le jeu, **« Fragments du Seuil »** (l'interface faite des éclats du Seuil, couleurs de l'aube : or, rose, turquoise), retouchée en vidéo (« Le mur de briques, l'animation de la parade avec le mur bleu j'aime pas faut le supprimer, le reste est bien »).

L'essai 6 se fait en trois étapes : **1. l'interface en jeu** (cette branche), 2. les écrans (Personnage, Journal, Atelier, dialogues) et l'arbre des Éclats ouvrable dans l'aventure, 3. les finitions animées.

**Préparer.** Rien à télécharger : polices, dessins et icônes viennent avec la branche. À la première ouverture, Unity ajoute le paquet *Unity UI* (uGUI) et l'éditeur importe tout seul les ressources de **TextMeshPro** (dossier `Assets/TextMesh Pro`, non suivi par git ; menu **Éclats du Seuil → Préparer l'interface (TextMeshPro)** pour le refaire). Sans elles, le jeu marche quand même, avec un texte moins net. Puis **Préparer l'essai de style** comme d'habitude, et Play.

Ce qui change :

- **Plus de disque sur le bouclier** : le chevalier garde son vrai bouclier. Quand tu gardes, l'interface affiche « Garde levée » (turquoise), « Parade » (or) ou « Garde brisée » (rouge). Si tu ajoutes à son dossier l'animation Mixamo **« Sword And Shield Block »** (à l'essai 7, Claude a déposé « Sword And Shield Block Idle », la garde tenue en boucle), il lève aussi son bouclier quand il garde à l'arrêt (sans elle, il reste au repos). Le disque reste seulement pour le corps simple, sans modèle.
- **L'interface en jeu refaite en uGUI** (texte TextMeshPro, net à toute taille) :
  - en bas, le **sceau fendu** du Bastion : la vie à gauche (rouge), l'endurance à droite (ambre), en liquide qui ondule. La **Résonance** brille dans la fissure du milieu, et les **Charges** sont des éclats en arc au-dessus ;
  - les **compétences en éclats** flottants autour du sceau : icônes peintes, recharge en cercle avec le temps, coût d'endurance (rouge s'il en manque), touche dessous. Un éclair passe quand tu lances, une lueur quand elle revient ;
  - en haut au milieu, la **cible** dans une barre fendue aux ailes dorées : son vrai nom (« Sanglier », plus « SANGLIER_V2 »), la part de vie perdue qui reste claire un instant, et l'**annonce du prochain coup** (« Charge · 2,5 s », « imparable » en rouge) ;
  - au-dessus des combattants : **nom et barre de vie**, et la barre du coup qui arrive ;
  - en haut à droite, la **carte peinte** de la zone (forêt, rivière, pont, toits, moulin, rochers), centrée sur toi, nord en haut. Ta flèche tourne avec toi ; elle montre les ennemis en vue, les PNJ, les points sûrs, l'atelier, les récoltes et l'objectif (ramené au bord s'il est loin). Dessous : le lieu, la zone et l'heure, puis la **quête en cours** et son avancée. **L** ouvre toujours la grande carte, maintenant peinte elle aussi ;
  - l'**aide** (H) en dalle brisée, une touche par ligne (T : talents, dans l'aventure) ;
  - les messages, la pause, l'indication d'action (« F : parler à… ») et l'annonce de défaite au milieu de l'écran ;
  - effets : reflets qui passent, braises qui montent du sceau, éclats de lumière.
- **Annonces des attaques au sol refaites** (la « charge rouge du sanglier », les ronds) :
  - une forme translucide au bord lumineux, avec un front qui avance jusqu'au bout au moment du coup ;
  - des chevrons qui filent vers l'avant pour une charge ou un cône, des ondes pour un cercle ;
  - couleur ambre qui rougit à l'approche, rouge vif et pulsé si l'attaque est imparable ;
  - les anneaux sous la cible (or) et l'allié visé (turquoise) prennent le même style.

Ce que tu regardes :
- le sceau pendant un combat : vie, endurance, Résonance, Charges ;
- les recharges des éclats ;
- la cible et son annonce ;
- les annonces au sol du sanglier et des loups ;
- la carte en marchant (la flèche tourne, les repères bougent) ;
- la garde : plus de disque.

**Étape 2 : l'arbre des Éclats dans l'aventure.** **T** ouvre l'arbre des talents dans l'aventure. Avant, il ne s'ouvrait qu'à l'entraînement.
- Une constellation par arbre : la graine de lumière en bas, les talents en éclats hexagonaux avec leurs icônes peintes et leurs rangs en losanges, la chaîne qui monte vers le **talent majeur**, entouré de l'anneau des points placés dans l'arbre.
- **Clic** : un point. **Clic droit** : un point de moins. Au doigt, les boutons + et − de la fiche du talent ; la fiche dit aussi ce que fait le talent et ce qu'il exige.
- **Valider les points** les garde : ils prennent effet à ta prochaine halte, comme l'équipement. **Annuler** (ou T, Échap) ferme sans rien garder.
- Pas en plein combat : éloigne-toi d'abord des ennemis (12 m).
- Sur téléphone, le bouton **Talents** est dans la rangée du haut.

**Étape 2 : les écrans de l'aventure.** Ils sont refaits dans le même style, en un grand panneau à onglets : **Personnage**, **Arbre des Éclats**, **Journal**, **Atelier**, **Carte**. Un clic sur l'onglet change d'écran.
- **Personnage** (I) :
  - l'équipement porté autour du sceau du Bastion : arme, bouclier, torse, cou ;
  - le statut : attributs, PV, endurance, points d'Éclat à placer ;
  - le **sac** en cases avec icônes : la fiche d'un objet s'ouvre au survol, avec ses bonus et son niveau requis. Clic : le porter. Clic sur un objet porté : le retirer ;
  - la barre d'expérience et les métiers.
- **Journal** (J) : les quêtes avec leur étape et leur avancée, et les découvertes. Sous-lot V1.6 : deux onglets (principales, secondaires, au clic ou au doigt), « Suivre » sur chaque quête en cours, et un bouton par objet quand une quête attend le choix de sa récompense ; aucune touche de plus que J.
- **Atelier** (K) : les recettes en cartes, avec ce qu'il faut, ce qu'on obtient, pourquoi ce n'est pas possible ici, et le bouton **Fabriquer**.
- **Carte** (L) : la carte peinte en grand, avec les noms des lieux, des PNJ et des points sûrs, l'objectif et les ennemis en vue.
- **Dialogue** : en bas de l'écran, réponses en boutons. Les touches 1 à 9 marchent toujours.

**Étape 3 : finitions animées.**
- Un **niveau gagné** s'annonce en grand : titre doré, rayons de lumière qui tournent, « Effet à ta prochaine halte ».
- Quand ta **vie passe sous 30 %**, les bords de l'écran rougissent et battent, de plus en plus vite.
- La barre de la **cible blanchit** un instant à chaque coup qu'elle prend.
- Le panneau des écrans et l'arbre des Éclats apparaissent **en fondu**.

Limites des étapes 1 à 3 :
- les menus du réseau et l'écran des talents de l'entraînement gardent l'ancienne interface ;
- la mise en page n'a pas encore été essayée sur le Note8 ;
- la flèche du joueur ne tourne pas encore sur la grande carte.

### Essai réaliste 7 (28 octobre 2026)

Avis de Damien sur l'essai 6 (photo du 28 octobre 2026, 1 h 41) : « Tout est bon les icônes sont juste trop gros faut tous les rétrécir, c'est quoi les petits trucs juste au dessus de la barre de vie ? Si c'est juste de l'esthétique tu peux les enlever. » Puis, pour la même correction : « Fait moi tout ça aussi » : sons d'interface libres, effets en particules, animations Mixamo (garde, coups variés), packs gratuits.

**Préparer.** Claude a déposé les fichiers dans ton projet (dossier `Assets/Style`, non suivi par git) :
- `Mixamo` : la garde « Sword And Shield Block Idle » et 7 gestes du chevalier (coup haut, coup de garde, coup de pied, frappe puissante, sort, coup reçu, coup sur la garde) ;
- `Mixamo/Archer`, `Rodeur`, `Gardien` : l'attaque et le coup reçu de chacun, plus le bond et le rugissement du Gardien ;
- `Music` : les musiques du village, de l'exploration, du combat et du Gardien ;
- `Ambience` : les nappes des oiseaux, du vent et de la rivière ;
- `Sounds/Combat`, `Sounds/Creatures`, `Sounds/Loot` : l'épée qui fend l'air, les sorts, les grognements, le rugissement, les pièces.

Les sons de l'interface et les textures des particules viennent avec la branche. Menu **Éclats du Seuil → Préparer l'essai de style**, puis Play. La fenêtre de fin compte maintenant les bruitages variés, les musiques (sur 4) et les ambiances (sur 3).

Ce qui change :
- **Compétences plus petites** : les éclats sont réduits d'un quart et rapprochés du sceau ; la touche, le coût et la recharge restent lisibles.
- **Les petits losanges au-dessus du sceau** sont tes **Charges de Rempart** (0 à 5) : tu en gagnes une à chaque parade parfaite, et Contrecoup les dépense. Ils n'apparaissent plus quand tu n'en as aucune. Ceux que tu détiens brillent en or, avec un halo ; les places libres restent pâles.
- **Sons de l'interface** (Kenney, CC0) : clic et survol des boutons, ouverture et fermeture du livre et de l'arbre, onglets, compétence prête, Charge gagnée, éclat placé ou retiré, validation, erreur, fanfare du niveau et des quêtes.
- **Effets en particules en 3D** (textures CC0 de Kenney, shader « EdS/Particule ») :
  - coup reçu : étincelles et éclair, plus forts quand le coup fait mal ;
  - coup gardé : étincelles turquoise ;
  - parade parfaite : éclair d'or, emblème des Éclats, onde au sol ;
  - garde brisée : éclats rouges et fumée ;
  - compétence : arc de lumière devant l'épée, ou cercle tournant au sol pour un sort ;
  - niveau gagné : colonne de lumière et étoiles ;
  - potion : éclats verts ; mort : poussière ; Charge gagnée : éclat d'or ;
  - coup critique : éclair et onde d'or sur ta cible ; onde du Gardien : onde et poussière à ses pieds.
- **La caméra tremble** quand tu prends un coup, gardes, pares ou que ta garde casse, et quand le Gardien rugit ou meurt. Le tremblement suit la force du coup (quelques centimètres pour un coup gardé, une vingtaine pour un gros coup) et se calme en moins d'une seconde.
- **Gestes variés** : chaque compétence du Bastion a son geste :
  - Frappe du rempart : coup haut ;
  - Heurt de bouclier : coup de garde ;
  - Contrecoup : frappe puissante ;
  - Onde de rupture : taille vers le sol ;
  - Provocation, Barrière d'acier, Bastion absolu : sort.

  Le chevalier accuse les coups, ou les encaisse sur son bouclier quand il garde.
- **Les adversaires attaquent vraiment** : jusqu'ici, ils ne jouaient jamais leur attaque.
  - Une attaque annoncée au sol (charge du sanglier, pluie de flèches, ondes) fait jouer leur geste un peu avant l'impact.
  - Une attaque immédiate (morsure du loup, flèche, éclat, griffe du Gardien) fait jouer le sien un peu avant le coup, si sa cible est à portée.
  - Avec un cri : grognement du loup ou du sanglier ; le Gardien rugit pour son onde et bondit pour sa ruée.
  - L'archer, le rôdeur et le Gardien accusent aussi les coups.
- **Musique et ambiance** : la musique suit le lieu et le combat (village, exploration, combat, Gardien), en fondu. Une musique quittée reprend où elle en était ; il faut rester quatre secondes dans un lieu pour que sa musique arrive (pas de va-et-vient au bord du village). Les oiseaux et le vent tournent en boucle, et la rivière s'entend en s'approchant de son lit. Tout baisse pendant la pause. Les volumes se règlent dans le catalogue (`MusicVolume`, `AmbienceVolume`).

Ce que tu regardes :
- les éclats plus petits ;
- les Charges après une parade parfaite (Espace au bon moment) ;
- les effets pendant un combat ;
- les gestes des compétences 1 à 6 et R (la touche 5 n'est plus l'ultime depuis V1.8 ; 7 et 8 ne font rien pour l'instant) ;
- le loup ou le Gardien qui attaque ;
- la musique quand tu sors du village, puis quand tu combats ;
- la rivière près du pont.

Limites :
- rien n'a encore été essayé dans Unity ni sur le Note8 ;
- les animaux des packs (loup, sanglier) gardent leurs propres animations ;
- la musique du village couvre tout le lieu « Village » ;
- l'agrandissement de la zone de jeu se fait dans la PR suivante.

### Vallons agrandis (28 octobre 2026)

Demande de Damien : « est-ce que tu peux aussi agrandir la map et l'améliorer ? je parle vraiment d'un grand agrandissement, on pourrait mettre des batiments, des routes ou pleins d'autres choses », puis « pas besoin de m'envoyer un plan fais le et on verra après ».

**Préparer.** Rien à télécharger : tout est fait par le code avec les textures déjà dans `Assets/Style` (crépi, pierre, planches, tuiles, pavés, terre). Menu **Éclats du Seuil → Préparer l'essai de style**, puis Play.

Ce qui change :
- **La zone fait 240 × 170 m** (84 × 58 avant). Le village, le moulin, la carrière et le Seuil n'ont pas bougé.
- **Bourg d'Aube**, à l'ouest :
  - huit maisons à colombages (soubassement de pierre, fenêtres à volets, porte côté rue avec ses marches, cheminée) ;
  - une auberge à deux étages avec son enseigne, une chapelle de pierre au clocher d'ardoises ;
  - une place pavée avec son puits et deux étals à auvent rayé.
- **Champs du sud** : grange, ferme, champs de blé (mûr ou vert) et labours. Une maison de plus au hameau de la carrière.
- **Autres lieux** :
  - le **lac des Aubes**, où se jette la rivière ;
  - le **hameau de la carrière** ;
  - la **tour de guet** crénelée, au nord ;
  - le **camp des rôdeurs** (palissades, tentes), au nord-est ;
  - de **grands bois**, dont la forêt des Loups.
- **Routes** : chemins de terre battue et place pavée relient tous les lieux. On y marche comme ailleurs.
- **Dix-sept créatures de plus** (loups, sangliers, archers, rôdeurs), et cinq nœuds de récolte de plus.
- **Trois haltes de plus** : place du bourg, ferme des champs, hameau de la carrière.
- La **carte (L)** et la petite carte montrent tout : toits, routes, pavés, champs, bois, tentes.
- La musique du village joue aussi dans le bourg et le hameau ; la rivière s'entend sur toute sa longueur et au bord du lac.
- Une halte ne reconstruit plus le décor ni ne repeint la carte : elle est plus rapide.
- Sur téléphone : moins d'arbres dans les bois, de plantes et de blé, et le blé se dessine moins loin.

Ce que tu regardes :
- le chemin du village vers le bourg (grand-rue, à l'ouest du pont) ;
- les maisons de près et la place pavée ;
- les champs et la grange au sud du bourg ;
- le lac au sud ;
- la tour et le camp au nord-est ;
- la carte (L) ;
- si le jeu reste fluide (surtout sur le Note8).

Limites :
- rien n'a encore été essayé dans Unity ni sur le Note8 ;
- les bâtiments sont faits par le code, sans modèle 3D : on peut les remplacer par des modèles de l'Asset Store si tu en veux ;
- le bourg n'a pas encore d'habitants (pas de PNJ nouveaux) ;
- on n'entre pas dans les bâtiments : ce sont des obstacles pleins.

### Essai réaliste 8 (28 octobre 2026)

Demande de Damien : « je veux que tu ajoutes les points 1 à 5 » (vrais bâtiments en 3D, villageois, eau, rochers et terrain des exemples de Unity, effets de combat, textures d'ambientCG), « on focus sur le PC pour l'instant ».

**Déjà déposé par Claude dans `Assets/Style`** (non suivi par git) :
- `Sketchfab/Batiments/<Genre>/<Modèle>/` : dix-sept bâtiments de Sketchfab, préparés dans Blender (porte vers +z, base au sol, textures 2k) : huit maisons, l'auberge, la chapelle de Fagnolle, la grange, le puits, trois étals, la tour de Fougères, la tente ;
- `Mixamo/Paysan`, `Mixamo/Garde`, `Mixamo/Sentinelle` : trois villageois et leurs animations (attente, parler, regarder autour, cueillir, marcher) ;
- `AmbientCG/Ground` (terre battue des chemins, terre labourée des champs) et `AmbientCG/Surfaces` (pavés de la place).

**Importés dans Unity** le 28 octobre 2026 (Package Manager → My Assets ; ils restent à la racine de `Assets` et ne sont pas suivis par git) : Viking Village, Terrain Sample, Hit Effects FREE, Magic Effects FREE, Free Slash VFX, Realistic Furry Wolf, Particle Pack ; et l'exemple « Production Ready Shaders » de Shader Graph (Package Manager → In Project → Shader Graph → Samples). À l'import :
- toujours **Skip** quand Unity propose d'installer des dépendances (le Terrain Sample demande HDRP, Free Slash VFX une version plus ancienne d'URP) ;
- dossiers de démo décochés : `Scene Demo` (Hit Effects), `Demo scene` (Magic Effects), `Demo` et `TextMesh Pro` (Free Slash VFX), `TerrainBrushes`, `Textures/Heightmaps` et le profil de HDRP de `Settings` (Terrain Sample, 1,6 Go sinon), `HDRP` et `LRP(Built-in)` (loup), `TextMesh Pro`, `Scenes`, `Settings` (rendus de démonstration), `Shared/Environment`, `Shared/Scripts` et `TutorialInfo` (Particle Pack, qui demandait aussi le nouveau système de saisie : refusé) ;
- Viking Village : ses deux scripts de démonstration (`Scripts/ControlSwitch.cs`, `Scripts/FlyingRigidbodyFirstPersonController.cs`) demandaient le nouveau système de saisie et les Standard Assets et empêchaient Unity de compiler ; ils sont remplacés par des coquilles vides au même nom (le reste du pack ne s'en sert pas pour le jeu).

**Préparer.** Menu **Éclats du Seuil → Préparer l'essai de style** (il mesure les bâtiments, prépare leurs matériaux, range les villageois et choisit les effets), puis Play.

Ce qui change :
- **Bourg** : chaque maison, l'auberge, la chapelle, la grange, le puits, les étals, la tour et les tentes prennent un vrai modèle 3D, porte vers la route, ajusté à son emprise (un peu étiré au besoin, jamais plus d'un quart) ; une maison sur deux en miroir. Un modèle qui laisserait plus de 15 % de vide le long d'un côté de son emprise (le joueur y buterait contre un mur invisible) est tourné d'un quart de tour s'il la remplit mieux ainsi, porte de côté : c'est le cas de l'auberge. Sans modèle d'un genre, ou si le seul modèle serait trop déformé, le bâtiment fait par le code reste.
- **Villageois** (affichage seulement, ils n'arrêtent rien) : au puits un paysan qui parle à un garde, un marchand derrière chaque étal, deux gardes à la porte du moulin, une sentinelle devant l'auberge, des paysans qui cueillent dans les champs, un passant qui va et vient sur la place. Le tiers sur téléphone.
- **Sol** : les chemins en terre battue, les champs en terre labourée (sur ordinateur) ; la place en vieux pavés.
- **Combat** (sur ordinateur) : impacts, critiques, garde, parade (avec l'emblème des Éclats), taille d'épée, sorts, soin, niveau, mort et onde du Gardien prennent les effets des packs. Leur taille, leur rotation et leur durée se règlent dans le catalogue (`Assets/Style/Resources/StyleCatalog`, rubrique « Vfx »). Hit Effects FREE et Magic Effects FREE sont faits pour l'ancien rendu de Unity et sortiraient roses : la préparation passe les matériaux des effets choisis sur les shaders de particules d'URP (texture, couleur, mélange ; comme le convertisseur de Unity).
- **Rochers, plantes et buissons** (point 3) : les rochers de Viking Village, les fougères, herbes, vivaces et buissons du Book of the Dead (dans Viking Village), du Terrain Sample et des Production Ready Shaders passent devant ceux de Poly Haven, qui complètent (chaque exemple donne les siens à tour de rôle). Les menhirs, piliers et buissons secs ne sont pas pris.
- **Terrain** (point 3) : l'herbe, la terre et la roche du sol prennent les couches du Terrain Sample (`Grass_A`, `Muddy`, `Rock`) ; les chemins et les champs gardent ceux d'ambientCG. La teinte de l'herbe du catalogue (`GrassTint`) s'applique toujours : la remettre en blanc si l'herbe sort trop verte.
- **Loup réaliste** (point 6, gratuit) : « Realistic Furry Wolf » (échantillon gratuit de Rip Vertices Studio, ajouté le 28 octobre 2026 à la demande de Damien) remplace le loup stylisé : sa variante pour URP (`Assets/Wolf/URP/Wolf/Prefab/Wolf_URP.prefab`), avec son pelage et ses animations (attente, marche, course, deux morsures, coup reçu, hurlement, mort), passées en « Legacy » par la préparation. S'il regarde de travers, régler `WolfYaw` dans le catalogue. Le sanglier reste celui du pack stylisé (aucun sanglier réaliste gratuit).
- **Forge** (sur ordinateur) : flammes et fumée du « Particle Pack » de Unity (gratuit) dans le foyer ; taille réglable dans le catalogue (« Vfx », `ForgeFire` et `ForgeSmoke`).
- **Eau** (point 3, sur ordinateur) : la rivière prend l'eau de lac des Production Ready Shaders (rides, reflets, lit vu par transparence, couleur de la profondeur), réglable dans son matériau `WaterLake` ; la préparation garde pour cela l'image et la profondeur de la scène dans le rendu de l'ordinateur. Le téléphone garde l'eau « EdS/Eau ».

Ce que tu regardes :
- le bourg de près : maisons, porte côté rue, rien qui flotte ni ne s'enfonce ;
- les villageois (animations, place, aucun dans un mur) ;
- les chemins et les champs ;
- un combat : effets bien placés, à la bonne taille, aucun rose ;
- la rivière, les rochers, les fougères et les buissons, la couleur du sol ;
- si le jeu reste fluide.

Limites :
- rien n'a encore été essayé en jeu (les packs sont importés et le projet compile chez Damien) ;
- l'eau de ruisseau des Production Ready Shaders (qui coule) n'est pas prise : elle suit les coordonnées de texture du maillage, que la surface de la rivière n'a pas ; l'eau de lac ride sans courant ;
- les emprises des maisons (6 à 8 m) sont petites pour les plus belles maisons de Sketchfab (11 à 16 m) : elles servent peu ; on peut agrandir les emprises si tu veux ;
- villageois sans dialogue ni trajet libre (le passant fait l'aller-retour sur la place) ;
- effets des packs sur ordinateur seulement.

### Intérieurs (28 octobre 2026)

Demande de Damien : entrer dans les maisons « comme dans une vraie maison », par une porte qui mène à une pièce à part (« on fait la façon 1 »), avec « de beaux intérieurs bien détaillés ». Fonctionnement : [MONDE.md](MONDE.md#intérieurs).

**Déjà déposé par Claude** (non suivi par git) : `Assets/Style/PolyHaven/Interieurs/<modèle>/`, 46 meubles et objets de Poly Haven en 1k (lit, armoire, commode, tables, bancs, tabourets, chaises, étagères, coffre, caisses, tonneaux, seaux, lanternes, lustres, chandeliers, bougeoirs, vaisselle, bouteilles, jarres, paniers, pommes, oignons, bouclier, épée, marteau, hache, balai, tableau, rouet, porte, statue ; liste dans [CREDITS.md](CREDITS.md)).

**Préparer.** Menu **Éclats du Seuil → Préparer l'essai de style**, puis Play. La préparation range les objets dans le catalogue (rubrique « Furniture » de `Assets/Style/Resources/StyleCatalog`) et leur donne leurs matières (couleur, relief, métal).

Ce que tu fais :
- trois portes au bourg, signalées par un disque doré : l'**auberge**, la **chapelle** et une **maison** ; approche-toi et appuie sur **F** (« F : passer la porte ») ;
- l'écran passe au noir un instant, tu es dans la pièce, devant la porte ; la même touche te ramène au bourg, devant la maison ;
- pas de passage si un ennemi est à moins de 12 m (le message le dit).

Ce que tu regardes :
- l'**auberge** : comptoir garni et aubergiste, quatre tables avec bancs, bougies et vaisselle, cheminée qui brûle, tonneaux, caisses, étagères, escalier, armes et lanternes aux murs, deux clients ;
- la **chapelle** : bancs, autel avec chandeliers, statue, grandes fenêtres et leurs rayons de lumière, lustres, une fidèle ;
- la **maison** : lit, coffre, armoire, rouet, table garnie, cheminée, l'habitant ;
- la lumière (feu et bougies qui vacillent, pièce sombre autour), la taille et le sens des objets (rien qui flotte, rien de travers), les murs qui s'effacent quand ils te cachent.

Réglages : la rotation (`Yaw`) et la taille (`Scale`) de chaque objet dans la rubrique « Furniture » du catalogue (gardées d'une préparation à l'autre) ; la lumière des intérieurs dans `IndoorAmbient`, `IndoorExposure` et `IndoorLights`.

Limites :
- rien n'a encore été essayé en jeu ;
- une pièce n'a qu'un niveau (l'escalier est un décor), les portes ne se ferment pas à clé ;
- les gens des intérieurs ne parlent pas (l'histoire et les métiers restent dehors) ;
- un objet sans modèle (Poly Haven absent) garde une forme simple faite par le code.

### Essai réaliste 9 : interface et caméra (29 octobre 2026)

Demandes de Damien : « la case où il y a les points de vie et l'endurance est trop grande, elle cache le perso, il faut la réduire » ; « je n'aime pas les vues de caméra, il faudrait les retravailler » (« elle bouge mal ») ; style choisi : **World of Warcraft** ; souris : « sans clic comme maintenant mais on peut le changer dans les réglages ».

Ce qui change :
- **Le sceau** de la vie et de l'endurance est réduit d'environ moitié et collé au bas de l'écran ; ses chiffres sont dessinés plus gros pour rester lisibles ; les compétences et les Charges se resserrent autour.
- **La caméra** tourne autour du personnage : la souris l'oriente (gauche-droite) et l'incline (haut-bas) sans changer sa distance ; la molette la rapproche ou l'éloigne, du très près au loin ; **elle ne tourne plus jamais toute seule** (avant, elle suivait la marche du personnage) ; A / E la tournent en douceur ; **C** la remet derrière toi. Par en dessous, elle se rapproche pour ne pas passer sous le sol. Dans une maison, elle reste à 8 m au plus.
- **Réglages** dans la pause (Échap) : souris libre (sans clic, comme avant) ou clic gauche maintenu (comme World of Warcraft), haut-bas inversé, sensibilité de la souris. Ils sont gardés d'une partie à l'autre.

Ce que tu regardes : le personnage bien visible au-dessus du sceau ; la caméra qui répond tout de suite à la souris, sans à-coups ni rotation toute seule ; le zoom ; les réglages de la pause.

Limites : rien n'a encore été essayé en jeu ; la caméra ne butte pas contre les murs (les murs qui cachent le personnage s'effacent, comme avant) ; V1.10 : en fondu transparent).

### Chapitre 1 : l'ouverture et le souvenir jouable (sous-lot V1.9.8, 5 novembre 2026)

PR B du chapitre 1 enrichi ([MONDE.md](MONDE.md#chapitre-1-enrichi--louverture-le-souvenir-jouable-croc-gris-sous-lot-v196)) : la mise en scène est dans `StoryStage.cs` ; le cœur décide de tout (quêtes, scène, combats), Unity ne fait que montrer. Aucun fichier payant : formes, lumières et sons faits par le code.

- **Le réveil** (nouvelle partie) : écran noir, « Éclats du Seuil », « Chapitre 1 · Les traces sous le moulin », la première phrase du journal ; les yeux s'ouvrent (un clignement) ; le personnage est couché sur la place sous une couverture rouge, il se relève (la couverture reste dans l'herbe). Une touche (sauf Échap, qui met en pause) passe l'écran noir. Le réveil ne se joue **qu'une fois** : seulement sur une partie dont le temps du monde n'a jamais avancé (recommandation par défaut, en attendant ton choix) ; dès qu'on a joué et sauvegardé, il ne revient plus. Aucun format de sauvegarde n'a changé.
- **Le tutoriel** : une bulle à gauche pour chaque étape de « Le réveil » (étape n/6, le texte du journal, la touche du PC qui clignote) ; au doigt, le bouton de l'étape (1, Garde, Parade, Potion, Agir) a un halo d'or. « Je sais me battre » chez Odile : **« Quête passée »** s'affiche en grand.
- **Le souvenir proposé** (après la meute) : « Un souvenir remonte » ; **V** ou le bouton : revivre ; **X** ou le bouton : passer (le texte du souvenir reste à l'écran le temps d'être lu). L'Éclat lance une lueur bleue tant que le souvenir attend.
- **Dans le souvenir** : fondu blanc, « Souvenir de la Rupture — Galeran — » ; nuit bleue, brume au sol, couleurs lavées ; la musique se tait, un chœur fait par le code chante. Au fond, la **porte de lumière** (arche blanche et voile), **sept Seuillards** en robe sombre qui se balancent, la **reine Ysolde** (robe sombre, cercle d'or, petit linceul blanc), la **lune** derrière la porte. Le personnage s'appelle « Galeran » au-dessus de sa tête : lance trop haute pour lui, épaulières et plastron neufs (son épée et son écu sont cachés). Après la dernière réponse d'Ysolde : éclair blanc, la porte s'ouvre, des milliers de voix, **la lune se fend**, la musique de combat revient. **L'Écho de la Rupture** est une silhouette de lumière avec un halo et des voix qui tournent. **X** passe le souvenir à tout moment, même pendant un dialogue ; au doigt, un bouton « Passer le souvenir » en haut de l'écran. Une partie reprise dans le souvenir déjà fini (l'Écho vaincu) rentre seule au pré ; le retour attend la fin d'une pause ou d'un écran ouvert.
- **Le retour** : l'Écho vaincu, une colonne de lumière, le blanc monte, puis le pré revient en fondu blanc avec le texte du retour (« La lumière t'entre dans la paume… »).
- **Les Vallons** : Croc-Gris est un loup bien plus grand que les autres (sa seconde forme encore plus) ; quand il devient « enragé », il hurle (son fait par le code), la caméra tremble. Après lui, une **silhouette en manteau sombre** apparaît quelques secondes à la lisière nord du pré, un éclat qui brille à la main, puis disparaît dans un nuage. Au milieu du rond de champignons (près de l'endroit du souvenir), une **pierre plate gravée** de signes bleus qui pulsent. Le **collier de Croc-Gris** porté : un croc d'ivoire sur la poitrine.
- Les **répliques de nuit** des villageois et le reste des dialogues passaient déjà par l'écran du dialogue : rien à changer.

**À vérifier à l'écran, dans cet ordre** (Play dans l'éditeur ; pour une nouvelle partie, renommer d'abord la sauvegarde `sauvegardes/vallons.json` du dossier des données persistantes d'Unity, sous Windows `AppData\LocalLow\…`, pour la retrouver ensuite) :

1. Nouvelle partie : l'écran noir, le titre, la phrase, les yeux qui s'ouvrent, le personnage couché qui se relève (pas enfoncé dans le sol, pas en l'air) ; une touche passe l'écran noir. Quitter après quelques pas, relancer : pas de nouveau réveil.
2. La bulle du tutoriel à chaque étape (parler à Odile, frapper, garder, parer, boire, revenir) ; avec F8 (commandes au doigt), le halo sur le bon bouton.
3. Une seconde nouvelle partie : « Je sais me battre » chez Odile → « Quête passée » en grand, la bulle disparaît.
4. Jusqu'à la meute : la proposition du souvenir ; **X** (passer) une fois sur une copie de sauvegarde, puis **V** (revivre) sur l'autre.
5. Le souvenir : le fondu blanc, le changement d'ambiance (brume, couleurs, silence puis chœur), la porte en arche (pas d'autel ni de chandeliers), les sept Seuillards, Ysolde (pas de paysan à sa place), « Galeran » et sa lance, la lune visible au-dessus du mur du fond.
6. Pendant un dialogue avec Ysolde, X (ou le bouton au doigt) passe bien le souvenir (sur une copie de sauvegarde). Parler à Ysolde jusqu'au bout : l'éclair, la porte qui s'ouvre, la lune fendue, les voix ; puis le combat de l'Écho (silhouette lumineuse, halo).
7. La victoire : le retour au pré en blanc, le texte du retour lisible ; l'ambiance des Vallons revenue (ciel, brume, musique).
8. Croc-Gris : sa taille, son hurlement à la seconde forme ; la silhouette à la lisière après la victoire ; le collier sur la poitrine une fois porté ; la pierre gravée dans le rond de champignons.
9. Sur téléphone si possible : la bulle, le halo, les boutons du souvenir au doigt, la fluidité dans le souvenir.

Limites : rien n'a encore été vu à l'écran (seule la compilation est vérifiée, contre les bouchons de l'API Unity) ; les formes de la reine, des Seuillards, de la silhouette et de Galeran sont faites de primitives (pas de modèle) ; l'arme et l'armure de Galeran sont posées sur le modèle du joueur à des hauteurs moyennes (elles peuvent flotter ou s'enfoncer selon le modèle) ; le voile, la brume et le halo demandent le shader des particules d'URP (sans lui, ils ne sont pas dessinés) ; la lune n'est visible que si la caméra regarde vers le fond de la salle ; le chant et le hurlement sont des sons de synthèse simples ; le texte de repli d'un souvenir passé et celui du retour restent à l'écran de 7 à 18 s (aucune touche pour les fermer) ; les trois textes ajoutés (« Un souvenir remonte », « L'Éclat brûle dans ta paume. », « Déjà aguerri ? … ») sont des propositions, dans `texts/fr.json` (clés `text.ui.souvenir.prompt`, `text.ui.souvenir.call`, `text.ui.reveil.aguerri`, textes 0.11.1) ; les autres libellés de l'interface (« Revivre le souvenir (V) », « Passer (X) », « X : passer le souvenir ») restent écrits dans le code, comme les autres libellés d'Unity.

### Essai V1.10 : maisons du bourg agrandies et les 5 défauts de l'essai 10 (5 novembre 2026)

**Liste de vérification à remplir** : [ESSAI_V1.10.md](ESSAI_V1.10.md) (quoi tester, dans quel ordre, une case et une remarque par point). Avant de jouer : menu **Éclats du Seuil → Préparer et lancer l'essai** (il prépare comme « Préparer l'essai de style », qui règle le rendu et la lumière des intérieurs).

**Outils d'essai (V1.10.1)**, scripts d'éditeur `unity/Assets/EdS/Editor/EdsEssaiTools.cs` (compilés en CI par `EdS.Unity.CompileCheck`) :
- menu **Éclats du Seuil → Préparer et lancer l'essai** : enchaîne la scène de démarrage, « Préparer l'essai de style » (rapport dans la Console au lieu d'une fenêtre), l'ouverture de `Assets/EdS/Scenes/Jeu.unity` puis le mode Jeu. Relançable : jeu déjà lancé = simple message ; scène ouverte modifiée = la question habituelle de Unity (annuler arrête le lancement, la préparation reste faite) ; dossier `Assets/Style` absent = message et arrêt. « Préparer l'essai de style » reste disponible seul.
- menu **Éclats du Seuil → Sauvegarder le dossier des ressources** : zip horodaté de `Assets/Style` (packs, Poly Haven, Mixamo, Sketchfab : non suivi par git) dans `Documents/EdS-Sauvegardes/Style-AAAA-MM-JJ_HH-MM-SS.zip`, hors du projet. Message de réussite ou d'échec (avec la cause). Ne supprime ni n'écrase rien ; un zip interrompu reste en `.incomplet`, à supprimer à la main.
- Logique testée : la sauvegarde et la décision « ouvrir la scène seule ? » (scène active et nombre de scènes ouvertes) sont dans `src/EdS.Runtime/EssaiTools.cs`, testées sur de vrais fichiers temporaires (`V1101OutilsEssaiTests`) ; le script d'éditeur ne fait qu'appeler et afficher.
- Détails : la sauvegarde avance avec une barre de progression, relit le zip (ouverture et nombre d'entrées) avant de dire « Réussi », distingue un zip complet non renommé (`.incomplet` à renommer) d'un zip incomplet, et prévient si Documents est dans OneDrive (le zip partirait dans le nuage). Le lancement survit à un rechargement de domaine (drapeau `SessionState` relu au chargement, mode Jeu lancé une seule fois) ; la scène de démarrage est ouverte seule (les scènes additives sont fermées après la question d'enregistrement de Unity).
- Limites : non essayés sous Unity (compilation seulement en CI) ; le zip peut être gros (plusieurs Go) et Unity se fige pendant sa création.

- **Maisons du bourg de toutes tailles** (décision de Damien : « Je veux des maisons de toutes tailles ») : trois petites (6,5 à 7 m), deux moyennes (10 m) et trois grandes (12 à 14,5 m, dont la Maison de la Guilde), pour que les grandes maisons de Sketchfab (11 à 16 m) aient leur place ; deux mètres au moins entre deux bâtiments ; la porte de la guilde suit sa façade ([MONDE.md](MONDE.md#les-vallons-en-données-sous-lot-124)).
- **Intérieur trop sombre** — cause : URP n'éclaire chaque objet que par ses **4** lumières d'appoint les plus fortes ; une pièce en a jusqu'à 7 et son sol et chaque mur sont un seul objet : trois lumières sur sept ne les éclairaient pas ; la lumière d'ambiance des pièces était aussi très basse. Correction : 8 lumières par objet sur ordinateur (réglage du rendu, fait par la préparation), ambiance, exposition et force des lumières des pièces relevées une fois (catalogue, réglages `IndoorAmbient`, `IndoorExposure`, `IndoorLights`, toujours modifiables).
- **Caméra qui passe dans les poutres** — cause : dans une pièce, la caméra est au-dessus du plafond (8 m du personnage, 22° : 4 à 5 m de haut, poutres à 2,7 m) ; seules les poutres sur la ligne de la tête s'effaçaient, les autres traversaient l'image. Correction : une poutre s'efface en fondu dès que la caméra arrive à sa hauteur, et revient quand la caméra repasse dessous.
- **Murs qui ne passent pas en fondu** (transparence à 25 % : **retenue par Damien** à la relecture de la PR #97) — cause : un décor ne comptait que s'il était entre la caméra et la **tête** ; il s'abaissait (murs) ou disparaissait d'un coup (modèles, toits). Correction : le bas des jambes, le milieu et la tête comptent ; le décor devient **transparent** en un quart de seconde (copie transparente de ses matériaux d'URP), puis redevient plein ; un décor dont le shader ne sait pas être transparent garde l'ancienne coupe.
- **Tache blanche du feu** — cause : les braises de la cheminée brillaient à 3,5 fois le blanc (et la lueur de la forge à 3,2) : le tonemapping et le halo (bloom) les étalaient en une tache blanche ; s'y ajoutait le halo additif du Particle Pack. Correction : braises à 1,4, et les parties « halo » ou « air qui ondule » du feu du pack ne sont plus dessinées (flammes, braises et étincelles restent).
- **Fumée noire de la forge** — cause probable : la fumée du Particle Pack est une fumée **éclairée** (passée sur « Particles/Lit » d'URP) ; au-dessus du foyer, sans lumière qui la touche, elle sortait noire. Correction : une fumée faite par le jeu, non éclairée, gris clair, qui monte et s'efface (aussi sur téléphone).

Limites : rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; les causes de la tache blanche et de la fumée sont déduites du code (le Particle Pack n'est pas dans le dépôt) ; la transparence suppose les matériaux d'URP (« Lit ») : les feuillages et les shaders des packs gardent l'ancienne coupe ; les maisons hors du bourg (champs, hameau) ne changent pas.

### Essai V2.1 : jour et nuit qui changent le monde

**Pour voir la nuit sans l'attendre** (la journée dure 3 h, la nuit commence à 22 h du monde) : hors du mode Jeu, menu **Éclats du Seuil → Essai de la nuit : avancer la partie à 21 h 55** (aussi : « à 4 h 55 » pour l'aube, « à minuit »). Seul le temps du monde avance ; la partie, le personnage et le reste de la sauvegarde ne changent pas. Puis lancer l'aventure. Pour régler la lumière sans changer l'heure du monde : dans le catalogue de style, **Preview Hour** (heure montrée par la lumière, éditeur seulement ; -1 = l'heure du monde).

À vérifier, dans l'ordre :

1. **Crépuscule (21 h 55 → 22 h)** : le ciel et la lumière passent à l'orangé puis au bleu **sans saut** ; les lanternes (à côté des portes, réverbères le long des rues pavées) et les fenêtres des maisons faites par le code s'allument peu à peu (de 20 h à 21 h 30 si l'on part plus tôt).
2. **À 22 h** : deux messages, « La nuit tombe. Des loups hurlent dans le pré… » et « Des lueurs pâles flottent sur les ruines… ». Des loups arrivent dans le pré et la forêt ; des **échos** (loups pâles et bleutés) dans les ruines, au lac et sous la tour. Les sangliers des champs, des vergers et de la plaine s'en vont (sauf s'ils se battent : ils finissent le combat).
3. **La nuit est bleue et lisible, pas noire** : on voit le chemin, les maisons, les loups à 20 m. Trop sombre ou trop clair : curseur **Night Brightness** (clarté de la nuit) du catalogue (1 = prévu). Lanternes trop fortes ou trop faibles : **Lantern Intensity** (force des lanternes).
4. **Villageois** : Odile devant le moulin, Maëlle et l'apprentie, Garin, Bertille, le doyen, l'herboriste du hameau et le fermier **marchent jusqu'à leur porte** ; on leur parle là (F), quêtes et livraisons comprises ; Garin et Bertille disent « Boutique fermée à cette heure ». Aldric, le capitaine, Lison, Sauge (ouverte), le boulanger, Gaspard et Renart (ouvert) restent à leur place. Les passants du bourg rentrent ; un veilleur sur quatre reste dehors.
5. **Ambiance** : feux follets qui flottent sur le lac, la rivière et les ruines ; yeux de hiboux qui clignent dans quelques arbres ; les oiseaux se taisent.
   **Son de la nuit (choix de Damien du 6 novembre 2026 : il dépose lui-même un fichier CC0)** : le mettre dans **`unity/Assets/Style/Ambience/`** sous le nom **`nuit_grillons_hibou.ogg`** (conseillé). Le code reconnaît tout fichier de ce dossier (ou d'un dossier `Ambiance`) dont le nom contient l'un des mots `nuit`, `night`, `grillon`, `cricket`, `hibou` ou `owl` (majuscules, espaces et accents ignorés ; ces mots passent avant « oiseaux », « vent » et « rivière »). Formats : ceux qu'Unity importe comme son, **`.ogg`** (conseillé), `.wav`, `.mp3`, `.aiff`. Une boucle de 1 à 3 minutes suffit. Puis menu **Éclats du Seuil → Préparer l'essai de style** (il range le son dans le catalogue), et jouer : la nappe monte de 19 h à 22 h et s'efface à l'aube. **Sans ce fichier, le jeu marche comme avant** : aucune nappe de nuit (la place est simplement vide, `StyleAudio` saute une ambiance sans son), les oiseaux baissent quand même la nuit. Aucun asset payant.
6. **La garde de nuit d'Aldric** se joue toujours (crépuscule ou nuit, deux loups : les loups de la nuit comptent) ; **Croc-Gris** et le **puits qui murmure** aussi.
7. **Aube (4 h 55 → 5 h)** : les bêtes de la nuit s'en vont dès qu'elles sont au calme, les sangliers reviennent à leur place, les villageois retournent à leur place, l'aube est rose, les lanternes s'éteignent vers 6 h.

Limites : rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; huit lumières de lanternes au plus éclairent vraiment (les plus proches de la caméra ; URP en applique quatre par objet, donc le terrain n'en reçoit que quatre) ; les bâtiments de Sketchfab n'ont que leur lanterne de porte (pas de fenêtres allumées) ; les ciels qui n'ont ni `_Exposure` ni `_Tint` gardent leur clarté.

### Essai V2.2 : météo

Le temps est tiré du **jour du monde** (le même jour a toujours le même temps, avant comme après un chargement) : huit tranches de 3 h du monde (22 min 30 s de jeu chacune), qui changent en douceur (environ 5 min 30 s de jeu). Le jour 0 (début d'une nouvelle partie) est toujours beau. Détail : [MONDE.md](MONDE.md#météo-sous-lot-v22).

**Pour le voir sans l'attendre**, menu **Éclats du Seuil → Essai de la météo : …** (pluie fine, averse, orage, brume du matin, ciel couvert, temps clair) :

- **hors du mode Jeu**, il avance la sauvegarde jusqu'au prochain moment où il fait ce temps (seul le temps du monde avance, comme l'essai de la nuit) : tout est vrai, répliques des villageois comprises. Avant d'écrire, une copie est gardée à côté (`vallons.json.essai-<date-heure>.bak`, jamais écrasée ; l'essai de la nuit fait de même) ;
- **en mode Jeu**, il force seulement l'affichage et le son de pluie (rien ne change dans la partie, les villageois parlent du vrai temps) ; « temps du monde (ne plus forcer) » rend le temps tiré du jour.

À vérifier, dans l'ordre :

1. **Pluie fine puis averse** : des gouttes fines et un peu penchées tombent autour du personnage, des petites éclaboussures au sol ; le ciel se couvre, la lumière baisse et grisonne **sans saut**, la brume se rapproche un peu ; les **flaques** apparaissent peu à peu sur les chemins et **sèchent** en environ un quart d'heure de jeu après la pluie. Les passants du bourg rentrent sous l'averse (un sur trois reste dehors) ; pas sous la pluie fine.
2. **Orage** (rare : l'après-midi ou le soir) : **éclairs** (la scène s'éclaire deux fois très vite, en blanc bleuté), puis le **tonnerre** une demi-seconde à cinq secondes plus tard (plus fort et plus tôt quand l'orage est là) ; les lanternes s'allument en plein jour. Dans une maison : ni gouttes ni éclairs, la pluie sur le toit et le tonnerre assourdi.
3. **Brume du matin** (de 4 h à 9 h 30 du monde, un jour sur trois environ) : une brume blanche et proche (rose à l'aube), qui se lève doucement vers 9 h.
4. **La nuit sous la pluie reste bleue et lisible** (V2.1) : on voit toujours le chemin et les loups.
5. **Répliques** : sous la pluie, Firmin, Renart, Maëlle, Achille, Josse, le capitaine, Aldric, Bertille et Garance parlent du temps ; sous l'orage, Firmin, le capitaine et frère Anselme ; dans la brume, Achille, Josse, Aldric et Sauge (quand ils n'ont pas une quête à dire).
6. **Réglages** : aucun curseur nouveau ; la clarté se règle toujours avec ceux du catalogue (Night Brightness, Exposure…).

**Sons (facultatifs, choix de Damien : il dépose lui-même des fichiers CC0)** : dans **`unity/Assets/Style/Ambience/`**, noms conseillés **`pluie.ogg`** (une boucle de pluie, 1 à 3 minutes) et **`orage_tonnerre.ogg`** (un seul coup de tonnerre, 3 à 8 secondes). Le code reconnaît tout fichier de ce dossier dont le nom contient `pluie`, `rain` ou `averse` (la nappe de pluie, en boucle ; un fichier « thunderstorm_rain » compte comme pluie) et `orage`, `thunder` ou `tonnerre` (le coup de tonnerre, joué une fois après chaque éclair, jamais en boucle). Formats : `.ogg` (conseillé), `.wav`, `.mp3`, `.aiff`. Puis menu **Éclats du Seuil → Préparer l'essai de style**. **Sans ces fichiers, le jeu marche** : une pluie et un tonnerre de synthèse (faits une fois au démarrage) les remplacent. **Déposés chez Damien le 6 novembre 2026** : `pluie.ogg` (BigSoundBank n° 1290) et `orage_tonnerre.ogg` (BigSoundBank n° 3114), tous deux CC0 ([crédits](CREDITS.md)). Les deux suivent le curseur **Ambience Volume** du catalogue (0 : muets), le tonnerre comme la pluie. Déposés aussi le même jour dans **`Assets/Style/Sounds/RPG`** : `anvil_1.ogg` et `anvil_2.ogg` (BigSoundBank n° 3590, « Anvil #2 », CC0, découpés ; reconnus par le mot `anvil` : son de la forge) et `potion_drink.ogg` (n° 3247, « Drink from the gourd #1 », CC0, découpé ; mots `potion`, `drink` : potion bue). Revue Codex de la PR #101 : la colonne de pluie suit la hauteur de la caméra (`WeatherCycle.RainColumn`) : de près comme de 25 m de haut, les gouttes touchent le sol.

Limites : rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; les flaques sont des disques sombres et lisses (aucun reflet calculé : elles prennent le ciel par la réflexion d'URP) ; le sol lui-même ne devient pas brillant ; sans brume au catalogue, celle de la météo part de 30 à 180 m avec la couleur de brume du catalogue ; la pluie ne passe pas sous les toits (les gouttes traversent les auvents).

### Essai V2.4 : animaux paisibles et bêtes qui se battent

Menu **Éclats du Seuil → Essai des animaux : …** (hors du mode Jeu ; seul le temps du monde avance, une copie de la sauvegarde est gardée) : **l'aube des cerfs** (prochaine aube à 5 h 10 sans averse ni orage), **le crépuscule des lièvres** (19 h 10), **la brume (loups gris et échos)** (prochain matin de brume). Le message dit où aller. Liste de vérification : [ESSAI_V1.10.md](ESSAI_V1.10.md#7-les-animaux-v24) ; règles : [MONDE.md](MONDE.md#animaux-paisibles-et-bêtes-qui-se-battent-sous-lot-v24).

- **Cerfs et lièvres** : aucun modèle livré ; ils sont **dessinés en formes simples** (`WildlifeLook`, pièces de `EdS.Runtime.Wildlife`) : corps, cou, tête, bois et queue blanche pour le cerf ; corps rond, longues oreilles et queue blanche pour le lièvre ; les pattes balancent et le corps rebondit à la course (le lièvre bondit), immobiles à l'arrêt ; couchés sur le flanc à leur mort. Un matériau par couleur, partagé. Ils se tournent dans le sens de leur course (le moteur les oriente).
- **Loups gris de la brume** : le modèle du loup ; **échos de la brume** : pâles comme ceux de la nuit (`loup_echo5`, `loup_echo6`).
- **Noms affichés** : « Cerf », « Lièvre », « Loup gris ».

Limites : rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; pas de son propre aux cerfs ni aux lièvres ; un modèle Mixamo ou de l'Asset Store pourra remplacer les formes simples plus tard (dossier `Cerf`, `Lievre`).

## 8. En cas de problème

- **Objets roses** : le rendu URP n'est pas configuré. Menu **Éclats du Seuil → Configurer le rendu URP**, puis relancer Play.
- **Cadre rouge « Impossible de charger le jeu »** : le dossier `unity` a été déplacé hors du dépôt. Il doit rester à côté de `data/` et `scenarios/`.
- **Erreurs rouges dans la Console** : faire une capture de la Console et l'envoyer à Claude.
- **Fichiers modifiés dans GitHub Desktop** après une ouverture (`ProjectVersion.txt` notamment) : c'est Unity qui les réécrit ; ne rien valider (*Commit*). Si un *Pull* refuse à cause d'eux : clic droit sur le fichier → *Discard changes*, puis *Pull*.
- Pour remettre le projet à zéro : fermer Unity et supprimer le dossier `unity/Library` (il est recréé à l'ouverture).
- **Réseau : « connexion impossible »** : vérifier l'adresse, que l'autre PC héberge bien (salle d'attente affichée) et que le pare-feu Windows autorise Unity (ou le jeu construit) sur les réseaux privés, port 7777.
- **Réseau : « désynchronisation persistante »** : ne devrait jamais arriver ; faire une capture et l'envoyer à Claude (c'est exactement ce que le lot 10 doit détecter).
- **Réseau : « protocole 1 non pris en charge »** (ou 2, ou 3) : les deux PC n'ont pas la même version du projet ; faire un *Pull* sur les deux (sous-lot 11.7 : protocole 2 ; sous-lot 11.9 : protocole 3 ; sous-lot V1.8 : protocole 4).
- **Téléphone non proposé dans *Run Device*** : câble de données (pas seulement de charge), débogage USB activé, autorisation acceptée sur le téléphone ; débrancher, rebrancher, *Refresh*.
- **Téléphone : écran bloqué sur « Préparation des données du jeu… » avec un sol rose** (premier essai du 22 octobre 2026) : le jeu construit n'emportait pas son shader de couleur. Corrigé : chaque construction emporte un matériau modèle (`EdsBuildShaders`) ; faire un *Pull*, puis **Build And Run** à nouveau. Si un cadre rouge « Démarrage du jeu impossible » s'affiche, envoyer une photo à Claude.
- **Essai 6 : textes de l'interface un peu flous ou sans ombre** : les ressources de TextMeshPro ne sont pas encore importées (message « ressources de TextMeshPro absentes » dans la Console). Menu **Éclats du Seuil → Préparer l'interface (TextMeshPro)**, puis relancer Play.
- **Téléphone : « Impossible de préparer les données du jeu »** : le jeu a été construit sans ses données (projet Unity déplacé hors du dépôt) ; reconstruire depuis le projet du dépôt.

## Ce qui est provisoire

Graphismes en formes simples, écrans en IMGUI (l'interface en jeu est en uGUI depuis l'essai réaliste 6), une seule touche par compétence. Le déplacement n'est simulé que dans la scène des Vallons (les autres séances et la coopération en réseau n'ont pas de décor ; le réseau dans l'espace vient au 11.7). Détail : [LIMITES_LOT_9.md](LIMITES_LOT_9.md#sous-lot-93--projet-unity) et [LIMITES_LOT_11.md](LIMITES_LOT_11.md#sous-lot-116--scène-de-démonstration-des-vallons).
