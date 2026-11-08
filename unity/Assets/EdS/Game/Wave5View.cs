using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Sous-lot V2.11 (5e vague de ressources) : pose les modèles de la liste partagée <see cref="Wave5"/> (four de Firmin, menhirs, cristaux,
    /// épouvantail…) et leur applique les réglages notés à la conversion (transparence découpée, lueur, teinte des modèles sans texture,
    /// matériau des scans), sur les copies des matériaux. Sans catalogue ou sans le fichier : rien n'est posé (null), le jeu garde ce qu'il
    /// montrait. Affichage seulement.
    /// </summary>
    public static class Wave5View
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>
        /// Pose le modèle de <paramref name="role"/> au sol en <paramref name="at"/>, tourné de <paramref name="yaw"/> degrés, à la taille voulue
        /// (<paramref name="size"/> m de haut si positif, sinon celle de la liste, sinon sa taille réelle) multipliée par l'échelle du catalogue.
        /// <paramref name="variant"/> : pour un rôle à plusieurs fichiers posés ensemble (les pains), le rang voulu parmi ceux trouvés.
        /// </summary>
        public static GameObject Place(Transform parent, Wave5Role role, string id, Vector3 at, float yaw, float size = 0f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            Wave5Look look = catalog != null ? catalog.Look(role) : null;
            if (look == null) return null;
            Wave5Entry entry = Wave5.Entry(role);
            var holder = new GameObject(id + " (V2.11)");
            GameObject model = Object.Instantiate(look.Model, holder.transform, false);
            foreach (Collider c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (SkinnedMeshRenderer skin in holder.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
            Fix(holder, look.Model.name, look.Material);
            holder.transform.SetParent(parent, false);
            float height = size > 0f ? size : (float)entry.Height;
            float k = 1f;
            if (height > 0f && StyleLook.VisibleBounds(holder, out Bounds b) && b.size.y > 0.01f) k = height / b.size.y;
            holder.transform.localScale = Vector3.one * k * (look.Scale > 0f ? look.Scale : 1f);
            holder.transform.rotation = Quaternion.Euler(0f, yaw + look.Yaw, 0f);
            holder.transform.position = at;
            if (!entry.Has(Wave5Fix.Floats)) StyleLook.Ground(holder, at.y);
            Loop(holder);
            return holder;
        }

        /// <summary>Le modèle a-t-il été déposé (et se montre-t-il sur cette plateforme) ?</summary>
        public static bool Has(Wave5Role role) => StyleCatalog.Loaded != null && StyleCatalog.Loaded.Look(role) != null;

        /// <summary>Une animation « Legacy » du modèle (feu follet, attente) tourne en boucle, à partir d'un instant varié.</summary>
        private static void Loop(GameObject holder)
        {
            Animation animation = holder.GetComponentInChildren<Animation>();
            if (animation == null) return;
            foreach (AnimationState state in animation)
            {
                state.wrapMode = WrapMode.Loop;
                animation.Play(state.name);
                state.time = state.length * StyleRules.Variant(holder.name, 100) / 100f;
                break;
            }
        }

        /// <summary>
        /// Réglages de la conversion pour le modèle <paramref name="modelName"/> (<see cref="Wave5.FixesFor"/>), sur les copies des matériaux de
        /// <paramref name="root"/> ; <paramref name="scanned"/> : matériau d'un scan, posé sur tout le modèle. Rien pour un autre modèle.
        /// </summary>
        public static void Fix(GameObject root, string modelName, Material scanned)
        {
            if (root == null) return;
            Wave5Fix fix = Wave5.FixesFor(modelName);
            if (scanned != null)
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    Material[] shared = r.sharedMaterials;
                    var materials = new Material[shared.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = scanned;
                    r.sharedMaterials = materials;
                }
            if ((fix & (Wave5Fix.AlphaClip | Wave5Fix.Emissive | Wave5Fix.Untextured)) == 0) return;
            (double tr, double tg, double tb) = Wave5.TintFor(modelName);
            var tint = new Color((float)tr, (float)tg, (float)tb);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                {
                    if (m == null) continue;
                    Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    if ((fix & Wave5Fix.Untextured) != 0 && baseMap == null) ArenaView.ApplyColor(m, tint);
                    if ((fix & Wave5Fix.AlphaClip) != 0 && baseMap != null)
                    {
                        // Plumes, brins et toiles : transparence découpée (URP Lit « Alpha Clipping »), sans tri ni ombre ratée.
                        m.SetFloat("_AlphaClip", 1f);
                        m.SetFloat("_Cutoff", 0.45f);
                        m.EnableKeyword("_ALPHATEST_ON");
                        m.renderQueue = 2450;
                        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // les deux faces d'une plume
                    }
                    if ((fix & Wave5Fix.Emissive) != 0)
                    {
                        // Runes, cristaux, portail : l'émission du FBX si elle est là, sinon la couleur elle-même ; le Bloom d'URP fait le halo.
                        Texture emission = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
                        m.EnableKeyword("_EMISSION");
                        if (emission == null && baseMap != null) m.SetTexture("_EmissionMap", baseMap);
                        Color c = StyleLook.BaseColor(m);
                        Color glow = emission != null ? new Color(1.6f, 1.6f, 1.8f) : new Color(c.r * 1.2f, c.g * 1.2f, c.b * 1.4f);
                        if ((fix & Wave5Fix.Untextured) != 0 && baseMap == null) glow = tint * 1.6f;
                        m.SetColor("_EmissionColor", glow);
                    }
                }
        }

        /// <summary>Points des obstacles de <paramref name="obstacles"/> (pour <see cref="Wave5.FreeSpot"/>).</summary>
        public static List<IReadOnlyList<(double X, double Z)>> Polygons(IEnumerable<EdS.Domain.Space.SpaceObstacle> obstacles)
        {
            var list = new List<IReadOnlyList<(double X, double Z)>>();
            if (obstacles == null) return list;
            foreach (EdS.Domain.Space.SpaceObstacle o in obstacles)
            {
                var points = new List<(double X, double Z)>();
                foreach (var p in o.Points) points.Add((p.X.Raw / 10000.0, p.Z.Raw / 10000.0));
                list.Add(points);
            }
            return list;
        }

        /// <summary>
        /// Pose <paramref name="role"/> près de (<paramref name="x"/>, <paramref name="z"/>) dans la direction <paramref name="yaw"/>, à la
        /// première place libre (à <paramref name="clearance"/> m des obstacles) ; null s'il n'y en a pas ou sans modèle.
        /// </summary>
        public static GameObject PlaceNear(Transform parent, Wave5Role role, string id, double x, double z, double yaw, double distance, double clearance,
            List<IReadOnlyList<(double X, double Z)>> obstacles, float facing, float size = 0f)
        {
            if (!Has(role)) return null;
            if (!(Wave5.FreeSpot(Wave5.Around(x, z, yaw, distance), obstacles, clearance) is (double X, double Z) spot)) return null;
            return Place(parent, role, id, new Vector3((float)spot.X, 0f, (float)spot.Z), facing, size);
        }
    }
}
