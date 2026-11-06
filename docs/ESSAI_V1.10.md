# Essai V1.10 — liste de vérification pour Damien

But : jouer la vague 1 de bout en bout sous Unity, vérifier les **5 défauts de l'essai 10** et les **maisons agrandies**, et noter ce qui ne va pas. Une ligne = un point à regarder. Coche la case `[x]` quand c'est bon ; sinon laisse `[ ]` et écris ce que tu vois dans **Remarque** (une capture d'écran aide beaucoup : touche `Impr. écran`, puis colle-la dans la conversation).

Comment noter : ouvre ce fichier (dans GitHub, bouton crayon, ou dans un éditeur de texte), remplis la colonne « Remarque », puis envoie-le à Claude (ou colle les lignes qui ont un problème).

Touches (clavier AZERTY) : **Z Q S D** marcher · **souris** tourner la caméra · **molette** zoom · **C** caméra derrière toi · **F** parler / passer une porte / récolter · **clic droit** se garder · **Espace** parer · **1 à 6** compétences · **P** boire une potion · **I** personnage et sac · **J** journal des quêtes · **K** forger · **L** carte · **B** compétences · **Échap** pause et réglages · **F6** sauvegarder.

## 0. Préparer (une seule fois)

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 0.1 | Dans GitHub Desktop, prendre la branche `v1.10-bourg-essai` (ou `main` une fois fusionnée) | Unity recharge les scripts, aucune erreur rouge dans la Console | [ ] | |
| 0.2 | (Conseil, avant tout) menu **Éclats du Seuil → Sauvegarder le dossier des ressources** (si ton dossier Documents est synchronisé par OneDrive, le zip part aussi dans le nuage : le message te prévient) | Une barre avance, puis un message dit « Réussi » et donne l'emplacement du zip (dans Documents, dossier `EdS-Sauvegardes`) ; rien n'est supprimé | [ ] | |
| 0.3 | Pour une **nouvelle partie** : renommer `sauvegardes/vallons.json` (dossier `AppData\LocalLow\…` du jeu) en `vallons_avant.json` | Le fichier n'est plus là sous son nom | [ ] | |
| 0.4 | Menu **Éclats du Seuil → Préparer et lancer l'essai** (une seule action : il prépare, ouvre la scène et lance le jeu) | Unity travaille un moment, puis le jeu démarre tout seul ; la Console (onglet en bas) contient un message commençant par « Essai de style » (le message « Lumière des intérieurs remise… » n'apparaît qu'à la toute première préparation) | [ ] | |
| 0.5 | Secours : si le jeu ne démarre pas tout seul, clique **Play** (▶ en haut au centre) | Le jeu démarre sur l'écran noir du réveil | [ ] | |

## 1. Chapitre 1 enrichi : le réveil et le tutoriel

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 1.1 | Attendre, ou appuyer sur une touche (pas Échap) | Écran noir, « Éclats du Seuil », « Chapitre 1 · Les traces sous le moulin », la phrase du journal, les yeux qui s'ouvrent | [ ] | |
| 1.2 | Regarder ton personnage | Couché sur la place sous une couverture rouge, il se relève ; ni enfoncé dans le sol, ni en l'air | [ ] | |
| 1.3 | Suivre la bulle à gauche : parler à Odile (**F**) | Bulle « étape 1/6 », la touche qui clignote | [ ] | |
| 1.4 | Frapper (**1**), se garder (**clic droit**), parer (**Espace** juste avant le coup), boire (**P**), revenir à Odile | Une bulle par étape, le bon texte, la bonne touche | [ ] | |
| 1.5 | Quitter (Échap puis stop), relancer Play | **Pas** de nouveau réveil | [ ] | |
| 1.6 | Sur une autre nouvelle partie (refaire 0.3, puis relancer 0.4) : chez Odile, choisir « **Je sais me battre** » | « **Quête passée** » en grand, la bulle disparaît, la mission continue | [ ] | |

## 2. Le souvenir, Croc-Gris et son collier

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 2.1 | Battre le loup derrière le moulin et la meute du pré | « Un souvenir remonte » ; **V** revivre, **X** passer | [ ] | |
| 2.2 | (Sur une copie de sauvegarde) **X** | Le texte du souvenir reste le temps d'être lu | [ ] | |
| 2.3 | **V** : entrer dans le souvenir | Fondu blanc, « Souvenir de la Rupture — Galeran — », nuit bleue, brume, couleurs lavées, silence puis chœur | [ ] | |
| 2.4 | Regarder la salle | La porte de lumière en arche, sept Seuillards, la reine Ysolde, « Galeran » et sa lance, la lune au fond | [ ] | |
| 2.5 | Parler à Ysolde jusqu'au bout (**F**, puis **1 à 9** pour répondre) | L'éclair, la porte qui s'ouvre, la lune fendue, les voix, puis le combat de l'Écho | [ ] | |
| 2.6 | Gagner | Retour au pré en blanc, le texte du retour lisible, ciel et musique des Vallons revenus | [ ] | |
| 2.7 | Croc-Gris (au pré) | Bien plus grand que les autres loups ; à sa 2e forme il hurle et la caméra tremble | [ ] | |
| 2.8 | Après la victoire | Une silhouette sombre quelques secondes à la lisière nord, puis elle disparaît | [ ] | |
| 2.9 | **I** : porter le **collier de Croc-Gris** | Un croc d'ivoire sur la poitrine ; ses chiffres (30 PV, 4 de puissance) dans la fiche | [ ] | |
| 2.10 | Aller au rond de champignons du pré | Une pierre plate gravée de signes bleus qui pulsent | [ ] | |

## 3. Les 5 quêtes du village, la nuit, Odile et Aldric

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 3.1 | **Le chat de Rosalie** (Rosalie, dans la maison du bourg : disque doré, **F**) : Firmin, puis le moulin, puis rendre le chat | Le chat fixe la vieille porte (indice) ; la quête se finit, 40 cuivre | [ ] | |
| 3.2 | **Le puits du moulin** (Firmin) : tuer le loup du puits, puiser, rapporter le seau | L'eau tiède qui frémit (indice) ; 50 cuivre | [ ] | |
| 3.3 | **Les champignons de Sauge** (Sauge, l'herboriste de la place du bourg ; porte du moulin ouverte) : 4 chapeaux-de-lune (**F** sur les champignons du pré) | La terre tiède au milieu du rond (indice) | [ ] | |
| 3.4 | **Le premier contrat** (capitaine Bérenger, niveau 5) : 3 loups, le sanglier des ruines, la paie | Il t'envoie à Dame Brunehilde, à la guilde du bourg ; récompense au choix | [ ] | |
| 3.5 | Attendre **le soir** (vers 19 h du monde : environ 1 h 20 de jeu après le début) | La lumière baisse, la nuit tombe | [ ] | |
| 3.6 | **La garde de nuit** (Aldric, au gué) le soir : veiller, 2 loups, puis **le choix** (taire ou dire qu'il a reculé) | La quête démarre seulement le soir ou la nuit ; le choix compte | [ ] | |
| 3.7 | La nuit, parler à Gaspard, Maëlle, Firmin, Aldric, Rosalie, Sauge, Bertille, le capitaine | Une réplique de nuit chez chacun ; aucune quête cachée par la nuit | [ ] | |
| 3.8 | Chez le capitaine : « Et Aldric, capitaine ? » | Sa réponse suit ton choix de 3.6 | [ ] | |
| 3.9 | Chez **Odile** : si tu l'as vexée, « Je vous ai mal parlé, Odile. Pardon. » ; en confiance, « Comment va votre roue ? » | Elle pardonne une fois ; la roue qui grince la nuit (indice) | [ ] | |

## 4. Les 5 défauts de l'essai 10 (corrigés en V1.10)

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 4.1 | Entrer dans l'auberge, la chapelle, la maison et la guilde (disque doré, **F**) | **Intérieur assez clair** : sol, murs et meubles bien visibles autour du feu et des bougies | [ ] | |
| 4.2 | Dans une pièce : tourner la caméra (souris), zoomer et dézoomer (molette) | **La caméra ne passe plus dans les poutres** : quand elle monte à leur hauteur, elles s'effacent en douceur ; de près, en vue basse, elles reviennent | [ ] | |
| 4.3 | Dans une pièce, puis dehors derrière une maison : te mettre derrière un mur | **Le mur devient transparent** en fondu (à 25 %, ton choix : on le devine, on te voit), puis redevient plein quand tu t'éloignes | [ ] | |
| 4.4 | Regarder la cheminée de l'auberge et de la maison | **Plus de tache blanche** : braises orange, flammes, pas de grand disque blanc | [ ] | |
| 4.5 | Aller à la forge de Maëlle (village) | **Fumée claire** (gris pâle) qui monte et s'efface ; **plus de fumée noire** ; flammes sans tache blanche | [ ] | |
| 4.6 | Si un intérieur reste trop sombre ou trop clair | Noter la pièce ; réglage : catalogue `Assets/Style/Resources/StyleCatalog`, champs `IndoorAmbient`, `IndoorExposure`, `IndoorLights` | [ ] | |

## 5. Les maisons du bourg agrandies

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 5.1 | Aller au bourg (carte **L**, voyage rapide « Place du Bourg ») et faire le tour | Des maisons de toutes tailles : trois petites, deux moyennes, trois grandes (la Maison de la Guilde, celle du boulanger au nord de la place, celle au sud de la place) | [ ] | |
| 5.2 | Longer chaque maison | Rien ne flotte ni ne s'enfonce, pas de mur invisible loin du mur visible | [ ] | |
| 5.3 | Regarder les portes | Chaque porte tournée vers la rue ; disques dorés de la maison et de la guilde devant la porte | [ ] | |
| 5.4 | Parler à Firmin, Bertille l'épicière, Garin l'armurier, Sauge l'herboriste, Renart, Lison, Mahaut ; puis Rosalie dans la maison du bourg (porte dorée) | Personne dans un mur, chacun accessible (**F**) | [ ] | |
| 5.5 | Passer entre les maisons et sur les chemins | On passe partout (au moins 2 m entre deux bâtiments), aucun chemin sous une maison | [ ] | |

## 6. La suite : chapitres 2 à 5

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 6.1 | **Chapitre 2 « Le bourg d'Aube »** (niveaux 5 à 10) : Sœur Albe, Renart, les vols d'éclats, l'inscription à la guilde | Dialogues lisibles, quêtes au journal (**J**), guilde (Dame Brunehilde) | [ ] | |
| 6.2 | Quêtes du chapitre 2 : la dette à l'auberge, les cloches, la dernière lanterne, le puits qui murmure, l'apprentie de Maëlle, les herbes pour la chapelle | Chacune se termine, récompense reçue | [ ] | |
| 6.3 | **Chapitre 3 « Les champs et le hameau »** (10 à 15) : attaque du hameau, piste jusqu'au lac | Bandits, étal de Renart | [ ] | |
| 6.4 | Quêtes du chapitre 3 : sangliers des champs, lettre au fond du lac, vergers pillés, potions du hameau, pont des Vallons | Chacune se termine | [ ] | |
| 6.5 | **Chapitre 4 « La tour et le camp »** (15 à 20) : la tour silencieuse, le chef des bandits, **le choix** (l'épargner ou non) | Le choix est retenu ensuite | [ ] | |
| 6.6 | Quêtes du chapitre 4 : la tour silencieuse, le déserteur, la prime du Borgne, le marchand qui en savait trop | Chacune se termine | [ ] | |
| 6.7 | **Chapitre 5 « Le Seuil »** (20 à 25) : le rôdeur, le Gardien et sa forme éveillée, retour chez Odile, la voix de l'Envers | Le combat du Gardien est jouable, la fin s'affiche | [ ] | |
| 6.8 | Quêtes du chapitre 5 : le loup blanc, les pierres gravées | Chacune se termine | [ ] | |
| 6.9 | Tout au long : sauvegarde (**F6**, puis relancer Play) | On repart où on était | [ ] | |
| 6.10 | Ton avis général : durée, difficulté, ce qui manque, ce que tu changerais | — | [ ] | |

## 7. V2.2.1 : Pain-d'Épice suit le joueur (compagnon d'affichage)

Quand **Pain-d'Épice, le chat de Rosalie** est dans le sac (quête du chapitre 1), un chat roux suit le personnage à l'écran. Affichage seulement : il ne combat pas, n'est pas sauvegardé (c'est l'objet qui l'est), aucun format ne change. Pour le voir sans faire la quête : lancer le jeu (Play) et l'aventure, puis menu **Éclats du Seuil → Essai du compagnon : montrer Pain-d'Épice** (« ne plus forcer » pour le voir partir ; le sac ne change pas).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 7.1 | Menu « montrer Pain-d'Épice » | Le chat apparaît derrière toi, un peu sur le côté, en grandissant avec un petit rebond et un miaulement | [ ] | |
| 7.2 | Marche, cours, tourne autour d'une maison | Il suit **ton chemin** à environ 1,7 m (il contourne la maison comme toi), sans à-coup ; pattes qui marchent, queue dressée au bout recourbé | [ ] | |
| 7.3 | Arrête-toi | Il freine, s'assoit tourné vers toi, la queue autour des pattes ; puis toilette, regard qui fait le tour, étirement, miaulement ; un ronronnement (une fois) s'il est près de toi | [ ] | |
| 7.4 | Reste 30 s sans bouger | Il se couche en boule ; il se relève dès que tu repars | [ ] | |
| 7.5 | Passe une porte, fais un voyage, la nuit (**Essai de la nuit**) | Il reparaît derrière toi ; la nuit, ses yeux luisent un peu | [ ] | |
| 7.6 | Combat (un loup) | Il ne s'approche pas à moins de 6 m de toi (il s'arrête où il est) ; rien ne le touche, il ne gêne ni tes coups ni la caméra | [ ] | |
| 7.7 | Menu « ne plus forcer » (ou rends le chat à Rosalie) | Il rapetisse sur place et disparaît | [ ] | |
| 7.8 | Ton avis : taille, couleur, vitesse, distance, sons | — | [ ] | |

**Sons (facultatifs, CC0 déposés par toi)** : dans `unity/Assets/Style/Sounds/` un fichier dont le nom contient `miaou` ou `meow` (miaulement) et un autre avec `ronron` ou `purr` (ronronnement), puis **Préparer l'essai de style**. Sans eux, un miaulement et un ronronnement faits par le code. Limites : formes simples (pas de modèle 3D) ; rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; en reparaissant derrière toi après une porte, il peut se trouver un instant contre un mur (il n'a pas de collision).
