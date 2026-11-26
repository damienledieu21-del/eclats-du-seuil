using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.50b : ce que dessine un obstacle des galeries sous les pierres du Seuil.</summary>
    public enum GalleryPiece
    {
        /// <summary>Rien de propre aux galeries.</summary>
        None = 0,
        /// <summary>Paroi de basalte veinée de cristal (occulteur, en tuiles).</summary>
        Wall = 1,
        /// <summary>Éclat géant : un cristal plus haut que les parois (occulteur, sa lueur).</summary>
        GiantShard = 2,
        /// <summary>Amas de cristaux, bas (on tire par-dessus), sa lueur.</summary>
        Crystals = 3,
        /// <summary>Pilier de basalte pris dans le cristal (occulteur).</summary>
        Pillar = 4,
        /// <summary>Tronçon du gouffre (bas, en tuiles) ; les ponts de pierre sont posés sur l'écart entre deux tronçons.</summary>
        Chasm = 5,
        /// <summary>Autel des éclats (bas), sa lueur.</summary>
        Altar = 6,
        /// <summary>Éboulis de roche et de cristal (bas).</summary>
        Rubble = 7,
    }

    /// <summary>
    /// Chantier V2.50b (galeries sous les pierres du Seuil, PLAN_GRANDE_CARTE.md, ligne V2.50) : rendu des galeries, sans Unity. Les obstacles
    /// prennent une pièce du kit de galeries cristallines par le début de leur identifiant (« eclat_geant_salle_2 » : un éclat géant) ; les
    /// modèles (kit Tripo préparé dans l'atelier, <c>Galeries/modeles.json</c>, 11 modèles, 330 crédits au plus, **non dépensés**) sont cherchés
    /// dans <see cref="AssetFolder"/> et, s'ils manquent, Unity pose une forme simple. Ponts de pierre sur l'écart entre deux tronçons du gouffre
    /// (<see cref="Bridges"/>), faille contre la paroi aux portes de genre faille (<see cref="RiftAt"/>), jour du dehors aux portes de la surface ;
    /// côté surface, l'entrée entre les pierres levées (<see cref="SurfaceModel"/>). La paroi et le gouffre sont posés en tuiles
    /// (<see cref="SewerLooks.WallTiles"/>, d'après les bornes réelles du modèle), les autres pièces sans être étirées
    /// (<see cref="SewerLooks.Fit"/>). Affichage seulement.
    /// </summary>
    public static class GalleryLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Galeries/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Galeries/";

        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Galeries";

        public const string BridgeModel = "pont_pierre", RiftModel = "faille_eclats", EntranceModel = "entree_galeries", VaultModel = "voute_cristal";

        /// <summary>Les onze modèles du kit (codes G01 à G11 de l'atelier, <c>Galeries/modeles.json</c>), dans l'ordre des codes.</summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            "paroi_cristal", "eclat_geant", "amas_cristal", "pilier_cristal", "gouffre_cristal", BridgeModel, "autel_eclats", "eboulis_cristal",
            RiftModel, EntranceModel, VaultModel,
        };

        private static readonly (string Prefix, GalleryPiece Piece)[] Prefixes =
        {
            ("paroi_cristal", GalleryPiece.Wall),
            ("eclat_geant", GalleryPiece.GiantShard),
            ("amas_cristal", GalleryPiece.Crystals),
            ("pilier_cristal", GalleryPiece.Pillar),
            ("gouffre_cristal", GalleryPiece.Chasm),
            ("autel_eclats", GalleryPiece.Altar),
            ("eboulis_cristal", GalleryPiece.Rubble),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="GalleryPiece.None"/> sinon.</summary>
        public static GalleryPiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return GalleryPiece.None;
            foreach ((string prefix, GalleryPiece piece) in Prefixes)
                if (obstacleId == prefix || (obstacleId.Length > prefix.Length && obstacleId.StartsWith(prefix, StringComparison.Ordinal) && obstacleId[prefix.Length] == '_'))
                    return piece;
            return GalleryPiece.None;
        }

        /// <summary>Nom du modèle de <paramref name="piece"/> ; null pour <see cref="GalleryPiece.None"/>.</summary>
        public static string? ModelFor(GalleryPiece piece) => piece switch
        {
            GalleryPiece.Wall => Models[0],
            GalleryPiece.GiantShard => Models[1],
            GalleryPiece.Crystals => Models[2],
            GalleryPiece.Pillar => Models[3],
            GalleryPiece.Chasm => Models[4],
            GalleryPiece.Altar => Models[6],
            GalleryPiece.Rubble => Models[7],
            _ => null,
        };

        /// <summary>La paroi, le pilier et l'éclat géant cachent le personnage : occulteurs (fondus ou coupés comme les autres murs).</summary>
        public static bool Occludes(GalleryPiece piece) => piece == GalleryPiece.Wall || piece == GalleryPiece.Pillar || piece == GalleryPiece.GiantShard;

        /// <summary>
        /// Hauteur d'une pièce posée (m), celle de sa fiche (<c>Galeries/modeles.json</c>) ; paroi et pilier à la hauteur des murs des dessous.
        /// Elle ne suit jamais l'emprise.
        /// </summary>
        public static double HeightOf(GalleryPiece piece) => piece switch
        {
            GalleryPiece.Wall => CaveLooks.WallHeight,
            GalleryPiece.Pillar => CaveLooks.WallHeight,
            GalleryPiece.GiantShard => 6.0,
            GalleryPiece.Crystals => 1.2,
            GalleryPiece.Chasm => 0.6,
            GalleryPiece.Altar => 1.4,
            GalleryPiece.Rubble => 0.8,
            _ => 1.5,
        };

        /// <summary>
        /// Taille au sol (long côté, petit côté, m) d'un modèle répété en tuiles (celle de sa fiche, pour les tests ; le jeu mesure les bornes
        /// réelles du modèle chargé) : la paroi et le gouffre ; null pour une pièce d'un seul tenant.
        /// </summary>
        public static (double Long, double Short)? TileOf(GalleryPiece piece) => piece switch
        {
            // Tailles au sol mesurées sur les modèles finis (Galeries/finir_galeries.py, 26 novembre 2026) ; le jeu mesure les bornes réelles.
            GalleryPiece.Wall => (8.0, 3.12),
            GalleryPiece.Chasm => (7.7, 5.06),
            _ => null,
        };

        /// <summary>Le gouffre est un creux : sa forme de remplacement est posée sous le sol, à cette profondeur (m), la lueur au fond.</summary>
        public const double ChasmDepth = 1.5;

        /// <summary>Hauteur (m) dont les lèvres du modèle du gouffre dépassent du sol.</summary>
        public const double ChasmLip = 0.15;

        /// <summary>
        /// Pose du modèle du gouffre (un creux profond, ouvert vers le haut, généré le 26 novembre 2026) : enfoncé dans le sol, son pied à
        /// <see cref="ChasmDepth"/> sous le sol et ses lèvres à <see cref="ChasmLip"/> au-dessus : on voit le creux d'en haut, jamais un bloc.
        /// </summary>
        public static (double Base, double Height) ChasmPose() => (-ChasmDepth, ChasmDepth + ChasmLip);

        /// <summary>
        /// Dalles d'un pont de pierre sur l'emprise [x0, x1] × [z0, z1] (le pont franchit le gouffre selon Z) pour un modèle de taille au sol
        /// <paramref name="modelLong"/> × <paramref name="modelShort"/> (bornes réelles ; le modèle généré est une passerelle étroite de
        /// 4 × 0,89 m) : chaque dalle couvre toute la longueur, le modèle mis à l'échelle de cette longueur ; il en faut assez, côte à côte, pour
        /// toute la largeur, chacune étirée en largeur de 2/3 à 1,5 fois au plus (sauf plafond <see cref="CryptLooks.MaxTiles"/>) : jamais de
        /// trou entre deux dalles. Vide pour une emprise ou une taille vide.
        /// </summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> BridgeSlabs(double x0, double z0, double x1, double z1, double modelLong, double modelShort)
        {
            var result = new List<(double, double, double, double)>();
            double width = x1 - x0, length = z1 - z0;
            if (!(width > 0) || !(length > 0) || !(modelLong > 0) || !(modelShort > 0)) return result;
            double slab = modelShort * length / modelLong;
            int n = (int)Math.Max(1, Math.Min(CryptLooks.MaxTiles, Math.Round(width / slab, MidpointRounding.AwayFromZero)));
            for (int i = 0; i < n; i++)
                result.Add((x0 + width * i / n, z0, i + 1 == n ? x1 : x0 + width * (i + 1) / n, z1));
            return result;
        }

        /// <summary>Lueur d'une pièce (couleur violette des cristaux du Seuil, intensité, portée en m) ; intensité 0 : aucune lumière.</summary>
        public static (double Intensity, double Range) GlowOf(GalleryPiece piece) => piece switch
        {
            GalleryPiece.GiantShard => (1.4, 7.0),
            GalleryPiece.Crystals => (0.8, 4.0),
            GalleryPiece.Altar => (1.6, 8.0),
            GalleryPiece.Chasm => (0.5, 5.0),
            _ => (0.0, 0.0),
        };

        /// <summary>Couleur de la lueur des cristaux du Seuil (violet pâle) et du cristal des formes de remplacement.</summary>
        public static readonly (double R, double G, double B) GlowColor = (0.7, 0.45, 1.0), CrystalColor = (0.62, 0.48, 0.92), Basalt = (0.22, 0.21, 0.25);

        /// <summary>Hauteur du pont de pierre (m), de la voûte (m), de la faille (m), de l'entrée côté surface (m) : celles des fiches.</summary>
        public const double BridgeHeight = 1.0, VaultHeight = 4.0, RiftHeight = 4.0, EntranceHeight = 1.5;

        /// <summary>Taille au sol de la faille (largeur le long de la paroi, épaisseur) et de l'entrée côté surface (côté), en mètres.</summary>
        public const double RiftWidth = 3.0, RiftDepth = 1.0, EntranceSide = 3.0;

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

        /// <summary>Vrai si l'un des obstacles est une pièce des galeries : le dessous est une galerie (ni rideau d'eau, ni autre kit).</summary>
        public static bool IsGallery(IEnumerable<SpaceObstacle> obstacles) => obstacles != null && obstacles.Any(o => o != null && PieceFor(o.Id) != GalleryPiece.None);

        /// <summary>
        /// Ponts de pierre : l'écart le long de l'axe des X entre deux tronçons consécutifs du gouffre (rangés par Z puis d'ouest en est) qui
        /// partagent une bande de Z ; le pont le franchit du sud au nord. Un écart nul ou une bande vide ne donne rien.
        /// </summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Bridges(IEnumerable<SpaceObstacle> obstacles)
        {
            var boxes = (obstacles ?? Array.Empty<SpaceObstacle>()).Where(o => o != null && PieceFor(o.Id) == GalleryPiece.Chasm && o.Points.Count > 0)
                .Select(o => (MinX: o.Points.Min(p => p.X.Raw), MaxX: o.Points.Max(p => p.X.Raw), MinZ: o.Points.Min(p => p.Z.Raw), MaxZ: o.Points.Max(p => p.Z.Raw)))
                .OrderBy(a => a.MinZ).ThenBy(a => a.MinX).ToList();
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

        /// <summary>Le pont déborde de tant (m) sur chaque rive du gouffre.</summary>
        public const double BridgeOverhang = 0.5;

        /// <summary>
        /// Emprise du pont posé sur l'écart <paramref name="span"/> : toute la largeur de l'écart, et le gouffre franchi de rive à rive avec
        /// <see cref="BridgeOverhang"/> de chaque côté (le pont couvre toujours le passage, sans trou).
        /// </summary>
        public static (double MinX, double MinZ, double MaxX, double MaxZ) BridgeSpan((double MinX, double MinZ, double MaxX, double MaxZ) span) =>
            (span.MinX, span.MinZ - BridgeOverhang, span.MaxX, span.MaxZ + BridgeOverhang);

        /// <summary>Ce qui est posé à une porte des galeries : la faille pour une porte de genre faille ; rien pour la porte du dehors (son jour suffit).</summary>
        public static string? DoorModel(MapDoor door)
        {
            if (door == null) throw new ArgumentNullException(nameof(door));
            return door.Kind == DoorKind.Rift ? RiftModel : null;
        }

        /// <summary>
        /// Emprise de la faille d'une porte (x, z) d'une aire [minX, maxX] × [minZ, maxZ] : plaquée contre le bord le plus proche
        /// (<see cref="CaveLooks.CurtainFacing"/>), centrée sur la porte le long du bord, épaisse de <see cref="RiftDepth"/> ; la place d'arrivée
        /// reste libre tant que la porte est à plus de <see cref="RiftDepth"/> du bord. Null si la porte est trop près du bord.
        /// </summary>
        public static (double MinX, double MinZ, double MaxX, double MaxZ)? RiftAt(double x, double z, double minX, double minZ, double maxX, double maxZ)
        {
            var facing = CaveLooks.CurtainFacing(x, z, minX, minZ, maxX, maxZ);
            double ox = facing.OutX, oz = facing.OutZ, half = RiftWidth / 2;
            double toEdge = ox < 0 ? x - minX : ox > 0 ? maxX - x : oz < 0 ? z - minZ : maxZ - z;
            if (!(toEdge > RiftDepth)) return null;
            if (ox < 0) return (minX, z - half, minX + RiftDepth, z + half);
            if (ox > 0) return (maxX - RiftDepth, z - half, maxX, z + half);
            if (oz < 0) return (x - half, minZ, x + half, minZ + RiftDepth);
            return (x - half, maxZ - RiftDepth, x + half, maxZ);
        }

        /// <summary>Côté surface : l'entrée des galeries sur la porte du cercle des pierres levées ; null pour toute autre porte.</summary>
        public static string? SurfaceModel(string doorId) => doorId == "porte_galeries_seuil" ? EntranceModel : null;

        /// <summary>Centre du cercle des pierres levées (source de la corruption, <c>corruption.json</c>), vers lequel les marches descendent.</summary>
        public static readonly (double X, double Z) CircleCentre = (1565.0, -625.0);

        /// <summary>
        /// Lacet (degrés, sens de Unity : 0 regarde vers +Z, 90 vers +X) de l'entrée posée en (<paramref name="x"/>, <paramref name="z"/>) : ses
        /// marches (l'avant du modèle, +Z) descendent vers le centre du cercle.
        /// </summary>
        public static double EntranceYaw(double x, double z) => Math.Atan2(CircleCentre.X - x, CircleCentre.Z - z) * 180.0 / Math.PI;

        /// <summary>
        /// Créatures de cristal (décision de Damien du 26 novembre 2026) : dossier de leurs modèles animés dans Unity
        /// (<c>Resources/Galeries/Creatures/&lt;nom&gt;</c>, kit Tripo préparé dans l'atelier <c>Galeries/Creatures</c>).
        /// </summary>
        public const string CreatureFolder = "Galeries/Creatures/", CreatureAssetFolder = "Assets/EdS/Modeles/Resources/Galeries/Creatures";

        /// <summary>Modèles des créatures de cristal (codes C01 et C02 de l'atelier ; C03, le veilleur en cristal, n'est pas encore branché).</summary>
        public const string SpiderModel = "araignee_cristal", GolemModel = "golem_cristal";

        /// <summary>Modèle animé de la créature <paramref name="id"/> : l'araignée ou le golem de cristal ; null pour toute autre.</summary>
        public static string? CreatureModel(string id) =>
            StyleRules.IsCrystalSpider(id) ? SpiderModel : StyleRules.IsCrystalGolem(id) ? GolemModel : null;

        /// <summary>Teinte de cristal (violet pâle) multipliée à la couleur du modèle de repli (loup, ours) d'une créature de cristal.</summary>
        public static readonly (double R, double G, double B) CrystalTint = (0.75, 0.6, 1.0);
    }
}
