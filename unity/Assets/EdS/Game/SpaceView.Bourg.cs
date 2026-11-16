using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// V2.30.2 : le Bourg d'Aube fortifié habillé des modèles de l'atelier (<see cref="BourgModels"/>) : palissade en modules, tours,
    /// portes ouvertes, ronde couverte aux portes, tours de guet. Un obstacle n'est habillé que si toutes ses pièces sont au catalogue
    /// (<see cref="StyleCatalog.OwnDecorPiece"/>) ; sinon la forme faite par le code reste. Affichage seulement.
    /// </summary>
    public sealed partial class SpaceView
    {
        /// <summary>Habillage du Bourg calculé sur les obstacles de l'espace (vide hors du Bourg).</summary>
        private BourgDress _bourg = BourgDress.Empty;

        /// <summary>Marge (m) autour de la fenêtre dans laquelle les pièces du Bourg sont posées.</summary>
        private const float BourgMargin = 40f;

        /// <summary>V2.30.2 : l'obstacle <paramref name="id"/> est-il habillé par des modèles du Bourg tous présents au catalogue ?</summary>
        private bool BourgCovers(string id)
        {
            if (_bourg == null || !_bourg.Needs.TryGetValue(id, out System.Collections.Generic.IReadOnlyList<string> needs)) return false;
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null) return false;
            foreach (string piece in needs)
                if (catalog.OwnDecorPiece(piece) == null) return false;
            return true;
        }

        /// <summary>
        /// V2.30.2 : pose les pièces du Bourg proches de la fenêtre, chacune sur le sol affiché (au plus bas sous son pied, V2.24), tournée
        /// vers l'extérieur ; les portes ont leurs vantaux rabattus vers la ville et la herse levée (<see cref="BourgModels.OpenParts"/>).
        /// Aucune collision (le moteur garde ses obstacles) ; elles s'effacent quand elles cachent le joueur.
        /// </summary>
        private void BourgDecor()
        {
            StyleCatalog catalog = StyleCatalog.Loaded;
            if (catalog == null || _bourg == null || _bourg.Pieces.Count == 0) return;
            float minX = Raw(_space.Min.X.Raw) - BourgMargin, maxX = Raw(_space.Max.X.Raw) + BourgMargin;
            float minZ = Raw(_space.Min.Z.Raw) - BourgMargin, maxZ = Raw(_space.Max.Z.Raw) + BourgMargin;
            var walled = new System.Collections.Generic.Dictionary<string, float>();
            foreach (BourgPiece p in _bourg.Near(minX, minZ, maxX, maxZ, 0))
            {
                if (p.Covers != null && !BourgCovers(p.Covers)) continue;
                GameObject prefab = catalog.OwnDecorPiece(p.Piece);
                if (prefab == null) continue;
                var holder = new GameObject(prefab.name + " (Bourg)");
                holder.transform.SetParent(_root.transform, false);
                float x = (float)p.X, z = (float)p.Z;
                holder.transform.position = new Vector3(x, Ground(x, z, (float)BourgModels.Foot(p.Piece)), z);
                holder.transform.rotation = Quaternion.Euler(0f, (float)p.Yaw, 0f);
                GameObject model = Object.Instantiate(prefab, holder.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                BourgOpen(holder.transform, model.transform, p.Piece);
                // Relecture indépendante : occulteurs pour les grandes pièces seulement (tours, portes, bâtiments).
                if (BourgModels.Occludes(p.Piece)) AddModelOccluders(model);
                // V2.30.3 : un bâtiment habillé arrête la caméra à son emprise (comme un modèle de l'atelier), jusqu'au haut de ses pièces.
                if (p.Covers != null && Blocks(TownRules.BuildingFor(p.Covers)) && TownRules.BuildingFor(p.Covers) != StyleBuilding.None)
                {
                    var box = BourgModels.Box(p.Piece);
                    float top = box != null ? (float)box.Value.Top : 8f;
                    walled[p.Covers] = walled.TryGetValue(p.Covers, out float seen) ? Mathf.Max(seen, top) : top;
                }
            }
            foreach (SpaceObstacle o in _space.Obstacles)
            {
                if (!walled.TryGetValue(o.Id, out float top)) continue;
                float x0 = Raw(o.Points.Min(q => q.X.Raw)), x1 = Raw(o.Points.Max(q => q.X.Raw));
                float z0 = Raw(o.Points.Min(q => q.Z.Raw)), z1 = Raw(o.Points.Max(q => q.Z.Raw));
                float ground = Ground((x0 + x1) / 2f, (z0 + z1) / 2f, 0.4f);
                _cameraBoxes.Add(new CameraBox(x0, ground, z0, x1, ground + top, z1));
            }
        }

        /// <summary>Vantaux tournés autour de leur charnière (repère du support), herse levée.</summary>
        private static void BourgOpen(Transform holder, Transform model, string piece)
        {
            foreach (BourgModels.OpenPart part in BourgModels.OpenParts(piece))
            {
                Transform t = FindChild(model, part.Name);
                if (t == null) continue;
                // Le support n'est tourné qu'autour de la verticale : sa verticale est celle du monde.
                if (part.Turn != 0)
                {
                    Vector3 hinge = holder.TransformPoint(new Vector3((float)part.X, 0f, (float)part.Z));
                    Quaternion turn = Quaternion.Euler(0f, (float)part.Turn, 0f);
                    t.position = hinge + turn * (t.position - hinge);
                    t.rotation = turn * t.rotation;
                }
                if (part.Lift != 0) t.position += Vector3.up * (float)part.Lift;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
