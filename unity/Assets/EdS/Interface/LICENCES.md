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
| Musiques « Determined Pursuit » (**Emma_MA**) et « The Old Tower Inn » (**RandomMind**), corps et animations de **Quaternius** (V2.9.2), vêtements *Modular Character Outfits - Fantasy* de **Quaternius** (V2.9.4) | OpenGameArt, quaternius.com | CC0 (cités par courtoisie) |

Les autres ressources de l'essai de style sous CC BY (moulin, arbres et bâtiments de Sketchfab, icônes) sont listées dans `docs/CREDITS.md`.
