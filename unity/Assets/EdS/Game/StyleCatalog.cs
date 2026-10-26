using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026) : les modèles et les sons choisis dans les packs gratuits déposés dans
    /// <c>Assets/Style</c>. Rempli par le menu « Éclats du Seuil → Préparer l'essai de style » (<c>Assets/Style/Resources/StyleCatalog.asset</c>),
    /// modifiable à la main dans l'inspecteur (glisser un autre modèle, tourner un modèle qui regarde de travers). Absent : le jeu garde ses
    /// formes simples.
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

        /// <summary>Rotation ajoutée (degrés) aux modèles qui ne regardent pas devant eux une fois importés.</summary>
        public float HeroYaw;
        public float WolfYaw;
        public float BoarYaw;

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
