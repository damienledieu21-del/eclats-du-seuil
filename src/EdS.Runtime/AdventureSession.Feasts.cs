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
