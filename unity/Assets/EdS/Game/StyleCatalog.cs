using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
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
        public Color SunColor = new Color(1f, 0.93f, 0.8f);
        [Range(0f, 3f)] public float SunIntensity = 1.5f;
        [Tooltip("Hauteur du soleil (degrés au-dessus de l'horizon).")]
        [Range(5f, 85f)] public float SunHeight = 40f;
        [Tooltip("Direction du soleil (degrés).")]
        [Range(-180f, 180f)] public float SunTurn = -35f;
        public Color AmbientSky = new Color(0.6f, 0.7f, 0.86f);
        public Color AmbientHorizon = new Color(0.62f, 0.62f, 0.55f);
        public Color AmbientGround = new Color(0.32f, 0.29f, 0.23f);
        public Color FogColor = new Color(0.74f, 0.81f, 0.87f);
        [Range(0f, 200f)] public float FogStart = 30f;
        [Range(10f, 400f)] public float FogEnd = 150f;
        [Tooltip("Luminosité générale.")]
        [Range(-2f, 2f)] public float Exposure = 0.2f;
        [Range(-50f, 50f)] public float Contrast = 8f;
        [Tooltip("Couleurs plus vives (positif) ou plus ternes (négatif).")]
        [Range(-50f, 50f)] public float Saturation = 20f;
        [Tooltip("Halo autour des zones très claires (ordinateur seulement).")]
        [Range(0f, 2f)] public float Bloom = 0.3f;
        [Tooltip("Bords de l'image assombris.")]
        [Range(0f, 0.5f)] public float Vignette = 0.15f;

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
                default: return null;
            }
        }

        public float Yaw(StyleSlot slot) => slot == StyleSlot.Hero ? HeroYaw : slot == StyleSlot.Wolf ? WolfYaw : slot == StyleSlot.Boar ? BoarYaw : 0f;

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
