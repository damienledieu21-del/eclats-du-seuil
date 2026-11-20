using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1, sauvegarde v29) : un meuble posé, coin bas-gauche de son emprise sur la grille de 0,25 m
    /// (<see cref="X"/>, <see cref="Z"/> en cases depuis le coin du sol), tourné de <see cref="Turn"/> quarts de tour (0 à 3).
    /// </summary>
    public sealed class SavedPiece
    {
        public string Piece { get; }
        public int X { get; }
        public int Z { get; }
        public int Turn { get; }

        /// <exception cref="ArgumentException">Meuble mal formé, case hors de [0, <see cref="SavedHome.MaxCells"/>[ ou quart de tour hors de [0, 3].</exception>
        public SavedPiece(string piece, int x, int z, int turn)
        {
            if (!SavedRebuild.IsLocalId(piece)) throw new ArgumentException($"Meuble « {piece} » mal formé.", nameof(piece));
            if (x < 0 || x >= SavedHome.MaxCells) throw new ArgumentOutOfRangeException(nameof(x));
            if (z < 0 || z >= SavedHome.MaxCells) throw new ArgumentOutOfRangeException(nameof(z));
            if (turn < 0 || turn > 3) throw new ArgumentOutOfRangeException(nameof(turn));
            Piece = piece;
            X = x;
            Z = z;
            Turn = turn;
        }
    }

    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1, sauvegarde v29 ; décisions de Damien du 20 novembre 2026) : la maison achetée (présente : achetée),
    /// ses paliers d'agrandissement finis et les apports au palier en cours (comme un chantier, <see cref="SavedRebuild"/>), les meubles posés
    /// (rangés par case, <see cref="SavedPiece"/> ; deux meubles ne commencent jamais sur la même case) et le coffre (places dans l'ordre,
    /// comme l'inventaire). L'accord avec le catalogue (<c>home.json</c> : meubles connus, sol, chevauchements, places du coffre, objets)
    /// est vérifié quand la partie reprend.
    /// </summary>
    public sealed class SavedHome
    {
        /// <summary>Paliers d'agrandissement au plus (borne technique, la même que celle du catalogue).</summary>
        public const int MaxStages = SavedRebuild.MaxStages;

        /// <summary>Côté au plus du sol, en cases de 0,25 m (32 m).</summary>
        public const int MaxCells = 128;

        /// <summary>Meubles posés au plus ; places du coffre au plus.</summary>
        public const int MaxPieces = 128, MaxChest = 64;

        public int Stage { get; }
        public IReadOnlyDictionary<string, long> Given { get; }
        public IReadOnlyList<SavedPiece> Placed { get; }
        public IReadOnlyList<ItemStack> Chest { get; }

        /// <summary>Maison tout juste achetée : rien d'agrandi, rien de posé, coffre vide.</summary>
        public static readonly SavedHome Bought = new SavedHome(0);

        /// <exception cref="ArgumentException">Bornes dépassées, identifiants mal formés, deux meubles sur la même case, pile vide.</exception>
        public SavedHome(int stage, IEnumerable<KeyValuePair<string, long>>? given = null, IEnumerable<SavedPiece>? placed = null, IEnumerable<ItemStack>? chest = null)
        {
            if (stage < 0 || stage > MaxStages) throw new ArgumentOutOfRangeException(nameof(stage));
            var map = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> g in given ?? Array.Empty<KeyValuePair<string, long>>())
            {
                if (!SavedRebuild.IsLocalId(g.Key)) throw new ArgumentException($"Besoin « {g.Key} » mal formé.", nameof(given));
                if (g.Value < 1 || g.Value > SavedRebuild.MaxAmount) throw new ArgumentOutOfRangeException(nameof(given));
                if (map.ContainsKey(g.Key)) throw new ArgumentException($"Besoin « {g.Key} » en double.", nameof(given));
                map.Add(g.Key, g.Value);
            }
            if (map.Count > SavedRebuild.MaxNeeds) throw new ArgumentException($"au plus {SavedRebuild.MaxNeeds} besoins.", nameof(given));
            List<SavedPiece> pieces = (placed ?? Array.Empty<SavedPiece>()).Select(p => p ?? throw new ArgumentNullException(nameof(placed)))
                .OrderBy(p => p.Z).ThenBy(p => p.X).ToList();   // forme canonique : par case (z, puis x)
            for (int i = 1; i < pieces.Count; i++)
                if (pieces[i].X == pieces[i - 1].X && pieces[i].Z == pieces[i - 1].Z) throw new ArgumentException("Deux meubles sur la même case.", nameof(placed));
            if (pieces.Count > MaxPieces) throw new ArgumentException($"au plus {MaxPieces} meubles.", nameof(placed));
            IReadOnlyList<ItemStack> stacks = Frozen.ListOrEmpty(chest, nameof(chest));
            if (stacks.Count > MaxChest) throw new ArgumentException($"au plus {MaxChest} places.", nameof(chest));
            if (stacks.Any(s => s is null || s.Count < 1 || !SaveValidator.IsItemId(s.Item))) throw new ArgumentException("Pile du coffre hors du format.", nameof(chest));
            Stage = stage;
            Given = new ReadOnlyDictionary<string, long>(map);
            Placed = pieces.AsReadOnly();
            Chest = stacks;
        }

        /// <summary>Ce qui a été apporté au besoin <paramref name="need"/> du palier en cours (0 si rien).</summary>
        public long Of(string need) => Given.TryGetValue(need, out long v) ? v : 0;

        public SavedHome WithStage(int stage, IEnumerable<KeyValuePair<string, long>> given) => new SavedHome(stage, given, Placed, Chest);
        public SavedHome WithPlaced(IEnumerable<SavedPiece> placed) => new SavedHome(Stage, Given, placed, Chest);
        public SavedHome WithChest(IEnumerable<ItemStack> chest) => new SavedHome(Stage, Given, Placed, chest);
    }
}
