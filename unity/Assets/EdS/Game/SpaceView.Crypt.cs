using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.48.2 (cryptes de l'abbaye et de la chapelle) : décor d'une crypte, dans la pénombre d'un dessous (<see cref="Cave"/>). Murs
    /// maçonnés et piliers (occulteurs), gisants, sarcophages, ossuaires, grilles, candélabres et bougies (lumières chaudes), autel, tombeau de
    /// l'abbé et coffre d'après les obstacles (<see cref="CryptLooks.PieceFor"/>) ; voûte sur l'écart entre deux grilles
    /// (<see cref="CryptLooks.Arches"/>) ; escalier de crypte sur chaque porte, avec le jour de la nef sur celle qui remonte dehors ; dalle
    /// descellée au sol de la chapelle (<see cref="CryptSlab"/>). Les modèles sont pris dans <c>Resources/Crypte/&lt;nom&gt;</c>
    /// (<see cref="CryptLooks.Models"/>, le coffre dans <c>Resources/Mines</c>) ; en repli (modèle absent), des formes simples les remplacent.
    /// </summary>
    public sealed partial class SpaceView
    {
        private static readonly Dictionary<string, GameObject> CryptModels = new Dictionary<string, GameObject>();

        private bool _crypt;

        /// <summary>Le modèle <paramref name="name"/> du dossier <paramref name="folder"/> de Resources, ou null (cherché une seule fois).</summary>
        private static GameObject CryptModel(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = folder + name;
            GameObject found;
            if (!CryptModels.TryGetValue(key, out found))
            {
                found = Resources.Load<GameObject>(key);
                CryptModels[key] = found;
            }
            return found;
        }

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une pièce de crypte ; faux sinon.</summary>
        private bool CryptObstacle(SpaceObstacle o, Material rock, float rockTile, Material wood, float woodTile)
        {
            CryptPiece piece = CryptLooks.PieceFor(o.Id);
            if (piece == CryptPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)CryptLooks.HeightOf(piece);
            Color candle = new Color((float)CryptLooks.CandleColor.R, (float)CryptLooks.CandleColor.G, (float)CryptLooks.CandleColor.B);
            if (piece == CryptPiece.Candelabra) CaveGlow(o.Id, cx, cz, candle, (float)CryptLooks.CandelabraIntensity, (float)CryptLooks.CandelabraRange);
            if (piece == CryptPiece.Candles) CaveGlow(o.Id, cx, cz, candle, (float)CryptLooks.CandlesIntensity, (float)CryptLooks.CandlesRange);
            GameObject model = CryptModel(CryptLooks.FolderFor(piece), CryptLooks.ModelFor(piece));
            var tile = CryptLooks.TileOf(piece);
            if (model != null && tile.HasValue)
            {
                // Relecture indépendante de la PR #251 : un mur ou un ossuaire n'est pas étiré sur une grande emprise (16 × 14 m) : le modèle
                // est répété en tuiles le long de l'emprise (CryptLooks.Tiles).
                int n = 0;
                foreach (var t in CryptLooks.Tiles(x0, z0, x1, z1, tile.Value.Long, tile.Value.Short))
                    FitMineModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, CryptLooks.Occludes(piece));
                return true;
            }
            if (FitMineModel(model, o.Id, x0, z0, x1, z1, height, CryptLooks.Occludes(piece))) return true;
            // Formes de remplacement.
            Material pale = Tinted(rock, new Color(0.78f, 0.75f, 0.7f), "pierre des tombeaux");
            switch (piece)
            {
                case CryptPiece.Wall:
                case CryptPiece.Pillar:
                    Prism(o.Id, points, height, rock, true, rockTile);
                    break;
                case CryptPiece.Ossuary:
                    Prism(o.Id, points, height, Tinted(rock, new Color(0.86f, 0.8f, 0.66f), "ossements"), false, rockTile);
                    break;
                case CryptPiece.Grille:
                    Prism(o.Id, points, height, Plain(new Color(0.12f, 0.12f, 0.13f)), false, 1f);
                    break;
                case CryptPiece.Candelabra:
                    Box(o.Id + " pied", new Vector3(cx, height / 2f, cz), new Vector3(0.12f, height, 0.12f), Plain(new Color(0.15f, 0.13f, 0.1f)), 1f);
                    Box(o.Id + " flamme", new Vector3(cx, height + 0.08f, cz), new Vector3(0.1f, 0.16f, 0.1f), Glow(candle, 2f), 1f);
                    break;
                case CryptPiece.Candles:
                    Prism(o.Id, points, height, Glow(new Color(0.95f, 0.9f, 0.78f), 0.6f), false, 1f);
                    break;
                case CryptPiece.Chest:
                    Prism(o.Id, points, height, wood, false, woodTile);
                    break;
                default:   // gisant, sarcophage, autel, tombeau : pierre claire
                    Prism(o.Id, points, height, pale, false, rockTile);
                    break;
            }
            return true;
        }

        /// <summary>Les voûtes des passages entre deux grilles (modèle, ou linteau de pierre), occulteurs : elles se fondent au-dessus du joueur.</summary>
        private void CryptArches(CombatSpace space, Material rock, float rockTile)
        {
            foreach (var span in CryptLooks.Arches(space.Obstacles))
            {
                float x0 = (float)span.MinX, x1 = (float)span.MaxX, z0 = (float)span.MinZ, z1 = (float)span.MaxZ;
                // La voûte prend appui sur les deux grilles (un demi-mètre de chaque côté).
                if (FitMineModel(CryptModel(CryptLooks.Folder, CryptLooks.VaultModel), "Voûte", x0 - 0.5f, z0, x1 + 0.5f, z1, (float)CryptLooks.VaultHeight, true)) continue;
                const float lintel = 3.4f;
                var points = new List<Vector3>
                {
                    new Vector3(x0 - 0.5f, lintel, z0), new Vector3(x1 + 0.5f, lintel, z0), new Vector3(x1 + 0.5f, lintel, z1), new Vector3(x0 - 0.5f, lintel, z1),
                };
                Prism("Voûte (linteau)", points, (float)CryptLooks.VaultHeight - lintel, rock, true, rockTile);
            }
        }

        /// <summary>
        /// Une porte de crypte : l'escalier contre le bord le plus proche, tourné vers l'intérieur (escalier, trappe vue d'en bas, porte qui
        /// remonte à la surface), et le jour de la nef sur celle qui mène dehors.
        /// </summary>
        private void CryptDoor(MapDoor door, float minX, float minZ, float maxX, float maxZ, Material rock, float rockTile)
        {
            var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
            if (door.Outside)
            {
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour de la nef (" + door.Id + ")", glow.x, glow.z, new Color(0.8f, 0.82f, 0.85f), 0f, (float)CaveLooks.CurtainReach));
            }
            string name = CryptLooks.DoorModel(door.Kind);
            if (name == null) return;
            // Relecture indépendante de la PR #251 : la volée longe le bord, à côté de la place d'arrivée (on n'apparaît pas dedans) et sans
            // sortir de l'aire (dans le mur) ; le modèle est posé sur la même emprise.
            var steps = CryptLooks.StairSteps(door.X, door.Z, minX, minZ, maxX, maxZ);
            if (steps.Count == 0) return;
            float sx0 = (float)steps.Min(s => s.MinX), sz0 = (float)steps.Min(s => s.MinZ), sx1 = (float)steps.Max(s => s.MaxX), sz1 = (float)steps.Max(s => s.MaxZ);
            if (FitMineModel(CryptModel(CryptLooks.Folder, name), "Escalier de crypte (" + door.Id + ")", sx0, sz0, sx1, sz1, (float)CryptLooks.StairsHeight, false)) return;
            // Escalier de remplacement : cinq marches qui montent le long du bord.
            for (int i = 0; i < steps.Count; i++)
            {
                var st = steps[i];
                float h = (float)st.Height;
                Vector3 c = new Vector3((float)(st.MinX + st.MaxX) / 2f, h / 2f, (float)(st.MinZ + st.MaxZ) / 2f);
                Box("Escalier de crypte (marche " + (i + 1) + ", " + door.Id + ")", c, new Vector3((float)(st.MaxX - st.MinX), h, (float)(st.MaxZ - st.MinZ)), rock, rockTile);
            }
        }

        /// <summary>Chantier V2.48.2 : la dalle descellée d'une trappe d'intérieur (chapelle), au sol, un peu de travers ; modèle ou dalle de pierre.</summary>
        private void CryptSlab(MapDoor door)
        {
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            float side = (float)CryptLooks.SlabSide;
            GameObject model = CryptModel(CryptLooks.Folder, CryptLooks.SlabModel);
            if (model != null)
            {
                FitMineModel(model, "Dalle (" + door.Id + ")", at.x - side / 2f, at.z - side / 2f, at.x + side / 2f, at.z + side / 2f, (float)CryptLooks.SlabHeight, false);
                return;
            }
            Material stone = Built(StyleSurface.Stone, new Color(0.62f, 0.6f, 0.56f), out float tile);
            GameObject slab = Box("Dalle descellée (" + door.Id + ")", at + new Vector3(0f, 0.03f, 0f), new Vector3(side, 0.06f, side), stone, tile);
            slab.transform.rotation = Quaternion.Euler(0f, 7f, 1.5f);   // descellée : un peu de travers, un bord levé
        }
    }
}
