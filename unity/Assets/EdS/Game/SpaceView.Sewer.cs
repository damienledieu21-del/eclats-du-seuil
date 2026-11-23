using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.49.2 (égouts du Bourg) : décor des égouts, dans la pénombre d'un dessous (<see cref="Cave"/>). Murs de brique et piliers
    /// (occulteurs), canal et bassin (eau sombre au ras du sol), grilles, tuyaux, déversoir, caisses des contrebandiers et repaires (lanterne du
    /// passeur, lueur de l'eau croupie de la bête) d'après les obstacles (<see cref="SewerLooks.PieceFor"/>) ; passerelles sur l'écart entre deux
    /// tronçons du canal (<see cref="SewerLooks.Bridges"/>) ; voûtes sur les grilles et les passerelles (<see cref="SewerLooks.Arches"/>) ; échelle
    /// contre le mur aux portes qui montent, jour du dehors sur les portes de la surface (<see cref="SewerDoor"/>) ; côté Bourg, bouche d'égout et
    /// déversoir (<see cref="SewerEntrances"/>). Les modèles sont pris dans <c>Resources/Egouts/&lt;nom&gt;</c> (<see cref="SewerLooks.Models"/>) ;
    /// en repli (modèle absent : kit préparé, pas encore généré), des formes simples les remplacent.
    /// </summary>
    public sealed partial class SpaceView
    {
        private bool _sewer;

        /// <summary>
        /// Pose <paramref name="model"/> sur l'emprise [x0, x1] × [z0, z1] à la hauteur <paramref name="height"/>, sans l'étirer
        /// (<see cref="SewerLooks.Fit"/>), au sol <paramref name="ground"/> ; occulteur si <paramref name="occludes"/>. Faux sans modèle.
        /// </summary>
        private bool FitSewerModel(GameObject model, string id, float x0, float z0, float x1, float z1, float height, bool occludes, float ground = 0f, float yaw = 0f)
        {
            if (model == null) return false;
            var look = new GameObject(id + " (" + model.name + ")");
            GameObject copy = Object.Instantiate(model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.SetParent(_root.transform, false);
            if (yaw != 0f) copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * copy.transform.localRotation;
            if (StyleLook.VisibleBounds(look, out Bounds b) && b.size.x > 0.01f && b.size.y > 0.01f && b.size.z > 0.01f)
            {
                var fit = SewerLooks.Fit(b.size.x, b.size.y, b.size.z, x1 - x0, z1 - z0, height);
                if (fit.Turn) copy.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * copy.transform.localRotation;
                look.transform.localScale = new Vector3((float)fit.Sx, (float)fit.Sy, (float)fit.Sz);
                StyleLook.VisibleBounds(look, out b);
                look.transform.position += new Vector3((x0 + x1) / 2f - b.center.x, ground - b.min.y, (z0 + z1) / 2f - b.center.z);
            }
            if (occludes) AddModelOccluders(look);
            return true;
        }

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une pièce d'égout ; faux sinon.</summary>
        private bool SewerObstacle(SpaceObstacle o, Material rock, float rockTile, Material wood, float woodTile)
        {
            SewerPiece piece = SewerLooks.PieceFor(o.Id);
            if (piece == SewerPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)SewerLooks.HeightOf(piece);
            if (piece == SewerPiece.Lair)
            {
                // Le repaire du passeur a sa lanterne ; celui de la bête, la lueur verdâtre de l'eau croupie.
                bool beast = o.Id.Contains("bete");
                var c = beast ? SewerLooks.MireColor : SewerLooks.LanternColor;
                CaveGlow(o.Id, cx, cz, new Color((float)c.R, (float)c.G, (float)c.B), (float)(beast ? SewerLooks.MireIntensity : SewerLooks.LanternIntensity),
                    (float)(beast ? SewerLooks.MireRange : SewerLooks.LanternRange));
            }
            // L'eau du canal et du bassin, au ras du sol, avec ou sans le modèle de sa margelle.
            if (SewerLooks.IsWater(piece)) _waters.Add(Prism(o.Id + " eau", points.Select(p => new Vector3(p.x, 0.04f, p.z)).ToList(), 0.04f, _water, false));
            GameObject model = CryptModel(SewerLooks.Folder, SewerLooks.ModelFor(piece));
            var tile = SewerLooks.TileOf(piece);
            if (model != null && tile.HasValue)
            {
                // Les grands murs et le canal ne sont pas étirés : le modèle est répété en tuiles le long de l'emprise (au plus 1,5 fois sa taille).
                int n = 0;
                foreach (var t in CryptLooks.Tiles(x0, z0, x1, z1, tile.Value.Long, tile.Value.Short))
                    FitSewerModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, SewerLooks.Occludes(piece));
                return true;
            }
            if (FitSewerModel(model, o.Id, x0, z0, x1, z1, height, SewerLooks.Occludes(piece))) return true;
            // Formes de remplacement.
            Material brick = Tinted(rock, new Color(0.62f, 0.42f, 0.34f), "brique des égouts");
            Material iron = Plain(new Color(0.16f, 0.13f, 0.11f));
            switch (piece)
            {
                case SewerPiece.Wall:
                case SewerPiece.Pillar:
                    Prism(o.Id, points, height, brick, true, rockTile);
                    break;
                case SewerPiece.Canal:
                case SewerPiece.Basin:
                    // La margelle : deux bordures de pierre le long des grands côtés (l'eau est déjà posée).
                    bool alongX = x1 - x0 >= z1 - z0;
                    if (alongX)
                    {
                        Box(o.Id + " (bord sud)", new Vector3(cx, 0.1f, z0 + 0.15f), new Vector3(x1 - x0, 0.2f, 0.3f), rock, rockTile);
                        Box(o.Id + " (bord nord)", new Vector3(cx, 0.1f, z1 - 0.15f), new Vector3(x1 - x0, 0.2f, 0.3f), rock, rockTile);
                    }
                    else
                    {
                        Box(o.Id + " (bord ouest)", new Vector3(x0 + 0.15f, 0.1f, cz), new Vector3(0.3f, 0.2f, z1 - z0), rock, rockTile);
                        Box(o.Id + " (bord est)", new Vector3(x1 - 0.15f, 0.1f, cz), new Vector3(0.3f, 0.2f, z1 - z0), rock, rockTile);
                    }
                    break;
                case SewerPiece.Grate:
                    Prism(o.Id, points, height, iron, false, 1f);
                    break;
                case SewerPiece.Pipe:
                    Prism(o.Id, points, height, Tinted(rock, new Color(0.55f, 0.45f, 0.35f), "terre cuite"), false, rockTile);
                    break;
                case SewerPiece.Crates:
                case SewerPiece.Lair:
                    Prism(o.Id, points, height, wood, false, woodTile);
                    break;
                default:   // déversoir : pierre maçonnée
                    Prism(o.Id, points, height, rock, false, rockTile);
                    break;
            }
            return true;
        }

        /// <summary>Les passerelles du canal (modèle, ou planches sur deux garde-corps), puis les voûtes sur les grilles et les passerelles.</summary>
        private void SewerBridgesAndArches(CombatSpace space, Material rock, float rockTile, Material wood, float woodTile)
        {
            foreach (var span in SewerLooks.Bridges(space.Obstacles))
            {
                float x0 = (float)span.MinX, x1 = (float)span.MaxX, z0 = (float)span.MinZ, z1 = (float)span.MaxZ;
                // La passerelle déborde d'un demi-mètre sur chaque rive.
                if (FitSewerModel(CryptModel(SewerLooks.Folder, SewerLooks.BridgeModel), "Passerelle", x0, z0 - 0.5f, x1, z1 + 0.5f, (float)SewerLooks.BridgeHeight, false)) continue;
                float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, length = z1 - z0 + 1f, width = x1 - x0;
                Box("Passerelle (planches)", new Vector3(cx, 0.12f, cz), new Vector3(width, 0.08f, length), wood, woodTile);
                Box("Passerelle (garde-corps ouest)", new Vector3(x0 + 0.08f, 0.6f, cz), new Vector3(0.12f, 0.08f, length), wood, woodTile);
                Box("Passerelle (garde-corps est)", new Vector3(x1 - 0.08f, 0.6f, cz), new Vector3(0.12f, 0.08f, length), wood, woodTile);
            }
            foreach (var span in SewerLooks.Arches(space.Obstacles))
            {
                float x0 = (float)span.MinX, x1 = (float)span.MaxX, z0 = (float)span.MinZ, z1 = (float)span.MaxZ;
                // La voûte prend appui de part et d'autre du passage (un demi-mètre de chaque côté) ; occulteur : elle se fond au-dessus du joueur.
                if (FitSewerModel(CryptModel(SewerLooks.Folder, SewerLooks.VaultModel), "Voûte", x0 - 0.5f, z0, x1 + 0.5f, z1, (float)SewerLooks.VaultHeight, true)) continue;
                const float lintel = 3.2f;
                var points = new List<Vector3>
                {
                    new Vector3(x0 - 0.5f, lintel, z0), new Vector3(x1 + 0.5f, lintel, z0), new Vector3(x1 + 0.5f, lintel, z1), new Vector3(x0 - 0.5f, lintel, z1),
                };
                Prism("Voûte (linteau)", points, (float)SewerLooks.VaultHeight - lintel, Tinted(rock, new Color(0.62f, 0.42f, 0.34f), "brique des égouts"), true, rockTile);
            }
        }

        /// <summary>
        /// Une porte des égouts : le jour du dehors sur celles qui mènent à la surface (bouche du marché, déversoir) ; l'échelle contre le mur le
        /// plus proche sur celles qui montent (puits, bouche), à côté de la place d'arrivée.
        /// </summary>
        private void SewerDoor(MapDoor door, float minX, float minZ, float maxX, float maxZ, Material wood, float woodTile)
        {
            var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
            if (door.Outside)
            {
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour du Bourg (" + door.Id + ")", glow.x, glow.z, new Color(0.8f, 0.82f, 0.8f), 0f, (float)CaveLooks.CurtainReach));
            }
            if (SewerLooks.DoorModel(door) == null) return;
            var ladder = SewerLooks.LadderAt(door.X, door.Z, minX, minZ, maxX, maxZ);
            if (!ladder.HasValue) return;
            float lx0 = (float)ladder.Value.MinX, lz0 = (float)ladder.Value.MinZ, lx1 = (float)ladder.Value.MaxX, lz1 = (float)ladder.Value.MaxZ;
            if (FitSewerModel(CryptModel(SewerLooks.Folder, SewerLooks.LadderModel), "Échelle (" + door.Id + ")", lx0, lz0, lx1, lz1, (float)SewerLooks.LadderHeight, false)) return;
            // Échelle de remplacement : deux montants et des barreaux contre le mur.
            Material iron = Plain(new Color(0.2f, 0.16f, 0.13f));
            float h = (float)SewerLooks.LadderHeight;
            bool alongX = facing.OutZ != 0;
            float cx = (lx0 + lx1) / 2f, cz = (lz0 + lz1) / 2f, half = (float)SewerLooks.LadderWidth / 2f - 0.05f;
            Vector3 side = alongX ? new Vector3(half, 0f, 0f) : new Vector3(0f, 0f, half);
            Box("Échelle (montant 1, " + door.Id + ")", new Vector3(cx, h / 2f, cz) - side, new Vector3(0.08f, h, 0.08f), iron, 1f);
            Box("Échelle (montant 2, " + door.Id + ")", new Vector3(cx, h / 2f, cz) + side, new Vector3(0.08f, h, 0.08f), iron, 1f);
            for (int i = 1; i <= 9; i++)
                Box("Échelle (barreau " + i + ", " + door.Id + ")", new Vector3(cx, h * i / 10f, cz),
                    alongX ? new Vector3(half * 2f, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, half * 2f), iron, 1f);
        }

        /// <summary>
        /// Côté Bourg (la surface) : sur les portes qui descendent aux égouts, la bouche d'égout au sol de la place du marché et le déversoir dans
        /// le talus du fossé (<see cref="SewerLooks.SurfaceModel"/>), posés sur le sol affiché ; modèle, ou grille de fer et arche de pierre.
        /// </summary>
        private void SewerEntrances()
        {
            float minX = Raw(_space.Min.X.Raw) - BourgMargin, maxX = Raw(_space.Max.X.Raw) + BourgMargin;
            float minZ = Raw(_space.Min.Z.Raw) - BourgMargin, maxZ = Raw(_space.Max.Z.Raw) + BourgMargin;
            foreach (MapDoor door in _doors)
            {
                string name = SewerLooks.SurfaceModel(door.Id);
                if (name == null) continue;
                float x = (float)door.X, z = (float)door.Z;
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;   // hors de la fenêtre affichée (comme les pièces du Bourg)
                bool manhole = name == SewerLooks.ManholeModel;
                float w = (float)(manhole ? SewerLooks.ManholeSide : SewerLooks.OutfallWidth), d = (float)(manhole ? SewerLooks.ManholeSide : SewerLooks.OutfallDepth);
                float ground = Ground(x, z, Mathf.Max(w, d) / 2f);
                float height = (float)(manhole ? SewerLooks.ManholeHeight : SewerLooks.OutfallHeight);
                if (FitSewerModel(CryptModel(SewerLooks.Folder, name), (manhole ? "Bouche d'égout (" : "Déversoir (") + door.Id + ")", x - w / 2f, z - d / 2f, x + w / 2f, z + d / 2f,
                        height, false, ground, manhole ? 0f : 180f))
                    continue;
                if (manhole)
                {
                    Box("Bouche d'égout (" + door.Id + ")", new Vector3(x, ground + 0.03f, z), new Vector3(1.2f, 0.06f, 1.2f), Plain(new Color(0.14f, 0.13f, 0.12f)), 1f);
                    continue;
                }
                Material stone = Built(StyleSurface.Stone, new Color(0.5f, 0.48f, 0.45f), out float tile);
                Box("Déversoir (pied ouest, " + door.Id + ")", new Vector3(x - w / 2f + 0.2f, ground + height / 2f, z), new Vector3(0.4f, height, d), stone, tile);
                Box("Déversoir (pied est, " + door.Id + ")", new Vector3(x + w / 2f - 0.2f, ground + height / 2f, z), new Vector3(0.4f, height, d), stone, tile);
                Box("Déversoir (linteau, " + door.Id + ")", new Vector3(x, ground + height - 0.2f, z), new Vector3(w, 0.4f, d), stone, tile);
                Box("Déversoir (grille, " + door.Id + ")", new Vector3(x, ground + (height - 0.4f) / 2f, z + d / 2f - 0.1f), new Vector3(w - 0.8f, height - 0.4f, 0.06f), Plain(new Color(0.16f, 0.13f, 0.11f)), 1f);
            }
        }
    }
}
