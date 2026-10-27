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
                case StyleSlot.Wolf: return Wolf;
                case StyleSlot.Boar: return Boar;
                case StyleSlot.Tree: return Pick(Trees, id);
                case StyleSlot.Rock: return Pick(Rocks, id);
                case StyleSlot.Plant: return Pick(Plants, id);
                case StyleSlot.Debris: return Pick(Debris, id);
                case StyleSlot.Character: return Character(id)?.Model;
                case StyleSlot.Shrub: return Pick(Shrubs, id);
                default: return null;
            }
        }

        public float Yaw(StyleSlot slot, string id = null) => slot == StyleSlot.Hero ? HeroYaw : slot == StyleSlot.Wolf ? WolfYaw : slot == StyleSlot.Boar ? BoarYaw
            : slot == StyleSlot.Character ? Character(id)?.Yaw ?? 0f : 0f;

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
