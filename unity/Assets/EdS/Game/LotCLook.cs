using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Lot C (16 novembre 2026) : pose d'un modèle Tripo du lot C (<see cref="LotCModels"/>) : copie sans collision, tournée, à l'échelle
    /// voulue (les modèles sont à l'échelle réelle, en mètres), le bas de ses rendus posé à la hauteur demandée.
    /// </summary>
    public static class LotCLook
    {
        /// <summary>Copie du modèle <paramref name="code"/> sous <paramref name="parent"/>, ou null sans catalogue ou sans ce modèle.</summary>
        public static GameObject Place(Transform parent, string code, string id, Vector3 at, float yaw, float scale = 1f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject source = catalog != null ? catalog.LotCModel(code) : null;
            if (source == null) return null;
            var look = new GameObject(id + " (" + code + ")");
            Object.Instantiate(source, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.SetParent(parent, false);
            look.transform.localScale = Vector3.one * scale;
            look.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            look.transform.position = at;
            if (StyleLook.VisibleBounds(look, out Bounds b)) look.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            return look;
        }

        /// <summary>
        /// Relecture de la PR #197 : « une seule variante » (<see cref="StyleLook.Prop"/>) pour un modèle du lot C : s'il a plusieurs rendus,
        /// un seul reste, tiré de <paramref name="id"/>. Un export Tripo n'a qu'un maillage : rien ne change alors.
        /// </summary>
        public static void OneVariant(GameObject look, string id)
        {
            Renderer[] renderers = look.GetComponentsInChildren<Renderer>();
            if (renderers.Length < 2) return;
            int keep = StyleRules.Variant(id ?? string.Empty, renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
                if (i != keep) renderers[i].enabled = false;
        }
    }
}
