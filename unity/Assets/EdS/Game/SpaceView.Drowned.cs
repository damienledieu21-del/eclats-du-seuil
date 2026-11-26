using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>
    /// Chantier V2.50.1b (grottes noyées du lac) : décor des grottes noyées, dans la pénombre d'un dessous (<see cref="Cave"/> : ni pluie ni
    /// neige, ni leur son). Parois de calcaire, piliers des voûtes basses (la voûte posée dessus), rochers et épaves (occulteurs), bassins (l'eau
    /// du jeu, « EdS/Eau » ou l'eau des samples, au ras du sol), algues et coffres engloutis d'après les obstacles
    /// (<see cref="DrownedLooks.PieceFor"/>) ; devant chaque porte d'eau, le passage noyé et son eau, qui monte et descend avec la marée
    /// (<see cref="DrownedLooks.PoolAt"/>, <see cref="DrownedLooks.PoolShown"/>) ; côté marais, l'entrée dans les roselières
    /// (<see cref="DrownedEntrances"/>). Modèles pris dans <c>Resources/Noyees/&lt;nom&gt;</c> (<see cref="DrownedLooks.Models"/>) ; en
    /// repli (kit préparé, pas encore généré), des formes simples. Pose : <see cref="FitSewerModel"/> (échelle avant lacet, sans cisaillement),
    /// murs en tuiles d'après les bornes réelles du modèle.
    /// </summary>
    public sealed partial class SpaceView
    {
        private bool _drowned;

        /// <summary>Eau des passages de marée (à sec à marée basse), montrée à marée haute seulement (<see cref="Tide"/>).</summary>
        private readonly List<GameObject> _tidePools = new List<GameObject>();

        /// <summary>
        /// V2.50.1b : la marée du lac (haute ou basse), donnée à chaque image par le jeu : l'eau des passages de marée se montre à marée haute
        /// seulement. Ailleurs que dans une grotte noyée : rien.
        /// </summary>
        public void Tide(bool high)
        {
            if (!_drowned) return;
            foreach (GameObject pool in _tidePools)
                if (pool != null && pool.activeSelf != DrownedLooks.PoolShown(true, true, high)) pool.SetActive(DrownedLooks.PoolShown(true, true, high));
        }

        /// <summary>Dessine l'obstacle <paramref name="o"/> s'il est une pièce de grotte noyée ; faux sinon.</summary>
        private bool DrownedObstacle(SpaceObstacle o, Material rock, float rockTile, Material wood, float woodTile)
        {
            DrownedPiece piece = DrownedLooks.PieceFor(o.Id);
            if (piece == DrownedPiece.None) return false;
            var points = o.Points.Select(p => new Vector3(Raw(p.X.Raw), 0f, Raw(p.Z.Raw))).ToList();
            float x0 = points.Min(p => p.x), x1 = points.Max(p => p.x), z0 = points.Min(p => p.z), z1 = points.Max(p => p.z);
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, height = (float)DrownedLooks.HeightOf(piece);
            bool occludes = DrownedLooks.Occludes(piece);
            if (DrownedLooks.IsWater(piece)) _waters.Add(Prism(o.Id + " eau", points.Select(p => new Vector3(p.x, 0.04f, p.z)).ToList(), 0.04f, _water, false));
            GameObject model = CryptModel(DrownedLooks.Folder, DrownedLooks.ModelFor(piece));
            if (model != null && DrownedLooks.Tiled(piece) && Footprint(model, out float modelLong, out float modelShort))
            {
                // Les parois sont répétées en tuiles bout à bout, d'après les bornes réelles du modèle (sans trou, étirées en longueur seulement).
                int n = 0;
                foreach (var t in SewerLooks.WallTiles(x0, z0, x1, z1, modelLong, modelShort))
                    FitSewerModel(model, o.Id + " (tuile " + (++n) + ")", (float)t.MinX, (float)t.MinZ, (float)t.MaxX, (float)t.MaxZ, height, occludes, 0f, 0f, true);
                return true;
            }
            if (piece == DrownedPiece.Vault)
            {
                // Le pilier porte la voûte basse : le modèle de la voûte l'enjambe (un mètre de chaque côté), sinon un pilier de roche.
                if (FitSewerModel(model, o.Id, x0 - 1f, z0, x1 + 1f, z1, height, true)) return true;
                Prism(o.Id, points, height, rock, true, rockTile);
                return true;
            }
            if (FitSewerModel(model, o.Id, x0, z0, x1, z1, height, occludes)) return true;
            // Formes de remplacement.
            Material limestone = Tinted(rock, new Color(0.62f, 0.66f, 0.64f), "calcaire mouillé");
            switch (piece)
            {
                case DrownedPiece.Wall:
                case DrownedPiece.Rock:
                    Prism(o.Id, points, height, limestone, true, rockTile);
                    break;
                case DrownedPiece.Basin:
                    Box(o.Id + " (margelle)", new Vector3(cx, 0.08f, cz), new Vector3(x1 - x0 + 0.3f, 0.06f, z1 - z0 + 0.3f), limestone, rockTile);
                    break;
                case DrownedPiece.Algae:
                    Prism(o.Id, points, height, Plain(new Color(0.16f, 0.3f, 0.18f)), false, 1f);
                    break;
                case DrownedPiece.Wreck:
                    Prism(o.Id, points, height, Tinted(wood, new Color(0.45f, 0.5f, 0.42f), "bois noyé"), true, woodTile);
                    break;
                default:   // coffre englouti
                    Prism(o.Id, points, height, wood, false, woodTile);
                    break;
            }
            return true;
        }

        /// <summary>
        /// Une porte des grottes noyées : le jour du marais sur celle qui mène dehors (les roselières) ; devant une porte d'eau, le passage noyé
        /// (modèle, ou arche de roche) et son eau tournée vers la paroi, avec sa lueur ; l'eau d'un passage de marée suit la marée.
        /// </summary>
        private void DrownedDoor(MapDoor door, float minX, float minZ, float maxX, float maxZ, Material rock, float rockTile)
        {
            var facing = CaveLooks.CurtainFacing(door.X, door.Z, minX, minZ, maxX, maxZ);
            Vector3 at = new Vector3((float)door.X, 0f, (float)door.Z);
            Vector3 outward = new Vector3((float)facing.OutX, 0f, (float)facing.OutZ);
            if (door.Outside)
            {
                Vector3 glow = at - outward * 1.5f;
                _curtains.Add(CaveGlow("Jour du marais (" + door.Id + ")", glow.x, glow.z, new Color(0.78f, 0.84f, 0.8f), 0f, (float)CaveLooks.CurtainReach));
            }
            if (door.Water == null) return;
            var pool = DrownedLooks.PoolAt(door.X, door.Z, minX, minZ, maxX, maxZ);
            if (!pool.HasValue) return;
            float px0 = (float)pool.Value.MinX, pz0 = (float)pool.Value.MinZ, px1 = (float)pool.Value.MaxX, pz1 = (float)pool.Value.MaxZ;
            var surface = new List<Vector3> { new Vector3(px0, 0.05f, pz0), new Vector3(px1, 0.05f, pz0), new Vector3(px1, 0.05f, pz1), new Vector3(px0, 0.05f, pz1) };
            GameObject water = Prism("Passage noyé, eau (" + door.Id + ")", surface, 0.04f, _water, false);
            _waters.Add(water);
            var c = DrownedLooks.PoolGlowColor;
            Light light = CaveGlow("Lueur du passage noyé (" + door.Id + ")", (px0 + px1) / 2f, (pz0 + pz1) / 2f, new Color((float)c.R, (float)c.G, (float)c.B),
                (float)DrownedLooks.PoolGlowIntensity, (float)DrownedLooks.PoolGlowRange);
            if (door.Water.Tide)
            {
                _tidePools.Add(water);
                if (light != null) _tidePools.Add(light.gameObject);
            }
            if (FitSewerModel(CryptModel(DrownedLooks.Folder, DrownedLooks.PassageModel), "Passage noyé (" + door.Id + ")", px0, pz0, px1, pz1,
                    (float)DrownedLooks.PassageHeight, false))
                return;
            // Passage de remplacement : une arche de roche basse au-dessus de l'eau, contre la paroi.
            bool alongX = facing.OutZ != 0;
            float cx = (px0 + px1) / 2f, cz = (pz0 + pz1) / 2f, h = (float)DrownedLooks.PassageHeight;
            Material limestone = Tinted(rock, new Color(0.62f, 0.66f, 0.64f), "calcaire mouillé");
            Vector3 side = alongX ? new Vector3((px1 - px0) / 2f, 0f, 0f) : new Vector3(0f, 0f, (pz1 - pz0) / 2f);
            Vector3 post = alongX ? new Vector3(0.5f, h, pz1 - pz0) : new Vector3(px1 - px0, h, 0.5f);
            Box("Passage noyé (montant 1, " + door.Id + ")", new Vector3(cx, h / 2f, cz) - side, post, limestone, rockTile);
            Box("Passage noyé (montant 2, " + door.Id + ")", new Vector3(cx, h / 2f, cz) + side, post, limestone, rockTile);
            Box("Passage noyé (voûte, " + door.Id + ")", new Vector3(cx, h - 0.3f, cz), new Vector3(px1 - px0, 0.6f, pz1 - pz0), limestone, rockTile);
        }

        /// <summary>
        /// Côté marais (la surface) : sur la porte qui descend aux grottes noyées, l'entrée dans les roselières, posée sur le sol affiché
        /// (<see cref="DrownedLooks.SurfaceModel"/>) ; modèle, ou bouche de roche sombre entre deux touffes de roseaux.
        /// </summary>
        private void DrownedEntrances()
        {
            float minX = Raw(_space.Min.X.Raw) - BourgMargin, maxX = Raw(_space.Max.X.Raw) + BourgMargin;
            float minZ = Raw(_space.Min.Z.Raw) - BourgMargin, maxZ = Raw(_space.Max.Z.Raw) + BourgMargin;
            foreach (MapDoor door in _doors)
            {
                string name = DrownedLooks.SurfaceModel(door.Id);
                if (name == null) continue;
                float x = (float)door.X, z = (float)door.Z;
                if (x < minX || x > maxX || z < minZ || z > maxZ) continue;   // hors de la fenêtre affichée
                float w = (float)DrownedLooks.ReedsWidth, d = (float)DrownedLooks.ReedsDepth, h = (float)DrownedLooks.ReedsHeight;
                float ground = Ground(x, z, Mathf.Max(w, d) / 2f);
                // L'entrée regarde le nord (le chemin des rives) ; elle se tient derrière la place de la porte, côté lac.
                if (FitSewerModel(CryptModel(DrownedLooks.Folder, name), "Grottes noyées (" + door.Id + ")", x - w / 2f, z - d - 0.5f, x + w / 2f, z - 0.5f, h, false, ground))
                    continue;
                Material stone = Built(StyleSurface.Stone, new Color(0.36f, 0.37f, 0.36f), out float tile);
                Material reeds = Plain(new Color(0.42f, 0.5f, 0.26f));
                Box("Grottes noyées (bouche, " + door.Id + ")", new Vector3(x, ground + 0.9f, z - 2.5f), new Vector3(3f, 1.8f, 2f), stone, tile);
                Box("Grottes noyées (roseaux ouest, " + door.Id + ")", new Vector3(x - 2.4f, ground + 0.9f, z - 2f), new Vector3(1.2f, 1.8f, 1.6f), reeds, 1f);
                Box("Grottes noyées (roseaux est, " + door.Id + ")", new Vector3(x + 2.4f, ground + 0.9f, z - 2f), new Vector3(1.2f, 1.8f, 1.6f), reeds, 1f);
            }
        }
    }
}
