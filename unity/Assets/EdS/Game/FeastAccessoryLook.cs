using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.56.3 : l'accessoire de la fête d'Orsédia en cours (<see cref="FeastAccessories"/>) sur un habitant habillé : le modèle des fêtes
    /// (<see cref="StyleCatalog.FeastModel"/>, échelle réelle, avant vers +z) posé à sa place (visage, poitrine ou main droite) dans la
    /// pose du corps au moment où il est habillé, puis attaché à l'os : il suit l'animation. Rien sans modèle, sans catalogue ou sans l'os.
    /// </summary>
    public static class FeastAccessoryLook
    {
        /// <summary>Pose l'accessoire <paramref name="accessory"/> (<see cref="FeastAccessories.Worn"/>, null : rien) sur le corps <paramref name="model"/> ; faux si rien n'est posé.</summary>
        public static bool Attach(GameObject model, FeastAccessory accessory)
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            GameObject prefab = accessory != null && catalog != null ? catalog.FeastModel(accessory.Code) : null;
            if (prefab == null || model == null) return false;
            Transform bone = Bone(model.transform, accessory.Place);
            if (bone == null) return false;
            var holder = new GameObject("Accessoire de fête (" + accessory.Code + ")");
            GameObject copy = Object.Instantiate(prefab, holder.transform, false);
            foreach (Collider c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (!StyleLook.VisibleBounds(holder, out Bounds b))
            {
                Object.Destroy(holder);
                return false;
            }
            // Mesuré à l'origine du monde, sans rotation : le centre de ses rendus ramené sur le point d'attache.
            copy.transform.localPosition = -b.center;
            Vector3 forward = model.transform.forward, up = Vector3.up;   // le corps est debout quand on l'habille
            Vector3 at = bone.position;
            switch (accessory.Place)
            {
                case FeastAccessoryPlace.Face:
                    at += forward * (0.1f + b.size.z / 2f) + up * 0.04f;   // devant le visage, un peu au-dessus de l'os de la tête
                    break;
                case FeastAccessoryPlace.Chest:
                    at += forward * (0.13f + b.size.z / 2f) + up * 0.05f;   // sur le haut de la poitrine
                    break;
                default:
                    at -= up * (b.size.y / 2f + 0.02f);   // le plat porté à plat, sous la main comme un plateau
                    break;
            }
            holder.transform.position = at;
            holder.transform.rotation = Quaternion.LookRotation(forward);
            // L'échelle du monde est gardée (modèle en mètres), quelle que soit celle de l'os.
            holder.transform.SetParent(bone, true);
            return true;
        }

        /// <summary>L'os de la place <paramref name="place"/> (<see cref="FeastAccessories.IsBone"/>), ou null.</summary>
        private static Transform Bone(Transform root, FeastAccessoryPlace place)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                if (FeastAccessories.IsBone(t.name, place)) return t;
            return null;
        }
    }
}
