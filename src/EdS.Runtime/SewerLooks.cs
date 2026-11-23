using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.49.2 : ce que dessine un obstacle des égouts du Bourg (grand collecteur et vieille citerne).</summary>
    public enum SewerPiece
    {
        /// <summary>Rien de propre aux égouts.</summary>
        None = 0,
        /// <summary>Mur de brique et de moellons (occulteur).</summary>
        Wall = 1,
        /// <summary>Pilier de brique de la voûte (occulteur).</summary>
        Pillar = 2,
        /// <summary>Tronçon du canal (eau, bas : on tire par-dessus) ; les passerelles sont posées sur l'écart entre deux tronçons.</summary>
        Canal = 3,
        /// <summary>Bassin de la vieille citerne (eau, bas).</summary>
        Basin = 4,
        /// <summary>Grille de fer, de part et d'autre d'un passage (la voûte est posée sur l'écart).</summary>
        Grate = 5,
        /// <summary>Tuyau qui crache dans le canal.</summary>
        Pipe = 6,
        /// <summary>Déversoir maçonné contre un mur.</summary>
        Outfall = 7,
        /// <summary>Caisses abandonnées des contrebandiers.</summary>
        Crates = 8,
        /// <summary>Repaire (du passeur, de la bête) : sa lumière.</summary>
        Lair = 9,
    }

    /// <summary>
    /// Chantier V2.49.2 (égouts du Bourg, PLAN_GRANDE_CARTE.md, ligne V2.49) : rendu des égouts, sans Unity. Les obstacles prennent une pièce du
    /// kit d'égout par le début de leur identifiant (« canal_egout_centre » : le canal) ; les modèles (kit Tripo préparé dans l'atelier,
    /// <c>Egouts/modeles.json</c>, 13 modèles, 390 crédits au plus, **non dépensés**) sont cherchés dans <see cref="AssetFolder"/> et, s'ils
    /// manquent, Unity pose une forme de remplacement. Passerelles sur l'écart entre deux tronçons du canal (<see cref="Bridges"/>), voûtes
    /// sur l'écart entre deux grilles et sur chaque passerelle (<see cref="Arches"/>), échelle aux portes qui montent (puits, bouche du marché),
    /// jour du dehors sur les portes de la surface ; côté Bourg, la bouche d'égout au sol de la place du marché et le déversoir dans le fossé
    /// (<see cref="SurfaceModel"/>). Les grands murs et le canal sont posés en tuiles (<see cref="CryptLooks.Tiles"/>), les autres pièces mises
    /// à l'échelle de leur emprise sans être étirées de plus de <see cref="CryptLooks.MaxStretch"/> (<see cref="Fit"/>). Affichage seulement.
    /// </summary>
    public static class SewerLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Egouts/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Egouts/";

        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Egouts";

        public const string VaultModel = "voute_egout", BridgeModel = "passerelle_egout", LadderModel = "echelle_egout", ManholeModel = "bouche_egout",
            OutfallModel = "deversoir_egout";

        /// <summary>
        /// Les treize modèles du kit (codes E01 à E13 de l'atelier, <c>Egouts/modeles.json</c>), dans l'ordre des codes. Les noms sont ceux des
        /// fichiers attendus.
        /// </summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            "mur_egout", VaultModel, "canal_egout", BridgeModel, "grille_egout", LadderModel, ManholeModel, "tuyau_egout", OutfallModel,
            "caisses_egout", "repaire_egout", "pilier_egout", "bassin_egout",
        };

        private static readonly (string Prefix, SewerPiece Piece)[] Prefixes =
        {
            ("mur_egout", SewerPiece.Wall),
            ("pilier_egout", SewerPiece.Pillar),
            ("canal_egout", SewerPiece.Canal),
            ("bassin_egout", SewerPiece.Basin),
            ("grille_egout", SewerPiece.Grate),
            ("tuyau_egout", SewerPiece.Pipe),
            ("deversoir_egout", SewerPiece.Outfall),
            ("caisses_egout", SewerPiece.Crates),
            ("repaire_egout", SewerPiece.Lair),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="SewerPiece.None"/> sinon.</summary>
        public static SewerPiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return SewerPiece.None;
            foreach ((string prefix, SewerPiece piece) in Prefixes)
                if (obstacleId == prefix || (obstacleId.Length > prefix.Length && obstacleId.StartsWith(prefix, StringComparison.Ordinal) && obstacleId[prefix.Length] == '_'))
                    return piece;
            return SewerPiece.None;
        }

        /// <summary>Nom du modèle de <paramref name="piece"/> ; null pour <see cref="SewerPiece.None"/>.</summary>
        public static string? ModelFor(SewerPiece piece) => piece switch
        {
            SewerPiece.Wall => Models[0],
            SewerPiece.Canal => Models[2],
            SewerPiece.Grate => Models[4],
            SewerPiece.Pipe => Models[7],
            SewerPiece.Outfall => Models[8],
            SewerPiece.Crates => Models[9],
            SewerPiece.Lair => Models[10],
            SewerPiece.Pillar => Models[11],
            SewerPiece.Basin => Models[12],
            _ => null,
        };

        /// <summary>Le mur et le pilier cachent le personnage : ils sont enregistrés comme occulteurs (fondus ou coupés comme les autres murs).</summary>
        public static bool Occludes(SewerPiece piece) => piece == SewerPiece.Wall || piece == SewerPiece.Pillar;

        /// <summary>Eau (canal, bassin) : posée au ras du sol, sombre.</summary>
        public static bool IsWater(SewerPiece piece) => piece == SewerPiece.Canal || piece == SewerPiece.Basin;

        /// <summary>
        /// Hauteur d'une pièce posée (m), modèle ou forme de remplacement, celle de sa fiche (<c>Egouts/modeles.json</c>) ; les murs et piliers à
        /// la hauteur des murs des dessous. Elle ne suit jamais l'emprise.
        /// </summary>
        public static double HeightOf(SewerPiece piece) => piece switch
        {
            SewerPiece.Wall => CaveLooks.WallHeight,
            SewerPiece.Pillar => CaveLooks.WallHeight,
            SewerPiece.Canal => 0.8,
            SewerPiece.Basin => 1.0,
            SewerPiece.Grate => 3.0,
            SewerPiece.Pipe => 1.4,
            SewerPiece.Outfall => 2.5,
            SewerPiece.Crates => 1.6,
            SewerPiece.Lair => 2.5,
            _ => 1.5,
        };

        /// <summary>
        /// Taille au sol (long côté, petit côté, en mètres) d'un modèle répété en tuiles le long de son emprise (celle de sa fiche) : le mur et le
        /// canal ; null pour une pièce posée d'un seul tenant.
        /// </summary>
        public static (double Long, double Short)? TileOf(SewerPiece piece) => piece switch
        {
            SewerPiece.Wall => (8.0, 1.5),
            SewerPiece.Canal => (8.0, 4.0),
            _ => null,
        };

        /// <summary>Hauteur de la voûte (m), de la passerelle (m), de l'échelle (m), de la bouche d'égout et du déversoir de la surface (m) : celles des fiches.</summary>
        public const double VaultHeight = 4.0, BridgeHeight = 1.2, LadderHeight = 4.5, ManholeHeight = 0.3, OutfallHeight = 2.5;

        /// <summary>Taille au sol de l'échelle (largeur le long du mur, épaisseur), de la bouche (côté) et du déversoir (largeur, profondeur), en mètres.</summary>
        public const double LadderWidth = 1.2, LadderDepth = 0.4, ManholeSide = 1.6, OutfallWidth = 2.0, OutfallDepth = 1.5;

        /// <summary>
        /// Pose d'un modèle d'une seule pièce sur une emprise, comme <see cref="MineLooks.Fit"/> (quart de tour si son long côté n'est pas dans le
        /// sens de l'emprise, hauteur de la fiche), mais jamais étiré : sur le sol, l'échelle d'un axe vaut au plus <see cref="CryptLooks.MaxStretch"/>
        /// fois celle de l'autre (le modèle ne remplit alors pas toute l'emprise, centré sur elle). Tailles nulles : aucune pose.
        /// </summary>
        public static (bool Turn, double Sx, double Sy, double Sz) Fit(double modelX, double modelY, double modelZ, double width, double depth, double height)
        {
            var fit = MineLooks.Fit(modelX, modelY, modelZ, width, depth, height);
            if (!(modelX > 0) || !(modelY > 0) || !(modelZ > 0) || !(width > 0) || !(depth > 0) || !(height > 0)) return fit;
            double sx = fit.Sx, sz = fit.Sz;
            if (sx > sz * CryptLooks.MaxStretch) sx = sz * CryptLooks.MaxStretch;
            if (sz > sx * CryptLooks.MaxStretch) sz = sx * CryptLooks.MaxStretch;
            return (fit.Turn, sx, fit.Sy, sz);
        }

        /// <summary>
        /// Nom du modèle du kit (<see cref="Models"/>) du fichier <paramref name="path"/>, ou null : autre dossier, sous-dossier, autre extension
        /// que .fbx (casse ignorée) ou nom inconnu.
        /// </summary>
        public static string? NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p = path.Replace('\\', '/');
            if (!p.StartsWith(AssetFolder + "/", StringComparison.Ordinal)) return null;
            string file = p.Substring(AssetFolder.Length + 1);
            if (file.IndexOf('/') >= 0 || !file.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return null;
            string name = file.Substring(0, file.Length - 4);
            foreach (string m in Models)
                if (m == name) return m;
            return null;
        }

        /// <summary>Vrai si l'un des obstacles est une pièce d'égout : le dessous est un égout (ni rideau d'eau, ni kit de mine ou de crypte).</summary>
        public static bool IsSewer(IEnumerable<SpaceObstacle> obstacles) => obstacles != null && obstacles.Any(o => o != null && PieceFor(o.Id) != SewerPiece.None);

        private static List<(long MinX, long MaxX, long MinZ, long MaxZ)> Boxes(IEnumerable<SpaceObstacle> obstacles, SewerPiece piece) =>
            (obstacles ?? Array.Empty<SpaceObstacle>()).Where(o => o != null && PieceFor(o.Id) == piece && o.Points.Count > 0)
                .Select(o => (MinX: o.Points.Min(p => p.X.Raw), MaxX: o.Points.Max(p => p.X.Raw), MinZ: o.Points.Min(p => p.Z.Raw), MaxZ: o.Points.Max(p => p.Z.Raw)))
                .OrderBy(a => a.MinZ).ThenBy(a => a.MinX).ToList();

        /// <summary>
        /// Écarts le long de l'axe des X entre deux pièces consécutives (rangées par Z puis d'ouest en est) qui partagent une bande de Z
        /// (rectangles [MinX, MaxX] × [MinZ, MaxZ], en mètres). Un écart nul ou une bande vide ne donne rien.
        /// </summary>
        private static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Gaps(List<(long MinX, long MaxX, long MinZ, long MaxZ)> boxes)
        {
            var result = new List<(double, double, double, double)>();
            for (int i = 0; i + 1 < boxes.Count; i++)
            {
                var a = boxes[i];
                var b = boxes[i + 1];
                long z0 = Math.Max(a.MinZ, b.MinZ), z1 = Math.Min(a.MaxZ, b.MaxZ);
                if (b.MinX <= a.MaxX || z1 <= z0) continue;
                result.Add((a.MaxX / 10000.0, z0 / 10000.0, b.MinX / 10000.0, z1 / 10000.0));
            }
            return result;
        }

        /// <summary>Passerelles : l'écart entre deux tronçons consécutifs du canal, sur la bande de Z qu'ils partagent (la passerelle le franchit du sud au nord).</summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Bridges(IEnumerable<SpaceObstacle> obstacles) => Gaps(Boxes(obstacles, SewerPiece.Canal));

        /// <summary>Voûtes : sur l'écart entre deux grilles consécutives (comme dans les cryptes), puis sur chaque passerelle.</summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Arches(IEnumerable<SpaceObstacle> obstacles)
        {
            var list = obstacles?.ToList() ?? new List<SpaceObstacle>();
            return Gaps(Boxes(list, SewerPiece.Grate)).Concat(Bridges(list)).ToList();
        }

        /// <summary>
        /// Ce qui est posé à une porte des égouts : l'échelle pour un puits ou pour une porte qui remonte par une bouche d'égout ; rien pour le
        /// déversoir (porte <c>deversoir</c> : on en sort à pied, le jour du fossé suffit) ni pour un autre genre.
        /// </summary>
        public static string? DoorModel(MapDoor door)
        {
            if (door == null) throw new ArgumentNullException(nameof(door));
            if (door.Kind == DoorKind.Shaft) return LadderModel;
            if (door.Kind == DoorKind.Door && door.Id != OutfallDoor) return LadderModel;
            return null;
        }

        /// <summary>Identifiant de la porte du déversoir, côté égouts.</summary>
        public const string OutfallDoor = "deversoir";

        /// <summary>
        /// Emprise de l'échelle d'une porte (x, z) d'une aire [minX, maxX] × [minZ, maxZ] : plaquée contre le bord le plus proche
        /// (<see cref="CaveLooks.CurtainFacing"/>), centrée sur la porte le long du bord, épaisse de <see cref="LadderDepth"/> ; la place
        /// d'arrivée (la porte) reste libre tant que la porte est à plus de <see cref="LadderDepth"/> du bord. Vide si la porte est sur le bord.
        /// </summary>
        public static (double MinX, double MinZ, double MaxX, double MaxZ)? LadderAt(double x, double z, double minX, double minZ, double maxX, double maxZ)
        {
            var facing = CaveLooks.CurtainFacing(x, z, minX, minZ, maxX, maxZ);
            double ox = facing.OutX, oz = facing.OutZ, half = LadderWidth / 2;
            double toEdge = ox < 0 ? x - minX : ox > 0 ? maxX - x : oz < 0 ? z - minZ : maxZ - z;
            if (!(toEdge > LadderDepth)) return null;
            if (ox < 0) return (minX, z - half, minX + LadderDepth, z + half);
            if (ox > 0) return (maxX - LadderDepth, z - half, maxX, z + half);
            if (oz < 0) return (x - half, minZ, x + half, minZ + LadderDepth);
            return (x - half, maxZ - LadderDepth, x + half, maxZ);
        }

        /// <summary>
        /// Côté Bourg : la pièce posée sur une porte des Vallons qui descend aux égouts, d'après son identifiant : la bouche d'égout (au sol)
        /// sur celle du marché, le déversoir sur celle du fossé ; null pour toute autre porte.
        /// </summary>
        public static string? SurfaceModel(string doorId) => doorId switch
        {
            "porte_egouts_marche" => ManholeModel,
            "porte_egouts_deversoir" => OutfallModel,
            _ => null,
        };

        /// <summary>Lumière du repaire du passeur (lanterne, chaude) et de la bête (lueur verdâtre de l'eau croupie).</summary>
        public static readonly (double R, double G, double B) LanternColor = (1.0, 0.72, 0.42), MireColor = (0.45, 0.7, 0.4);

        /// <summary>Portée (m) et intensité de la lanterne d'un repaire, puis de la lueur de l'eau croupie.</summary>
        public const double LanternRange = 7.0, LanternIntensity = 1.0, MireRange = 6.0, MireIntensity = 0.5;

        /// <summary>Couleur de l'eau du canal et du bassin (forme de remplacement).</summary>
        public static readonly (double R, double G, double B) Water = (0.12, 0.16, 0.1);
    }
}
