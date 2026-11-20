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
            // Revue Codex de la PR #230 : tout ou rien. Le réconfort n'est gardé que si la halte (sauvegarde puis nouvelle session) réussit ;
            // si l'écriture échoue (disque plein, accès refusé), le réconfort et son nom d'avant reviennent, le motif s'affiche.
            long? comfortBefore = World.ComfortUntilMinute;
            string? dishBefore = _comfortDish;
            WorldSession before = World;
            HomeRefusal refusal = World.SleepInBed();
            if (refusal != HomeRefusal.None) return HomeRefused(refusal);
            _comfortDish = Text("text.ui.home.rested");
            try
            {
                Reopen("text.ui.home.slept");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                if (World == before) World.CarryComfort(comfortBefore);   // la session n'a pas changé : son réconfort revient
                _comfortDish = dishBefore;
                Say("text.ui.save_failed", e.Message);
                return false;
            }
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
    }
}
