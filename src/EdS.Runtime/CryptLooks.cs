using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Zones;
using EdS.Domain.Space;

namespace EdS.Runtime
{
    /// <summary>Chantier V2.48.2 : ce que dessine un obstacle des cryptes (abbaye et chapelle).</summary>
    public enum CryptPiece
    {
        /// <summary>Rien de propre aux cryptes.</summary>
        None = 0,
        /// <summary>Mur de crypte maçonné (occulteur).</summary>
        Wall = 1,
        /// <summary>Pilier roman (occulteur).</summary>
        Pillar = 2,
        /// <summary>Gisant : dalle funéraire sculptée d'un moine couché.</summary>
        Effigy = 3,
        Sarcophagus = 4,
        /// <summary>Ossuaire : niches d'ossements contre le mur.</summary>
        Ossuary = 5,
        /// <summary>Grille de fer ouverte, de part et d'autre d'un passage (la voûte est posée sur l'écart).</summary>
        Grille = 6,
        /// <summary>Candélabre (et sa lumière).</summary>
        Candelabra = 7,
        /// <summary>Bougies posées au sol (et leur lueur).</summary>
        Candles = 8,
        /// <summary>Autel funéraire.</summary>
        Altar = 9,
        /// <summary>Tombeau de l'abbé.</summary>
        Tomb = 10,
        /// <summary>Coffre (décor) : le coffre du kit de mine.</summary>
        Chest = 11,
    }

    /// <summary>
    /// Chantier V2.48.2 (cryptes de l'abbaye et de la chapelle, PLAN_GRANDE_CARTE.md) : rendu des cryptes, sans Unity. Les obstacles prennent
    /// une pièce du kit de crypte par le début de leur identifiant (« mur_crypte_entree_ouest » : un mur) ; les modèles (kit préparé, non généré :
    /// dépense à valider par Damien) sont cherchés dans <see cref="AssetFolder"/> et, s'ils manquent, Unity pose une forme de remplacement. La
    /// voûte est posée sur l'écart entre deux grilles (<see cref="Arches"/>) ; l'escalier de crypte sur les portes de la crypte (escalier, trappe
    /// ou porte vers la surface) ; la dalle sur une trappe vue d'en haut (la dalle descellée de la chapelle). Affichage seulement.
    /// </summary>
    public static class CryptLooks
    {
        /// <summary>Dossier des modèles dans Unity (<c>Resources/Crypte/&lt;nom&gt;</c>).</summary>
        public const string Folder = "Crypte/";

        /// <summary>Dossier des modèles dans le projet Unity : <c>&lt;nom&gt;.fbx</c>, <c>&lt;nom&gt;_couleur.jpg</c>, <c>&lt;nom&gt;_normale.png</c> à plat.</summary>
        public const string AssetFolder = "Assets/EdS/Modeles/Resources/Crypte";

        public const string VaultModel = "voute_crypte", StairsModel = "escalier_crypte", SlabModel = "dalle_crypte";

        /// <summary>
        /// Les treize modèles du kit (codes C01 à C13 de l'atelier, <c>Crypte/modeles.json</c>), dans l'ordre des codes. Les noms sont ceux des
        /// fichiers attendus. Le coffre reprend celui du kit de mine (<see cref="MineLooks"/>).
        /// </summary>
        public static readonly IReadOnlyList<string> Models = new[]
        {
            "mur_crypte", VaultModel, "pilier_crypte", "gisant_crypte", "sarcophage_crypte", "ossuaire_crypte", "grille_crypte", "candelabre_crypte",
            "bougies_crypte", "autel_crypte", "tombeau_abbe", StairsModel, SlabModel,
        };

        private static readonly (string Prefix, CryptPiece Piece)[] Prefixes =
        {
            ("mur_crypte", CryptPiece.Wall),
            ("pilier_crypte", CryptPiece.Pillar),
            ("gisant_crypte", CryptPiece.Effigy),
            ("sarcophage_crypte", CryptPiece.Sarcophagus),
            ("ossuaire_crypte", CryptPiece.Ossuary),
            ("grille_crypte", CryptPiece.Grille),
            ("candelabre_crypte", CryptPiece.Candelabra),
            ("bougies_crypte", CryptPiece.Candles),
            ("autel_crypte", CryptPiece.Altar),
            ("tombeau_abbe", CryptPiece.Tomb),
            ("coffre_crypte", CryptPiece.Chest),
        };

        /// <summary>Ce que dessine l'obstacle <paramref name="obstacleId"/> (identifiant exact ou suivi de « _… »), <see cref="CryptPiece.None"/> sinon.</summary>
        public static CryptPiece PieceFor(string obstacleId)
        {
            if (string.IsNullOrEmpty(obstacleId)) return CryptPiece.None;
            foreach ((string prefix, CryptPiece piece) in Prefixes)
                if (obstacleId == prefix || (obstacleId.Length > prefix.Length && obstacleId.StartsWith(prefix, StringComparison.Ordinal) && obstacleId[prefix.Length] == '_'))
                    return piece;
            return CryptPiece.None;
        }

        /// <summary>Nom du modèle de <paramref name="piece"/> : du kit de crypte, le coffre du kit de mine ; null pour <see cref="CryptPiece.None"/>.</summary>
        public static string? ModelFor(CryptPiece piece) => piece switch
        {
            CryptPiece.Wall => Models[0],
            CryptPiece.Pillar => Models[2],
            CryptPiece.Effigy => Models[3],
            CryptPiece.Sarcophagus => Models[4],
            CryptPiece.Ossuary => Models[5],
            CryptPiece.Grille => Models[6],
            CryptPiece.Candelabra => Models[7],
            CryptPiece.Candles => Models[8],
            CryptPiece.Altar => Models[9],
            CryptPiece.Tomb => Models[10],
            CryptPiece.Chest => MineLooks.ModelFor(MinePiece.Chest),
            _ => null,
        };

