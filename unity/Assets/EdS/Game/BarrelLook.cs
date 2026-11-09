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

        /// <summary>Le modèle a-t-il l'air d'un tonneau de bois (une matière brune ou à texture, aucune bleue) ?</summary>
        internal static bool LooksWooden(GameObject barrel)
        {
            if (barrel == null) return false;
            bool any = false;
            foreach (Renderer r in barrel.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials ?? new Material[0])
                {
                    if (m == null) return false;   // matière absente : sortirait rose
                    Color c = StyleLook.BaseColor(m);
                    bool textured = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
                    if (MaterialRepairRules.LooksLikeWood(c.r, c.g, c.b, textured)) any = true;
                }
            return any;
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
            Material wood = ArenaView.MakeMaterial(new Color(0.42f, 0.28f, 0.16f));
            if (wood.HasProperty("_Smoothness")) wood.SetFloat("_Smoothness", 0.2f);
            Material iron = ArenaView.MakeMaterial(new Color(0.13f, 0.13f, 0.14f));
            if (iron.HasProperty("_Metallic")) iron.SetFloat("_Metallic", 0.6f);
            // Fût : trois tranches de planches, celle du milieu plus large (le ventre du tonneau).
            Part(holder.transform, "Fût bas", 0.2f, 0.42f, Width * 0.9f, wood);
            Part(holder.transform, "Fût milieu", 0.475f, 0.5f, Width, wood);
            Part(holder.transform, "Fût haut", 0.75f, 0.42f, Width * 0.9f, wood);
            // Cercles de fer : deux aux extrémités, un au ventre.
            foreach (float y in new[] { 0.1f, 0.475f, 0.85f })
                Part(holder.transform, "Cercle", y, 0.05f, Width * (y == 0.475f ? 1.05f : 0.95f), iron);
            // Fond du dessus : planches plus sombres.
            Material lid = ArenaView.MakeMaterial(new Color(0.3f, 0.2f, 0.12f));
            Part(holder.transform, "Dessus", Height - 0.02f, 0.03f, Width * 0.84f, lid);
            return holder;
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
