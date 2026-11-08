using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.9.3 : gravité d'une ligne du rapport de vérification de l'essai.</summary>
    public enum EssaiLevel
    {
        /// <summary>Prêt (✅).</summary>
        Ok = 0,
        /// <summary>Le jeu tourne, avec un repli (⚠️) : formes simples, son fait par le code, fichier mal importé…</summary>
        Warning = 1,
        /// <summary>Bloquant (❌) : l'essai ne marchera pas comme prévu tant que ce n'est pas réglé.</summary>
        Blocking = 2,
    }

    /// <summary>Sous-lot V2.9.3 : modèle attendu pour une bête ou le héros, ses fichiers du meilleur au dernier repli.</summary>
    public sealed class EssaiModel
    {
        public EssaiModel(string label, StyleSlot slot, string without, bool packFirst, params (string File, bool Animated)[] files)
        {
            if (files is null || files.Length == 0) throw new ArgumentException("Au moins un fichier attendu.", nameof(files));
            Label = label;
            Slot = slot;
            Without = without;
            PackFirst = packFirst;
            Files = files.Select(f => f.File).ToList();
            Animated = files.Select(f => f.Animated).ToList();
        }

        /// <summary>Nom affiché (« Cerf à bois »).</summary>
        public string Label { get; }

        /// <summary>Case du catalogue, cherchée par <see cref="StyleRules.Candidates"/> comme le fait la préparation.</summary>
        public StyleSlot Slot { get; }

        /// <summary>Ce que le jeu montre sans aucun fichier (« formes simples »).</summary>
        public string Without { get; }

        /// <summary>Un prefab de pack de l'Asset Store (<see cref="StyleRules.PackAnimal"/>) passe devant les fichiers (loup, sanglier).</summary>
        public bool PackFirst { get; }

        /// <summary>Chemins attendus dans <c>Assets/Style</c>, le meilleur d'abord (l'ordre où la préparation les choisit).</summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>Le fichier de même rang porte-t-il des animations ? (le sanglier de Hanabi_Map n'en a aucune : le code l'anime).</summary>
        public IReadOnlyList<bool> Animated { get; }
    }

    /// <summary>Sous-lot V2.9.3 : son, musique ou ambiance attendu : où le déposer, ce que fait le jeu sans lui.</summary>
    public sealed class EssaiAudio
    {
        public EssaiAudio(string label, string folder, string without, StyleSound? sound = null, StyleMusic? music = null, StyleAmbience? ambience = null)
        {
            Label = label;
            Folder = folder;
            Without = without;
            Sound = sound;
            Music = music;
            Ambience = ambience;
        }

        public string Label { get; }
        public string Folder { get; }
        public string Without { get; }
        public StyleSound? Sound { get; }
        public StyleMusic? Music { get; }
        public StyleAmbience? Ambience { get; }

        /// <summary>Mots que le nom du fichier doit contenir (le premier est le meilleur), lus dans <see cref="StyleRules"/> : jamais recopiés.</summary>
        public IReadOnlyList<string> Words =>
            Sound is StyleSound s ? StyleRules.SoundWords(s)
            : Music is StyleMusic m ? StyleRules.MusicWordsOf(m)
            : Ambience is StyleAmbience a ? StyleRules.AmbienceWordsOf(a)
            : Array.Empty<string>();
    }

    /// <summary>Sous-lot V2.9.3 : où en est un modèle attendu.</summary>
    public enum ModelState { Best, Fallback, Missing }

    /// <summary>Sous-lot V2.9.3 : modèle que la préparation prendra, et son rang dans la liste attendue (-1 : un autre fichier reconnu).</summary>
    public readonly struct ModelPick
    {
        public ModelPick(ModelState state, string? path, int rank)
        {
            State = state;
            Path = path;
            Rank = rank;
        }

        public ModelState State { get; }
        public string? Path { get; }
        public int Rank { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.3 (« Damien doit savoir en un clic si tout est prêt ») : la liste des ressources attendues par l'essai, partagée par la
    /// préparation (<c>EdsStyle</c>, <c>EdsHumans</c>) et le vérificateur (<c>EdsVerification</c>). Les fichiers sont reconnus par les mêmes
    /// règles que la préparation (<see cref="StyleRules"/>, <see cref="HumanLooks"/>) ; les tests vérifient que cette liste couvre tout ce que
    /// le code cherche, dans le même ordre.
    /// </summary>
    public static class EssaiManifest
    {
        public const string Root = "Assets/Style";

        /// <summary>Rapport écrit par le vérificateur (non suivi par git).</summary>
        public const string ReportPath = "Assets/EdS/VERIFICATION_ESSAI.md";

        /// <summary>Shaders du jeu (<c>unity/Assets/EdS/Game/*.shader</c>) cherchés par leur nom.</summary>
        public const string EchoShader = "EdS/Echo";
        public const string RiftShader = "EdS/Faille";
        public static readonly IReadOnlyList<string> GameShaders = new[] { EchoShader, RiftShader, "EdS/Eau", "EdS/Herbe", "EdS/Feuillage", "EdS/Particule", "EdS/Annonce" };

        private const string A1 = Root + "/Sketchfab/Animaux/";
        private const string A2 = Root + "/Sketchfab/Animaux2/";
        private const string A3 = Root + "/Sketchfab/Animaux3/";
        private const string Uaa = Root + "/Animals/Quaternius/";
        private const string Farm = Root + "/Quaternius/FarmAnimals/";

        /// <summary>Le meilleur modèle par bête, puis ses replis (docs/UNITY.md, V2.9 à V2.9.2).</summary>
        public static readonly IReadOnlyList<EssaiModel> Models = new[]
        {
            new EssaiModel("Héros (paladin)", StyleSlot.Hero, "chevalier en formes simples", false, (Root + "/Mixamo/Paladin WProp J Nordstrom.fbx", true)),
            new EssaiModel("Loup", StyleSlot.Wolf, "loup en formes simples", true, (A2 + "Loup_roo3d.fbx", true), (Uaa + "Wolf_Quaternius.fbx", true)),
            new EssaiModel("Sanglier", StyleSlot.Boar, "sanglier en formes simples", true, (A2 + "Sanglier_Dakta.fbx", true), (A1 + "Boar_Hanabi.fbx", false)),
            new EssaiModel("Cerf à bois", StyleSlot.Stag, "cerf en formes simples", false, (A2 + "Elan_WildMesh.fbx", true), (A2 + "CerfRouge_Suiteofart.fbx", true),
                (A3 + "CerfCel_WildMesh.fbx", true), (Uaa + "Stag_Quaternius.fbx", true)),
            new EssaiModel("Biche", StyleSlot.Doe, "biche en formes simples", false, (A3 + "Biche_WildMesh.fbx", true), (A2 + "Faon_AssetsAnimated.fbx", true),
                (Uaa + "Deer_Quaternius.fbx", true)),
            new EssaiModel("Lièvre", StyleSlot.Hare, "lièvre en formes simples", false, (A2 + "Lievre_bluekiwi.fbx", true), (A1 + "Rabbit_Pneshik.fbx", true)),
            new EssaiModel("Pain-d'Épice (chat)", StyleSlot.Cat, "chat en formes simples", false, (A1 + "Cat_Stripe_DreamNoms.fbx", true)),
            new EssaiModel("Corbeau", StyleSlot.Crow, "pas de corbeau dans les champs", false, (A2 + "Corbeau_alexanders823.fbx", true)),
            new EssaiModel("Vache", StyleSlot.Cow, "pas de vache dans les champs", false, (A3 + "Vache_nataliekirk.fbx", true), (A2 + "Vache_rukh3d.fbx", true),
                (Uaa + "Cow.fbx", true), (Farm + "Cow.fbx", true), (Uaa + "Bull.fbx", true)),
            new EssaiModel("Chien", StyleSlot.Dog, "pas de chien au hameau", false, (A3 + "Chien_SharPei_artsiom.fbx", true), (A2 + "Chien_quander.fbx", true),
                (Uaa + "Husky.fbx", true), (Uaa + "ShibaInu.fbx", true), (Farm + "Pug.fbx", true)),
            new EssaiModel("Cheval", StyleSlot.Horse, "pas de cheval au hameau", false, (A2 + "Cheval_henry7.fbx", true), (Uaa + "Horse.fbx", true), (Farm + "Horse.fbx", true)),
            new EssaiModel("Âne", StyleSlot.Donkey, "pas d'âne dans la basse-cour", false, (A3 + "Ane_WildMesh.fbx", true), (Uaa + "Donkey.fbx", true)),
            new EssaiModel("Poule", StyleSlot.Hen, "pas de poule dans la basse-cour", false, (A3 + "Poule_kill6lucius.fbx", true)),
            new EssaiModel("Coq", StyleSlot.Rooster, "pas de coq dans la basse-cour", false, (A3 + "Coq_rukh3d.fbx", true)),
            new EssaiModel("Ours", StyleSlot.Bear, "ours en formes simples", false, (A3 + "Ours_WildMesh.fbx", true)),
        };

        /// <summary>Voix des bêtes, cloches et failles (fenêtre de la préparation : « … sur 11 »).</summary>
        public static readonly IReadOnlyList<StyleSound> Voices = new[]
        {
            StyleSound.Meow, StyleSound.Purr, StyleSound.Howl, StyleSound.Bellow, StyleSound.HareDash, StyleSound.BoarGrumble, StyleSound.Tocsin,
            StyleSound.ChurchBell, StyleSound.Glass, StyleSound.Shatter, StyleSound.RiftHum,
        };

        /// <summary>Portail, pas, cris de la ferme, sabots et souffle des failles (« … sur 15 »).</summary>
        public static readonly IReadOnlyList<StyleSound> WorldSounds = new[]
        {
            StyleSound.Portal, StyleSound.StepGrass, StyleSound.StepGravel, StyleSound.StepLeaves, StyleSound.StepWood, StyleSound.Caw, StyleSound.Moo,
            StyleSound.Bark, StyleSound.Neigh, StyleSound.Rooster, StyleSound.Hens, StyleSound.Sheep, StyleSound.Owl, StyleSound.Hooves, StyleSound.RiftSurge,
        };

        /// <summary>Bruitages du combat et de l'interface (« Sons : … sur 7 » pour les sept premiers).</summary>
        public static readonly IReadOnlyList<StyleSound> CombatSounds = new[]
        {
            StyleSound.Hit, StyleSound.Block, StyleSound.Parry, StyleSound.Death, StyleSound.Potion, StyleSound.Gather, StyleSound.Craft,
            StyleSound.Swing, StyleSound.Cast, StyleSound.Growl, StyleSound.Grunt, StyleSound.Roar, StyleSound.Coin, StyleSound.Bray,
        };

        private static readonly Dictionary<StyleSound, string> SoundLabels = new Dictionary<StyleSound, string>
        {
            [StyleSound.Hit] = "coup porté", [StyleSound.Block] = "coup bloqué", [StyleSound.Parry] = "parade", [StyleSound.Death] = "chute d'un vaincu",
            [StyleSound.Potion] = "potion bue", [StyleSound.Gather] = "récolte", [StyleSound.Craft] = "fabrication (enclume)", [StyleSound.Swing] = "coup dans le vide",
            [StyleSound.Cast] = "sort lancé", [StyleSound.Growl] = "grondement du loup", [StyleSound.Grunt] = "grognement de combat", [StyleSound.Roar] = "rugissement",
            [StyleSound.Coin] = "pièces", [StyleSound.Meow] = "miaulements de Pain-d'Épice", [StyleSound.Purr] = "ronron", [StyleSound.Howl] = "hurlements des loups",
            [StyleSound.Bellow] = "brame du cerf", [StyleSound.HareDash] = "lièvre qui détale", [StyleSound.BoarGrumble] = "sanglier qui grogne",
            [StyleSound.Tocsin] = "tocsin", [StyleSound.ChurchBell] = "cloche d'église", [StyleSound.Glass] = "verre qui éclate (faille)",
            [StyleSound.Shatter] = "miroir qui explose (faille)", [StyleSound.RiftHum] = "bourdonnement de la faille", [StyleSound.Portal] = "portail des failles",
            [StyleSound.StepGrass] = "pas dans l'herbe", [StyleSound.StepGravel] = "pas sur le gravier", [StyleSound.StepLeaves] = "pas dans les feuilles",
            [StyleSound.StepWood] = "pas sur le plancher", [StyleSound.Caw] = "corbeaux", [StyleSound.Moo] = "meuglements", [StyleSound.Bark] = "aboiements",
            [StyleSound.Neigh] = "hennissements", [StyleSound.Rooster] = "coq", [StyleSound.Hens] = "poules", [StyleSound.Sheep] = "mouton", [StyleSound.Owl] = "hulotte",
            [StyleSound.Hooves] = "sabots sur les pavés", [StyleSound.RiftSurge] = "souffle magique des failles", [StyleSound.Bray] = "braiment de l'âne",
        };

        /// <summary>Tous les bruitages attendus (une entrée par valeur de <see cref="StyleSound"/>).</summary>
        public static readonly IReadOnlyList<EssaiAudio> Sounds =
            Voices.Select(s => new EssaiAudio(SoundLabels[s], Root + "/Sounds/Animaux", s == StyleSound.Meow || s == StyleSound.Purr ? "sons faits par le code" : "silence", sound: s))
                .Concat(WorldSounds.Select(s => new EssaiAudio(SoundLabels[s], Root + "/Sounds/Monde", "silence", sound: s)))
                .Concat(CombatSounds.Select(s => new EssaiAudio(SoundLabels[s], Root + "/Sounds (un sous-dossier, pas Music ni Ambience)",
                    s == StyleSound.Bray ? "le hennissement, plus grave" : "son fait par le code ou silence", sound: s)))
                .ToList();

        /// <summary>Musiques attendues (une par valeur de <see cref="StyleMusic"/>).</summary>
        public static readonly IReadOnlyList<EssaiAudio> Music = new[]
        {
            new EssaiAudio("exploration (Into The Wilds)", Root + "/Music/ScottBuckley", "silence", music: StyleMusic.Exploration),
            new EssaiAudio("village", Root + "/Music", "musique d'exploration", music: StyleMusic.Village),
            new EssaiAudio("combat", Root + "/Music", "musique d'exploration", music: StyleMusic.Combat),
            new EssaiAudio("chefs et attaques (Determined Pursuit)", Root + "/Music/OGA", "musique du combat", music: StyleMusic.Boss),
            new EssaiAudio("le Seuil (Sanctum)", Root + "/Music/ScottBuckley", "musique d'exploration", music: StyleMusic.Threshold),
            new EssaiAudio("scènes de l'histoire (The Legend of Kvothe)", Root + "/Music/ScottBuckley", "musique du lieu", music: StyleMusic.Story),
            new EssaiAudio("bourg la nuit (Night Town)", Root + "/Music/OGA", "musique du village", music: StyleMusic.VillageNight),
            new EssaiAudio("auberge (The Old Tower Inn)", Root + "/Music/OGA", "musique du village", music: StyleMusic.Inn),
        };

        /// <summary>Ambiances attendues (une par valeur de <see cref="StyleAmbience"/>).</summary>
        public static readonly IReadOnlyList<EssaiAudio> Ambiences = new[]
        {
            new EssaiAudio("oiseaux", Root + "/Ambience", "silence", ambience: StyleAmbience.Birds),
            new EssaiAudio("vent dans la forêt", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Wind),
            new EssaiAudio("rivière et ruisseau", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.River),
            new EssaiAudio("nuit (grillons, hibou)", Root + "/Ambience", "silence", ambience: StyleAmbience.Night),
            new EssaiAudio("pluie", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Rain),
            new EssaiAudio("tonnerre", Root + "/Ambience", "silence", ambience: StyleAmbience.Thunder),
            new EssaiAudio("rumeur du village", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Village),
            new EssaiAudio("marché", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Market),
            new EssaiAudio("taverne", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Tavern),
            new EssaiAudio("forge", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Forge),
            new EssaiAudio("feu de camp", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Campfire),
            new EssaiAudio("campagne", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Countryside),
            new EssaiAudio("forêt", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Forest),
            new EssaiAudio("salle de l'auberge à table", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Dining),
            new EssaiAudio("bord du lac", Root + "/Sounds/Monde", "silence", ambience: StyleAmbience.Lake),
        };

        // --- Corps humains (4e vague, V2.9.2 : Universal Base Characters, Universal Animation Library) ---------------------------------

        public const string HumanFolder = Root + "/Quaternius/BaseCharacters";
        public const string MaleBody = HumanFolder + "/Superhero_Male_FullBody.fbx";
        public const string FemaleBody = HumanFolder + "/Superhero_Female_FullBody.fbx";
        public static readonly IReadOnlyList<string> AnimationLibraries = new[] { Root + "/Quaternius/UAL1/UAL1_Standard.fbx", Root + "/Quaternius/UAL2/UAL2_Standard.fbx" };

        /// <summary>Peaux : homme clair, homme sombre, femme claire, femme sombre (<see cref="HumanLooks.SkinTexture"/>).</summary>
        public static readonly IReadOnlyList<string> SkinTextures = new[]
        {
            HumanFolder + "/Textures/T_Superhero_Male_Ligh.png", HumanFolder + "/Textures/T_Superhero_Male_Dark.png",
            HumanFolder + "/Textures/T_Superhero_Female_Light_BaseColor.png", HumanFolder + "/Textures/T_Superhero_Female_Dark_BaseColor.png",
        };

        public const string HairFolder = HumanFolder + "/Hairstyles/Rigged to Head Bone/FBX (Unity)";

        /// <summary>Coiffures dans l'ordre de <see cref="HumanHair"/> (celles que prend la préparation), puis la barbe.</summary>
        public static readonly IReadOnlyList<string> Hairs = new[]
        {
            HairFolder + "/Hair_Buzzed.fbx", HairFolder + "/Hair_SimpleParted.fbx", HairFolder + "/Hair_Long.fbx", HairFolder + "/Hair_Buns.fbx",
            HairFolder + "/Hair_BuzzedFemale.fbx", HairFolder + "/Hair_Beard.fbx",
        };

        // --- Vrais vêtements (V2.9.4 : Modular Character Outfits - Fantasy de Quaternius, pièces reconnues par HumanWardrobe) -----------------

        /// <summary>Dossier du pack de vêtements (y copier <c>Exports/FBX (Unity)</c> et <c>Textures</c> du zip).</summary>
        public const string OutfitFolder = Root + "/Quaternius/Outfits";
        private const string OutfitParts = OutfitFolder + "/Exports/FBX (Unity)/Modular Parts/";

        /// <summary>Places qu'une tenue doit couvrir pour que les vrais vêtements soient pris (sinon les tenues peintes).</summary>
        public static readonly IReadOnlyList<WardrobeSlot> OutfitBasicSlots = new[] { WardrobeSlot.Body, WardrobeSlot.Arms, WardrobeSlot.Legs, WardrobeSlot.Feet };

        /// <summary>Les pièces trouvées (<paramref name="slots"/>) couvrent-elles chaque place de <see cref="OutfitBasicSlots"/> ?</summary>
        public static bool OutfitReady(IEnumerable<WardrobeSlot> slots)
        {
            var have = new HashSet<WardrobeSlot>(slots ?? Array.Empty<WardrobeSlot>());
            return OutfitBasicSlots.All(have.Contains);
        }

        /// <summary>Familles du pack gratuit et une pièce attendue par place (homme), pour dire quoi déposer.</summary>
        public static readonly IReadOnlyList<(WardrobeFamily Family, IReadOnlyList<string> Parts)> OutfitFamilies = new[]
        {
            (WardrobeFamily.Peasant, (IReadOnlyList<string>)new[] { OutfitParts + "Male_Peasant_Body.fbx", OutfitParts + "Male_Peasant_Arms.fbx",
                OutfitParts + "Male_Peasant_Legs.fbx", OutfitParts + "Male_Peasant_Feet.fbx" }),
            (WardrobeFamily.Ranger, (IReadOnlyList<string>)new[] { OutfitParts + "Male_Ranger_Body.fbx", OutfitParts + "Male_Ranger_Arms.fbx",
                OutfitParts + "Male_Ranger_Legs.fbx", OutfitParts + "Male_Ranger_Feet_Boots.fbx" }),
        };

        /// <summary>Étoffes (cartes de couleur) attendues, une par famille.</summary>
        public static readonly IReadOnlyList<string> OutfitTextures = new[]
        {
            OutfitFolder + "/Textures/Peasant/T_Peasant_BaseColor.png", OutfitFolder + "/Textures/Ranger/T_Ranger_BaseColor.png",
        };

        /// <summary>Peau des mains (sombre ; la claire en est tirée) : homme, femme (<see cref="HumanWardrobe.SkinTexture"/>).</summary>
        public static readonly IReadOnlyList<string> OutfitSkins = new[]
        {
            OutfitFolder + "/Textures/Base/T_Regular_Male_Dark_BaseColor.png", OutfitFolder + "/Textures/Base/T_Regular_Female_Dark_BaseColor.png",
        };

        // --- Textures (essais réalistes) : dossiers conseillés ; les fichiers sont reconnus par StyleRules (Sky, GroundTexture, SurfaceTexture). ---

        public const string SkyFolder = Root + "/PolyHaven/Sky";
        public const string GroundFolder = Root + "/PolyHaven/Ground";
        public const string RoadFolder = Root + "/AmbientCG/Ground";
        public const string SurfaceFolder = Root + "/PolyHaven/Surfaces";

        /// <summary>Animations sans lesquelles les corps ne sont pas prêts (<c>HumanCatalog.Ready</c>).</summary>
        public static readonly IReadOnlyList<string> RequiredHumanClips = new[] { "Idle", "Walk" };

        /// <summary>
        /// Revue Codex de la PR #118 (tour 2) : animations requises absentes de <paramref name="clipNames"/> (celles de toutes les bibliothèques
        /// trouvées). Toujours vérifié, même sans aucune animation importée : aucune animation = corps non prêts.
        /// </summary>
        public static IReadOnlyList<string> MissingHumanClips(IEnumerable<string> clipNames)
        {
            var have = new HashSet<string>(clipNames ?? Array.Empty<string>(), StringComparer.Ordinal);
            return RequiredHumanClips.Where(c => !have.Contains(c)).ToList();
        }

        /// <summary>
        /// Revue Codex de la PR #118 (tour 2) : fichiers où la préparation cherche les animations d'un modèle choisi. Pour le héros, le modèle
        /// et ses fichiers compagnons (Mixamo livre chaque animation à part, <see cref="StyleRules.Companions"/>, comme <c>EdsStyle.Prepare</c>
        /// pour <c>catalog.HeroClips</c>) ; pour une bête, le modèle seul.
        /// </summary>
        public static IReadOnlyList<string> ClipSources(StyleSlot slot, string model, IEnumerable<string> models) =>
            slot == StyleSlot.Hero ? new[] { model }.Concat(StyleRules.Companions(model, models)).ToList() : (IReadOnlyList<string>)new[] { model };

        /// <summary>Nom de fichier d'un chemin (« Assets/Style/x/Loup_roo3d.fbx » → « Loup_roo3d.fbx »).</summary>
        public static string FileName(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(slash + 1) : p;
        }

        /// <summary>Dossier d'un chemin, sans la barre finale.</summary>
        public static string Folder(string path)
        {
            string p = (path ?? string.Empty).Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash >= 0 ? p.Substring(0, slash) : p;
        }

        /// <summary>
        /// Modèle que la préparation prendra pour <paramref name="model"/> parmi <paramref name="models"/> (même règle :
        /// <see cref="StyleRules.Candidates"/>, premier), et où il se place dans la liste attendue. Un fichier déplacé dans un autre dossier
        /// compte : seul son nom est comparé. <paramref name="pack"/> : prefab de l'Asset Store trouvé (loup, sanglier), qui passe devant.
        /// </summary>
        public static ModelPick Pick(EssaiModel model, IEnumerable<string> models, string? pack = null)
        {
            if (model is null) throw new ArgumentNullException(nameof(model));
            if (model.PackFirst && pack != null) return new ModelPick(ModelState.Best, pack, 0);
            string? picked = StyleRules.Candidates(models ?? Array.Empty<string>(), model.Slot).FirstOrDefault();
            if (picked is null) return new ModelPick(ModelState.Missing, null, -1);
            string name = FileName(picked);
            int rank = -1;
            for (int i = 0; i < model.Files.Count; i++)
                if (string.Equals(FileName(model.Files[i]), name, StringComparison.OrdinalIgnoreCase))
                {
                    rank = i;
                    break;
                }
            return new ModelPick(rank == 0 ? ModelState.Best : ModelState.Fallback, picked, rank);
        }

        /// <summary>Ligne du rapport pour un modèle (gravité, constat, quoi faire), avant les contrôles d'import faits dans Unity.</summary>
        public static (EssaiLevel Level, string Detail, string Fix) Describe(EssaiModel model, ModelPick pick)
        {
            string best = FileName(model.Files[0]), where = Folder(model.Files[0]);
            switch (pick.State)
            {
                case ModelState.Best:
                    return (EssaiLevel.Ok, "meilleur modèle : " + FileName(pick.Path ?? model.Files[0]), "");
                case ModelState.Fallback:
                    return (EssaiLevel.Warning, "repli utilisé : " + FileName(pick.Path ?? "") + (pick.Rank > 0 ? " (choix n° " + (pick.Rank + 1) + ")" : " (autre fichier reconnu)"),
                        "Pour le meilleur, dépose " + best + " dans " + where + ".");
                default:
                    return (EssaiLevel.Warning, "absent : " + model.Without, "Dépose " + best + " dans " + where + ".");
            }
        }

        /// <summary>Ligne du rapport pour un son, une musique ou une ambiance (<paramref name="found"/> : fichier choisi, ou null).</summary>
        public static (EssaiLevel Level, string Detail, string Fix) Describe(EssaiAudio audio, string? found)
        {
            if (found != null) return (EssaiLevel.Ok, FileName(found), "");
            string word = audio.Words.Count > 0 ? audio.Words[0] : "?";
            return (EssaiLevel.Warning, "absent : " + audio.Without, "Dépose dans " + audio.Folder + " un fichier dont le nom contient « " + word + " ».");
        }

        /// <summary>
        /// Taille aberrante d'un modèle importé (hauteur en mètres dans Unity, avant la mise à l'échelle du jeu) : null si elle est plausible.
        /// Le jeu remet chaque bête à sa taille ; seule une échelle d'import cassée (centimètres pris pour des mètres, maillage vide) gêne.
        /// </summary>
        public static string? ScaleProblem(double height)
        {
            if (double.IsNaN(height) || height <= 0.005) return "taille nulle (maillage vide ou mal importé)";
            if (height > 50) return "taille aberrante (" + height.ToString("0", CultureInfo.InvariantCulture) + " m : échelle d'import à vérifier)";
            return null;
        }

        /// <summary>
        /// Version de Unity : null si celle qui tourne (<paramref name="running"/>, « 6000.3.26f1 ») est de la même série que celle du projet
        /// (<paramref name="expected"/>, lue dans <c>ProjectSettings/ProjectVersion.txt</c>) ; sinon le constat.
        /// </summary>
        public static string? VersionProblem(string running, string? expected)
        {
            if (string.IsNullOrEmpty(expected)) return null;
            if (string.Equals(running, expected, StringComparison.Ordinal)) return null;
            string Series(string v)
            {
                string[] parts = (v ?? string.Empty).Split('.');
                return parts.Length >= 2 ? parts[0] + "." + parts[1] : v ?? string.Empty;
            }
            return Series(running) == Series(expected!) ? null : "le projet est fait pour Unity " + expected + ", tu as " + running;
        }

        /// <summary>Version attendue lue dans le texte de <c>ProjectVersion.txt</c> (« m_EditorVersion: 6000.3.26f1 ») ; null sinon.</summary>
        public static string? ExpectedVersion(string? projectVersionText)
        {
            foreach (string line in (projectVersionText ?? string.Empty).Split('\n'))
            {
                string l = line.Trim();
                if (l.StartsWith("m_EditorVersion:", StringComparison.Ordinal)) return l.Substring("m_EditorVersion:".Length).Trim();
            }
            return null;
        }
    }

    /// <summary>Sous-lot V2.9.3 : une ligne du rapport.</summary>
    public sealed class EssaiLine
    {
        public EssaiLine(string section, EssaiLevel level, string label, string detail, string fix)
        {
            Section = section;
            Level = level;
            Label = label;
            Detail = detail ?? string.Empty;
            Fix = fix ?? string.Empty;
        }

        public string Section { get; }
        public EssaiLevel Level { get; }
        public string Label { get; }
        public string Detail { get; }
        public string Fix { get; }
    }

    /// <summary>
    /// Sous-lot V2.9.3 : rapport de vérification de l'essai, rangé par section, avec son résumé : ✅ prêt, ⚠️ prêt avec replis, ❌ bloquant
    /// (une phrase simple par problème).
    /// </summary>
    public sealed class EssaiReport
    {
        private readonly List<EssaiLine> _lines = new List<EssaiLine>();

        public IReadOnlyList<EssaiLine> Lines => _lines;

        public void Add(string section, EssaiLevel level, string label, string detail, string fix = "") =>
            _lines.Add(new EssaiLine(section, level, label, detail, fix));

        /// <summary>La pire gravité du rapport (prêt s'il est vide).</summary>
        public EssaiLevel Overall => _lines.Count == 0 ? EssaiLevel.Ok : _lines.Max(l => l.Level);

        public int Count(EssaiLevel level) => _lines.Count(l => l.Level == level);

        public static string Mark(EssaiLevel level) => level == EssaiLevel.Ok ? "✅" : level == EssaiLevel.Warning ? "⚠️" : "❌";

        /// <summary>
        /// Revue Codex de la PR #118 (tour 3) : une seule ligne, de taille fixe, pour le haut de la fenêtre : l'état et le compte par statut ;
        /// la liste des problèmes (<see cref="Summary"/>) est dans la zone qui défile.
        /// </summary>
        public string Headline()
        {
            string state = Overall == EssaiLevel.Blocking ? "❌ Bloquant" : Overall == EssaiLevel.Warning ? "⚠️ Prêt avec replis" : "✅ Prêt";
            return state + " : " + Count(EssaiLevel.Blocking) + " bloquant" + (Count(EssaiLevel.Blocking) > 1 ? "s" : "") + ", " + Count(EssaiLevel.Warning)
                + " repli" + (Count(EssaiLevel.Warning) > 1 ? "s" : "") + ", " + Count(EssaiLevel.Ok) + " OK." + (Overall == EssaiLevel.Ok ? "" : " Détail plus bas.");
        }

        /// <summary>Résumé en quelques lignes (fenêtre, Console, haut du fichier).</summary>
        public string Summary()
        {
            var text = new StringBuilder();
            List<EssaiLine> blocking = _lines.Where(l => l.Level == EssaiLevel.Blocking).ToList();
            List<EssaiLine> warnings = _lines.Where(l => l.Level == EssaiLevel.Warning).ToList();
            if (blocking.Count > 0)
            {
                text.Append("❌ Bloquant : ").Append(blocking.Count).Append(blocking.Count > 1 ? " problèmes à régler" : " problème à régler").Append(" avant l'essai.\n");
                foreach (EssaiLine l in blocking) text.Append("- ").Append(l.Label).Append(" : ").Append(l.Fix.Length > 0 ? l.Fix : l.Detail).Append('\n');
                if (warnings.Count > 0) text.Append("Et ").Append(warnings.Count).Append(" replis (voir plus bas).\n");
            }
            else if (warnings.Count > 0)
            {
                text.Append("⚠️ Prêt avec replis : l'essai se lance ; ").Append(warnings.Count).Append(warnings.Count > 1 ? " éléments prennent" : " élément prend").Append(" un repli.\n");
                foreach (EssaiLine l in warnings) text.Append("- ").Append(l.Label).Append(" : ").Append(l.Detail).Append('\n');
            }
            else text.Append("✅ Prêt : tout est en place, lance l'essai.\n");
            return text.ToString();
        }

        /// <summary>Le rapport complet en Markdown (fichier <see cref="EssaiManifest.ReportPath"/>).</summary>
        public string ToMarkdown(DateTime when, string unityVersion)
        {
            var md = new StringBuilder();
            md.Append("# Vérification de l'essai — Éclats du Seuil\n\n");
            md.Append("Faite le ").Append(when.ToString("dd/MM/yyyy 'à' HH:mm", CultureInfo.InvariantCulture)).Append(", Unity ").Append(unityVersion)
                .Append(". Relancer : menu **Éclats du Seuil → Vérifier l'essai**.\n\n");
            md.Append("## Résumé\n\n").Append(Summary()).Append('\n');
            foreach (IGrouping<string, EssaiLine> section in _lines.GroupBy(l => l.Section))
            {
                md.Append("## ").Append(section.Key).Append("\n\n| | Élément | État | Quoi faire |\n|---|---|---|---|\n");
                foreach (EssaiLine l in section)
                    md.Append("| ").Append(Mark(l.Level)).Append(" | ").Append(Cell(l.Label)).Append(" | ").Append(Cell(l.Detail)).Append(" | ").Append(Cell(l.Fix)).Append(" |\n");
                md.Append('\n');
            }
            return md.ToString();
        }

        private static string Cell(string s) => (s ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
