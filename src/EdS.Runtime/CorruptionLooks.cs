using System;
using System.Collections.Generic;
using EdS.Simulation.Randomness;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.42.2 : une pièce du décor d'une terre corrompue : modèle prévu, place (en mètres, monde), lacet en degrés, échelle.</summary>
    public readonly struct CorruptionPiece
    {
        public string Model { get; }
        public double X { get; }
        public double Z { get; }
        public double Yaw { get; }
        public double Scale { get; }

        public CorruptionPiece(string model, double x, double z, double yaw, double scale)
        {
            Model = model;
            X = x;
            Z = z;
            Yaw = yaw;
            Scale = scale;
        }
    }

    /// <summary>
    /// Sous-lot V2.42.2 : décor de la corruption (affichage seulement). Chaque terre corrompue se couvre d'arbres morts, de souches, de rochers
    /// fendus et de cristaux sombres, <see cref="PiecesPerStage"/> par palier, toujours aux mêmes places (tirées de l'identifiant de la terre :
    /// les pièces d'un palier restent quand elle grandit) ; son autel est corrompu tant qu'elle l'est, purifié sinon. Les modèles prévus
    /// (atelier des modèles, « Corruption/modeles.json ») sont branchés par leur nom ; sans eux, Unity montre des formes de remplacement.
    /// </summary>
    public static class CorruptionLooks
    {
        /// <summary>Dossier des modèles dans le projet Unity (« Assets/EdS/Modeles/Corruption/&lt;nom&gt;/&lt;nom&gt;.fbx »).</summary>
        public const string Folder = "Assets/EdS/Modeles/Corruption";

        public const string Tree1 = "arbre_corrompu_1", Tree2 = "arbre_corrompu_2", Stump = "souche_corrompue", Rock1 = "rocher_corrompu_1",
            Rock2 = "rocher_corrompu_2", Crystals = "cristaux_sombres", AltarCorrupted = "autel_corrompu", AltarPurified = "autel_purifie";

        /// <summary>Les huit modèles prévus, dans l'ordre de l'atelier (K01 à K08).</summary>
        public static readonly IReadOnlyList<string> Models = Array.AsReadOnly(new[] { Tree1, Tree2, Stump, Rock1, Rock2, Crystals, AltarCorrupted, AltarPurified });

        /// <summary>Pièces de décor par palier (au palier 3 : 36).</summary>
        public const int PiecesPerStage = 12;

        /// <summary>Distance au plus entre la caméra et le bord d'une terre pour la construire (mètres).</summary>
        public const double ShowRange = 250;

        /// <summary>Aucune pièce de décor à moins de cette distance de l'autel (on le voit et on l'atteint).</summary>
        public const double AltarClearance = 6;

        /// <summary>Taille demandée à l'atelier : longueur, largeur, hauteur (mètres).</summary>
        public static (double Length, double Width, double Height) Size(string model) => model switch
        {
            Tree1 => (4.0, 4.0, 7.0),
            Tree2 => (5.0, 3.5, 6.0),
            Stump => (2.2, 2.2, 1.4),
            Rock1 => (2.5, 2.0, 1.8),
            Rock2 => (3.0, 2.2, 1.0),
            Crystals => (1.6, 1.6, 1.8),
            AltarCorrupted => (2.0, 1.4, 1.6),
            AltarPurified => (2.0, 1.4, 1.6),
            _ => throw new ArgumentException($"Modèle de la corruption inconnu « {model} ».", nameof(model)),
        };

        /// <summary>Nom du modèle d'un fichier du dossier (son nom sans extension, s'il est prévu), ou null.</summary>
        public static string? NameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (string m in Models) if (m == name) return m;
            return null;
        }

        /// <summary>
        /// Les pièces de la terre <paramref name="v"/> : d'abord l'autel (sauf la source : ses deux autels sont ceux du cercle), puis le décor,
        /// <see cref="PiecesPerStage"/> par palier (rien pour une terre propre). Même terre, même palier : mêmes pièces.
        /// </summary>
        public static IReadOnlyList<CorruptionPiece> Layout(CorruptionView v)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));
            double cx = v.Position.X.Raw / 10000.0, cz = v.Position.Z.Raw / 10000.0, r = v.Radius.Raw / 10000.0;
            double ax = v.Altar.X.Raw / 10000.0, az = v.Altar.Z.Raw / 10000.0;
            var pieces = new List<CorruptionPiece>();
            var rng = new Pcg32(Seed(v.Area.Id));
            if (!v.IsSource) pieces.Add(new CorruptionPiece(v.IsCorrupted ? AltarCorrupted : AltarPurified, ax, az, rng.NextBounded(360), 1.0));
            int count = checked(PiecesPerStage * v.Stage);
            for (int i = 0, tries = 0; i < count && tries < count * 4; tries++)
            {
                double angle = rng.NextBounded(36000) / 100.0 * Math.PI / 180.0;
                double dist = r * Math.Sqrt(0.02 + 0.93 * rng.NextBounded(10001) / 10000.0);   // uniforme sur le disque, ni au centre exact ni au bord
                double x = cx + dist * Math.Sin(angle), z = cz + dist * Math.Cos(angle);
                uint kind = rng.NextBounded(100), yaw = rng.NextBounded(360), scale = rng.NextBounded(41);
                if (!v.IsSource && (x - ax) * (x - ax) + (z - az) * (z - az) < AltarClearance * AltarClearance) continue;
                string model = v.IsSource ? (kind < 60 ? Crystals : Rock1) : kind < 22 ? Tree1 : kind < 40 ? Tree2 : kind < 50 ? Stump : kind < 68 ? Rock1 : kind < 82 ? Rock2 : Crystals;
                pieces.Add(new CorruptionPiece(model, x, z, yaw, 0.8 + scale / 100.0));
                i++;
            }
            return pieces;
        }

        /// <summary>Graine d'une terre : FNV-1a 64 bits de son identifiant (stable d'une plateforme à l'autre).</summary>
        public static ulong Seed(string id)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in id) { h ^= c; h = unchecked(h * 1099511628211UL); }
            return h;
        }
    }
}
