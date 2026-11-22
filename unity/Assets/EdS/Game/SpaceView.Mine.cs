using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.47.2 (mines de la carrière) : décor de la mine, dans la pénombre d'un dessous (<see cref="Cave"/>). Parois boisées, étais
    /// et leurs lanternes, rails, wagonnets, filons, effondrements, caisses, cristaux du Seuil, coffre et rivière souterraine d'après les
    /// obstacles (<see cref="MineLooks.PieceFor"/>) ; passerelle sur l'écart entre deux bras de la rivière (<see cref="MineLooks.Bridges"/>) ;
    /// ascenseur sur les portes de genre puits, échelles sur les portes de genre escalier, jour de la bouche (sans rideau d'eau) sur la porte
    /// qui mène dehors. Les modèles du kit de mine sont pris dans <c>Resources/Mines/&lt;nom&gt;</c> (<see cref="MineLooks.Models"/>) ; en repli
    /// (modèle absent : kit préparé, pas encore généré), des formes simples les remplacent.
    /// </summary>
    public sealed partial class SpaceView
    {
        private static readonly Dictionary<string, GameObject> MineModels = new Dictionary<string, GameObject>();

        private bool _mine;

        /// <summary>Le modèle du kit de mine nommé <paramref name="name"/>, ou null (cherché une seule fois).</summary>
        private static GameObject MineModel(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            GameObject found;
            if (!MineModels.TryGetValue(name, out found))
            {
                found = Resources.Load<GameObject>(MineLooks.Folder + name);
                MineModels[name] = found;
            }
            return found;
        }

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une pièce de la mine ; faux sinon (rocher simple de la grotte).</summary>
        private bool MineObstacle(SpaceObstacle o, Material rock, float rockTile, Material wood, float woodTile)
        {
            MinePiece piece = MineLooks.PieceFor(o.Id);
            if (piece == MinePiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)MineLooks.HeightOf(piece);
            if (piece == MinePiece.River)
            {
                // L'eau de la rivière souterraine, et son lit sombre dessous.
                Prism(o.Id + " lit", points.Select(p => new Vector3(p.x, -0.3f, p.z)).ToList(), 0.3f, Tinted(rock, new Color(0.35f, 0.35f, 0.4f), "lit de la rivière souterraine"), false, rockTile);
                _waters.Add(Prism(o.Id + " eau", points.Select(p => new Vector3(p.x, 0.04f, p.z)).ToList(), 0.04f, _water, false));
                return true;
            }
            Color seuil = new Color((float)MineLooks.SeuilCrystal.R, (float)MineLooks.SeuilCrystal.G, (float)MineLooks.SeuilCrystal.B);
            if (piece == MinePiece.Crystals) CaveGlow(o.Id, cx, cz, seuil, 1.6f, 7f);
            if (piece == MinePiece.Prop) Lantern(o.Id, cx, cz);
            if (FitMineModel(MineModel(MineLooks.ModelFor(piece)), o.Id, x0, z0, x1, z1, piece == MinePiece.Wall)) return true;
            // Formes de remplacement.
            switch (piece)
            {
                case MinePiece.Wall:
                    Prism(o.Id, points, height, rock, true, rockTile);
                    break;
                case MinePiece.Prop:
                    Box(o.Id + " poteau", new Vector3(cx, height / 2f, cz), new Vector3(x1 - x0, height, z1 - z0), wood, woodTile);
                    break;
                case MinePiece.Rails:
                    Prism(o.Id, points, height, Tinted(wood, new Color(0.45f, 0.4f, 0.38f), "traverses de la mine"), false, woodTile);
                    break;
                case MinePiece.Cart:
                case MinePiece.Crates:
                case MinePiece.Chest:
                    Prism(o.Id, points, height, wood, false, woodTile);
                    break;
                case MinePiece.Vein:
                    Prism(o.Id, points, height, Tinted(rock, new Color(1f, 0.62f, 0.38f), "filon de la mine"), false, rockTile);
                    break;
                case MinePiece.Crystals:
                    Prism(o.Id, points, height, Glow(seuil, 1.5f), false, 1f);
                    break;
                default:
                    Prism(o.Id, points, height, rock, false, rockTile);
                    break;
            }
            return true;
        }

        /// <summary>Lanterne d'un étai : une lumière chaude (et le modèle de la lanterne s'il est là).</summary>
        private void Lantern(string id, float x, float z)
        {
            Color warm = new Color((float)MineLooks.LanternColor.R, (float)MineLooks.LanternColor.G, (float)MineLooks.LanternColor.B);
            CaveGlow(id + " lanterne", x, z, warm, (float)MineLooks.LanternIntensity, (float)MineLooks.LanternRange);
            GameObject model = MineModel(MineLooks.LanternModel);
            if (model == null) return;
            GameObject copy = Object.Instantiate(model, _root.transform, false);
            copy.name = id + " (" + model.name + ")";
            foreach (Collider c in copy.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            copy.transform.position = new Vector3(x, 2.4f, z);
        }

        /// <summary>Le modèle <paramref name="model"/> étiré à l'emprise (x0, z0)-(x1, z1), posé au sol ; faux sans lui.</summary>
        private bool FitMineModel(GameObject model, string id, float x0, float z0, float x1, float z1, bool occludes)
        {
            if (model == null) return false;
            var look = new GameObject(id + " (" + model.name + ")");
            Object.Instantiate(model, look.transform, false);
            foreach (Collider c in look.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            look.transform.SetParent(_root.transform, false);
            if (StyleLook.VisibleBounds(look, out Bounds b) && b.size.x > 0.01f && b.size.z > 0.01f)
            {
                float sx = (x1 - x0) / b.size.x, sz = (z1 - z0) / b.size.z;
                look.transform.localScale = new Vector3(sx, Mathf.Max(sx, sz), sz);
                StyleLook.VisibleBounds(look, out b);
                look.transform.position += new Vector3((x0 + x1) / 2f - b.center.x, -b.min.y, (z0 + z1) / 2f - b.center.z);
            }
            if (occludes) AddModelOccluders(look);
            return true;
        }

        /// <summary>Les passerelles de la rivière souterraine (modèle, ou planches sur deux longerons).</summary>
        private void MineBridges(CombatSpace space, Material wood, float woodTile)
        {
            foreach (var span in MineLooks.Bridges(space.Obstacles))
            {
                float x0 = (float)span.MinX, x1 = (float)span.MaxX, z0 = (float)span.MinZ, z1 = (float)span.MaxZ;
                // La passerelle déborde d'un demi-mètre sur chaque rive.
                if (FitMineModel(MineModel(MineLooks.BridgeModel), "Passerelle", x0, z0 - 0.5f, x1, z1 + 0.5f, false)) continue;
                float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, length = z1 - z0 + 1f, width = x1 - x0;
                Box("Passerelle (planches)", new Vector3(cx, 0.12f, cz), new Vector3(width, 0.08f, length), wood, woodTile);
                Box("Passerelle (garde-corps ouest)", new Vector3(x0 + 0.08f, 0.6f, cz), new Vector3(0.12f, 0.08f, length), wood, woodTile);
                Box("Passerelle (garde-corps est)", new Vector3(x1 - 0.08f, 0.6f, cz), new Vector3(0.12f, 0.08f, length), wood, woodTile);
            }
        }

        /// <summary>
        /// Une porte de la mine : le jour de la bouche sur celle qui mène dehors (pas de rideau d'eau), l'ascenseur sur un puits, les échelles
        /// sur un escalier ; posés contre le bord le plus proche, tournés vers l'intérieur.
        /// </summary>
        private void MineDoor(MapDoor door, float minX, float minZ, float maxX, float maxZ, Material wood, float woodTile)
        {
            var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
            Quaternion turn = Quaternion.Euler(0f, (float)facing.Yaw, 0f);
            if (door.Outside)
            {
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour de la bouche (" + door.Id + ")", glow.x, glow.z, new Color(0.85f, 0.85f, 0.8f), 0f, (float)CaveLooks.CurtainReach));
                return;
            }
            string name = door.Kind == DoorKind.Shaft ? MineLooks.LiftModel : door.Kind == DoorKind.Stairs ? MineLooks.LaddersModel : null;
            if (name == null) return;
            Vector3 center = at + outward * 1.2f;
            GameObject model = MineModel(name);
            if (model != null)
            {
                GameObject copy = Object.Instantiate(model, _root.transform, false);
                copy.name = (door.Kind == DoorKind.Shaft ? "Ascenseur (" : "Échelles (") + door.Id + ")";
                foreach (Collider c in copy.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                copy.transform.rotation = turn;
                copy.transform.position = center;
                return;
            }
            if (door.Kind == DoorKind.Shaft)
            {
                // Chevalement de remplacement : quatre montants, deux poutres en haut, la cage entre eux.
                const float half = 1.5f, top = 6f;
                foreach (float dx in new[] { -half, half })
                    foreach (float dz in new[] { -half, half })
                        Box("Ascenseur (montant)", center + new Vector3(dx, top / 2f, dz), new Vector3(0.3f, top, 0.3f), wood, woodTile);
                Box("Ascenseur (poutre)", center + new Vector3(0f, top, -half), new Vector3(2f * half + 0.3f, 0.3f, 0.3f), wood, woodTile);
                Box("Ascenseur (poutre)", center + new Vector3(0f, top, half), new Vector3(2f * half + 0.3f, 0.3f, 0.3f), wood, woodTile);
                Box("Ascenseur (cage)", center + new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 2.4f, 2.2f), Tinted(wood, new Color(0.6f, 0.55f, 0.5f), "cage de l'ascenseur"), woodTile);
            }
            else
            {
                // Échelles de remplacement : la bouche noire du puits et deux montants qui dépassent.
                Box("Échelles (bouche)", center + new Vector3(0f, 0.01f, 0f), new Vector3(2.2f, 0.02f, 2.2f), Plain(new Color(0.01f, 0.01f, 0.012f)), 1f);
                foreach (float dx in new[] { -0.4f, 0.4f })
                    Box("Échelles (montant)", center + new Vector3(dx, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), wood, woodTile);
            }
        }
    }
}
