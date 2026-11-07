using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdS.Runtime
{
    /// <summary>Ce qu'un modèle de l'essai de style représente.</summary>
    public enum StyleSlot
    {
        Hero = 0,
        Wolf = 1,
        Boar = 2,
        Tree = 3,
        Rock = 4,
        Plant = 5,

        /// <summary>Essai réaliste : bois mort (souches, troncs couchés), posé dans la forêt autour de l'aire.</summary>
        Debris = 6,

        /// <summary>
        /// Essai réaliste (demande de Damien du 26 octobre 2026) : un personnage de Mixamo rangé dans son dossier
        /// (<c>Mixamo/Archer</c>…), pour un PNJ ou un ennemi d'après son identifiant (<see cref="StyleRules.CharacterOf"/>).
        /// </summary>
        Character = 7,

        /// <summary>Essai réaliste (27 octobre 2026) : arbuste des haies (« haie_ouest » était un bloc de pierre).</summary>
        Shrub = 8,

        /// <summary>Sous-lot V2.9 (vrais modèles) : cerf à bois (« Stag » de Quaternius), le chef de la harde et un cerf sur trois.</summary>
        Stag = 9,

        /// <summary>Sous-lot V2.9 : biche (« Deer » de Quaternius), les autres cerfs de la harde.</summary>
        Doe = 10,

        /// <summary>Sous-lot V2.9 : lièvre (« Rabbit » de Pneshik, Sketchfab).</summary>
        Hare = 11,

        /// <summary>Sous-lot V2.9 : le chat compagnon (« Stripe the Cat » de DreamNoms, Sketchfab, converti en FBX), teint aux couleurs du compagnon.</summary>
        Cat = 12,
    }

    /// <summary>Essai réaliste : matière d'un bâtiment (textures d'un dossier « Surfaces »).</summary>
    public enum StyleSurface
    {
        /// <summary>Pierre (murs, piliers de pierre).</summary>
        Stone = 0,

        /// <summary>Crépi (murs du moulin).</summary>
        Plaster = 1,

        /// <summary>Tuiles (toits).</summary>
        Roof = 2,

        /// <summary>Planches (pont, ailes du moulin, piliers de bois).</summary>
        Wood = 3,

        /// <summary>Pavés (murets du bord de l'aire).</summary>
        Cobble = 4,

        /// <summary>Écorce (rondins de la palissade ; demande de Damien du 27 octobre 2026).</summary>
        Bark = 5,
    }

    /// <summary>
    /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : effets de combat des packs gratuits « Hit Effects FREE » de Matthew Guz,
    /// « Magic Effects FREE » de Hovl Studio et « Free Slash VFX » de MaykerStudio) : un effet de combat dont le jeu prend le modèle
    /// (prefab) d'un pack, choisi par son nom (<see cref="StyleRules.VfxPrefab"/>) ; sans lui, les particules faites par le jeu restent.
    /// </summary>
    public enum StyleVfx
    {
        /// <summary>Coup reçu.</summary>
        Hit = 0,

        /// <summary>Coup critique.</summary>
        Crit = 1,

        /// <summary>Coup gardé (bouclier).</summary>
        Block = 2,

        /// <summary>Parade parfaite.</summary>
        Parry = 3,

        /// <summary>Garde brisée.</summary>
        GuardBroken = 4,

        /// <summary>Taille d'épée.</summary>
        Slash = 5,

        /// <summary>Sort ou soutien lancé (cercle au sol).</summary>
        Cast = 6,

        /// <summary>Soin (potion bue).</summary>
        Heal = 7,

        /// <summary>Niveau gagné.</summary>
        LevelUp = 8,

        /// <summary>Mort (poussière).</summary>
        Death = 9,

        /// <summary>Frappe au sol d'un adversaire (onde du Gardien).</summary>
        Slam = 10,

        /// <summary>Charge de Rempart gagnée.</summary>
        Charge = 11,

        /// <summary>Essai réaliste 8 : flammes du foyer de la forge (Particle Pack de Unity), en continu.</summary>
        ForgeFire = 12,

        /// <summary>Essai réaliste 8 : fumée qui monte du foyer de la forge, en continu.</summary>
        ForgeSmoke = 13,
    }

    /// <summary>
    /// Essai réaliste (demandes de Damien du 27 octobre 2026 : « la forge est à faire, les minerais et l'herbe de l'Aube aussi, le pont ») :
    /// objet de décor rangé dans les packs, choisi par son nom (<see cref="StyleRules.PropCandidates"/>).
    /// </summary>
    public enum StyleProp
    {
        /// <summary>Foyer de la forge (« stone_fire_pit »).</summary>
        FirePit = 0,

        /// <summary>Marteau posé sur l'enclume.</summary>
        Hammer = 1,

        /// <summary>Caisse près de la forge.</summary>
        Crate = 2,

        /// <summary>Tonneau près de la forge.</summary>
        Barrel = 3,

        /// <summary>Enclume (sinon faite par le code).</summary>
        Anvil = 4,

        /// <summary>Tronçon de ponton en bois, pour le pont.</summary>
        Bridge = 5,

        /// <summary>Plante des nœuds d'herboristerie (herbe de l'Aube).</summary>
        Herb = 6,

        /// <summary>Moulin (modèle de Sketchfab choisi par Damien le 27 octobre 2026).</summary>
        Mill = 7,
    }

    /// <summary>Clôture qu'habille un obstacle en longueur (<see cref="StyleRules.FenceFor"/>).</summary>
    public enum StyleFence
    {
        None = 0,

        /// <summary>Palissade de rondins taillés en pointe (murs, portes).</summary>
        Palisade = 1,

        /// <summary>Haie d'arbustes.</summary>
        Hedge = 2,
    }

    /// <summary>Un rondin de palissade, posé debout : centre au sol, rayon et hauteur (m).</summary>
    public readonly struct StyleLog
    {
        public StyleLog(double x, double z, double radius, double height)
        {
            X = x;
            Z = z;
            Radius = radius;
            Height = height;
        }

        public double X { get; }
        public double Z { get; }
        public double Radius { get; }
        public double Height { get; }
    }

    /// <summary>Ce que fait un corps animé, pour choisir son animation.</summary>
    public enum StyleMotion
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Attack = 3,
        Death = 4,

        /// <summary>
        /// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : le disque du bouclier est retiré, le chevalier garde son vrai bouclier) :
        /// garde levée à l'arrêt, si le modèle a une animation de blocage (« Sword And Shield Block » de Mixamo) ; sinon l'attente.
        /// </summary>
        Block = 5,

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « des clips Mixamo, garde, coups variés ») : sort ou soutien lancé
        /// (« Sword And Shield Casting »), rugissement du Gardien ; sinon une attaque.
        /// </summary>
        Cast = 6,

        /// <summary>Essai réaliste 7 : coup reçu sans garde (« Sword And Shield Impact », « Archer Impact ») ; sinon rien ne change.</summary>
        Hurt = 7,

        /// <summary>Essai réaliste 7 : coup reçu sur la garde (« Sword And Shield Guard Impact ») ; sinon la garde reste.</summary>
        GuardHit = 8,

        /// <summary>
        /// Sous-lot V2.4.1 (bêtes qui se promènent) : une bête qui broute ou fouille à l'arrêt (« Eat », « Graze », « Sniff », « Dig ») ;
        /// sinon l'attente.
        /// </summary>
        Graze = 9,
    }

    /// <summary>Un son de l'essai de style.</summary>
    public enum StyleSound
    {
        Hit = 0,
        Block = 1,
        Parry = 2,
        Death = 3,
        Potion = 4,
        Gather = 5,
        Craft = 6,

        /// <summary>Essai réaliste 7 : épée qui fend l'air (compétence lancée).</summary>
        Swing = 7,

        /// <summary>Essai réaliste 7 : sort ou soutien lancé.</summary>
        Cast = 8,

        /// <summary>Essai réaliste 7 : grognement d'un loup qui attaque.</summary>
        Growl = 9,

        /// <summary>Essai réaliste 7 : grognement d'un sanglier qui charge.</summary>
        Grunt = 10,

        /// <summary>Essai réaliste 7 : rugissement du Gardien et des grandes créatures.</summary>
        Roar = 11,

        /// <summary>Essai réaliste 7 : butin ramassé (pièces).</summary>
        Coin = 12,

        /// <summary>Sous-lot V2.2.1 : miaulement d'un compagnon d'affichage (sans fichier : un miaulement fait par le code).</summary>
        Meow = 13,

        /// <summary>Sous-lot V2.2.1 : ronronnement d'un compagnon d'affichage (sans fichier : un ronronnement fait par le code).</summary>
        Purr = 14,

        /// <summary>Sous-lot V2.9 : hurlement des loups (la nuit et par brume), un passage d'un long enregistrement.</summary>
        Howl = 15,

        /// <summary>Sous-lot V2.9 : brame du cerf (rare, à l'aube et au crépuscule).</summary>
        Bellow = 16,

        /// <summary>Sous-lot V2.9 : lièvre qui détale dans l'herbe.</summary>
        HareDash = 17,

        /// <summary>Sous-lot V2.9 : grognements d'un sanglier qui fouille (passages courts d'un long enregistrement).</summary>
        BoarGrumble = 18,

        /// <summary>Sous-lot V2.9 : cloche du tocsin (attaque du hameau), frappée vite et plusieurs fois.</summary>
        Tocsin = 19,

        /// <summary>Sous-lot V2.9 : cloche d'église (le hameau défendu).</summary>
        ChurchBell = 20,

        /// <summary>Sous-lot V2.9 : verre qui éclate (une faille du Seuil s'ouvre, une cicatrice grandit).</summary>
        Glass = 21,

        /// <summary>Sous-lot V2.9 : miroir qui explose (une faille du Seuil se referme).</summary>
        Shatter = 22,

        /// <summary>Sous-lot V2.9 : bourdonnement d'une faille ouverte (en boucle, en 3D).</summary>
        RiftHum = 23,
    }

    /// <summary>Essai réaliste 7 : musique de l'essai de style, d'après le lieu et le combat.</summary>
    public enum StyleMusic
    {
        /// <summary>Hors du village, sans combat.</summary>
        Exploration = 0,

        /// <summary>Au village.</summary>
        Village = 1,

        /// <summary>En combat.</summary>
        Combat = 2,

        /// <summary>Face au Gardien.</summary>
        Boss = 3,
    }

    /// <summary>Essai réaliste 7 : nappes d'ambiance (en boucle, mêlées).</summary>
    public enum StyleAmbience
    {
        Birds = 0,
        Wind = 1,
        River = 2,

        /// <summary>Sous-lot V2.1 : la nuit (grillons, hibou), qui remplace les oiseaux de 22 h à 5 h.</summary>
        Night = 3,

        /// <summary>Sous-lot V2.2 : la pluie (nappe en boucle, son volume suit la pluie) ; sans fichier, une pluie de synthèse.</summary>
        Rain = 4,

        /// <summary>
        /// Sous-lot V2.2 : un coup de tonnerre, joué une fois après chaque éclair (jamais en boucle) ; sans fichier, un tonnerre de synthèse.
        /// </summary>
        Thunder = 5,
    }

    /// <summary>
    /// Essai réaliste (choix de Damien du 26 octobre 2026) : couche de sol du terrain. L'ordre est celui des couches du terrain dans Unity.
    /// </summary>
    public enum StyleGround
    {
        /// <summary>Terre de forêt (sous les arbres, et par taches).</summary>
        Soil = 0,

        /// <summary>Roche (autour des blocs de la carrière, des pierres du Seuil, des murs).</summary>
        Rock = 1,

        /// <summary>Herbe (le reste du sol).</summary>
        Grass = 2,

        /// <summary>
        /// Essai réaliste 8 : terre battue des chemins (fichier « chemin… » ou « road… » d'un dossier « Ground ») ; sans elle, les chemins
        /// prennent la terre.
        /// </summary>
        Road = 3,

        /// <summary>Essai réaliste 8 : terre labourée des champs (fichier « champ… », « labour… », « field… ») ; sans elle, la terre.</summary>
        Field = 4,
    }

    /// <summary>Un endroit du sol qui change sa couche autour de lui (un rocher appelle la roche, un arbre la terre).</summary>
    public readonly struct StyleSpot
    {
        public StyleSpot(double x, double z, double radius, StyleGround ground)
        {
            X = x;
            Z = z;
            Radius = radius;
            Ground = ground;
        }

        public double X { get; }
        public double Z { get; }

        /// <summary>Rayon (m) où la couche est pleine ; elle s'efface ensuite sur <see cref="StyleRules.SpotFade"/> mètres.</summary>
        public double Radius { get; }

        public StyleGround Ground { get; }
    }

    /// <summary>Part de chaque couche en un point du sol (somme 1).</summary>
    public readonly struct StyleMix
    {
        public StyleMix(double soil, double rock, double grass)
        {
            Soil = soil;
            Rock = rock;
            Grass = grass;
        }

        public double Soil { get; }
        public double Rock { get; }
        public double Grass { get; }

        public double this[StyleGround ground] =>
            ground == StyleGround.Soil ? Soil : ground == StyleGround.Rock ? Rock : ground == StyleGround.Grass ? Grass : 0;
    }

    /// <summary>Essai réaliste 8 : parts des cinq couches du terrain en un point (terre, roche, herbe, chemin, champ).</summary>
    public readonly struct StyleLayers
    {
        public StyleLayers(double soil, double rock, double grass, double road, double field)
        {
            Soil = soil;
            Rock = rock;
            Grass = grass;
            Road = road;
            Field = field;
        }

        public double Soil { get; }
        public double Rock { get; }
        public double Grass { get; }
        public double Road { get; }
        public double Field { get; }
    }

    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026 : « un petit essai de style » après le 12.9, avant le lot 13, avec des packs
    /// gratuits) : règles sans Unity, testées, pour habiller les Vallons avec des modèles et des sons déposés par Damien dans
    /// <c>unity/Assets/Style</c>, quels que soient les noms des fichiers des packs. Le jeu, lui, ne change pas : ni le moteur, ni les
    /// emprises, ni les obstacles ; seuls l'aspect et le son changent, et sans pack tout reste en formes simples.
    /// <para>
    /// Essai réaliste (26 octobre 2026 : le premier essai, tout en formes simples des packs « low poly », ne plaît pas ; Damien choisit
    /// « un mélange de réaliste et de stylisé coloré », « lumineux, réaliste mais pas trop ») : les modèles réalistes (Poly Haven, Mixamo)
    /// passent devant les packs stylisés, le sol devient un terrain à trois couches (terre, roche, herbe) et la scène reçoit un ciel, une
    /// lumière, une brume et des couleurs. Toujours rien côté moteur.
    /// </para>
    /// </summary>
    public static class StyleRules
    {
        private static readonly Dictionary<StyleSlot, (string[] Want, string[] Avoid)> Models = new Dictionary<StyleSlot, (string[], string[])>
        {
            // Le paladin de Mixamo d'abord, sinon le chevalier des packs. Les fichiers d'animation de Mixamo (« Sword And Shield Idle »)
            // ne sont pas des héros.
            [StyleSlot.Hero] = (new[] { "paladin", "knight" }, new[] { "weapon", "sword", "shield" }),
            // Sous-lot V2.9 : le loup de Quaternius (« Wolf_Quaternius », animé : marche, galop, attaque, mort, manger) passe devant le loup
            // du premier pack (attente et marche seulement) ; le loup réaliste de l'Asset Store (PackAnimal) reste devant les deux.
            [StyleSlot.Wolf] = (new[] { "wolfquaternius", "wolf" }, new string[0]),
            [StyleSlot.Boar] = (new[] { "boar", "pig" }, new string[0]),
            // Essai réaliste 4 (27 octobre 2026) : les arbres de Sketchfab d'evolveduk s'appellent « oak », « Pine », « Spruce », « birch ».
            [StyleSlot.Tree] = (new[] { "firtree", "pine", "commontree", "tree", "birch", "maple", "oak", "spruce", "beech", "fir" },
                new[] { "dead", "stump", "log", "leaves", "leaf", "trunk", "root", "bark", "branch", "fire" }),
            // « stone_fire_pit » est le foyer de la forge, pas un rocher ; la pervenche est l'herbe de l'Aube, pas une plante du décor.
            [StyleSlot.Rock] = (new[] { "rock", "boulder", "stone" }, new[] { "path", "pebble", "small", "tiny", "fire", "pit" }),
            [StyleSlot.Plant] = (new[] { "bush", "fern", "grass", "flower", "plant", "clover", "shrub", "moss", "nettle", "celandine", "dandelion", "weed" },
                new[] { "big", "tall", "tree", "rock", "periwinkle" }),
            [StyleSlot.Debris] = (new[] { "stump", "log", "trunk", "root", "branch" }, new[] { "quiver" }),
            [StyleSlot.Shrub] = (new[] { "shrub", "hedge", "bush" }, new[] { "dead", "berry", "flower" }),
            // Les personnages sont rangés par dossier (StyleRules.Characters), pas reconnus par leur nom.
            [StyleSlot.Character] = (new string[0], new string[0]),
            // Sous-lot V2.9 : bêtes paisibles et chat (le chat est reconnu par un mot entier, voir Matches).
            [StyleSlot.Stag] = (new[] { "stagquaternius", "stag", "cerf" }, new[] { "stage", "staging" }),
            [StyleSlot.Doe] = (new[] { "deerquaternius", "deer", "doe", "biche", "hind" }, new[] { "stag", "reindeer", "cerf", "behind" }),
            [StyleSlot.Hare] = (new[] { "rabbitpneshik", "rabbit", "hare", "lievre", "bunny" }, new[] { "share", "hardware" }),
            [StyleSlot.Cat] = (new[] { "catstripe", "stripecat", "cat", "chat" }, new string[0]),
        };

        /// <summary>Mots des objets de décor (nom du fichier), par ordre de préférence.</summary>
        private static readonly Dictionary<StyleProp, (string[] Want, string[] Avoid)> PropWords = new Dictionary<StyleProp, (string[], string[])>
        {
            [StyleProp.FirePit] = (new[] { "firepit", "fireplace", "brazier", "hearth" }, new string[0]),
            [StyleProp.Hammer] = (new[] { "hammer" }, new string[0]),
            [StyleProp.Crate] = (new[] { "crate" }, new string[0]),
            [StyleProp.Barrel] = (new[] { "barrel" }, new string[0]),
            [StyleProp.Anvil] = (new[] { "anvil" }, new string[0]),
            [StyleProp.Bridge] = (new[] { "pier", "bridge" }, new string[0]),
            [StyleProp.Herb] = (new[] { "periwinkle", "herb" }, new string[0]),
            [StyleProp.Mill] = (new[] { "windmill", "mill" }, new string[0]),
        };

        /// <summary>Mots cherchés dans les noms des sons, par ordre de préférence (noms des packs gratuits de Kenney d'abord).</summary>
        private static readonly Dictionary<StyleSound, string[]> Sounds = new Dictionary<StyleSound, string[]>
        {
            [StyleSound.Hit] = new[] { "impactpunchmedium", "impactpunch", "punch", "hit" },
            [StyleSound.Block] = new[] { "impactplate", "impactmetal", "shield", "block" },
            [StyleSound.Parry] = new[] { "impactbell", "metalclick", "parry", "clang" },
            [StyleSound.Death] = new[] { "impactsoftheavy", "impactwoodheavy", "death", "fall" },
            [StyleSound.Potion] = new[] { "bottle", "drink", "potion", "liquid", "handlesmallleather", "cloth" },
            [StyleSound.Gather] = new[] { "impactmining", "chop", "dig", "gather" },
            [StyleSound.Craft] = new[] { "anvil", "forge", "impactmetalheavy", "metalpot" },
            // Essai réaliste 7 : packs CC0 d'OpenGameArt (artisticdude, rubberduck) et de Kenney.
            [StyleSound.Swing] = new[] { "swish", "swing", "whoosh" },
            [StyleSound.Cast] = new[] { "spell", "magic", "cast" },
            [StyleSound.Growl] = new[] { "growl", "howl", "wolf", "barking" },
            [StyleSound.Grunt] = new[] { "grunt", "boar", "pig", "oink" },
            [StyleSound.Roar] = new[] { "roar", "troll", "monster" },
            [StyleSound.Coin] = new[] { "coin" },
            // Sous-lot V2.2.1 : le chat qui suit (fichiers CC0 déposés par Damien, facultatifs).
            [StyleSound.Meow] = new[] { "miaou", "meow", "miaul" },
            [StyleSound.Purr] = new[] { "ronron", "purr" },
            // Sous-lot V2.9 (vrais sons, fichiers déposés par Damien dans « Sounds/Animaux ») : voix des bêtes, cloches, failles.
            [StyleSound.Howl] = new[] { "hurlement", "howling", "howl" },
            [StyleSound.Bellow] = new[] { "brame", "bellow", "rut" },
            [StyleSound.HareDash] = new[] { "lievre", "hare", "rabbit", "smallanimal" },
            [StyleSound.BoarGrumble] = new[] { "sanglier", "grogne", "wildboar" },
            [StyleSound.Tocsin] = new[] { "tocsin", "clochebsb", "cloche", "handbell" },
            [StyleSound.ChurchBell] = new[] { "eglise", "church", "chapelle" },
            [StyleSound.Glass] = new[] { "verre", "glassbreak", "glass" },
            [StyleSound.Shatter] = new[] { "miroir", "mirror", "shatter" },
            [StyleSound.RiftHum] = new[] { "faille", "bourdon", "humming", "drone" },
        };

        /// <summary>
        /// Sous-lot V2.9 : mots qu'un son ne doit pas contenir pour <paramref name="sound"/> (la cloche du tocsin n'est pas celle de l'église,
        /// le tocsin n'est pas une cloche d'église).
        /// </summary>
        private static readonly Dictionary<StyleSound, string[]> SoundAvoid = new Dictionary<StyleSound, string[]>
        {
            [StyleSound.Tocsin] = new[] { "eglise", "church", "chapelle" },
        };

        /// <summary>Essai réaliste 7 : mots des musiques (rangées dans un dossier « Music »), par ordre de préférence.</summary>
        private static readonly (StyleMusic Kind, string[] Words)[] MusicWords =
        {
            (StyleMusic.Exploration, new[] { "exploration", "explore", "woodland", "field" }),
            (StyleMusic.Village, new[] { "village", "town", "festival" }),
            (StyleMusic.Combat, new[] { "combat", "battle", "fight" }),
            (StyleMusic.Boss, new[] { "boss", "heroic", "epic" }),
        };

        /// <summary>
        /// Essai réaliste 7 : mots des ambiances (rangées dans un dossier « Ambience »), dans l'ordre où on les essaie. Relecture de l'essai 7 :
        /// la rivière d'abord, puis le vent, les oiseaux en dernier (« forest_river » est une rivière, pas des oiseaux).
        /// </summary>
        private static readonly (StyleAmbience Kind, string[] Words)[] AmbienceWords =
        {
            // Sous-lot V2.2 : la pluie d'abord (« thunderstorm_rain » est une nappe de pluie, qui tourne en boucle), puis le tonnerre seul.
            (StyleAmbience.Rain, new[] { "pluie", "rain", "averse" }),
            (StyleAmbience.Thunder, new[] { "orage", "thunder", "tonnerre" }),
            (StyleAmbience.Night, new[] { "nuit", "night", "grillon", "cricket", "hibou", "owl" }),   // sous-lot V2.1, avant tout le reste
            (StyleAmbience.River, new[] { "riviere", "river", "stream", "water" }),
            (StyleAmbience.Wind, new[] { "vent", "wind", "outdoor", "dehors" }),
            (StyleAmbience.Birds, new[] { "oiseaux", "birds", "morning", "forest" }),
        };

        /// <summary>
        /// Mots des animations. « sword » n'est pas un mot d'attaque : les animations de Mixamo s'appellent toutes « Sword And Shield … »
        /// (l'attente doit tourner en boucle).
        /// </summary>
        private static readonly Dictionary<StyleMotion, string[]> Clips = new Dictionary<StyleMotion, string[]>
        {
            [StyleMotion.Idle] = new[] { "idle", "stand", "breath" },
            [StyleMotion.Walk] = new[] { "walk" },
            [StyleMotion.Run] = new[] { "run", "gallop", "sprint", "trot" },
            [StyleMotion.Attack] = new[] { "attack", "slash", "strike", "bite", "punch", "hit" },
            [StyleMotion.Death] = new[] { "death", "die", "dead", "dying" },
            [StyleMotion.Block] = new[] { "block" },
            // Essai réaliste 7 : « power » n'en est pas (« Power Slash » est une attaque).
            // Essai réaliste 8 : le loup réaliste de l'Asset Store hurle (« WolfHowl ») et encaisse (« GetHit_1 »).
            [StyleMotion.Cast] = new[] { "cast", "spell", "roar", "taunt", "howl" },
            [StyleMotion.Hurt] = new[] { "impact", "hurt", "react", "damage", "gethit" },
            [StyleMotion.GuardHit] = new[] { "guard" },
            [StyleMotion.Graze] = new[] { "eat", "graze", "sniff", "dig", "eating" },   // sous-lot V2.4.1
        };

        /// <summary>Essai réaliste 7 : animations réservées à leur mouvement (jamais prises pour l'attente, la marche ou une attaque).</summary>
        private static readonly StyleMotion[] Reserved = { StyleMotion.Block, StyleMotion.Cast, StyleMotion.Hurt, StyleMotion.GuardHit, StyleMotion.Graze };

        /// <summary>Repli quand le modèle n'a pas l'animation demandée.</summary>
        private static readonly Dictionary<StyleMotion, StyleMotion[]> Fallbacks = new Dictionary<StyleMotion, StyleMotion[]>
        {
            [StyleMotion.Idle] = new StyleMotion[0],
            [StyleMotion.Walk] = new[] { StyleMotion.Run, StyleMotion.Idle },
            [StyleMotion.Run] = new[] { StyleMotion.Walk, StyleMotion.Idle },
            [StyleMotion.Attack] = new[] { StyleMotion.Idle },
            [StyleMotion.Death] = new StyleMotion[0],
            [StyleMotion.Block] = new[] { StyleMotion.Idle },
            [StyleMotion.Cast] = new[] { StyleMotion.Attack, StyleMotion.Idle },
            [StyleMotion.Hurt] = new StyleMotion[0],
            [StyleMotion.GuardHit] = new StyleMotion[0],
            [StyleMotion.Graze] = new[] { StyleMotion.Idle },   // sous-lot V2.4.1
        };

        /// <summary>Sources réalistes : dossiers où sont rangés les fichiers de Poly Haven, de Mixamo et de Sketchfab.</summary>
        private static readonly string[] Realistic = { "polyhaven", "mixamo", "sketchfab" };

        /// <summary>Profondeur (m) du lit de la rivière sous le sol, une fois la berge descendue.</summary>
        public const double RiverBed = 1.0;

        /// <summary>Largeur (m) de la berge : le lit descend du bord de la rivière jusqu'à sa profondeur sur cette distance.</summary>
        public const double RiverBank = 1.0;

        /// <summary>Hauteur (m) de la surface de l'eau, sous le sol où l'on marche (berges visibles).</summary>
        public const double WaterLevel = -0.3;

        /// <summary>Distance (m) en deçà de laquelle un PNJ se tourne vers le joueur.</summary>
        public const double FaceRange = 8.0;

        /// <summary>Mots des textures de sol (dans un dossier « Ground ») : roche, puis herbe ; le reste est de la terre.</summary>
        private static readonly string[] RockGround = { "rock", "trail", "stone", "gravel", "cliff" };
        private static readonly string[] GrassGround = { "grass", "meadow", "lawn", "moss" };

        /// <summary>Essai réaliste 8 : début du nom d'une texture de chemin ou de champ (dossier « Ground »).</summary>
        private static readonly string[] RoadGround = { "chemin", "road", "dirtroad" };
        private static readonly string[] FieldGround = { "champ", "labour", "field", "plough", "plow", "tilled", "farmland" };

        /// <summary>Distance (m) sur laquelle la couche d'un endroit s'efface autour de son rayon.</summary>
        public const double SpotFade = 3.0;

        /// <summary>Taille (m) des taches de terre semées dans l'herbe.</summary>
        public const double PatchSize = 9.0;

        /// <summary>Nom réduit pour comparer : minuscules, lettres et chiffres seulement (« Common_Tree_1.fbx » → « commontree1fbx »).</summary>
        public static string Key(string name) =>
            new string((name ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>Le fichier (chemin ou nom) correspond-il à <paramref name="slot"/> ? On juge le nom du fichier, pas ses dossiers.</summary>
        public static bool Matches(string path, StyleSlot slot)
        {
            // Intérieurs : les meubles et objets des intérieurs (dossier « Interieurs ») ne sont pas du décor des Vallons.
            if (InteriorRules.IsInterior(path)) return false;
            string file = Key(FileName(path));
            // Sous-lot V2.9 : « cat » se cache dans trop de noms (« catapult », « location ») : le chat est un mot entier du nom
            // (« Cat_Stripe_DreamNoms », « chat_roux »), ou « StripeCat ».
            if (slot == StyleSlot.Cat)
                return file.Contains("catstripe") || file.Contains("stripecat")
                    || Words(path).Any(w => w == "cat" || w == "chat" || w == "kitty" || w == "kitten");
            (string[] want, string[] avoid) = Models[slot];
            return want.Any(w => file.Contains(w)) && !avoid.Any(a => file.Contains(a));
        }

        /// <summary>
        /// Intérieurs : carte de métal de Poly Haven qui va avec la carte de couleur <paramref name="colorPath"/> (« …_diff_1k.jpg » →
        /// « …_metal_1k.exr », ou « …_metallic_1k.exr » pour les meubles gothiques), parmi <paramref name="maps"/> ; null s'il n'y en a pas.
        /// </summary>
        public static string? MetalFor(string? colorPath, IEnumerable<string> maps)
        {
            if (colorPath is null || maps is null) return null;
            string name = System.IO.Path.GetFileNameWithoutExtension(colorPath.Replace('\\', '/')).ToLowerInvariant();
            int at = name.LastIndexOf("_diff", StringComparison.Ordinal);
            if (at < 0) return null;
            var names = maps.Where(m => m != null)
                .Select(m => (path: m, name: System.IO.Path.GetFileNameWithoutExtension(m.Replace('\\', '/')).ToLowerInvariant())).ToList();
            foreach (string word in new[] { "_metal", "_metallic" })
            {
                string want = name.Substring(0, at) + word + name.Substring(at + 5);
                foreach (var map in names)
                    if (map.name == want) return map.path;
            }
            return null;
        }

        /// <summary>Le fichier vient-il d'une source réaliste (rangé dans un dossier Poly Haven ou Mixamo) ?</summary>
        public static bool IsRealistic(string path)
        {
            string key = Key(path);
            return Realistic.Any(r => key.Contains(r));
        }

        /// <summary>
        /// Modèles qui correspondent à <paramref name="slot"/>, dans un ordre stable : les FBX d'abord (les packs livrent aussi des OBJ sans
        /// animation), puis les sources réalistes, puis selon le premier mot cherché qu'ils contiennent (« paladin » avant « knight »), puis
        /// dans l'ordre ordinal de leur chemin.
        /// </summary>
        public static IReadOnlyList<string> Candidates(IEnumerable<string> paths, StyleSlot slot) =>
            (paths ?? Array.Empty<string>()).Where(p => Matches(p, slot))
                .OrderBy(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => IsRealistic(p) ? 0 : 1)
                .ThenBy(p => WantRank(p, slot))
                .ThenBy(p => p, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Modèles retenus pour le catalogue (au plus <paramref name="max"/>) : les candidats dans leur ordre ; pour les rochers et les
        /// plantes, seulement les réalistes s'il y en a (un rocher dessiné détonne sur le sol réaliste ; essai de Damien du 27 octobre 2026 :
        /// « les espèces de buissons tout rouges » étaient des buissons d'automne du MegaKit, mêlés aux plantes de Poly Haven).
        /// </summary>
        public static IReadOnlyList<string> ForCatalog(IEnumerable<string> paths, StyleSlot slot, int max)
        {
            IReadOnlyList<string> all = Candidates(paths, slot);
            // Essai réaliste 4 : les arbres aussi, dès qu'il y en a de réalistes (Sketchfab).
            if ((slot == StyleSlot.Rock || slot == StyleSlot.Plant || slot == StyleSlot.Shrub || slot == StyleSlot.Tree) && all.Any(IsRealistic))
                all = all.Where(IsRealistic).ToList();
            return all.Take(Math.Max(0, max)).ToList();
        }

        /// <summary>
        /// Essai réaliste 8 (point 3 de la demande de Damien du 28 octobre 2026 : rochers, terrain et eau des samples de Unity) : prefabs de
        /// décor des samples importés à la racine de Assets (Viking Village et sa végétation du Book of the Dead, Terrain Sample,
        /// Production Ready Shaders) pour <paramref name="slot"/> (rochers, plantes, arbustes), au plus <paramref name="max"/>. Seuls les
        /// « .prefab » rangés dans le dossier d'un sample comptent (pas les effets de combat ni les prefabs faits par la préparation) ; le
        /// nom du fichier décide (« pf_boulder_01 », « Fern_var01_Prefab », « GreenBush… »). Les samples donnent chacun leur tour (dans
        /// l'ordre de leur dossier, chacun dans l'ordre ordinal de ses chemins), pour mêler les fougères du Terrain Sample et celles du
        /// Book of the Dead plutôt que de ne prendre que le premier.
        /// </summary>
        public static IReadOnlyList<string> SampleDecor(IEnumerable<string> paths, StyleSlot slot, int max)
        {
            if (!SampleWords.TryGetValue(slot, out (string[] Want, string[] Avoid) words)) return Array.Empty<string>();
            List<List<string>> bySample = (paths ?? Array.Empty<string>())
                .Where(p => p != null && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && SampleOf(p) != null)
                .Where(p =>
                {
                    string file = Key(ClipName(p));
                    return words.Want.Any(w => file.Contains(w)) && !words.Avoid.Any(a => file.Contains(a)) && !SampleScenes.Any(a => file.Contains(a));
                })
                .Distinct().GroupBy(p => SampleOf(p)!, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.OrderBy(p => p, StringComparer.Ordinal).ToList()).ToList();
            var picked = new List<string>();
            for (int rank = 0; picked.Count < max && bySample.Any(s => rank < s.Count); rank++)
                foreach (List<string> sample in bySample)
                    if (rank < sample.Count && picked.Count < max) picked.Add(sample[rank]);
            return picked;
        }

        /// <summary>
        /// Essai réaliste 8 (point 6 de la demande de Damien : des loups réalistes ; « Realistic Furry Wolf », échantillon gratuit de
        /// l'Asset Store) : prefab d'un animal (<paramref name="slot"/> : loup ou sanglier) d'un pack importé à la racine de Assets, prêt pour
        /// URP (« Wolf_URP.prefab ») ; ses variantes pour HDRP et pour l'ancien rendu ne sont pas prises. Les prefabs rangés dans
        /// <c>Assets/Style</c> (préparés par Claude) ne comptent pas ici. Null sans lui.
        /// </summary>
        public static string? PackAnimal(IEnumerable<string> paths, StyleSlot slot)
        {
            if (slot != StyleSlot.Wolf && slot != StyleSlot.Boar) return null;
            return (paths ?? Array.Empty<string>())
                .Where(p => p != null && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                    && !Segments(p).Skip(1).Take(1).Contains("style") && Matches(p, slot))
                .Where(p =>
                {
                    string file = Key(ClipName(p));
                    return !PackPipelineVariants.Any(v => file.Contains(v)) && !Segments(p).Any(s => PackPipelineVariants.Any(v => s.StartsWith(v, StringComparison.Ordinal)));
                })
                .OrderBy(p => Key(ClipName(p)).Contains("urp") || Segments(p).Contains("urp") ? 0 : 1)
                .ThenBy(p => p, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>
        /// Essai réaliste 8 (revue Codex de la PR #65) : modèle animé d'un prefab de pack parmi les modèles importés trouvés dans sa
        /// hiérarchie, en remontant la chaîne des sources de chaque objet (le prefab peut être une variante, ou garder le modèle en
        /// enfant) : le premier qui porte un maillage animé (os), sinon le premier ; null s'il n'y en a aucun.
        /// </summary>
        public static string? AnimatedModel(IEnumerable<(string Path, bool Skinned)> models)
        {
            var found = (models ?? Array.Empty<(string Path, bool Skinned)>()).Where(m => !string.IsNullOrEmpty(m.Path)).ToList();
            return found.Where(m => m.Skinned).Select(m => m.Path).FirstOrDefault() ?? found.Select(m => m.Path).FirstOrDefault();
        }

        /// <summary>Essai réaliste 8 : variantes d'un pack faites pour un autre rendu qu'URP (dossiers ou noms).</summary>
        private static readonly string[] PackPipelineVariants = { "hdrp", "lrp", "builtin", "standard" };

        /// <summary>
        /// Essai réaliste 8 : rangé dans le dossier d'un sample de Unity (Viking Village, Terrain Sample, Production Ready Shaders) ? On juge
        /// les dossiers du chemin, réduits (« Assets/Viking Village/… », « Assets/TerrainSampleAssets/… »).
        /// </summary>
        public static bool IsSample(string path) => SampleOf(path) != null;

        /// <summary>Essai réaliste 8 : le sample du chemin (début réduit de son dossier : « vikingvillage »…), ou null.</summary>
        private static string? SampleOf(string path) =>
            Segments(path).Select(s => Samples.FirstOrDefault(k => s.StartsWith(k, StringComparison.Ordinal))).FirstOrDefault(k => k != null);

        /// <summary>
        /// Essai réaliste 8 (point 3 : le terrain des samples) : couche de terrain d'un sample (« Grass_A_TerrainLayer.terrainlayer » du
        /// Terrain Sample) pour <paramref name="ground"/> (herbe, terre, roche), d'après les mots de son nom dans l'ordre de préférence, puis
        /// l'ordre ordinal ; null sinon (le sol garde les textures de Poly Haven). Les chemins et les champs gardent les leurs.
        /// </summary>
        public static string? SampleGround(IEnumerable<string> layers, StyleGround ground)
        {
            if (!SampleGroundWords.TryGetValue(ground, out (string[] Want, string[] Avoid) words)) return null;
            var files = (layers ?? Array.Empty<string>()).Where(p => p != null && p.EndsWith(".terrainlayer", StringComparison.OrdinalIgnoreCase) && IsSample(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (string word in words.Want)
            {
                string? found = files.FirstOrDefault(p =>
                {
                    string file = Key(ClipName(p));
                    return file.StartsWith(word, StringComparison.Ordinal) && !words.Avoid.Any(a => file.Contains(a));
                });
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Essai réaliste 8 (point 3 : l'eau des samples) : matériau de l'eau de lac des Production Ready Shaders (« WaterLake.mat »), ou
        /// null. L'eau de lac ride d'après la position dans le monde ; celle du ruisseau (« WaterStream ») coule le long des coordonnées de
        /// texture du maillage, que la surface de la rivière n'a pas : elle n'est pas prise.
        /// </summary>
        public static string? SampleWater(IEnumerable<string> paths) =>
            (paths ?? Array.Empty<string>()).Where(p => p != null && p.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) && IsSample(p)
                && Key(ClipName(p)) == "waterlake").OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>
        /// Essai réaliste 8 : début des noms des couches de terrain des samples, par ordre de préférence. L'herbe verte (pas la sèche), la
        /// boue pour la terre (la terre caillouteuse à défaut), la roche nue (pas la terre caillouteuse).
        /// </summary>
        private static readonly Dictionary<StyleGround, (string[] Want, string[] Avoid)> SampleGroundWords = new Dictionary<StyleGround, (string[], string[])>
        {
            [StyleGround.Grass] = (new[] { "grassa", "grassmoss", "grassb", "grass" }, new[] { "dry", "soil", "sand" }),
            [StyleGround.Soil] = (new[] { "muddy", "mud", "dirt", "soil", "grasssoil" }, new[] { "sand" }),
            [StyleGround.Rock] = (new[] { "rock", "cliff", "stone" }, new[] { "soil", "sand" }),
        };

        /// <summary>
        /// Essai réaliste 8 : prefabs de démonstration des samples (« PlatformRock », « TutorialStep5AddRocks » des Production Ready Shaders :
        /// des scènes entières, pas des rochers).
        /// </summary>
        private static readonly string[] SampleScenes = { "platform", "tutorial", "demo", "levelcontent", "environment" };

        /// <summary>Débuts des dossiers des samples de Unity (réduits).</summary>
        private static readonly string[] Samples = { "vikingvillage", "terrainsample", "productionreadyshaders" };

        /// <summary>
        /// Essai réaliste 8 : mots des prefabs de décor des samples. Les menhirs, piliers et falaises ne sont pas des rochers (l'obstacle
        /// donne leur largeur : un menhir à la largeur d'un bloc serait une tour) ; les buissons secs ne font pas une haie.
        /// </summary>
        private static readonly Dictionary<StyleSlot, (string[] Want, string[] Avoid)> SampleWords = new Dictionary<StyleSlot, (string[], string[])>
        {
            [StyleSlot.Rock] = (new[] { "boulder", "rock" }, new[] { "cliff", "pebble", "small", "tiny", "menhir", "pillar", "decal", "rocky" }),
            [StyleSlot.Plant] = (new[] { "fern", "meadowgrass", "perennial", "grass", "plant", "heather", "clover", "flower", "weed" },
                new[] { "bush", "shrub", "tree", "rock", "dead" }),
            [StyleSlot.Shrub] = (new[] { "bush", "shrub" }, new[] { "dead", "dry", "berry" }),
        };

        /// <summary>
        /// Essai réaliste 8 : liste du catalogue faite des prefabs des samples d'abord (<paramref name="samples"/>), complétée par les autres
        /// modèles (<paramref name="others"/>, Poly Haven), sans doublon, au plus <paramref name="max"/>.
        /// </summary>
        public static IReadOnlyList<string> MergeDecor(IEnumerable<string> samples, IEnumerable<string> others, int max) =>
            (samples ?? Array.Empty<string>()).Concat(others ?? Array.Empty<string>()).Where(p => p != null).Distinct()
                .Take(Math.Max(0, max)).ToList();

        /// <summary>
        /// Essai réaliste 8 : modèle (prefab) d'un pack pour l'effet <paramref name="kind"/> : le premier, dans l'ordre de préférence, dont le
        /// nom (sans extension, réduit) est l'un des noms connus (« Basic Hit 2 », « Holy hit », « Magic circle », « Slash VFX »…) ; null
        /// sinon. Seuls les fichiers « .prefab » comptent.
        /// </summary>
        public static string? VfxPrefab(IEnumerable<string> paths, StyleVfx kind)
        {
            var prefabs = (paths ?? Array.Empty<string>()).Where(p => p != null && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            foreach (string name in VfxNames[kind])
            {
                string? found = prefabs.FirstOrDefault(p => Key(ClipName(p)) == name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Noms des effets des packs, par ordre de préférence (réduits : minuscules, sans espaces ni signes).</summary>
        private static readonly Dictionary<StyleVfx, string[]> VfxNames = new Dictionary<StyleVfx, string[]>
        {
            [StyleVfx.Hit] = new[] { "basichit2", "basichit", "basichit7", "stoneshit" },
            [StyleVfx.Crit] = new[] { "basichit8new", "starhit", "holyhit" },
            [StyleVfx.Block] = new[] { "sparksexplodewhite", "sparksexplodeblue", "electrohit" },
            [StyleVfx.Parry] = new[] { "holyhit", "sparksexplodeyellow", "starhit" },
            [StyleVfx.GuardBroken] = new[] { "redenergyexplosion", "sparksexplodered", "stoneshit" },
            [StyleVfx.Slash] = new[] { "slashvfx", "slashfirevfx", "chargeslashblue", "stoneslash" },
            [StyleVfx.Cast] = new[] { "magiccircle", "magiccircle2", "healingcircle" },
            [StyleVfx.Heal] = new[] { "healing", "healingcircle" },
            [StyleVfx.LevelUp] = new[] { "buff", "staraura" },
            [StyleVfx.Death] = new[] { "dustpuff", "smokepuff" },
            [StyleVfx.Slam] = new[] { "dustground", "smokeground", "groundaoeexplosion" },
            [StyleVfx.Charge] = new[] { "sparksflashingyellow", "sparksyellow" },
            // Essai réaliste 8 : Particle Pack de Unity (gratuit, ajouté le 28 octobre 2026).
            [StyleVfx.ForgeFire] = new[] { "tinyflames", "mediumflames" },
            [StyleVfx.ForgeSmoke] = new[] { "smokeeffect", "risingsteam" },
        };

        /// <summary>
        /// Modèles d'un objet de décor (<see cref="StyleProp"/>), dans l'ordre des <see cref="Candidates"/> : FBX d'abord, puis sources
        /// réalistes, puis premier mot cherché, puis chemin ordinal.
        /// </summary>
        public static IReadOnlyList<string> PropCandidates(IEnumerable<string> paths, StyleProp prop)
        {
            (string[] want, string[] avoid) = PropWords[prop];
            int Rank(string p)
            {
                string file = Key(FileName(p));
                for (int i = 0; i < want.Length; i++)
                    if (file.Contains(want[i])) return i;
                return want.Length;
            }
            return (paths ?? Array.Empty<string>()).Where(p =>
                {
                    string file = Key(FileName(p));
                    return want.Any(w => file.Contains(w)) && !avoid.Any(a => file.Contains(a)) && !InteriorRules.IsInterior(p);
                })
                .OrderBy(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(p => IsRealistic(p) ? 0 : 1)
                .ThenBy(Rank)
                .ThenBy(p => p, StringComparer.Ordinal).ToList();
        }

        private static int WantRank(string path, StyleSlot slot)
        {
            string file = Key(FileName(path));
            string[] want = Models[slot].Want;
            for (int i = 0; i < want.Length; i++)
                if (file.Contains(want[i])) return i;
            return want.Length;
        }

        /// <summary>
        /// Animations à part du modèle du héros (Mixamo livre le personnage et chaque animation dans des fichiers séparés) : les autres FBX
        /// rangés dans le même dossier que lui, dans l'ordre ordinal.
        /// </summary>
        public static IReadOnlyList<string> Companions(string hero, IEnumerable<string> paths)
        {
            if (string.IsNullOrEmpty(hero)) return Array.Empty<string>();
            string folder = Folder(hero);
            return (paths ?? Array.Empty<string>())
                .Where(p => p != null && p != hero && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && Folder(p) == folder)
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>Nom d'une animation tirée d'un fichier : le nom du fichier sans extension (« Sword And Shield Idle »).</summary>
        public static string ClipName(string path)
        {
            string file = FileName(path);
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        /// <summary>
        /// Couche de sol d'une texture : seulement les textures d'un dossier nommé « Ground » ; roche ou herbe d'après le nom, terre sinon.
        /// Null : ce n'est pas une texture de sol.
        /// </summary>
        public static StyleGround? GroundOf(string path)
        {
            if (!Segments(path).Any(s => s == "ground")) return null;
            string file = Key(FileName(path));
            // Essai réaliste 8 : chemins et champs d'abord (« chemin_terre_battue », « champ_labour »).
            if (RoadGround.Any(w => file.StartsWith(w, StringComparison.Ordinal))) return StyleGround.Road;
            if (FieldGround.Any(w => file.StartsWith(w, StringComparison.Ordinal))) return StyleGround.Field;
            if (RockGround.Any(w => file.Contains(w))) return StyleGround.Rock;
            if (GrassGround.Any(w => file.Contains(w))) return StyleGround.Grass;
            return StyleGround.Soil;
        }

        /// <summary>Carte de relief (« _nor_gl_ », « _normal ») : un mot du nom de fichier.</summary>
        public static bool IsNormalMap(string path) => Words(path).Any(w => w == "nor" || w == "normal" || w == "nrm" || w == "nm");

        /// <summary>Carte de couleur : ni relief, ni rugosité, ni occlusion, ni hauteur, ni transparence.</summary>
        public static bool IsColorMap(string path) =>
            !IsNormalMap(path) && !Words(path).Any(w => w == "rough" || w == "roughness" || w == "ao" || w == "disp" || w == "height" || w == "arm"
                || w == "spec" || w == "specular" || w == "metal" || w == "metallic" || w == "gloss" || w == "alpha" || w == "opacity" || w == "mask"
                || w == "glow" || w == "emission" || w == "emissive" || w == "translucency" || w == "transmission" || w == "opass");

        /// <summary>Intérieurs : carte de métal (« _metal_ », « _metallic_ »), une donnée que Unity lit sans correction de couleur.</summary>
        public static bool IsMetalMap(string path) => Words(path).Any(w => w == "metal" || w == "metallic");

        /// <summary>Carte de transparence (« _alpha_ », « _opacity », « _opass » du moulin de Sketchfab).</summary>
        public static bool IsAlphaMap(string path) => Words(path).Any(w => w == "alpha" || w == "opacity" || w == "opass");

        /// <summary>Texture de couleur (ou de relief) choisie pour une couche de sol, ou null ; ordre ordinal des chemins.</summary>
        public static string? GroundTexture(IEnumerable<string> paths, StyleGround ground, bool normal) =>
            (paths ?? Array.Empty<string>()).Where(p => GroundOf(p) == ground && (normal ? IsNormalMap(p) : IsColorMap(p)))
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>Ciel : une image HDR (.hdr ou .exr) dont le nom ou le dossier parle de ciel (« …_puresky_1k.hdr »).</summary>
        public static bool IsSky(string path)
        {
            string p = path ?? string.Empty;
            bool hdr = p.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".exr", StringComparison.OrdinalIgnoreCase);
            return hdr && (Key(FileName(p)).Contains("sky") || Segments(p).Any(s => s == "sky"));
        }

        /// <summary>Premier ciel dans l'ordre ordinal, ou null.</summary>
        public static string? Sky(IEnumerable<string> paths) =>
            (paths ?? Array.Empty<string>()).Where(IsSky).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>
        /// Premier son qui correspond à <paramref name="sound"/> (mots dans l'ordre de préférence, puis chemin ordinal), ou null.
        /// </summary>
        public static string? PickSound(IEnumerable<string> paths, StyleSound sound)
        {
            // Essai réaliste 7 : les musiques et les ambiances ne sont pas des bruitages.
            var files = (paths ?? Array.Empty<string>()).Where(p => p != null && !InMusicFolder(p) && !InAmbienceFolder(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            string[] avoid = SoundAvoid.TryGetValue(sound, out string[]? a) ? a : Array.Empty<string>();
            foreach (string word in Sounds[sound])
                if (files.FirstOrDefault(p => Key(FileName(p)).Contains(word) && !avoid.Any(x => Key(FileName(p)).Contains(x))) is string found) return found;
            return null;
        }

        /// <summary>
        /// Essai réaliste 7 : variantes d'un son (jusqu'à <paramref name="max"/>) : les fichiers du premier mot qui en trouve, dans l'ordre
        /// ordinal (« swish-1 », « swish-10 », …), pour qu'un même geste ne sonne pas toujours pareil. Les musiques et les ambiances
        /// (dossiers « Music » et « Ambience ») n'en sont jamais.
        /// </summary>
        public static IReadOnlyList<string> PickSounds(IEnumerable<string> paths, StyleSound sound, int max)
        {
            var files = (paths ?? Array.Empty<string>()).Where(p => p != null && !InMusicFolder(p) && !InAmbienceFolder(p))
                .OrderBy(p => p, StringComparer.Ordinal).ToList();
            string[] avoid = SoundAvoid.TryGetValue(sound, out string[]? a) ? a : Array.Empty<string>();
            foreach (string word in Sounds[sound])
            {
                List<string> found = files.Where(p => Key(FileName(p)).Contains(word) && !avoid.Any(x => Key(FileName(p)).Contains(x)))
                    .Take(Math.Max(0, max)).ToList();
                if (found.Count > 0) return found;
            }
            return Array.Empty<string>();
        }

        /// <summary>Sous-lot V2.9 : sons faits de longs enregistrements (joués par passages), gardés compressés en mémoire sur le téléphone.</summary>
        private static readonly StyleSound[] LongSounds =
            { StyleSound.Purr, StyleSound.Howl, StyleSound.Bellow, StyleSound.BoarGrumble, StyleSound.Tocsin, StyleSound.ChurchBell, StyleSound.RiftHum };

        /// <summary>
        /// Sous-lot V2.9 : le fichier est-il un long enregistrement (ronronnement, hurlements, brame, grognements, cloches, bourdonnement) ?
        /// Décompressé au chargement, il prendrait des dizaines de Mo : il reste compressé en mémoire (comme les ambiances).
        /// </summary>
        public static bool IsLongRecording(string path)
        {
            if (path is null || InMusicFolder(path) || InAmbienceFolder(path)) return false;
            string file = Key(FileName(path));
            foreach (StyleSound sound in LongSounds)
            {
                string[] avoid = SoundAvoid.TryGetValue(sound, out string[]? a) ? a : Array.Empty<string>();
                if (Sounds[sound].Any(w => file.Contains(w)) && !avoid.Any(x => file.Contains(x))) return true;
            }
            return false;
        }

        /// <summary>Essai réaliste 7 : musique d'un fichier rangé dans un dossier « Music » (ou « Musique »), d'après son nom ; null sinon.</summary>
        public static StyleMusic? MusicOf(string path)
        {
            if (!InMusicFolder(path)) return null;
            string file = Key(FileName(path));
            foreach ((StyleMusic kind, string[] words) in MusicWords)
                if (words.Any(w => file.Contains(w))) return kind;
            return null;
        }

        /// <summary>Essai réaliste 7 : ambiance d'un fichier rangé dans un dossier « Ambience » (ou « Ambiance »), d'après son nom ; null sinon.</summary>
        public static StyleAmbience? AmbienceOf(string path)
        {
            if (!InAmbienceFolder(path)) return null;
            string file = Key(FileName(path));
            foreach ((StyleAmbience kind, string[] words) in AmbienceWords)
                if (words.Any(w => file.Contains(w))) return kind;
            return null;
        }

        /// <summary>
        /// Relecture de l'essai 7 : le fichier est-il rangé dans un dossier de musiques (« Music », « Musique ») ? Même si son nom n'est
        /// pas reconnu : il n'est alors ni une musique jouée ni un bruitage, et Unity le lit en continu (pas décompressé en mémoire).
        /// </summary>
        public static bool InMusicFolder(string path) => Segments(path).Any(s => s == "music" || s == "musique" || s == "musiques");

        /// <summary>Relecture de l'essai 7 : le fichier est-il rangé dans un dossier d'ambiances (« Ambience », « Ambiance ») ?</summary>
        public static bool InAmbienceFolder(string path) => Segments(path).Any(s => s == "ambience" || s == "ambiance" || s == "ambiances");

        /// <summary>
        /// Essai réaliste 7 : musique à jouer. Le Gardien engagé l'emporte, puis le combat (un adversaire vivant tout proche), puis le
        /// village, sinon l'exploration.
        /// </summary>
        public static StyleMusic MusicFor(bool bossEngaged, bool fighting, bool inVillage) =>
            bossEngaged ? StyleMusic.Boss : fighting ? StyleMusic.Combat : inVillage ? StyleMusic.Village : StyleMusic.Exploration;

        /// <summary>
        /// Essai réaliste 7 : volume d'une nappe d'ambiance (0 à 1) : la rivière d'après la distance à son lit (pleine à 4 m, éteinte à
        /// 30 m), le vent un peu plus fort loin du village, les oiseaux partout, plus discrets en combat. Intérieurs (relecture
        /// indépendante) : ni oiseaux ni rivière, le vent du dehors à peine.
        /// </summary>
        /// <param name="night">Sous-lot V2.1 : part de nuit (0 à 1) : les oiseaux se taisent la nuit, la nappe de nuit (grillons, hibou) prend
        /// leur place, en douceur à l'aube et au crépuscule.</param>
        /// <param name="rain">Sous-lot V2.2 : pluie (0 à 1) : la nappe de pluie la suit (assourdie dans une pièce : on l'entend sur le toit), les
        /// oiseaux se taisent sous l'averse, le vent forcit un peu. Le tonnerre n'est pas une nappe (0 : il est joué après chaque éclair).</param>
        public static double AmbienceVolume(StyleAmbience ambience, double riverDistance, bool inVillage, bool fighting, bool indoors = false, double night = 0,
            double rain = 0)
        {
            double calm = fighting ? 0.5 : 1;
            rain = double.IsNaN(rain) ? 0 : Math.Max(0, Math.Min(1, rain));
            if (ambience == StyleAmbience.Thunder) return 0;
            if (ambience == StyleAmbience.Rain) return (indoors ? 0.3 : 0.4 + 0.6 * rain) * Math.Min(1, rain * 4) * (fighting ? 0.8 : 1);
            if (indoors) return ambience == StyleAmbience.Wind ? 0.12 * calm : 0;
            night = double.IsNaN(night) ? 0 : Math.Max(0, Math.Min(1, night));
            switch (ambience)
            {
                case StyleAmbience.Night: return (inVillage ? 0.6 : 0.9) * calm * night * (1 - 0.7 * rain);
                case StyleAmbience.Birds: return (inVillage ? 0.7 : 1.0) * calm * (1 - 0.9 * night) * (1 - 0.85 * rain);
                case StyleAmbience.River:
                    if (double.IsNaN(riverDistance) || riverDistance < 0) return 0;
                    return Math.Max(0, Math.Min(1, (30 - riverDistance) / 26.0));
                case StyleAmbience.Wind: return Math.Min(1, (inVillage ? 0.35 : 0.6) * calm * (1 + 0.5 * rain));
                default: return (inVillage ? 0.7 : 1.0) * calm;
            }
        }

        /// <summary>
        /// Choix stable d'un modèle parmi <paramref name="count"/> pour l'élément <paramref name="id"/> (« arbre3 » a toujours le même arbre) :
        /// empreinte FNV-1a de l'identifiant, sans hasard. -1 s'il n'y a aucun modèle.
        /// </summary>
        public static int Variant(string id, int count)
        {
            if (count <= 0) return -1;
            uint hash = 2166136261;
            foreach (char c in id ?? string.Empty)
            {
                hash ^= c;
                hash = unchecked(hash * 16777619);
            }
            return (int)(hash % (uint)count);
        }

        /// <summary>
        /// Animation à jouer parmi celles du modèle (noms des clips, « HumanArmature|Idle »), avec repli ; null si aucune ne convient. Pour
        /// chaque mot, le nom exact l'emporte (« Idle » avant « Idle_swordLeft »), puis un nom qui commence par le mot, puis un nom qui le
        /// contient.
        /// </summary>
        public static string? Clip(IEnumerable<string> clips, StyleMotion motion) => Clip(clips, motion, null, 0);

        /// <summary>
        /// Essai réaliste 7 : comme <see cref="Clip(IEnumerable{string}, StyleMotion)"/>, avec une préférence (<paramref name="prefer"/> : un
        /// mot du nom, « hilt », ou la fin du nom réduit, « shieldslash ») puis, sans elle, une variante stable parmi toutes les animations
        /// du mouvement (<paramref name="variant"/> positif : rang modulo leur nombre, dans l'ordre ordinal). Les animations réservées
        /// (garde, sort, coup reçu) ne servent qu'à leur mouvement : « Sword And Shield Block Idle » n'est pas une attente,
        /// « Idle_HitReact_Left » n'est pas une attaque.
        /// </summary>
        public static string? Clip(IEnumerable<string> clips, StyleMotion motion, string? prefer, int variant)
        {
            var all = (clips ?? Array.Empty<string>()).ToList();
            // Relecture de l'essai 7 : « !mot » écarte les animations de ce mot (une attaque ordinaire n'est pas le saut du Gardien), sauf
            // s'il ne reste qu'elles.
            string? exclude = prefer != null && prefer.StartsWith("!", StringComparison.Ordinal) ? Key(prefer.Substring(1)) : null;
            if (exclude != null) prefer = null;
            foreach (StyleMotion m in new[] { motion }.Concat(Fallbacks[motion]))
            {
                List<string> names = all.Where(n => Usable(n, m)).ToList();
                if (m == motion && !string.IsNullOrEmpty(exclude))
                {
                    List<string> kept = names.Where(n => !HasWord(n, new[] { exclude! })).ToList();
                    if (kept.Any(n => HasWord(n, Clips[m]))) names = kept;
                }
                if (m == motion && !string.IsNullOrEmpty(prefer))
                {
                    string want = Key(prefer);
                    string? liked = names.Where(n => HasWord(n, Clips[m])).OrderBy(n => n, StringComparer.Ordinal)
                        .FirstOrDefault(n => HasWord(n, new[] { want }) || Key(n).EndsWith(want, StringComparison.Ordinal));
                    if (liked != null) return liked;
                }
                if (m == motion && variant > 0)
                {
                    List<string> every = names.Where(n => HasWord(n, Clips[m])).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    if (every.Count > 0) return every[variant % every.Count];
                }
                foreach (string word in Clips[m])
                {
                    string? found = names.FirstOrDefault(n => Segment(n) == word) ?? names.FirstOrDefault(n => Segment(n).StartsWith(word, StringComparison.Ordinal))
                        ?? names.FirstOrDefault(n => HasWord(n, new[] { word }));
                    if (found != null) return found;
                }
            }
            return null;
        }

        /// <summary>
        /// L'animation <paramref name="clip"/> peut-elle servir au mouvement <paramref name="motion"/> ? Une animation réservée (garde,
        /// sort, coup reçu, coup sur la garde) ne sert qu'au sien ; un coup reçu sur la garde n'est pas un coup reçu sans garde.
        /// </summary>
        private static bool Usable(string clip, StyleMotion motion)
        {
            foreach (StyleMotion reserved in Reserved)
                if (reserved != motion && HasWord(clip, Clips[reserved]))
                {
                    // « Sword And Shield Guard Impact » : coup sur la garde (réservé), pas un coup reçu ni une garde.
                    if (motion == StyleMotion.GuardHit && reserved == StyleMotion.Hurt) continue;
                    return false;
                }
            return true;
        }

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « coups variés ») : animation d'une compétence du joueur. L'ultime et
        /// les soutiens (Provocation, Barrière d'acier) sont des sorts ; chaque frappe du Bastion a son geste de Mixamo (Frappe du rempart :
        /// le coup haut, Heurt de bouclier : le coup de garde, Contrecoup : la frappe puissante, Onde de rupture : la taille vers le sol) ;
        /// une autre compétence prend une attaque variée d'après son identifiant. <c>Prefer</c> : voir <see cref="Clip(IEnumerable{string}, StyleMotion, string, int)"/>.
        /// </summary>
        public static (StyleMotion Motion, string? Prefer, int Variant) SkillAnimation(string skillId, bool ultimate)
        {
            if (ultimate) return (StyleMotion.Cast, null, 0);
            // Relecture de l'essai 7 : mot à mot, sur le dernier segment de l'identifiant (« skill.bastion.defi » ne contient pas « onde »,
            // ni « besoin » « soin »).
            string[] words = IdWords(skillId);
            foreach ((string word, StyleMotion motion, string? prefer) in SkillMoves)
                if (words.Contains(word)) return (motion, prefer, 0);
            return (StyleMotion.Attack, null, 1 + Variant(skillId, 997));
        }

        /// <summary>Mots du dernier segment d'un identifiant (« skill.bastion.onde_de_rupture » → onde, de, rupture), réduits.</summary>
        private static string[] IdWords(string id)
        {
            string s = id ?? string.Empty;
            int dot = s.LastIndexOf('.');
            return s.Substring(dot + 1).Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Key).Where(w => w.Length > 0).ToArray();
        }

        private static readonly (string Word, StyleMotion Motion, string? Prefer)[] SkillMoves =
        {
            ("provocation", StyleMotion.Cast, null), ("barriere", StyleMotion.Cast, null), ("soin", StyleMotion.Cast, null),
            ("frappe", StyleMotion.Attack, "high"), ("heurt", StyleMotion.Attack, "hilt"), ("contrecoup", StyleMotion.Attack, "power"),
            ("onde", StyleMotion.Attack, "shieldslash"),
        };

        /// <summary>
        /// Essai réaliste 7 : animation d'une attaque d'un adversaire, d'après les mots de l'identifiant de l'attaque. Relecture de l'essai
        /// 7 : d'après les vraies attaques des Vallons. Une onde ou une pluie (onde du Seuil, onde du rôdeur, pluie de flèches) est lancée
        /// d'un cri (« Gardien Roar » ; sans geste de cri, une attaque) ; une ruée ou un bond est un saut (« Gardien Jump Attack ») ; les
        /// autres (griffe, morsure, flèche, éclat, charge) prennent une attaque variée, stable pour cette attaque, jamais le saut.
        /// </summary>
        public static (StyleMotion Motion, string? Prefer, int Variant) AttackAnimation(string attackId)
        {
            string[] words = IdWords(attackId);
            if (words.Any(w => CastAttackWords.Any(c => w.StartsWith(c, StringComparison.Ordinal)))) return (StyleMotion.Cast, null, 0);
            if (words.Any(w => LeapAttackWords.Any(c => w.StartsWith(c, StringComparison.Ordinal)))) return (StyleMotion.Attack, "jump", 0);
            return (StyleMotion.Attack, NotJump, 1 + Variant(attackId, 997));
        }

        private static readonly string[] CastAttackWords = { "onde", "pluie", "rugi", "roar", "hurle", "cri", "volee" };   // sous-lot V1.6 : la volée des bandits archers, comme la pluie
        private static readonly string[] LeapAttackWords = { "ruee", "bond", "saut", "jump", "ecras" };

        /// <summary>Préférence spéciale de <see cref="AttackAnimation"/> : toute attaque sauf le saut.</summary>
        public const string NotJump = "!jump";

        /// <summary>Dernier segment d'un nom de clip (après le dernier « | »), réduit.</summary>
        private static string Segment(string clip)
        {
            string c = clip ?? string.Empty;
            int bar = c.LastIndexOf('|');
            return Key(bar >= 0 ? c.Substring(bar + 1) : c);
        }

        /// <summary>
        /// Animations qui tournent en boucle (attente, marche, course ; sous-lot V2.9 : brouter, « Eating » des bêtes de Quaternius, qui
        /// restait figé sur sa dernière image).
        /// </summary>
        public static bool Loops(string clip) =>
            new[] { StyleMotion.Idle, StyleMotion.Walk, StyleMotion.Run, StyleMotion.Graze }.Any(m => HasWord(clip, Clips[m]))
                && !HasWord(clip, Clips[StyleMotion.Death]) && !HasWord(clip, Clips[StyleMotion.Attack])
                && !HasWord(clip, Clips[StyleMotion.Hurt]) && !HasWord(clip, Clips[StyleMotion.Cast]);

        /// <summary>
        /// Un mot du nom commence-t-il par l'un des mots cherchés ? Les mots sont coupés aux signes et aux majuscules (« Run_swordAttack » →
        /// run, sword, attack). Essai du 26 octobre 2026 : « Gardien Idle » contient « die » au milieu d'un mot, et ne doit pas passer pour
        /// une mort.
        /// </summary>
        private static bool HasWord(string clip, string[] wanted)
        {
            List<string> words = Regex.Matches(clip ?? string.Empty, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])").Cast<Match>().Select(m => m.Value.ToLowerInvariant()).ToList();
            // Essai réaliste 8 : deux mots qui se suivent comptent aussi ensemble (« GetHit » : get, hit, et gethit, un coup reçu).
            IEnumerable<string> all = words.Concat(words.Zip(words.Skip(1), (a, b) => a + b));
            return all.Any(w => wanted.Any(x => w.StartsWith(x, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Allure d'après la vitesse au sol affichée (m/s) : attente sous 0,3, marche sous 3,5, course au-delà ; la mort l'emporte, puis
        /// l'attaque.
        /// </summary>
        public static StyleMotion Motion(double speed, bool dead, bool attacking) => Motion(speed, dead, attacking, false);

        /// <summary>Essai réaliste 6 : la garde levée à l'arrêt joue le blocage (en marchant, la marche l'emporte).</summary>
        public static StyleMotion Motion(double speed, bool dead, bool attacking, bool guarding) =>
            dead ? StyleMotion.Death : attacking ? StyleMotion.Attack : speed < 0.3 ? (guarding ? StyleMotion.Block : StyleMotion.Idle)
                : speed < 3.5 ? StyleMotion.Walk : StyleMotion.Run;

        /// <summary>
        /// Sous-lot V2.4.1 (bêtes qui se promènent) : comme <see cref="Motion(double, bool, bool, bool)"/>, et une bête arrêtée qui broute ou
        /// fouille (<paramref name="wander"/> : <c>graze</c>, <c>root</c>, voir <c>CombatantView.Wander</c>) joue le broutage. Une bête qui
        /// erre marche lentement : sa marche reste la marche.
        /// </summary>
        public static StyleMotion Motion(double speed, bool dead, bool attacking, bool guarding, string? wander) =>
            !dead && !attacking && speed < 0.3 && (wander == "graze" || wander == "root") ? StyleMotion.Graze : Motion(speed, dead, attacking, guarding);

        /// <summary>Facteur d'échelle uniforme qui donne au modèle la hauteur voulue (1 si sa hauteur est nulle ou inconnue).</summary>
        public static double Scale(double modelHeight, double height) =>
            modelHeight > 1e-6 && height > 0 ? height / modelHeight : 1;

        /// <summary>
        /// Échelle d'une plante : sa taille réelle si elle est plausible (entre 2 cm et 2 m : les modèles réalistes sont à l'échelle du
        /// mètre ; la mousse ne fait que quelques centimètres), sinon la hauteur demandée.
        /// </summary>
        public static double NaturalScale(double modelHeight, double height) =>
            modelHeight >= 0.02 && modelHeight <= 2.0 ? 1 : Scale(modelHeight, height);

        /// <summary>Modèle exporté par Poly Haven : nom de fichier terminé par sa résolution (« rock_07_1k »).</summary>
        public static bool IsPolyHavenName(string model) => Regex.IsMatch(model ?? string.Empty, "_(1|2|4|8|16)k$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Pièces à garder d'un modèle de Poly Haven pour l'élément <paramref name="id"/> (essai de Damien du 26 octobre 2026 : des rangées
        /// de pissenlits et des arbustes alignés). Poly Haven livre chaque modèle en lot de variantes posées côte à côte
        /// (« dandelion_01_a_LOD0 », « dandelion_01_b_LOD0 »…), chacune avec ses niveaux de détail (« _LOD1 »…) : on garde une seule
        /// variante, choisie d'après l'identifiant, à son meilleur niveau de détail. Null : tout garder (pas un modèle de Poly Haven, ou
        /// une seule pièce).
        /// </summary>
        public static IReadOnlyList<int>? KeepParts(string model, IReadOnlyList<string> parts, string id)
        {
            if (!IsPolyHavenName(model) || parts == null || parts.Count < 2) return null;
            var named = parts.Select((p, i) => (Index: i, Variant: Regex.Replace(p ?? string.Empty, "_LOD[0-9]+$", "", RegexOptions.IgnoreCase),
                Lod: Lod(p))).ToList();
            List<string> variants = named.Select(n => n.Variant).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList();
            string chosen = variants[Math.Max(0, Variant((id ?? string.Empty) + "/variante", variants.Count))];
            int best = named.Where(n => n.Variant == chosen).Min(n => n.Lod);
            return named.Where(n => n.Variant == chosen && n.Lod == best).Select(n => n.Index).ToList();
        }

        private static int Lod(string? part)
        {
            Match m = Regex.Match(part ?? string.Empty, "_LOD([0-9]+)$", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out int lod) ? lod : 0;
        }

        /// <summary>
        /// Couches du sol en (<paramref name="x"/>, <paramref name="z"/>) : la roche autour des endroits rocheux, la terre autour des arbres
        /// et par taches (bruit fixe, sans hasard), l'herbe ailleurs. La roche l'emporte, puis la terre ; la somme fait 1.
        /// </summary>
        public static StyleMix GroundMix(double x, double z, IReadOnlyList<StyleSpot> spots)
        {
            double rock = 0, soil = 0;
            foreach (StyleSpot spot in spots ?? Array.Empty<StyleSpot>())
            {
                double dx = x - spot.X, dz = z - spot.Z;
                double outside = Math.Max(0, Math.Sqrt(dx * dx + dz * dz) - Math.Max(0, spot.Radius));
                double w = Math.Max(0, 1 - outside / SpotFade);
                if (spot.Ground == StyleGround.Rock) rock = Math.Max(rock, w);
                else if (spot.Ground == StyleGround.Soil) soil = Math.Max(soil, w * 0.85);
            }
            // Taches de terre dans l'herbe : bruit de valeur lissé, sur une grille de PatchSize mètres.
            double patch = Math.Min(1, Math.Max(0, (Noise(x / PatchSize, z / PatchSize) - 0.58) * 2.5)) * 0.7;
            soil = Math.Max(soil, patch);
            double r = Math.Min(1, rock), s = (1 - r) * Math.Min(1, soil);
            return new StyleMix(s, r, Math.Max(0, 1 - r - s));
        }

        /// <summary>
        /// Places des arbres de la lisière, hors de l'aire de jeu (simple décor : le moteur ne les connaît pas) : dans la bande de
        /// <paramref name="margin"/> mètres autour de l'aire, à plus de <paramref name="gap"/> mètres de son bord et à plus de
        /// <paramref name="spacing"/> mètres l'un de l'autre. Suite fixe : les mêmes places à chaque lancement ; au plus
        /// <paramref name="count"/> (moins si la bande est pleine).
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> Forest(double minX, double minZ, double maxX, double maxZ, double margin, int count,
            double gap = 4, double spacing = 3.5)
        {
            var spots = new List<(double X, double Z)>();
            if (count <= 0 || margin <= gap) return spots;
            uint seed = 2463534242;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            for (int attempt = 0; attempt < count * 30 && spots.Count < count; attempt++)
            {
                double x = minX - margin + (maxX - minX + 2 * margin) * Next();
                double z = minZ - margin + (maxZ - minZ + 2 * margin) * Next();
                if (x > minX - gap && x < maxX + gap && z > minZ - gap && z < maxZ + gap) continue;
                if (spots.Any(s => (s.X - x) * (s.X - x) + (s.Z - z) * (s.Z - z) < spacing * spacing)) continue;
                spots.Add((x, z));
            }
            return spots;
        }

        /// <summary>
        /// Une touffe d'herbe en brins : place au sol, rotation (degrés) et échelle ; essai réaliste 5 : <see cref="Flowers"/> si la touffe
        /// porte des fleurs.
        /// </summary>
        public readonly struct GrassSpot
        {
            public GrassSpot(double x, double z, double yaw, double scale, bool flowers = false)
            {
                X = x;
                Z = z;
                Yaw = yaw;
                Scale = scale;
                Flowers = flowers;
            }

            public double X { get; }
            public double Z { get; }
            public double Yaw { get; }
            public double Scale { get; }
            public bool Flowers { get; }
        }

        /// <summary>Taille (m) des plaques d'herbe haute ou rase, et des coins fleuris (essai réaliste 5).</summary>
        public const double MeadowPatch = 7.0;

        /// <summary>
        /// Essai réaliste 4 : places des touffes d'herbe en brins sur [minX, maxX] × [minZ, maxZ], environ <paramref name="density"/> par
        /// mètre carré : une par case d'une grille régulière, décalée au hasard dans sa case (suite fixe : les mêmes à chaque lancement),
        /// tournée, d'échelle 0,47 à 1,67 (plaques d'herbe haute ou rase), fleurie par endroits. Vide si la densité ou l'emprise est nulle. Au plus <paramref name="max"/> touffes : une grande
        /// emprise reçoit une herbe plus clairsemée, mais partout (pas seulement ses premières rangées).
        /// </summary>
        public static IReadOnlyList<GrassSpot> GrassSpots(double minX, double minZ, double maxX, double maxZ, double density, int max = 200000)
        {
            var spots = new List<GrassSpot>();
            if (!(density > 0) || maxX <= minX || maxZ <= minZ || max <= 0) return spots;
            double cells = (Math.Ceiling((maxX - minX) * Math.Sqrt(density)) + 1) * (Math.Ceiling((maxZ - minZ) * Math.Sqrt(density)) + 1);
            if (cells > max) density *= max / cells;
            double step = 1 / Math.Sqrt(density);
            uint seed = 0x2545F491;
            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return (seed >> 8) / 16777216.0;
            }
            for (double z = minZ; z < maxZ && spots.Count < max; z += step)
                for (double x = minX; x < maxX && spots.Count < max; x += step)
                {
                    double px = Math.Min(maxX, x + Next() * step), pz = Math.Min(maxZ, z + Next() * step);
                    // Essai réaliste 5 (« l'herbe pourrait être encore mieux ») : un pré n'est pas tondu : des plaques d'herbe plus haute et
                    // d'autres plus rases (bruit lissé), et des coins fleuris.
                    double meadow = 0.55 + 0.9 * Noise(px / MeadowPatch, pz / MeadowPatch);
                    double scale = meadow * (0.85 + 0.3 * Next()), yaw = Next() * 360, roll = Next();
                    bool flowers = Noise(px / (MeadowPatch * 0.7) + 101.3, pz / (MeadowPatch * 0.7) - 57.1) > 0.6 && roll < 0.35;
                    spots.Add(new GrassSpot(px, pz, yaw, scale, flowers));
                }
            return spots;
        }

        /// <summary>Bruit de valeur lissé dans [0, 1], fixe (empreinte entière des coins de la case).</summary>
        public static double Noise(double x, double z)
        {
            double fx = Math.Floor(x), fz = Math.Floor(z);
            long ix = (long)fx, iz = (long)fz;
            double tx = Smooth(x - fx), tz = Smooth(z - fz);
            double a = Corner(ix, iz), b = Corner(ix + 1, iz), c = Corner(ix, iz + 1), d = Corner(ix + 1, iz + 1);
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz;
        }

        private static double Smooth(double t) => t * t * (3 - 2 * t);

        private static double Corner(long x, long z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)z * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215.0;
            }
        }

        /// <summary>
        /// Ce qu'habille un combattant, d'après son identifiant (vies suivantes comprises : « loup1_v3 ») : le joueur en chevalier, les loups
        /// et les sangliers ; depuis l'essai réaliste (demande de Damien du 26 octobre 2026), les autres (archer, rôdeur, Gardien) prennent le
        /// personnage de leur dossier Mixamo s'il existe (<see cref="CharacterOf"/>) ; sans lui, ils gardent leur forme simple.
        /// </summary>
        public static StyleSlot ForCombatant(string id, bool player)
        {
            if (player) return StyleSlot.Hero;
            string key = Key(id);
            // Sous-lot V1.9.8 : Croc-Gris (ses deux formes) est un loup.
            if (key.StartsWith("loup", StringComparison.Ordinal) || key.StartsWith("crocgris", StringComparison.Ordinal)) return StyleSlot.Wolf;
            if (key.StartsWith("sanglier", StringComparison.Ordinal)) return StyleSlot.Boar;
            // Sous-lot V2.9 : cerfs (cerf à bois ou biche) et lièvres ont leur modèle ; sans lui, leurs formes simples (WildlifeLook).
            if (key.StartsWith("cerf", StringComparison.Ordinal)) return DeerSlot(id);
            if (key.StartsWith("lievre", StringComparison.Ordinal)) return StyleSlot.Hare;
            return StyleSlot.Character;
        }

        /// <summary>
        /// Sous-lot V2.9 : un cerf de la harde est un cerf à bois (le chef, « cerf1 », puis un sur trois : « cerf4 », « cerf7 »…) ou une biche
        /// (les autres) ; les vies suivantes (« cerf4_v2 ») gardent le leur. Affichage seulement.
        /// </summary>
        public static StyleSlot DeerSlot(string id)
        {
            Match m = Regex.Match(id ?? string.Empty, "^cerf(\\d+)");
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int n)) return StyleSlot.Stag;
            return n % 3 == 1 ? StyleSlot.Stag : StyleSlot.Doe;
        }

        /// <summary>Sous-lot V2.9 : une bête habillée d'un modèle (loup, sanglier, cerf, biche, lièvre) : centrée sur son corps, voix 3D.</summary>
        public static bool IsBeast(StyleSlot slot) =>
            slot == StyleSlot.Wolf || slot == StyleSlot.Boar || slot == StyleSlot.Stag || slot == StyleSlot.Doe || slot == StyleSlot.Hare;

        /// <summary>
        /// Sous-lot V2.9 : rotation (degrés) proposée par la préparation pour un modèle connu qui ne regarde pas devant lui une fois importé :
        /// le sanglier de Hanabi_Map est tourné de 60,7° dans son fichier (mesuré sous Blender : le groin pointe à 60,7° de l'axe) ; les
        /// bêtes de Quaternius, le lièvre de Pneshik et le chat regardent devant eux (0). Réglable ensuite dans le catalogue.
        /// </summary>
        public static double ModelYaw(string path)
        {
            string file = Key(FileName(path));
            return file.Contains("hanabi") || file.Contains("jabali") ? 60.7 : 0;
        }

        /// <summary>
        /// Sous-lot V1.9.8 : hauteur (m) du modèle d'une bête (loup, sanglier) : 0,95 ; Croc-Gris, le chef de la meute du pré, est bien plus
        /// massif que les autres loups (1,4 ; enragé, 1,5). Affichage seulement.
        /// </summary>
        public static double BeastHeight(string id)
        {
            string key = Key(id);
            if (key.StartsWith("crocgrisenrage", StringComparison.Ordinal)) return 1.5;
            if (key.StartsWith("crocgris", StringComparison.Ordinal)) return 1.4;
            // Sous-lot V2.9 : hauteur des modèles des bêtes paisibles (bois ou oreilles compris).
            if (key.StartsWith("cerf", StringComparison.Ordinal)) return DeerSlot(id) == StyleSlot.Stag ? 2.05 : 1.6;
            if (key.StartsWith("lievre", StringComparison.Ordinal)) return 0.5;
            return 0.95;
        }

        /// <summary>
        /// Personnage d'un PNJ ou d'un ennemi : le premier mot de son identifiant, sans le préfixe « npc » ni les chiffres (« npc.odile » →
        /// odile, « gardien_eveille » → gardien, « archer_v2 » → archer). C'est le nom du dossier du personnage dans <c>Assets/Style/Mixamo</c>,
        /// réduit. Vide si l'identifiant n'a aucun mot.
        /// </summary>
        public static string CharacterOf(string id)
        {
            foreach (string word in Regex.Split(id ?? string.Empty, "[^A-Za-z]+"))
            {
                string w = Key(word);
                if (w.Length == 0 || w == "npc" || w == "pnj") continue;
                return w;
            }
            return string.Empty;
        }

        /// <summary>
        /// Personnages rangés chacun dans un dossier de <c>Mixamo</c> (« Assets/Style/Mixamo/Archer/Archer Idle.fbx ») : nom du dossier
        /// réduit → ses FBX dans l'ordre ordinal. Le héros, posé à la racine de <c>Mixamo</c>, et les dossiers « Textures » n'en sont pas.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<string>> Characters(IEnumerable<string> paths)
        {
            var found = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in paths ?? Array.Empty<string>())
            {
                if (path == null || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                string[] parts = path.Replace('\\', '/').Split('/');
                if (parts.Length < 3 || Key(parts[parts.Length - 3]) != "mixamo") continue;
                string name = Key(parts[parts.Length - 2]);
                if (name.Length == 0 || name == "textures") continue;
                if (!found.TryGetValue(name, out List<string>? list)) found[name] = list = new List<string>();
                list.Add(path);
            }
            return found.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value.OrderBy(p => p, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Texture d'un matériau de Mixamo (couleur, ou relief si <paramref name="normal"/>) : celle dont le nom commence par celui du matériau
        /// (« Body_MAT1 » → Body_DIFF.jpg, « maria_M1 » → maria_diffuse.png), sinon la seule texture de ce genre s'il n'y en a qu'une
        /// (« warrok_MAT » → bear_diffuse.png) ; null sinon.
        /// </summary>
        public static string? MaterialTexture(string material, IEnumerable<string> textures, bool normal)
        {
            List<string> kind = (textures ?? Array.Empty<string>()).Where(t => t != null && (normal ? IsNormalMap(t) : IsColorMap(t)))
                .OrderBy(t => t, StringComparer.Ordinal).ToList();
            string stem = Regex.Replace(Key(material), "(mat|m)[0-9]*$", "");
            if (stem.Length > 0 && kind.FirstOrDefault(t => Key(FileName(t)).StartsWith(stem, StringComparison.Ordinal)) is string named) return named;
            if (kind.Count == 1) return kind[0];
            // Essai réaliste 4 : les arbres ont une matière d'écorce et une de feuilles, nommées librement (« leaves », « Bark ») : la texture
            // qui parle de la même chose (feuille, branche, aiguille ; écorce, tronc, bois).
            foreach (string[] words in new[] { FoliageWords, BarkWords })
                if (words.Any(w => Key(material).Contains(w)) && kind.FirstOrDefault(t => words.Any(w => Key(FileName(t)).Contains(w))) is string alike)
                    return alike;
            return null;
        }

        private static readonly string[] FoliageWords = { "leaf", "leaves", "branch", "needle", "foliage", "twig" };
        private static readonly string[] BarkWords = { "bark", "trunk", "wood", "stem" };

        /// <summary>
        /// Racine d'une texture pour l'apparier à sa transparence : les mots du nom sans ceux du genre de carte ni la résolution
        /// (« fern_02_diff_2k.jpg » et « fern_02_alpha_2k.jpg » → fern02 ; « oak leaf.png » et « oak_leaf_alpha.png » → oakleaf).
        /// </summary>
        public static string TextureStem(string path)
        {
            var words = Words(path).ToList();
            if (words.Count > 1) words.RemoveAt(words.Count - 1);   // extension
            return Key(string.Concat(words.Where(w => !MapWords.Contains(w) && !Regex.IsMatch(w, "^[0-9]+k$"))));
        }

        private static readonly HashSet<string> MapWords = new HashSet<string>(StringComparer.Ordinal)
            { "diff", "diffuse", "albedo", "basecolor", "color", "col", "alpha", "opacity", "opass", "mask", "normal", "nor", "gl", "dx", "nrm", "nm" };

        /// <summary>
        /// Essai réaliste 4 : textures d'une pièce d'un arbre de Sketchfab (fichiers de Tree It : pas de matière, une texture par pièce,
        /// pièces nommées « oak leaf_1 », « bark08_0 » ou « Tree_0 »). Couleur : celle dont la racine est le nom de la pièce sans son
        /// numéro ; sinon l'écorce pour la pièce 0 et le feuillage pour les autres. Relief : celui de même racine que la couleur. Null s'il
        /// n'y a rien.
        /// </summary>
        public static (string Color, string? Normal)? PartTextures(string part, IEnumerable<string> textures)
        {
            var all = (textures ?? Array.Empty<string>()).Where(t => t != null).OrderBy(t => t, StringComparer.Ordinal).ToList();
            List<string> colors = all.Where(IsColorMap).ToList();
            Match numbered = Regex.Match(part ?? string.Empty, "^(.*)_([0-9]+)$");
            string name = numbered.Success ? numbered.Groups[1].Value : part ?? string.Empty;
            int index = numbered.Success && int.TryParse(numbered.Groups[2].Value, out int i) ? i : 0;
            string? color = colors.FirstOrDefault(t => TextureStem(t) == Key(name))
                ?? (index == 0 ? colors.FirstOrDefault(t => BarkWords.Any(w => Key(FileName(t)).Contains(w)))
                    : colors.FirstOrDefault(t => FoliageWords.Any(w => Key(FileName(t)).Contains(w))));
            if (color == null) return null;
            return (color, all.Where(IsNormalMap).FirstOrDefault(t => TextureStem(t) == TextureStem(color)));
        }

        /// <summary>Une texture de couleur de feuillage (feuille, branche, aiguille…), à transparence découpée.</summary>
        public static bool IsFoliage(string texture) => FoliageWords.Any(w => Key(FileName(texture)).Contains(w));

        /// <summary>
        /// Transparence d'une texture de couleur : la carte de transparence de même racine (<see cref="TextureStem"/>), sinon null.
        /// </summary>
        public static string? AlphaFor(string color, IEnumerable<string> textures)
        {
            string stem = TextureStem(color);
            return stem.Length == 0 ? null : (textures ?? Array.Empty<string>()).Where(t => t != null && IsAlphaMap(t) && TextureStem(t) == stem)
                .OrderBy(t => t, StringComparer.Ordinal).FirstOrDefault();
        }

        /// <summary>
        /// Essai réaliste 4 (choix de Damien du 27 octobre 2026 : textures 2k et ciel 8k) : quand un même fichier de Poly Haven est là en
        /// plusieurs résolutions (« rock_07_1k.fbx » et « rock_07_2k.fbx », « …_puresky_1k.hdr » et « …_8k.hdr »), seule la plus haute reste ;
        /// les fichiers sans résolution dans leur nom restent tous. Ordre d'origine gardé.
        /// </summary>
        public static IReadOnlyList<string> Highest(IEnumerable<string> paths)
        {
            var list = (paths ?? Array.Empty<string>()).Where(p => p != null).ToList();
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string p in list)
                if (Resolution(p) is (string identity, int k) && (!best.TryGetValue(identity, out int b) || k > b)) best[identity] = k;
            return list.Where(p => !(Resolution(p) is (string identity, int k)) || best[identity] == k).ToList();
        }

        /// <summary>Nom sans résolution (en minuscules) et résolution en k d'un fichier de Poly Haven, ou null sans résolution.</summary>
        private static (string Identity, int K)? Resolution(string path)
        {
            Match m = Regex.Match(FileName(path), "_([0-9]+)k(?=[._])", RegexOptions.IgnoreCase);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int k)) return null;
            string file = FileName(path);
            return ((file.Substring(0, m.Index) + file.Substring(m.Index + m.Length)).ToLowerInvariant(), k);
        }

        /// <summary>
        /// Matière d'une texture de bâtiment : seulement les textures d'un dossier nommé « Surfaces » ; crépi, tuiles, planches, pavés ou
        /// pierre d'après le nom (dans cet ordre : « plastered_stone_wall » est du crépi, « cobblestone » des pavés). Null sinon.
        /// </summary>
        public static StyleSurface? SurfaceOf(string path)
        {
            if (!Segments(path).Any(s => s == "surfaces")) return null;
            string file = Key(FileName(path));
            if (file.Contains("bark")) return StyleSurface.Bark;
            if (file.Contains("plaster")) return StyleSurface.Plaster;
            if (file.Contains("roof") || file.Contains("tile")) return StyleSurface.Roof;
            if (file.Contains("plank") || file.Contains("wood") || file.Contains("timber")) return StyleSurface.Wood;
            if (file.Contains("cobble") || file.Contains("paving") || file.Contains("pavement")) return StyleSurface.Cobble;
            if (file.Contains("stone") || file.Contains("brick") || file.Contains("wall") || file.Contains("rock")) return StyleSurface.Stone;
            return null;
        }

        /// <summary>Texture de couleur (ou de relief) d'une matière de bâtiment, ou null ; ordre ordinal des chemins.</summary>
        public static string? SurfaceTexture(IEnumerable<string> paths, StyleSurface surface, bool normal) =>
            (paths ?? Array.Empty<string>()).Where(p => SurfaceOf(p) == surface && (normal ? IsNormalMap(p) : IsColorMap(p)))
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();

        /// <summary>
        /// Matière d'un obstacle bâti : écorce pour les murs et les portes (palissade de rondins, choix de Damien du 27 octobre 2026),
        /// planches pour les piliers ; null pour les autres (le moulin a ses propres matières : crépi, tuiles, planches ; la rivière son eau).
        /// </summary>
        public static StyleSurface? SurfaceFor(string id)
        {
            if (FenceFor(id) == StyleFence.Palisade) return StyleSurface.Bark;
            if (Key(id).StartsWith("pilier", StringComparison.Ordinal)) return StyleSurface.Wood;
            return null;
        }

        /// <summary>
        /// Clôture qu'habille un obstacle en longueur : palissade de rondins pour les murs et les portes (« mur_nord », « porte_moulin ») ;
        /// essai de Damien du 27 octobre 2026 : « le mur à côté de l'archer ne me plaît pas, peut-être mettre quelque chose d'autre » ; haie
        /// d'arbustes pour les haies (« haie_ouest », jusque-là un bloc de pierre). Aucune pour les autres.
        /// </summary>
        public static StyleFence FenceFor(string id)
        {
            string key = Key(id);
            if (key.StartsWith("mur", StringComparison.Ordinal) || key.StartsWith("porte", StringComparison.Ordinal)
                || key.StartsWith("palissade", StringComparison.Ordinal)) return StyleFence.Palisade;
            if (key.StartsWith("haie", StringComparison.Ordinal)) return StyleFence.Hedge;
            return StyleFence.None;
        }

        /// <summary>Rayon (m) d'un rondin de palissade.</summary>
        public const double LogRadius = 0.16;

        /// <summary>
        /// Rondins d'une palissade sur l'emprise rectangulaire [minX, maxX] × [minZ, maxZ] : alignés le long du grand côté, jointifs, en deux
        /// rangs décalés si l'emprise est assez épaisse (sinon un seul, au milieu) ; hauteurs de 2,6 à 3,1 m, fixes d'après
        /// l'identifiant (les mêmes à chaque lancement). Le moteur garde son emprise : la palissade n'en sort pas.
        /// </summary>
        public static IReadOnlyList<StyleLog> Palisade(double minX, double minZ, double maxX, double maxZ, string id)
        {
            var logs = new List<StyleLog>();
            bool alongX = maxX - minX >= maxZ - minZ;
            double length = alongX ? maxX - minX : maxZ - minZ, thickness = alongX ? maxZ - minZ : maxX - minX;
            if (length < 2 * LogRadius || thickness <= 0) return logs;
            double step = 2 * LogRadius * 0.92;
            int count = (int)Math.Floor((length - 2 * LogRadius) / step) + 1;
            double start = (alongX ? minX : minZ) + (length - (count - 1) * step) / 2;
            double middle = alongX ? (minZ + maxZ) / 2 : (minX + maxX) / 2;
            bool two = thickness >= 4 * LogRadius;
            double offset = two ? Math.Min(0.14, thickness / 2 - LogRadius) : 0;
            for (int row = 0; row < (two ? 2 : 1); row++)
                for (int i = 0; i < count - (row == 1 ? 1 : 0); i++)
                {
                    double along = start + i * step + (row == 1 ? step / 2 : 0);
                    double across = middle + (row == 0 ? offset : -offset);
                    double height = 2.6 + 0.005 * Variant(id + "/" + row + "/" + i, 101);
                    logs.Add(alongX ? new StyleLog(along, across, LogRadius, height) : new StyleLog(across, along, LogRadius, height));
                }
            return logs;
        }

        /// <summary>
        /// Places régulières le long du grand côté de l'emprise [minX, maxX] × [minZ, maxZ], au milieu de sa largeur, tous les
        /// <paramref name="spacing"/> mètres environ (au moins une ; les bouts à une demi-place du bord) : les arbustes d'une haie.
        /// </summary>
        public static IReadOnlyList<(double X, double Z)> Row(double minX, double minZ, double maxX, double maxZ, double spacing)
        {
            bool alongX = maxX - minX >= maxZ - minZ;
            double length = alongX ? maxX - minX : maxZ - minZ;
            int count = Math.Max(1, (int)Math.Round(length / Math.Max(0.1, spacing)));
            double step = length / count;
            var spots = new List<(double X, double Z)>();
            for (int i = 0; i < count; i++)
            {
                double along = (alongX ? minX : minZ) + step * (i + 0.5);
                spots.Add(alongX ? (along, (minZ + maxZ) / 2) : ((minX + maxX) / 2, along));
            }
            return spots;
        }

        /// <summary>
        /// Distance (m) d'un point au bord d'un polygone convexe (sommets dans un sens ou dans l'autre) : positive dedans, négative dehors
        /// (la plus petite des distances signées aux côtés). <see cref="double.NegativeInfinity"/> pour un polygone de moins de 3 sommets.
        /// </summary>
        public static double InsideDistance(double x, double z, IReadOnlyList<(double X, double Z)> polygon)
        {
            if (polygon == null || polygon.Count < 3) return double.NegativeInfinity;
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Z - b.X * a.Z;
            }
            double sign = area >= 0 ? 1 : -1;
            double inside = double.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                double ex = b.X - a.X, ez = b.Z - a.Z, len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-9) continue;
                // Sens trigonométrique (aire positive) : l'intérieur est à gauche de chaque côté.
                double d = sign * (ex * (z - a.Z) - ez * (x - a.X)) / len;
                inside = Math.Min(inside, d);
            }
            return double.IsPositiveInfinity(inside) ? double.NegativeInfinity : inside;
        }

        /// <summary>
        /// Bord de la réunion de polygones convexes (les bras de la rivière et le passage sous le pont) : leurs côtés, privés des morceaux
        /// qui touchent ou traversent un autre polygone (revue Codex de la PR #58 : deux polygones voisins gardaient leur côté commun comme
        /// berge, et deux bandes de terre coupaient la rivière de part et d'autre du pont).
        /// </summary>
        public static IReadOnlyList<(double X0, double Z0, double X1, double Z1)> UnionBoundary(IReadOnlyList<IReadOnlyList<(double X, double Z)>> polygons)
        {
            var boundary = new List<(double X0, double Z0, double X1, double Z1)>();
            if (polygons == null) return boundary;
            for (int i = 0; i < polygons.Count; i++)
            {
                IReadOnlyList<(double X, double Z)> polygon = polygons[i];
                if (polygon == null || polygon.Count < 3) continue;
                double sign = Area(polygon) >= 0 ? 1 : -1;
                for (int e = 0; e < polygon.Count; e++)
                {
                    var a = polygon[e];
                    var b = polygon[(e + 1) % polygon.Count];
                    double ex = b.X - a.X, ez = b.Z - a.Z, len = Math.Sqrt(ex * ex + ez * ez);
                    if (len < 1e-9) continue;
                    // Morceaux du côté (paramètre t de 0 à 1) qui ne sont pas un bord de la réunion : juste au-delà du côté (0,1 mm vers
                    // l'extérieur), on est encore dans un autre polygone. Un côté commun à deux voisins disparaît ; deux côtés alignés du
                    // même côté (polygones qui se chevauchent) restent.
                    double ox = sign * ez / len * 1e-4, oz = -sign * ex / len * 1e-4;
                    var covered = new List<(double T0, double T1)>();
                    for (int j = 0; j < polygons.Count; j++)
                        if (j != i && Clip((a.X + ox, a.Z + oz), (b.X + ox, b.Z + oz), polygons[j]) is (double t0, double t1) && t1 - t0 > 1e-9)
                            covered.Add((t0, t1));
                    double start = 0;
                    foreach (var c in covered.OrderBy(c => c.T0))
                    {
                        if (c.T0 > start + 1e-9) boundary.Add(Lerp(a, b, start, c.T0));
                        start = Math.Max(start, c.T1);
                    }
                    if (start < 1 - 1e-9) boundary.Add(Lerp(a, b, start, 1));
                }
            }
            return boundary;
        }

        /// <summary>
        /// Distance (m) d'un point au bord de la réunion de polygones convexes (<see cref="UnionBoundary"/>, calculé une fois) : positive
        /// dedans, négative dehors.
        /// </summary>
        public static double UnionDistance(double x, double z, IReadOnlyList<IReadOnlyList<(double X, double Z)>> polygons,
            IReadOnlyList<(double X0, double Z0, double X1, double Z1)> boundary)
        {
            if (polygons == null || boundary == null || boundary.Count == 0) return double.NegativeInfinity;
            double nearest = double.PositiveInfinity;
            foreach (var s in boundary)
            {
                double ex = s.X1 - s.X0, ez = s.Z1 - s.Z0, len2 = ex * ex + ez * ez;
                double t = len2 < 1e-18 ? 0 : Math.Max(0, Math.Min(1, ((x - s.X0) * ex + (z - s.Z0) * ez) / len2));
                double dx = x - (s.X0 + t * ex), dz = z - (s.Z0 + t * ez);
                nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dz * dz));
            }
            bool inside = polygons.Any(p => p != null && InsideDistance(x, z, p) >= 0);
            return inside ? nearest : -nearest;
        }

        /// <summary>Morceau [t0, t1] du segment [a, b] dans le polygone convexe (bords compris, à 1 µm près), ou null.</summary>
        private static (double T0, double T1)? Clip((double X, double Z) a, (double X, double Z) b, IReadOnlyList<(double X, double Z)> polygon)
        {
            if (polygon == null || polygon.Count < 3) return null;
            double sign = Area(polygon) >= 0 ? 1 : -1, t0 = 0, t1 = 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                double ex = q.X - p.X, ez = q.Z - p.Z, len = Math.Sqrt(ex * ex + ez * ez);
                if (len < 1e-9) continue;
                // Distance signée (positive dedans) le long du segment : f(t) = f0 + t * df, gardée là où f(t) >= -1e-6.
                double f0 = sign * (ex * (a.Z - p.Z) - ez * (a.X - p.X)) / len;
                double f1 = sign * (ex * (b.Z - p.Z) - ez * (b.X - p.X)) / len;
                double df = f1 - f0;
                if (Math.Abs(df) < 1e-12)
                {
                    if (f0 < -1e-6) return null;
                    continue;
                }
                double t = (-1e-6 - f0) / df;
                if (df > 0) t0 = Math.Max(t0, t);
                else t1 = Math.Min(t1, t);
                if (t0 > t1) return null;
            }
            return (t0, t1);
        }

        /// <summary>Double de l'aire signée (positive dans le sens où l'intérieur est à gauche des côtés, comme <see cref="InsideDistance"/>).</summary>
        private static double Area(IReadOnlyList<(double X, double Z)> polygon)
        {
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                area += p.X * q.Z - q.X * p.Z;
            }
            return area;
        }

        private static (double X0, double Z0, double X1, double Z1) Lerp((double X, double Z) a, (double X, double Z) b, double t0, double t1) =>
            (a.X + (b.X - a.X) * t0, a.Z + (b.Z - a.Z) * t0, a.X + (b.X - a.X) * t1, a.Z + (b.Z - a.Z) * t1);

        /// <summary>
        /// Profondeur (m, positive) du lit de la rivière à <paramref name="inside"/> mètres en dedans de son bord : nulle au bord et dehors,
        /// puis la berge descend en douceur jusqu'à <see cref="RiverBed"/> sur <see cref="RiverBank"/> mètres.
        /// </summary>
        public static double ChannelDepth(double inside) =>
            inside <= 0 ? 0 : RiverBed * Smooth(Math.Min(1, inside / RiverBank));

        /// <summary>Demi-largeur (m) de la bande de terre mouillée autour du bord de la rivière.</summary>
        public const double RiverShore = 0.8;

        /// <summary>
        /// Couches du sol près de la rivière (<paramref name="inside"/> : distance en dedans de son bord, négative dehors) : la terre gagne
        /// sur la berge et le lit (pas d'herbe sous l'eau), pleine à <see cref="RiverShore"/> mètres en dedans, sans effet à
        /// <see cref="RiverShore"/> mètres dehors ; la somme reste 1.
        /// </summary>
        public static StyleMix RiverMix(StyleMix mix, double inside)
        {
            double wet = RiverWet(inside);
            return new StyleMix(mix.Soil * (1 - wet) + wet, mix.Rock * (1 - wet), mix.Grass * (1 - wet));
        }

        /// <summary>Part de la terre mouillée près de la rivière (0 à 1), celle que <see cref="RiverMix"/> verse dans la terre.</summary>
        public static double RiverWet(double inside) => Math.Max(0, Math.Min(1, (inside + RiverShore) / (2 * RiverShore)));

        /// <summary>
        /// Essai réaliste 8 (relecture indépendante : calcul sorti du terrain de Unity pour être testé) : parts des cinq couches du sol en un
        /// point. Le chemin (<paramref name="road"/>) et le champ (<paramref name="field"/>) prennent leur part du mélange
        /// <paramref name="mix"/>, le champ l'emporte sur le chemin, et la berge de la rivière (<paramref name="wet"/>) reprend la terre
        /// partout (pas de chemin ni de champ sous l'eau). Parts bornées à [0, 1] ; la somme fait 1 quand celle du mélange fait 1.
        /// </summary>
        public static StyleLayers GroundLayers(StyleMix mix, double road, double field, double wet)
        {
            double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
            field = Clamp(field);
            road = field > 0 ? 0 : Clamp(road);
            wet = Clamp(wet);
            double rest = 1 - road - field, dry = 1 - wet;
            return new StyleLayers(mix.Soil * rest * dry + wet, mix.Rock * rest * dry, mix.Grass * rest * dry, road * dry, field * dry);
        }

        /// <summary>Angle (degrés, 0 vers +z, 90 vers +x, comme Unity) pour regarder de (fromX, fromZ) vers (toX, toZ).</summary>
        public static double FaceYaw(double fromX, double fromZ, double toX, double toZ) =>
            Math.Atan2(toX - fromX, toZ - fromZ) * 180 / Math.PI;

        /// <summary>
        /// Angle <paramref name="current"/> tourné vers <paramref name="target"/> d'au plus <paramref name="maxStep"/> degrés, par le plus
        /// court chemin ; résultat dans ]-180, 180].
        /// </summary>
        public static double Turn(double current, double target, double maxStep)
        {
            double delta = Wrap(target - current);
            double step = Math.Max(-Math.Abs(maxStep), Math.Min(Math.Abs(maxStep), delta));
            return Wrap(current + step);
        }

        private static double Wrap(double angle)
        {
            double a = angle % 360;
            if (a <= -180) a += 360;
            if (a > 180) a -= 360;
            return a;
        }

        /// <summary>
        /// Pont fait d'un ponton modulaire (Poly Haven « modular_wooden_pier » : planches, pieux et tronçons) : la pièce montrée, le premier
        /// tronçon dans l'ordre ordinal (« section_01 », tablier posé sur ses pieux), sinon la première pièce ; -1 s'il n'y a rien.
        /// </summary>
        public static int BridgeSection(IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0) return -1;
            var sections = parts.Select((p, i) => (Name: p ?? string.Empty, Index: i)).Where(p => Key(p.Name).Contains("section"))
                .OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            return sections.Count > 0 ? sections[0].Index : 0;
        }

        /// <summary>
        /// Pièces d'un modèle livré en plusieurs exemplaires posés côte à côte (le moulin de Sketchfab : le moulin détaillé et sa version
        /// simplifiée, à une vingtaine de mètres) : la pièce la plus détaillée (<paramref name="detail"/> le plus grand, la première à égalité)
        /// et, de proche en proche, celles dont la boîte touche une pièce gardée (à <paramref name="margin"/> mètres près). Indices dans
        /// l'ordre ; vide sans pièce.
        /// </summary>
        public static IReadOnlyList<int> Cluster(IReadOnlyList<(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)> boxes,
            IReadOnlyList<int> detail, double margin = 0.05)
        {
            if (boxes == null || boxes.Count == 0) return Array.Empty<int>();
            int first = 0;
            for (int i = 1; i < boxes.Count; i++)
                if (detail != null && i < detail.Count && detail[i] > (first < detail.Count ? detail[first] : 0)) first = i;
            var kept = new HashSet<int> { first };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (kept.Contains(i)) continue;
                    var b = boxes[i];
                    if (!kept.Any(k => boxes[k].MinX - margin <= b.MaxX && b.MinX <= boxes[k].MaxX + margin && boxes[k].MinY - margin <= b.MaxY
                        && b.MinY <= boxes[k].MaxY + margin && boxes[k].MinZ - margin <= b.MaxZ && b.MinZ <= boxes[k].MaxZ + margin)) continue;
                    kept.Add(i);
                    grew = true;
                }
            }
            return kept.OrderBy(i => i).ToList();
        }

        /// <summary>Pièce qui donne la hauteur du tablier du ponton (« planks ») : le dessus des planches ; -1 s'il n'y en a pas.</summary>
        public static int BridgeDeck(IReadOnlyList<string> parts)
        {
            if (parts == null) return -1;
            for (int i = 0; i < parts.Count; i++)
                if (Key(parts[i]).Contains("plank")) return i;
            return -1;
        }

        /// <summary>Ce qu'habille un obstacle du décor : arbres, blocs de la carrière et pierres du Seuil. Null : il garde sa forme.</summary>
        public static StyleSlot? ForObstacle(string id)
        {
            string key = Key(id);
            if (key.StartsWith("arbre", StringComparison.Ordinal)) return StyleSlot.Tree;
            if (key.StartsWith("bloc", StringComparison.Ordinal) || key.StartsWith("pierreseuil", StringComparison.Ordinal)) return StyleSlot.Rock;
            return null;
        }

        /// <summary>
        /// Couche de sol qu'appelle un obstacle autour de lui : la roche pour les blocs, les pierres du Seuil et les piliers, la terre pour
        /// les arbres, le moulin et les clôtures (palissades, haies, portes : terre battue au pied) ; null pour les autres (rivière…).
        /// </summary>
        public static StyleGround? GroundFor(string id)
        {
            string key = Key(id);
            if (ForObstacle(id) == StyleSlot.Rock || key.StartsWith("pilier", StringComparison.Ordinal)) return StyleGround.Rock;
            if (key.StartsWith("arbre", StringComparison.Ordinal) || key.StartsWith("moulin", StringComparison.Ordinal) || FenceFor(id) != StyleFence.None)
                return StyleGround.Soil;
            return null;
        }

        private static string FileName(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(slash + 1) : p;
        }

        private static string Folder(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(0, slash) : string.Empty;
        }

        /// <summary>Dossiers du chemin, réduits (« Assets/Style/PolyHaven/Ground/x.jpg » → assets, style, polyhaven, ground).</summary>
        private static IEnumerable<string> Segments(string path)
        {
            string[] parts = (path ?? string.Empty).Replace('\\', '/').Split('/');
            return parts.Take(Math.Max(0, parts.Length - 1)).Select(Key);
        }

        /// <summary>Mots du nom de fichier, coupés aux signes (« forest_ground_04_nor_gl_1k.jpg » → forest, ground, 04, nor, gl, 1k, jpg).</summary>
        private static IEnumerable<string> Words(string path) =>
            FileName(path).ToLowerInvariant().Split(new[] { '_', '-', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
