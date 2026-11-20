using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Feasts;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Une ligne de l'étal de fête affichée : la référence, son nom, sa sorte, son prix et ce que donnerait l'achat.</summary>
    public sealed class FeastShopLine
    {
        public string Code { get; }
        public string Name { get; }

        /// <summary>Sorte affichée (« Décoration », « Costume », « Plat », « Récompense de l'an dernier »).</summary>
        public string Kind { get; }

        public FeastCategory Category { get; }
        public int Price { get; }

        /// <summary>Prix affiché (« 40 Sou des Lanternes »), ou l'état (« Acquis », « Rien à rattraper »).</summary>
        public string PriceText { get; }

        /// <summary>L'achat est possible maintenant.</summary>
        public bool Buyable { get; }

        /// <summary>Manque de monnaie seulement (le prix s'affiche en rouge).</summary>
        public bool TooDear { get; }

        /// <summary>Icône peinte (« fete_f01_d01 », <c>Resources/EdsInterface/Peintes</c>).</summary>
        public string Icon { get; }

        internal FeastShopLine(string code, string name, string kind, FeastCategory category, int price, string priceText, bool buyable, bool tooDear)
        {
            Code = code;
            Name = name;
            Kind = kind;
            Category = category;
            Price = price;
            PriceText = priceText;
            Buyable = buyable;
            TooDear = tooDear;
            Icon = FeastLooks.IconOf(code);
        }
    }

    /// <summary>Une monnaie de fête du personnage, pour la bourse : nom, pièces, plafond, icône ; celle de la fête en cours en tête.</summary>
    public sealed class FeastWalletLine
    {
        public string Festival { get; }
        public string Name { get; }
        public long Coins { get; }
        public int Cap { get; }
        public string Icon { get; }
        public bool Current { get; }

        internal FeastWalletLine(string festival, string name, long coins, int cap, string icon, bool current)
        {
            Festival = festival;
            Name = name;
            Coins = coins;
            Cap = cap;
            Icon = icon;
            Current = current;
        }

        /// <summary>« 120 / 999 ».</summary>
        public string CoinsText => Coins.ToString(CultureInfo.InvariantCulture) + " / " + Cap.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sous-lot F4 (les 12 fêtes d'Orsédia dans le jeu) : l'étal de Maître Florentin (ouvert depuis son dialogue), les activités des invités
    /// (réponses ajoutées à leur dialogue), les annonces des gains de fête, les monnaies de fête dans la bourse et l'étal posé sur la place.
    /// Tout passe par le moteur (<see cref="WorldSession.JoinFeast(string, out FeastGain, out IReadOnlyList{StoryEvent})"/>,
    /// <see cref="WorldSession.BuyFeast"/>) : un refus ne change rien et sa raison s'affiche dans les messages du jeu.
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>L'étal de fête du forain est ouvert (panneau de l'aventure).</summary>
        public bool FeastStallOpen { get; private set; }

        /// <summary>La fête d'Orsédia en cours, ou null.</summary>
        public Feast? FeastNow => World.FeastNow;

        /// <summary>Le PNJ <paramref name="npc"/> est le forain des fêtes.</summary>
        public bool IsFeastMerchant(string? npc) => npc != null && World.FeastBook?.Merchant == npc;

        /// <summary>Nom traduit d'une fête de l'histoire.</summary>
        public string FestivalName(string festival) => _story.HasFestival(festival) ? Text(_story.Festival(festival).Name) : festival;

        /// <summary>
        /// Réponses de fête ajoutées au dialogue, après celles de l'histoire et des cartes (même numérotation que <see cref="Choose"/>) : chez
        /// le forain, « voir l'étal » ; chez un invité de la fête en cours, une réponse par activité qu'il mène (gain, ou déjà faite aujourd'hui).
        /// </summary>
        public IReadOnlyList<string> FeastAnswers => FeastChoices.Select(c => c.Label).ToList();

        private IReadOnlyList<(string Label, string? Activity)> FeastChoices
        {
            get
            {
                if (!(Dialogue is DialogueView d) || !(World.FeastNow is Feast feast)) return Array.Empty<(string, string?)>();
                var list = new List<(string, string?)>();
                if (IsFeastMerchant(d.Npc)) list.Add((Text("text.ui.fete.stall_answer"), null));
                foreach (FeastActivity a in feast.Activities)
                {
                    if (a.Host != d.Npc) continue;
                    string label = ActivityDoneToday(feast, a)
                        ? string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.activity_done"), Text(a.Name))
                        : string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.activity_answer"), Text(a.Name), a.Coins, Text(feast.Currency.Name));
                    list.Add((label, a.Id));
                }
                return list;
            }
        }

        /// <summary>L'activité <paramref name="a"/> a déjà rapporté aujourd'hui (essai à blanc des règles, rien ne change).</summary>
        private bool ActivityDoneToday(Feast feast, FeastActivity a) =>
            World.FeastBook is FeastBook book && World.FeastDay(feast.Festival) is (long edition, long day)
            && FeastRules.Earn(World.FeastState, book, feast.Festival, a.Id, edition, day, out _, out _) == FeastRefusal.AlreadyDone;

        /// <summary>Réponse de fête de rang <paramref name="index"/> (après celles de l'histoire et des cartes).</summary>
        private DialogueRefusal ChooseFeast(int index)
        {
            IReadOnlyList<(string Label, string? Activity)> choices = FeastChoices;
            if (index < 0 || index >= choices.Count) return DialogueRefusal.NoSuchChoice;
            if (choices[index].Activity is string activity) JoinFeastActivity(activity);
            else OpenFeastStall();
            return DialogueRefusal.None;
        }

        /// <summary>Prend part à l'activité <paramref name="activity"/> de la fête en cours (son invité à portée) ; annonces ou refus.</summary>
        public bool JoinFeastActivity(string activity)
        {
            FeastRefusal refusal = World.JoinFeast(activity, out _, out IReadOnlyList<StoryEvent> events);
            if (refusal != FeastRefusal.None)
            {
                FeastRefused(refusal, null);
                return false;
            }
            _dirty = true;
            if (World.FeastNow?.Activity(activity) is FeastActivity a) Say("text.ui.fete.joined", Text(a.Name));
            foreach (StoryEvent e in events) Story(e);
            return true;
        }

        /// <summary>Ouvre l'étal de fête (le forain là et à portée, pendant une fête) ; sinon le motif s'affiche et rien ne change.</summary>
        public bool OpenFeastStall()
        {
            FeastRefusal refusal = World.FeastShopOpen(out _);
            if (refusal != FeastRefusal.None)
            {
                FeastRefused(refusal, null);
                return false;
            }
            CloseDialogue();
            Shop = null;
            GuildOpen = false;
            FeastStallOpen = true;
            _feastStallRequest = true;
            return true;
        }

        private bool _feastStallRequest;

        /// <summary>L'étal vient d'être ouvert (depuis le dialogue) : vrai une seule fois, pour que l'écran montre son panneau.</summary>
        public bool TakeFeastStallRequest()
        {
            bool asked = _feastStallRequest && FeastStallOpen;
            _feastStallRequest = false;
            return asked;
        }

        public void CloseFeastStall()
        {
            FeastStallOpen = false;
            _feastStallRequest = false;
        }

        /// <summary>Ferme l'étal et parle au forain (son dialogue de l'histoire).</summary>
        public bool TalkToFeastMerchant()
        {
            FeastStallOpen = false;
            if (!(World.FeastBook is FeastBook book)) return false;
            DialogueRefusal refusal = World.Talk(book.Merchant, out _);
            if (refusal == DialogueRefusal.TooFar) Say("text.ui.too_far");
            return refusal == DialogueRefusal.None;
        }

        /// <summary>Titre de l'étal ouvert (« Étal de fête · Nuit des Lanternes »), ou null.</summary>
        public string? FeastStallTitle => World.FeastNow is Feast f ? Text("text.ui.fete.stall_title") : null;

        /// <summary>Sous-titre : la fête, son accroche et le forain.</summary>
        public string? FeastStallSubtitle => World.FeastNow is Feast f && World.FeastBook is FeastBook book
            ? FestivalName(f.Festival) + " · " + Text(_story.Npc(book.Merchant).Name)
            : null;

        /// <summary>Accroche de la fête en cours.</summary>
        public string? FeastMotto => World.FeastNow is Feast f ? Text(f.Motto) : null;

        /// <summary>
        /// Les offres de l'étal de fête : décorations, costume, plat, récompense de l'an dernier (rattrapage), avec leur prix en monnaie de la
        /// fête et ce que donnerait l'achat ; vide si l'étal n'est pas ouvert.
        /// </summary>
        public IReadOnlyList<FeastShopLine> FeastShopLines()
        {
            if (!(World.FeastNow is Feast feast)) return Array.Empty<FeastShopLine>();
            string currency = Text(feast.Currency.Name);
            var lines = new List<FeastShopLine>();
            foreach (WorldSession.FeastOffer o in World.FeastShop())
            {
                FeastRef r = o.Ref;
                string price = o.Status switch
                {
                    FeastRefusal.AlreadyOwned => Text("text.ui.fete.owned"),
                    FeastRefusal.NothingToCatchUp => Text("text.ui.fete.nothing_to_catch_up"),
                    FeastRefusal.DishesFull => string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.dishes_full"), World.FeastBook!.MaxDishes),
                    _ => string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.price"), o.Price, currency),
                };
                if (r.Category == FeastCategory.Dish && o.Status != FeastRefusal.DishesFull)
                    price += " · " + string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.dishes_kept"), World.FeastState.Of(feast.Festival).Dishes);
                lines.Add(new FeastShopLine(r.Code, Text(r.Name), Text(KindKey(r.Category)), r.Category, o.Price, price, o.Status == FeastRefusal.None,
                    o.Status == FeastRefusal.NotEnoughCoins));
            }
            return lines;
        }

        private static string KindKey(FeastCategory c) => c switch
        {
            FeastCategory.Decoration => "text.ui.fete.kind.decoration",
            FeastCategory.Costume => "text.ui.fete.kind.costume",
            FeastCategory.Dish => "text.ui.fete.kind.dish",
            FeastCategory.Reward => "text.ui.fete.kind.catch_up",
            _ => "text.ui.fete.kind.other",
        };

        /// <summary>Achète la référence <paramref name="code"/> à l'étal de fête ; annonce ou refus.</summary>
        public bool BuyFeastOffer(string code)
        {
            Feast? feast = World.FeastNow;
            FeastRefusal refusal = World.BuyFeast(code);
            FeastRef? r = World.FeastBook?.Ref(code);
            if (refusal != FeastRefusal.None)
            {
                FeastRefused(refusal, feast);
                return false;
            }
            _dirty = true;
            if (r != null && feast != null)
            {
                if (r.Category == FeastCategory.Reward) Say("text.ui.fete.caught_up", Text(r.Name));
                else Say("text.ui.fete.bought", Text(r.Name), r.Price ?? 0, Text(feast.Currency.Name));
            }
            return true;
        }

        /// <summary>La monnaie de la fête en cours : « Sou des Lanternes · 120 / 999 », ou null hors fête.</summary>
        public string? FeastWalletText => World.FeastNow is Feast f && World.FeastBook is FeastBook book
            ? Text(f.Currency.Name) + " · " + World.FeastState.Of(f.Festival).Coins.ToString(CultureInfo.InvariantCulture) + " / "
              + book.WalletCap.ToString(CultureInfo.InvariantCulture)
            : null;

        /// <summary>Où en est la fête en cours : activités faites dans l'édition sur celles qu'il faut, ou fête gagnée cette année.</summary>
        public string? FeastProgressText
        {
            get
            {
                if (!(World.FeastNow is Feast f) || f.RewardAfter <= 0 || !(World.FeastDay(f.Festival) is (long edition, long _))) return null;
                SavedFeast state = World.FeastState.Of(f.Festival);
                if (state.HasReward(edition)) return string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.progress_won"), Text(f.Reward.Name));
                int done = Math.Min(state.DoneIn(edition).Count, f.RewardAfter);
                return string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.progress"), done, f.RewardAfter, Text(f.Reward.Name));
            }
        }

        /// <summary>
        /// Les monnaies de fête du personnage pour la bourse : celle de la fête en cours d'abord (même à zéro), puis les autres qui ont des
        /// pièces, dans l'ordre de l'année.
        /// </summary>
        public IReadOnlyList<FeastWalletLine> FeastWallets
        {
            get
            {
                if (!(World.FeastBook is FeastBook book)) return Array.Empty<FeastWalletLine>();
                string? now = World.FeastNow?.Festival;
                var lines = new List<FeastWalletLine>();
                foreach (Feast f in book.Feasts.OrderBy(f => f.Festival == now ? 0 : 1).ThenBy(f => f.Number))
                {
                    long coins = World.FeastState.Of(f.Festival).Coins;
                    if (coins <= 0 && f.Festival != now) continue;
                    lines.Add(new FeastWalletLine(f.Festival, Text(f.Currency.Name), coins, book.WalletCap, FeastLooks.IconOf(f.Currency.Code), f.Festival == now));
                }
                return lines;
            }
        }

        /// <summary>
        /// L'étal de fête à poser sur la place (affichage seulement) : la fête, la place du forain et les décorations du mois qui l'habillent
        /// (<see cref="WorldSession.FeastStall"/>) ; null hors fête ou sans forain.
        /// </summary>
        public FeastStallLook? FeastStallLook => World.FeastStall is { } s
            ? new FeastStallLook(s.Feast.Festival, s.Position.X.Raw / 10000.0, s.Position.Z.Raw / 10000.0, s.Decor.Select(r => (r.Code, r.Size.X.Raw / 10000.0, r.Size.Y.Raw / 10000.0, r.Size.Z.Raw / 10000.0)).ToList())
            : null;

        /// <summary>Regard au repos du forain (degrés, 0 = +z) : vers le point sûr le plus proche, comme tout PNJ (WorldMarkers).</summary>
        public double FeastMerchantFace(double x, double z)
        {
            double best = double.MaxValue, face = 0;
            foreach (EdS.Data.Zones.SafePoint p in _zone.SafePoints)
            {
                double px = p.Position.X.Raw / 10000.0, pz = p.Position.Z.Raw / 10000.0, d = (px - x) * (px - x) + (pz - z) * (pz - z);
                if (d > 0.01 && d < best)
                {
                    best = d;
                    face = StyleRules.FaceYaw(x, z, px, pz);
                }
            }
            return face;
        }

        /// <summary>Obstacles de la carte entière, en polygones (m).</summary>
        private List<IReadOnlyList<(double X, double Z)>> FeastObstacles() => RegionMap.Whole.Obstacles
            .Select(o => (IReadOnlyList<(double X, double Z)>)o.Points.Select(q => (q.X.Raw / 10000.0, q.Z.Raw / 10000.0)).ToList()).ToList();

        /// <summary>
        /// Relecture indépendante de la PR #218 : les points qu'aucun décor de fête ne couvre : les PNJ (place, places de la journée et de
        /// pluie), sauf <paramref name="except"/> ; les invités de toutes les fêtes ; les points sûrs ; les portes.
        /// </summary>
        public IReadOnlyList<(double X, double Z)> FeastAvoid(string? except = null)
        {
            var avoid = new List<(double X, double Z)>();
            foreach (EdS.Data.Story.Npc n in _story.Npcs)
            {
                if (n.Id == except) continue;
                avoid.Add((n.Position.X.Raw / 10000.0, n.Position.Z.Raw / 10000.0));
                foreach (EdS.Data.Story.ScheduleSlot slot in n.Schedule)
                {
                    if (slot.Zone != null) continue;
                    avoid.Add((slot.Position.X.Raw / 10000.0, slot.Position.Z.Raw / 10000.0));
                    if (slot.Rain is EdS.Data.Story.SchedulePlace rain) avoid.Add((rain.Position.X.Raw / 10000.0, rain.Position.Z.Raw / 10000.0));
                }
            }
            foreach (EdS.Data.Story.Festival f in _story.Festivals)
                foreach (EdS.Data.Story.FestivalGuest g in f.Guests)
                    if (g.Npc != except) avoid.Add((g.Position.X.Raw / 10000.0, g.Position.Z.Raw / 10000.0));
            foreach (EdS.Data.Zones.SafePoint p in _zone.SafePoints) avoid.Add((p.Position.X.Raw / 10000.0, p.Position.Z.Raw / 10000.0));
            foreach (MapDoor d in RegionMap.Whole.Doors) avoid.Add((d.X, d.Z));
            return avoid;
        }

        /// <summary>
        /// L'étal de fête de la fête <paramref name="feast"/> et ses décorations, posés pour l'affichage (<see cref="FeastLooks.Stall"/>, sur la
        /// carte entière : obstacles, rues, PNJ, invités, points sûrs, portes) ; null sans forain.
        /// </summary>
        public (double X, double Z, double Yaw, IReadOnlyList<FeastPiece> Pieces)? FeastStallOf(Feast feast)
        {
            if (!(World.FeastBook is FeastBook book) || !(_story.Npcs.FirstOrDefault(n => n.Id == book.Merchant) is EdS.Data.Story.Npc merchant)) return null;
            double mx = merchant.Position.X.Raw / 10000.0, mz = merchant.Position.Z.Raw / 10000.0;
            var decor = feast.Stall.Select(r => (r.Code, r.Size.X.Raw / 10000.0, r.Size.Y.Raw / 10000.0, r.Size.Z.Raw / 10000.0)).ToList();
            return FeastLooks.Stall(mx, mz, FeastMerchantFace(mx, mz), decor, FeastObstacles(), RegionMap.Whole.Roads, FeastAvoid(book.Merchant));
        }

        private string? _feastStallKey;
        private (double X, double Z, double Yaw, IReadOnlyList<FeastPiece> Pieces)? _feastStallLayout;

        /// <summary>L'étal de la fête en cours à poser (<see cref="FeastStallOf"/>), calculé une fois par fête ; null hors fête, hors de la zone de l'histoire, sans forain.</summary>
        public (double X, double Z, double Yaw, IReadOnlyList<FeastPiece> Pieces)? FeastStallLayout
        {
            get
            {
                if (World.FeastStall is null || !(World.FeastNow is Feast feast)) return null;
                if (_feastStallKey != feast.Festival)
                {
                    _feastStallLayout = FeastStallOf(feast);
                    _feastStallKey = feast.Festival;
                }
                return _feastStallLayout;
            }
        }

        private string? _feastDecorKey;
        private IReadOnlyList<FeastPiece> _feastDecor = Array.Empty<FeastPiece>();

        /// <summary>
        /// Sous-lot F5 (décor du mois) : les décorations de la fête en cours posées dans les lieux du décor (<see cref="FeastDecorPlaces"/>,
        /// <see cref="FeastLooks.Village"/>) ; vide hors fête, en intérieur ou hors de la zone de l'histoire. Relecture indépendante de la
        /// PR #219 : calculé une fois par fête **et par zone** (la clé porte la zone : une fête commencée en intérieur, ou une sauvegarde reprise
        /// en intérieur, ne garde pas une liste vide pour le mois).
        /// </summary>
        public IReadOnlyList<FeastPiece> FeastDecorPieces
        {
            get
            {
                if (World.FeastStall is null || !(World.FeastNow is Feast feast)) return Array.Empty<FeastPiece>();
                string key = feast.Festival + "|" + World.Definition?.Id;
                if (_feastDecorKey != key)
                {
                    _feastDecor = FeastDecorOf(feast);
                    _feastDecorKey = key;
                }
                return _feastDecor;
            }
        }

        /// <summary>
        /// Lieux du décor du mois (décisions de Damien du 19 novembre 2026) : la place du village et la place du marché du Bourg (lieux des
        /// données), devant l'auberge et au parvis de la chapelle (un carré de 14 m devant leur porte, côté rue), aux portes du Bourg fortifié
        /// (un carré de 16 m à 9 m de chaque porte, côté ville).
        /// </summary>
        public IReadOnlyList<(string Id, double MinX, double MinZ, double MaxX, double MaxZ)> FeastDecorPlaces()
        {
            AdventureMap map = RegionMap.Whole;
            var places = FeastLooks.DecorPlaces.Select(id => map.Places.FirstOrDefault(p => p.Id == id)).Where(p => p != null)
                .Select(p => (p!.Id, p.MinX, p.MinZ, p.MaxX, p.MaxZ)).ToList();
            foreach (string building in FeastLooks.DecorDoors)
            {
                if (!(map.Doors.FirstOrDefault(d => d.Building == building) is MapDoor door)
                    || !(map.Obstacles.FirstOrDefault(o => o.Id == building) is EdS.Domain.Space.SpaceObstacle body)) continue;
                double bx = body.Points.Average(q => q.X.Raw / 10000.0), bz = body.Points.Average(q => q.Z.Raw / 10000.0);
                (double cx, double cz) = FeastLooks.Ahead(door.X, door.Z, door.X - bx, door.Z - bz, 7);
                places.Add(("devant:" + building, cx - 7, cz - 7, cx + 7, cz + 7));
            }
            if (map.Places.FirstOrDefault(p => p.Id == "ville_du_bourg") is MapPlace town)
                foreach (BourgPiece gate in map.Bourg.Pieces.Where(b => FeastLooks.IsTownGate(b.Piece)))
                {
                    (double cx, double cz) = FeastLooks.Ahead(gate.X, gate.Z, town.CenterX - gate.X, town.CenterZ - gate.Z, 9);
                    places.Add(("porte:" + gate.Piece + "@" + Math.Round(gate.X).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(gate.Z).ToString(CultureInfo.InvariantCulture),
                        cx - 8, cz - 8, cx + 8, cz + 8));
                }
            return places;
        }

        /// <summary>Les décorations de la fête <paramref name="feast"/> dans les lieux du décor (<see cref="FeastDecorPieces"/>), quel que soit le jour.</summary>
        public IReadOnlyList<FeastPiece> FeastDecorOf(Feast feast)
        {
            AdventureMap map = RegionMap.Whole;
            var obstacles = FeastObstacles();
            var doors = map.Doors.Select(d => (d.X, d.Z)).ToList();
            var avoid = FeastAvoid().ToList();
            foreach (BourgPiece b in map.Bourg.Pieces)   // accessoires du Bourg fortifié (pas les sols : pavés, rues, bordures)
                if (!BourgModels.IsGround(b.Piece) && b.Covers is null) avoid.Add((b.X, b.Z));
            // L'étal du forain et ses décorations : jamais recouverts.
            var taken = new List<IReadOnlyList<(double X, double Z)>>();
            if (FeastStallOf(feast) is { } stall)
            {
                taken.Add(FeastLooks.Box(stall.X, stall.Z, stall.Yaw, FeastLooks.StallWidth + 1, FeastLooks.StallDepth + 1));
                foreach (FeastPiece p in stall.Pieces) taken.Add(FeastLooks.Box(p.X, p.Z, p.Yaw, p.Width, p.Depth));
            }
            var decor = feast.Decorations.Select(r => (r.Code, r.Size.X.Raw / 10000.0, r.Size.Y.Raw / 10000.0, r.Size.Z.Raw / 10000.0)).ToList();
            return FeastLooks.Village(decor, FeastDecorPlaces(), obstacles, map.Roads, doors, avoid, taken);
        }

        // --- Sous-lot F6 : plats, ambiance, tenues -------------------------------------------------------------------------------

        /// <summary>Un plat de fête gardé : la fête, le nom du plat, combien, son icône.</summary>
        public sealed class FeastDishLine
        {
            public string Festival { get; }
            public string Name { get; }
            public int Count { get; }
            public string Icon { get; }

            internal FeastDishLine(string festival, string name, int count, string icon)
            {
                Festival = festival;
                Name = name;
                Count = count;
                Icon = icon;
            }
        }

        /// <summary>Les plats de fête gardés (achetés chez le forain), dans l'ordre de l'année ; vide sans catalogue.</summary>
        public IReadOnlyList<FeastDishLine> FeastDishes => World.FeastBook is FeastBook book
            ? book.Feasts.Where(f => World.FeastState.Of(f.Festival).Dishes > 0)
                .Select(f => new FeastDishLine(f.Festival, Text(f.Dish.Name), World.FeastState.Of(f.Festival).Dishes, FeastLooks.IconOf(f.Dish.Code))).ToList()
            : (IReadOnlyList<FeastDishLine>)Array.Empty<FeastDishLine>();

        /// <summary>Durée du réconfort d'un plat de fête, en minutes du monde.</summary>
        public const int ComfortMinutes = WorldSession.ComfortMinutes;

        /// <summary>Le dernier plat mangé (pour le bandeau du réconfort), hors sauvegarde.</summary>
        private string? _comfortDish;

        /// <summary>
        /// Mange un plat de la fête <paramref name="festival"/> (<see cref="WorldSession.EatFeastDish"/>) : un de moins, et un moment de
        /// réconfort annoncé et affiché (<see cref="ComfortText"/>) ; **aucune statistique de combat** (ni soin, ni bonus). Refus : rien ne change.
        /// </summary>
        public bool EatFeastDish(string festival)
        {
            FeastRefusal refusal = World.EatFeastDish(festival);
            if (refusal != FeastRefusal.None)
            {
                if (refusal == FeastRefusal.NoDish) Say("text.ui.fete.refused.no_dish");
                else if (refusal == FeastRefusal.NotNow) Say("text.ui.fete.refused.eat_combat");
                else FeastRefused(refusal, null);
                return false;
            }
            _dirty = true;
            string dish = Text(World.FeastBook!.Feast(festival).Dish.Name);
            Say("text.ui.fete.eaten", dish);
            _comfortDish = dish;
            Combat.SetComfort(World.ComfortWanted);   // sous-lot F7 : la marche plus rapide part au prochain tick
            return true;
        }

        /// <summary>« Réconforté (Tourte des vœux) » pendant <see cref="ComfortMinutes"/> minutes du monde après un plat ; null sinon.</summary>
        public string? ComfortText
        {
            get
            {
                if (_comfortDish is null || !(World.ComfortUntilMinute is long until)) return null;
                WorldTime t = World.Zone.Time;
                return t.Day * 1440 + t.Minute < until ? string.Format(CultureInfo.InvariantCulture, Text("text.ui.fete.comfort"), _comfortDish) : null;
            }
        }

        /// <summary>L'ambiance de la fête en cours (<see cref="FeastMood.Of"/>), dehors dans la zone de l'histoire ; aucune sinon.</summary>
        public FeastMood FeastMood => !Indoors && World.InStoryZone && World.FeastNow is Feast f ? FeastMood.Of(f.Number) : FeastMood.None;

        /// <summary>Numéro de la fête d'Orsédia en cours (1 à 12) pour la tenue des habitants (<see cref="HumanWardrobe"/>), 0 sinon.</summary>
        public int FeastNumber => World.FeastNow?.Number ?? 0;

        /// <summary>Annonces des gains de fête (activités et concours de l'histoire).</summary>
        private void FeastMessage(StoryEvent e)
        {
            if (!(World.FeastBook is FeastBook book) || !(book.Of(e.Id) is Feast f)) return;
            string currency = Text(f.Currency.Name);
            switch (e.Kind)
            {
                case StoryEventKind.FeastCoins:
                    Say("text.ui.fete.coins", e.Value, currency, World.FeastState.Of(f.Festival).Coins, book.WalletCap);
                    break;
                case StoryEventKind.FeastWalletFull:
                    Say("text.ui.fete.wallet_full", currency, book.WalletCap, e.Value);
                    break;
                case StoryEventKind.FeastWon:
                    if (e.Step == "title") Say("text.ui.fete.won_title", FestivalName(f.Festival), Text(f.Reward.Name), Text(f.TitleName));
                    else Say("text.ui.fete.won", FestivalName(f.Festival), Text(f.Reward.Name));
                    break;
                case StoryEventKind.FeastCountersFull:
                    Say("text.ui.fete.counters_full");
                    break;
            }
        }

        private void FeastRefused(FeastRefusal refusal, Feast? feast)
        {
            switch (refusal)
            {
                case FeastRefusal.AlreadyDone: Say("text.ui.fete.refused.done"); return;
                case FeastRefusal.TooFar: Say("text.ui.too_far"); return;
                case FeastRefusal.MerchantAway: Say("text.ui.fete.refused.away"); return;
                case FeastRefusal.AlreadyOwned: Say("text.ui.fete.refused.owned"); return;
                case FeastRefusal.DishesFull: Say("text.ui.fete.refused.dishes", World.FeastBook?.MaxDishes ?? 0); return;
                case FeastRefusal.NotEnoughCoins: Say("text.ui.fete.refused.coins", feast != null ? Text(feast.Currency.Name) : ""); return;
                case FeastRefusal.NothingToCatchUp: Say("text.ui.fete.refused.catch_up"); return;
                case FeastRefusal.CountersFull: Say("text.ui.fete.counters_full"); return;
                case FeastRefusal.NoFeast:
                case FeastRefusal.NotNow: Say("text.ui.fete.refused.not_now"); return;
                default: Say("text.ui.fete.refused.other"); return;
            }
        }
    }

    /// <summary>L'étal de fête posé sur la place : la fête, la place du forain (m) et les décorations du mois (code, enveloppe X, Y, Z en m).</summary>
    public sealed class FeastStallLook
    {
        public string Festival { get; }
        public double X { get; }
        public double Z { get; }
        public IReadOnlyList<(string Code, double SizeX, double SizeY, double SizeZ)> Decor { get; }

        public FeastStallLook(string festival, double x, double z, IReadOnlyList<(string Code, double SizeX, double SizeY, double SizeZ)> decor)
        {
            Festival = festival;
            X = x;
            Z = z;
            Decor = decor;
        }

        /// <summary>Clé de reconstruction : une autre fête (ou un forain ailleurs) refait l'étal.</summary>
        public string Key => Festival + "@" + X.ToString("0.##", CultureInfo.InvariantCulture) + "," + Z.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
