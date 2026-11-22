using System;
using System.Collections.Generic;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.41.2 : une pièce d'une caravane à montrer (modèle prévu, ou forme de remplacement).</summary>
    public readonly struct CaravanPiece
    {
        /// <summary>Nom du modèle prévu (<see cref="CaravanLooks.Wagon"/>, <see cref="CaravanLooks.Team"/>, <see cref="CaravanLooks.Goods"/>).</summary>
        public string Model { get; }

        /// <summary>Décalage en mètres dans le repère de la caravane : <c>Forward</c> le long de la marche, <c>Side</c> à droite.</summary>
        public double Forward { get; }
        public double Side { get; }

        /// <summary>Rotation autour de la verticale (degrés, 0 : dans le sens de la marche) et roulis (degrés, 90 : couché sur le flanc).</summary>
        public double Yaw { get; }
        public double Roll { get; }

        public CaravanPiece(string model, double forward, double side, double yaw = 0, double roll = 0)
        {
            Model = model;
            Forward = forward;
            Side = side;
            Yaw = yaw;
            Roll = roll;
        }
    }

    /// <summary>
    /// Sous-lot V2.41.2 : à quoi ressemble une caravane (affichage seulement). Les modèles prévus (liste de l'atelier, <c>Caravanes/modeles.json</c>)
    /// sont branchés par leur nom ; tant qu'ils manquent, Unity montre des formes simples de la même taille (<see cref="Size"/>).
    /// </summary>
    /// <summary>Sous-lot V2.41.3 (revue Codex de la PR #243) : un coffre à reprendre au fort, à afficher (clé stable, place au sol).</summary>
    public readonly struct FortChest
    {
        /// <summary>« caravane/jour du pillage/rang » : stable tant que le coffre attend.</summary>
        public string Key { get; }
        public double X { get; }
        public double Z { get; }

        public FortChest(string key, double x, double z)
        {
            Key = key;
            X = x;
            Z = z;
        }
    }

    public static class CaravanLooks
    {
        /// <summary>
        /// Sous-lot V2.41.3 (revue Codex de la PR #243) : les coffres encore à reprendre des quêtes ouvertes (ni repris, ni quête fermée),
        /// dans l'ordre des quêtes puis des coffres. Affichage seulement (modèle <see cref="Goods"/>).
        /// </summary>
        public static IReadOnlyList<FortChest> RecoveryChests(IReadOnlyList<CaravanRecoveryView> open)
        {
            if (open is null) throw new ArgumentNullException(nameof(open));
            var list = new List<FortChest>();
            foreach (CaravanRecoveryView v in open)
                for (int k = 0; k < v.Recovery.Chests.Count; k++)
                    if (!v.IsTaken(k))
                        list.Add(new FortChest(v.Caravan.Id + "/" + v.PillageDay + "/" + k, v.Recovery.Chests[k].X.Raw / 10000.0, v.Recovery.Chests[k].Z.Raw / 10000.0));
            return list;
        }

        /// <summary>Dossier des modèles des caravanes dans le projet Unity (« …/Caravanes/&lt;nom&gt;/&lt;nom&gt;.fbx »).</summary>
        public const string Folder = "Assets/EdS/Modeles/Caravanes";

        public const string Wagon = "chariot_bache", Team = "attelage", Goods = "coffres_marchandises";

        /// <summary>Les noms des modèles prévus, dans l'ordre de la liste de l'atelier (C01, C02, C03).</summary>
        public static IReadOnlyList<string> Models { get; } = Array.AsReadOnly(new[] { Wagon, Team, Goods });

        /// <summary>Taille d'un modèle (longueur le long de la marche, largeur, hauteur, en mètres), celle demandée à l'atelier.</summary>
        public static (double Length, double Width, double Height) Size(string model) => model switch
        {
            Wagon => (4.2, 2.0, 2.8),
            Team => (3.0, 1.8, 1.7),
            Goods => (1.6, 1.0, 1.1),
            _ => throw new ArgumentException($"Modèle de caravane « {model} » inconnu.", nameof(model)),
        };

        /// <summary>Écart entre deux chariots qui se suivent (de l'avant de l'un à l'avant de l'autre), en mètres.</summary>
        public const double Spacing = 9.0;

        /// <summary>Distance la plus grande (m) entre la caméra et une caravane montrée.</summary>
        public const double ShowRange = 600;

        /// <summary>Nom d'un modèle d'après son chemin (« …/Caravanes/attelage/attelage.fbx » → « attelage »), ou null.</summary>
        public static string? NameOf(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string p = path!.Replace('\\', '/');
            if (!p.StartsWith(Folder + "/", StringComparison.Ordinal)) return null;
            string[] parts = p.Substring(Folder.Length + 1).Split('/');
            if (parts.Length != 2) return null;
            int dot = parts[1].LastIndexOf('.');
            if (dot <= 0) return null;
            string ext = parts[1].Substring(dot).ToLowerInvariant();
            if (ext != ".fbx" && ext != ".glb") return null;
            string name = parts[1].Substring(0, dot);
            return name == parts[0] && Array.IndexOf(new[] { Wagon, Team, Goods }, name) >= 0 ? name : null;
        }

        /// <summary>
        /// Pièces de la caravane <paramref name="v"/>, dans son repère (l'origine est sa place, le chariot de tête) : en route ou attaquée, chaque
        /// chariot a son attelage devant lui et le suivant derrière ; attaquée, les coffres sont posés à côté du chariot de tête. Pillée : le
        /// chariot de tête est couché sur le flanc, sans attelage (les bêtes ont été emmenées), les autres de travers, et les coffres
        /// éventrés jonchent la route.
        /// </summary>
        public static IReadOnlyList<CaravanPiece> Layout(CaravanView v)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));
            var pieces = new List<CaravanPiece>();
            int wagons = v.Caravan.Wagons;
            double team = (Size(Wagon).Length + Size(Team).Length) / 2 + 0.4;
            for (int i = 0; i < wagons; i++)
            {
                double back = -i * Spacing;
                if (v.State == CaravanState.Pillaged)
                {
                    pieces.Add(i == 0 ? new CaravanPiece(Wagon, back, 1.5, 25, 90) : new CaravanPiece(Wagon, back, -1.0, -30));
                    continue;
                }
                pieces.Add(new CaravanPiece(Team, back + team, 0));
                pieces.Add(new CaravanPiece(Wagon, back, 0));
            }
            if (v.State == CaravanState.Attacked) pieces.Add(new CaravanPiece(Goods, 0, -2.2, 90));
            if (v.State == CaravanState.Pillaged)
            {
                pieces.Add(new CaravanPiece(Goods, 2.5, -2.0, 40));
                pieces.Add(new CaravanPiece(Goods, -4.0, 2.8, -70));
            }
            return pieces;
        }
    }
}
