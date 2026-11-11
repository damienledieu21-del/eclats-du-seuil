# Essai V1.10 — liste de vérification pour Damien

But : jouer la vague 1 de bout en bout sous Unity, vérifier les **5 défauts de l'essai 10** et les **maisons agrandies**, et noter ce qui ne va pas. Une ligne = un point à regarder. Coche la case `[x]` quand c'est bon ; sinon laisse `[ ]` et écris ce que tu vois dans **Remarque** (une capture d'écran aide beaucoup : touche `Impr. écran`, puis colle-la dans la conversation).

Comment noter : ouvre ce fichier (dans GitHub, bouton crayon, ou dans un éditeur de texte), remplis la colonne « Remarque », puis envoie-le à Claude (ou colle les lignes qui ont un problème).

Touches (clavier AZERTY) : **Z Q S D** marcher · **souris** tourner la caméra · **molette** zoom · **C** caméra derrière toi · **F** parler / passer une porte / récolter · **clic droit** se garder · **Espace** parer · **1 à 6** compétences · **P** boire une potion · **I** personnage et sac · **J** journal des quêtes · **K** forger · **L** carte · **B** compétences · **Échap** pause et réglages · **F6** sauvegarder.

## 0. Préparer (une seule fois)

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 0.1 | Dans GitHub Desktop, prendre la branche `v1.10-bourg-essai` (ou `main` une fois fusionnée) | Unity recharge les scripts, aucune erreur rouge dans la Console | [ ] | |
| 0.0 | **D'abord** (V2.9.3) : menu **Éclats du Seuil → Vérifier l'essai** | Tout vert : la Console dit « ✅ tout est prêt » (pas de fenêtre). Sinon une fenêtre « Vérification de l'essai » s'ouvre ; en haut, en orange « [REPLI] Prêt avec replis » (l'essai se lance, la liste dit ce qui prend un repli : formes simples, son fait par le code…) ou en rouge « [BLOQUANT] » (une phrase par problème : quoi faire). Chaque ligne orange dit le fichier à déposer et le dossier exact. Le même rapport est écrit dans `unity/Assets/EdS/VERIFICATION_ESSAI.md` (bouton « Ouvrir le fichier du rapport ») : **envoie ce fichier à Claude** s'il y a du rouge ou de l'orange que tu ne comprends pas. « Préparer et lancer l'essai » (0.4) refait cette vérification tout seul, et demande « Lancer quand même ? » s'il y a du rouge | [ ] | |
| 0.2 | (Conseil, avant tout) menu **Éclats du Seuil → Sauvegarder le dossier des ressources** (si ton dossier Documents est synchronisé par OneDrive, le zip part aussi dans le nuage : le message te prévient) | Une barre avance, puis un message dit « Réussi » et donne l'emplacement du zip (dans Documents, dossier `EdS-Sauvegardes`) ; rien n'est supprimé | [ ] | |
| 0.3 | Pour une **nouvelle partie** : renommer `sauvegardes/vallons.json` (dossier `AppData\LocalLow\…` du jeu) en `vallons_avant.json` | Le fichier n'est plus là sous son nom | [ ] | |
| 0.4 | Menu **Éclats du Seuil → Préparer et lancer l'essai** (une seule action : il prépare, ouvre la scène et lance le jeu) | La fenêtre « Vérification de l'essai » s'ouvre seulement s'il y a de l'orange ou du rouge (voir 0.0), Unity travaille un moment, puis le jeu démarre tout seul ; la Console (onglet en bas) contient un message commençant par « Essai de style » (le message « Lumière des intérieurs remise… » n'apparaît qu'à la toute première préparation) | [ ] | |
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

Avant l'essai : menu **Éclats du Seuil → Préparer l'essai de style** (rien à déposer : les deux shaders, `EdS/Echo` et `EdS/Faille`, sont dans le dépôt). Si la console affiche une erreur de shader, note-la ici : le jeu garde alors les échos pâles de la V2.1 et les failles en formes simples de la V2.5 (un écho ne devient jamais invisible). Pour voir sans attendre : menus de la nuit (« minuit »), des événements (« ouvrir une faille du Seuil ») et du Seuil qui grandit (paliers 1 à 3). Détail : [UNITY.md](UNITY.md#essai-v291--échos-fantômes-et-failles).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 17.1 | Menu « minuit », aller vers les échos de la nuit (`loup_echo`) | Des loups **transparents bleu froid**, au **contour lumineux**, qui **scintillent** doucement, avec de fines bandes de lumière qui montent ; aucune ombre au sol ; on ne voit pas leurs pattes à travers le corps | [ ] | |
| 17.2 | En frapper un, le blesser, le tuer | Plus il est blessé, plus il est **transparent** ; à sa mort, il **se défait en braises** bleues (le haut part le premier), sans poussière ; il disparaît en une seconde et demie | [ ] | |
| 17.3 | Attendre qu'une bête revienne après sa mort (loup du pré, sanglier : une à trois minutes) | Elle revient **en écho** gris-bleu pâle : elle **se forme en lueurs** qui montent ; en marchant, elle laisse **quelques lueurs** derrière elle (discrètes) | [ ] | |
| 17.4 | Le loup blanc (contrat de Gaspard, une nuit sur trois environ) | **Blanc d'argent**, presque solide, contour très vif la nuit | [ ] | |
| 17.5 | Le souvenir de la Rupture (chapitre 1) | L'Écho de la Rupture et les Voix : **or pâle de l'aube**, très solides | [ ] | |
| 17.6 | Menu « ouvrir une faille du Seuil », la nuit | Une **déchirure verticale** qui s'ouvre en trois secondes : lèvres **déchiquetées qui brûlent** (orange et violet), vide violet qui tourne dedans ; sur ordinateur, le décor autour **ondule**, tiré vers la fente ; des **lueurs sont aspirées** vers elle ; sa lumière **bat** ; la déchirure te fait toujours face. Les échos qu'elle crache sont **violets** | [ ] | |
| 17.6 bis | Une biche ou un lièvre qui revient après sa mort, de jour | Elle revient **normale** (la forêt se repeuple : pas un écho, décision du 7 novembre) ; si un combat de chef se rejoue (Croc-Gris) : normal après une défaite, en écho gris-bleu après une victoire | [ ] | |
| 17.7 | Cicatrice au palier 1, puis 3 (menus du Seuil qui grandit), puis rouvrir une faille dessus | Au cœur de la cicatrice, une **déchirure dormante** couchée au sol, plus longue et plus ouverte au palier 3 ; une faille rouverte dessus est **plus grande** (+30 % par palier), bat plus vite, aspire plus de lueurs ; les échos rôdeurs y sont **plus solides et plus vifs** | [ ] | |
| 17.8 | Combat contre trois échos à la fois, de jour puis de nuit | On voit toujours bien les annonces au sol et la cible ; de nuit, les échos brillent plus (fondu de quatre secondes à la tombée de la nuit) | [ ] | |
| 17.9 | Sur le Note8 | Image fluide avec trois échos et une faille ; pas de distorsion du décor sur le téléphone (la déchirure seule), moitié moins de lueurs | [ ] | |
| 17.10 | Ton avis : transparence, force du contour, durée des braises, taille des failles (couleurs déjà validées le 7 novembre) | — | [ ] | |

## 18. Vraies animations, vrais corps, 3e et 4e vagues (V2.9.2)

Avant l'essai : déposer les huit FBX convertis dans `Assets/Style/Sketchfab/Animaux3/` ([UNITY.md](UNITY.md#essai-v292--vraies-animations-vrais-corps-3e-et-4e-vagues)), puis menu **Éclats du Seuil → Préparer l'essai de style**. Note ici ce que dit sa fenêtre (« Corps humains : … », « Basse-cour : … »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 18.1 | Fenêtre de la préparation | « Corps humains : prêts (52 tenues, … animations, 5 coiffures, barbe) » ; « Basse-cour : âne Ane_WildMesh.fbx, poule Poule_kill6lucius.fbx, coq Coq_rukh3d.fbx ; ours : Ours_WildMesh.fbx » ; vache Vache_nataliekirk, chien Chien_SharPei_artsiom ; aucune erreur rouge dans la console | [ ] | |
| 18.2 | Le bourg, « 12 h 15, midi » | Les villageois sont de **vrais corps** : hommes et femmes, peaux claires et sombres, coiffures et barbes variées, tuniques de couleurs différentes avec ceinture, chausses, bottes ; ils parlent avec les mains, croisent les bras, marchent ; **personne en pose en T**, personne d'invisible | [ ] | |
| 18.3 | Les gardes du bourg | Tabard **bleu** (gardes) ou **rouge** (sentinelles) ; ils regardent autour d'eux, bras croisés | [ ] | |
| 18.4 | Parler à Odile, Sœur Albe, Maëlle, Renart, Gaspard | Odile, Albe et Maëlle sont des **femmes** ; Albe en **robe claire**, Maëlle en **tablier de cuir**, Renart en **prune** ; Gaspard un homme ; chacun toujours le même d'une partie à l'autre | [ ] | |
| 18.5 | La forge de Maëlle et les champs | Les paysans qui travaillent **récoltent, sèment ou arrosent** ; près de la forge on entend **le marteau sur l'enclume** | [ ] | |
| 18.6 | « 19 h 45, la taverne », entrer dans l'auberge | **The Old Tower Inn** joue ; on entend la salle **à table** (couverts) en plus des rires ; l'aubergiste et les clients sont de vrais corps | [ ] | |
| 18.7 | « minuit », marcher dans le bourg | **Night Town** joue (le bourg la nuit) | [ ] | |
| 18.8 | Un combat contre des bandits ou des rôdeurs (s'ils n'ont pas leur personnage de Mixamo) | Ils frappent (**coups d'épée variés**), parent (**bouclier levé**), encaissent (**recul**), meurent (**chute**) ; aucun ne glisse au sol | [ ] | |
| 18.9 | « l'attaque du hameau maintenant » | Le **tocsin** : sept coups lents d'une grosse cloche ; pendant le combat, **Determined Pursuit** ; hameau défendu : les cloches d'église | [ ] | |
| 18.10 | « ouvrir une faille du Seuil » | À l'ouverture et à chaque vague, un **souffle magique** en plus du verre | [ ] | |
| 18.11 | Le hameau, de jour | **Un âne** qui erre, broute, hennit (plus grave) ; **cinq poules** qui picorent et trottinent ; **le coq** (picore, chante à l'aube) ; un cheval ou l'âne qui marche près de toi fait **claquer ses sabots** | [ ] | |
| 18.12 | Les champs, le chien du bourg | Les vaches (**nataliekirk**) bougent la tête et la queue, marchent en balançant les pattes ; le **Shar Pei** marche, s'assoit | [ ] | |
| 18.13 | La harde | Les biches sont celles de **WildMesh** (broutent, fuient, tombent à leur mort, couchées au repos) ; les cerfs à bois inchangés (l'élan) | [ ] | |
| 18.14 | Bord du lac, sous la pluie, près de la rivière | Clapotis du **lac** ; **pluie** sur le jardin et le toit ; le **ruisseau** ; le **vent** dans la forêt | [ ] | |
| 18.15 | Sur le Note8 | Image fluide au bourg à midi (un tiers des villageois), textures nettes de loin ; aucune bête ou personne figée en pose en T | [ ] | |
| 18.16 | Forêt des Loups, le jour, à l'ouest | **L'ourse** et **deux oursons** (petits, assis ou couchés) ; à moins de 9 m d'elle, elle charge ; les oursons ne se combattent pas | [ ] | |
| 18.17 | « minuit », au nord-est de la forêt | **L'ours** rôde (signe : des branches cassées) ; griffe, charge annoncée, il se dresse et retombe (cercle annoncé) ; certaines nuits, **le vieux Brunot** (grondement), énorme, qui **rugit** | [ ] | |
| 18.18 | Après Croc-Gris, niveau 14 : parler à Gaspard | Il propose « Le vieux Brunot » (sa balafre, ses cicatrices) ; griffures dans la forêt, Brunot abattu, retour : la **cape du vieux Brunot** | [ ] | |
| 18.19 | Ton avis : les visages, la force des ours, la quête de Brunot | — | [ ] | |

## 19. Vrais vêtements des villageois (V2.9.4)

Avant l'essai : *Modular Character Outfits - Fantasy* [Standard] de Quaternius dans `Assets/Style/Quaternius/Outfits/` (`Exports/FBX (Unity)` et `Textures` : fait le 8 novembre 2026) ([UNITY.md](UNITY.md#essai-v294--vrais-vêtements-des-villageois)), puis menu **Éclats du Seuil → Préparer l'essai de style**. Note ici la fin de la ligne « Corps humains : … ; vrais vêtements : … ».

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 19.1 | Fenêtre de la préparation | « vrais vêtements : prêts (20 pièces : Peasant 8, Ranger 12 ; 2 coiffes, 0 capes ; 4 étoffes, peau des mains) » (et la note « aucune cape ») ; sinon la raison (à envoyer à Claude avec la liste des fichiers du dossier `Outfits`) | [ ] | |
| 19.2 | Le bourg, « 12 h 15, midi », de près | Les villageois portent de **vrais vêtements** (tissu qui a du volume, plis, manches, bottes) ; plus d'effet « collant » ; la peau ne traverse nulle part (bras, cuisses, ventre) ; mains et visages à la bonne couleur de peau ; personne de nu ni d'invisible | [ ] | |
| 19.3 | Les métiers (version gratuite : paysan et rôdeur) | Gardes en rôdeur **bleu** avec épaulières, sentinelles en rôdeur **rouge**, Sœur Albe en **laine claire** (capuche une fois sur deux), Renart en **prune**, Maëlle en cuir avec brassards, paysans en habits de paysan (lin, saule, rouille, guède), rôdeurs en **vert**, bandits **encapuchonnés** et usés ; mains de la couleur du visage (claire ou sombre) | [ ] | |
| 19.4 | Variété | Deux paysans voisins ne sont pas habillés pareil (pièces, teinte, usure) ; chacun garde la même tenue d'une partie à l'autre | [ ] | |
| 19.5 | Catalogue : **Human Season** sur *Winter*, Play (*Auto* : la saison du jeu, printemps tant qu'il n'y a pas de calendrier) | Capes et capuches presque partout (pas à la forge) ; les gardes ont un gorgerin ; sur *Summer* : têtes nues, pas de cape | [ ] | |
| 19.6 | Une averse (V2.2), puis entrer et ressortir d'une maison | Les villageois qui apparaissent sous la pluie ont la **capuche** levée ; une capuche ou un casque ne laisse pas passer les cheveux | [ ] | |
| 19.7 | Catalogue : **Human Festival** coché, Play | Couleurs de fête (or, garance, bleuet), habits neufs, marchands et aubergiste en noble, le capitaine sans cuirasse | [ ] | |
| 19.8 | Catalogue : **Human Clothes** décoché, Play | Retour exact aux **tenues peintes** de V2.9.2 | [ ] | |
| 19.9 | Animations | Marche, travail, parler, combat : les vêtements suivent le corps (pas de pièce qui reste en l'air ou en pose en T) | [ ] | |
| 19.10 | Sur le Note8 | Image toujours fluide au bourg à midi | [ ] | |
| 19.11 | Ton avis : les tenues par métier, les couleurs, faut-il la version *Source* (15 $, décision après cet essai) pour avoir tout le pack ? | — | [ ] | |

## 20. La 5e vague branchée (V2.11)

Avant l'essai : les dossiers de la 5e vague déposés tels quels sous `Assets/Style` (`Sketchfab/{Creatures,Personnages,Armes,Seuil,Village,Boulangerie,Fete,Donjon,Interieurs}` en **FBX**, `PolyHaven/{Armes,Nourriture,Objets,Interieurs2,Cuisine,Donjon,Decoration,Nature}`, `Fab/Megascans_Pains`, `Mixamo/Metiers`, `Sounds/{Boulangerie,Fete,Donjon,Monstres,Armes,Sorts,FantasySFX_LittleRobot,CC0_rubberduck}`, `Music/{ScottBuckley2,OGA2}`), le pack *6200 Fantasy RPG Icons* de CraftPix importé n'importe où dans `Assets`, puis **Éclats du Seuil → Préparer l'essai de style**, puis **Vérifier l'essai** ([UNITY.md](UNITY.md#essai-v211--la-5e-vague-branchée)).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 20.1 | Fenêtre de la préparation | Une ligne « 5e vague : N ressources branchées trouvées sur 25, M prêtes pour plus tard, grand loup de Croc-Gris, 9 pains scannés ; gestes de métier : 5 sur 5 ; icônes de CraftPix : X objets sur 117 » | [ ] | |
| 20.2 | Vérifier l'essai | Sections « 5e vague », « 5e vague prête pour plus tard » et « Icônes et licences hors dépôt » : ✅ ou ⚠️ avec le fichier à déposer ; « Manifeste des licences » ✅ (`Assets/Style/Generated/LICENCES_ASSET_STORE.md`) | [ ] | |
| 20.3 | Croc-Gris (pré du nord), puis le loup blanc (nuit, quête de Gaspard), sur ordinateur | Le **grand loup animé** (marche, course, attaque, rugissement, coup reçu, mort), plus massif que les autres loups ; les autres loups gardent leur modèle ; sur téléphone, le loup des meutes (plus grand) | [ ] | Le loup blanc garde le pelage gris du modèle texturé (teinte impossible sur une texture) : à juger |
| 20.4 | Les corbeaux des champs et des ruines, puis s'approcher | Ils **regardent autour d'eux** posés, puis **décollent et volent** trois secondes ; plumes nettes (pas de carrés noirs autour) | [ ] | |
| 20.5 | Le souvenir de la Rupture | Galeran est le **chevalier en armure à la lance** (le roi en armure s'il manque) ; il se balance quand il avance ; sans les deux : lance et armure en formes simples **avec le heaume** | [ ] | Le chevalier n'a qu'une attente : glisse-t-il trop ? |
| 20.6 | Équiper la masse du bourg, la hachette, une épée (héros sans paladin), une cape | La **vraie masse**, la **vraie hache**, la vraie épée **dans la main**, tête en haut (épée : garde en bas) ; la **demi-cape** dans le dos (sur ordinateur ; sur téléphone, la cape simple) | [ ] | Si une arme est à l'envers : catalogue → 5e vague → Grip Sign = −Grip Sign |
| 20.7 | Les pierres du Seuil, la nuit | Les **menhirs** aux runes qui **luisent** (halo du Bloom) à la place des rochers | [ ] | |
| 20.8 | Le cœur du Seuil | Grand cristal, quatre touffes de **cristaux lumineux**, le **feu follet** qui flotte, une lueur bleue | [ ] | |
| 20.9 | Une faille ouverte (événement « faille du Seuil ») | Un **cadre de portail** lumineux autour de la déchirure, toujours de face, et des cristaux à ses pieds | [ ] | |
| 20.10 | Le bourg : Firmin devant sa maison | Le **four à pain de briques** à sa droite (jamais dans un mur), deux sacs de farine, une table avec **pains scannés** (Megascans) et viennoiseries à sa gauche ; tout près du four, **les braises crépitent** | [ ] | Place à juger (le four fait 3,3 m) |
| 20.11 | Bertille, les champs, le hameau | Caisse de poires à l'étal, **épouvantail** et bottes de **foin** (brins nets) aux champs, **charrette** au hameau | [ ] | |
| 20.12 | Catalogue : **Human Festival** coché, le bourg de 9 h à 23 h | Guirlande et bannières sur la place (guirlande : ordinateur seulement), **foule de fête** en fond | [ ] | |
| 20.13 | Le moulin d'Odile, le jour | La **meule** qui tourne en fond | [ ] | |
| 20.14 | Près de la forge de Maëlle, le jour | La musique « **Song of the Forge** » remplace celle du village (à moins de 14 m) | [ ] | |
| 20.15 | Un combat : coups, parade, blocage, sort, rugissement du Gardien | Les sons déjà choisis restent ; les nouveaux ne viennent qu'à défaut | [ ] | |
| 20.16 | L'auberge, une maison, la chapelle | Nouveaux meubles de Poly Haven (tables, chaises, lits, armoires, lampes), casseroles et chopes, coffre, chaudron | [ ] | |
| 20.17 | Les villageois au travail | Gestes de métier de Mixamo : au **comptoir** (marchands), **porter** et **la pause** (paysans), **s'agenouiller** (forge, prière à la chapelle) ; sans les fichiers, comme avant | [ ] | |
| 20.18 | Sac et boutiques | Les objets ont les **icônes peintes de CraftPix** (six épées différentes) ; ceux sans correspondance gardent leur icône | [ ] | Liste des objets mal reliés à envoyer à Claude |
| 20.19 | Sur le Note8 | Image fluide au bourg et au Seuil ; ni guirlande, ni demi-cape, ni tours en ruine, ni grand loup ; le pont cintré de bois à la place du pont de pierre | [ ] | |
| 20.20 | Le pont des Vallons (décision du 9 novembre) | Le **pont de pierre** de JooL en travers de la rivière, à la place du pont cintré, sur ordinateur ; on le traverse à la même hauteur qu'avant ; sans le modèle, et sur téléphone, le pont cintré | [ ] | Échelle et hauteur estimées : régler Scale dans le catalogue |
| 20.21 | Les ruines, sur ordinateur | Les **tours en ruine** de taz83 à la taille du lieu ; sur téléphone, les ruines comme avant | [ ] | |

## 21. Marchands vivants et fêtes du bourg (V2.10)

Avant l'essai : une partie déjà lancée une fois. Les menus **Éclats du Seuil → Essai des fêtes** avancent la partie (hors du mode Jeu) au prochain jour de marché (8 h), à la foire de printemps (9 h), à la fête des moissons (10 h 30), à la veillée de l'Aube (19 h), ou au lendemain à 9 h (le stock se regagne). Valeurs provisoires : [MONDE.md](MONDE.md#marchands-vivants-et-fêtes-du-bourg-sous-lot-v210).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 21.1 | Chez Sauge, acheter des potions de soin | Chaque offre dit son stock (« 30 en stock ») ; au-delà, « Plus en stock : le marchand en recevra demain. », la ligne en rouge | [ ] | |
| 21.2 | « le lendemain, 9 h », revenir chez Sauge | La moitié du stock est revenue (15 potions de plus, jusqu'au plein) | [ ] | |
| 21.3 | Vendre des peaux de loup à Sauge | Elles apparaissent dans sa boutique, « revendu par toi », plus chères qu'elle ne les a payées ; le lendemain, il en reste moins | [ ] | |
| 21.4 | Laisser piller le hameau (attaque manquée), puis aller au bourg | Potions et remèdes rares (un tiers du stock) et plus chers pendant trois jours ; la faille restée ouverte : plus d'éclats de Seuil | [ ] | |
| 21.5 | « jour de marché, 8 h » | Le bandeau du lieu dit « Fête au bourg : le jour de marché » ; **Maître Anseau** est sur la place (un autre jour, il n'est pas là) ; Lison devant son étal ; trois objets rares qui changent d'une semaine à l'autre ; tant que Mâchefer tient le camp, pas de bottes ni de pendentif | [ ] | |
| 21.6 | « fête des moissons, 10 h 30 » | Firmin à la table du concours, Mathurin au tonneau de cidre, Lison au bal, Odile et Aldric à la fête ; la foule du marché et la taverne s'entendent toute la journée | [ ] | |
| 21.7 | Avec 4 herbes de l'Aube, parler à Firmin | « [Concours] Présenter une tourte… » : 150 cuivre, deux miches, villageois +15 ; le concours se referme jusqu'à l'an prochain | [ ] | |
| 21.8 | « foire de printemps, 9 h », parler à Aldric au pas de tir | Concours de tir (20 cuivre) : au niveau 4, viser le cœur (150 cuivre, garde +10) ; sinon un prix de consolation | [ ] | |
| 21.9 | « veillée de l'Aube, 19 h », devant la chapelle | Frère Anselme, Sœur Albe, Lison et Rosalie à la veillée ; raconter les traces sous le moulin (villageois +12, un remède) | [ ] | |
| 21.10 | Pendant une fête, une quête en cours chez Firmin ou Aldric | La quête passe avant la fête : rien n'est bloqué | [ ] | |
| 21.11 | « jour de marché, 8 h », trois semaines de suite | Une semaine la tourte de Firmin (la première fois : le **tablier de maître**), la suivante le tir d'Aldric (l'**arc de concours**), puis les contes de frère Anselme (le **livre de contes**) ; ensuite des bourses ordinaires | [ ] | |
| 21.12 | Une fête au bourg, en automne puis en hiver | **King's Feast** aux moissons et à la foire, **Minstrel Dance** au marché et à la veillée ; les villageois en tenues de la saison, beaux habits pendant la fête | [ ] | |
| 21.13 | Ton avis : le rythme du stock, les prix d'hiver, les dates des fêtes, les concours, le colporteur | — | [ ] | |

## 22. Les packs de l'Asset Store (V2.12)

Avant l'essai : les packs importés tels quels **à la racine de `Assets`** (Package Manager → My Assets → Import) : `Baked Bread and Pastry`, `RPG FOOD PROPS` (seul le dossier `UNIVERSAL RENDER PIPELINE` sert), `MyDreamGameStudio/Medieval Village Pack Vol.1`, `Advance Studios/Medieval Castle`, `DungeonModularPack`, `Medieval Props Pack 01`, `Multiple Solutions/Basic RPG Sounds`, `Fantasy Wooden GUI  Free` (deux espaces), `SopaDopa/URP_Medieval_Fantasy`, `Fantasy RPG Icons Pack`. Rien de tout cela ne va dans GitHub (GitHub Desktop ne doit montrer aucun de ces dossiers). Puis **Éclats du Seuil → Préparer l'essai de style**, puis **Vérifier l'essai**.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 22.1 | Fenêtre de la préparation | En plus de la ligne de la 5e vague : « Asset Store : N ressources branchées sur 24, M prêtes pour plus tard (P prefabs au catalogue) ; interface : Stone ; icônes de CraftPix : X objets reliés sur 117 » | [ ] | Noter X |
| 22.2 | Vérifier l'essai | Sections « Packs de l'Asset Store » et « … prêts pour plus tard » : ✅, ou ⚠️ avec le pack à importer ; « Sons de Basic RPG Sounds : K gardés sur 202 » ; « Objets reliés à une icône de CraftPix » | [ ] | |
| 22.3 | GitHub Desktop après la préparation | **Aucun** fichier des packs ni `LICENCES_ASSET_STORE.md` dans les changements | [ ] | Sinon : ne rien valider, prévenir Claude |
| 22.4 | Le fournil de Firmin | Dans la vitrine, le **pain et la galette photoscannés** (LUMI) en tête des pains, le **croissant** de LUMI en tête des viennoiseries (une place sur trois) ; aucune pièce sautée ; aucune ne flotte ni ne s'enfonce | [ ] | Taille : environ 30 cm chacun |
| 22.5 | L'étal de Bertille | Une **table d'étal** garnie (pains, fromages, viandes, saucisse, bouteilles, pomme), deux **caisses garnies** au pied ; ni tomate, ni orange, ni pastèque, ni citrouille | [ ] | Si les victuailles flottent ou sont sur un auvent : catalogue → Offset des victuailles |
| 22.6 | Un jour de fête (jour de marché, 6e jour de la semaine, 7 h – 13 h ; ou catalogue : **Human Festival** coché) | Au bourg : un **étal sous tente** garni (ordinateur), un étal garni près de **Lison**, et chez **Maître Anseau, le colporteur**, l'**étal exotique** (oranges, tomates, pastèques, citrouilles : fruits rares venus de loin) ; guirlande et bannières ; tout disparaît après la fête | [ ] | Sur téléphone : tables garnies sans tente |
| 22.7 | La forge | **Enclume**, **établi**, **présentoir d'armes**, bac et tonneau autour de l'atelier, sans gêner l'enclume du jeu | [ ] | Place à juger |
| 22.8 | Le moulin, côté rivière | Une **roue à eau** sur la rive, jamais dans le moulin de Tim0 (ordinateur ; pas posée s'il n'y a pas de place libre) | [ ] | Position estimée : Offset et Yaw dans le catalogue |
| 22.9 | Le hameau, le village, le bourg | **Abri à bêtes** et **rouet** au hameau ; tonneaux, seaux, caisses, échelle, charrette à bras, souche au village et au bourg ; jamais dans un mur | [ ] | Trop ou pas assez ? |
| 22.10 | Le camp des rôdeurs | Deux **tentes** et une caisse | [ ] | |
| 22.11 | L'auberge, une maison, la chapelle | Les meubles de Poly Haven restent ; les objets des packs ne viennent que pour ce qui manquait (pots, vases, chopes…) | [ ] | |
| 22.12 | Ouvrir le livre : sac, boutique, dialogue, forge, voyage, réglages | Le fond de ces grandes fenêtres en **pierre** sculptée (décision du 9 novembre) ; infobulles, avis, aide et quête gardent la dalle du jeu ; textes lisibles ; bords nets | [ ] | Bords flous ou trop larges : régler Gui Border Fraction ; le parchemin reste possible (Gui Board Kind = Parchment) |
| 22.13 | Un combat, une potion, un butin | Les sons déjà choisis restent ; Basic RPG Sounds seulement pour un son qui manquait, **jamais un son arcade** | [ ] | Un son déplacé : envoyer son nom |
| 22.14 | Sac et boutiques | Icônes de CraftPix **sans fond** ; les pièces d'armure sans icône à leur nom prennent une icône d'armure | [ ] | Liste des objets mal reliés |
| 22.15 | Sur le Note8 | Image fluide ; ni tente d'étal, ni roue à eau, ni abri à bêtes, ni bâtiments lointains ; l'étal de Bertille et la vitrine restent ; Console de la construction : « N ressources réservées à l'ordinateur retirées » | [ ] | APK plus léger qu'avec la V2.11 |
| 22.16 | Le dossier `Assets/Style/Generated` | `LICENCES_ASSET_STORE.md` a une ligne par pack (dossier, usage, fichiers pris) | [ ] | |
| 22.17 | Depuis le hameau, le village, le bourg et les champs, regarder vers le bord du monde (ordinateur) | Quelques **maisons**, la **taverne** et le **moulin** du pack de village **au loin**, hors de l'aire de jeu, tournés vers elle ; tes maisons de Sketchfab et le moulin de Tim0 à leurs places | [ ] | Dans les arbres ou trop loin : Offset au catalogue |

## 23. PC modeste : textures réduites (V2.12.4)

Ton essai du 9 novembre 2026 : « System out of memory » (textures, `d3d11 failed to create 2D texture`) en sortant d'une maison vers le village. Ton PC (4 Go de RAM, GTX 470 à 1224 Mo de mémoire vidéo) est un **PC modeste** : le jeu y réduit les textures au **quart** et y écarte, comme sur le Note8, les ressources « jamais sur téléphone » (grand loup, pont de pierre, tours en ruine, araignée, rat, demi-cape, gambison, étals du marché, guirlande, tente d'étal, roue à eau, abri à bêtes, bâtiments et décor lointain, château, armures…) : à leur place, l'ancien rendu.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 23.1 | Vérifier l'essai | Section « Unity et rendu », ligne « Machine et textures » : « PC modeste (mémoire vidéo 1224 Mo, mémoire 4096 Mo environ) ; textures : quart (limite de mipmaps 2) ; ressources lourdes … écartées » | [ ] | Recopier la ligne si elle dit autre chose |
| 23.2 | Jouer, entrer dans une maison puis sortir vers le village (plusieurs fois) | Plus de « System out of memory » ; textures un peu moins nettes de près | [ ] | Si ça plante encore : envoyer le message complet |
| 23.3 | Pause → Réglages | Bouton « Qualité des textures : auto (quart) » ; un clic : pleine, moitié, quart, puis auto ; le changement se voit tout de suite et reste à la partie suivante | [ ] | « pleine » sur ton PC risque de replanter |
| 23.4 | Le village, le pont, les ruines | Le pont cintré du jeu, ni tours en ruine, ni tente, ni roue à eau, ni bâtiments au loin (comme sur le Note8) | [ ] | |

## 24. Tonneaux, caméra et étiquettes (V2.12.6)

Ton essai du 9 novembre 2026 (PC modeste) : objets blancs à la forge de Maëlle, caméra dans les murs du bourg, étiquettes empilées, Garin dans une maison, disque bleu en double sur la place. **Refaire « Préparer l'essai de style »** avant de jouer (les matières des modèles à pièces sont refaites).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 24.1 | Aller à la forge de Maëlle | Le tonneau est un **tonneau seul** (bois et cercles), plus d'objets blancs | [ ] | Si un objet reste blanc : son nom, et les lignes « sans pièce à son nom » des notes de la préparation |
| 24.2 | Regarder les autres objets de la 5e vague (cuisine, décoration, nourriture, donjon, armes) | Aucun objet blanc | [ ] | Noter ceux qui le sont |
| 24.3 | Marcher dans le bourg, caméra loin (molette), tourner la vue vers les maisons | La caméra **se rapproche** devant le mur au lieu d'y entrer, puis recule en douceur une fois passée ; plus de mur sombre plein écran | [ ] | Un à-coup gênant ? Le dire |
| 24.4 | Se coller dos à un mur, caméra basse | La caméra reste au-dessus du sol, près de la tête, sans traverser | [ ] | |
| 24.5 | Devant la forge | **Un seul** « Maëlle… », les noms ne se recouvrent plus (un nom gêné monte d'un cran ou se cache ; en s'approchant, celui du plus proche gagne) | [ ] | |
| 24.6 | Carte (L) sur le bourg | Les noms des lieux lisibles ; les noms des PNJ qui se gêneraient sont cachés, **la souris sur un rond** montre son nom | [ ] | |
| 24.7 | Garin, de 12 h à 13 h puis à 22 h | Il ne traverse aucune maison (trajets vérifiés par un test du cœur) | [ ] | S'il semble encore dans une maison : laquelle, à quelle heure (c'est alors le modèle de la maison qui déborde) |
| 24.8 | Place du village (« Entrée : faire halte ») | Un seul disque bleu clair, mince, au-dessus des pavés | [ ] | |

## 25. Objets blancs ou roses, tonneau et flaques (V2.12.7)

Suite de ton essai du 9 novembre 2026 (PC modeste) : four et comptoir blancs, petits objets roses près de la maison « Herbe de l'Aube », tonneau bleu uni à la forge, flaques noires comme des trous. **Refaire « Préparer l'essai de style »** (il répare les matières ; le compte rendu dit « matières réparées »), puis **« Vérification de l'essai »**.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 25.1 | Vérification de l'essai, section « Objets blancs ou roses (V2.12.7) » | Une ligne verte « aucun objet blanc ni rose » ; sinon une ligne par matière avec le **nom du fichier** | [ ] | Envoyer les noms de fichier restants |
| 25.2 | Village, près de la maison « Herbe de l'Aube », le four en dôme | Un four **de brique** (texturé, ou brun rouge), plus blanc | [ ] | |
| 25.3 | Le comptoir ou plan de travail à côté | Du **bois** (texturé ou brun), plus blanc | [ ] | |
| 25.4 | Les petits objets de la table voisine | De vraies couleurs (pain, fromage, bouteilles…), **plus rose magenta** | [ ] | S'il en reste un : son nom (la liste du 25.1 le donne) |
| 25.5 | Forge de Maëlle | Un **tonneau de bois** brun avec des cercles de fer sombres, plus de cylindre bleu | [ ] | |
| 25.6 | Les autres tonneaux du village et du bourg | Du bois aussi | [ ] | |
| 25.7 | Pluie (touche météo de l'essai), puis regarder les chemins | Des flaques **transparentes**, bord fondu, qui reprennent la couleur du ciel, plus de disques noirs ; à l'arrêt de la pluie elles rétrécissent puis disparaissent | [ ] | Trop claires ou trop sombres ? Le dire |
| 25.8 | Même chose en mode « PC modeste » | Des flaques plus petites en nombre, mates, mais pas noires | [ ] | |
| 24.7 (à refaire) | Garin, de 12 h à 13 h puis à 22 h | Il ne traverse aucune maison | [ ] | Pas revu à cet essai : s'il semble encore dans une maison, laquelle et à quelle heure |

## 26. Mémoire au lancement et « Lancer l'essai (déjà préparé) » (V2.12.9)

Suite de ton essai du 9 novembre 2026 (PC de 4 Go, mode « PC modeste ») : après la préparation et la vérification, le passage en mode Jeu s'arrêtait sur « System out of memory » (texture). La mémoire est maintenant libérée avant le mode Jeu et pendant la vérification des objets blancs ou roses. **Sur PC modeste : prépare une fois** (« Éclats du Seuil → Préparer l'essai de style »), **ferme les autres applications, rouvre Unity**, puis utilise **« Éclats du Seuil → Lancer l'essai (déjà préparé) »** : ni préparation ni vérification complète, il ouvre la scène de démarrage et lance le mode Jeu.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 26.1 | « Préparer et lancer l'essai » | Le jeu démarre, plus de « System out of memory » | [ ] | Sinon : capture de la Console |
| 26.2 | Unity rouvert, « Lancer l'essai (déjà préparé) » | La scène de démarrage s'ouvre et le jeu démarre, sans préparation | [ ] | |
| 26.3 | Même menu sur un projet jamais préparé | Un message demande de faire d'abord « Préparer l'essai de style » | [ ] | |

## 27. Jeu plus lumineux et curseur de luminosité (V2.13)

Suite de ton retour du 9 novembre 2026 (« le jeu est trop sombre »). **Refaire « Préparer l'essai de style »** (il remet la lumière du dehors en version 3 et celle des intérieurs en version 2 ; le compte rendu le dit), puis lancer le jeu.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 27.1 | Village, en plein jour (12 h) | Image **nettement plus claire** qu'avant, couleurs vives mais pas délavées, ombres encore lisibles | [ ] | Trop clair, trop terne ? Le dire |
| 27.2 | Le même endroit au loin | Brume plus lointaine : on voit plus loin avant qu'elle ne gagne | [ ] | |
| 27.3 | Entrer dans une maison, une boutique, la taverne | Pièce **claire**, on distingue meubles et murs sans lanterne proche ; feux et bougies chauds | [ ] | |
| 27.4 | Dehors à minuit (ou aperçu de l'heure dans l'éditeur) | Nuit **bleue mais lisible** : chemins, maisons et créatures se voient, pas de noir total | [ ] | Trop sombre ou plus assez « nuit » ? Le dire |
| 27.5 | Aube et crépuscule (6 h, 20 h 30) | Transitions douces, pas plus sombres que le plein jour au point de gêner | [ ] | |
| 27.6 | Échap, « Pause et réglages » | Une ligne « Luminosité : 100 % » avec « − » et « + » sous le bouton des textures | [ ] | |
| 27.7 | Appuyer sur « + » plusieurs fois, jusqu'à 200 % | L'image s'éclaircit **en direct**, par pas de 10 %, et s'arrête à 200 % | [ ] | Délavé à 200 % ? Normal : réglage d'appoint |
| 27.8 | Appuyer sur « − » jusqu'à 50 % | L'image s'assombrit en direct, s'arrête à 50 % | [ ] | |
| 27.9 | Régler 130 %, entrer dans une maison, ressortir | Le réglage tient dehors comme dedans | [ ] | |
| 27.10 | Quitter le jeu et le relancer | La luminosité choisie est **retrouvée** dans les réglages | [ ] | |
| 27.11 | En mode « PC modeste » | Même rendu ; aucune lenteur nouvelle | [ ] | Images par seconde plus basses qu'avant ? Le dire |

## 28. Démarche réaliste (V2.15)

Suite de ton retour du 9 novembre 2026 : « la façon de marcher des personnages et des animaux manque de réalisme ». Direction décidée le même jour : réaliste, adaptée au PC (y compris « PC modeste »). Tout est de l'affichage : le combat et les places du moteur ne changent pas. Pas besoin de refaire la préparation. Regarde surtout les **pieds** (glissent-ils ?) et les **demi-tours**.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 28.1 | Le héros marche lentement, puis court, au bourg | Les pieds **ne glissent plus** : la cadence des pas suit la vitesse ; entre marche et course, le pas s'allonge peu à peu (corps humain : marche et course mêlées, pieds ensemble) | [ ] | Paladin (Mixamo) : passage marche → course net, mais cadence réglée |
| 28.2 | Lâcher la touche en pleine course | Il s'arrête en un court instant, **sans patiner sur place** ; il ne repart pas en piétinant | [ ] | |
| 28.3 | Faire demi-tour (touche arrière ou cible derrière soi) | Il **tourne** (environ une demi-seconde), plus de volte-face instantanée ; à l'arrêt, il pivote à **petits pas** | [ ] | Trop lent pour jouer ? Le dire (on règle la vitesse de rotation) |
| 28.4 | Courir en décrivant un grand virage | Le corps **penche** légèrement vers l'intérieur du virage (quelques degrés), se redresse en ligne droite | [ ] | |
| 28.5 | Monter sur le pont cintré, à l'arrêt puis en marchant | Les pieds **posés sur le tablier**, le bassin descend un peu côté pente (pas sur PC modeste : les pieds restent à plat, le corps à la bonne hauteur) | [ ] | Seulement les corps humains (Quaternius) |
| 28.6 | Regarder 30 s un groupe de villageois qui attendent | Ils ne respirent **pas ensemble** ; de temps en temps l'un croise les bras ou regarde autour, chacun à son moment ; la tête bouge un peu | [ ] | |
| 28.7 | Passer à moins de 9 m d'un PNJ ou d'un garde | Sa **tête suit** le héros (sans se tordre le cou ; s'il est derrière, il ne le suit plus) | [ ] | |
| 28.8 | Suivre un passant jusqu'au bout de son allée, et un villageois qui va au puits | Il **ralentit** avant d'arriver, se retourne à petits pas, **repart en accélérant** | [ ] | |
| 28.9 | Ferme : un cheval, un âne, une vache qui se promènent | Ils partent au pas en accélérant, **tournent en marchant** (jamais sur place), ralentissent avant de s'arrêter ; sabots et pattes sans glisser ; leur tête suit le héros proche, sinon regarde autour | [ ] | |
| 28.10 | Bêtes de la forêt (loup, sanglier, cerf, biche, ours) : les approcher pour les faire fuir ou charger | **Pas, trot, galop** selon la vitesse si le modèle en a les clips, sinon cadence réglée ; à l'arrêt elles broutent ou reniflent ; elles ne pivotent plus sur place | [ ] | Dire quelle bête n'a qu'un seul cycle (on cherchera le clip) |
| 28.11 | Cerf ou lièvre en formes simples (sans modèle) | Les pattes battent plus lentement quand il ralentit | [ ] | |
| 28.12 | Même tour en mode « PC modeste » | Tout pareil sauf les pieds sur le relief (28.5) ; pas de saccade en plus | [ ] | Images par seconde avant/après, si possible |

## 29. Créer tes personnages : MakeHuman et créateur de héros (V2.16)

Ton souhait du 9 novembre 2026 : « designer des choses nous-mêmes ». Guide complet : [CREER_PERSONNAGES.md](CREER_PERSONNAGES.md). **À essayer** (les points 29.1 à 29.5 demande MakeHuman ; sans lui, commence au 29.6).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 29.1 | Dans MakeHuman : un humain, squelette « Game engine », export FBX en mètres dans `Assets/Style/MakeHuman/Gaspard/` | Un `.fbx` et un dossier de textures | [ ] | |
| 29.2 | Unity : « Préparer l'essai de style » puis « Importer les personnages MakeHuman » | Une fenêtre de rapport : Gaspard « prêt » (ou la raison du refus), triangles et textures signalés | [ ] | Noter le rapport en entier |
| 29.3 | Ouvrir `Assets/Style/MakeHuman/PNJ.txt` (créé par le menu), écrire `gaspard = Gaspard.fbx`, relancer le menu | Plus de ligne en erreur dans le rapport | [ ] | |
| 29.4 | Jouer, aller voir Gaspard | Il a **ton** personnage, qui marche, attend et travaille (animations de Quaternius) | [ ] | Pose en T, sol, taille, regard de travers : le dire |
| 29.5 | Mettre une licence NC dans son `MANIFESTE.md`, relancer le menu | Le rapport la refuse (CC0 et CC BY seulement) | [ ] | |
| 29.6 | Nouvelle partie | La fenêtre « Ton personnage » s'ouvre, jeu en pause | [ ] | |
| 29.7 | Changer coiffure, barbe, peau, cheveux, tenue, puis Valider | Le héros change à l'écran | [ ] | Pas d'aperçu en direct : voulu |
| 29.8 | Quitter, relancer, continuer la partie | Le héros garde son aspect (sauvegarde v17) | [ ] | |
| 29.9 | Échap, Réglages, « Mon personnage… », Annuler | Rien ne change | [ ] | |
| 29.10 | Choisir le corps « Gaspard » dans le créateur | Ton héros prend le personnage MakeHuman ; les autres choix sont grisés | [ ] | |

## 30. Vraies boutiques et grand jour de marché (V2.14)

Avant l'essai : une partie déjà lancée une fois. Les menus **Éclats du Seuil → Essai des boutiques** avancent la partie (hors du mode Jeu) au prochain mercredi de marché (6 h 30, 9 h, 18 h), au jeudi (10 h), au mardi (5 h, 6 h, 19 h 15), au lundi (10 h) ou au dimanche (10 h). Le calendrier : le jour 0 est un lundi ; marché le **mercredi** et le **samedi**.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 30.1 | Au bourg, regarder les maisons des boutiques | Une **enseigne** de bois pendue à côté de la porte de la boulangerie, de l'épicerie, de l'herboristerie, de la mercerie et de l'auberge ; l'étiquette dit son nom et « ouverte », « fermée », « fermée, on répond à la porte » ou « fermée : jour de repos » | [ ] | |
| 30.2 | « mardi, 5 h » : la cheminée du fournil, puis entrer dans la boulangerie (porte au sud de la grande maison, à droite de Firmin) | De la **fumée** sort de la cheminée (Firmin pétrit depuis 2 h) ; dedans : le **four voûté** dont la bouche rougeoie, le **pétrin** et sa pâte, les étagères de pains, Bastien au comptoir ; la boutique dit « Fermé : Bastien, le commis vous répond à la porte » et « Première fournée à 5 h 30 » ; rien en stock | [ ] | |
| 30.3 | « mardi, 6 h », au comptoir | « Pains chauds, tout juste sortis du four ! » ; pain de campagne, miche de seigle, croissants, brioches en stock (un peu moins chaque heure : le bourg achète aussi) | [ ] | |
| 30.4 | « mardi, 19 h 15 » | « Tout est parti : prochaine fournée demain à 5 h 30 » ; les offres fraîches en rupture (rouge) | [ ] | |
| 30.5 | « lundi, 10 h », boulangerie | Jour de repos : Bastien répond à la porte ; le four est éteint, **rien de frais** (le pain de campagne en rupture), seulement le **pain d'épices** qui se garde ; la boutique dit la réouverture mardi à 5 h 30 ; pas de fumée | [ ] | |
| 30.6 | « dimanche, 10 h », chez Garin puis chez Bertille | « Boutique fermée : jour de repos. Réouverture lundi à 5 h. » ; leur dialogue s'ouvre quand même ; Sauge, elle, reste ouverte (jour et nuit, comme décidé le 30 octobre) | [ ] | |
| 30.7 | Entrer dans l'épicerie, l'herboristerie, la mercerie | Un comptoir, des étagères garnies (rouleaux de toile à la mercerie, rouet) ; les employés nommés : Lucie (épicerie), Tiphaine (herboristerie), Margot et Jeannot à la table de coupe (mercerie), chacun avec sa réplique et « range la boutique » hors de ses heures ; F au comptoir ouvre la boutique (fermée, elle le dit) | [ ] | |
| 30.8 | « mercredi, 6 h 30 », place du bourg | Les **étals s'installent** (tables, tentes, sans marchandises) ; Gisèle la fromagère et Ilario, marchand d'épices, sont là mais ne vendent pas encore ; le bandeau dit « Jour de marché : les étals s'installent » | [ ] | |
| 30.9 | « mercredi, 9 h » | Marchandises sur les huit étals, **foule** plus dense, **criées** dans le bandeau quand tu es sur la place ; fromages et miel chez Gisèle, épices et fruits du Sud chez Ilario, Anseau le colporteur ; potions et herbes à 90 % chez Sauge et Bertille | [ ] | |
| 30.10 | Chez Gisèle, avec une réputation « appréciée » auprès des villageois | Le bouton **Marchander** : accepté (15 % de moins pour la journée) ou refusé (« c'est mon dernier prix ») ; redemander ne change rien ce jour-là ; « honoré » ou « héros » : plus de chances ; neutre : pas de bouton | [ ] | |
| 30.11 | « mercredi, 18 h » puis 20 h | On démonte : tables sans marchandises, plus de foule ; à 20 h la place est vide, Gisèle et Ilario repartis | [ ] | |
| 30.12 | « jeudi, 10 h » | Place calme ; le bandeau de la boutique ou du lieu annonce le prochain marché (samedi) | [ ] | |
| 30.13 | La forge de Maëlle | Un **atelier couvert** (poteaux et toit d'appentis) au-dessus de l'enclume et de l'établi | [ ] | |
| 30.14 | Une quête en cours (Firmin, Sauge, Bertille) un jour de repos ou la nuit | La quête n'est jamais bloquée : le PNJ parle ; ce qu'une quête demande d'acheter se trouve toujours (Sauge jour et nuit, ou à la porte) | [ ] | |
| 30.15 | Ton avis : horaires, jours de repos, fournées, prix du pain, jours et heures du marché, marchandage, enseignes, fumée, foule | — | [ ] | |

## 31. La boulangerie de Firmin, notre premier bâtiment (V2.17)

Ta décision du 9 novembre 2026 : « créer nous-mêmes les modèles 3D ». Avant l'essai : récupérer la branche avec les fichiers de `unity/Assets/EdS/Modeles/Batiments/maison2/` (`boulangerie_firmin.fbx`, `boulangerie_firmin_couleur.jpg`, `boulangerie_firmin_normale.png`). Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 31.1 | Unity : **Éclats du Seuil → Préparer l'essai de style** | Dans le rapport : « Bâtiments de l'atelier : maison2 (boulangerie_firmin, 14,0 × 10,6 m) » (à peu près), aucune ligne « Atelier : … introuvable » | [ ] | Noter la ligne en entier |
| 31.2 | Relancer le menu une deuxième fois | Même rapport, pas d'erreur ; `Assets/Style/Generated/boulangerie_firmin.mat` existe une seule fois | [ ] | |
| 31.3 | Jouer, aller au bourg des Vallons, devant la grande maison de Firmin | **Notre boulangerie** à la place de l'ancienne maison2 (plus le modèle de Sketchfab ni la maison faite par le code) | [ ] | |
| 31.4 | Regarder l'enseigne peinte | Elle se **lit à l'endroit** (pas en miroir) | [ ] | |
| 31.5 | Regarder la porte | La porte est **face au chemin**, au sud ; la porte d'entrée du jeu (touche pour entrer) tombe bien sur elle | [ ] | Écart en mètres : le dire |
| 31.6 | Faire le tour du bâtiment, caméra basse et haute | **Pas de trou** (mur ou toit manquant, faces invisibles de dos), pas de bâtiment qui flotte ou s'enfonce ; on ne bute pas contre un mur invisible à plus d'un pas du mur ; le bâtiment s'efface quand il cache le héros | [ ] | |
| 31.7 | Regarder de près, de jour puis de nuit | Couleurs et relief (pierres, tuiles, bois) nets, pas de matière rose ou blanche | [ ] | |
| 31.8 | Sur ton PC modeste (qualité des textures « auto ») | Le jeu tient : pas de « out of memory », images par seconde comme avant au bourg | [ ] | Noter les images par seconde |
| 31.9 | L'enseigne et la lanterne, de nuit ; la fumée du fournil (« mardi, 5 h », 30.2) | Plus d'enseigne de bois ni de lanterne faites par le code (celles du modèle seulement) ; la nuit, la lanterne du modèle **éclaire** à droite de la porte ; la fumée sort **de la cheminée du modèle** | [ ] | Écart en mètres : le dire |
| 31.10 | Ton avis : proportions, style, couleurs, ce qu'il faut pour le bâtiment suivant | — | [ ] | |

## 32. La nouvelle boulangerie de Firmin (V2.18)

Ton modèle validé le 10 novembre 2026 remplace celui de la V2.17. Avant l'essai : récupérer `main` à jour (fichiers de `unity/Assets/EdS/Modeles/Batiments/maison2/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 32.1 | Lire le rapport du menu | « Bâtiments de l'atelier : maison2 (boulangerie_firmin, 15,7 × 12,9 m) » (à peu près), aucune ligne « introuvable » ni « descend de … sous son pivot » | [ ] | Noter la ligne |
| 32.2 | Jouer, aller devant la boulangerie au bourg des Vallons | La **nouvelle** boulangerie (boutique à colombages à gauche, fournil à droite), **façade vers le chemin**, la porte du jeu sur sa porte | [ ] | Écart en mètres : le dire |
| 32.3 | Regarder l'enseigne | Elle se **lit à l'endroit** (« Boulangerie »), à gauche de la vitrine | [ ] | |
| 32.4 | Faire le tour, caméra basse | **Posée au sol** : ni flottante ni enfoncée ; le dôme du four, à l'arrière, ne s'enfonce plus sous terre ; pas de trou ; **pas déformée** (échelle 1 : le toit, l'enseigne et l'appentis du four dépassent un peu de l'emprise, c'est voulu), la façade au bord du chemin | [ ] | |
| 32.5 | « Mardi, 5 h » (fournée) | La fumée sort **de la cheminée du fournil** (à droite, vers l'arrière), pas à côté | [ ] | Écart en mètres : le dire |
| 32.6 | De nuit, devant la porte | La lumière est **sur la lanterne**, à droite de la porte | [ ] | |
| 32.7 | Ton avis : proportions, débords du toit et de l'appentis sur les alentours, couleurs, relief | — | [ ] | |

## 33. L'auberge et la chapelle de l'atelier (V2.19)

Tes modèles du 10 novembre 2026 remplacent l'auberge et la chapelle des Vallons. Avant l'essai : récupérer `main` à jour (dossiers `unity/Assets/EdS/Modeles/Batiments/auberge/` et `chapelle/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 33.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `auberge` (auberge_vallons) et `chapelle` (chapelle_vallons), aucune ligne « introuvable » | [ ] | Noter la ligne |
| 33.2 | Aller devant l'auberge, au bourg | La **nouvelle** auberge (trois niveaux, abri à chevaux à droite), **porte face au chemin**, sur la porte du jeu ; le panneau « Auberge » se lit à l'endroit | [ ] | Écart en mètres : le dire |
| 33.3 | Aller devant la chapelle | La chapelle de pierre, **façade (petit côté) et porte face au chemin**, le muret du parvis ouvert devant la porte | [ ] | |
| 33.4 | Faire le tour des deux, caméra basse | **Posées au sol**, ni flottantes ni enfoncées, pas déformées ; bancs et muret dépassent un peu vers le chemin, c'est voulu | [ ] | |
| 33.5 | Regarder la chapelle de loin, depuis la place | La **flèche** et sa croix se voient **au-dessus de l'auberge** | [ ] | |
| 33.6 | Auberge ouverte (de 6 h à 1 h) | La fumée sort **de la cheminée de la cuisine** (à droite, sur le pan arrière), pas de l'autre ; aucune fumée sur la chapelle | [ ] | Écart en mètres : le dire |
| 33.7 | De nuit, devant l'auberge | La lumière est **sur la lanterne, au-dessus de la porte** | [ ] | |
| 33.8 | Ton avis : proportions, couleurs, relief (normales en 1024) | — | [ ] | |

## 34. L'épicerie de Bertille (V2.20)

Le modèle de l'atelier (fiche technique et ta décision du 10 novembre 2026) remplace l'épicerie du bourg (`maison4`). Avant l'essai : récupérer `main` à jour (dossier `unity/Assets/EdS/Modeles/Batiments/maison4/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 34.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `maison4` (epicerie_vallons), aucune ligne « introuvable » | [ ] | Noter la ligne |
| 34.2 | Aller devant l'épicerie, au bourg | La **nouvelle** épicerie (rez de pierre, étage à colombages, auvent de tuiles), **façade et porte face à la porte du jeu** ; l'enseigne « Épicerie » se lit à l'endroit | [ ] | Écart en mètres : le dire |
| 34.3 | Passer sous l'auvent jusqu'à la porte | Le **passage central est libre** (aucun poteau, étal ni produit devant la porte) | [ ] | |
| 34.4 | Regarder les étals | Légumes à gauche ; à droite le comptoir avec **meules de fromage, pots de miel, sacs d'épices**, les **jambons** pendus ; **tresses d'ail et d'oignons** sous l'auvent ; le **tonneau de sel** au bout à droite | [ ] | |
| 34.5 | Faire le tour, caméra basse | **Posée au sol**, ni flottante ni enfoncée, pas déformée ; l'auvent et les sacs dépassent un peu vers la porte du jeu, c'est voulu | [ ] | |
| 34.6 | Épicerie ouverte (de 5 h à 22 h) puis la nuit | **Aucune fumée** sur sa cheminée (pas de feu de service) ; la nuit, une lumière près de la porte | [ ] | Veux-tu un feu d'âtre ? Le dire |
| 34.7 | Ton avis : proportions, couleurs, relief (normales en 1024), produits | — | [ ] | |

## 35. L'herboristerie de Sauge et l'épicerie retouchée (V2.21)

Le modèle de l'atelier remplace l'herboristerie du bourg (`maison7`) ; l'épicerie (`maison4`) reçoit sa lanterne et sa fumée (ta décision du 10 novembre 2026 au soir). Avant l'essai : récupérer `main` à jour (dossiers `unity/Assets/EdS/Modeles/Batiments/maison7/` et `maison4/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 35.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `maison7` (herboristerie_sauge) et `maison4` (epicerie_vallons), aucune ligne « introuvable » | [ ] | Noter la ligne |
| 35.2 | Aller devant l'herboristerie, à l'ouest du bourg | La **nouvelle** herboristerie (plain-pied de pierre, pignon à colombages), **façade et porte face à la porte du jeu** ; l'enseigne « Sauge » se lit à l'endroit | [ ] | Écart en mètres : le dire |
| 35.3 | Faire le tour, caméra basse | **Posée au sol**, pas déformée ; le **jardin** (plates-bandes) à droite de la maison vue de la porte dépasse un peu de l'emprise, c'est voulu | [ ] | |
| 35.4 | De jour, puis la nuit, devant Sauge | Sa **lanterne éclaire jour et nuit** ; **aucune fumée** sur sa cheminée | [ ] | Veux-tu un feu pour ses tisanes ? Le dire |
| 35.5 | Devant l'épicerie, la nuit | La **lanterne de fer** sous l'auvent, à droite de la porte, **allumée** ; le passage devant la porte reste libre | [ ] | |
| 35.6 | Épicerie ouverte (de 5 h à 22 h), puis après 22 h | La **cheminée fume** pendant l'ouverture, **plus du tout** après la fermeture et le jour de repos | [ ] | |
| 35.7 | Ton avis : proportions, couleurs, plantes (cuites dans l'atlas en LOD1) | — | [ ] | |

## 36. La mercerie, la maison de la Guilde et le feu de tisanes de Sauge (V2.22)

Les modèles de l'atelier remplacent la mercerie (`maison1`) et la maison de la Guilde (`maison6`) ; l'herboristerie de Sauge reçoit son feu de tisanes (ta décision du 10 novembre 2026 au soir). Avant l'essai : récupérer `main` à jour (dossiers `unity/Assets/EdS/Modeles/Batiments/maison1/` et `maison6/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 36.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `maison1` (mercerie_vallons) et `maison6` (guilde_vallons), aucune ligne « introuvable » | [ ] | Noter la ligne |
| 36.2 | Aller devant la mercerie, sur la grand-rue | La **nouvelle** mercerie (vitrines d'étoffes et de rubans, enseigne en ciseaux), **façade et porte face à la porte du jeu** | [ ] | Écart en mètres : le dire |
| 36.3 | Devant la mercerie, la nuit | La **lanterne murale**, à droite de la porte, **allumée** ; **aucune fumée** sur la cheminée | [ ] | |
| 36.4 | Aller devant la maison de la Guilde, à l'ouest | La **nouvelle** Guilde (rez de pierre, étage à colombages, blason, deux bannières), **façade face à la porte du jeu** ; le **perron** sort un peu vers la porte, c'est voulu | [ ] | Le perron gêne-t-il l'entrée ? Le dire |
| 36.5 | Faire le tour des deux, caméra basse | **Posées au sol**, ni flottantes ni enfoncées, pas déformées ; aucune fumée sur les deux cheminées de la Guilde | [ ] | |
| 36.6 | Devant Sauge, de jour puis la nuit | Une **fumée légère** monte de sa cheminée **à toute heure** ; sa lanterne éclaire jour et nuit, même avec d'autres lanternes plus proches | [ ] | Fumée trop forte ou trop faible : le dire |
| 36.7 | Ton avis : proportions, couleurs, relief (normales en 1024) | — | [ ] | |

## 37. La maison de Rosalie et les maisons 3, 8, 9 et 10 (V2.23)

Les modèles de l'atelier remplacent la maison de Rosalie (`maison5`) et les maisons `maison3`, `maison8`, `maison9` et `maison10`. Avant l'essai : récupérer `main` à jour (dossiers `unity/Assets/EdS/Modeles/Batiments/maison3/`, `maison5/`, `maison8/`, `maison9/`, `maison10/`), puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 37.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `maison3`, `maison5` (maison_rosalie_vallons), `maison8`, `maison9` et `maison10`, aucune ligne « introuvable » | [ ] | Noter la ligne |
| 37.2 | Aller devant la maison de Rosalie, sur la grand-rue ouest | La **nouvelle** maison (œillets aux fenêtres, colombages), **porte face à la porte du jeu** ; le **potager** et sa clôture à droite (à l'est) ; le **chat roux** couché sur l'appui de la première fenêtre du mur droit | [ ] | Écart en mètres : le dire |
| 37.3 | Marcher dans le potager de Rosalie | Le potager sort de 1,4 m de l'emprise : on le **traverse** (pas de collision sur la clôture) | [ ] | Gênant ? Le dire |
| 37.4 | Maisons 3 (grand-rue), 8 (à l'ouest de Rosalie), 10 (chemin de l'est) | Façades vers la rue, **posées au sol**, ni flottantes ni enfoncées, pas déformées | [ ] | |
| 37.5 | Maison 9, au bout du chemin du lac | Façade vers l'est ; l'**appentis** (fourche, râteau, panier) au sud sort de 1,5 m de l'emprise, à 1,7 m du chemin des champs | [ ] | Trop près du chemin ? Le dire |
| 37.6 | Les cinq maisons, la nuit | **Aucune fumée** ; une lanterne de porte à droite de la porte (celle du jeu, pas du modèle) | [ ] | |
| 37.7 | Ton avis : proportions, couleurs, relief (normales en 1024) | — | [ ] | |

## 38. Grange, moulin, tour, puits, étals, camp ; LOD1 refaits (V2.25)

Les modèles de l'atelier remplacent la grange, le moulin (et la roue à eau de l'Asset Store), la tour de guet, le puits, les deux étals, les trois tentes et les deux palissades du camp ; les LOD1 des bâtiments déjà livrés sont refaits (un mur entier y manquait). Avant l'essai : récupérer `main` à jour, puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »).

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 38.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `grange`, `moulin`, `tour`, `puits`, `etal1`, `etal2`, `tente`, `palissade_camp1`, `palissade_camp2` ; « moulin : roue » et « moulin : eau » ; aucune ligne « introuvable » ni « sans bâtiment » | [ ] | Noter la ligne |
| 38.2 | Faire le tour de l'auberge, de la chapelle, de l'épicerie, de l'herboristerie, de la mercerie, de la Guilde et des maisons 3, 5, 8, 9, 10 | **Aucun mur manquant** (avant : un pan de mur entier absent, on voyait l'intérieur) | [ ] | Le bâtiment et le côté : le dire |
| 38.3 | Aller au moulin (chemin du moulin) | Façade vers le chemin (ouest) ; la **roue tourne doucement** au sud, aubes basses dans le sens de l'eau du bief, qui **coule vers la rivière** (est) ; ni l'ancien moulin ni l'ancienne roue | [ ] | Sens ou vitesse faux : le dire |
| 38.4 | Regarder la roue de près | Elle tourne **autour de son axe** (pas de balancement, pas de décalage avec le palier) | [ ] | Si elle tourne de travers : le dire |
| 38.5 | Grange (ouest, chemin des champs) | Façade et porte charretière **au sud** (côté bourg), pignon vers le chemin | [ ] | Préférer une autre face ? |
| 38.6 | Tour de guet, puits, étals de la place | Posés au sol, pas déformés ; auvents des étals et toit du puits un peu hors de l'emprise (voulu) | [ ] | |
| 38.7 | Camp des rôdeurs | Trois tentes identiques tournées vers leur route ; palissade ouest (16 m) et sud (23 m) face rugueuse dehors ; **portail fermé** à x ≈ 100 sur la palissade sud | [ ] | Où mettre le portail, faut-il l'ouvrir ? |
| 38.8 | Ton avis : proportions, couleurs, relief | — | [ ] | |

## 39. Pierres du Seuil, barrière du moulin, ruines, carrière (V2.25.2)

Les modèles de l'atelier remplacent les menhirs, la porte du moulin, les murs et les piliers des ruines, le front de taille et les six blocs. Avant l'essai : récupérer `main` à jour, puis **Éclats du Seuil → Préparer l'essai de style**. Détail : [UNITY.md](UNITY.md) (« L'atelier »). Rien de tout cela n'a encore été vu dans Unity.

| # | À faire | Ce que tu dois voir | OK | Remarque |
|---|---|---|---|---|
| 39.1 | Lire le rapport du menu | « Bâtiments de l'atelier » cite `pierre_seuil1` à `3`, `porte_moulin`, `mur_nord`, `mur_sud`, `pilier1` à `4`, `front_de_taille`, `bloc1` à `6` ; « Décor de l'atelier » cite cailloux, touffe, chèvre, traîneau, outils, tas d'éclats ; lignes « émission » pour les trois menhirs | [ ] | Noter la ligne |
| 39.2 | Aller au Seuil (nord-est), de jour | Trois menhirs gravés, **faces gravées tournées vers le milieu** du triangle ; un cercle de cailloux autour ; touffes pâles au pied ; plus les menhirs de DragonJojo | [ ] | Une face tournée de travers : laquelle ? |
| 39.3 | Même endroit, la nuit | Les fissures et les éclats **luisent** bleu-violet pâle, nettement, sans halo autour | [ ] | Trop fort, trop faible ? |
| 39.4 | La porte du moulin, quête pas faite | Deux poteaux, deux vantaux **fermés**, chaîne et cadenas | [ ] | |
| 39.5 | Faire poser la ferrure par Maëlle | Les vantaux **s'ouvrent doucement vers le moulin** (environ 3 s), la chaîne pend, cadenas ouvert ; après la halte, on passe et la barrière reste ouverte | [ ] | S'ouvrent du mauvais côté ? |
| 39.6 | Les ruines (à l'est de la rivière) | Deux murs en ruine (baie en arc, départ de voûte), débris au pied ; plus les tours de l'Asset Store | [ ] | |
| 39.7 | Les piliers à l'ouest du moulin | Quatre piliers brisés de hauteurs différentes ; **plus d'abri** sur eux | [ ] | Garder l'abri ailleurs ? |
| 39.8 | La carrière | Front à gradins face à l'ouest, blocs, chèvre de levage, traîneau, outils et tas d'éclats devant le front, rien sur les chemins ni sur le filon | [ ] | |
| 39.9 | Ton avis : proportions, couleurs | — | [ ] | |

