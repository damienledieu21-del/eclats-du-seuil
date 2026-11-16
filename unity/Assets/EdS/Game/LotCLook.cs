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
        public static GameObject Place(Transform parent, string code, string id, Vector3 at, float yaw, float scale = 1f, float thinX = 1f, float thinZ = 1f)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject source = catalog != null ? catalog.LotCModel(code) : null;
            if (source == null) return null;
            var look = new GameObject(id + " (" + code + ")");
            GameObject model = Object.Instantiate(source, look.transform, false);
            // Lot C (intérieurs) : petit côté aminci pour tenir dans l'obstacle du meuble (LotCModels.Fit), avant la rotation.
            if (thinX != 1f || thinZ != 1f) {
                Vector3 own = model.transform.localScale;
                model.transform.localScale = new Vector3(own.x * thinX, own.y, own.z * thinZ);
            }
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.SetParent(parent, false);
            look.transform.localScale = Vector3.one * scale;
            look.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            look.transform.position = at;
            if (StyleLook.VisibleBounds(look, out Bounds b)) look.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            return look;
        }
    }
}
