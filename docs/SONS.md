# Sons et musiques des Vallons (V2.54, passe son)

Demande de Damien (27 novembre 2026) : un vrai système sonore pour les Vallons, avec une musique par lieu et par moment, des ambiances par milieu, des sons de jeu, un mixage et une limite de voix pour le PC modeste. Ce document fait l'inventaire de ce qui existe, décrit le système, liste **tous les sons et musiques à produire** (chaque entrée sert directement de prompt pour un générateur) et chiffre l'option de génération par IA. **Aucune dépense n'est engagée et aucun compte n'est créé : c'est la décision de Damien.**

## 1. Inventaire avant la V2.54

| Quoi | Où | Suivi par git ? |
|---|---|---|
| 13 sons d'interface (clic, survol, ouverture, fermeture, onglet, compétence prête, charge, talent, retrait, validé, erreur, niveau, quête), Kenney CC0 | `unity/Assets/EdS/Interface/Resources/EdsInterface/Sons/*.ogg`, `UiSounds.cs` | oui |
| Musiques déposées par Damien : exploration (*Into The Wilds*), village, combat, chefs (*Determined Pursuit*), Seuil (*Sanctum*), scènes (*The Legend of Kvothe*), bourg la nuit (*Night Town*), auberge (*The Old Tower Inn*), forge (*Song of the Forge*), crypte (*The Long Dark*), fêtes (*King's Feast*, *Minstrel Dance*) | `Assets/Style/Music/…`, catalog `StyleCatalog`, `StyleAudio.cs` | non (dossier `Assets/Style`) |
| Nappes déposées : oiseaux, vent, rivière, nuit, pluie, tonnerre, village, marché, taverne, forge, feu de camp, campagne, forêt, salle à manger, lac, four, moulin, foule de fête, crypte (Freesound) | `Assets/Style/Ambience`, `Assets/Style/Sounds/Monde`… | non |
| Bruitages déposés : coups, blocage, parade, mort, potion, récolte, enclume, élan, sort, voix des bêtes, cloches, failles, 4 pas (herbe, gravier, feuilles, plancher), cris de la ferme | `Assets/Style/Sounds/…` | non |
| Sons faits par le code : pluie et tonnerre (`WeatherCycle.Rain`, `Thunder`), bourdon de l'Envers (`EnversLook.Drone`), miaulement et ronron du chat, cri des failles et cloche (`WorldEventsView`) | `src/EdS.Runtime`, `unity/Assets/EdS/Game` | oui |
| Choix existants : musique (`StyleRules.MusicFor`), volumes des nappes (`AmbienceVolume`, `PlaceVolume`), pas (`StyleRules.StepSound`), fondu unique de 2,5 s | `src/EdS.Runtime/StyleRules.cs` | oui |

Manques constatés : aucun son sans le dossier `Assets/Style` (sauf l'interface) ; aucune musique ni nappe propre aux égouts, galeries, grottes, grottes noyées, au marais ou à l'altitude ; ni nuit ni hiver dans la musique d'exploration ; pas de pas sur pierre, eau, neige, boue ; ni coffre, ni porte, ni plongée ; un seul volume d'ambiance et de musique, réglé dans le catalogue (aucun réglage pour le joueur) ; aucune limite de bruitages simultanés.

## 2. Le système (V2.54)

Toutes les règles de choix sont des fonctions pures, testées (`tests/EdS.Data.Tests/V254SonTests.cs`) :

- **Milieu** (`SoundRules.BiomeOf`) : d'après la zone (`egouts_…`, `crypte_…`, `galeries_…` et `mine_…`, `grottes_noyees_…`, `grotte_…`, `envers_…`, intérieur, dessous) puis les lieux du dehors (marais : `marais`, `pilotis`, `tourbiere`, `etang`… ; altitude : `col`, `crete`, `cairns`, `hautes_collines`, `tour_de_signal` ; bourg ; forêt ; lac), sinon le plein air.
- **Musique d'un lieu et d'un moment** (`SoundRules.Music`) : le Gardien, les attaques et les scènes passent devant ; un combat sous terre ou dans l'Envers a sa musique ; chaque souterrain a la sienne, l'Envers aussi ; l'exploration a une version de nuit et une d'hiver ; bourg, bourg la nuit, auberge, forge et fêtes restent. **Replis** (`SoundRules.MusicChain`) : galeries et grottes noyées → grottes → crypte → exploration ; égouts → crypte ; Envers → Seuil ; nuit et hiver → exploration ; combat sous terre → combat. **Fondus** (`SoundRules.FadeSeconds`) : 1 s vers le combat, 4 s pour en sortir, 5 s vers ou depuis l'Envers, 3 s entre le jour et la nuit, 2,5 s sinon ; une musique quittée est mise en pause et reprend où elle en était (le combat repart du début). Aucune coupure.
- **Nappes des milieux** (`SoundRules.BedVolume`) : marais, grotte, égouts, galeries, grottes noyées, Envers, sous l'eau, rafales en altitude ; sous terre, dans l'Envers et sous l'eau, les nappes du dehors se taisent (`MutesWorld`) ; la pluie et le vent ne s'entendent pas sous terre.
- **Pas** (`SoundRules.Step`) : eau (grottes noyées), pierre (souterrains, Envers), plancher (intérieurs), boue (marais), neige (sol enneigé à 50 % au moins, sauf plancher), sinon herbe, gravier ou feuilles comme avant.
- **Sons de jeu** : coups, blocage, parade, mort, élan, sort, potion, pièces (butin), récolte, enclume, héros blessé, coffre ouvert, porte passée, plongée puis remontée (avec la nappe sous l'eau pendant le passage), failles (déjà là), interface (déjà là).
- **Mixage** (`SoundRules` + `SoundMixer`) : quatre volumes — musique (70 % par défaut), ambiance (80 %), effets (90 %), interface (80 %) — par pas de 10 %, courbe au carré, réglés dans la pause (**Réglages → ligne Volume** : − / nom du canal / + ; un clic sur le nom passe au canal suivant), sauvegardés dans les préférences.
- **Limite de voix** (`SoundRules.VoiceLimit`, `Admit`, `PriorityOf`, `MinGap`) : 16 bruitages simultanés sur téléphone, 24 sur le PC modeste, 32 au-delà ; les pas et les bêtes s'arrêtent aux trois quarts, les coups à la limite, la mort, les portes, les coffres et la plongée passent toujours ; une salve du même son ne fait qu'un son (60 ms, 120 ms pour un pas).
- **Repli** : un fichier du catalogue s'il est déposé, sinon un **son de synthèse** (`SoundSynth`, fait par le code, déterministe, aucun fichier) pour les 8 nappes de milieu et 26 bruitages (tous les pas, coups, parade, mort, élan, sort, portail, pièces, verre, potion, récolte, enclume, coffre, porte, plongée, remontée, héros blessé) ; une musique absente prend son repli ; sans aucune musique, silence musical.

Ce qui sonne **dès maintenant**, sans aucun fichier déposé : les nappes des 8 milieux, les pas sur les 8 sols, tous les bruitages de combat et du monde ci-dessus, la pluie, le tonnerre, le bourdon de l'Envers, l'interface. La musique demande les fichiers de Damien (déjà déposés pour 12 des 20 musiques).

Les sons de synthèse sont honnêtes mais simples (bruit filtré, résonances) : ils valent une maquette sonore, pas un enregistrement. Les fichiers ci-dessous les remplacent un par un, sans code : il suffit de les déposer et de relancer **Éclats du Seuil → Préparer l'essai de style**.

## 3. Où déposer les fichiers

Dans `unity/Assets/Style/` (non suivi par git), comme les packs actuels :

- musiques : `Music/PasseSon/` ; nappes : `Ambience/PasseSon/` ; bruitages : `Sounds/PasseSon/` ;
- le nom du fichier décide de son rôle (colonne « Fichier ») ; plusieurs variantes : `_01`, `_02`… (jusqu'à 6, jouées tour à tour) ;
- format : OGG Vorbis (ou WAV), 44,1 ou 48 kHz ; musiques et nappes en stéréo, bruitages en mono ; crête à −1 dBFS, sans silence au début d'un bruitage ;
- une boucle (« oui » dans la colonne Boucle) doit se raccorder sans clic : fin et début au même niveau, sans fondu au noir.

## 4. Liste complète à produire

Les prompts sont en anglais (les générateurs les comprennent mieux) ; la description française dit l'intention. Durées indicatives.

### 4.1 Musiques (Suno, Stable Audio ; instrumentales, sans voix)

Direction commune : fantasy médiévale intimiste, orchestre de chambre et instruments anciens (luth, vielle, harpe, flûtes, cordes, cor lointain), jamais épique tapageur sauf combat et chefs ; tonalités modales (dorien, éolien) ; mixage doux pour passer sous les nappes.

| Fichier | Rôle | Durée | Boucle | Prompt |
|---|---|---|---|---|
| `eds_musique_exploration_nuit` | exploration la nuit (remplace la musique d'exploration de 22 h à 5 h) | 3–4 min | oui | `Instrumental fantasy nocturne, solo harp and soft low strings, distant wooden flute, slow 60 bpm, D dorian, calm and mysterious, starry countryside at night, sparse, no drums, no vocals, seamless loop` |
| `eds_musique_exploration_hiver` | exploration en hiver | 3–4 min | oui | `Instrumental medieval winter theme, celesta and plucked harp over cold sustained strings, gentle sleigh-bell shimmer, 70 bpm, A aeolian, quiet snowy hills, melancholic and warm, no vocals, seamless loop` |
| `eds_musique_grottes` | grottes (cascade, mines à cristaux) | 3 min | oui | `Dark ambient fantasy cave music, low drones, slow glassy bell motifs with long reverb, water-like marimba plinks, 50 bpm, mysterious not scary, no drums, no vocals, seamless loop` |
| `eds_musique_egouts` | égouts du bourg | 3 min | oui | `Underground sewer exploration music, muted low cello ostinato, dripping pizzicato, metallic distant clanks, tense but restrained, 80 bpm, E phrygian, no vocals, seamless loop` |
| `eds_musique_galeries` | galeries des éclats, du Seuil, mines | 3 min | oui | `Fantasy mine tunnels theme, deep frame drum heartbeat, bowed psaltery, shimmering crystal tones, sense of depth and ancient magic, 66 bpm, C minor, no vocals, seamless loop` |
| `eds_musique_noyees` | grottes noyées du lac | 3 min | oui | `Flooded cavern ambient music, slow underwater-sounding pads, soft harp arpeggios drenched in reverb, low choir hum without words, 56 bpm, F lydian, dreamy and cold, seamless loop` |
| `eds_musique_envers` | l'Envers (le monde reflété) | 4 min | oui | `Eerie otherworld ambient, reversed strings, detuned music box fragments, very low sub drone, sparse dissonant clusters, timeless, 40 bpm feel, unsettling but beautiful, no vocals, seamless loop` |
| `eds_musique_combat_donjon` | combat sous terre et dans l'Envers | 2–3 min | oui | `Dungeon battle music, driving low taiko and frame drums, aggressive cello ostinato, brass stabs echoing in stone halls, 140 bpm, D minor, intense, no vocals, seamless loop` |
| `eds_musique_combat` *(facultatif, remplace l'existante)* | combat au dehors | 2–3 min | oui | `Medieval fantasy skirmish music, fast hurdy-gurdy and fiddle over war drums, horn calls, 150 bpm, A minor, urgent and heroic, no vocals, seamless loop` |
| `eds_musique_marais` *(prévu, non branché : voir question 2)* | marais | 3 min | oui | `Swamp ambient folk, slow bowed bass, reed flute, buzzing jaw harp, humid and sleepy, 60 bpm, G dorian, no vocals, seamless loop` |
| `eds_musique_fete_hiver` *(prévu, non branché)* | veillée d'hiver au bourg | 2–3 min | oui | `Cozy medieval winter feast, hurdy-gurdy, recorder and frame drum, warm tavern dance in 6/8, 110 bpm, G major, joyful, no vocals, seamless loop` |
| `eds_jingle_coffre` | petit motif d'un coffre rare | 3 s | non | `Short fantasy treasure jingle, harp glissando and bell, bright, 3 seconds, no vocals` |

Les 12 musiques déjà déposées restent (exploration, village, combat, chefs, Seuil, scènes, bourg la nuit, auberge, forge, crypte, deux fêtes).

### 4.2 Nappes d'ambiance (ElevenLabs Sound Effects, Stable Audio ; boucles)

| Fichier | Milieu | Durée | Boucle | Prompt |
|---|---|---|---|---|
| `eds_nappe_marais` | marais (pilotis) | 60 s | oui | `Swamp ambience loop, croaking frogs at varied distances, buzzing insects, still water lapping on wooden stilts, occasional heron call, humid, no music` |
| `eds_nappe_grotte` | grotte | 60 s | oui | `Cave ambience loop, slow water drips echoing in a large stone cavern, faint air movement, distant trickle, no music, no voices` |
| `eds_nappe_egouts` | égouts | 60 s | oui | `Medieval sewer ambience loop, flowing dirty water in a stone channel, gurgling, dripping, rats squeaking far away, echoing brick tunnel, no music` |
| `eds_nappe_galeries` | galeries, mines | 60 s | oui | `Old mine tunnel ambience loop, deep low rumble, creaking wooden support beams, small pebbles trickling, distant pickaxe echo, no music` |
| `eds_nappe_noyees` | grottes noyées | 60 s | oui | `Flooded cave ambience loop, gentle waves lapping against rock, water sloshing in hollows, drips with long echo, cold and vast, no music` |
| `eds_nappe_envers` | l'Envers | 60 s | oui | `Otherworldly ambience loop, deep pulsing drone, reversed whispers without words, faint glassy crackles, wind through dead trees, unsettling, no music` |
| `eds_nappe_sous_eau` | sous l'eau (plongée) | 20 s | oui | `Underwater ambience loop, muffled low rumble, small bubbles rising, pressure hum, heard from inside the water` |
| `eds_nappe_rafales` | col, crêtes, hautes collines | 60 s | oui | `High mountain wind ambience loop, strong gusts over a rocky ridge, whistling through stones, no birds, no music` |
| `eds_nappe_crypte` *(remplace l'air de la crypte si Damien le veut)* | crypte | 60 s | oui | `Crypt ambience loop, cold still air, faint distant drips, very soft stone settling, candle flicker crackle, no music` |
| `eds_nappe_vent_plaine` *(remplace « vent » si besoin)* | plaine | 60 s | oui | `Gentle countryside wind loop, breeze through tall grass and hedges, soft and steady, no birds, no music` |

### 4.3 Pas (ElevenLabs Sound Effects ; 4 à 6 variantes chacun, un seul pas par fichier)

Personnage en bottes de cuir, pas de marche, mono, 0,2 à 0,5 s, attaque nette, sans réverbération (le jeu la met s'il le faut).

| Fichier | Sol | Prompt |
|---|---|---|
| `eds_pas_pierre_01…06` | pierre (souterrains, Envers) | `Single footstep, leather boot on dry stone floor, close, mono, no reverb` |
| `eds_pas_eau_01…06` | eau peu profonde | `Single footstep splashing in ankle-deep water, leather boot, close, mono` |
| `eds_pas_neige_01…06` | neige | `Single footstep crunching in fresh snow, leather boot, close, mono` |
| `eds_pas_boue_01…06` | boue (marais) | `Single footstep in thick wet mud, squelch, leather boot, close, mono` |
| `eds_pas_herbe_01…06` *(remplace)* | herbe | `Single footstep on short grass, leather boot, soft, close, mono` |
| `eds_pas_gravier_01…06` *(remplace)* | gravier, route | `Single footstep on gravel path, leather boot, crunchy, close, mono` |
| `eds_pas_feuilles_01…06` *(remplace)* | feuilles | `Single footstep on dry fallen leaves, leather boot, close, mono` |
| `eds_pas_parquet_01…06` *(remplace)* | plancher | `Single footstep on old wooden floorboards, slight creak, leather boot, close, mono` |

Les fichiers « *(remplace)* » passent devant les sons déjà déposés du même rôle. Attention : un nom de pas ne doit contenir aucun mot de milieu (`grotte`, `egout`, `marais`…), sinon il serait pris pour une nappe.

### 4.4 Bruitages de jeu (ElevenLabs Sound Effects ; 3 à 6 variantes)

| Fichier | Quoi | Durée | Prompt |
|---|---|---|---|
| `eds_coffre_01…03` | coffre (ou cache) qu'on ouvre | 1 s | `Old wooden treasure chest opening, iron latch click then creaking hinge, close, mono` |
| `eds_porte_ouvre_01…03` | porte passée (changement de zone) | 1–1,5 s | `Heavy medieval wooden door opening, creaking iron hinges, short, close, mono` |
| `eds_plongee_01…02` | plongée | 1,5 s | `Person diving into deep cold water, big splash then muffled bubbles, mono` |
| `eds_remontee_01…02` | remontée et souffle | 1,5 s | `Person surfacing from water, splash and a deep gasp for air, mono` |
| `eds_blesse_01…06` | le héros encaisse un coup | 0,3 s | `Short male pain grunt, warrior hit in combat, no words, mono` |
| `eds_coup_epee_01…06` *(remplace)* | coup porté | 0,3 s | `Sword hitting leather armor and flesh, meaty impact, mono` |
| `eds_bouclier_01…06` *(remplace)* | blocage au bouclier | 0,5 s | `Sword striking a wooden shield with iron rim, solid block, mono` |
| `eds_parade_01…04` *(remplace)* | parade parfaite | 0,8 s | `Two steel swords clashing, bright ringing parry, mono` |
| `eds_chute_01…03` *(remplace)* | un vaincu tombe | 1 s | `Body in armor collapsing on the ground, thud and metal rattle, mono` |
| `eds_elan_01…06` *(remplace)* | coup dans le vide | 0,3 s | `Fast sword swing whoosh, mono` |
| `eds_ecole_feu_01…04` | sort de feu (prévu, non branché : voir question 3) | 1 s | `Fireball spell cast, whoosh and roaring flame burst, mono` |
| `eds_ecole_givre_01…04` | sort de givre (prévu) | 1 s | `Ice spell cast, crystalline crackle and cold wind burst, mono` |
| `eds_ecole_lumiere_01…04` | soin, lumière (prévu) | 1,2 s | `Holy healing spell, soft choir-like shimmer and chimes, mono` |
| `eds_sort_01…06` *(remplace « Cast »)* | sort générique | 0,8 s | `Arcane magic spell cast, rising sparkle and energy whoosh, mono` |
| `eds_pieces_01…04` *(remplace)* | butin, pièces | 0,5 s | `Handful of gold coins dropped into a leather pouch, mono` |
| `eds_potion_01…03` *(remplace)* | potion bue | 1 s | `Drinking a potion from a glass vial, two gulps and a cork pop, mono` |
| `eds_faille_ouvre_01…02` | une faille s'ouvre (complète « portail ») | 2 s | `Magical rift tearing open in the air, glassy crack and deep whoosh, mono` |
| `eds_faille_ferme_01…02` | une faille se referme | 1,5 s | `Magic portal collapsing, implosion and shattering glass, mono` |

### 4.5 Interface (facultatif : les 13 sons de Kenney restent)

Ceux-ci sont livrés avec le jeu : un remplaçant prend **le même nom** dans `unity/Assets/EdS/Interface/Resources/EdsInterface/Sons/` (suivi par git, licence à vérifier avant de l'y mettre).

| Fichier | Prompt |
|---|---|
| `clic`, `survol`, `onglet` | `Soft UI click, wooden and parchment feel, fantasy game menu, very short, mono` |
| `ouverture`, `fermeture` | `Leather book opening (or closing), fantasy UI, short, mono` |
| `niveau` | `Level up fanfare, short medieval brass and harp flourish, 2 seconds` |
| `quete` | `Quest completed jingle, warm lute and bell, 2 seconds` |
| `erreur` | `Muted wooden knock, UI error, very short, mono` |

Total : 12 musiques (dont 8 branchées tout de suite), 10 nappes, 8 sols × 6 pas, 18 bruitages × 3 à 6 variantes, 8 sons d'interface facultatifs : environ **12 musiques et 160 bruitages ou nappes**.

## 5. Chiffrage de la génération par IA (non engagé)

Prix publics relevés le 27 novembre 2026 (en dollars, hors taxes ; à revérifier avant tout achat). **Rien n'a été acheté, aucun compte n'a été créé.**

| Service | Formule utile | Prix | Ce qu'elle couvre ici |
|---|---|---|---|
| ElevenLabs (Sound Effects) | Creator, 1 mois | 22 $/mois (11 $ le premier mois) ; 605 générations de bruitages incluses ; bruitages libres de droits | les 10 nappes, les 48 pas, les ~90 bruitages, avec 3 à 4 essais par son |
| Suno (musique) | Pro, 1 mois | 10 $/mois (8 $ à l'année) ; 2 500 crédits ; **20 téléchargements par mois** ; usage commercial inclus | les 12 musiques avec de la marge d'essais ; Premier (30 $/mois, 60 téléchargements) si Damien veut tout refaire |
| Stable Audio (alternative) | Pro, 1 mois | 30 $/mois, 1 800 crédits | nappes et musiques d'ambiance ; à préférer seulement si Suno déçoit pour les boucles |

Recommandation (à décider par Damien) : **un mois d'ElevenLabs Creator + un mois de Suno Pro**, environ **32 $ (≈ 30 € HT, ~25 € avec le premier mois d'ElevenLabs à moitié prix)**, puis résilier ; budget confortable avec Suno Premier : environ 52 $. À vérifier : la licence commerciale ne vaut que pour ce qui est généré pendant l'abonnement payant (Suno), et les conditions d'ElevenLabs pour un jeu vendu.

Option gratuite : continuer avec les packs CC0 (Kenney, OpenGameArt, Freesound CC0) et les sons de synthèse ; les musiques de Scott Buckley (CC-BY) couvrent déjà une partie des besoins.

## 6. Choix faits par Claude (recommandations appliquées, à confirmer par Damien)

1. **Musique des souterrains** : options (a) une seule musique « donjon » pour tous, (b) une par souterrain avec replis, (c) aucune musique sous terre. **Appliqué : (b)**, avec replis vers la crypte puis l'exploration, pour que rien ne change tant que les fichiers manquent.
2. **Marais et altitude** : (a) nappe seule, (b) nappe et musique propre. **Appliqué : (a)** (la musique d'exploration y reste) ; `eds_musique_marais` est prévue mais non branchée.
3. **Sons des sorts** : (a) un son générique, (b) un son par école (feu, givre, lumière). **Appliqué : (a)** pour l'instant (le moteur ne donne pas encore l'école du sort à l'affichage) ; les fichiers par école sont prévus.
4. **Saisons et fêtes** : l'hiver a sa musique d'exploration ; les fêtes gardent leurs deux musiques ; une veillée d'hiver est prévue mais non branchée.
5. **Volumes par défaut** : musique 70 %, ambiance 80 %, effets 90 %, interface 80 %.

## 7. Limites connues

- Les sons de synthèse sont une maquette (ni voix ni instruments réels) ; aucune musique n'est synthétisée.
- La plongée est un passage instantané (fondu au noir) : la nappe sous l'eau dure 1,8 s, le temps du fondu.
- Pas dans l'eau : seulement dans les grottes noyées (le gué et les rigoles des égouts gardent la pierre ou l'herbe).
- La limite de voix compte les bruitages lancés par le jeu (`StyleLook.Play`, pas, bêtes) ; les sons d'interface et les nappes n'en font pas partie.
- Rien n'est encore essayé dans Unity par Damien (CompileCheck seulement).
