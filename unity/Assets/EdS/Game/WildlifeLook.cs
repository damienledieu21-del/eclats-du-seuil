using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.4 (animaux paisibles) : cerf ou lièvre dessiné en formes simples (aucun modèle livré) sur le corps d'un combattant : pièces
    /// de <see cref="Wildlife.Parts"/>, pattes qui balancent et corps qui rebondit à la course (<see cref="Wildlife.Gait"/>). La capsule du
    /// corps est cachée. Sous-lot V2.4.1 (bêtes qui se promènent) : la tête tourne autour d'une nuque, qui se baisse pour brouter, se relève
    /// pour guetter ; un lièvre se ramasse entre deux bonds (<see cref="Wildlife.Pose"/>). Affichage seulement.
    /// </summary>
    public sealed class WildlifeLook
    {
        /// <summary>Un matériau par couleur, partagé par toutes les bêtes (jamais une copie par bête).</summary>
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        private readonly WildlifeKind _kind;
        private readonly Transform _root;
        private readonly Transform[] _legs = new Transform[5];
        private Transform _neck;
        private float _pitch;
        private float _crouch;
        private Vector3 _last;
        private bool _seen;
        private float _time;
        private float _speed;
        private float _stride;

        public WildlifeLook(GameObject body, WildlifeKind kind)
        {
            _kind = kind;
            Renderer own = body.GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            var root = new GameObject("Bête (formes simples)");
            _root = root.transform;
            _root.SetParent(body.transform, false);
            // Le corps est une capsule de 2 m de haut, centrée à 1 m du sol, à l'échelle de l'emprise : la bête garde ses mesures en mètres.
            Vector3 s = body.transform.localScale;
            _root.localScale = new Vector3(1f / Mathf.Max(0.01f, s.x), 1f / Mathf.Max(0.01f, s.y), 1f / Mathf.Max(0.01f, s.z));
            _root.localPosition = new Vector3(0f, -1f, 0f);
            // Sous-lot V2.4.1 : la nuque, pivot de la tête.
            var neck = new GameObject("Nuque");
            _neck = neck.transform;
            _neck.SetParent(_root, false);
            var n = Wildlife.Neck(kind);
            _neck.localPosition = new Vector3((float)n.X, (float)n.Y, (float)n.Z);
            foreach (WildlifePart part in Wildlife.Parts(kind)) Build(part);
        }

        private void Build(WildlifePart part)
        {
            GameObject piece = GameObject.CreatePrimitive(Primitive(part.Shape));
            piece.name = part.Name;
            Object.Destroy(piece.GetComponent<Collider>());
            Renderer renderer = piece.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialFor(new Color((float)part.R, (float)part.G, (float)part.B));
            var scale = new Vector3((float)part.ScaleX, (float)part.ScaleY, (float)part.ScaleZ);
            if (part.Leg > 0 && part.Leg < _legs.Length)
            {
                // Une patte tourne autour de la hanche (le haut de la pièce), pas de son milieu.
                var hip = new GameObject(part.Name + " (hanche)");
                hip.transform.SetParent(_root, false);
                hip.transform.localPosition = new Vector3((float)part.X, (float)(part.Y + part.ScaleY), (float)part.Z);
                piece.transform.SetParent(hip.transform, false);
                piece.transform.localPosition = new Vector3(0f, -(float)part.ScaleY, 0f);
                piece.transform.localScale = scale;
                _legs[part.Leg] = hip.transform;
                return;
            }
            piece.transform.SetParent(_root, false);
            piece.transform.localPosition = new Vector3((float)part.X, (float)part.Y, (float)part.Z);
            // Sous-lot V2.4.1 : une pièce de la tête suit la nuque (gardée à sa place, puis rattachée).
            if (Wildlife.IsHead(part.Name)) piece.transform.SetParent(_neck, true);
            piece.transform.localRotation = Quaternion.Euler((float)part.Pitch, 0f, 0f);
            piece.transform.localScale = scale;
        }

        private static PrimitiveType Primitive(WildlifeShape shape)
        {
            switch (shape)
            {
                case WildlifeShape.Capsule: return PrimitiveType.Capsule;
                case WildlifeShape.Cube: return PrimitiveType.Cube;
                case WildlifeShape.Cylinder: return PrimitiveType.Cylinder;
                default: return PrimitiveType.Sphere;
            }
        }

        private static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out Material m) && m != null) return m;
            m = ArenaView.MakeMaterial(color);
            Materials[color] = m;
            return m;
        }

        /// <summary>
        /// Allure de la bête à sa place affichée <paramref name="position"/> ; couchée sur le flanc, immobile, si elle est morte. Sous-lot
        /// V2.4.1 : <paramref name="wander"/> (<c>CombatantView.Wander</c>) donne sa posture : tête basse qui broute, tête haute qui guette.
        /// </summary>
        public void Update(Vector3 position, bool dead, float deltaTime, string wander = null)
        {
            if (_root == null) return;
            if (dead)
            {
                _root.localRotation = Quaternion.Euler(0f, 0f, 80f);
                return;
            }
            if (_seen && deltaTime > 0f)
            {
                Vector3 step = position - _last;
                step.y = 0f;
                // Vitesse lissée : les places glissent entre deux ticks du moteur.
                // Sous-lot V2.15 : départ en douceur, arrêt vif, quelle que soit la cadence d'images.
                _speed = (float)Locomotion.Ease(_speed, step.magnitude / deltaTime, 0.15, 0.06, deltaTime);
            }
            _last = position;
            _seen = true;
            _time += deltaTime;
            // Sous-lot V2.15 : l'horloge des foulées avance avec la vitesse (cadence de la course à pleine vitesse, plus lente en dessous) :
            // les pattes ne battent plus l'air au même rythme quand la bête ralentit.
            _stride += deltaTime * (float)Locomotion.PlaybackRate(_speed, _kind == WildlifeKind.Deer ? 6.0 : 7.0, 0.35, 1.3);
            (double bob, double[] legs) = Wildlife.Gait(_kind, _speed, _stride);
            // Sous-lot V2.4.1 : posture lissée (la tête descend vers l'herbe, se relève pour guetter).
            (double pitch, double crouch) = Wildlife.Pose(_kind, wander, _time);
            float ease = 1f - Mathf.Exp(-6f * deltaTime);
            _pitch = Mathf.Lerp(_pitch, (float)pitch, ease);
            _crouch = Mathf.Lerp(_crouch, (float)crouch, ease);
            if (_neck != null) _neck.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            _root.localPosition = new Vector3(0f, -1f + (float)bob - _crouch, 0f);   // le corps garde une hauteur d'échelle 1 (ArenaView.Fit)
            for (int i = 1; i < _legs.Length; i++)
                if (_legs[i] != null) _legs[i].localRotation = Quaternion.Euler((float)legs[i], 0f, 0f);
        }
    }
}
