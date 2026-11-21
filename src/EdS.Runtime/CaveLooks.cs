using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.46 : ce que dessine un obstacle d'un dessous (grotte de la cascade).</summary>
    public enum CavePiece
    {
        /// <summary>Rien de propre à la grotte : le rocher simple.</summary>
        None = 0,
        Wall = 1,
        Stalagmite = 2,
        Pillar = 3,
        Pool = 4,
        Crystals = 5,
        Rubble = 6,
        Chest = 7,
    }

    /// <summary>
    /// Chantier V2.46 (grotte derrière la cascade, PLAN_GRANDE_CARTE.md) : rendu d'un dessous, sans Unity. Les obstacles de la grotte
    /// prennent un modèle du kit de grotte par le début de leur identifiant (« paroi_grotte_ouest » : une paroi) ; tant que le modèle
    /// n'est pas là (kit préparé dans l'atelier, aucune dépense Tripo engagée), Unity pose une forme de remplacement. La pénombre est
    /// réglée ici : un dessous n'a ni ciel, ni soleil, ni pluie, ni neige, ni glace ; le jour qui passe par le rideau d'eau éclaire
    /// l'entrée et s'éteint vers le fond.
    /// </summary>
    public static class CaveLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Grotte/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Grotte/";

        /// <summary>
        /// Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c> et <c>&lt;nom&gt;_normale.png</c> à plat
        /// (finition Blender de l'atelier, <c>Grotte/finir_grotte.py</c>), chargés par <c>Resources.Load(Folder + nom)</c>.
        /// </summary>
        public const string AssetFolder = "Assets/Resources/Grotte";

        /// <summary>
        /// Nom du modèle du kit (<see cref="Models"/>) du fichier <paramref name="path"/> (« Assets/Resources/Grotte/paroi_grotte.fbx »), ou null :
        /// autre dossier, sous-dossier, autre extension que .fbx (casse ignorée) ou nom inconnu.
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

        /// <summary>Le rideau d'eau vu de dedans, posé sur chaque porte d'un dessous qui mène dehors.</summary>
        public const string CurtainModel = "rideau_d_eau";

        /// <summary>
        /// Les huit modèles du kit (codes G01 à G08 de l'atelier, <c>Grotte/modeles.json</c>), dans l'ordre des codes. Les noms sont
        /// ceux des fichiers attendus.
        /// </summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            "paroi_grotte", "stalagmites", "pilier_grotte", CurtainModel, "bassin_grotte", "cristaux_grotte", "eboulis_grotte", "coffre_grotte",
        };

        private static readonly (string Prefix, CavePiece Piece)[] Prefixes =
        {
            ("paroi_grotte", CavePiece.Wall),
            ("stalagmite_grotte", CavePiece.Stalagmite),
            ("pilier_grotte", CavePiece.Pillar),
            ("bassin_grotte", CavePiece.Pool),
            ("cristaux_grotte", CavePiece.Crystals),
            ("eboulis_grotte", CavePiece.Rubble),
            ("coffre_grotte", CavePiece.Chest),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="CavePiece.None"/> sinon.</summary>
        public static CavePiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return CavePiece.None;
            foreach ((string prefix, CavePiece piece) in Prefixes)
                if (obstacleId == prefix || (obstacleId.Length > prefix.Length && obstacleId.StartsWith(prefix, StringComparison.Ordinal) && obstacleId[prefix.Length] == '_'))
                    return piece;
            return CavePiece.None;
        }

        /// <summary>Nom du modèle du kit pour <paramref name="piece"/>, null pour <see cref="CavePiece.None"/>.</summary>
        public static string? ModelFor(CavePiece piece) => piece switch
        {
            CavePiece.Wall => Models[0],
            CavePiece.Stalagmite => Models[1],
            CavePiece.Pillar => Models[2],
            CavePiece.Pool => Models[4],
            CavePiece.Crystals => Models[5],
            CavePiece.Rubble => Models[6],
            CavePiece.Chest => Models[7],
            _ => null,
        };

        /// <summary>Hauteur des parois et des piliers (m) : la caméra voit par-dessus, la grotte n'a pas de voûte affichée.</summary>
        public const double WallHeight = 6.0;

        /// <summary>Hauteur des formes de remplacement des autres pièces (m).</summary>
        public static double HeightOf(CavePiece piece) => piece switch
        {
            CavePiece.Wall => WallHeight,
            CavePiece.Pillar => WallHeight,
            CavePiece.Stalagmite => 2.2,
            CavePiece.Crystals => 1.4,
            CavePiece.Rubble => 1.0,
            CavePiece.Chest => 0.7,
            CavePiece.Pool => 0.0,
            _ => 1.5,
        };

        /// <summary>Lumière ambiante d'un dessous (couleur linéaire, 0 à 1) : pénombre froide, bien plus sombre qu'une pièce.</summary>
        public static readonly (double R, double G, double B) Ambient = (0.055, 0.065, 0.08);

        /// <summary>Brume de la grotte (m) : on y voit à une vingtaine de mètres, le fond se perd dans le noir.</summary>
        public const double FogStart = 5.0, FogEnd = 24.0;

        /// <summary>Couleur du fond et de la brume : presque noir, un peu bleu.</summary>
        public static readonly (double R, double G, double B) Fog = (0.015, 0.02, 0.028);

        /// <summary>Portée (m) du jour qui entre par le rideau d'eau, depuis la porte.</summary>
        public const double CurtainReach = 14.0;

        /// <summary>
        /// Part du jour (0 à 1) qui passe par le rideau d'eau à la place <paramref name="distance"/> m de la porte : 1 sur la porte, 0
        /// à <see cref="CurtainReach"/> m et au-delà, décroissance douce (carré). <paramref name="daylight"/> (0 à 1) est la lumière du
        /// dehors (nuit : 0) : la nuit, l'entrée est aussi sombre que le fond.
        /// </summary>
        public static double CurtainLight(double distance, double daylight)
        {
            if (double.IsNaN(distance) || double.IsNaN(daylight)) return 0;
            double day = Math.Max(0, Math.Min(1, daylight));
            if (distance <= 0) return day;
            if (distance >= CurtainReach) return 0;
            double t = 1 - distance / CurtainReach;
            return day * t * t;
        }
    }
}
