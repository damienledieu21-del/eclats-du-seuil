using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.12.7 (essai du 9 novembre 2026, PC modeste : « à la forge de Maëlle, le tonneau est un cylindre bleu uni, sans bois ni
    /// cercles ») : un tonneau posé par le jeu (le modèle de Poly Haven de la forge, celui du pack de village) n'est gardé que s'il a l'air
    /// de bois (<see cref="MaterialRepairRules.LooksLikeWood"/>) ; sinon, ou sans modèle, un tonneau de bois fait par le code : fût
    /// légèrement bombé en planches brunes, trois cercles de fer sombre et un fond. Même rendu en mode PC modeste.
    /// </summary>
    internal static class BarrelLook
    {
        private const float Height = 0.95f;
        private const float Width = 0.7f;

        /// <summary>Le modèle a-t-il l'air d'un tonneau de bois (<see cref="MaterialRepairRules.BarrelIsWood"/> : aucune matière bleue, au moins la moitié brunes ou texturées) ?</summary>
        internal static bool LooksWooden(GameObject barrel)
        {
            if (barrel == null) return false;
            var materials = new List<(double, double, double, bool)>();
            foreach (Renderer r in barrel.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials ?? new Material[0])
                {
                    if (m == null) return false;   // matière absente : sortirait rose
                    Color c = StyleLook.BaseColor(m);
                    bool textured = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
                    materials.Add((c.r, c.g, c.b, textured));
                }
            return MaterialRepairRules.BarrelIsWood(materials);
        }

        /// <summary>
        /// Rend <paramref name="placed"/> s'il a l'air de bois ; sinon le remplace par un tonneau fait par le code, au même endroit et dans le
        /// même sens. Null si <paramref name="placed"/> est null.
        /// </summary>
        internal static GameObject Ensure(GameObject placed)
        {
            if (placed == null || LooksWooden(placed)) return placed;
            Transform parent = placed.transform.parent;
            Vector3 at = placed.transform.position;
            float ground = StyleLook.VisibleBounds(placed, out Bounds b) ? b.min.y : at.y;
            GameObject made = Make(parent, placed.name, new Vector3(at.x, ground, at.z), placed.transform.eulerAngles.y);
            Object.Destroy(placed);
            return made;
        }

        /// <summary>Tonneau de bois à cercles sombres posé au sol en <paramref name="at"/> (y : le sol).</summary>
        internal static GameObject Make(Transform parent, string id, Vector3 at, float yaw)
        {
            var holder = new GameObject(id + " (tonneau de bois)");
            holder.transform.SetParent(parent, false);
            holder.transform.position = at;
            holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Material wood = Shared(0), iron = Shared(1), lid = Shared(2);
            holder.AddComponent<Owner>();   // libère les matières partagées quand le dernier tonneau disparaît
            // Fût : trois tranches de planches, celle du milieu plus large (le ventre du tonneau).
            Part(holder.transform, "Fût bas", 0.2f, 0.42f, Width * 0.9f, wood);
            Part(holder.transform, "Fût milieu", 0.475f, 0.5f, Width, wood);
            Part(holder.transform, "Fût haut", 0.75f, 0.42f, Width * 0.9f, wood);
            // Cercles de fer : deux aux extrémités, un au ventre.
            foreach (float y in new[] { 0.1f, 0.475f, 0.85f })
                Part(holder.transform, "Cercle", y, 0.05f, Width * (y == 0.475f ? 1.05f : 0.95f), iron);
            // Fond du dessus : planches plus sombres.
            Part(holder.transform, "Dessus", Height - 0.02f, 0.03f, Width * 0.84f, lid);
            return holder;
        }

        // Matières partagées par tous les tonneaux (bois, fer, dessus), comptées par tonneau vivant (Owner) et détruites avec le dernier.
        private static readonly Material[] _shared = new Material[3];
        private static int _owners;

        private static Material Shared(int kind)
        {
            if (_shared[kind] == null)
            {
                Color color = kind == 0 ? new Color(0.42f, 0.28f, 0.16f) : kind == 1 ? new Color(0.13f, 0.13f, 0.14f) : new Color(0.3f, 0.2f, 0.12f);
                Material m = ArenaView.MakeMaterial(color);
                if (kind == 0 && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
                if (kind == 1 && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
                _shared[kind] = m;
            }
            return _shared[kind];
        }

        /// <summary>Un tonneau vivant : à sa destruction, le dernier libère les matières partagées.</summary>
        private sealed class Owner : MonoBehaviour
        {
            private void Awake() => _owners++;

            private void OnDestroy()
            {
                if (--_owners > 0) return;
                _owners = 0;
                for (int i = 0; i < _shared.Length; i++)
                {
                    if (_shared[i] != null) Object.Destroy(_shared[i]);
                    _shared[i] = null;
                }
            }
        }

        private static void Part(Transform parent, string name, float y, float height, float width, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(0f, y, 0f);
            part.transform.localScale = new Vector3(width, height / 2f, width);   // le cylindre de Unity fait 2 m de haut
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
