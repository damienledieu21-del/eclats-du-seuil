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
            }
            foreach (Transform t in body.GetComponentsInChildren<Transform>())
            {
                string n = t.name ?? "";
                if (_hand == null && n.EndsWith("RightHand")) _hand = t;
                if (_back == null && n.EndsWith("Spine2")) _back = t;
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

        private static void Tint(Renderer r, ItemRarity rarity)
        {
            if (rarity == ItemRarity.Common) return;   // les couleurs du modèle
            foreach (Material m in r.materials) ArenaView.ApplyColor(m, Color.Lerp(Color.white, AdventureBook.RarityColor(rarity), 0.45f));
        }

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

        private void Cloak(ItemRarity rarity)
        {
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
