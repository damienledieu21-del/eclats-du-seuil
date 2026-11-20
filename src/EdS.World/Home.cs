using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Homes;
using EdS.Data.Items;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Validation;

namespace EdS.World
{
    /// <summary>Maison du joueur : raison d'une opération refusée (rien ne change).</summary>
    public enum HomeRefusal
    {
        None = 0,

        /// <summary>Aucun catalogue de la maison en jeu.</summary>
        NoHome = 1,

        /// <summary>La maison n'est pas encore achetée.</summary>
        NotOwned = 2,

        /// <summary>La maison est déjà achetée (une seule maison, achetée une fois).</summary>
        AlreadyOwned = 3,

        /// <summary>Bourse trop légère pour acheter la maison.</summary>
        NotEnoughMoney = 4,

        /// <summary>Tous les paliers d'agrandissement sont finis.</summary>
        FullyBuilt = 5,

        /// <summary>Besoin inconnu dans le palier en cours.</summary>
        UnknownNeed = 6,

        /// <summary>Ce besoin est déjà rempli.</summary>
        NeedMet = 7,

        /// <summary>Rien à donner : bourse vide ou aucun de ces matériaux disponible.</summary>
        NothingToGive = 8,

        /// <summary>Meuble inconnu du catalogue.</summary>
        UnknownPiece = 9,

        /// <summary>Meuble non disponible : aucune copie de base dans l'inventaire, décoration non possédée ou déjà posée.</summary>
        NotAvailable = 10,

        /// <summary>Le meuble déborde du sol de la maison.</summary>
        OutOfFloor = 11,

        /// <summary>Le meuble chevauche un meuble déjà posé.</summary>
        Overlap = 12,

        /// <summary>Déjà autant de meubles posés que la maison en permet.</summary>
        TooManyPieces = 13,

        /// <summary>Aucun meuble sur cette case, ou place du coffre (ou de l'inventaire) vide.</summary>
        NothingThere = 14,

        /// <summary>Quart de tour hors de [0, 3].</summary>
        BadTurn = 15,

        /// <summary>Le coffre n'a plus la place.</summary>
        ChestFull = 16,

        /// <summary>L'inventaire n'a plus la place.</summary>
        InventoryFull = 17,

        /// <summary>Quantité hors de la pile, ou objets réservés.</summary>
        NotEnough = 18,

        /// <summary>Pas au bon endroit : dans la maison (meubles, coffre) ou dans la zone de sa porte (achat, agrandissement) ; tombé ou absent.</summary>
        NotThere = 19,

