using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.29 (2) (sujet 30 : « brouillard à dissiper ») : cases vues d'une région. Une case est vue quand le personnage passe à
    /// moins de <see cref="SightMeters"/> mètres d'elle (écart entre son point et le rectangle de la case), ou depuis un point de vue
    /// (<see cref="Viewpoint"/>). Bits rangés comme dans la sauvegarde (<c>character.explored</c>, version 23) : le bit
    /// <c>colonne + rangée × colonnes</c>, octet par octet, bit de poids faible d'abord, écrit en hexadécimal minuscule. Arithmétique
    /// entière seulement (déterministe).
    /// </summary>
    public sealed class ExploredCells
    {
        /// <summary>Distance de vue d'une case en marchant (une case de 128 m : le détail que le téléphone montre autour du joueur).</summary>
        public const int SightMeters = RegionGrid.CellMeters;

        private readonly bool[] _seen;

        public RegionGrid Grid { get; }

        /// <summary>Cases vues.</summary>
        public int Count { get; private set; }

        public ExploredCells(RegionGrid grid, string? hex = null)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _seen = new bool[grid.CellCount];
            if (string.IsNullOrEmpty(hex)) return;
            // Relecture indépendante de la PR #185 : la sauvegarde ne garde pas la largeur de la grille. Une chaîne d'une autre longueur que
            // celle de cette grille (région redécoupée), ou qui n'est pas en hexadécimal minuscule, est ignorée tout entière (rien n'est vu,
            // Discarded), plutôt que de décaler les cases sans rien dire.
            if (hex!.Length != HexLength(grid) || hex.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
            {
                Discarded = true;
                return;
            }
            for (int i = 0; i < hex.Length; i += 2)
            {
                int b = Digit(hex[i]) * 16 + Digit(hex[i + 1]);
                for (int bit = 0; bit < 8; bit++)
                {
                    int index = i / 2 * 8 + bit;
                    if (index < _seen.Length && (b & (1 << bit)) != 0)
                    {
                        _seen[index] = true;
                        Count++;
                    }
                }
            }
        }

        /// <summary>La chaîne lue n'avait pas la longueur de cette grille (ou n'était pas hexadécimale) : elle a été ignorée.</summary>
        public bool Discarded { get; }

        /// <summary>Longueur (caractères) de la chaîne des cases d'une grille : deux par octet de huit cases.</summary>
        public static int HexLength(RegionGrid grid) => (grid ?? throw new ArgumentNullException(nameof(grid))).CellCount / 8 * 2 + (grid.CellCount % 8 == 0 ? 0 : 2);

        private static int Digit(char c) => c <= '9' ? c - '0' : c - 'a' + 10;

        public bool IsSeen(int column, int row) =>
            column >= 0 && column < Grid.Columns && row >= 0 && row < Grid.Rows && _seen[column + row * Grid.Columns];

        /// <summary>
        /// Rend vues les cases à moins de <paramref name="meters"/> mètres de <paramref name="p"/> (rectangle de la case touché par le disque,
        /// bord compris) ; rend le nombre de cases nouvellement vues. Un point hors de la région ne montre rien.
        /// </summary>
        public int Reveal(GroundVector p, int meters)
        {
            if (meters < 0) throw new ArgumentOutOfRangeException(nameof(meters));
            if (!Grid.Contains(p)) return 0;
            RegionCell center = Grid.CellOf(p);
            int reach = meters / RegionGrid.CellMeters + 1;
            long r = checked((long)meters * Fixed.Scale);
            long r2 = checked(r * r);
            int added = 0;
            for (int row = Math.Max(0, center.Row - reach); row <= Math.Min(Grid.Rows - 1, center.Row + reach); row++)
                for (int column = Math.Max(0, center.Column - reach); column <= Math.Min(Grid.Columns - 1, center.Column + reach); column++)
                {
                    int index = column + row * Grid.Columns;
                    if (_seen[index]) continue;
                    long minX = checked((long)(Grid.OriginX + column * RegionGrid.CellMeters) * Fixed.Scale), minZ = checked((long)(Grid.OriginZ + row * RegionGrid.CellMeters) * Fixed.Scale);
                    long maxX = checked(minX + (long)RegionGrid.CellMeters * Fixed.Scale), maxZ = checked(minZ + (long)RegionGrid.CellMeters * Fixed.Scale);
                    long dx = p.X.Raw < minX ? minX - p.X.Raw : p.X.Raw > maxX ? p.X.Raw - maxX : 0;
                    long dz = p.Z.Raw < minZ ? minZ - p.Z.Raw : p.Z.Raw > maxZ ? p.Z.Raw - maxZ : 0;
                    if (checked(dx * dx + dz * dz) > r2) continue;
                    _seen[index] = true;
                    Count++;
                    added++;
                }
            return added;
        }

        /// <summary>Bits des cases vues, en hexadécimal minuscule (format de la sauvegarde) ; null si aucune n'est vue.</summary>
        public string? ToHex()
        {
            if (Count == 0) return null;
            var b = new StringBuilder((_seen.Length + 7) / 8 * 2);
            for (int i = 0; i < _seen.Length; i += 8)
            {
                int value = 0;
                for (int bit = 0; bit < 8 && i + bit < _seen.Length; bit++)
                    if (_seen[i + bit]) value |= 1 << bit;
                b.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }
            return b.ToString();
        }
    }

    /// <summary>
    /// Sous-lot V2.29 (2) : un point de vue de la région (décision de Damien du 15 novembre 2026 pour le sommet des hautes collines) : à la
    /// première visite (à moins de <see cref="Range"/> mètres), <see cref="Xp"/> points d'expérience, une découverte au carnet
    /// (<see cref="Discovery"/>, qui marque aussi la visite), et le brouillard dissipé à <see cref="RevealMeters"/> mètres autour.
    /// </summary>
    public sealed class Viewpoint
    {
        public string Id { get; }
        public string Zone { get; }
        public GroundVector Position { get; }
        public string Discovery { get; }
        public long Xp { get; }
        public int RevealMeters { get; }

        /// <summary>Distance de la visite (comme la découverte d'un point sûr).</summary>
        public const int Range = 12;

        public Viewpoint(string id, string zone, GroundVector position, string discovery, long xp, int revealMeters)
        {
            Id = id;
            Zone = zone;
            Position = position;
            Discovery = discovery;
            Xp = xp;
            RevealMeters = revealMeters;
        }

        /// <summary>
        /// Points de vue connus. Le sommet (180, 1 360), au bout du chemin des cairns : 200 XP (Damien), brouillard dissipé à 1 024 m autour
        /// (valeur de Claude : « autour », soit huit cases ; environ un cinquième des Vallons).
        /// </summary>
        public static readonly IReadOnlyList<Viewpoint> All = Array.AsReadOnly(new[]
        {
            new Viewpoint("sommet", "zone.vallons", new GroundVector(Fixed.FromInt(180), Fixed.FromInt(1360)), "discovery.point_de_vue_du_sommet", 200, 1024),
        });
    }
}
