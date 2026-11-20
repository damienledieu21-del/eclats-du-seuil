using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EdS.Data.Homes;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Maison du joueur (sous-lot V2.53.2, décisions de Damien du 20 novembre 2026) : la porte de la maison fermée tant qu'elle n'est pas
    /// achetée, le maître d'œuvre qui la vend et l'agrandit (réponses « [Maison] » du dialogue), le lit (halte et réconfort), le retour à la
    /// maison depuis un point sûr (le point sûr à soi).
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>PNJ qui vend la maison et l'agrandit (Maître Bertin, devant la porte).</summary>
        public const string HomeVendor = "npc.maitre_oeuvre";

        /// <summary>La maison du personnage, ou null (pas encore achetée).</summary>
        public SavedHome? Home => World.HomeState;

        /// <summary>Le catalogue de la maison en jeu, ou null.</summary>
        public HomeBook? HomeBook => World.HomeBook;

        /// <summary>Le personnage est dans sa maison.</summary>
        public bool AtHome => _home != null && _zone.Id == _home.Interior && Home != null;

        /// <summary>
        /// Réponses de la maison ajoutées au dialogue du maître d'œuvre, après celles des fêtes : « acheter » tant que la maison n'est pas à
        /// soi, puis une réponse par besoin d'or ou de matériaux du palier d'agrandissement en cours qui n'est pas rempli.
        /// </summary>
        public IReadOnlyList<string> HomeAnswers => HomeChoices.Select(c => c.Label).ToList();

        private IReadOnlyList<(string Label, string? Need)> HomeChoices
        {
            get
            {
                if (!(Dialogue is DialogueView d) || d.Npc != HomeVendor || !(_home is HomeBook book)) return Array.Empty<(string, string?)>();
                if (!(Home is SavedHome home))
                    return new[] { (string.Format(CultureInfo.InvariantCulture, Text("text.ui.home.buy_answer"), Coins.Format(book.Price)), (string?)null) };
                if (!(HomeRules.CurrentStage(home, book) is RebuildStage stage)) return Array.Empty<(string, string?)>();
                var list = new List<(string, string?)>();
                foreach (RebuildNeed n in stage.Needs)
                {
                    long left = HomeRules.Remaining(home, n);
                    if (left <= 0) continue;
                    string what = n.Kind == RebuildNeedKind.Coins ? Coins.Format(left) : $"{left} × {ItemName(n.Target!)}";
                    list.Add((string.Format(CultureInfo.InvariantCulture, Text("text.ui.home.give_answer"), Text("text.ui.home.stage." + stage.Id),
                        n.Kind == RebuildNeedKind.Coins ? Text("text.ui.home.gold") : ItemName(n.Target!), what), n.Id));
                }
                return list;
            }
        }

        /// <summary>Réponse de la maison de rang <paramref name="index"/> (après celles de l'histoire, des cartes et des fêtes).</summary>
        private DialogueRefusal ChooseHome(int index)
        {
            IReadOnlyList<(string Label, string? Need)> choices = HomeChoices;
            if (index < 0 || index >= choices.Count) return DialogueRefusal.NoSuchChoice;
            if (choices[index].Need is string need) GiveToHome(need);
            else BuyHome();
            return DialogueRefusal.None;
        }

        /// <summary>Parle au maître d'œuvre (à portée) : son dialogue propose d'acheter puis d'agrandir la maison.</summary>
        public bool TalkToHomeVendor()
        {
            DialogueRefusal refusal = World.Talk(HomeVendor, out _);
            if (refusal == DialogueRefusal.TooFar) Say("text.ui.too_far");
            return refusal == DialogueRefusal.None;
        }

        /// <summary>Achète la maison (son prix quitte la bourse) ; sinon le motif s'affiche et rien ne change.</summary>
        public bool BuyHome()
        {
            HomeRefusal refusal = World.BuyHome();
            if (refusal != HomeRefusal.None) return HomeRefused(refusal);
            _dirty = true;
            Say("text.ui.home.bought");
            return true;
        }

        /// <summary>Apporte au besoin <paramref name="need"/> du palier en cours tout ce qui est possible ; annonce le palier fini.</summary>
        public bool GiveToHome(string need)
        {
            HomeRefusal refusal = World.GiveToHome(need, out long given, out IReadOnlyList<RebuildStage> finished);
            if (refusal != HomeRefusal.None) return HomeRefused(refusal);
            _dirty = true;
            RebuildNeed? n = HomeRules.CurrentStage(Home!, _home!)?.Need(need) ?? finished.SelectMany(s => s.Needs).FirstOrDefault(x => x.Id == need);
            Say("text.ui.home.given", n?.Kind == RebuildNeedKind.Item ? $"{given} × {ItemName(n.Target!)}" : Coins.Format(given));
            foreach (RebuildStage s in finished) Say("text.ui.home.stage_done", Text("text.ui.home.stage." + s.Id));
            return true;
        }

        private bool HomeRefused(HomeRefusal refusal)
        {
            Say("text.ui.home.refused", Text("text.ui.home.why." + refusal.ToString().ToLowerInvariant()));
            return false;
        }

        /// <summary>
        /// Dort dans son lit (dans la maison, un lit posé, aucun ennemi, aucun dialogue) : halte (sauvegarde, PV pleins) et réconfort pendant
        /// <see cref="HomeRules.BedComfortMinutes"/> minutes de jeu. Faux, sans rien changer, sinon (le motif s'affiche).
        /// </summary>
        public bool Sleep()
        {
            if (World.IsDefeated || Dialogue != null || MustContinue) return false;
            HomeRefusal refusal = World.SleepInBed();
            if (refusal != HomeRefusal.None) return HomeRefused(refusal);
            _comfortDish = Text("text.ui.home.rested");
            Reopen("text.ui.home.slept");
            return true;
        }

        /// <summary>Retour à la maison possible : maison achetée, hors de la maison, halte possible ici (point sûr proche, aucun ennemi).</summary>
        public bool CanGoHome => _home != null && Home != null && _zone.Id != _home.Interior && CanRest;

        /// <summary>
        /// Le point sûr à soi : depuis un point sûr (comme une halte), rentre chez soi : la partie est sauvegardée dans la maison, au point sûr
        /// de la maison. Faux, sans rien changer, si ce n'est pas possible ou si la sauvegarde échoue.
        /// </summary>
        public bool GoHome()
        {
            if (!CanGoHome) return false;
            ZoneDefinition target = _zones.First(z => z.Id == _home!.Interior);
            WorldSave here = World.Save();
            var moved = new WorldSave(target.Id, here.WorldTick, here.Sessions, here.Consumed, here.Character, sectors: here.Sectors);
            WorldSession next = Build(moved, target, target.Entry.Id, null);
            moved = moved.WithResume(next.Resume);
            try
            {
                Write(moved);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Say("text.ui.save_failed", e.Message);
                return false;
            }
            _zone = target;
            Commit(next);
            Say("text.ui.home.went_home");
            AnnounceLearned();
            return true;
        }
    
        // --- Sous-lot V2.53.3 : rendu, mode décoration, coffre ----------------------------------------------------------------------

        /// <summary>Un meuble que l'on peut poser : identifiant, nom, usage, et combien en poser (objets de l'inventaire ; une décoration : 0 ou 1).</summary>
        public readonly struct HomePieceLine
        {
            public HomePieceLine(string piece, string name, HomeUse use, int available, double width, double depth)
            {
                Piece = piece;
                Name = name;
                Use = use;
                Available = available;
                Width = width;
                Depth = depth;
            }

            public string Piece { get; }
            public string Name { get; }
            public HomeUse Use { get; }
            public int Available { get; }
            public double Width { get; }
            public double Depth { get; }
        }

        /// <summary>Les meubles posables maintenant (au moins un disponible), dans l'ordre du catalogue ; vide hors de la maison.</summary>
        public IReadOnlyList<HomePieceLine> HomePiecesToPlace
        {
            get
            {
                if (!AtHome) return Array.Empty<HomePieceLine>();
                var list = new List<HomePieceLine>();
                foreach (HomePiece p in _home!.Furniture)
                {
                    int n = p.Item != null ? World.Inventory.BaseAvailable(p.Item)
                        : HomeRules.Owns(World.Character.Feasts, p) && Home!.Placed.All(x => x.Piece != p.Id) ? 1 : 0;
                    if (n > 0) list.Add(new HomePieceLine(p.Id, HomePieceName(p), p.Use, n, p.Width * HomeLook.Cell, p.Depth * HomeLook.Cell));
                }
                return list;
            }
        }

        /// <summary>Nom affiché d'un meuble : celui de son objet, ou de sa décoration de fête.</summary>
        public string HomePieceName(HomePiece piece) => piece.Item != null ? ItemName(piece.Item)
            : World.FeastBook?.Ref(piece.FeastCode) is EdS.Data.Feasts.FeastRef r ? Text(r.Name) : piece.Id;

        /// <summary>Nom affiché du meuble <paramref name="piece"/> (identifiant s'il est inconnu).</summary>
        public string HomePieceNameOf(string piece) => _home?.Piece(piece) is HomePiece p ? HomePieceName(p) : piece;

        /// <summary>Les places du sac (nom, quantité), dans l'ordre de l'inventaire (pour ranger au coffre).</summary>
        public IReadOnlyList<(string Name, int Count)> BagLines => World.Inventory.Stacks.Select(s => (ItemName(s.Item), s.Count)).ToList();

        /// <summary>Les meubles posés, tels qu'ils se dessinent ; vide hors de la maison.</summary>
        public IReadOnlyList<HomePieceLook> HomePieces => AtHome ? HomeLook.Pieces(_home!, Home!, _zone) : (IReadOnlyList<HomePieceLook>)Array.Empty<HomePieceLook>();

        /// <summary>Le sol ouvert aujourd'hui (mètres), ou null hors de la maison.</summary>
        public (double MinX, double MinZ, double MaxX, double MaxZ)? HomeFloor => AtHome ? HomeLook.Floor(_home!, Home!, _zone) : ((double, double, double, double)?)null;

        /// <summary>Clé du dessin de la maison (vide hors de la maison).</summary>
        public string HomeKey => AtHome ? HomeLook.Key(Home) : string.Empty;

        /// <summary>Case de la grille sous un point de la maison.</summary>
        public (int X, int Z) HomeCellAt(double x, double z) => HomeLook.CellAt(_zone, x, z);

        /// <summary>Aperçu du mode décoration : le meuble tel qu'il se dessinerait sur la case, ou null (hors de la maison, meuble inconnu).</summary>
        public HomePieceLook? HomePreview(string piece, int x, int z, int turn) =>
            AtHome && _home!.Piece(piece) is HomePiece def ? HomeLook.At(def, x, z, turn, _zone) : (HomePieceLook?)null;

        /// <summary>Ce que donnerait la pose de <paramref name="piece"/> sur la case, sans rien changer (aperçu du mode décoration).</summary>
        public HomeRefusal CanPlaceFurniture(string piece, int x, int z, int turn)
        {
            if (!AtHome) return HomeRefusal.NotThere;
            if (!(_home!.Piece(piece) is HomePiece def)) return HomeRefusal.UnknownPiece;
            if (HomePiecesToPlace.All(l => l.Piece != piece)) return HomeRefusal.NotAvailable;
            return HomeRules.CanPlace(Home!, _home, def, x, z, turn);
        }

        /// <summary>Pose un meuble (mode décoration) ; le motif d'un refus s'affiche, rien ne change.</summary>
        public bool PlaceFurniture(string piece, int x, int z, int turn) => HomeDone(World.PlaceFurniture(piece, x, z, turn));

        /// <summary>Déplace le meuble qui couvre la case (<paramref name="fromX"/>, <paramref name="fromZ"/>).</summary>
        public bool MoveFurniture(int fromX, int fromZ, int x, int z, int turn) => HomeDone(World.MoveFurniture(fromX, fromZ, x, z, turn));

        /// <summary>Un quart de tour au meuble qui couvre la case.</summary>
        public bool RotateFurniture(int x, int z) => HomeDone(World.RotateFurniture(x, z));

        /// <summary>Reprend le meuble qui couvre la case (un objet revient dans l'inventaire).</summary>
        public bool PickUpFurniture(int x, int z) => HomeDone(World.PickUpFurniture(x, z));

        /// <summary>Le coffre : places dans l'ordre (nom, quantité), et sa capacité ; vide hors de la maison.</summary>
        public IReadOnlyList<(string Item, string Name, int Count)> HomeChest => AtHome
            ? Home!.Chest.Select(s => (s.Item, ItemName(s.Item), s.Count)).ToList()
            : (IReadOnlyList<(string, string, int)>)Array.Empty<(string, string, int)>();

        /// <summary>Places du coffre de la maison aujourd'hui (0 hors de la maison).</summary>
        public int HomeChestSize => AtHome ? HomeRules.Tier(Home!, _home!).Chest : 0;

        /// <summary>Range au coffre toute la pile de la place <paramref name="place"/> de l'inventaire.</summary>
        public bool ChestPut(int place) =>
            place >= 0 && place < World.Inventory.Stacks.Count && HomeDone(World.ChestPut(place, World.Inventory.AvailableAt(place)));

        /// <summary>Reprend du coffre toute la pile de sa place <paramref name="place"/>.</summary>
        public bool ChestTake(int place) => AtHome && place >= 0 && place < Home!.Chest.Count && HomeDone(World.ChestTake(place, Home.Chest[place].Count));

        private bool HomeDone(HomeRefusal refusal)
        {
            if (refusal != HomeRefusal.None) return HomeRefused(refusal);
            _dirty = true;
            return true;
        }
    }
}
