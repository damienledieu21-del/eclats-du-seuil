# Sons et musiques des Vallons (V2.54, passe son)

Demande de Damien (27 novembre 2026) : un vrai système sonore pour les Vallons, avec une musique par lieu et par moment, des ambiances par milieu, des sons de jeu, un mixage et une limite de voix pour le PC modeste. Ce document fait l'inventaire de ce qui existe, décrit le système, liste **tous les sons et musiques à produire** (chaque entrée sert directement de prompt pour un générateur) et chiffre l'option de génération par IA. Damien a validé la génération par IA le 27 novembre 2026 (section 3) ; c'est lui qui crée les comptes et paie, Claude n'a rien acheté.

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
- **Mixage** (`SoundRules` + `SoundMixer`) : quatre volumes — musique (70 % par défaut), ambiance (80 %), effets (90 %), interface (80 %) — par pas de 10 %, gain neutre (1) au volume par défaut et au carré autour de lui (décision et revue du 27 novembre 2026), réglés dans la pause (**Réglages → ligne Volume** : − / nom du canal / + ; un clic sur le nom passe au canal suivant), sauvegardés dans les préférences.
- **Limite de voix** (`SoundRules.VoiceLimit`, `Admit`, `PriorityOf`, `MinGap`) : 16 bruitages simultanés sur téléphone, 24 sur le PC modeste, 32 au-delà ; les pas et les bêtes s'arrêtent aux trois quarts, les coups à la limite, la mort, les portes, les coffres et la plongée passent toujours ; une salve du même son ne fait qu'un son (60 ms, 120 ms pour un pas).
- **Repli** : un fichier du catalogue s'il est déposé, sinon un **son de synthèse** (`SoundSynth`, fait par le code, déterministe, aucun fichier) pour les 8 nappes de milieu et 26 bruitages (tous les pas, coups, parade, mort, élan, sort, portail, pièces, verre, potion, récolte, enclume, coffre, porte, plongée, remontée, héros blessé) ; une musique absente prend son repli ; sans aucune musique, silence musical.

Ce qui sonne **dès maintenant**, sans aucun fichier déposé : les nappes des 8 milieux, les pas sur les 8 sols, tous les bruitages de combat et du monde ci-dessus, la pluie, le tonnerre, le bourdon de l'Envers, l'interface. La musique demande les fichiers de Damien (déjà déposés pour 12 des 20 musiques).

Les sons de synthèse sont honnêtes mais simples (bruit filtré, résonances) : ils valent une maquette sonore, pas un enregistrement. Les fichiers ci-dessous les remplacent un par un, sans code : il suffit de les déposer et de relancer **Éclats du Seuil → Préparer l'essai de style**.

## 3. Décisions de Damien du 27 novembre 2026

