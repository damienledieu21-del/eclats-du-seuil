using System.Collections.Generic;
using EdS.Data.Items;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V1.2 (« sur le Bastion, on voit l'arme, le bouclier et la cape ») : montre l'équipement visible du joueur (<see cref="VisibleGear"/>).
    /// Avec le paladin de Mixamo, son épée et son bouclier (dans le modèle) restent et prennent la teinte de la rareté ; une arme d'une autre
    /// forme (masse, hache, marteau) cache l'épée et prend une forme simple dans la main droite. Sans modèle, l'arme et le bouclier sont des
    /// formes simples. La cape est toujours une forme simple dans le dos : aucun modèle de cape n'est dans les packs déposés (et aucun achat
    /// sans l'accord de Damien). Affichage seulement : rien ne revient vers le moteur.
    /// </summary>
    public sealed class GearLook
    {
        private readonly Transform _body;
        private readonly bool _styled;
        private readonly List<GameObject> _shapes = new List<GameObject>();
        private readonly List<Renderer> _sword = new List<Renderer>();
        private readonly List<Renderer> _shield = new List<Renderer>();
        private readonly Transform _hand;
        private readonly Transform _back;
        private VisibleGear _shown;

        /// <summary>
        /// Relecture de la PR #75 : couleurs de base des matériaux de l'épée et du bouclier du modèle, relevées une fois, pour les remettre
        /// quand l'objet porté redevient commun (sinon la teinte d'un objet rare restait).
        /// </summary>
        private readonly Dictionary<Renderer, Color[]> _baseColors = new Dictionary<Renderer, Color[]>();

        public GearLook(Transform body, bool styled)
        {
            _body = body;
            _styled = styled;
            if (!styled) return;
            // Les pièces du paladin « W/Prop » et ses os, reconnus par leur nom (Mixamo : « mixamorig:RightHand », « mixamorig:Spine2 »).
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                string n = (r.name ?? "").ToLowerInvariant();
                if (n.Contains("sword")) _sword.Add(r);
                else if (n.Contains("shield")) _shield.Add(r);
                else continue;
                // « materials » donne les copies propres à cette pièce (créées au premier appel, les mêmes ensuite) : on les teinte et on
                // garde leur couleur d'origine.
                Material[] materials = r.materials;
                var colors = new Color[materials.Length];
                for (int i = 0; i < materials.Length; i++) colors[i] = BaseColor(materials[i]);
                _baseColors[r] = colors;
            }
            foreach (Transform t in body.GetComponentsInChildren<Transform>())
            {
                string n = t.name ?? "";
                // Sous-lot V2.11 : aussi les squelettes de Quaternius (« hand.r », « Hand_R »), pour l'arme en main des corps humains.
                string low = n.ToLowerInvariant();
                if (_hand == null && (n.EndsWith("RightHand") || low == "hand.r" || low == "hand_r" || low.EndsWith(":hand_r"))) _hand = t;
                if (_back == null && (n.EndsWith("Spine2") || low == "chest" || low == "spine_02" || low == "spine.002")) _back = t;
            }
        }

        /// <summary>Montre <paramref name="gear"/> (rien ne se refait si rien n'a changé).</summary>
        public void Show(VisibleGear gear)
        {
            if (gear == null || gear.Equals(_shown)) return;
            _shown = gear;
            foreach (GameObject shape in _shapes) Object.Destroy(shape);
            _shapes.Clear();
            bool sword = gear.Weapon == WeaponLook.Sword;
            foreach (Renderer r in _sword)
            {
                r.enabled = sword;
                Tint(r, gear.WeaponRarity);
            }
            foreach (Renderer r in _shield)
            {
                r.enabled = gear.Shield != ShieldLook.None;
                Tint(r, gear.ShieldRarity);
            }
            if (gear.Weapon != WeaponLook.None && (!_styled || !sword || _sword.Count == 0)) Weapon(gear.Weapon, gear.WeaponRarity);
            if (gear.Shield != ShieldLook.None && (!_styled || _shield.Count == 0)) Shield(gear.Shield, gear.ShieldRarity);
            if (gear.Cloak) Cloak(gear.CloakRarity);
        }

        /// <summary>Teinte de la rareté sur une pièce du modèle ; commune : ses couleurs d'origine remises (relecture de la PR #75).</summary>
        private void Tint(Renderer r, ItemRarity rarity)
        {
            if (!_baseColors.TryGetValue(r, out Color[] colors)) return;
            Material[] materials = r.materials;
            Color tint = Color.Lerp(Color.white, AdventureBook.RarityColor(rarity), 0.45f);
            for (int i = 0; i < materials.Length && i < colors.Length; i++)
                if (materials[i] != null) ArenaView.ApplyColor(materials[i], rarity == ItemRarity.Common ? colors[i] : tint);
        }

        /// <summary>Couleur de base d'un matériau (celle que <see cref="ArenaView.ApplyColor"/> remplace), blanc sans couleur.</summary>
        private static Color BaseColor(Material m) =>
            m == null ? Color.white : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        private GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Color color)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            Object.Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localRotation = Quaternion.Euler(euler.x, euler.y, euler.z);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = ArenaView.MakeMaterial(color);
            _shapes.Add(shape);
            return shape;
        }

        private void Weapon(WeaponLook look, ItemRarity rarity)
        {
            // Sous-lot V2.11 (5e vague) : la vraie masse, la hache ou l'épée (le marteau : celui de la forge), sinon les formes simples.
            if (WeaponModel(look, rarity)) return;
            Color metal = Color.Lerp(new Color(0.72f, 0.74f, 0.78f), AdventureBook.RarityColor(rarity), 0.35f), wood = new Color(0.42f, 0.28f, 0.16f);
            // Dans la main droite du modèle, sinon sur le flanc droit du corps simple (capsule de 2 m centrée à 1 m).
            Transform parent = _styled && _hand != null ? _hand : _body;
            Vector3 at = _styled && _hand != null ? Vector3.zero : new Vector3(0.55f, -0.1f, 0.15f);
            float k = _styled && _hand != null ? 1f / Mathf.Max(0.01f, _hand.lossyScale.y) : 1f;
            GameObject handle = Shape("Manche", PrimitiveType.Cylinder, parent, at, Vector3.zero, new Vector3(0.05f, 0.45f, 0.05f) * k, wood);
            Vector3 top = at + new Vector3(0f, 0.45f, 0f) * k;
            switch (look)
            {
                case WeaponLook.Mace:
                    Shape("Tête de masse", PrimitiveType.Sphere, parent, top, Vector3.zero, new Vector3(0.18f, 0.18f, 0.18f) * k, metal);
                    break;
                case WeaponLook.Axe:
                    Shape("Fer de hache", PrimitiveType.Cube, parent, top + new Vector3(0.1f, 0f, 0f) * k, Vector3.zero, new Vector3(0.22f, 0.16f, 0.03f) * k, metal);
                    break;
                case WeaponLook.Hammer:
                    Shape("Tête de marteau", PrimitiveType.Cube, parent, top, Vector3.zero, new Vector3(0.3f, 0.14f, 0.14f) * k, metal);
                    break;
                default:
                    Object.Destroy(handle);
                    _shapes.Remove(handle);
                    Shape("Lame", PrimitiveType.Cube, parent, at + new Vector3(0f, 0.45f, 0f) * k, Vector3.zero, new Vector3(0.06f, 0.9f, 0.02f) * k, metal);
                    Shape("Garde", PrimitiveType.Cube, parent, at, Vector3.zero, new Vector3(0.22f, 0.04f, 0.04f) * k, wood);
                    break;
            }
        }

        private void Shield(ShieldLook look, ItemRarity rarity)
        {
            Color face = Color.Lerp(new Color(0.45f, 0.3f, 0.18f), AdventureBook.RarityColor(rarity), 0.4f);
            // Au bras gauche du corps simple.
            Vector3 at = new Vector3(-0.5f, 0f, 0.25f);
            switch (look)
            {
                case ShieldLook.Round:
                    Shape("Rondache", PrimitiveType.Cylinder, _body, at, new Vector3(0f, 0f, 90f), new Vector3(0.6f, 0.03f, 0.6f), face);
                    break;
                case ShieldLook.Tower:
                    Shape("Pavois", PrimitiveType.Cube, _body, at, Vector3.zero, new Vector3(0.05f, 1.1f, 0.6f), face);
                    break;
                default:
                    Shape("Écu", PrimitiveType.Cube, _body, at, Vector3.zero, new Vector3(0.05f, 0.75f, 0.55f), face);
                    break;
            }
        }

        /// <summary>
        /// Sous-lot V2.11 : le modèle de l'arme <paramref name="look"/> dans la main : son grand axe (mesuré par la préparation) dressé, la tête
        /// en haut (l'épée, garde en bas), à sa longueur, tenu au quart de sa longueur ; teinté de la rareté. Faux sans modèle.
        /// </summary>
        private bool WeaponModel(WeaponLook look, ItemRarity rarity)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return false;
            GameObject prefab = null;
            int axis = 1, sign = 1;
            float extraYaw = 0f, scale = 1f;
            // Relecture de la PR #121 : deux haches déposées (hache et hachette de Poly Haven) : l'une ou l'autre selon la rareté portée.
            List<Wave5Look> choices = Wave5.WeaponRole(look) is Wave5Role wanted ? catalog.Looks(wanted) : new List<Wave5Look>();
            if (choices.Count > 0 && choices[(int)rarity % choices.Count] is Wave5Look w)
            {
                prefab = w.Model;
                axis = w.GripAxis;
                sign = w.GripSign;
                extraYaw = w.Yaw;
                scale = w.Scale > 0f ? w.Scale : 1f;
            }
            else if (look == WeaponLook.Hammer && catalog.Prop(StyleProp.Hammer) is PropLook hammer)
            {
                prefab = hammer.Model;
                axis = -1;   // mesuré ici
            }
            if (prefab == null) return false;
            Transform parent = _styled && _hand != null ? _hand : _body;
            // Mesurée à l'origine du monde, sans rotation (la boîte du monde est alors celle du modèle), puis tenue en main.
            var holder = new GameObject("Arme (V2.11)");
            GameObject model = Object.Instantiate(prefab, holder.transform, false);
            foreach (Collider c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (!StyleLook.VisibleBounds(holder, out Bounds world) || world.size.sqrMagnitude < 1e-6f)
            {
                Object.Destroy(holder);
                return false;
            }
            Vector3 size = world.size;
            if (axis < 0) axis = Wave5.LongAxis(size.x, size.y, size.z);
            Vector3 along = axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up;
            Vector3 center = world.center;
            float length = axis == 0 ? size.x : axis == 2 ? size.z : size.y;
            // Le grand axe vers le haut du support, la tête en haut ; puis le support tenu dans la main.
            Quaternion stand = Quaternion.FromToRotation(along * (sign >= 0 ? 1f : -1f), Vector3.up);
            model.transform.localRotation = stand * Quaternion.Euler(0f, extraYaw, 0f);
            model.transform.localPosition = -(stand * center) + Vector3.up * (length * 0.25f);
            float k = (float)Wave5.WeaponLength(look) * scale / Mathf.Max(0.01f, length);
            float parentScale = _styled && _hand != null ? Mathf.Max(0.01f, _hand.lossyScale.y) : 1f;
            holder.transform.SetParent(parent, false);
            holder.transform.localScale = Vector3.one * (k / parentScale);
            holder.transform.localPosition = _styled && _hand != null ? Vector3.zero : new Vector3(0.55f, -0.35f, 0.15f);
            if (rarity != ItemRarity.Common)
            {
                Color tint = Color.Lerp(Color.white, AdventureBook.RarityColor(rarity), 0.35f);
                foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
                    foreach (Material m in Wave5View.Copies(r, holder)) if (m != null) ArenaView.ApplyColor(m, StyleLook.BaseColor(m) * tint);
            }
            _shapes.Add(holder);
            return true;
        }

        /// <summary>Sous-lot V2.11 : la demi-cape (5e vague) dans le dos, à la largeur des épaules ; faux sans elle (ou sur téléphone).</summary>
        private bool CloakModel(ItemRarity rarity)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            Wave5Look cape = catalog != null ? catalog.Look(Wave5Role.HalfCape) : null;
            if (cape == null) return false;
            Transform parent = _styled && _back != null ? _back : _body;
            var holder = new GameObject("Cape (V2.11)");
            Object.Instantiate(cape.Model, holder.transform, false);
            foreach (Collider c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (!StyleLook.VisibleBounds(holder, out Bounds b) || b.size.x < 0.01f)
            {
                Object.Destroy(holder);
                return false;
            }
            holder.transform.SetParent(parent, false);
            float parentScale = Mathf.Max(0.01f, parent.lossyScale.y);
            float k = (float)Wave5.Entry(Wave5Role.HalfCape).Height * (cape.Scale > 0f ? cape.Scale : 1f) / Mathf.Max(b.size.x, b.size.z);
            holder.transform.localScale = Vector3.one * (k / parentScale);
            holder.transform.localRotation = Quaternion.Euler(0f, 180f + cape.Yaw, 0f);
            holder.transform.localPosition = _styled && _back != null ? new Vector3(0f, -0.1f, -0.12f) / parentScale : new Vector3(0f, 0.25f, -0.3f);
            if (rarity != ItemRarity.Common)
                foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
                    foreach (Material m in Wave5View.Copies(r, holder)) if (m != null) ArenaView.ApplyColor(m, Color.Lerp(StyleLook.BaseColor(m), AdventureBook.RarityColor(rarity), 0.45f));
            _shapes.Add(holder);
            return true;
        }

        private void Cloak(ItemRarity rarity)
        {
            if (CloakModel(rarity)) return;
            Color cloth = rarity == ItemRarity.Common ? new Color(0.36f, 0.12f, 0.1f) : Color.Lerp(new Color(0.2f, 0.12f, 0.1f), AdventureBook.RarityColor(rarity), 0.55f);
            if (_styled && _back != null)
            {
                float k = 1f / Mathf.Max(0.01f, _back.lossyScale.y);
                Shape("Cape", PrimitiveType.Cube, _back, new Vector3(0f, -0.45f, -0.16f) * k, new Vector3(-6f, 0f, 0f), new Vector3(0.5f, 1.1f, 0.03f) * k, cloth);
            }
            else Shape("Cape", PrimitiveType.Cube, _body, new Vector3(0f, -0.05f, -0.42f), new Vector3(-6f, 0f, 0f), new Vector3(0.7f, 1.3f, 0.04f), cloth);
        }

        /// <summary>Retire les formes ajoutées (le corps est détruit avec elles sinon).</summary>
        public void Clear()
        {
            foreach (GameObject shape in _shapes) Object.Destroy(shape);
            _shapes.Clear();
            _shown = null;
        }
    }
}
