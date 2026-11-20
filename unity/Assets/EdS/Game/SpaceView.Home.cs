using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>V2.53.3 : l'aperçu du meuble que l'on pose dans la maison (mode décoration) : où il irait, et si la pose est possible.</summary>
    public readonly struct HomeGhost
    {
        public HomeGhost(HomePieceLook look, bool ok)
        {
            Look = look;
            Ok = ok;
        }

        public HomePieceLook Look { get; }
        public bool Ok { get; }
    }

    /// <summary>
    /// Maison du joueur, sous-lot V2.53.3 : l'intérieur de la maison est dessiné d'après les meubles posés (<see cref="AdventureSession.HomePieces"/>) :
    /// modèle du lot C pour un meuble-objet, modèle de la fête pour une décoration, sinon une caisse à la taille de l'emprise. Tant que la
    /// maison n'est pas agrandie au plus, des cloisons ferment le sol ouvert. L'état est relu à chaque image et le dessin refait dès qu'il
    /// change ; l'aperçu du mode décoration (vert : la pose est possible, rouge : non) suit chaque image. Affichage seulement.
    /// </summary>
    public sealed partial class SpaceView
    {
        private GameObject _homeRoot, _homeGhost;
        private string _homeKey, _homeGhostPiece;
        private Material _homeBox, _homeWall, _homeOk, _homeNo;
        private Renderer _homeGhostSlab;

        public void Home(AdventureSession adventure, HomeGhost? ghost, float deltaTime)
        {
            if (adventure == null || _root == null || !_indoor) return;
            Ghost(ghost);
            // Relecture de la PR #231 : la clé est relue à chaque image (une courte chaîne), le dessin refait dès qu'un meuble change.
            string key = adventure.HomeKey;
            if (key == _homeKey) return;
            _homeKey = key;
            if (_homeRoot != null) Object.Destroy(_homeRoot);
            _homeRoot = null;
            if (key.Length == 0) return;
            _homeRoot = new GameObject("Maison du joueur");
            _homeRoot.transform.SetParent(_root.transform, false);
            float y = StyleWorld.StandHeight(0f, 0f);
            foreach (HomePieceLook p in adventure.HomePieces) PlacePiece(_homeRoot.transform, p, y);
            Partitions(adventure, y);
        }

        private void PlacePiece(Transform parent, HomePieceLook p, float y)
        {
            var at = new Vector3((float)p.X, y, (float)p.Z);
            GameObject look = p.Model == null ? null
                : p.Feast ? LotCLook.PlaceFeast(parent, p.Model, p.Piece, at, (float)p.Yaw)
                : LotCLook.Place(parent, p.Model, p.Piece, at, (float)p.Yaw);
            if (look != null) return;
            if (_homeBox == null) _homeBox = ArenaView.MakeMaterial(new Color(0.55f, 0.42f, 0.28f));
            Box(parent, p.Piece, at + Vector3.up * 0.4f, new Vector3((float)p.Width, 0.8f, (float)p.Depth), _homeBox);   // sans modèle : une caisse
        }

        /// <summary>Cloisons au bord du sol ouvert (côtés x et z les plus loin du coin), seulement s'il reste de la place derrière elles.</summary>
        private void Partitions(AdventureSession adventure, float y)
        {
            if (!(adventure.HomeFloor is (double MinX, double MinZ, double MaxX, double MaxZ) f)) return;
            if (_homeWall == null) _homeWall = ArenaView.MakeMaterial(new Color(0.78f, 0.72f, 0.62f));
            float minX = (float)f.MinX, minZ = (float)f.MinZ, maxX = (float)f.MaxX, maxZ = (float)f.MaxZ;
            float roomX = Raw(_space.Max.X.Raw), roomZ = Raw(_space.Max.Z.Raw);
            if (maxX < roomX - 0.05f) Box(_homeRoot.transform, "Cloison est", new Vector3(maxX + 0.05f, y + 1.3f, (minZ + maxZ) / 2f), new Vector3(0.1f, 2.6f, maxZ - minZ), _homeWall);
            if (maxZ < roomZ - 0.05f) Box(_homeRoot.transform, "Cloison nord", new Vector3((minX + maxX) / 2f, y + 1.3f, maxZ + 0.05f), new Vector3(maxX - minX + 0.2f, 2.6f, 0.1f), _homeWall);
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        /// <summary>L'aperçu du meuble que l'on pose : une dalle verte ou rouge à la taille de l'emprise, et le modèle par-dessus.</summary>
        private void Ghost(HomeGhost? ghost)
        {
            if (!(ghost is HomeGhost g))
            {
                if (_homeGhost != null) Object.Destroy(_homeGhost);
                _homeGhost = null;
                _homeGhostPiece = null;
                return;
            }
            string id = g.Look.Piece + "/" + g.Look.Yaw;
            if (_homeGhost == null || _homeGhostPiece != id)
            {
                if (_homeGhost != null) Object.Destroy(_homeGhost);
                _homeGhost = new GameObject("Meuble à poser");
                _homeGhost.transform.SetParent(_root.transform, false);
                _homeGhostPiece = id;
                if (_homeOk == null) _homeOk = ArenaView.MakeMaterial(new Color(0.3f, 0.8f, 0.35f));
                if (_homeNo == null) _homeNo = ArenaView.MakeMaterial(new Color(0.85f, 0.25f, 0.2f));
                _homeGhostSlab = Box(_homeGhost.transform, "Emprise", Vector3.zero, new Vector3((float)g.Look.Width, 0.04f, (float)g.Look.Depth), _homeOk).GetComponent<Renderer>();
                var copy = new HomePieceLook(g.Look.Piece, g.Look.Model, g.Look.Feast, 0, 0, g.Look.Yaw, g.Look.Width, g.Look.Depth, 0, 0);
                PlacePiece(_homeGhost.transform, copy, 0f);
            }
            float y = StyleWorld.StandHeight(0f, 0f);
            _homeGhost.transform.position = new Vector3((float)g.Look.X, y + 0.02f, (float)g.Look.Z);
            if (_homeGhostSlab != null) _homeGhostSlab.sharedMaterial = g.Ok ? _homeOk : _homeNo;
        }

        /// <summary>Les matériaux de la maison, rendus avec la vue.</summary>
        private void ReleaseHome()
        {
            foreach (Material m in new[] { _homeBox, _homeWall, _homeOk, _homeNo })
                if (m != null) Object.Destroy(m);
            _homeBox = _homeWall = _homeOk = _homeNo = null;
        }
    }
}
