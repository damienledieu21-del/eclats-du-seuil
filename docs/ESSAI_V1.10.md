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
| 7.6 | Combat (un loup) | Il s'écarte en trottinant jusqu'à environ 6 m, puis s'assoit ; rien ne le touche, il ne gêne ni tes coups ni la caméra | [ ] | |
| 7.7 | Menu « ne plus forcer » (ou rends le chat à Rosalie) | Il rapetisse sur place et disparaît | [ ] | |
| 7.8 | Ton avis : taille, couleur, vitesse, distance, sons | — | [ ] | |

**Sons (facultatifs, CC0 déposés par toi)** : dans `unity/Assets/Style/Sounds/` un fichier dont le nom contient `miaou` ou `meow` (miaulement) et un autre avec `ronron` ou `purr` (ronronnement), puis **Préparer l'essai de style**. Sans eux, un miaulement et un ronronnement faits par le code. Limites : formes simples (pas de modèle 3D) ; rien n'a été vu à l'écran (compilation contre les bouchons de l'API Unity seulement) ; en reparaissant derrière toi après une porte, il peut se trouver un instant contre un mur (il n'a pas de collision).

## 8. La journée du village (V2.3)

Menus **Éclats du Seuil → Essai de la journée du village : …**, hors du mode Jeu (détail : [UNITY.md](UNITY.md#essai-v23--la-journée-du-village)).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 8.1 | Menu « 12 h 15, midi », puis jouer | Firmin vend son pain sur la place, Garin et le capitaine mangent dans la salle de l'auberge (V2.3.1) ; sous leur nom, ce qu'ils font | [ ] | |
| 8.2 | À l'étal de Garin, touche **F** | Un message dit qu'il est à la taverne ; dans la salle de l'auberge, on lui achète | [ ] | |
| 8.3 | Menu « 19 h 45, la taverne » ; parler à Firmin, Maëlle, Aldric (s'il y est encore), Renart, puis à Mathurin dans l'auberge | Chacun a une réplique du soir ; depuis V2.3.1, ils sont dans la salle de l'auberge (Aldric seul sur la terrasse) et Mathurin parle d'eux | [ ] | |
| 8.4 | Menu « 5 h 30, l'aube » ; « 14 h 30 » | Firmin au fournil (sa réplique de l'aube) ; l'après-midi, Firmin fait la sieste devant chez lui | [ ] | |
| 8.5 | Menu « la prochaine averse » | Ceux qui sont dehors vont s'abriter et le disent | [ ] | |
| 8.6 | Regarder les passants du bourg un moment | Certains vont et viennent (puits, maison, auberge), puis reprennent leur place | [ ] | |

## 9. Les animaux (V2.4)

Menu **Éclats du Seuil → Essai des animaux : …**, **hors du mode Jeu** (il avance la partie jusqu'au bon moment ; une copie de la sauvegarde est gardée, comme pour l'essai de la nuit). Détail : [MONDE.md](MONDE.md#animaux-paisibles-et-bêtes-qui-se-battent-sous-lot-v24).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 9.1 | « l'aube des cerfs », puis Play ; aller au sud du **lac des Aubes** (voyage rapide) | Trois **cerfs** fauves avec leurs bois (formes simples) ; message « Des cerfs sortent du bois… » si tu arrives avant eux | [ ] | |
| 9.2 | T'approcher à moins de 9 m d'un cerf | **Toute la harde détale** au galop (pattes qui battent), s'arrête à 14 m et te regarde ; tu ne peux pas les rattraper (ils courent plus vite que toi) | [ ] | |
| 9.3 | Passer la porte du moulin, aller à la **lisière ouest de la forêt des Loups** (trois autres cerfs) ; les pousser vers les loups | Un cerf qui passe près d'un loup le fait **chasser** : le loup le poursuit (il ne le rattrape pas), puis lâche | [ ] | |
| 9.4 | « le crépuscule des lièvres », puis Play ; aller aux **champs du sud** | Des **lièvres** gris (longues oreilles) qui bondissent quand on approche (6 m) | [ ] | |
| 9.5 | « la brume (loups gris et échos) », puis Play | Message « La brume monte de la rivière… » ; **trois loups gris** au nord des champs du sud ; deux **échos** pâles (lac et ruines) | [ ] | |
| 9.6 | Attirer un loup gris jusqu'aux **sangliers** des champs (à 15 m d'eux), puis reculer | **Loup et sanglier se battent** : morsures, charges ; tu peux t'en mêler. Une bête tuée par une autre ne te donne ni expérience ni butin | [ ] | |
| 9.7 | Attendre que la brume se lève (vers 9 h du monde) | Les loups gris et les échos s'en vont dès qu'ils ne se battent plus | [ ] | |
| 9.8 | Une **averse** de jour (essai de la météo) | Les cerfs ne sont plus là (ils s'abritent) ; ils reviennent après la pluie | [ ] | |
| 9.9 | Ton avis : nombre de bêtes, vitesse de fuite, distance (9 m), loups gris le matin dès le niveau 1 | — | [ ] | |

## 10. Les événements du monde (V2.5)

Menu **Éclats du Seuil → Essai des événements : …**, **hors du mode Jeu** (il avance la partie jusqu'à 5 minutes du monde avant le prochain événement ; une copie de la sauvegarde est gardée). Détail : [MONDE.md](MONDE.md#événements-du-monde-sous-lot-v25).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 10.1 | Avec un personnage **de niveau 6 ou plus** (en dessous, rien ne se passe : choix du 7 novembre), « l'attaque du hameau maintenant », puis Play ; voyage rapide au **hameau de la carrière** | En haut, « En cours : L'attaque du hameau » (ou « Bientôt : … ») ; l'annonce (« Des feux s'allument sur la crête… » ou un hurlement) | [ ] | |
| 10.2 | Attendre quelques secondes | **La cloche** sonne trois coups ; message « La cloche du hameau sonne à toute volée ! » ; trois bandits (ou trois loups) entrent par l'est ; **Josse et Aubine** courent au pied de la carrière (étiquette « à l'abri au pied de la carrière ») | [ ] | |
| 10.3 | Vaincre les assaillants | « Le hameau de la carrière est sauf… », puis la bourse de Josse : **400 points, 3 argent, deux remèdes de l'aube** ; Josse et Aubine reviennent | [ ] | |
| 10.4 | Parler à Josse, puis à Aubine | Leur **remerciement** (une seule fois chacun) | [ ] | |
| 10.5 | Relancer l'essai, puis **ne pas** aller au hameau ; attendre la fin (4 h du monde, environ 30 min de jeu) ou faire halte longtemps ailleurs | « Les bandits repartent… une grange fume » (ou « Pendant que tu étais ailleurs… ») ; au hameau, **fumée et caisses brisées** pendant une journée du monde ; Josse te le reproche | [ ] | |
| 10.6 | Défendre une seconde fois | Des remerciements, **pas** de seconde récompense | [ ] | |
| 10.7 | « ouvrir une faille du Seuil », puis Play ; aller où le dit le message (ruines, lac ou pré) | Une **faille** violette et pâle (éclats qui tournent, lueurs qui montent, lumière), un son de cloche grave ; trois **échos** en sortent, puis d'autres toutes les 30 s (trois vagues) | [ ] | |
| 10.8 | Approcher de la faille avec des échos debout, appuyer sur F | « Des échos sont encore debout… » | [ ] | |
| 10.9 | Vaincre les échos, puis F près de la faille (« F : refermer la faille ») | La faille se referme ; récompense : 450 points, 2 argent 50, **un éclat de Seuil** ; plus de vague | [ ] | |
| 10.10 | « la chasse de l'aube », puis Play ; aller aux **champs du sud** | Deux loups sortent du bois et attaquent un sanglier ; personne ne meurt (une bête n'en tue pas une autre) ; ils repartent après une heure du monde | [ ] | |
| 10.11 | Ton avis : fréquence (un soir sur deux, une faille tous les trois jours), annonces, difficulté (les assaillants ont le niveau du hameau), récompenses, cloche | — | [ ] | |

## 11. Le Seuil qui grandit (V2.6)

Menu **Éclats du Seuil → Essai du Seuil qui grandit : …**, **hors du mode Jeu** (il écrit une cicatrice dans la sauvegarde, avance les jours du monde ou efface les cicatrices ; une copie de la sauvegarde est gardée à chaque fois). Pour purifier, il faut un **éclat de Seuil** (Renart, la guilde, ou la première faille refermée). Détail : [MONDE.md](MONDE.md#le-seuil-qui-grandit-sous-lot-v26).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 11.1 | « une cicatrice aux ruines (palier 1) », puis Play ; aller aux **ruines** (près du camp de la carrière) | Un rond d'**herbe pâle** au sol (8 m), un cœur gris, trois **pointes sombres**, des grains de **brume froide** qui montent ; en haut, « Cicatrice du Seuil · palier 1 sur 4 — l'herbe pâlit » quand on est dedans | [ ] | |
| 11.2 | Marcher du bord vers le cœur | Les **couleurs se fanent** peu à peu, la lumière devient froide, la brume se rapproche ; elles reviennent en sortant | [ ] | |
| 11.3 | Arrêter, « avancer de 2 jours du monde », Play | La cicatrice a **grandi** (palier 2, 12 m, plus de pointes) ; message « Des échos se lèvent autour de la cicatrice… » ; **un écho rôdeur**, un peu plus fort qu'un écho ordinaire | [ ] | |
| 11.4 | Approcher du cœur avec l'écho debout, appuyer sur F (« F : purifier la cicatrice ») | « Des échos rôdent encore… » ; sans éclat : « Il te faut un éclat de Seuil… » | [ ] | |
| 11.5 | Vaincre l'écho, puis F au cœur avec un éclat de Seuil | « Tu poses l'éclat de Seuil au cœur… » ; **200 points** ; la cicatrice disparaît, les couleurs reviennent ; l'écho ne revient plus | [ ] | |
| 11.6 | « une cicatrice aux ruines (palier 3, échos rôdeurs) », puis « avancer de 6 jours » | Palier 4 sur 4 (« le Seuil respire »), 20 m, trois échos rôdeurs à 145 %, couleurs presque grises au cœur | [ ] | |
| 11.7 | Essai des événements : « ouvrir une faille du Seuil » sur une cicatrice, la refermer | Échos de la faille plus forts ; une fois refermée, « la cicatrice recule » (un palier de moins) | [ ] | |
| 11.8 | Laisser une faille ouverte (ne pas y aller jusqu'à la fin) | « …une cicatrice froide reste dans l'air », puis « La faille s'est éteinte…, mais elle a laissé une cicatrice » | [ ] | |
| 11.9 | Parler à **Sauge** (après une cicatrice), à **Sœur Albe** (après des échos rôdeurs), à **Firmin** (après une purification) | Une réplique chacun, une seule fois | [ ] | |
| 11.10 | « effacer toutes les cicatrices » | Plus rien aux ruines, au lac ni dans le pré | [ ] | |
| 11.11 | Ton avis : vitesse (un palier tous les 2 jours), force des échos (+15 % par palier), nombre de rôdeurs, prix de la purification (un éclat), couleurs | — | [ ] | |
## 12. Les bêtes qui se promènent (V2.4.1)

(Le § 11 est réservé au Seuil qui grandit, V2.6.) Menu **Éclats du Seuil → Essai des bêtes qui se promènent : …**, **hors du mode Jeu** (il avance la partie au bon moment ; une copie de la sauvegarde est gardée). Détail : [MONDE.md](MONDE.md#bêtes-qui-se-promènent-sous-lot-v241).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 12.1 | « le matin (harde, meute, sangliers) », puis Play ; aller au sud du **lac des Aubes** et rester à distance (plus de 10 m) | Les trois **cerfs broutent tête basse** (petits coups de mâchoire), relèvent la tête de temps en temps, puis font quelques **pas lents** vers un autre coin d'herbe, **groupés autour du premier** | [ ] | |
| 12.2 | T'approcher à moins de 9 m, puis repartir loin (plus de 14 m) et attendre | La harde **détale ensemble** comme avant ; environ **15 s** après ton départ, elle **revient au pas** brouter chez elle | [ ] | |
| 12.3 | Passer la porte du moulin, regarder les loups de la **lisière de la forêt** et ceux du **nord** (à distance) | Les loups **patrouillent en file**, le chef devant, sur un petit circuit, avec de courtes haltes ; ils ne viennent pas vers toi tant que tu restes à plus de 7 m | [ ] | |
| 12.4 | Aller aux **champs du sud** et à l'est (sangliers) | Les **sangliers fouillent** : longues pauses groin au sol, quelques pas, puis encore | [ ] | |
| 12.5 | « le crépuscule (lièvres) », puis Play ; aller aux champs du sud et aux vergers | Les **lièvres sautillent par à-coups** (petits bonds, arrêts tapis), se dressent aux aguets entre deux ; ils fuient toujours à 6 m | [ ] | |
| 12.6 | Rester une heure du monde au **village** et au **camp de la carrière** | Aucune bête ne vient d'elle-même jusqu'à toi ; aucune bagarre entre bêtes qui se promènent | [ ] | |
| 12.7 | Ton avis : allures (cerf lent, lièvre vif), pauses, taille des aires (4 m pour un cerf, 3 m pour un loup), 15 s de calme, la meute de Croc-Gris qui reste au pré | — | [ ] | |

## 13. Les PNJ qui changent de lieu (V2.3.1)

Menu **Éclats du Seuil → Essai des PNJ qui changent de lieu : …**, **hors du mode Jeu** (il avance la partie jusqu'à cinq minutes avant le départ ; une copie de la sauvegarde est gardée). Détail : [MONDE.md](MONDE.md#pnj-qui-changent-de-lieu-sous-lot-v231).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 13.1 | « 6 h 25, Rosalie va prier », puis Play ; entrer chez Rosalie, attendre 6 h 30 | Rosalie **marche jusqu'à sa porte et disparaît** ; dans la chapelle, elle est assise sur un banc (« à la prière du matin ») ; à 7 h 30 elle rentre | [ ] | |
| 13.2 | « 11 h 55, le puits et la salle », puis Play ; aller au puits du bourg, puis entrer dans l'auberge | Rosalie au puits avec Bertille et Renart ; dans la salle, **Garin, le capitaine et Aubin** à table ; on achète à Garin dans la salle | [ ] | |
| 13.3 | « 15 h 55, Mathurin sort fumer », Play, rester dans l'auberge | Mathurin marche jusqu'à la porte et disparaît ; dehors, il est sous l'auvent (« sur la terrasse, sa pipe à la main »), avec sa réplique | [ ] | |
| 13.4 | « 15 h 25, la tournée de Frère Anselme », Play ; aller sur la route du lac | Anselme sort de la chapelle et rejoint la veuve ; à 16 h 30 il entre dans l'auberge (« pour une bénédiction ») ; Sœur Albe et la veuve en parlent | [ ] | |
| 13.5 | « 18 h 25, la salle se remplit », Play, rester dans la salle | Firmin, Renart, le fermier, Maëlle, Bertille **apparaissent à la porte** et vont s'asseoir ; à 20 h 30 le capitaine ; à 21 h Mathurin sort fumer | [ ] | |
| 13.6 | Parler à un PNJ juste avant son départ et laisser le dialogue ouvert | Il **finit la conversation** (il ne part qu'à la fermeture du dialogue) | [ ] | |
| 13.7 | Suivre une quête de Rosalie ou de Mathurin quand il est ailleurs | L'étoile de la carte mène à la porte de la zone où il est ; on lui parle là-bas | [ ] | |
| 13.8 | Ton avis : qui entre dans la salle, Aldric seul sur la terrasse, horaires, répliques | — | [ ] | |

## 14. Les suites du Seuil (V2.6.1)

Menu **Éclats du Seuil → Essai des suites du Seuil : …**, **hors du mode Jeu** (une copie de la sauvegarde est gardée à chaque fois). Détail : [MONDE.md](MONDE.md#suites-du-seuil-sous-lot-v261).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 14.1 | « propager une cicatrice (des ruines vers le lac) », puis Play | Message « Le froid a cheminé sous l'herbe depuis la cicatrice aux ruines : une nouvelle cicatrice s'ouvre au lac des Aubes. » ; au lac, un rond d'herbe pâle au palier 1 ; aux ruines, palier 4 et trois échos rôdeurs | [ ] | |
| 14.2 | Arrêter, « avancer de 2 jours du monde », Play | Le froid gagne aussi **le pré** (même message) ; le lac a grandi d'un palier | [ ] | |
| 14.3 | Purifier la cicatrice du **lac** (un éclat), puis avancer de 2 jours | Pendant 2 jours, rien ; ensuite le lac se rouvre, tant que les **ruines** ne sont pas purifiées | [ ] | |
| 14.4 | Purifier les **ruines** (vaincre d'abord leurs rôdeurs), puis avancer de plusieurs jours | Plus aucune cicatrice ne naît | [ ] | |
| 14.5 | « lancer la quête de Sœur Albe », puis Play | Journal : « Les cicatrices du Seuil » suivie ; l'étoile de la carte montre la cicatrice la plus proche ; deux éclats dans le sac | [ ] | |
| 14.6 | Purifier une cicatrice, puis aller voir **Sœur Albe** (à la chapelle, ou **sur le parvis de 17 h à 18 h**) | Elle rend un éclat (« Les vieux livres disaient vrai… ») ; l'étape suivante demande deux cicatrices (compteur 0/2) | [ ] | |
| 14.7 | Sans aucune cicatrice, lui parler pendant la quête | Elle dit de **guetter la prochaine faille** (la quête attend) | [ ] | |
| 14.8 | Purifier deux autres cicatrices (« propager » en fait naître), revenir la voir | « Trois. Vous en avez refermé trois… » ; **chapelet de l'Aube** et 900 points, une seule fois ; ensuite un mot d'après, une fois | [ ] | |
| 14.9 | Parler à **Gaspard** (son poste près des ruines) et à **Odile** (au moulin) après une cicatrice, une propagation, une purification | Une réplique par chose vue, une seule fois chacune ; ensuite, tant qu'il reste des cicatrices, un mot sur le froid ; pendant la quête, Gaspard donne un remède de l'aube | [ ] | |
| 14.10 | Aldric (après les échos rôdeurs), le capitaine (après une propagation) | Une réplique chacun, une seule fois | [ ] | |
| 14.11 | Ton avis : vitesse de la propagation (2 jours au palier 4), voisins (les ruines au milieu), quête (trois cicatrices), récompense, répliques | — | [ ] | |

## 15. Les chemins (V2.8)

(Le § 14 est réservé à un autre lot.) Menu **Éclats du Seuil → Essai des chemins : …**, **hors du mode Jeu** (il avance la partie au bon moment ; une copie de la sauvegarde est gardée). Détail : [MONDE.md](MONDE.md#recherche-de-chemin-sous-lot-v28).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 15.1 | « 11 h 30 (les PNJ traversent le bourg) », puis Play ; rester sur la place du bourg | Les PNJ quittent leur place du matin et **contournent les maisons, les étals et le puits** : ils longent un coin puis repartent droit ; aucun ne traverse un mur ni ne s'y colle | [ ] | |
| 15.2 | Suivre Firmin jusqu'au marché, puis à la sieste (14 h) | Il prend la rue, tourne au coin de sa maison et s'arrête **devant** sa porte | [ ] | |
| 15.3 | « 19 h (portes de l'auberge, passants) », puis Play ; rester devant l'auberge | Les passants viennent devant l'auberge **par les rues** ; les clients de la taverne marchent **jusqu'à la porte** en contournant les maisons, puis disparaissent | [ ] | |
| 15.4 | Entrer dans l'auberge | Les clients **apparaissent à la porte** et vont s'asseoir **entre les tables** (sans les traverser) | [ ] | |
| 15.5 | Dehors, te faire poursuivre par un loup puis passer derrière une maison ou un gros rocher | Le loup **fait le tour** au lieu de rester collé au mur ; il te rattrape par le côté | [ ] | |
| 15.6 | Effrayer un lièvre ou un cerf contre une clôture ou un muret | Il **file de biais** le long de l'obstacle au lieu de s'y bloquer | [ ] | |
| 15.7 | Monter sur un point que la bête ne peut pas atteindre (de l'autre côté d'une palissade fermée, si tu en trouves un) | Elle **t'attend sans bouger**, tournée vers toi ; jamais d'aller-retour | [ ] | |
| 15.8 | Ton avis : allure des détours, PNJ qui vont droit s'il n'y a vraiment aucun chemin, tireurs qui restent coincés contre un mur en reculant | — | [ ] | |

## 16. Vrais modèles et sons (V2.9)

Avant l'essai : déposer `Cat_Stripe_DreamNoms.fbx` dans `Assets/Style/Sketchfab/Animaux/`, puis menu **Éclats du Seuil → Préparer l'essai de style** ; la fenêtre doit dire « Cerf : Stag_Quaternius.fbx, biche : Deer_Quaternius.fbx, lièvre : Rabbit_Pneshik.fbx, chat : Cat_Stripe_DreamNoms.fbx » et « Voix des bêtes, cloches et failles : 11 sur 11 ». Pour voir les bêtes sans attendre : les menus d'essai des animaux et des bêtes qui se promènent (§ 9 et 12), de la nuit, de la brume et des événements. Détail : [UNITY.md](UNITY.md#essai-v29--vrais-modèles-et-sons).

| # | À faire | Ce que tu dois voir et entendre | OK | Remarque |
|---|---|---|---|---|
| 16.1 | Prendre Pain-d'Épice (le chat de Rosalie), marcher, courir, s'arrêter longtemps | Un vrai **chat roux** à rayures plus foncées (collier rouge), qui marche et court en suivant ton allure, attend debout, fouette de la queue, regarde autour de lui, se tapit ; il **miaule** (vrais miaulements, pas toujours le même) et **ronronne** quelques secondes | [ ] | |
| 16.2 | « l'aube des cerfs », Play, sud du lac des Aubes | Un **cerf à bois** (le chef) et des **biches** qui broutent tête basse (en boucle, sans se figer), marchent, puis **galopent** ensemble quand tu approches ; à l'aube, de temps en temps (une fois toutes les deux ou trois minutes), un **brame** au loin | [ ] | |
| 16.3 | « le crépuscule des lièvres », champs du sud | De vrais **lièvres** (texture) qui mangent, sautillent, **détalent** avec un bruit de course dans l'herbe | [ ] | |
| 16.4 | Le matin, champs du sud (sangliers) | Des **sangliers** qui marchent dans le bon sens (sinon : régler **Boar Yaw**, voir UNITY.md), se dandinent, fouillent le groin au sol ; des **grognements** de près ; ils chargent le groin en avant ; morts, couchés sur le flanc | [ ] | |
| 16.5 | La nuit (menu « 21 h 55 ») et la brume ; s'approcher des loups | Loups **gris** (modèle de l'Asset Store ou de Quaternius), **hurlements** de temps en temps du côté de la meute ; en combat : galop, attaque, coup reçu, mort ; le **loup blanc** du contrat est blanc ; les échos restent pâles et bleutés | [ ] | |
| 16.6 | Attaque du hameau (menu des événements) | Le **tocsin** sonne vite, du côté du hameau ; si tu le défends : la **cloche d'église** | [ ] | |
| 16.7 | Une faille du Seuil | **Verre qui éclate** à l'ouverture, **bourdonnement** autour d'elle (plus fort en s'approchant), **miroir qui explose** quand elle se referme ; une cicatrice qui grandit crisse | [ ] | |
| 16.8 | Entrer dans une maison, mettre en pause | Les voix des bêtes se taisent | [ ] | |
| 16.9 | Sur le Note8 | L'image reste fluide avec les bêtes animées ; les sons ne coupent pas | [ ] | |
| 16.10 | Ton avis : modèles (loup de Quaternius ou loup réaliste ?), taille des bêtes, couleur du chat, fréquence des hurlements et du brame, volume (**Beast Volume**), portée (**Beast Hearing**) | — | [ ] | |
| 16.11 | 2e vague : déposer les FBX convertis dans `Assets/Style/Sketchfab/Animaux2/`, préparer | La fenêtre cite `CerfRouge_Suiteofart`, `Faon_AssetsAnimated`, `Lievre_bluekiwi`, `Sanglier_Dakta`, et « Ferme : corbeau …, vache …, chien …, cheval … » | [ ] | |
| 16.12 | Cerfs, biches, lièvres, sangliers, loups (comme 16.2 à 16.5) ; déposer d'abord les nouveaux `Elan_WildMesh.fbx` et `Sanglier_Dakta.fbx` (7 novembre, 20 h) | Le cerf est l'**élan** : il broute vraiment, marche et galope sur place sans glisser en avant, sursaute quand on le frappe, s'effondre à sa mort ; le **sanglier a un pelage** (dos sombre, flancs bruns, pattes noires) et marche droit ; une bête sans attaque charge le museau en avant | [ ] | |
| 16.13 | Champs du sud, hameau, bourg, ruines, le jour ; rester quelques minutes ; puis s'approcher des corbeaux | Vaches, chevaux, chiens, corbeaux qui **se promènent** dans leur coin (marche, arrêt, broutage), sans traverser les maisons ni les routes (le chien, si) ; meuglements, hennissements, aboiements ; les corbeaux sautillent, s'envolent en croassant et reviennent une minute plus tard | [ ] | |
| 16.14 | Le bourg à 9 h 30, à 15 h, à 21 h ; entrer dans l'auberge ; passer devant la forge de Maëlle | Marché le matin, rumeur l'après-midi, taverne le soir (rires dans l'auberge), charbon et soufflet près de la forge | [ ] | |
| 16.15 | Marcher sur une route, dans l'herbe, dans la forêt des Loups, dans une maison | Pas sur le gravier, dans l'herbe, dans les feuilles, sur le plancher | [ ] | |
| 16.16 | Hameau à l'aube ; forêt la nuit ; camp des rôdeurs | Le coq ; la hulotte ; le feu de camp | [ ] | |
| 16.17 | Explorer ; aller au Seuil ; une scène de l'histoire | « Into The Wilds », « Sanctum », « The Legend of Kvothe » | [ ] | |
| 16.18 | Ton avis : quel loup (Asset Store, roo3d, Quaternius), quel lièvre (bluekiwi ou Pneshik), le cerf élaphe sans mort ni brouter, nombre de bêtes de la ferme (**Livestock**), volume des pas (**Step Volume**) | — | [ ] | |

## 17. Échos fantômes et failles (V2.9.1)

Avant l'essai : menu **Éclats du Seuil → Préparer l'essai de style** (rien à déposer : les deux shaders, `EdS/Echo` et `EdS/Faille`, sont dans le dépôt). Si la console affiche une erreur de shader, note-la ici : le jeu garde alors les échos pâles de la V2.1 et les failles en formes simples de la V2.5. Pour voir sans attendre : menus de la nuit (« minuit »), des événements (« ouvrir une faille du Seuil ») et du Seuil qui grandit (paliers 1 à 3). Détail : [UNITY.md](UNITY.md#essai-v291--échos-fantômes-et-failles).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 17.1 | Menu « minuit », aller vers les échos de la nuit (`loup_echo`) | Des loups **transparents bleu froid**, au **contour lumineux**, qui **scintillent** doucement, avec de fines bandes de lumière qui montent ; aucune ombre au sol ; on ne voit pas leurs pattes à travers le corps | [ ] | |
| 17.2 | En frapper un, le blesser, le tuer | Plus il est blessé, plus il est **transparent** ; à sa mort, il **se défait en braises** bleues (le haut part le premier), sans poussière ; il disparaît en une seconde et demie | [ ] | |
| 17.3 | Attendre qu'une bête revienne après sa mort (loup du pré, sanglier : une à trois minutes) | Elle revient **en écho** gris-bleu pâle : elle **se forme en lueurs** qui montent ; en marchant, elle laisse **quelques lueurs** derrière elle (discrètes) | [ ] | |
| 17.4 | Le loup blanc (contrat de Gaspard, une nuit sur trois environ) | **Blanc d'argent**, presque solide, contour très vif la nuit | [ ] | |
| 17.5 | Le souvenir de la Rupture (chapitre 1) | L'Écho de la Rupture et les Voix : **or pâle de l'aube**, très solides | [ ] | |
| 17.6 | Menu « ouvrir une faille du Seuil », la nuit | Une **déchirure verticale** qui s'ouvre en trois secondes : lèvres **déchiquetées qui brûlent** (orange et violet), vide violet qui tourne dedans ; sur ordinateur, le décor autour **ondule**, tiré vers la fente ; des **lueurs sont aspirées** vers elle ; sa lumière **bat** ; la déchirure te fait toujours face. Les échos qu'elle crache sont **violets** | [ ] | |
| 17.7 | Cicatrice au palier 1, puis 3 (menus du Seuil qui grandit), puis rouvrir une faille dessus | Au cœur de la cicatrice, une **déchirure dormante** couchée au sol, plus longue et plus ouverte au palier 3 ; une faille rouverte dessus est **plus grande** (+30 % par palier), bat plus vite, aspire plus de lueurs ; les échos rôdeurs y sont **plus solides et plus vifs** | [ ] | |
| 17.8 | Combat contre trois échos à la fois, de jour puis de nuit | On voit toujours bien les annonces au sol et la cible ; de nuit, les échos brillent plus (fondu de quatre secondes à la tombée de la nuit) | [ ] | |
| 17.9 | Sur le Note8 | Image fluide avec trois échos et une faille ; pas de distorsion du décor sur le téléphone (la déchirure seule), moitié moins de lueurs | [ ] | |
| 17.10 | Ton avis : couleurs par sorte d'écho, transparence, force du contour, durée des braises, taille des failles ; le gibier (cerfs, lièvres) qui revient doit-il aussi être un écho ? | — | [ ] | |
