using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Homes;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>
    /// Maison du joueur (sous-lot V2.53.1, sauvegarde v29 ; décisions de Damien du 20 novembre 2026) : le catalogue de la maison en jeu et
    /// la maison du personnage (<see cref="SavedCharacter.Home"/>). On l'achète une fois en or et on l'agrandit par paliers dans la zone de sa
    /// porte (le Bourg fortifié) ; on y pose des meubles (objets de l'inventaire ou décorations de fête possédées) et l'on y range des objets
    /// au coffre. Toute opération refusée ne change rien (refus atomiques : tout est calculé avant la moindre mutation).
    /// </summary>
    public sealed partial class WorldSession
    {
        private HomeBook? _home;

        /// <summary>Le catalogue de la maison en jeu, ou null.</summary>
        public HomeBook? HomeBook => _home;

        /// <summary>La maison du personnage, ou null (pas encore achetée).</summary>
        public SavedHome? HomeState => _character.Home;

        /// <summary>
        /// Met le catalogue de la maison en jeu (null : aucun). La maison gardée doit aller avec lui (<see cref="HomeRules.Mismatch"/>), sinon
        /// <see cref="ArgumentException"/> et rien ne change.
        /// </summary>
        public void UseHome(HomeBook? book)
        {
            if (book != null && _character.Home is SavedHome home && HomeRules.Mismatch(home, book, _items, _character.Feasts) is string why)
                throw new ArgumentException($"Maison sauvegardée hors du catalogue de la maison : {why}.", nameof(book));
            _home = book;
        }

        /// <summary>La porte <paramref name="door"/> est celle de la maison, qui n'est pas encore achetée (fermée).</summary>
        public bool DoorAwaitsHome(string door) => _home != null && _home.Door == door && Definition?.Id == _home.DoorZone && _character.Home is null;

        /// <summary>V2.53.2 : le point sûr <paramref name="safePoint"/> est celui de la maison, pas encore achetée (ni repos, ni retour).</summary>
        public bool AwaitsHomeSafePoint(string safePoint) =>
            _home != null && _character.Home is null && Definition?.Id == _home.Interior && Definition.SafePoints.Any(s => s.Id == safePoint);

        /// <summary>Tick du monde où l'atelier de la maison est de nouveau libre (hors sauvegarde).</summary>
        private long _homeCraftReadyTick;

        /// <summary>
        /// Tick du monde où l'atelier de la maison est de nouveau libre (0 : libre). Hors sauvegarde (décision de Damien du 20 novembre
        /// 2026) ; l'aventure le reporte d'une session à l'autre (<see cref="CarryHomeCraft"/>).
        /// </summary>
        public long HomeCraftReadyTick => _homeCraftReadyTick;

        /// <summary>Reprend l'attente de l'atelier de la maison d'une session précédente (porte, retour à la maison, halte).</summary>
        public void CarryHomeCraft(long readyTick) => _homeCraftReadyTick = Math.Max(_homeCraftReadyTick, readyTick);

        /// <summary>
        /// V2.53.2 : la recette <paramref name="r"/> se fabrique à l'atelier de la maison : dans la maison achetée, un meuble posé qui la sert
        /// (foyer : cuisine ; établi : bois et pierre), et pas une recette de la forge à qualité.
        /// </summary>
        private bool HomeWorkshopFor(EdS.Data.Crafting.Recipe r) =>
            !r.Quality && _home is HomeBook book && _character.Home is SavedHome home && Definition?.Id == book.Interior
            && home.Placed.Any(p => book.Piece(p.Piece) is HomePiece piece && HomeRules.WorkshopServes(piece.Use, r.Profession));

        /// <summary>
        /// V2.53.2 : dormir dans son lit (dans la maison, un lit posé, hors combat) : réconfort pendant <see cref="HomeRules.BedComfortMinutes"/>
        /// minutes de jeu (marche un peu plus rapide hors combat, aucune statistique de combat ; un réconfort plus long en cours est gardé).
        /// </summary>
        public HomeRefusal SleepInBed()
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            if (!HomeRules.Has(home, book, HomeUse.Bed)) return HomeRefusal.NothingThere;
            long until = checked(NowMinute + HomeRules.BedComfortMinutes);
            if (!(ComfortUntilMinute is long now) || now < until) CarryComfort(until);
            return HomeRefusal.None;
        }

        /// <summary>Le personnage est vivant, présent, hors combat, dans la zone <paramref name="zone"/>.</summary>
        private HomeRefusal HomeWhere(string zone)
        {
            if (Definition?.Id != zone) return HomeRefusal.NotThere;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector)) return HomeRefusal.NotThere;
            return InCombat ? HomeRefusal.InCombat : HomeRefusal.None;
        }

        /// <summary>Achète la maison (une fois), dans la zone de sa porte : son prix quitte la bourse.</summary>
        public HomeRefusal BuyHome()
        {
            if (!(_home is HomeBook book)) return HomeRefusal.NoHome;
            if (_character.Home != null) return HomeRefusal.AlreadyOwned;
            HomeRefusal where = HomeWhere(book.DoorZone);
            if (where != HomeRefusal.None) return where;
            HomeRefusal why = HomeRules.Buy(null, book, _character.Money, out SavedHome? bought);
            if (why != HomeRefusal.None) return why;
            _character = _character.WithMoney(checked(_character.Money - book.Price)).WithHome(bought);
            return HomeRefusal.None;
        }

        /// <summary>
        /// Apporte au besoin <paramref name="need"/> du palier d'agrandissement en cours tout ce que le personnage peut, au plus ce qui reste
        /// (cuivre de la bourse ou matériaux de l'inventaire), dans la zone de la porte. <paramref name="finished"/> : paliers finis.
        /// </summary>
        public HomeRefusal GiveToHome(string need, out long given, out IReadOnlyList<RebuildStage> finished)
        {
            given = 0;
            finished = Array.Empty<RebuildStage>();
            if (!(_home is HomeBook book)) return HomeRefusal.NoHome;
            if (!(_character.Home is SavedHome home)) return HomeRefusal.NotOwned;
            if (!(HomeRules.CurrentStage(home, book) is RebuildStage stage)) return HomeRefusal.FullyBuilt;
            if (!(stage.Need(need) is RebuildNeed n)) return HomeRefusal.UnknownNeed;
            long left = HomeRules.Remaining(home, n);
            if (left <= 0) return HomeRefusal.NeedMet;
            HomeRefusal where = HomeWhere(book.DoorZone);
            if (where != HomeRefusal.None) return where;
            long have = n.Kind == RebuildNeedKind.Coins ? _character.Money : Math.Max(0, Inventory.Available(n.Target!));
            long put = Math.Min(left, have);
            if (put <= 0) return HomeRefusal.NothingToGive;
            SavedHome next = HomeRules.Give(home, book, n.Id, put, out finished);
            if (n.Kind == RebuildNeedKind.Item)
            {
                if (Inventory.Remove(n.Target!, (int)put) != InventoryRefusal.None) { finished = Array.Empty<RebuildStage>(); return HomeRefusal.NothingToGive; }
            }
            else _character = _character.WithMoney(checked(_character.Money - put));
            _character = _character.WithHome(next);
            given = put;
            return HomeRefusal.None;
        }

        /// <summary>Maison achetée et personnage dedans (vivant, hors combat).</summary>
        private HomeRefusal Inside(out HomeBook book, out SavedHome home)
        {
            book = _home!;
            home = _character.Home!;
            if (_home is null) return HomeRefusal.NoHome;
            if (_character.Home is null) return HomeRefusal.NotOwned;
            return HomeWhere(_home.Interior);
        }

        /// <summary>Le meuble est disponible : une copie de base dans l'inventaire, ou une décoration de fête possédée.</summary>
        private bool HasPiece(HomePiece piece) => piece.Item != null
            ? Inventory.BaseAvailable(piece.Item) >= 1
            : HomeRules.Owns(_character.Feasts, piece);

        /// <summary>
        /// Pose le meuble <paramref name="piece"/> en (<paramref name="x"/>, <paramref name="z"/>) cases, tourné de <paramref name="turn"/>
        /// quarts de tour : un objet quitte l'inventaire, une décoration de fête reste au portefeuille (posée une fois).
        /// </summary>
        public HomeRefusal PlaceFurniture(string piece, int x, int z, int turn)
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            if (!(book.Piece(piece) is HomePiece def)) return HomeRefusal.UnknownPiece;
            if (!HasPiece(def)) return HomeRefusal.NotAvailable;
            why = HomeRules.Place(home, book, piece, x, z, turn, out SavedHome next);
            if (why != HomeRefusal.None) return why;
            if (def.Item != null && Inventory.Remove(def.Item, 1) != InventoryRefusal.None) return HomeRefusal.NotAvailable;
            _character = _character.WithHome(next);
            return HomeRefusal.None;
        }

        /// <summary>Déplace (et tourne) le meuble qui couvre la case (<paramref name="fromX"/>, <paramref name="fromZ"/>).</summary>
        public HomeRefusal MoveFurniture(int fromX, int fromZ, int x, int z, int turn)
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            why = HomeRules.Move(home, book, fromX, fromZ, x, z, turn, out SavedHome next);
            if (why == HomeRefusal.None) _character = _character.WithHome(next);
            return why;
        }

        /// <summary>Un quart de tour de plus pour le meuble qui couvre la case.</summary>
        public HomeRefusal RotateFurniture(int x, int z)
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            why = HomeRules.Rotate(home, book, x, z, out SavedHome next);
            if (why == HomeRefusal.None) _character = _character.WithHome(next);
            return why;
        }

        /// <summary>Reprend le meuble qui couvre la case : un objet revient dans l'inventaire (refusé s'il est plein).</summary>
        public HomeRefusal PickUpFurniture(int x, int z)
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            why = HomeRules.PickUp(home, book, x, z, out SavedHome next, out HomePiece? piece);
            if (why != HomeRefusal.None) return why;
            if (piece!.Item != null)
            {
                if (!Inventory.CanAdd(piece.Item, 1)) return HomeRefusal.InventoryFull;
                Inventory.Add(piece.Item, 1);
            }
            _character = _character.WithHome(next);
            return HomeRefusal.None;
        }

        /// <summary>Range au coffre <paramref name="count"/> objets de la place <paramref name="place"/> de l'inventaire.</summary>
        public HomeRefusal ChestPut(int place, int count)
        {
            HomeRefusal why = Inside(out HomeBook book, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            if (place < 0 || place >= Inventory.Stacks.Count) return HomeRefusal.NothingThere;
            ItemStack stack = Inventory.Stacks[place];
            if (count < 1 || count > stack.Count || Inventory.AvailableAt(place) < count) return HomeRefusal.NotEnough;
            ItemStack moved = count == stack.Count ? stack : stack.WithCount(count);
            why = HomeRules.ChestPut(home.Chest, moved, HomeRules.Tier(home, book).Chest, _items.Item(stack.Item).Stack, out IReadOnlyList<ItemStack> chest);
            if (why != HomeRefusal.None) return why;
            if (Inventory.RemoveAt(place, count, out _) != InventoryRefusal.None) return HomeRefusal.NotEnough;
            _character = _character.WithHome(home.WithChest(chest));
            return HomeRefusal.None;
        }

        /// <summary>Reprend du coffre <paramref name="count"/> objets de sa place <paramref name="place"/> (refusé si l'inventaire est plein).</summary>
        public HomeRefusal ChestTake(int place, int count)
        {
            HomeRefusal why = Inside(out HomeBook _, out SavedHome home);
            if (why != HomeRefusal.None) return why;
            why = HomeRules.ChestTake(home.Chest, place, count, out IReadOnlyList<ItemStack> chest, out ItemStack? taken);
            if (why != HomeRefusal.None) return why;
            bool fits = taken!.IsUnique || _items.Item(taken.Item).Stack == 1 ? Inventory.FreePlaces >= taken.Count : Inventory.CanAdd(taken.Item, taken.Count);
            if (!fits) return HomeRefusal.InventoryFull;
            if (taken.IsUnique || _items.Item(taken.Item).Stack == 1)
            {
                for (int i = 0; i < taken.Count; i++) Inventory.Add(taken.Count == 1 ? taken : taken.WithCount(1));
            }
            else Inventory.Add(taken.Item, taken.Count);
            _character = _character.WithHome(home.WithChest(chest));
            return HomeRefusal.None;
        }
    }
}
