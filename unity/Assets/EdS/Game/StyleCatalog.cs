using System;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>Essai réaliste : un personnage de Mixamo (son dossier), avec ses animations.</summary>
    [Serializable]
    public sealed class CharacterLook
    {
        /// <summary>Nom du dossier, réduit (« archer », « odile ») : <see cref="StyleRules.CharacterOf"/> d'un PNJ ou d'un ennemi.</summary>
        public string Id;
        public GameObject Model;
        public AnimationClip[] Clips = new AnimationClip[0];
        [Tooltip("Hauteur du personnage (m).")]
        public float Height = 1.8f;
        [Tooltip("Rotation ajoutée (degrés) si le modèle regarde de travers.")]
        public float Yaw;
    }

    /// <summary>
    /// Sous-lot V2.9.2 (4e vague) : les corps humains des Universal Base Characters de Quaternius, préparés par le menu : corps d'homme et de
    /// femme (Humanoid), matériaux des tenues peintes (un par corps, peau et tenue : <see cref="HumanLooks.Palettes"/>), coiffures, barbe,
    /// sourcils, yeux, et les animations Humanoid des Universal Animation Library renommées (<see cref="HumanLooks.CanonicalClip"/>).
    /// </summary>
    [Serializable]
    public sealed class HumanCatalog
    {
        public GameObject Male;
        public GameObject Female;
        [Tooltip("Tenues : homme peau claire, homme peau sombre, femme peau claire, femme peau sombre, chacun dans l'ordre de HumanLooks.Palettes.")]
        public Material[] Outfits = new Material[0];
        [Tooltip("Coiffures (Hair_Buzzed, Hair_SimpleParted, Hair_Long, Hair_Buns, Hair_BuzzedFemale), dans l'ordre de HumanHair (1 à 5).")]
        public GameObject[] Hairs = new GameObject[0];
        public GameObject Beard;
        public Material Hair1;
        public Material Hair2;
        public Material Eyes;
        public AnimationClip[] Clips = new AnimationClip[0];

        [Header("Vrais vêtements (sous-lot V2.9.4)")]
        [Tooltip("Pièces des Modular Character Outfits - Fantasy de Quaternius reconnues par la préparation (vide : tenues peintes).")]
        public WardrobeLook[] Clothes = new WardrobeLook[0];
        [Tooltip("Étoffes : un matériau partagé par famille, corps et variante de texture du pack.")]
        public WardrobeFabric[] Fabrics = new WardrobeFabric[0];
        [Tooltip("Peau des mains des pièces : homme clair, homme sombre, femme claire, femme sombre (vide : la peau du pack, assombrie).")]
        public Material[] Skins = new Material[0];
        [Tooltip("Corps réduits à la tête et au cou, portés sous les vrais vêtements (rien ne traverse) : homme, femme.")]
        public Mesh MaleHead;
        public Mesh FemaleHead;

        [NonSerialized] private WardrobeItem[] _items;

        /// <summary>Les pièces, sous la forme des règles (<see cref="HumanWardrobe.Resolve"/>), dans l'ordre de <see cref="Clothes"/>.</summary>
        public WardrobeItem[] Items
        {
            get
            {
                if (_items == null || _items.Length != (Clothes != null ? Clothes.Length : 0))
                {
                    var list = new System.Collections.Generic.List<WardrobeItem>();
                    if (Clothes != null)
                        foreach (WardrobeLook c in Clothes)
                            list.Add(c != null && c.Model != null ? new WardrobeItem(c.Family, c.Slot, c.Kind, c.Gender, c.Key) : new WardrobeItem(c != null ? c.Family : WardrobeFamily.Peasant, (WardrobeSlot)(-1), "", 0, ""));
                    _items = list.ToArray();
                }
                return _items;
            }
        }

        /// <summary>
        /// L'étoffe de racine <paramref name="stem"/> (<see cref="HumanWardrobe.FabricStem"/> du matériau de la pièce) pour ce corps, variante
        /// <paramref name="variant"/>, ou null (la pièce garde son matériau).
        /// </summary>
        public Material Fabric(string stem, int gender, int variant)
        {
            if (Fabrics == null || string.IsNullOrEmpty(stem)) return null;
            WardrobeFabric best = null;
            foreach (WardrobeFabric f in Fabrics)
                if (f != null && f.Stem == stem && f.Variants != null && f.Variants.Length > 0 && (f.Gender == gender || (f.Gender == 0 && best == null))) best = f;
            if (best == null) return null;
            return best.Variants[Math.Abs(variant) % best.Variants.Length];
        }

        /// <summary>Prêts : deux corps, toutes les tenues, une attente et une marche.</summary>
        public bool Ready
        {
            get
            {
                if (Male == null || Female == null || Outfits == null || Outfits.Length < 4 * HumanLooks.Palettes.Length || Clips == null) return false;
                bool idle = false, walk = false;
                foreach (AnimationClip c in Clips)
                {
                    if (c == null) continue;
                    if (c.name == "Idle") idle = true;
                    if (c.name == "Walk") walk = true;
                }
                return idle && walk;
            }
        }

        /// <summary>Matériau de la tenue <paramref name="palette"/> pour ce corps et cette peau, ou null.</summary>
        public Material Outfit(bool female, bool dark, int palette)
        {
            int n = HumanLooks.Palettes.Length;
            if (Outfits == null || palette < 0 || palette >= n) return null;
            int i = ((female ? 2 : 0) + (dark ? 1 : 0)) * n + palette;
            return i < Outfits.Length ? Outfits[i] : null;
        }

        /// <summary>La coiffure <paramref name="hair"/>, ou null.</summary>
        public GameObject Hair(HumanHair hair)
        {
            int i = (int)hair - 1;
            return Hairs != null && i >= 0 && i < Hairs.Length ? Hairs[i] : null;
        }
    }

    /// <summary>
    /// Sous-lot V2.9.4 : une pièce de vêtement des Modular Character Outfits - Fantasy de Quaternius : le modèle (même squelette que les
    /// Universal Base Characters), le maillage qui la porte (<see cref="Part"/> ; vide : tous ceux du fichier), sa famille, sa place, son genre.
    /// </summary>
    [Serializable]
    public sealed class WardrobeLook
    {
        public GameObject Model;
        public string Part;
        [Tooltip("Rang du maillage dans le fichier (-1 : son seul maillage).")]
        public int PartIndex = -1;
        [Tooltip("Maillages posés avec la pièce (ceintures, brassards), par leur rang dans le fichier.")]
        public int[] Companions = new int[0];
        public WardrobeFamily Family;
        public WardrobeSlot Slot;
        public string Kind;
        [Tooltip("0 : les deux corps ; 1 : homme ; 2 : femme.")]
        public int Gender;
        [HideInInspector] public string Key;
    }

    /// <summary>Sous-lot V2.9.4 : les étoffes d'une famille de vêtements (les variantes de texture du pack), un matériau partagé par variante.</summary>
    [Serializable]
    public sealed class WardrobeFabric
    {
        [Tooltip("Racine du nom commune au matériau des pièces et à ses textures (« ranger », « rangerbelts »).")]
        public string Stem;
        public WardrobeFamily Family;
        public int Gender;
        public Material[] Variants = new Material[0];
    }

    /// <summary>Sous-lot V2.9.4 : saison des vêtements (Auto : la saison du jeu, <see cref="HumanBodies.GameSeason"/> ; les autres la forcent pour l'essai).</summary>
    public enum StyleSeason
    {
        Auto = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3,
        Winter = 4,
    }

    /// <summary>Essai réaliste : une matière de bâtiment (couleur, relief, taille d'une répétition).</summary>
    [Serializable]
    public sealed class SurfaceLook
    {
        public StyleSurface Kind;
        public Texture2D Color;
        public Texture2D Normal;
        [Tooltip("Taille (m) d'une répétition de la texture.")]
        public float Tile = 2.5f;
    }

    /// <summary>Essai réaliste (27 octobre 2026) : un objet de décor (forge, pont, herbe de l'Aube, moulin) et son modèle.</summary>
    [Serializable]
    public sealed class PropLook
    {
        public StyleProp Kind;
        public GameObject Model;
        [Tooltip("Échelle ajoutée (1 : taille réelle du modèle, ou taille réglée par le jeu pour le pont et le moulin).")]
        public float Scale = 1f;
        [Tooltip("Rotation ajoutée (degrés) si le modèle est posé de travers.")]
        public float Yaw;
    }

    /// <summary>
    /// Essai réaliste 8 (demande de Damien du 28 octobre 2026 : de vrais bâtiments en 3D) : un modèle de bâtiment de Sketchfab, rangé par
    /// genre (« Assets/Style/Sketchfab/Batiments/Maison/… »), et son pied (m, x et z, à l'échelle 1, face vers +z) mesuré par le menu.
    /// </summary>
    [Serializable]
    public sealed class BuildingLook
    {
        public StyleBuilding Kind;
        public GameObject Model;
        [Tooltip("Pied du modèle (m) le long de x et de z, à l'échelle 1 (mesuré par le menu).")]
        public Vector2 Size;
    }

    /// <summary>
    /// Essai réaliste 8 : un effet de combat d'un pack de l'Asset Store (prefab de particules), sa taille, sa rotation et sa durée,
    /// modifiables dans l'inspecteur.
    /// </summary>
    [Serializable]
    public sealed class VfxLook
    {
        public StyleVfx Kind;
        public GameObject Prefab;
        [Tooltip("Échelle de l'effet (1 : taille du pack).")]
        public float Scale = 1f;
        [Tooltip("Rotation ajoutée (degrés) si l'effet est tourné de travers (la taille d'épée).")]
        public float Yaw;
        [Tooltip("Durée (s) avant que l'effet soit retiré.")]
        public float Life = 2.5f;
    }

    /// <summary>
    /// Intérieurs (demande de Damien du 28 octobre 2026 : « de beaux intérieurs bien détaillés ») : un meuble ou un objet d'intérieur de
    /// Poly Haven (dossier « Assets/Style/PolyHaven/Interieurs »), reconnu par son nom ; rotation et échelle réglables dans l'inspecteur.
    /// </summary>
    [Serializable]
    public sealed class InteriorLook
    {
        public InteriorProp Kind;
        public GameObject Model;
        [Tooltip("Rotation ajoutée (degrés) si le modèle regarde de travers.")]
        public float Yaw;
        [Tooltip("Échelle ajoutée à la taille donnée par le jeu (1 : celle du jeu).")]
        public float Scale = 1f;
    }

    /// <summary>Essai réaliste 7 : les variantes d'un bruitage.</summary>
    [Serializable]
    public sealed class SoundSet
    {
        public StyleSound Kind;
        public AudioClip[] Clips;
    }

    /// <summary>Essai réaliste 7 : une musique (village, exploration, combat, Gardien).</summary>
    [Serializable]
    public sealed class MusicLook
    {
        public StyleMusic Kind;
        public AudioClip Clip;
    }

    /// <summary>Essai réaliste 7 : une nappe d'ambiance en boucle (oiseaux, vent, rivière).</summary>
    [Serializable]
    public sealed class AmbienceLook
    {
        public StyleAmbience Kind;
        public AudioClip Clip;
    }

    /// <summary>
    /// Sous-lot V2.11 (5e vague) : une ressource de la liste partagée <see cref="Wave5"/> choisie par la préparation : son modèle, sa taille et
    /// sa rotation réglables à la main (gardées d'une préparation à l'autre), le matériau fait pour un scan photo, et, pour une arme, le sens
    /// de sa prise (grand axe et bout du haut, mesurés sur ses sommets par la préparation).
    /// </summary>
    [Serializable]
    public sealed class Wave5Look
    {
        public Wave5Role Role;
        public GameObject Model;
        public float Scale = 1f;
        public float Yaw;
        /// <summary>Sous-lot V2.12 : décalage (m) ajouté à la place choisie par le jeu (positions estimées sans Unity : réglables ici, gardées).</summary>
        [Tooltip("Décalage (m) ajouté à la place choisie par le jeu.")]
        public Vector3 Offset;
        /// <summary>Matériau URP Lit fait depuis les textures d'un scan (Megascans), posé sur tout le modèle ; null sinon.</summary>
        public Material Material;
        /// <summary>Arme : grand axe du modèle (0 : x, 1 : y, 2 : z).</summary>
        public int GripAxis = 1;
        /// <summary>Arme : +1 si le bout « max » de ce grand axe va en haut, -1 sinon.</summary>
        public int GripSign = 1;
    }

    /// <summary>Sous-lot V2.11 : icône du pack de CraftPix (licence de l'Asset Store, hors dépôt) d'un objet, choisie par <see cref="ItemIcons"/>.</summary>
    [Serializable]
    public sealed class ItemIconLook
    {
        public string Item;
        public Texture2D Icon;
    }

    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026) : les modèles et les sons choisis dans les packs gratuits déposés dans
    /// <c>Assets/Style</c>. Rempli par le menu « Éclats du Seuil → Préparer l'essai de style » (<c>Assets/Style/Resources/StyleCatalog.asset</c>),
    /// modifiable à la main dans l'inspecteur (glisser un autre modèle, tourner un modèle qui regarde de travers). Absent : le jeu garde ses
    /// formes simples. Essai réaliste (26 octobre 2026) : aussi le ciel, les textures du sol, les animations Mixamo du héros, et les
    /// réglages de lumière et de couleurs (<see cref="StyleWorld"/>), modifiables pendant le jeu dans l'éditeur.
    /// </summary>
    public sealed class StyleCatalog : ScriptableObject
    {
        public const string ResourceName = "StyleCatalog";

        public GameObject Hero;
        public GameObject Wolf;
        public GameObject Boar;
        public GameObject[] Trees = new GameObject[0];
        public GameObject[] Rocks = new GameObject[0];
        public GameObject[] Plants = new GameObject[0];

        /// <summary>Essai réaliste : bois mort (souches, troncs couchés) posé dans la forêt autour de l'aire.</summary>
        public GameObject[] Debris = new GameObject[0];

        [Header("Essai réaliste : personnages (dossiers de Mixamo) et bâtiments")]
        public CharacterLook[] Characters = new CharacterLook[0];
        public SurfaceLook[] Surfaces = new SurfaceLook[0];
        [Tooltip("Couleur des loups (leur pack n'a pas de texture).")]
        public Color WolfColor = new Color(0.45f, 0.42f, 0.38f);
        [Tooltip("Couleur des sangliers (leur pack n'a pas de texture).")]
        public Color BoarColor = new Color(0.33f, 0.24f, 0.17f);
        [Tooltip("Couleur de l'eau de la rivière.")]
        public Color WaterColor = new Color(0.1f, 0.24f, 0.27f);

        [Header("Essai réaliste : forge, pont, herbe de l'Aube, moulin, eau (27 octobre 2026)")]
        public PropLook[] Props = new PropLook[0];
        [Tooltip("Matériau de l'eau (shader « EdS/Eau », créé par le menu) ; ses réglages se changent dans Assets/Style/Generated/Eau.")]
        public Material Water;
        [Tooltip("Arbustes des haies.")]
        public GameObject[] Shrubs = new GameObject[0];
        [Tooltip("Teinte de l'herbe du sol (blanc : couleur de la texture ; essai du 27 octobre 2026 : herbe trop jaune).")]
        public Color GrassTint = new Color(0.66f, 0.86f, 0.58f);

        [Header("Essai réaliste 4 : herbe en brins, arbres, rendu du téléphone (27 octobre 2026)")]
        [Tooltip("Matériau de l'herbe en brins (shader « EdS/Herbe », créé par le menu) ; couleurs et vent se changent dans Assets/Style/Generated/Herbe.")]
        public Material Grass;
        [Tooltip("Touffes d'herbe par mètre carré sur ordinateur (le cinquième sur téléphone).")]
        [Range(0f, 12f)] public float GrassDensity = 4f;
        [Tooltip("Distance (m) jusqu'où l'herbe en brins est dessinée sur ordinateur (la moitié sur téléphone).")]
        [Range(10f, 120f)] public float GrassDistance = 45f;
        [Tooltip("Hauteur (m) des arbres de la forêt (variée de plus ou moins un quart) ; les arbres de l'aire en font les quatre cinquièmes.")]
        [Range(3f, 30f)] public float TreeHeight = 11f;
        [Tooltip("Rendu plus léger pour le téléphone (ombres, effets), créé par le menu ; l'ordinateur garde le rendu du projet.")]
        public UnityEngine.Rendering.RenderPipelineAsset PhonePipeline;

        /// <summary>Rotation ajoutée (degrés) aux modèles qui ne regardent pas devant eux une fois importés.</summary>
        public float HeroYaw;
        public float WolfYaw;
        public float BoarYaw;

        [Header("Sous-lot V2.9 : vrais modèles des bêtes et du chat (remplis par le menu)")]
        [Tooltip("Cerf à bois (Stag de Quaternius) : le chef de la harde et un cerf sur trois ; sans lui, les formes simples.")]
        public GameObject Stag;
        [Tooltip("Biche (Deer de Quaternius) : les autres cerfs de la harde.")]
        public GameObject Doe;
        [Tooltip("Lièvre (Rabbit de Pneshik, Sketchfab).")]
        public GameObject Hare;
        [Tooltip("Chat compagnon (Stripe the Cat de DreamNoms, converti en FBX) ; teint aux couleurs de Pain-d'Épice.")]
        public GameObject Cat;
        [Tooltip("2e vague : corbeau, vache, chien et cheval (Sketchfab, convertis) : les bêtes de la ferme et des champs, posées par le jeu.")]
        public GameObject Crow;
        public GameObject Cow;
        public GameObject Dog;
        public GameObject Horse;
        [Tooltip("Sous-lot V2.9.2 (3e vague) : âne, poule, coq (basse-cour du hameau et du bourg) et ours (prêt pour une créature « ours… »).")]
        public GameObject Donkey;
        public GameObject Hen;
        public GameObject Rooster;
        public GameObject Bear;
        public float BearYaw;
        /// <summary>Sous-lot V2.11 (5e vague) : le grand loup animé d'emptypen2477, pour Croc-Gris et le loup blanc (<see cref="Wave5.IsDireWolf"/>).</summary>
        public GameObject DireWolf;
        public float DireWolfYaw;
        /// <summary>Sous-lot V2.11 : les ressources de la 5e vague trouvées (une par rôle).</summary>
        public Wave5Look[] Wave5 = new Wave5Look[0];
        /// <summary>Sous-lot V2.11 : icônes de CraftPix des objets (vide sans le pack : les icônes du jeu restent).</summary>
        public ItemIconLook[] ItemIcons = new ItemIconLook[0];
        /// <summary>Sous-lot V2.12 : fond des fenêtres du livre pris dans Fantasy Wooden GUI (pierre par défaut ; Off : la dalle du jeu).</summary>
        [Tooltip("Fond des fenêtres : Stone (pierre, garde le texte clair lisible), Parchment (clair : à juger), Off (dalle du jeu).")]
        public AssetStore.GuiBoard GuiBoardKind = AssetStore.GuiBoard.Stone;
        /// <summary>Sous-lot V2.12 : planches de pierre et de parchemin trouvées (null sans le pack : la dalle du jeu reste).</summary>
        public Texture2D GuiStone;
        public Texture2D GuiParchment;
        /// <summary>Relecture de la PR #122 : bords gardés entiers quand la planche est étirée, en part de son plus petit côté (estimé : à régler à l'essai).</summary>
        [Tooltip("Bords gardés entiers quand la planche est étirée, en part de son plus petit côté (0,12 : 12 %).")]
        [Range(0f, 0.45f)] public float GuiBorderFraction = 0.12f;
        /// <summary>Sous-lot V2.11 : animations de métiers de Mixamo (<c>Assets/Style/Mixamo/Metiers</c>), prêtes pour les villageois.</summary>
        public AnimationClip[] TradeClips = new AnimationClip[0];
        [Header("Sous-lot V2.9.2 : corps humains (Universal Base Characters et Universal Animation Library, remplis par le menu)")]
        public HumanCatalog Humans = new HumanCatalog();
        [Tooltip("Les villageois, les gardes et les PNJ qui parlent prennent les corps humains variés (coché) ou leurs personnages de Mixamo.")]
        public bool HumanVillagers = true;
        [Tooltip("Le héros prend un corps humain (tenue d'acier bleuté) quand le paladin de Mixamo manque.")]
        public bool HumanHero = true;

        /// <summary>Sous-lot V2.9.2 : version des tenues peintes par la préparation (repeintes si elle change).</summary>
        [HideInInspector] public int HumanBake;
        [Tooltip("Sous-lot V2.9.4 : vrais vêtements (pièces du pack de Quaternius) sur les corps humains ; décoché ou sans pièces : les tenues peintes.")]
        public bool HumanClothes = true;
        [Tooltip("Saison des vêtements : Auto suit la saison du jeu (printemps tant qu'il n'y a pas de calendrier) ; les autres la forcent pour l'essai.")]
        public StyleSeason HumanSeason = StyleSeason.Auto;
        [Tooltip("Tenues de fête (beaux habits, couleurs vives, usure effacée) : pour les fêtes du bourg à venir, ou pour l'essai.")]
        public bool HumanFestival;
        [Tooltip("Bêtes de la ferme et des champs (vaches, chevaux, chiens, corbeaux) : 1 = le nombre prévu, 0 = aucune (la moitié sur téléphone).")]
        [Range(0f, 2f)] public float Livestock = 1f;
        [Tooltip("Volume des pas du personnage (herbe, gravier, feuilles, plancher).")]
        [Range(0f, 1f)] public float StepVolume = 0.35f;
        public float StagYaw;
        public float DoeYaw;
        public float HareYaw;
        public float CatYaw;
        [Tooltip("Couleur du loup blanc du contrat de la guilde (les loups gris gardent les couleurs de leur modèle).")]
        public Color WhiteWolfColor = new Color(0.9f, 0.9f, 0.88f);
        [Tooltip("Teindre le chat aux couleurs du compagnon (Pain-d'Épice est roux) ; décoché : les couleurs du modèle.")]
        public bool TintCat = true;
        [Tooltip("Hauteur (m) du chat, oreilles comprises.")]
        [Range(0.2f, 0.7f)] public float CatHeight = 0.36f;
        [Tooltip("Volume des voix des bêtes (hurlements, brame, grognements, lièvre) et des sons des failles et du tocsin.")]
        [Range(0f, 1f)] public float BeastVolume = 0.8f;
        [Tooltip("Distance (m) jusqu'où l'on entend une bête (les hurlements et le brame portent deux fois plus loin, le tocsin trois fois).")]
        [Range(10f, 150f)] public float BeastHearing = 60f;

        /// <summary>Sous-lot V2.9 : version des réglages proposés par la préparation (rotation du sanglier de Hanabi), mis une seule fois.</summary>
        public const int AnimalDefaults = 1;

        [HideInInspector] public int AnimalVersion;

        [Header("Essai réaliste : animations du héros (fichiers Mixamo à côté de son modèle)")]
        public AnimationClip[] HeroClips = new AnimationClip[0];

        [Header("Essai réaliste : ciel et sol (remplis par le menu)")]
        public Material Sky;
        public Material Terrain;
        public Texture2D SoilColor;
        public Texture2D SoilNormal;
        public Texture2D RockColor;
        public Texture2D RockNormal;
        public Texture2D GrassColor;
        public Texture2D GrassNormal;

        [Header("Essai réaliste 8 : bâtiments de Sketchfab, chemins et champs (28 octobre 2026)")]
        [Tooltip("Modèles des bâtiments, par genre (remplis par le menu) ; sans modèle d'un genre, le bâtiment fait par le code reste.")]
        public BuildingLook[] Buildings = new BuildingLook[0];
        [Tooltip("Terre battue des chemins (dossier « Ground », nom « chemin… »).")]
        public Texture2D RoadColor;
        public Texture2D RoadNormal;
        [Tooltip("Terre labourée des champs (dossier « Ground », nom « champ… »).")]
        public Texture2D FieldColor;
        public Texture2D FieldNormal;
        [Tooltip("Villageois au plus sur ordinateur (le tiers sur téléphone).")]
        [Range(0, 30)] public int Villagers = 14;
        [Tooltip("Effets de combat des packs (Hit Effects FREE, Magic Effects FREE, Free Slash VFX), sur ordinateur ; sans eux, les particules du jeu.")]
        public VfxLook[] Vfx = new VfxLook[0];
        [Tooltip("Eau de lac du sample Production Ready Shaders (rempli par le menu), sur ordinateur ; sans elle, l'eau « EdS/Eau ».")]
        public Material SampleWater;

        [Header("Intérieurs : meubles et objets (28 octobre 2026)")]
        [Tooltip("Meubles et objets des intérieurs (Poly Haven, dossier « Interieurs »), remplis par le menu ; sans modèle, une forme simple.")]
        public InteriorLook[] Furniture = new InteriorLook[0];
        [Tooltip("Lumière d'ambiance des intérieurs (les feux, bougies et lanternes éclairent le reste).")]
        public Color IndoorAmbient = DefaultIndoorAmbient;
        [Tooltip("Luminosité générale des intérieurs.")]
        [Range(-2f, 2f)] public float IndoorExposure = DefaultIndoorExposure;
        [Tooltip("Force des feux, bougies et lanternes des intérieurs.")]
        [Range(0f, 4f)] public float IndoorLights = DefaultIndoorLights;

        /// <summary>
        /// V1.10 (essai 10 de Damien : « intérieur trop sombre ») : version des réglages par défaut de la lumière des intérieurs. Le menu
        /// « Préparer l'essai de style » remet une fois les nouveaux (ambiance plus claire, exposition et lumières plus fortes) ; les
        /// retouches faites ensuite par Damien sont gardées. Les réglages du dehors (<see cref="AmbienceVersion"/>) ne bougent pas.
        /// </summary>
        public const int IndoorDefaults = 1;

        [HideInInspector] public int IndoorVersion;

        private static readonly Color DefaultIndoorAmbient = new Color(0.38f, 0.31f, 0.25f);
        private const float DefaultIndoorExposure = 0.3f;
        private const float DefaultIndoorLights = 1.6f;

        /// <summary>Remet la lumière des intérieurs aux réglages de l'essai (clic droit sur le catalogue dans l'inspecteur).</summary>
        [ContextMenu("Remettre la lumière des intérieurs par défaut")]
        public void ResetIndoor()
        {
            IndoorAmbient = DefaultIndoorAmbient;
            IndoorExposure = DefaultIndoorExposure;
            IndoorLights = DefaultIndoorLights;
            IndoorVersion = IndoorDefaults;
        }

        [Header("Essai réaliste : décor (pris au lancement du combat)")]
        [Tooltip("Taille (m) d'une répétition des textures du sol.")]
        [Range(1f, 20f)] public float GroundTile = 5f;
        [Tooltip("Largeur (m) de la forêt autour de l'aire de jeu.")]
        [Range(0f, 80f)] public float ForestMargin = 45f;
        [Tooltip("Arbres de la lisière (le tiers sur téléphone).")]
        [Range(0, 300)] public int ForestTrees = 110;
        [Tooltip("Plantes et touffes d'herbe dans l'aire (la moitié sur téléphone).")]
        [Range(0, 300)] public int PlantCount = 140;

        [Header("Essai réaliste : lumière et couleurs (réglables pendant le jeu, dans l'éditeur)")]
        public Color SunColor = DefaultSunColor;
        [Range(0f, 3f)] public float SunIntensity = DefaultSunIntensity;
        [Tooltip("Hauteur du soleil (degrés au-dessus de l'horizon).")]
        [Range(5f, 85f)] public float SunHeight = DefaultSunHeight;
        [Tooltip("Direction du soleil (degrés).")]
        [Range(-180f, 180f)] public float SunTurn = DefaultSunTurn;
        public Color AmbientSky = DefaultAmbientSky;
        public Color AmbientHorizon = DefaultAmbientHorizon;
        public Color AmbientGround = DefaultAmbientGround;
        public Color FogColor = DefaultFogColor;
        [Range(0f, 200f)] public float FogStart = DefaultFogStart;
        [Range(10f, 400f)] public float FogEnd = DefaultFogEnd;
        [Tooltip("Luminosité générale.")]
        [Range(-2f, 2f)] public float Exposure = DefaultExposure;
        [Range(-50f, 50f)] public float Contrast = DefaultContrast;
        [Tooltip("Couleurs plus vives (positif) ou plus ternes (négatif).")]
        [Range(-50f, 50f)] public float Saturation = DefaultSaturation;
        [Tooltip("Halo autour des zones très claires (ordinateur seulement).")]
        [Range(0f, 2f)] public float Bloom = DefaultBloom;
        [Tooltip("Bords de l'image assombris.")]
        [Range(0f, 0.5f)] public float Vignette = DefaultVignette;

        [Header("Sous-lot V2.1 : jour et nuit (réglables pendant le jeu, dans l'éditeur)")]
        [Tooltip("Clarté de la nuit : 1 = nuit bleue et lisible prévue ; plus haut, plus clair ; plus bas, plus sombre.")]
        [Range(0.5f, 2f)] public float NightBrightness = 1f;
        [Tooltip("Force des lanternes, réverbères et fenêtres allumées la nuit.")]
        [Range(0f, 4f)] public float LanternIntensity = 1.6f;
        [Tooltip("Éditeur seulement : heure montrée par la lumière (0 à 24) pour régler la nuit sans l'attendre ; -1 = l'heure du monde. Seule la lumière change (lanternes, ciel, couleurs) : les créatures et les villageois suivent l'heure du monde.")]
        [Range(-1f, 24f)] public float PreviewHour = -1f;

        /// <summary>
        /// Version des réglages par défaut de la lumière et des couleurs. Quand Claude change ces réglages (essai de Damien du 26 octobre
        /// 2026 : image trop claire et jaune), le menu « Préparer l'essai de style » remet une fois les nouveaux ; les retouches faites
        /// ensuite par Damien sont gardées.
        /// </summary>
        public const int AmbienceDefaults = 2;

        [HideInInspector] public int AmbienceVersion;

        private static readonly Color DefaultSunColor = new Color(1f, 0.95f, 0.87f);
        private const float DefaultSunIntensity = 1.25f;
        private const float DefaultSunHeight = 45f;
        private const float DefaultSunTurn = -35f;
        private static readonly Color DefaultAmbientSky = new Color(0.5f, 0.6f, 0.76f);
        private static readonly Color DefaultAmbientHorizon = new Color(0.52f, 0.53f, 0.5f);
        private static readonly Color DefaultAmbientGround = new Color(0.26f, 0.24f, 0.2f);
        private static readonly Color DefaultFogColor = new Color(0.7f, 0.78f, 0.87f);
        private const float DefaultFogStart = 40f;
        private const float DefaultFogEnd = 170f;
        private const float DefaultExposure = -0.1f;
        private const float DefaultContrast = 15f;
        private const float DefaultSaturation = 12f;
        private const float DefaultBloom = 0.25f;
        private const float DefaultVignette = 0.2f;

        /// <summary>Remet la lumière et les couleurs aux réglages de l'essai (clic droit sur le catalogue dans l'inspecteur).</summary>
        [ContextMenu("Remettre la lumière et les couleurs par défaut")]
        public void ResetAmbience()
        {
            SunColor = DefaultSunColor;
            SunIntensity = DefaultSunIntensity;
            SunHeight = DefaultSunHeight;
            SunTurn = DefaultSunTurn;
            AmbientSky = DefaultAmbientSky;
            AmbientHorizon = DefaultAmbientHorizon;
            AmbientGround = DefaultAmbientGround;
            FogColor = DefaultFogColor;
            FogStart = DefaultFogStart;
            FogEnd = DefaultFogEnd;
            Exposure = DefaultExposure;
            Contrast = DefaultContrast;
            Saturation = DefaultSaturation;
            Bloom = DefaultBloom;
            Vignette = DefaultVignette;
            AmbienceVersion = AmbienceDefaults;
        }

        public AudioClip Hit;
        public AudioClip Block;
        public AudioClip Parry;
        public AudioClip Death;
        public AudioClip Potion;
        public AudioClip Gather;
        public AudioClip Craft;

        /// <summary>
        /// Essai réaliste 7 (demande de Damien du 28 octobre 2026 : « sons […] libres », musique) : variantes de chaque bruitage (un même
        /// geste ne sonne pas toujours pareil), musiques et nappes d'ambiance, trouvées par la préparation dans <c>Assets/Style</c>.
        /// </summary>
        [Header("Essai réaliste 7 : sons, musique, ambiance")]
        public SoundSet[] SoundSets;

        public MusicLook[] Music;
        public AmbienceLook[] Ambiences;

        [Range(0f, 1f)] public float MusicVolume = 0.35f;
        [Range(0f, 1f)] public float AmbienceVolume = 0.45f;

        private static StyleCatalog _loaded;
        private static bool _tried;

        /// <summary>Le catalogue de l'essai, ou null (pas d'essai de style préparé).</summary>
        public static StyleCatalog Loaded
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _loaded = Resources.Load<StyleCatalog>(ResourceName);
                }
                return _loaded;
            }
        }

        /// <summary>Modèle pour <paramref name="slot"/> ; pour le décor, une variante stable d'après l'identifiant de l'élément.</summary>
        public GameObject Model(StyleSlot slot, string id)
        {
            switch (slot)
            {
                case StyleSlot.Hero: return Hero;
                case StyleSlot.Wolf: return UseDireWolf(id) ? DireWolf : Wolf;   // V2.11 : Croc-Gris, loup blanc
                case StyleSlot.Boar: return Boar;
                case StyleSlot.Tree: return Pick(Trees, id);
                case StyleSlot.Rock: return Pick(Rocks, id);
                case StyleSlot.Plant: return Pick(Plants, id);
                case StyleSlot.Debris: return Pick(Debris, id);
                case StyleSlot.Character: return Character(id)?.Model;
                case StyleSlot.Shrub: return Pick(Shrubs, id);
                case StyleSlot.Stag: return Stag;
                case StyleSlot.Doe: return Doe;
                case StyleSlot.Hare: return Hare;
                case StyleSlot.Cat: return Cat;
                case StyleSlot.Crow: return Crow;
                case StyleSlot.Cow: return Cow;
                case StyleSlot.Dog: return Dog;
                case StyleSlot.Horse: return Horse;
                case StyleSlot.Donkey: return Donkey;
                case StyleSlot.Hen: return Hen;
                case StyleSlot.Rooster: return Rooster;
                case StyleSlot.Bear: return Bear;
                default: return null;
            }
        }

        public float Yaw(StyleSlot slot, string id = null)
        {
            switch (slot)
            {
                case StyleSlot.Hero: return HeroYaw;
                case StyleSlot.Wolf: return UseDireWolf(id) ? DireWolfYaw : WolfYaw;
                case StyleSlot.Boar: return BoarYaw;
                case StyleSlot.Stag: return StagYaw;
                case StyleSlot.Doe: return DoeYaw;
                case StyleSlot.Hare: return HareYaw;
                case StyleSlot.Cat: return CatYaw;
                case StyleSlot.Bear: return BearYaw;
                case StyleSlot.Character: return Character(id)?.Yaw ?? 0f;
                default: return 0f;
            }
        }

        /// <summary>Essai réaliste 8 : l'effet de combat <paramref name="kind"/> d'un pack, ou null.</summary>
        public VfxLook Effect(StyleVfx kind)
        {
            if (Vfx == null) return null;
            foreach (VfxLook v in Vfx)
                if (v != null && v.Kind == kind && v.Prefab != null) return v;
            return null;
        }

        /// <summary>Essai réaliste 8 : les modèles de bâtiment du genre <paramref name="kind"/>, mesurés (vide sans modèle).</summary>
        public System.Collections.Generic.List<BuildingLook> BuildingsOf(StyleBuilding kind)
        {
            var list = new System.Collections.Generic.List<BuildingLook>();
            if (Buildings == null) return list;
            foreach (BuildingLook b in Buildings)
                if (b != null && b.Kind == kind && b.Model != null && b.Size.x > 0.01f && b.Size.y > 0.01f) list.Add(b);
            return list;
        }

        /// <summary>Intérieurs : un modèle de l'objet <paramref name="kind"/>, choisi de façon stable d'après <paramref name="id"/> ; null sans modèle.</summary>
        public InteriorLook Interior(InteriorProp kind, string id)
        {
            if (Furniture == null) return null;
            var list = new System.Collections.Generic.List<InteriorLook>();
            foreach (InteriorLook f in Furniture)
                if (f != null && f.Kind == kind && f.Model != null) list.Add(f);
            return list.Count == 0 ? null : list[StyleRules.Variant(id ?? string.Empty, list.Count)];
        }

        /// <summary>Personnage d'un PNJ ou d'un ennemi d'après son identifiant (« npc.odile », « gardien_eveille »), ou null.</summary>
        public CharacterLook Character(string id)
        {
            string key = StyleRules.CharacterOf(id);
            if (Characters == null || key.Length == 0) return null;
            foreach (CharacterLook c in Characters)
                if (c != null && c.Model != null && c.Id == key) return c;
            return null;
        }

        /// <summary>Animations à part d'un modèle : celles du héros (Mixamo) ou d'un personnage ; vide sinon.</summary>
        public AnimationClip[] Clips(StyleSlot slot, string id) =>
            slot == StyleSlot.Hero ? HeroClips : slot == StyleSlot.Character ? Character(id)?.Clips : null;

        /// <summary>Sous-lot V2.11 : la ressource de la 5e vague du rôle <paramref name="role"/>, ou null (pas déposée, ou pas sur ce téléphone).</summary>
        public Wave5Look Look(Wave5Role role)
        {
            if (Wave5 == null) return null;
            foreach (Wave5Look w in Wave5)
                if (w != null && w.Role == role && w.Model != null)
                    return Shown(role) ? w : null;
            return null;
        }

        /// <summary>Relecture de la PR #121 : la ressource se montre-t-elle ici ? (un rôle hors de la liste, comme le corbeau : oui)</summary>
        private static bool Shown(Wave5Role role) =>
            !(EdS.Runtime.Wave5.EntryOrNull(role) is Wave5Entry entry) || EdS.Runtime.Wave5.Shown(entry, Application.isMobilePlatform);

        /// <summary>Le grand loup pour Croc-Gris et le loup blanc, sur ordinateur seulement (35 k triangles : le Note8 garde le loup des meutes).</summary>
        private bool UseDireWolf(string id) => DireWolf != null && EdS.Runtime.Wave5.IsDireWolf(id) && Shown(Wave5Role.DireWolf);

        /// <summary>Sous-lot V2.11 : toutes les ressources trouvées du rôle (les pains : plusieurs, le meilleur d'abord) ; vide sans elles.</summary>
        public System.Collections.Generic.List<Wave5Look> Looks(Wave5Role role)
        {
            var list = new System.Collections.Generic.List<Wave5Look>();
            if (Wave5 == null || !Shown(role)) return list;
            foreach (Wave5Look w in Wave5)
                if (w != null && w.Role == role && w.Model != null) list.Add(w);
            return list;
        }

        /// <summary>Sous-lot V2.12 : la planche de Fantasy Wooden GUI à poser en fond de fenêtre, ou null (la dalle du jeu reste).</summary>
        public Texture2D GuiBoard()
        {
            if (GuiBoardKind == AssetStore.GuiBoard.Stone) return GuiStone;
            if (GuiBoardKind == AssetStore.GuiBoard.Parchment) return GuiParchment;
            return null;
        }

        /// <summary>Sous-lot V2.11 : l'icône de CraftPix de l'objet <paramref name="item"/>, ou null (l'icône du jeu reste).</summary>
        public Texture2D ItemIcon(string item)
        {
            if (ItemIcons == null || string.IsNullOrEmpty(item)) return null;
            foreach (ItemIconLook i in ItemIcons)
                if (i != null && i.Icon != null && i.Item == item) return i.Icon;
            return null;
        }

        /// <summary>Objet de décor, ou null s'il n'a pas de modèle.</summary>
        public PropLook Prop(StyleProp kind)
        {
            if (Props == null) return null;
            foreach (PropLook p in Props)
                if (p != null && p.Kind == kind && p.Model != null) return p;
            return null;
        }

        /// <summary>Matière de bâtiment, ou null si elle n'a pas de texture.</summary>
        public SurfaceLook Surface(StyleSurface kind)
        {
            if (Surfaces == null) return null;
            foreach (SurfaceLook s in Surfaces)
                if (s != null && s.Kind == kind && s.Color != null) return s;
            return null;
        }

        /// <summary>
        /// Essai réaliste 7 : une variante du son <paramref name="sound"/> (rang <paramref name="variant"/> modulo leur nombre) ; sans
        /// variantes, le son unique des essais précédents ; null sans son.
        /// </summary>
        public AudioClip Sound(StyleSound sound, int variant)
        {
            if (SoundSets != null)
                foreach (SoundSet set in SoundSets)
                    if (set != null && set.Kind == sound && set.Clips != null && set.Clips.Length > 0)
                        return set.Clips[(variant % set.Clips.Length + set.Clips.Length) % set.Clips.Length];
            return Sound(sound);
        }

        /// <summary>Essai réaliste 7 : la musique <paramref name="kind"/>, ou null.</summary>
        public AudioClip MusicClip(StyleMusic kind)
        {
            if (Music == null) return null;
            foreach (MusicLook m in Music)
                if (m != null && m.Kind == kind && m.Clip != null) return m.Clip;
            return null;
        }

        /// <summary>Essai réaliste 7 : la nappe d'ambiance <paramref name="kind"/>, ou null.</summary>
        public AudioClip AmbienceClip(StyleAmbience kind)
        {
            if (Ambiences == null) return null;
            foreach (AmbienceLook a in Ambiences)
                if (a != null && a.Kind == kind && a.Clip != null) return a.Clip;
            return null;
        }

        public AudioClip Sound(StyleSound sound)
        {
            switch (sound)
            {
                case StyleSound.Hit: return Hit;
                case StyleSound.Block: return Block;
                case StyleSound.Parry: return Parry;
                case StyleSound.Death: return Death;
                case StyleSound.Potion: return Potion;
                case StyleSound.Gather: return Gather;
                case StyleSound.Craft: return Craft;
                default: return null;
            }
        }

        private static GameObject Pick(GameObject[] models, string id)
        {
            if (models == null || models.Length == 0) return null;
            int i = StyleRules.Variant(id, models.Length);
            return i >= 0 ? models[i] : null;
        }
    }
}
