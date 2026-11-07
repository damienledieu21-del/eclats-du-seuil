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
