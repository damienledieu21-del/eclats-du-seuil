using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Lieux reconstruits, sous-lot V2.43.3 : le décor des chantiers suit l'état de chaque lieu (<see cref="RebuildDecor"/>,
    /// <see cref="AdventureSession.RebuildSpots"/>) : échafaudage fait par le code, tas de bois, chariot, éclats et outils pendant le
    /// chantier ; chariot chargé, caisse et tonneau une fois reconstruit. L'état est relu deux fois par seconde ; le décor est refait quand
    /// il change (un palier fini, un lieu reconstruit). Affichage seulement, sans collision.
    /// </summary>
    public sealed partial class SpaceView
    {
        private GameObject _rebuildRoot;
        private Material _scaffoldWood, _scaffoldPlank;   // relecture indépendante de la PR #225 : une fois par vue, rendus avec elle
        private string _rebuildKey;
        private float _rebuildPoll;

        /// <summary>Marge (m) autour de la fenêtre dans laquelle le décor des chantiers est posé.</summary>
        private const float RebuildMargin = 40f;

        public void Rebuilds(AdventureSession adventure, float deltaTime)
        {
            if (adventure == null || _root == null || _indoor) return;   // relecture de la PR #225 : jamais en intérieur
            _rebuildPoll -= deltaTime;
            if (_rebuildPoll > 0f && _rebuildKey != null) return;
            _rebuildPoll = 0.5f;
            string key = adventure.RebuildDecorKey;
            if (key == _rebuildKey && _rebuildRoot != null) return;
            _rebuildKey = key;
            if (_rebuildRoot != null) Object.Destroy(_rebuildRoot);
            _rebuildRoot = new GameObject("Chantiers");
            _rebuildRoot.transform.SetParent(_root.transform, false);
            if (key.Length == 0) return;
            StyleCatalog catalog = StyleCatalog.Loaded;
            float minX = Raw(_space.Min.X.Raw) - RebuildMargin, maxX = Raw(_space.Max.X.Raw) + RebuildMargin;
            float minZ = Raw(_space.Min.Z.Raw) - RebuildMargin, maxZ = Raw(_space.Max.Z.Raw) + RebuildMargin;
            foreach (DecorSpot spot in adventure.RebuildSpots())
            {
                float x = (float)spot.X, z = (float)spot.Z;
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;
                if (spot.Piece == RebuildDecor.Scaffold) { ScaffoldAt(x, z, (float)spot.Yaw); continue; }
                GameObject prefab = catalog != null ? catalog.OwnDecorPiece(spot.Piece) : null;
                if (prefab == null) continue;   // modèle absent du catalogue : rien
                var holder = new GameObject(prefab.name + " (chantier)");
                holder.transform.SetParent(_rebuildRoot.transform, false);
                holder.transform.position = new Vector3(x, Ground(x, z, (float)RebuildDecor.Radius(spot.Piece)), z);
                holder.transform.rotation = Quaternion.Euler(0f, (float)spot.Yaw, 0f);
                GameObject model = Object.Instantiate(prefab, holder.transform, false);
                foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
        }

        /// <summary>
        /// Échafaudage fait par le code (provisoire, faute de modèle) : quatre perches de 5 m, deux planchers à 1,8 m et 3,6 m, une
        /// échelle de deux montants, en bois brut ; 4 × 2 m au sol.
        /// </summary>
        private void ScaffoldAt(float x, float z, float yaw)
        {
            var holder = new GameObject("Échafaudage (chantier)");
            holder.transform.SetParent(_rebuildRoot.transform, false);
            holder.transform.position = new Vector3(x, Ground(x, z, 2.2f), z);
            holder.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_scaffoldWood == null) _scaffoldWood = ArenaView.MakeMaterial(new Color(0.52f, 0.38f, 0.24f));
            if (_scaffoldPlank == null) _scaffoldPlank = ArenaView.MakeMaterial(new Color(0.62f, 0.48f, 0.31f));
            Material wood = _scaffoldWood, plank = _scaffoldPlank;
            var parts = new List<(Vector3 Center, Vector3 Size, Material Mat)>();
            foreach (float px in new[] { -2f, 2f })
                foreach (float pz in new[] { -1f, 1f })
                    parts.Add((new Vector3(px, 2.5f, pz), new Vector3(0.12f, 5f, 0.12f), wood));
            foreach (float h in new[] { 1.8f, 3.6f }) parts.Add((new Vector3(0f, h, 0f), new Vector3(4.2f, 0.08f, 2.1f), plank));
            foreach (float px in new[] { 2.3f, 2.7f }) parts.Add((new Vector3(px, 1.8f, -1.1f), new Vector3(0.07f, 3.6f, 0.07f), wood));
            for (float h = 0.4f; h < 3.6f; h += 0.45f) parts.Add((new Vector3(2.5f, h, -1.1f), new Vector3(0.45f, 0.05f, 0.05f), wood));
            foreach ((Vector3 center, Vector3 size, Material mat) in parts)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(holder.transform, false);
                p.transform.localPosition = center;
                p.transform.localScale = size;
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        /// <summary>Relecture indépendante de la PR #225 : les matériaux de l'échafaudage, rendus avec la vue (<see cref="Destroy"/>).</summary>
        private void ReleaseRebuilds()
        {
            if (_scaffoldWood != null) Object.Destroy(_scaffoldWood);
            if (_scaffoldPlank != null) Object.Destroy(_scaffoldPlank);
            _scaffoldWood = _scaffoldPlank = null;
        }
    }
}