        /// <summary>En plein combat.</summary>
        InCombat = 20,
    }

    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1, décisions de Damien du 20 novembre 2026) : règles pures, sans état. On achète la maison une fois
    /// en or (<see cref="Buy"/>), on l'agrandit par paliers comme un chantier (<see cref="Give"/>, avec <see cref="RebuildRules"/>), on pose
    /// librement des meubles au sol sur la grille de 0,25 m avec rotation par quarts de tour, sans chevauchement (<see cref="Place"/>,
    /// <see cref="Move"/>, <see cref="PickUp"/>), et l'on range des objets au coffre (<see cref="ChestPut"/>, <see cref="ChestTake"/>). Chaque
    /// règle calcule le nouvel état ou refuse sans rien changer.
    /// </summary>
    public static class HomeRules
    {
        /// <summary>Nom de la suite de paliers de la maison pour <see cref="RebuildRules"/>.</summary>
        public const string StagesId = "home";

        /// <summary>Le palier d'agrandissement en cours, ou null (maison agrandie jusqu'au dernier palier connu).</summary>
        public static RebuildStage? CurrentStage(SavedHome home, HomeBook book) => RebuildRules.CurrentStage(AsRebuild(home), book.Stages);

        /// <summary>Ce que la maison offre aujourd'hui (sol, coffre, meubles).</summary>
        public static HomeTier Tier(SavedHome home, HomeBook book) => book.Tier(home.Stage);

        /// <summary>Ce qui reste à apporter au besoin <paramref name="need"/> du palier en cours.</summary>
        public static long Remaining(SavedHome home, RebuildNeed need) => need.Amount - home.Of(need.Id);

        private static SavedRebuild? AsRebuild(SavedHome home) =>
            home.Stage == 0 && home.Given.Count == 0 ? null : new SavedRebuild(StagesId, home.Stage, home.Given);

        /// <summary>Achat : la maison neuve et son prix, ou un refus (déjà achetée, bourse trop légère).</summary>
        public static HomeRefusal Buy(SavedHome? home, HomeBook book, long money, out SavedHome? bought)
        {
            if (book is null) throw new ArgumentNullException(nameof(book));
            bought = null;
            if (home != null) return HomeRefusal.AlreadyOwned;
            if (money < book.Price) return HomeRefusal.NotEnoughMoney;
            bought = SavedHome.Bought;
            return HomeRefusal.None;
        }

        /// <summary>
        /// Apport de <paramref name="amount"/> au besoin <paramref name="need"/> du palier en cours (plafonné à ce qui reste, comme un chantier) ;
        /// <paramref name="finished"/> reçoit les paliers finis par cet apport.
        /// </summary>
        /// <exception cref="ArgumentException">Maison agrandie au dernier palier, besoin hors du palier en cours, ou quantité non positive.</exception>
        public static SavedHome Give(SavedHome home, HomeBook book, string need, long amount, out IReadOnlyList<RebuildStage> finished)
        {
            if (home is null) throw new ArgumentNullException(nameof(home));
            if (book is null) throw new ArgumentNullException(nameof(book));
            SavedRebuild next = RebuildRules.Give(AsRebuild(home), StagesId, book.Stages, need, amount, _ => false, out finished);
            return home.WithStage(next.Stage, next.Given);
        }

        /// <summary>Indice du meuble posé qui couvre la case (<paramref name="x"/>, <paramref name="z"/>), ou -1.</summary>
        public static int PieceAt(SavedHome home, HomeBook book, int x, int z)
        {
            for (int i = 0; i < home.Placed.Count; i++)
            {
                SavedPiece p = home.Placed[i];
                if (!(book.Piece(p.Piece) is HomePiece def)) continue;
                (int w, int d) = def.Footprint(p.Turn);
                if (x >= p.X && x < p.X + w && z >= p.Z && z < p.Z + d) return i;
            }
            return -1;
        }

        /// <summary>
        /// Le meuble <paramref name="piece"/> peut-il se poser en (<paramref name="x"/>, <paramref name="z"/>), tourné de <paramref name="turn"/>
        /// quarts de tour, en ignorant le meuble posé d'indice <paramref name="ignore"/> (un déplacement) ? Dans le sol, sans chevauchement, et
        /// sous le nombre de meubles permis.
        /// </summary>
        public static HomeRefusal CanPlace(SavedHome home, HomeBook book, HomePiece piece, int x, int z, int turn, int ignore = -1)
        {
            if (turn < 0 || turn > 3) return HomeRefusal.BadTurn;
            HomeTier tier = Tier(home, book);
            (int w, int d) = piece.Footprint(turn);
            if (x < 0 || z < 0 || (long)x + w > tier.Width || (long)z + d > tier.Depth) return HomeRefusal.OutOfFloor;
            if (ignore < 0 && home.Placed.Count >= tier.Pieces) return HomeRefusal.TooManyPieces;
            for (int i = 0; i < home.Placed.Count; i++)
            {
                if (i == ignore) continue;
                SavedPiece o = home.Placed[i];
                if (!(book.Piece(o.Piece) is HomePiece od)) continue;
                (int ow, int odp) = od.Footprint(o.Turn);
                if (x < o.X + ow && o.X < x + w && z < o.Z + odp && o.Z < z + d) return HomeRefusal.Overlap;
            }
            return HomeRefusal.None;
        }

        /// <summary>Pose : la maison avec le meuble en plus, ou un refus (la disponibilité du meuble est vérifiée par l'appelant).</summary>
        public static HomeRefusal Place(SavedHome home, HomeBook book, string piece, int x, int z, int turn, out SavedHome next)
        {
            next = home ?? throw new ArgumentNullException(nameof(home));
            if (!(book.Piece(piece) is HomePiece def)) return HomeRefusal.UnknownPiece;
            if (def.FeastCode != null && home.Placed.Any(p => p.Piece == def.Id)) return HomeRefusal.NotAvailable;   // une décoration, posée une fois
            HomeRefusal why = CanPlace(home, book, def, x, z, turn);
            if (why != HomeRefusal.None) return why;
            next = home.WithPlaced(home.Placed.Append(new SavedPiece(def.Id, x, z, turn)));
            return HomeRefusal.None;
        }

        /// <summary>Déplacement (et rotation) du meuble qui couvre la case (<paramref name="fromX"/>, <paramref name="fromZ"/>).</summary>
        public static HomeRefusal Move(SavedHome home, HomeBook book, int fromX, int fromZ, int x, int z, int turn, out SavedHome next)
        {
            next = home ?? throw new ArgumentNullException(nameof(home));
            int at = PieceAt(home, book, fromX, fromZ);
            if (at < 0) return HomeRefusal.NothingThere;
            HomePiece def = book.Piece(home.Placed[at].Piece)!;
            HomeRefusal why = CanPlace(home, book, def, x, z, turn, at);
            if (why != HomeRefusal.None) return why;
            next = home.WithPlaced(home.Placed.Where((_, i) => i != at).Append(new SavedPiece(def.Id, x, z, turn)));
            return HomeRefusal.None;
        }

        /// <summary>Un quart de tour de plus pour le meuble qui couvre la case, autour de son coin (refusé s'il déborde ou chevauche).</summary>
        public static HomeRefusal Rotate(SavedHome home, HomeBook book, int x, int z, out SavedHome next)
        {
            next = home ?? throw new ArgumentNullException(nameof(home));
            int at = PieceAt(home, book, x, z);
            if (at < 0) return HomeRefusal.NothingThere;
            SavedPiece p = home.Placed[at];
            return Move(home, book, x, z, p.X, p.Z, (p.Turn + 1) & 3, out next);
        }

        /// <summary>Reprise du meuble qui couvre la case : la maison sans lui, et le meuble repris.</summary>
        public static HomeRefusal PickUp(SavedHome home, HomeBook book, int x, int z, out SavedHome next, out HomePiece? piece)
        {
            next = home ?? throw new ArgumentNullException(nameof(home));
            piece = null;
            int at = PieceAt(home, book, x, z);
            if (at < 0) return HomeRefusal.NothingThere;
            piece = book.Piece(home.Placed[at].Piece);
            next = home.WithPlaced(home.Placed.Where((_, i) => i != at));
            return HomeRefusal.None;
        }

        /// <summary>
        /// Range <paramref name="stack"/> au coffre de <paramref name="capacity"/> places : une copie de base complète d'abord les piles entamées
        /// du même objet (jusqu'à <paramref name="stackSize"/>), puis de nouvelles places ; une instance unique prend une place. Tout ou rien.
        /// </summary>
        public static HomeRefusal ChestPut(IReadOnlyList<ItemStack> chest, ItemStack stack, int capacity, int stackSize, out IReadOnlyList<ItemStack> next)
        {
            if (chest is null) throw new ArgumentNullException(nameof(chest));
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            if (stackSize < 1 || stack.Count < 1) throw new ArgumentOutOfRangeException(nameof(stackSize));
            next = chest;
            var list = chest.ToList();
            if (stack.IsUnique || stackSize == 1)
            {
                if (stack.IsUnique && stack.Count != 1) throw new ArgumentException("Une instance unique ne s'empile pas.", nameof(stack));
                if (checked(list.Count + stack.Count) > capacity) return HomeRefusal.ChestFull;
                for (int i = 0; i < stack.Count; i++) list.Add(stack.Count == 1 ? stack : stack.WithCount(1));
                next = list.AsReadOnly();
                return HomeRefusal.None;
            }
            int left = stack.Count;
            for (int i = 0; i < list.Count && left > 0; i++)
                if (list[i].Item == stack.Item && !list[i].IsUnique && list[i].Count < stackSize)
                {
                    int put = Math.Min(left, stackSize - list[i].Count);
                    list[i] = list[i].WithCount(list[i].Count + put);
                    left -= put;
                }
            while (left > 0)
            {
                if (list.Count >= capacity) return HomeRefusal.ChestFull;
                int put = Math.Min(left, stackSize);
                list.Add(new ItemStack(stack.Item, put));
                left -= put;
            }
            next = list.AsReadOnly();
            return HomeRefusal.None;
        }

        /// <summary>Retire <paramref name="count"/> objets de la place <paramref name="place"/> du coffre (la place garde le reste).</summary>
        public static HomeRefusal ChestTake(IReadOnlyList<ItemStack> chest, int place, int count, out IReadOnlyList<ItemStack> next, out ItemStack? taken)
        {
            if (chest is null) throw new ArgumentNullException(nameof(chest));
            next = chest;
            taken = null;
            if (place < 0 || place >= chest.Count) return HomeRefusal.NothingThere;
            ItemStack s = chest[place];
            if (count < 1 || count > s.Count) return HomeRefusal.NotEnough;
            var list = chest.ToList();
            if (count == s.Count) list.RemoveAt(place);
            else list[place] = s.WithCount(s.Count - count);
            taken = count == s.Count ? s : s.WithCount(count);
            next = list.AsReadOnly();
            return HomeRefusal.None;
        }

        /// <summary>La décoration de fête <paramref name="piece"/> est possédée (portefeuille de sa fête, <c>owned</c>).</summary>
        public static bool Owns(SavedFeasts feasts, HomePiece piece) =>
            piece.FeastCode != null && feasts.Entries.TryGetValue(piece.Festival!, out SavedFeast f) && f.Owned.Contains(piece.FeastCode);

        /// <summary>Pourquoi la maison gardée ne va pas avec le catalogue (premier écart, voir <see cref="Check"/>), ou null si elle va.</summary>
        public static string? Mismatch(SavedHome home, HomeBook book, ItemCatalog items, SavedFeasts feasts)
        {
            var report = new ValidationReport();
            Check(home, book, items, feasts, report, "$.data.character.home");
            return report.IsValid ? null : report.Errors[0].Message;
        }

        /// <summary>
        /// Accord de la maison gardée avec le catalogue, chaque écart avec son chemin et son code : paliers au-delà du dernier, apports hors du
        /// palier en cours ou au-delà du besoin ; meubles inconnus, hors du sol, qui se chevauchent, trop nombreux, décoration posée deux fois ;
        /// décoration de fête posée sans être possédée (<paramref name="feasts"/>, portefeuilles du personnage) ; coffre trop plein, objets
        /// inconnus ou instances refusées par le catalogue, piles trop hautes.
        /// </summary>
        public static void Check(SavedHome home, HomeBook book, ItemCatalog items, SavedFeasts feasts, ValidationReport report, string path)
        {
            if (feasts is null) throw new ArgumentNullException(nameof(feasts));
            if (home is null) throw new ArgumentNullException(nameof(home));
            if (book is null) throw new ArgumentNullException(nameof(book));
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (report is null) throw new ArgumentNullException(nameof(report));
            if (home.Stage > book.Stages.Count)
                report.Add(path + ".stage", ValidationCode.OutOfRange, $"{home.Stage} paliers finis, il n'y en a que {book.Stages.Count}.");
            else
            {
                RebuildStage? stage = CurrentStage(home, book);
                foreach (KeyValuePair<string, long> g in home.Given)
                {
                    string gp = path + ".given." + g.Key;
                    if (stage is null) report.Add(gp, ValidationCode.InvalidValue, "apport à une maison agrandie jusqu'au dernier palier.");
                    else if (!(stage.Need(g.Key) is RebuildNeed need)) report.Add(gp, ValidationCode.UnknownReference, $"besoin « {g.Key} » hors du palier « {stage.Id} ».");
                    else if (g.Value > need.Amount) report.Add(gp, ValidationCode.OutOfRange, $"{g.Value} apportés au besoin « {g.Key} », qui en demande {need.Amount}.");
                }
            }
            HomeTier tier = Tier(home, book);
            if (home.Placed.Count > tier.Pieces) report.Add(path + ".placed", ValidationCode.LimitExceeded, $"{home.Placed.Count} meubles posés, au plus {tier.Pieces}.");
            var placedFeasts = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < home.Placed.Count; i++)
            {
                string pp = $"{path}.placed[{i}]";
                SavedPiece p = home.Placed[i];
                if (!(book.Piece(p.Piece) is HomePiece def)) { report.Add(pp + ".piece", ValidationCode.UnknownReference, $"meuble « {p.Piece} » inconnu."); continue; }
                if (def.FeastCode != null && !placedFeasts.Add(def.Id)) report.Add(pp + ".piece", ValidationCode.DuplicateId, $"décoration « {def.Id} » posée deux fois.");
                else if (def.FeastCode != null && !Owns(feasts, def))
                    report.Add(pp + ".piece", ValidationCode.InvalidValue, $"décoration « {def.FeastCode} » posée sans être possédée (portefeuille de « {def.Festival} »).");
                (int w, int d) = def.Footprint(p.Turn);
                if (p.X + w > tier.Width || p.Z + d > tier.Depth) { report.Add(pp, ValidationCode.OutOfRange, $"« {def.Id} » déborde du sol."); continue; }
                for (int k = 0; k < i; k++)
                {
                    SavedPiece o = home.Placed[k];
                    if (!(book.Piece(o.Piece) is HomePiece od)) continue;
                    (int ow, int odp) = od.Footprint(o.Turn);
                    if (p.X < o.X + ow && o.X < p.X + w && p.Z < o.Z + odp && o.Z < p.Z + d)
                    { report.Add(pp, ValidationCode.InvalidValue, $"« {def.Id} » chevauche « {od.Id} »."); break; }
                }
            }
            if (home.Chest.Count > tier.Chest) report.Add(path + ".chest", ValidationCode.LimitExceeded, $"{home.Chest.Count} places au coffre, au plus {tier.Chest}.");
            for (int i = 0; i < home.Chest.Count; i++)
            {
                string cp = $"{path}.chest[{i}]";
                ItemStack s = home.Chest[i];
                if (!items.TryGetItem(s.Item, out ItemDefinition def)) { report.Add(cp + ".item", ValidationCode.UnknownReference, $"objet « {s.Item} » inconnu."); continue; }
                if (s.Count > def.Stack) report.Add(cp + ".count", ValidationCode.OutOfRange, $"{s.Count} « {s.Item} », piles de {def.Stack} au plus.");
                else if (s.IsUnique && items.CheckInstance(s) is string problem) report.Add(cp, ValidationCode.InvalidValue, problem);
            }
        }
    }
}
