using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.47.2 : ce que dessine un obstacle de la mine de la carrière (hors des pièces du kit de grotte).</summary>
    public enum MinePiece
    {
        /// <summary>Rien de propre à la mine.</summary>
        None = 0,
        /// <summary>Paroi de galerie boisée (roche et planches).</summary>
        Wall = 1,
        /// <summary>Étai de bois (et sa lanterne).</summary>
        Prop = 2,
        Rails = 3,
        Cart = 4,
        Vein = 5,
        Collapse = 6,
        Crates = 7,
        /// <summary>Cristaux du Seuil (violets, ils luisent).</summary>
        Crystals = 8,
        Chest = 9,
        /// <summary>Rivière souterraine (eau).</summary>
        River = 10,
    }

    /// <summary>
    /// Chantier V2.47.2 (mines de la carrière, PLAN_GRANDE_CARTE.md) : rendu de la mine, sans Unity. Les obstacles prennent une pièce du kit
    /// de mine par le début de leur identifiant (« paroi_mine_nord » : une paroi) ; les modèles (kit fait le 22 novembre 2026, 390 crédits validés
    /// par Damien) sont cherchés dans <see cref="AssetFolder"/> et, s'ils manquent, Unity pose une forme de remplacement. Les
    /// portes de genre puits reçoivent l'ascenseur, celles de genre escalier les échelles ; la passerelle est posée sur l'écart entre deux bras
    /// de la rivière souterraine (<see cref="Bridges"/>). La caverne du fond reprend les pièces du kit de grotte (<see cref="CaveLooks"/>).
    /// </summary>
    public static class MineLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Mines/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Mines/";

        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Mines";

        public const string LiftModel = "ascenseur_mine", LaddersModel = "echelles_mine", BridgeModel = "passerelle_mine", LanternModel = "lanterne_mine";

        /// <summary>
        /// Les treize modèles du kit (codes M01 à M13 de l'atelier, <c>Mines/modeles.json</c>), dans l'ordre des codes. Les noms sont ceux des
        /// fichiers attendus.
        /// </summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            "paroi_mine", "etai_mine", "rails_mine", "wagonnet_mine", LiftModel, "filon_mine", "effondrement_mine", BridgeModel, "cristaux_seuil",
            LanternModel, LaddersModel, "coffre_mine", "caisses_mine",
        };

        private static readonly (string Prefix, MinePiece Piece)[] Prefixes =
        {
            ("paroi_mine", MinePiece.Wall),
            ("etai_mine", MinePiece.Prop),
            ("rails_mine", MinePiece.Rails),
            ("wagonnet_mine", MinePiece.Cart),
            ("filon_mine", MinePiece.Vein),
            ("effondrement_mine", MinePiece.Collapse),
            ("caisses_mine", MinePiece.Crates),
            ("cristaux_seuil", MinePiece.Crystals),
            ("coffre_mine", MinePiece.Chest),
            ("riviere_mine", MinePiece.River),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="MinePiece.None"/> sinon.</summary>
        public static MinePiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return MinePiece.None;
            foreach ((string prefix, MinePiece piece) in Prefixes)
                if (obstacleId == prefix || (obstacleId.Length > prefix.Length && obstacleId.StartsWith(prefix, StringComparison.Ordinal) && obstacleId[prefix.Length] == '_'))
                    return piece;
            return MinePiece.None;
        }

        /// <summary>Nom du modèle du kit pour <paramref name="piece"/>, null pour <see cref="MinePiece.None"/> et la rivière (de l'eau).</summary>
        public static string? ModelFor(MinePiece piece) => piece switch
        {
            MinePiece.Wall => Models[0],
            MinePiece.Prop => Models[1],
            MinePiece.Rails => Models[2],
            MinePiece.Cart => Models[3],
            MinePiece.Vein => Models[5],
            MinePiece.Collapse => Models[6],
            MinePiece.Crystals => Models[8],
            MinePiece.Chest => Models[11],
            MinePiece.Crates => Models[12],
            _ => null,
        };

        /// <summary>Hauteur des formes de remplacement (m).</summary>
        public static double HeightOf(MinePiece piece) => piece switch
        {
            MinePiece.Wall => CaveLooks.WallHeight,
            MinePiece.Prop => 3.2,
            MinePiece.Rails => 0.12,
            MinePiece.Cart => 1.2,
            MinePiece.Vein => 2.0,
            MinePiece.Collapse => 2.2,
            MinePiece.Crates => 1.3,
            MinePiece.Crystals => 2.2,
            MinePiece.Chest => 0.8,
            MinePiece.River => 0.0,
            _ => 1.5,
        };

        /// <summary>
        /// Hauteur du modèle d'une pièce une fois posé (m), celle de sa fiche (<c>Mines/modeles.json</c>), la paroi à la hauteur des murs de la
        /// grotte. Elle ne suit jamais l'emprise : une pièce longue et plate (rails, passerelle) reste plate.
        /// </summary>
        public static double ModelHeightOf(MinePiece piece) => piece switch
        {
            MinePiece.Wall => CaveLooks.WallHeight,
            MinePiece.Prop => 3.2,
            MinePiece.Rails => 0.25,
            MinePiece.Cart => 1.3,
            MinePiece.Vein => 2.0,
            MinePiece.Collapse => 2.5,
            MinePiece.Crates => 1.4,
            MinePiece.Crystals => 2.2,
            MinePiece.Chest => 0.8,
            _ => 1.5,
        };

        /// <summary>Hauteur de la passerelle posée (m), celle de sa fiche.</summary>
        public const double BridgeHeight = 1.2;

        /// <summary>
        /// Pose d'un modèle de taille (<paramref name="modelX"/>, <paramref name="modelY"/>, <paramref name="modelZ"/>) sur une emprise de
        /// <paramref name="width"/> (X) sur <paramref name="depth"/> (Z) à la hauteur <paramref name="height"/> : tourné d'un quart de tour
        /// (<c>Turn</c>) si son long côté n'est pas dans le sens de l'emprise (les rails, longs sur X, suivent une voie le long de Z), puis
        /// étiré à l'emprise au sol ; l'échelle verticale ne dépend que de la hauteur voulue, jamais de l'emprise. Échelles à appliquer au
        /// modèle tourné, dans les axes du monde. Tailles nulles ou négatives : aucune pose (échelles 1, pas de quart de tour).
        /// </summary>
        public static (bool Turn, double Sx, double Sy, double Sz) Fit(double modelX, double modelY, double modelZ, double width, double depth, double height)
        {
            if (!(modelX > 0) || !(modelY > 0) || !(modelZ > 0) || !(width > 0) || !(depth > 0) || !(height > 0)) return (false, 1, 1, 1);
            bool turn = (modelX > modelZ && depth > width) || (modelZ > modelX && width > depth);
            double footX = turn ? modelZ : modelX, footZ = turn ? modelX : modelZ;
            return (turn, width / footX, height / modelY, depth / footZ);
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

        /// <summary>Vrai si l'un des obstacles est une pièce de la mine : le dessous est la mine (pas de rideau d'eau à sa sortie).</summary>
        public static bool IsMine(IEnumerable<SpaceObstacle> obstacles) => obstacles != null && obstacles.Any(o => PieceFor(o.Id) != MinePiece.None);

        /// <summary>
        /// Passerelles de la rivière souterraine : entre deux bras consécutifs (rangés d'ouest en est), l'écart qui les sépare le long de l'axe
        /// des X, sur la bande de Z qu'ils partagent (rectangle [MinX, MaxX] × [MinZ, MaxZ], en mètres). Un écart nul ou une bande vide ne donne
        /// rien. Les bras sont les obstacles <see cref="MinePiece.River"/>.
        /// </summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Bridges(IEnumerable<SpaceObstacle> obstacles)
        {
            var arms = (obstacles ?? Array.Empty<SpaceObstacle>()).Where(o => PieceFor(o.Id) == MinePiece.River && o.Points.Count > 0)
                .Select(o => (MinX: o.Points.Min(p => p.X.Raw), MaxX: o.Points.Max(p => p.X.Raw), MinZ: o.Points.Min(p => p.Z.Raw), MaxZ: o.Points.Max(p => p.Z.Raw)))
                .OrderBy(a => a.MinX).ThenBy(a => a.MinZ).ToList();
            var result = new List<(double, double, double, double)>();
            for (int i = 0; i + 1 < arms.Count; i++)
            {
                var a = arms[i];
                var b = arms[i + 1];
                long z0 = Math.Max(a.MinZ, b.MinZ), z1 = Math.Min(a.MaxZ, b.MaxZ);
                if (b.MinX <= a.MaxX || z1 <= z0) continue;
                result.Add((a.MaxX / 10000.0, z0 / 10000.0, b.MinX / 10000.0, z1 / 10000.0));
            }
            return result;
        }

        /// <summary>Couleur des lanternes des étais (chaude, dans la pénombre froide du dessous).</summary>
        public static readonly (double R, double G, double B) LanternColor = (1.0, 0.72, 0.4);

        /// <summary>Portée (m) et intensité d'une lanterne d'étai.</summary>
        public const double LanternRange = 7.0, LanternIntensity = 1.1;

        /// <summary>Couleur des cristaux du Seuil (violets, plan de la mine).</summary>
        public static readonly (double R, double G, double B) SeuilCrystal = (0.68, 0.5, 1.0);
    }
}
