# Licences des fichiers de l'interface

Essai réaliste 5 (choix de Damien du 27 octobre 2026 : interface façon Diablo 4, police et icônes gratuites, téléchargements acceptés).

| Fichier | Source | Licence |
| --- | --- | --- |
| `Resources/EdsInterface/Cinzel.ttf` | [Cinzel](https://github.com/NDISCOVER/Cinzel) de Natanael Gama (The Cinzel Project Authors), pris sur [Google Fonts](https://github.com/google/fonts/tree/main/ofl/cinzel) | SIL Open Font License 1.1 : texte complet dans `Cinzel-OFL.txt` |
| `Resources/EdsInterface/Icones/*.png` | [game-icons.net](https://game-icons.net) : *broadsword*, *shouting*, *sword-clash*, *checked-shield*, *quake-stomp*, *crenulated-shield*, *bordered-shield*, *shield-reflect*, *sword-spin* de **Lorc** ; *shield-bash* de **Delapouite** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) : auteurs cités ici et dans `docs/CREDITS.md` ; icônes converties en PNG blanc sur fond transparent (128 × 128) |

Correspondance des icônes : frappe du rempart (*broadsword*), heurt de bouclier (*shield-bash*), provocation (*shouting*), contrecoup (*sword-clash*), barrière d'acier (*checked-shield*), onde de rupture (*quake-stomp*), Bastion absolu (*crenulated-shield*), garde (*bordered-shield*), parade (*shield-reflect*), autre compétence (*sword-spin*).

## Essai réaliste 6 : interface « Fragments du Seuil »

Choix de Damien du 27 octobre 2026 (« je veux vraiment le meilleur »), identité créée pour le jeu. Les dessins de `Resources/EdsInterface/Hud` (sceau, éclats, cadres, carte, repères, voiles, reflets) sont faits pour le jeu (maquettes exportées en PNG transparents, deux fois la taille d'affichage) : aucune licence tierce.

| Fichier | Source | Licence |
| --- | --- | --- |
| `Resources/EdsInterface/CinzelDecorative-Bold.ttf` | [Cinzel Decorative](https://github.com/NDISCOVER/Cinzel) de Natanael Gama, pris sur [Google Fonts](https://github.com/google/fonts/tree/main/ofl/cinzeldecorative) | SIL Open Font License 1.1 : texte complet dans `CinzelDecorative-OFL.txt` |
| `Resources/EdsInterface/BarlowCondensed-SemiBold.ttf`, `BarlowCondensed-Bold.ttf` | [Barlow Condensed](https://github.com/jpt/barlow) de Jeremy Tribby (The Barlow Project Authors), pris sur [Google Fonts](https://github.com/google/fonts/tree/main/ofl/barlowcondensed) | SIL Open Font License 1.1 : texte complet dans `BarlowCondensed-OFL.txt` |
| `Resources/EdsInterface/Peintes/` : compétences (`frappe_du_rempart`, `heurt_de_bouclier`, `barriere_d_acier`, `onde_de_rupture`, `bastion_absolu`, `provocation`, `contrecoup`), `garde`, `parade`, les 18 talents (`t_*`), `herbe_de_l_aube`, `minerai_de_fer` | [« Painterly Spell Icons »](https://opengameart.org/node/4413) parties 1 à 4 de **J. W. Bjerk (eleazzaar)**, www.jwbjerk.com/art, sur OpenGameArt | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) (l'auteur propose CC BY 3.0, CC BY-SA 3.0, GPL 2.0 et GPL 3.0 ; le jeu retient CC BY 3.0) ; réduites à 128 × 128 |
| `Resources/EdsInterface/Peintes/` : `epee_du_milicien`, `epee_de_garde`, `lame_du_seuil`, `ecu_de_chene`, `pavois_du_moulin`, `cotte_de_mailles`, `amulette_de_l_aube`, `talisman_du_seuil`, `potion_de_soin`, `remede_de_l_aube`, `eclat_de_seuil`, `fleche_brisee`, `sac`, `journal`, `quete` | [« Handpainted RPG icons »](https://opengameart.org/content/handpainted-rpg-icons) de **LudicArts** (Franco Giachetti, ludicarts.com), sur OpenGameArt | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) ; version 128 × 128 du pack |
| `Resources/EdsInterface/Peintes/` : `coffre`, `gambison`, `marteau_du_carrier`, `rondache_cerclee` | [« Fantasy RPG Icons »](https://opengameart.org/content/fantasy-rpg-icons-0) de **Drummyfish**, sur OpenGameArt | CC0 ; agrandies à 128 × 128 |
| `Resources/EdsInterface/Peintes/` : `defense_de_sanglier`, `ferrure`, `peau_de_loup` | [game-icons.net](https://game-icons.net) : *boar-tusks* et *metal-bar* de **Lorc**, *animal-hide* de **Delapouite** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) : silhouettes peintes par le code (dégradé, relief), 128 × 128 |

Les icônes de l'essai 5 (`Resources/EdsInterface/Icones`) ne sont plus affichées par l'interface en jeu ; elles restent dans le projet jusqu'à l'étape 2 de l'essai 6 (écrans), qui les retirera avec l'ancienne interface IMGUI.

## Essai réaliste 7 : sons de l'interface et particules

Demandes de Damien du 28 octobre 2026 (« sons d'interface libres : clic, compétence prête, charge gagnée », « effets […] en particules faites dans Unity »).

| Fichier | Source | Licence |
| --- | --- | --- |
| `Resources/EdsInterface/Sons/` : `clic`, `ouverture`, `fermeture`, `onglet`, `competence_prete`, `charge`, `talent`, `retrait`, `valide`, `erreur` | [« Interface Sounds »](https://kenney.nl/assets/interface-sounds) de **Kenney** (Kenney Vleugels, www.kenney.nl) | CC0 (texte de licence de Kenney : usage personnel et commercial permis, crédit apprécié mais pas obligatoire) ; renommés |
| `Resources/EdsInterface/Sons/survol.ogg` | [« UI Audio »](https://kenney.nl/assets/ui-audio) de **Kenney** | CC0 ; renommé |
| `Resources/EdsInterface/Sons/niveau.ogg`, `quete.ogg` | [« Music Jingles »](https://kenney.nl/assets/music-jingles) de **Kenney** (*jingles_STEEL02*, *jingles_PIZZI02*) | CC0 ; renommés |
| `Resources/EdsInterface/Particules/*.png` | [« Particle Pack »](https://kenney.nl/assets/particle-pack) de **Kenney** (avec les modèles de filtres d'Indigo Ray, Craig Nisbet, Zoltan Erdokovy, Heliagon, ThreeDee, Killst4r et Tim2501) | CC0 ; réduites de 512 × 512 à 128 × 128 et renommées |


## Ressources de l'essai de style sous licence CC BY (non suivies par git)

Relecture de la PR #114 : `docs/CREDITS.md` renvoie ici pour les auteurs à citer. Les fichiers suivants sont déposés chez Damien dans `unity/Assets/Style` (non suivis par git) ; leur licence demande de citer l'auteur là où le jeu est montré. Liste complète (CC0 compris) : [docs/CREDITS.md](../../../../docs/CREDITS.md).

| Ressource | Auteur | Licence |
| --- | --- | --- |
| Musiques « Enchanted Festival », « Woodland Fantasy », « Heroic Demise » (essai réaliste 7) | **Matthew Pablo**, www.matthewpablo.com (OpenGameArt) | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| Musiques « Into The Wilds », « The Legend of Kvothe », « Sanctum » (V2.9) | **Scott Buckley**, www.scottbuckley.com.au | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Low-poly animated rabbit » (lièvre, V2.9) | **Pneshik** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Low Poly Wild Boar » (sanglier, V2.9) | **Hanabi_Map** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Stripe the Cat Rigged and Animated » (Pain-d'Épice, V2.9) | **DreamNoms** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Deer-red » (cerf élaphe, V2.9) | **Suiteofart** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Realistic Animated Elk 3D Model » (élan, V2.9) | **WildMesh_3D** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Fawn_A3 » (biches, V2.9) | **Assets_Animated** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Wild Boar - Male » (sanglier, V2.9) | **Dakta.Grower.Nzl** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Boar » et « Wild Boar » (sangliers essayés, non retenus, V2.9) | **anru97580** et **emptypen2477** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Hare (animated) » (lièvre, V2.9) | **bluekiwi.dog** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Grey Wolf (Rigged and Animated) » (V2.9, non retenu) | **rhcreations** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Animated Wolf Scene » (loup, V2.9) | **roo3d** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Crow » (corbeaux, V2.9) | **alexanders823** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Farm Cow Animated Dairy cattle » (vaches, V2.9) | **rukh3d** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Animated Dog, Shiba Inu » (chiens, V2.9) | **quander** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Horse » (chevaux, V2.9) | **henry_7** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Wolves howling (spontaneous and free) » (hurlements, V2.9) | **Sacha.Julien** (Freesound n° 762242) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Red Deer Stag Bellowing » (brame, V2.9) | **naturenotesuk** (Freesound n° 757773) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Small Animal Running on Grass » (lièvre qui détale, V2.9) | **JiggleSticks** (Freesound n° 635012) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « 220718_2057_FR_ChurchBell » (cloche d'église, V2.9) | **kevp888** (Freesound n° 646519) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Realistic Animated Doe », « Realistic Animated Donkey », « Deer Animated Cel Shading », « Realistic Animated Bear » (biche, âne, cerf, ours, V2.9.2) | **WildMesh_3D** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « ANIMAL & FOOD Chicken Model (CS2) » (poules, V2.9.2) | **kill6lucius** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Handpainted Rooster » (coq, V2.9.2) | **rukh3d** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Cow NPC » (vaches, V2.9.2) | **nataliekirk** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Free Shar Pei Animated Dog » (chiens, V2.9.2) | **artsiom** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Falun Tower Bell Hit Close » (tocsin, V2.9.2) | **Kristoffer_Andersson** (Freesound n° 760390) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Summer Rain, garden / metal roof » (pluie, V2.9.2) | **newlocknew** (Freesound n° 836064) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « diamond peak stream 02 » (ruisseau, V2.9.2) | **tim.kahn** (Freesound n° 164895) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « FR WindyForest » (vent, V2.9.2) | **kevp888** (Freesound n° 593439) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| « Lakeside Ambience 06 » (bord du lac, V2.9.2) | **Soundrack** (Freesound n° 733274) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| Musique « Fantasy Music - Night Town » (le bourg la nuit, V2.9.2) | **Angus Macnaughton** 2014 (OpenGameArt) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| Musiques « Determined Pursuit » (**Emma_MA**) et « The Old Tower Inn », « King's Feast » et « Minstrel Dance » (**RandomMind**, V2.9.2 et V2.10), corps et animations de **Quaternius** (V2.9.2), vêtements *Modular Character Outfits - Fantasy* de **Quaternius** (V2.9.4) | OpenGameArt, quaternius.com | CC0 (cités par courtoisie) |

### 5e vague (V2.11)

| Ressource | Auteur | Licence |
| --- | --- | --- |
| [« Animated Wolf »](https://sketchfab.com/3d-models/0d82257ee1454f87a05458980e43f1b2) (Croc-Gris et le loup blanc (grand loup animé)) | **emptypen2477** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Armored Guard Knight Rig »](https://sketchfab.com/3d-models/b7bba7eddb13470b88eb8b24531cb6b3) (Galeran en armure (souvenir de la Rupture)) | **SuperKapoo913** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Armored King »](https://sketchfab.com/3d-models/e3978fb660cf47278485d44eeba488ef) (Galeran en armure (souvenir de la Rupture)) | **Calviking073** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval helmet »](https://sketchfab.com/3d-models/b2f9c198e30043f09170fa4eef12733a) (Heaume de Galeran) | **bingiz** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« medieval sword »](https://sketchfab.com/3d-models/da574cba504e4b83a3293f3d3bd067fb) (Épée en main (corps sans épée)) | **lowseb** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Halfcape Soft »](https://sketchfab.com/3d-models/c2a7e27288aa4c46b4ab910696eea112) (Cape dans le dos (demi-cape)) | **madsstensrud** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Menhirs »](https://sketchfab.com/3d-models/12340f47ccd74aa6a87e25ad692fab10) (Pierres du Seuil (menhirs aux runes)) | **DragonJojo** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Glowing Crystals »](https://sketchfab.com/3d-models/cb97c1da9c284f77b1fd0842bd0b26ed) (Cristaux lumineux (cœur du Seuil, failles)) | **ked182** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Strange Crystal »](https://sketchfab.com/3d-models/88da8269bc5a4c76ab420585eb9d622e) (Grand cristal du cœur du Seuil) | **SebastianSosnowski** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Magic Portal »](https://sketchfab.com/3d-models/dc447f04768a4bfe8932d976bd2b1677) (Portail autour des failles ouvertes) | **nickbroad** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Wisp soul »](https://sketchfab.com/3d-models/f4fc937e7e1f4afeb59e2b0ac1c41855) (Feu follet du cœur du Seuil) | **seyXR** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Brick Bread Oven »](https://sketchfab.com/3d-models/f65f1a343b5347caaeb204d2c023667f) (Four à pain de Firmin) | **torricane** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Sack of Flour Low Poly »](https://sketchfab.com/3d-models/8122a0e6a8ad4971a8ce237dbfda5642) (Sacs de farine du fournil) | **gokhan.bayhan44** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Flour sack »](https://sketchfab.com/3d-models/1080516fda8a4b91a5947c2b16948cb9) (Sacs de farine du fournil) | **rozalia.skvarkova** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval_Bread_Loaf_FBX »](https://sketchfab.com/3d-models/6c974828b9f4427bb4460287041c7f5f) (Pains du fournil et des étals) | **GetDeadEntertainment** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« (Game Ready Free) Bread Loaf »](https://sketchfab.com/3d-models/daf4801f72ae4f9eba9c338afbbb2926) (Pains du fournil et des étals) | **Igrodev** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« French baguette »](https://sketchfab.com/3d-models/b6c483995db04698bf5041c9cef369c0) (Pains du fournil et des étals) | **encoded01** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Sliced B Read FBX »](https://sketchfab.com/3d-models/86516676fcac4e4e85d397b6aea561cc) (Pains du fournil et des étals) | **GetDeadEntertainment** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Pie »](https://sketchfab.com/3d-models/951f96e462b14ea79cefb31602a3a3fd) (Croissants, gâteaux et tourte du fournil) | **rudolfs** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Crate of Pears (Game Ready / 4K PBR) »](https://sketchfab.com/3d-models/f4c3ab39ce8b429082a6f0f598e6b2cf) (Caisse de poires de Bertille) | **meerschaumdigital** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Scarecrow »](https://sketchfab.com/3d-models/dd66376c24f246f28b10a67f047b8d6a) (Épouvantail des champs) | **baardus** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Hay bales »](https://sketchfab.com/3d-models/d6e087f9a2a9416c94918f0503943c17) (Bottes de foin des champs) | **zbrojmistrz** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Cart »](https://sketchfab.com/3d-models/5dedd347d77943729475486f0d1d05e8) (Charrette du hameau) | **Peter.Nox** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Colorful Festival Bunting Banner »](https://sketchfab.com/3d-models/f8f0f6dcad85423bbbf2249bdd18bad7) (Guirlande de fête sur la place) | **nadiya69** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Hanging Banner »](https://sketchfab.com/3d-models/74d14ebdbbc94d8780f03df21cd93674) (Bannières de fête sur la place) | **awenram** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Spider animated character »](https://sketchfab.com/3d-models/746f5c5173ce4f47ad0970b62ee4106e) (Araignée géante) | **TheGameAssets** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Animated Spider »](https://sketchfab.com/3d-models/af87017501fc44e39a33c220f2435100) (Araignée géante) | **shadocks** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Black Rat ( Free download ) »](https://sketchfab.com/3d-models/3db3acb4140d4de8bd62a171212bad9c) (Rat) | **Nestaeric** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Bat Attack »](https://sketchfab.com/3d-models/c6abf0e73f904f959522f7d9da6891a7) (Chauve-souris) | **LostModels2025** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Skeleton Warrior - Animated Low-Poly Enemy »](https://sketchfab.com/3d-models/dfeb342871204ecba0ae25f48c487738) (Squelette guerrier) | **Arsyuta777** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Skeleton (Animated) »](https://sketchfab.com/3d-models/bd1de4a8153a44fe93e728ca8e06701b) (Squelette (prise longue découpée)) | **one_boy** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Lich »](https://sketchfab.com/3d-models/4ad4ece50273473ba1d843797b8f4fa6) (Liche) | **Spinnee** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Ghost »](https://sketchfab.com/3d-models/8fc07a30ce9f45d5a10ef229f2ece730) (Fantôme (base pour le shader EdS/Echo)) | **csbr** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Troll »](https://sketchfab.com/3d-models/bceb1cb47186457c8571a23e6aff1b8e) (Troll) | **sjunior3d** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Ogre Character »](https://sketchfab.com/3d-models/676c3e1fb1154768b4f99410c38ea00f) (Ogre) | **snackwacko** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Drugdör The Golem Animated »](https://sketchfab.com/3d-models/19c1855bdb2c4cdc89da2cfb64da48cf) (Golem de pierre) | **Phons** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Werewolf Stylized Animated »](https://sketchfab.com/3d-models/16cefdf83e5e457eb6e53f1fbd97edb5) (Loup-garou) | **imitate** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Witch »](https://sketchfab.com/3d-models/2a7672475d514543b618b5a82fd5582b) (Sorcière) | **MAXDESIGN** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Low Poly Priest (Rigged) »](https://sketchfab.com/3d-models/a8900861df984faebabfb0e0d5ff59eb) (Prêtre (repli)) | **Yanez-Designs** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Girl with clothes. Worker set »](https://sketchfab.com/3d-models/4cf10e2dde6d4b12b6dccd25a4cf3b77) (Ouvrière) | **cattleya** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Crossbow (Game Model) »](https://sketchfab.com/3d-models/697aa7fee7894ad2aa0e87ebc1b9d631) (Arbalète) | **makivic** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Gambeson »](https://sketchfab.com/3d-models/0fc44d2876ed449ca9473e78c83a8d39) (Gambison (sur mannequin)) | **Leon.Steiner** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Bridge »](https://sketchfab.com/3d-models/e536f8aa3a15473bb9eaffeb53ae31e9) (Pont de pierre des Vallons (décision de Damien du 9 novembre 2026)) | **_JooL_** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Pack of old towers in ruins »](https://sketchfab.com/3d-models/f213359c6cbb4c29bf8880764faa0fb8) (Tours en ruine aux ruines (73 k triangles : ordinateur seulement)) | **taz83** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Fountain Empty (Free) »](https://sketchfab.com/3d-models/952a9c257db641229902c36abb97e8bc) (Fontaine) | **wolfgar74** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Street lantern »](https://sketchfab.com/3d-models/1a38842082494b0cb62f74e630a00043) (Lanterne de rue) | **Alexey.inc** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Market Asset Pack »](https://sketchfab.com/3d-models/006ffc4ac5f34a1782f567b07e6605f2) (Étals du marché (88 textures, 51 Mo)) | **vmatthew** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Mushrooms »](https://sketchfab.com/3d-models/f9dbd4c34a4f4389bf3901527ecc89a0) (Champignons) | **leha_pu** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Long Banner »](https://sketchfab.com/3d-models/1b9956e1c98742a5bab251d81ac364ff) (Longue bannière) | **awenram** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Wooden Coffins (Free) »](https://sketchfab.com/3d-models/23f49c5ab417455a9d8fca828919354e) (Cercueils (crypte)) | **Shaddy** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Broken Coffin »](https://sketchfab.com/3d-models/715fd5c8503b4a0e8106168535c186ff) (Cercueils (crypte)) | **DivyeSh.PanchAl** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Low Poly Bone Pile (Downloadable) »](https://sketchfab.com/3d-models/5ec580b41a934cea86d4297980d1378f) (Tas d'os (crypte, antre)) | **3dsauce** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Pile of Skulls »](https://sketchfab.com/3d-models/11d46d32494c44218a55192adc067e57) (Tas de crânes (crypte)) | **abimaelgonzalez** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Stone Altar »](https://sketchfab.com/3d-models/3d4f2edb18e4424cabb41b60aa160cb5) (Autel de pierre (crypte)) | **TheoClarke** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Tombstones »](https://sketchfab.com/3d-models/047c28dfd28f4f9eb630bfd42dfeb910) (Stèles (cimetière)) | **drobluda** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Wall Torch »](https://sketchfab.com/3d-models/77db436da2844cbfb4dde0bb9b396835) (Torches murales (crypte)) | **Kigha** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Torch »](https://sketchfab.com/3d-models/238cd6056e2940debb4f67fc24c6df35) (Torches murales (crypte)) | **bumstrum** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval castle modular kits (interior) »](https://sketchfab.com/3d-models/bde29c804d6247c59d29a2ade11e69bc) (Kit d'intérieur de château) | **pedramashoori** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Modular Dungeon »](https://sketchfab.com/3d-models/c4d0cbde62b748caa58e40699477f8cd) (Donjon modulaire (33 k triangles)) | **bumstrum** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Modular Dungeon Props »](https://sketchfab.com/3d-models/62ccabc4e78b4822ac8c5af74fe9c66a) (Objets de donjon) | **just_polygons** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« SPIDER WEB »](https://sketchfab.com/3d-models/cc5d3b794341445a90bc6f6e9422792d) (Toiles d'araignée) | **sammephi** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Aged Cheese FBX »](https://sketchfab.com/3d-models/7fe307bc3142404e8b2666f9228d83c5) (Fromages (étal, auberge)) | **GetDeadEntertainment** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Half Cheese Wheel »](https://sketchfab.com/3d-models/53eb788dee544f8c926638a27d7bea5a) (Fromages (étal, auberge)) | **meerschaumdigital** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Wooden Rolling Pin »](https://sketchfab.com/3d-models/e5d077535f1241d39121d5c87c65bfaa) (Rouleau à pâtisserie) | **GameDevNick** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Cauldron »](https://sketchfab.com/3d-models/969634fbc14f40feb263409aa3c3ed5d) (Chaudron (intérieurs)) | **DarrenMcnerney3D** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Chest »](https://sketchfab.com/3d-models/037b03a3e0274279be4b93b7c7cedf01) (Coffre (intérieurs)) | **aiv** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Medieval Tankard »](https://sketchfab.com/3d-models/d95203038a9841d1b6f87b28c2e347aa) (Chopes (intérieurs)) | **Kigha** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Animated Crow »](https://sketchfab.com/3d-models/0609852ae9084d8a8643517e4eae68e1) (Corbeau animé) | **emptypen2477** (Sketchfab) | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Grist Mill Start Process (n° 70201) »](https://freesound.org/s/70201/) | **JustinBW** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Water Wheel (n° 202764) »](https://freesound.org/s/202764/) | **morturn** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| [« Sehusa Fest Medieval Crowd (n° 40133) »](https://freesound.org/s/40133/) | **inchadney** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Water drips in the cave HQ (n° 546359) »](https://freesound.org/s/546359/) | **tosha73** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Creepy Dungeon Ambience (n° 166187) »](https://freesound.org/s/166187/) | **DrMinky** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Chains 1 (n° 348973) »](https://freesound.org/s/348973/) | **freemaster2** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Spider_Killed_Foley_01 (n° 197447) »](https://freesound.org/s/197447/) | **dheming** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Bat Wings, Flapping (n° 811528) »](https://freesound.org/s/811528/) | **designerschoice** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Sword Clash and Slide (n° 361485) »](https://freesound.org/s/361485/) | **FunWithSound** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« SwordClash09 (n° 180828) »](https://freesound.org/s/180828/) | **32cheeseman32** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Cut Through Armor / Slice / Clang (n° 420675) »](https://freesound.org/s/420675/) | **SypherZent** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Shield block-01 (n° 812518) »](https://freesound.org/s/812518/) | **DinisNakamura** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Magic Healing Spell SFX (n° 853303) »](https://freesound.org/s/853303/) | **TommasoMotteran** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« Fantasy Sound Effects Library »](https://opengameart.org/content/fantasy-sound-effects-library) | **Little Robot Sound Factory** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| [« Magic SFX Sample »](https://opengameart.org/content/magic-sfx-sample) | **ViRiX Dreamcore (David McKee)** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |
| [« Musiques (16 morceaux, dossier Music/ScottBuckley2) »](https://www.scottbuckley.com.au) | **Scott Buckley** | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) |
| [« RPG For Wenches, Ale, and LOOT!! ; RPG Never Go Full Bard ; RPG - The Graveyard »](https://opengameart.org/users/hitctrl) | **Hitctrl** | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |

Les autres ressources de l'essai de style sous CC BY (moulin, arbres et bâtiments de Sketchfab, icônes) sont listées dans `docs/CREDITS.md`.
