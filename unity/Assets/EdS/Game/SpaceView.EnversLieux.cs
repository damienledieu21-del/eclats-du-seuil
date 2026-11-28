using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.52b : le décor des douze lieux de l'Envers (kit Envers2 : moulin et clocher renversés, cascade figée, arbre millénaire mort,
    /// cairns suspendus, palissade calcinée, wagonnets, rosace brisée, barques noires, ponts brisés) et les secrets (coffres, stèles). Rien
    /// n'est construit sur toute l'aire : les pièces et les secrets sont rangés par case de la grille, puis construits seulement autour de la
    /// caméra, au plus quelques cases par image, avec un nombre de pièces par case selon la machine (<see cref="EnversLieux.MaxPiecesPerCell"/>),
    /// et défaits quand la caméra s'éloigne (avec leurs occulteurs). Chaque pièce est posée d'après les bornes réelles de son modèle (échelle
    /// avant le lacet, <see cref="FitSewerModel"/>), sur le sol affiché (<see cref="EnversLieux.Ground"/>) ; formes simples en repli si un
    /// modèle manque. Placements par fonctions pures testées (<see cref="EnversLieux"/>).
    /// </summary>
    public sealed partial class SpaceView
    {
        private readonly List<(SpaceObstacle Obstacle, EnversLieuPiece Piece)> _lieuPieces = new List<(SpaceObstacle, EnversLieuPiece)>();
        private readonly List<(double X, double Z, double Area)> _lieuSpots = new List<(double, double, double)>();
        private readonly Dictionary<int, GameObject> _lieuCells = new Dictionary<int, GameObject>();
        private List<MapSecret> _lieuSecrets = new List<MapSecret>();

        /// <summary>
        /// Garde l'obstacle <paramref name="o"/> s'il est une pièce d'un lieu de l'Envers (construite plus tard, par case) ; faux sinon.
        /// </summary>
        private bool EnversLieuObstacle(SpaceObstacle o)
        {
            EnversLieuPiece piece = EnversLieux.PieceFor(o.Id);
            if (piece == EnversLieuPiece.None) return false;
            float x0 = Raw(o.Points.Min(p => p.X.Raw)), x1 = Raw(o.Points.Max(p => p.X.Raw)), z0 = Raw(o.Points.Min(p => p.Z.Raw)), z1 = Raw(o.Points.Max(p => p.Z.Raw));
            _lieuPieces.Add((o, piece));
            _lieuSpots.Add(((x0 + x1) / 2.0, (z0 + z1) / 2.0, (x1 - x0) * (z1 - z0)));
            return true;
        }

        /// <summary>Les secrets de la carte, dessinés par case.</summary>
        private void EnversLieuxSecrets(AdventureMap map) => _lieuSecrets = map != null ? map.Secrets.ToList() : new List<MapSecret>();

        /// <summary>Construit les cases proches de la caméra qui manquent, défait celles qui sont sorties du rayon plus la marge.</summary>
        private void StreamLieux(Vector3 camera)
        {
            if (_enversRegion == null || (_lieuPieces.Count == 0 && _lieuSecrets.Count == 0)) return;
            double radius = EnversLook.StreamRadius(DeviceQuality.Tier);
            List<int> gone = _goneCells;   // V2.59 : liste réutilisée (avant : une liste neuve à chaque image)
            gone.Clear();
            foreach (KeyValuePair<int, GameObject> cell in _lieuCells)
                if (!EnversLook.CellKept(_enversRegion, cell.Key % 64, cell.Key / 64, camera.x, camera.z, radius)) gone.Add(cell.Key);
            foreach (int key in gone)
            {
                GameObject cell = _lieuCells[key];
                _lieuCells.Remove(key);
                if (cell == null) continue;
                DropCellOccluders(_lieuCellOccluders, key);   // V2.59 : ceux de la case, retenus à sa construction
                // Relecture indépendante de la PR #263 : les secrets de la case quittent aussi la liste de ce qui garde sa clarté
                // (V2.59 : ceux retenus à sa construction, puis les racines détruites).
                if (_lieuCellBright.TryGetValue(key, out List<Transform> bright))
                {
                    for (int i = 0; i < bright.Count; i++) _keepBright.Remove(bright[i]);
                    _lieuCellBright.Remove(key);
                }
                _keepBright.RemoveWhere(BrightGone);
                Object.Destroy(cell);
            }
            int built = 0;
            foreach ((int column, int row) in EnversLook.CellsNear(_enversRegion, camera.x, camera.z, radius))
            {
                if (built >= EnversLook.StreamCellsPerFrame) break;
                int key = row * 64 + column;
                if (_lieuCells.ContainsKey(key)) continue;
                int first = _occluders.Count;
                GameObject made = BuildLieuCell(column, row, out List<Transform> bright);
                _lieuCells[key] = made;
                _lieuCellOccluders[key] = OccludersSince(first, made.transform);
                _lieuCellBright[key] = bright;
                built++;
            }
        }

        /// <summary>Les pièces et les secrets d'une case, sous une racine de case ; les pièces assombries, les secrets gardent leur clarté.</summary>
        private GameObject BuildLieuCell(int column, int row, out List<Transform> bright)
        {
            bright = new List<Transform>();
            var cell = new GameObject("Envers lieux c" + column + "_r" + row);
            cell.transform.SetParent(_root.transform, false);
            DeviceTier tier = DeviceQuality.Tier;
            foreach (int i in EnversLieux.CellPieces(_enversRegion, column, row, _lieuSpots, tier))
            {
                int first = _root.transform.childCount;
                LieuPiece(_lieuPieces[i].Obstacle, _lieuPieces[i].Piece);
                Adopt(cell, first);
            }
            DarkenUnder(cell);
            foreach (int i in EnversLieux.CellSecrets(_enversRegion, column, row, _lieuSecrets))
            {
                int first = _root.transform.childCount;
                LieuSecret(_lieuSecrets[i], tier);
                for (int k = first; k < _root.transform.childCount; k++)
                {
                    Transform t = _root.transform.GetChild(k);
                    _keepBright.Add(t);
                    bright.Add(t);
                }
                Adopt(cell, first);
            }
            SmallCasters(cell);   // V2.59 : comme le décor posé au chargement
            return cell;
        }

        /// <summary>V2.59 : occulteurs et racines gardant leur clarté de chaque case des lieux, par clé de case.</summary>
        private readonly Dictionary<int, List<Occluder>> _lieuCellOccluders = new Dictionary<int, List<Occluder>>();
        private readonly Dictionary<int, List<Transform>> _lieuCellBright = new Dictionary<int, List<Transform>>();

        /// <summary>V2.59 : une racine détruite quitte ce qui garde sa clarté (sans allocation de prédicat).</summary>
        private static readonly System.Predicate<Transform> BrightGone = t => t == null;

        /// <summary>Range sous la case ce qui vient d'être posé sous la racine du décor (depuis le rang <paramref name="first"/>).</summary>
        private void Adopt(GameObject cell, int first)
        {
            var made = new List<Transform>();
            for (int k = first; k < _root.transform.childCount; k++) made.Add(_root.transform.GetChild(k));
            foreach (Transform t in made)
                if (t != cell.transform) t.SetParent(cell.transform, true);
        }

        /// <summary>Une pièce d'un lieu : le modèle sur l'emprise de l'obstacle (tuiles pour la palissade), au sol local ; sinon un prisme.</summary>
        private void LieuPiece(SpaceObstacle o, EnversLieuPiece piece)
        {
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float height = (float)EnversLieux.HeightOf(piece);
            bool occludes = EnversLieux.Occludes(piece);
            float ground = (float)EnversLieux.Ground(piece, x0, z0, x1, z1, (x, z) => StyleWorld.StandHeight((float)x, (float)z));
            GameObject model = CryptModel(EnversLieux.Folder, EnversLieux.ModelFor(piece));
            if (model != null && EnversLieux.Tiled(piece) && Footprint(model, out float modelLong, out float modelShort))
            {
                int n = 0;
                foreach (var t in SewerLooks.WallTiles(x0, z0, x1, z1, modelLong, modelShort))
                    FitSewerModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, occludes, ground, 0f, true);
                return;
            }
            if (FitSewerModel(model, o.Id, x0, z0, x1, z1, height, occludes, ground)) return;
            // Repli (modèle absent) : un prisme sombre de la hauteur de la pièce, posé sur le sol local.
            GameObject shape = Prism(o.Id, points, height, Plain(new Color(0.26f, 0.24f, 0.28f)), false, 2f);   // sans occulteur : il serait mesuré avant la montée au sol
            if (shape != null) shape.transform.position += Vector3.up * ground;
        }

        /// <summary>Un secret : le coffre ou la stèle (bornes réelles, lacet tiré de son identifiant), sa lueur violette ; sinon une forme simple.</summary>
        private void LieuSecret(MapSecret secret, DeviceTier tier)
        {
            var size = EnversLieux.SecretSize(secret.Kind);
            float x = (float)secret.X, z = (float)secret.Z, l = (float)size.Length / 2f, w = (float)size.Width / 2f;
            float ground = Ground(x, z, 0.5f);
            GameObject model = CryptModel(EnversLieux.Folder, EnversLieux.SecretModel(secret.Kind));
            if (!FitSewerModel(model, "Secret " + secret.Id, x - l, z - w, x + l, z + w, (float)size.Height, false, ground, (float)EnversLieux.SecretYaw(secret.Id)))
            {
                Color color = secret.Kind == SecretKind.Chest ? new Color(0.36f, 0.26f, 0.2f) : new Color(0.3f, 0.28f, 0.34f);
                Box("Secret " + secret.Id, new Vector3(x, ground + (float)size.Height / 2f, z), new Vector3(l * 2f, (float)size.Height, w * 2f), Plain(color), 1f);
            }
            if (tier == DeviceTier.Phone) return;   // pas de lumière de plus sur un téléphone
            var glow = EnversLieux.SecretGlow;
            Light light = CaveGlow("secret " + secret.Id, x, z, new Color((float)glow.R, (float)glow.G, (float)glow.B), (float)EnversLieux.SecretGlowIntensity, (float)EnversLieux.SecretGlowRange);
            light.transform.position = new Vector3(x, ground + 1.2f, z);
        }

        private void ReleaseLieux()
        {
            _lieuCells.Clear();
            _lieuCellOccluders.Clear();
            _lieuCellBright.Clear();
            _lieuPieces.Clear();
            _lieuSpots.Clear();
            _lieuSecrets = new List<MapSecret>();
        }
    }
}