- **Génération par IA validée**, environ **32 $** : un mois d'**ElevenLabs Creator** (22 $, 11 $ le premier mois) et un mois de **Suno Pro** (10 $). **C'est Damien qui crée les comptes et paie** ; Claude n'a rien acheté et ne crée aucun compte.
- **Volumes par défaut validés** : musique 70 %, ambiance 80 %, effets 90 %, interface 80 %. Le gain vaut 1 à ces valeurs (revue de la PR #267) : le jeu sonne comme avant pour qui n'a rien réglé ; 0 % coupe tout ; 100 % monte jusqu'à +6 dB.
- **Recommandations gardées** : une musique par souterrain, avec ses replis ; marais et col avec leur nappe seule, la musique d'exploration y reste ; un son de sort générique tant que le moteur ne donne pas l'école du sort à l'affichage.

## 4. Mode d'emploi

1. Générer les fichiers de la section 5, dans l'ordre des priorités (P1 d'abord : c'est ce qui manque le plus ou ce que la synthèse rend le moins bien).
2. Les renommer **exactement** comme la colonne « Fichier » (les `_01`, `_02`… sont des variantes : jusqu'à 6, jouées tour à tour). L'extension peut être `.mp3` (téléchargement de Suno), `.wav` ou `.ogg`.
3. Les déposer dans le dossier indiqué. Tous sont sous `unity/Assets/Style/`, qui n'est **pas** suivi par git (comme les autres packs) : créer les sous-dossiers `PasseSon` s'ils n'existent pas.
   - musiques (Suno) : `unity/Assets/Style/Music/PasseSon/`
   - nappes (ElevenLabs) : `unity/Assets/Style/Ambience/PasseSon/`
   - bruitages et pas (ElevenLabs) : `unity/Assets/Style/Sounds/PasseSon/`
4. Dans Unity : menu **Éclats du Seuil → Préparer l'essai de style**. La fenêtre de fin compte ce qui a été trouvé ; chaque fichier trouvé remplace le son de synthèse ou la musique de repli. Aucun code à changer.

Règles à respecter :

- un nom de pas ou de bruitage ne doit contenir **aucun mot de milieu** (`grotte`, `egout`, `marais`, `galerie`, `envers`…), sinon il serait pris pour une nappe ;
- une boucle doit se raccorder sans clic : fin et début au même niveau, sans fondu au noir ; si l'outil propose une option de boucle, la cocher ;
- bruitages courts : un seul son par fichier, attaque nette, sans silence au début, sans réverbération ;
- licence : générer **pendant** l'abonnement payant (droits commerciaux de Suno et d'ElevenLabs) ; garder la page de l'abonnement comme preuve.

## 5. Prompts prêts à copier

### 5.1 Suno (Pro) : musiques

Réglages communs : **Instrumental** coché, style = le prompt ci-dessous, titre = le nom du fichier. Suno rend des morceaux de 2 à 4 min : garder le meilleur des deux rendus, le télécharger, le renommer. Pro permet 20 téléchargements par mois : les 8 de P1 et les 3 de P3 tiennent dedans, avec de la marge pour des essais.

Direction commune : fantasy médiévale intimiste, orchestre de chambre et instruments anciens, jamais épique tapageur sauf combat ; mixage doux pour passer sous les nappes.

**P1 (branchées : remplacent les replis tout de suite)**, dossier `unity/Assets/Style/Music/PasseSon/`

| Fichier | Rôle dans le jeu |
|---|---|
| `eds_musique_exploration_nuit` | exploration la nuit |
| `eds_musique_grottes` | grotte de la cascade, mines à cristaux |
| `eds_musique_egouts` | égouts du Bourg |
| `eds_musique_galeries` | galeries des éclats, du Seuil, mines |
| `eds_musique_noyees` | grottes noyées du lac |
| `eds_musique_envers` | l'Envers |
| `eds_musique_combat_donjon` | combat sous terre et dans l'Envers |
| `eds_musique_exploration_hiver` | exploration en hiver |

```
eds_musique_exploration_nuit
Instrumental fantasy nocturne, solo harp and soft low strings, distant wooden flute, slow 60 bpm, D dorian, calm and mysterious, starry countryside at night, sparse, no drums, no vocals, seamless loop
```
```
eds_musique_grottes
Dark ambient fantasy cave music, low drones, slow glassy bell motifs with long reverb, water-like marimba plinks, 50 bpm, mysterious not scary, no drums, no vocals, seamless loop
```
```
eds_musique_egouts
Underground sewer exploration music, muted low cello ostinato, dripping pizzicato, metallic distant clanks, tense but restrained, 80 bpm, E phrygian, no vocals, seamless loop
```
```
eds_musique_galeries
Fantasy mine tunnels theme, deep frame drum heartbeat, bowed psaltery, shimmering crystal tones, sense of depth and ancient magic, 66 bpm, C minor, no vocals, seamless loop
```
```
eds_musique_noyees
Flooded cavern ambient music, slow underwater-sounding pads, soft harp arpeggios drenched in reverb, low wordless choir hum, 56 bpm, F lydian, dreamy and cold, seamless loop
```
```
eds_musique_envers
Eerie otherworld ambient, reversed strings, detuned music box fragments, very low sub drone, sparse dissonant clusters, timeless, 40 bpm feel, unsettling but beautiful, no vocals, seamless loop
```
```
eds_musique_combat_donjon
Dungeon battle music, driving low taiko and frame drums, aggressive cello ostinato, brass stabs echoing in stone halls, 140 bpm, D minor, intense, no vocals, seamless loop
```
```
eds_musique_exploration_hiver
Instrumental medieval winter theme, celesta and plucked harp over cold sustained strings, gentle sleigh-bell shimmer, 70 bpm, A aeolian, quiet snowy hills, melancholic and warm, no vocals, seamless loop
```

**P3 (facultatives)** : `eds_musique_combat` remplace la musique de combat déjà déposée ; `eds_musique_marais` et `eds_musique_fete_hiver` sont prévues mais **pas encore branchées** (décision du 27 novembre : le marais garde la musique d'exploration).

```
eds_musique_combat
Medieval fantasy skirmish music, fast hurdy-gurdy and fiddle over war drums, horn calls, 150 bpm, A minor, urgent and heroic, no vocals, seamless loop
```
```
eds_musique_marais
Swamp ambient folk, slow bowed bass, reed flute, buzzing jaw harp, humid and sleepy, 60 bpm, G dorian, no vocals, seamless loop
```
```
eds_musique_fete_hiver
Cozy medieval winter feast, hurdy-gurdy, recorder and frame drum, warm tavern dance in 6/8, 110 bpm, G major, joyful, no vocals, seamless loop
```

### 5.2 ElevenLabs (Sound Effects, Creator) : nappes, pas, bruitages

Réglages communs : la durée indiquée (pour une nappe, la plus longue que l'outil permet, au moins 20 s : le jeu la boucle), option de boucle cochée pour les nappes si elle existe ; générer 2 à 4 essais et garder le meilleur. Creator inclut 605 générations : la liste ci-dessous en demande environ 150 à garder, soit 3 à 4 essais par son.

**P1 : nappes des milieux (8 fichiers)**, dossier `unity/Assets/Style/Ambience/PasseSon/`

| Fichier | Milieu | Prompt |
|---|---|---|
| `eds_nappe_egouts` | égouts | `Medieval sewer ambience loop, flowing dirty water in a stone channel, gurgling, dripping, rats squeaking far away, echoing brick tunnel, no music` |
| `eds_nappe_grotte` | grotte | `Cave ambience loop, slow water drips echoing in a large stone cavern, faint air movement, distant trickle, no music, no voices` |
| `eds_nappe_galeries` | galeries, mines | `Old mine tunnel ambience loop, deep low rumble, creaking wooden support beams, small pebbles trickling, distant pickaxe echo, no music` |
| `eds_nappe_noyees` | grottes noyées | `Flooded cave ambience loop, gentle waves lapping against rock, water sloshing in hollows, drips with long echo, cold and vast, no music` |
| `eds_nappe_marais` | marais (pilotis) | `Swamp ambience loop, croaking frogs at varied distances, buzzing insects, still water lapping on wooden stilts, occasional heron call, humid, no music` |
| `eds_nappe_rafales` | col, crêtes, hautes collines | `High mountain wind ambience loop, strong gusts over a rocky ridge, whistling through stones, no birds, no music` |
| `eds_nappe_sous_eau` | sous l'eau (plongée) | `Underwater ambience loop, muffled low rumble, small bubbles rising, pressure hum, heard from inside the water` |
| `eds_nappe_envers` | l'Envers | `Otherworldly ambience loop, deep pulsing drone, reversed whispers without words, faint glassy crackles, wind through dead trees, unsettling, no music` |

**P1 : pas des nouveaux sols et bruitages du monde (9 sons, 2 à 6 variantes)**, dossier `unity/Assets/Style/Sounds/PasseSon/`

| Fichier | Variantes | Durée | Prompt |
|---|---|---|---|
| `eds_pas_pierre_01…06` | 6 | 0,3 s | `Single footstep, leather boot on dry stone floor, close, mono, no reverb` |
| `eds_pas_eau_01…06` | 6 | 0,4 s | `Single footstep splashing in ankle-deep water, leather boot, close, mono` |
| `eds_pas_neige_01…06` | 6 | 0,4 s | `Single footstep crunching in fresh snow, leather boot, close, mono` |
| `eds_pas_boue_01…06` | 6 | 0,4 s | `Single footstep in thick wet mud, squelch, leather boot, close, mono` |
| `eds_coffre_01…03` | 3 | 1 s | `Old wooden treasure chest opening, iron latch click then creaking hinge, close, mono` |
| `eds_porte_ouvre_01…03` | 3 | 1,5 s | `Heavy medieval wooden door opening, creaking iron hinges, short, close, mono` |
| `eds_plongee_01…02` | 2 | 1,5 s | `Person diving into deep cold water, big splash then muffled bubbles, mono` |
| `eds_remontee_01…02` | 2 | 1,5 s | `Person surfacing from water, splash and a deep gasp for air, mono` |
| `eds_blesse_01…06` | 6 | 0,3 s | `Short male pain grunt, warrior hit in combat, no words, mono` |

**P2 : remplaçants des bruitages de combat et des sols d'avant (passent devant les sons déjà déposés)**, même dossier `unity/Assets/Style/Sounds/PasseSon/`

| Fichier | Variantes | Durée | Prompt |
|---|---|---|---|
| `eds_coup_epee_01…06` | 6 | 0,3 s | `Sword hitting leather armor and flesh, meaty impact, mono` |
| `eds_bouclier_01…06` | 6 | 0,5 s | `Sword striking a wooden shield with iron rim, solid block, mono` |
| `eds_parade_01…04` | 4 | 0,8 s | `Two steel swords clashing, bright ringing parry, mono` |
| `eds_chute_01…03` | 3 | 1 s | `Body in armor collapsing on the ground, thud and metal rattle, mono` |
| `eds_elan_01…06` | 6 | 0,3 s | `Fast sword swing whoosh, mono` |
| `eds_sort_01…06` | 6 | 0,8 s | `Arcane magic spell cast, rising sparkle and energy whoosh, mono` |
| `eds_pieces_01…04` | 4 | 0,5 s | `Handful of gold coins dropped into a leather pouch, mono` |
| `eds_potion_01…03` | 3 | 1 s | `Drinking a potion from a glass vial, two gulps and a cork pop, mono` |
| `eds_faille_ouvre_01…02` | 2 | 2 s | `Magical rift tearing open in the air, glassy crack and deep whoosh, mono` |
| `eds_faille_ferme_01…02` | 2 | 1,5 s | `Magic portal collapsing, implosion and shattering glass, mono` |
| `eds_pas_herbe_01…06` | 6 | 0,3 s | `Single footstep on short grass, leather boot, soft, close, mono` |
| `eds_pas_gravier_01…06` | 6 | 0,3 s | `Single footstep on gravel path, leather boot, crunchy, close, mono` |
| `eds_pas_feuilles_01…06` | 6 | 0,3 s | `Single footstep on dry fallen leaves, leather boot, close, mono` |
| `eds_pas_parquet_01…06` | 6 | 0,3 s | `Single footstep on old wooden floorboards, slight creak, leather boot, close, mono` |

**P3 : facultatifs ou prévus, pas encore branchés**

| Fichier | Dossier | Prompt | État |
|---|---|---|---|
| `eds_nappe_crypte` | `Ambience/PasseSon/` | `Crypt ambience loop, cold still air, faint distant drips, very soft stone settling, candle flicker crackle, no music` | remplace l'air de la crypte (branché) |
| `eds_nappe_vent_plaine` | `Ambience/PasseSon/` | `Gentle countryside wind loop, breeze through tall grass and hedges, soft and steady, no birds, no music` | reconnu comme le vent (mot `vent`) : en concurrence avec le vent déjà déposé |
| `eds_ecole_feu_01…04` | `Sounds/PasseSon/` | `Fireball spell cast, whoosh and roaring flame burst, mono` | prévu (son par école de sort) |
| `eds_ecole_givre_01…04` | `Sounds/PasseSon/` | `Ice spell cast, crystalline crackle and cold wind burst, mono` | prévu |
| `eds_ecole_lumiere_01…04` | `Sounds/PasseSon/` | `Holy healing spell, soft choir-like shimmer and chimes, mono` | prévu |
| `eds_jingle_coffre` | `Sounds/PasseSon/` | `Short fantasy treasure jingle, harp glissando and bell, bright, 3 seconds` | prévu |

`eds_nappe_crypte` : renommer en `eds_nappe_crypte_dungeonair` pour qu'il soit reconnu comme l'air de la crypte (mot `dungeonair`).

**Interface (facultatif)** : les 13 sons de Kenney restent. Un remplaçant garde **le même nom** et va dans `unity/Assets/EdS/Interface/Resources/EdsInterface/Sons/` (ce dossier est suivi par git : licence à vérifier avant).

| Fichiers | Prompt |
|---|---|
| `clic`, `survol`, `onglet` | `Soft UI click, wooden and parchment feel, fantasy game menu, very short, mono` |
| `ouverture`, `fermeture` | `Leather book opening (or closing), fantasy UI, short, mono` |
| `niveau` | `Level up fanfare, short medieval brass and harp flourish, 2 seconds` |
| `quete` | `Quest completed jingle, warm lute and bell, 2 seconds` |
| `erreur` | `Muted wooden knock, UI error, very short, mono` |

Total : 8 musiques en P1 (plus 3 facultatives), 8 nappes, 9 sons du monde (40 fichiers), 14 remplaçants (66 fichiers), puis le facultatif.

## 6. Chiffrage (validé par Damien le 27 novembre 2026)

Prix publics relevés le 27 novembre 2026, en dollars hors taxes, à revérifier au moment de l'achat.

| Service | Formule | Prix | Ce qu'elle couvre |
|---|---|---|---|
| ElevenLabs (Sound Effects) | Creator, 1 mois | 22 $/mois (11 $ le premier mois) ; 605 générations ; bruitages libres de droits | toutes les nappes, les pas et les bruitages, avec 3 à 4 essais par son |
| Suno | Pro, 1 mois | 10 $/mois ; 2 500 crédits ; 20 téléchargements par mois ; usage commercial inclus | les 8 musiques de P1 et les 3 de P3 |
| Stable Audio (de secours) | Pro, 1 mois | 30 $/mois, 1 800 crédits | seulement si Suno déçoit pour les boucles |

Total validé : environ **32 $**, environ 21 $ avec le premier mois d'ElevenLabs à moitié prix, puis résilier les deux.

## 7. Limites connues

- Les sons de synthèse sont une maquette (ni voix ni instruments réels) ; aucune musique n'est synthétisée.
- La plongée est un passage instantané (fondu au noir) : la nappe sous l'eau dure 1,8 s ; pluie et tonnerre se taisent pendant ce temps.
- Pas dans l'eau : grottes noyées, gués (`gue…`) et déversoir des égouts (`deversoir`) ; les égouts n'ont pas encore de lieu « rigole ».
- La limite de voix compte les bruitages, les pas, les bêtes, les cloches, le tonnerre, le chat et les failles ; les sons d'interface et les nappes n'en font pas partie.
- Rien n'est encore essayé dans Unity par Damien (CompileCheck seulement).