        /// <summary>Dossier Resources du modèle de <paramref name="piece"/> : celui du kit de mine pour le coffre, de la crypte sinon.</summary>
        public static string FolderFor(CryptPiece piece) => piece == CryptPiece.Chest ? MineLooks.Folder : Folder;

        /// <summary>Le mur et le pilier cachent le personnage : ils sont enregistrés comme occulteurs (fondus ou coupés comme les autres murs).</summary>
        public static bool Occludes(CryptPiece piece) => piece == CryptPiece.Wall || piece == CryptPiece.Pillar;

        /// <summary>
        /// Hauteur d'une pièce posée (m), modèle ou forme de remplacement, celle de sa fiche (<c>Crypte/modeles.json</c>) ; les murs et piliers à
        /// la hauteur des murs de la grotte. Elle ne suit jamais l'emprise (<see cref="MineLooks.Fit"/>).
        /// </summary>
        public static double HeightOf(CryptPiece piece) => piece switch
        {
            CryptPiece.Wall => CaveLooks.WallHeight,
            CryptPiece.Pillar => CaveLooks.WallHeight,
            CryptPiece.Effigy => 0.9,
            CryptPiece.Sarcophagus => 1.1,
            CryptPiece.Ossuary => 2.6,
            CryptPiece.Grille => 2.8,
            CryptPiece.Candelabra => 1.8,
            CryptPiece.Candles => 0.35,
            CryptPiece.Altar => 1.1,
            CryptPiece.Tomb => 1.6,
            CryptPiece.Chest => 0.8,
            _ => 1.5,
        };

        /// <summary>Hauteur de la voûte posée sur un passage (m), celle de sa fiche.</summary>
        public const double VaultHeight = 4.2;

        /// <summary>Hauteur de l'escalier de crypte posé à une porte (m) et épaisseur de la dalle (m).</summary>
        public const double StairsHeight = 2.4, SlabHeight = 0.2;

        /// <summary>Taille au sol de l'escalier (largeur, longueur) et de la dalle (côté), en mètres.</summary>
        public const double StairsWidth = 2.4, StairsLength = 3.0, SlabSide = 1.2;

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

        /// <summary>Vrai si l'un des obstacles est une pièce de crypte : le dessous est une crypte (ni rideau d'eau, ni kit de mine).</summary>
        public static bool IsCrypt(IEnumerable<SpaceObstacle> obstacles) => obstacles != null && obstacles.Any(o => PieceFor(o.Id) != CryptPiece.None);

        /// <summary>
        /// Voûtes des passages : entre deux grilles consécutives (rangées d'ouest en est) qui partagent une bande de Z, l'écart qui les sépare le
        /// long de l'axe des X (rectangle [MinX, MaxX] × [MinZ, MaxZ], en mètres). Un écart nul ou une bande vide ne donne rien.
        /// </summary>
        public static IReadOnlyList<(double MinX, double MinZ, double MaxX, double MaxZ)> Arches(IEnumerable<SpaceObstacle> obstacles)
        {
            var posts = (obstacles ?? Array.Empty<SpaceObstacle>()).Where(o => o != null && PieceFor(o.Id) == CryptPiece.Grille && o.Points.Count > 0)
                .Select(o => (MinX: o.Points.Min(p => p.X.Raw), MaxX: o.Points.Max(p => p.X.Raw), MinZ: o.Points.Min(p => p.Z.Raw), MaxZ: o.Points.Max(p => p.Z.Raw)))
                .OrderBy(a => a.MinZ).ThenBy(a => a.MinX).ToList();
            var result = new List<(double, double, double, double)>();
            for (int i = 0; i + 1 < posts.Count; i++)
            {
                var a = posts[i];
                var b = posts[i + 1];
                long z0 = Math.Max(a.MinZ, b.MinZ), z1 = Math.Min(a.MaxZ, b.MaxZ);
                if (b.MinX <= a.MaxX || z1 <= z0) continue;
                result.Add((a.MaxX / 10000.0, z0 / 10000.0, b.MinX / 10000.0, z1 / 10000.0));
            }
            return result;
        }

        /// <summary>
        /// Ce qui est posé à une porte de crypte : l'escalier pour un escalier, une trappe vue d'en bas ou la porte qui remonte à la surface ;
        /// rien pour un puits ou une faille (aucun dans les cryptes).
        /// </summary>
        public static string? DoorModel(DoorKind kind) => kind == DoorKind.Stairs || kind == DoorKind.Hatch || kind == DoorKind.Door ? StairsModel : null;

        /// <summary>
        /// Une trappe d'un intérieur (la dalle descellée de la chapelle) est au sol : ni ouverture dans un mur, ni porte adossée ; Unity y pose
        /// la dalle (<see cref="SlabModel"/>). Faux pour toute autre porte.
        /// </summary>
        public static bool OnFloor(MapDoor door) => (door ?? throw new ArgumentNullException(nameof(door))).Kind == DoorKind.Hatch;

        /// <summary>Lumière des bougies et des candélabres (chaude, dans la pénombre froide).</summary>
        public static readonly (double R, double G, double B) CandleColor = (1.0, 0.74, 0.45);

        /// <summary>Portée (m) et intensité d'un candélabre, puis de bougies au sol.</summary>
        public const double CandelabraRange = 6.0, CandelabraIntensity = 1.0, CandlesRange = 3.5, CandlesIntensity = 0.7;
    }
}
