using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.50b (galeries sous les pierres du Seuil) : décor des galeries cristallines, dans la pénombre d'un dessous (<see cref="Cave"/> :
    /// ni pluie ni neige, ni leur son). Parois de basalte veinées de cristal, piliers et éclats géants (occulteurs), amas de cristaux, gouffre,
    /// autel des éclats et éboulis d'après les obstacles (<see cref="GalleryLooks.PieceFor"/>), avec la lueur violette des cristaux du Seuil ;
    /// ponts de pierre sur l'écart entre deux tronçons du gouffre (<see cref="GalleryLooks.Bridges"/>) ; faille contre la paroi aux portes de
    /// genre faille, jour du dehors à la porte de la surface (<see cref="GalleryDoor"/>) ; côté surface, l'entrée entre les pierres levées
    /// (<see cref="GalleryEntrance"/>). Modèles pris dans <c>Resources/Galeries/&lt;nom&gt;</c> (<see cref="GalleryLooks.Models"/>) et posés
    /// par <see cref="FitSewerModel"/> (échelle sur une pose alignée sur les axes du modèle, lacet au-dessus : jamais d'échelle non uniforme
    /// au-dessus d'un enfant tourné) ; en repli (kit préparé, pas encore généré), des formes simples.
    /// </summary>
    public sealed partial class SpaceView
    {
        private bool _gallery;

        private Color GalleryGlowColor => new Color((float)GalleryLooks.GlowColor.R, (float)GalleryLooks.GlowColor.G, (float)GalleryLooks.GlowColor.B);

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une pièce des galeries ; faux sinon.</summary>
        private bool GalleryObstacle(SpaceObstacle o, Material rock, float rockTile)
        {
            GalleryPiece piece = GalleryLooks.PieceFor(o.Id);
            if (piece == GalleryPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)GalleryLooks.HeightOf(piece);
            var glow = GalleryLooks.GlowOf(piece);
            if (glow.Intensity > 0) CaveGlow(o.Id, cx, cz, GalleryGlowColor, (float)glow.Intensity, (float)glow.Range);
            GameObject model = CryptModel(GalleryLooks.Folder, GalleryLooks.ModelFor(piece));
            if (model != null && GalleryLooks.TileOf(piece).HasValue && Footprint(model, out float modelLong, out float modelShort))
            {
                // La paroi et le gouffre en tuiles bout à bout, d'après les bornes réelles du modèle : sans trou, étirées en longueur seulement.
                int n = 0;
                foreach (var t in SewerLooks.WallTiles(x0, z0, x1, z1, modelLong, modelShort))
                    FitSewerModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, GalleryLooks.Occludes(piece), 0f, 0f, true);
                return true;
            }
            if (FitSewerModel(model, o.Id, x0, z0, x1, z1, height, GalleryLooks.Occludes(piece))) return true;
            // Formes de remplacement.
            Material basalt = Tinted(rock, new Color((float)GalleryLooks.Basalt.R, (float)GalleryLooks.Basalt.G, (float)GalleryLooks.Basalt.B), "basalte des galeries");
            Material crystal = ArenaView.MakeMaterial(new Color((float)GalleryLooks.CrystalColor.R, (float)GalleryLooks.CrystalColor.G, (float)GalleryLooks.CrystalColor.B));
            switch (piece)
            {
                case GalleryPiece.Wall:
                case GalleryPiece.Pillar:
                    Prism(o.Id, points, height, basalt, true, rockTile);
                    break;
                case GalleryPiece.GiantShard:
                    GiantShardShape(o.Id, cx, cz, Mathf.Min(x1 - x0, z1 - z0) / 2f, height, crystal);
                    break;
                case GalleryPiece.Chasm:
                    // Un creux sombre : le fond posé sous le sol, deux lèvres de roche le long des grands côtés.
                    float depth = (float)GalleryLooks.ChasmDepth;
                    Prism(o.Id + " (fond)", points.Select(p => new Vector3(p.x, -depth, p.z)).ToList(), 0.05f, Plain(new Color(0.03f, 0.02f, 0.05f)), false);
                    bool alongX = x1 - x0 >= z1 - z0;
                    if (alongX)
                    {
                        Box(o.Id + " (lèvre sud)", new Vector3(cx, 0.1f, z0 + 0.2f), new Vector3(x1 - x0, 0.2f, 0.4f), basalt, rockTile);
                        Box(o.Id + " (lèvre nord)", new Vector3(cx, 0.1f, z1 - 0.2f), new Vector3(x1 - x0, 0.2f, 0.4f), basalt, rockTile);
                    }
                    else
                    {
                        Box(o.Id + " (lèvre ouest)", new Vector3(x0 + 0.2f, 0.1f, cz), new Vector3(0.4f, 0.2f, z1 - z0), basalt, rockTile);
                        Box(o.Id + " (lèvre est)", new Vector3(x1 - 0.2f, 0.1f, cz), new Vector3(0.4f, 0.2f, z1 - z0), basalt, rockTile);
                    }
                    break;
                case GalleryPiece.Crystals:
                    Prism(o.Id, points, height, crystal, false, 1f);
                    break;
                case GalleryPiece.Altar:
                    Prism(o.Id, points, height, basalt, false, rockTile);
                    GiantShardShape(o.Id + " (éclats)", cx, cz, Mathf.Min(x1 - x0, z1 - z0) / 6f, height + 1.2f, crystal);
                    break;
                default:   // éboulis
                    Prism(o.Id, points, height, basalt, false, rockTile);
                    break;
            }
            return true;
        }

        /// <summary>Forme de remplacement d'un éclat géant : trois prismes de cristal effilés (cylindres à quatre pans), le plus haut au centre.</summary>
        private void GiantShardShape(string id, float x, float z, float radius, float top, Material crystal)
        {
            for (int i = 0; i < 3; i++)
            {
                float k = 1f - 0.3f * i;
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = id + " " + (i + 1);
                Object.Destroy(shard.GetComponent<Collider>());
                // Pose alignée (échelle) sous un parent tourné (lacet) : l'échelle n'est jamais au-dessus d'un enfant tourné.
                var turn = new GameObject(id + " (lacet " + (i + 1) + ")");
                turn.transform.SetParent(_root.transform, false);
                float dx = i == 0 ? 0f : (i == 1 ? 0.45f : -0.4f) * radius, dz = i == 2 ? 0.35f * radius : -0.25f * radius;
                turn.transform.position = new Vector3(x + dx, top * k / 2f, z + dz);
                turn.transform.rotation = Quaternion.Euler(0f, 25f + 40f * i, 0f);
                shard.transform.SetParent(turn.transform, false);
                shard.transform.localScale = new Vector3(radius * k, top * k, radius * k * 0.8f);
                shard.GetComponent<Renderer>().sharedMaterial = crystal;
                if (i == 0) AddModelOccluders(turn);
            }
        }

        /// <summary>Les ponts de pierre sur le gouffre (modèle, ou dalle de basalte), puis une voûte de roche au-dessus de chacun.</summary>
        private void GalleryBridges(CombatSpace space, Material rock, float rockTile)
        {
            Material basalt = Tinted(rock, new Color((float)GalleryLooks.Basalt.R, (float)GalleryLooks.Basalt.G, (float)GalleryLooks.Basalt.B), "basalte des galeries");
            foreach (var gap in GalleryLooks.Bridges(space.Obstacles))
            {
                var span = GalleryLooks.BridgeSpan(gap);
                float x0 = (float)span.MinX, x1 = (float)span.MaxX, z0 = (float)span.MinZ, z1 = (float)span.MaxZ;
                if (!FitSewerModel(CryptModel(GalleryLooks.Folder, GalleryLooks.BridgeModel), "Pont de pierre", x0, z0, x1, z1, (float)GalleryLooks.BridgeHeight, false, -0.8f))
                    Box("Pont de pierre (dalle)", new Vector3((x0 + x1) / 2f, 0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), basalt, rockTile);
                // La voûte de roche prend appui de part et d'autre du pont (un demi-mètre de chaque côté) ; occulteur.
                FitSewerModel(CryptModel(GalleryLooks.Folder, GalleryLooks.VaultModel), "Voûte de cristal", x0 - 0.5f, (float)gap.MinZ, x1 + 0.5f, (float)gap.MaxZ, (float)GalleryLooks.VaultHeight, true);
            }
        }

        /// <summary>Une porte des galeries : le jour des pierres levées sur celle qui mène dehors ; la faille contre la paroi la plus proche sur une porte de genre faille.</summary>
        private void GalleryDoor(MapDoor door, float minX, float minZ, float maxX, float maxZ)
        {
            var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
            if (door.Outside)
            {
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour des pierres levées (" + door.Id + ")", glow.x, glow.z, new Color(0.78f, 0.78f, 0.82f), 0f, (float)CaveLooks.CurtainReach));
            }
            if (GalleryLooks.DoorModel(door) == null) return;
            var rift = GalleryLooks.RiftAt(door.X, door.Z, minX, minZ, maxX, maxZ);
            if (!rift.HasValue) return;
            float rx0 = (float)rift.Value.MinX, rz0 = (float)rift.Value.MinZ, rx1 = (float)rift.Value.MaxX, rz1 = (float)rift.Value.MaxZ;
            CaveGlow("Faille (" + door.Id + ")", (rx0 + rx1) / 2f, (rz0 + rz1) / 2f, GalleryGlowColor, 1.2f, 5f);
            if (FitSewerModel(CryptModel(GalleryLooks.Folder, GalleryLooks.RiftModel), "Faille (" + door.Id + ")", rx0, rz0, rx1, rz1, (float)GalleryLooks.RiftHeight, false)) return;
            // Faille de remplacement : une fente sombre bordée de cristal contre la paroi.
            Box("Faille (" + door.Id + ")", new Vector3((rx0 + rx1) / 2f, (float)GalleryLooks.RiftHeight / 2f, (rz0 + rz1) / 2f),
                new Vector3(rx1 - rx0, (float)GalleryLooks.RiftHeight, rz1 - rz0), Plain(new Color(0.05f, 0.03f, 0.08f)), 1f);
        }

        /// <summary>
        /// Côté surface : sur la porte qui descend aux galeries, l'entrée entre les pierres levées (marches vers le centre du cercle,
        /// <see cref="GalleryLooks.EntranceYaw"/>), posée sur le sol affiché ; modèle, ou dalle sombre bordée de cristaux.
        /// </summary>
        private void GalleryEntrance()
        {
            float minX = Raw(_space.Min.X.Raw) - BourgMargin, maxX = Raw(_space.Max.X.Raw) + BourgMargin;
            float minZ = Raw(_space.Min.Z.Raw) - BourgMargin, maxZ = Raw(_space.Max.Z.Raw) + BourgMargin;
            foreach (MapDoor door in _doors)
            {
                string name = GalleryLooks.SurfaceModel(door.Id);
                if (name == null) continue;
                float x = (float)door.X, z = (float)door.Z;
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;
                float side = (float)GalleryLooks.EntranceSide, half = side / 2f;
                float ground = Ground(x, z, half);
                float yaw = (float)GalleryLooks.EntranceYaw(x, z);
                if (FitSewerModel(CryptModel(GalleryLooks.Folder, name), "Entrée des galeries (" + door.Id + ")", x - half, z - half, x + half, z + half,
                        (float)GalleryLooks.EntranceHeight, false, ground - 0.6f, yaw))
                    continue;
                Box("Entrée des galeries (" + door.Id + ")", new Vector3(x, ground + 0.02f, z), new Vector3(side * 0.7f, 0.04f, side * 0.7f), Plain(new Color(0.04f, 0.03f, 0.06f)), 1f);
            }
        }
    }
}
