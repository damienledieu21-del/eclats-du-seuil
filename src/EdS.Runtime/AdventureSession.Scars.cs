using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.World;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        /// <summary>Sous-lot V2.6 : annonce d'une cicatrice (son palier et son lieu dans le texte).</summary>
        public static bool IsScarNotice(WorldEventNoticeKind kind) => kind >= WorldEventNoticeKind.ScarBorn && kind <= WorldEventNoticeKind.ScarSpread;

        /// <summary>Sous-lot V2.6 : lieu d'une cicatrice, pour les textes (« aux ruines »), sinon une formule commune.</summary>
        public string ScarPlace(string eventId, string group)
        {
            string key = $"text.event.{eventId}.lieu.{group}";
            return Texts.Has(key) ? Text(key) : string.Empty;
        }

        /// <summary>Sous-lot V2.6 : cicatrices de la zone (couleurs fanées, brume froide, échos rôdeurs).</summary>
        public IReadOnlyList<ScarView> Scars => World.Scars;

        /// <summary>Portée de la touche d'action pour le cœur d'une cicatrice (un peu plus que la portée du monde, qui fait foi).</summary>
        public static readonly Fixed ScarReach = Fixed.FromInt(4);

        /// <summary>Sous-lot V2.6 : cicatrice dont le cœur est à portée (pour la purifier), ou null ; jamais pendant qu'une faille y est ouverte.</summary>
        public ScarView? ScarInReach
        {
            get
            {
                // Relecture de la PR #110 : lue à chaque image : une boucle sur la liste gardée par le monde, sans allocation.
                IReadOnlyList<ScarView> scars = Scars;
                if (scars.Count == 0 || World.IsDefeated || !(Me is GroundVector p)) return null;
                for (int i = 0; i < scars.Count; i++)
                    if (!scars[i].RiftOpen && Within(p, scars[i].Position, ScarReach)) return scars[i];
                return null;
            }
        }

        /// <summary>Sous-lot V2.6 : purifie la cicatrice à portée ; un message dit pourquoi si c'est refusé.</summary>
        public bool PurifyScar()
        {
            if (!(ScarInReach is ScarView scar)) return false;
            ScarRefusal refusal = World.PurifyScar(scar.Key);
            if (refusal == ScarRefusal.TooFar) Say("text.ui.scar.too_far");
            else if (refusal == ScarRefusal.HauntsRemain) Say("text.ui.scar.haunts");
            else if (refusal == ScarRefusal.RiftOpen) Say("text.ui.scar.rift_open");
            else if (refusal == ScarRefusal.NoItem) Say("text.ui.scar.no_item");
            if (refusal == ScarRefusal.None) _dirty = true;
            return refusal == ScarRefusal.None;
        }

        /// <summary>
        /// Sous-lot V2.6 : à quel point les couleurs se fanent là où est le personnage, en millièmes (0 : rien ; 1 000 : au cœur d'une cicatrice
        /// au palier maximal). Pour chaque cicatrice : son palier sur le palier maximal, fois <c>1 - d²/r²</c> (d : distance au cœur, r : rayon) ;
        /// la plus forte compte. Calcul entier, affichage seulement.
        /// </summary>
        public int ScarFade => Me is GroundVector p ? FadeAt(Scars, p) : 0;

        /// <summary>Sous-lot V2.6 : même calcul en un point quelconque (Unity : le ciel et la brume suivent la caméra ; tests).</summary>
        public static int FadeAt(IReadOnlyList<ScarView> scars, GroundVector p)
        {
            if (scars is null) throw new ArgumentNullException(nameof(scars));
            long best = 0;
            foreach (ScarView v in scars)
            {
                long r2 = checked(v.Radius.Raw * v.Radius.Raw), d2 = Distance2(p, v.Position);
                if (r2 <= 0 || d2 >= r2) continue;
                // (palier / max) × (r² - d²) / r², en millièmes, sans débordement : r² ≤ (60 m)² en unités brutes (3,6 × 10¹³).
                long fade = checked(1000L * v.Stage * (r2 - d2)) / checked((long)v.MaxStage * r2);
                if (fade > best) best = fade;
            }
            return (int)Math.Min(1000, best);
        }

        /// <summary>Sous-lot V2.6 : libellé de la cicatrice la plus marquée autour du personnage (« Cicatrice du Seuil · palier 2 sur 4 »), ou null.</summary>
        public string? ScarLabel
        {
            get
            {
                IReadOnlyList<ScarView> scars = Scars;
                if (scars.Count == 0 || !(Me is GroundVector p)) return null;
                ScarView? v = null;
                for (int i = 0; i < scars.Count; i++)
                    if (Within(p, scars[i].Position, scars[i].Radius) && (v is null || scars[i].Stage > v.Stage)) v = scars[i];
                if (v is null) return null;
                if (ReferenceEquals(v, _labelOf)) return _label;   // relecture de la PR #110 : le même texte tant que la vue ne change pas
                string stage = Texts.Has($"text.ui.scar.stage.{v.Stage}") ? " — " + Text($"text.ui.scar.stage.{v.Stage}") : string.Empty;
                _labelOf = v;
                return _label = string.Format(System.Globalization.CultureInfo.InvariantCulture, Text("text.ui.scar.label"), v.Stage, v.MaxStage) + stage;
            }
        }

        private ScarView? _labelOf;
        private string? _label;
    }

    /// <summary>
    /// Sous-lot V2.6 : pour l'essai du Seuil qui grandit (Unity, menu « Essai du Seuil qui grandit ») : crée une cicatrice dans la sauvegarde,
    /// avance les jours du monde, ou les efface toutes. Une copie de la sauvegarde est gardée avant chaque essai.
    /// </summary>
    public static class ScarTrial
    {
        /// <summary>
        /// Crée (ou remplace) la cicatrice de la variante <paramref name="group"/> de la faille du Seuil, au palier <paramref name="stage"/>,
        /// née aujourd'hui (jour du monde de la sauvegarde) ; rend le message à montrer.
        /// </summary>
        public static string Create(string path, ZoneDefinition zone, string group, int stage, string? stamp = null)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save) || save.Character is null) return "Sauvegarde illisible ou sans personnage : " + (read.Problem ?? "lance d'abord l'aventure une fois.");
            WorldEventDefinition? e = zone.Events.FirstOrDefault(x => x.Scar != null && x.Groups.Any(g => g.Id == group));
            if (e is null) return $"Aucune faille à cicatrice « {group} » dans la zone : rien n'a changé.";
            stage = Math.Max(1, Math.Min(e.Scar!.MaxStage, stage));
            var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
            long day = clock.At(save.WorldTick).Day;
            string key = WorldScarRules.Key(zone.Id, e.Id, group);
            var scars = save.Character.Scars.Where(x => x.Id != key).Concat(new[] { new SavedScar(key, stage, day, day) }).ToList();
            if (scars.Count > SavedScar.MaxEntries) return "Trop de cicatrices : rien n'a changé.";
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, save.Character.WithScars(scars), save.Migrated));
            GroundVector at = e.Groups.First(g => g.Id == group).Position!.Value;
            string haunts = stage >= e.Scar.HauntStage ? " Des échos y rôdent : viens à bout d'eux," : "";
            return $"Cicatrice créée {Where(group)} (x {at.X}, z {at.Z}), palier {stage} sur {e.Scar.MaxStage}, jour {day}. Elle gagne un palier tous les {e.Scar.GrowDays} jours du monde."
                + $"{haunts} Pour la purifier, approche-toi de son cœur avec un éclat de Seuil et appuie sur F. Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Avance la sauvegarde de <paramref name="days"/> jours du monde (même heure) ; rend le message à montrer.</summary>
        public static string AdvanceDays(string path, int days, long tickMilliseconds = 50, string? stamp = null)
        {
            if (days < 1 || days > 30) throw new ArgumentOutOfRangeException(nameof(days));
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(tickMilliseconds);
            long tick = checked(save.WorldTick + days * clock.TicksPerDay);
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, DayTrial.Advanced(save, tick));
            return $"Partie avancée de {days} jour{(days > 1 ? "s" : "")} du monde : {clock.At(tick)} (tick {tick}). Les cicatrices ont grandi d'autant ; les failles de ces jours-là n'ont pas eu lieu pendant l'essai. Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Efface toutes les cicatrices de la sauvegarde ; rend le message à montrer.</summary>
        public static string Clear(string path, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save) || save.Character is null) return "Sauvegarde illisible ou sans personnage.";
            if (save.Character.Scars.Count == 0) return "Aucune cicatrice : rien n'a changé.";
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, save.Character.WithScars(Array.Empty<SavedScar>()), save.Migrated));
            return $"Cicatrices effacées. Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Même essai, la zone lue des données du dépôt (menu de l'éditeur).</summary>
        public static string Create(string path, string dataDirectory, string scenariosDirectory, string group, int stage, string? stamp = null) =>
            Create(path, EventTrial.LoadZone(dataDirectory, scenariosDirectory), group, stage, stamp);

        /// <summary>Sous-lot V2.6.1 : quête de Sœur Albe (« Les cicatrices du Seuil »).</summary>
        public const string ScarQuest = "quest.cicatrices_du_seuil";

        /// <summary>
        /// Sous-lot V2.6.1 (essai de la propagation) : une cicatrice aux ruines au palier de propagation, depuis le délai de propagation
        /// (<c>spreadDays</c> jours avant aujourd'hui), et rien au lac : à l'entrée dans les Vallons, le froid gagne le lac ; le pré suit
        /// <c>spreadDays</c> jours plus tard. Rend le message à montrer.
        /// </summary>
        public static string Propagate(string path, ZoneDefinition zone, string? stamp = null)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save) || save.Character is null) return "Sauvegarde illisible ou sans personnage : " + (read.Problem ?? "lance d'abord l'aventure une fois.");
            WorldEventDefinition? e = zone.Events.FirstOrDefault(x => x.Scar is WorldEventScar r && r.Spreads && x.Groups.Any(g => g.SpreadTo.Count > 0));
            if (e is null) return "Aucune faille dont la cicatrice se propage dans la zone : rien n'a changé.";
            WorldEventGroup from = e.Groups.First(g => g.SpreadTo.Count > 0);
            WorldEventScar rule = e.Scar!;
            long day = new WorldClock(zone.Setup.Combat.TickMilliseconds).At(save.WorldTick).Day;
            long since = Math.Max(0, day - rule.SpreadDays);
            string key = WorldScarRules.Key(zone.Id, e.Id, from.Id);
            var targets = new HashSet<string>(from.SpreadTo.Select(g => WorldScarRules.Key(zone.Id, e.Id, g)), StringComparer.Ordinal);
            var scars = save.Character.Scars.Where(x => x.Id != key && !targets.Contains(x.Id)).Concat(new[] { new SavedScar(key, rule.SpreadStage, since, since) }).ToList();
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, save.Character.WithScars(scars), save.Migrated));
            return $"Cicatrice au palier {rule.SpreadStage} {Where(from.Id)} depuis {rule.SpreadDays} jours du monde, et plus rien {string.Join(" ni ", from.SpreadTo.Select(Where))}. "
                + $"En entrant dans les Vallons, le froid gagne {Where(from.SpreadTo[0])} ; la voisine suivante {rule.SpreadDays} jours plus tard. Purifier la nouvelle accorde {rule.SpreadDays} jours de répit ; purifier la source arrête tout. Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>
        /// Sous-lot V2.6.1 (essai de la quête de Sœur Albe) : la première cicatrice connue, la quête commencée (première étape) et suivie,
        /// une cicatrice aux ruines au palier 1 (sans écho rôdeur) s'il n'y en a aucune, et deux éclats de Seuil dans le sac (s'ils tiennent).
        /// Rend le message à montrer.
        /// </summary>
        public static string StartQuest(string path, ZoneDefinition zone, string? stamp = null)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save) || save.Character is null) return "Sauvegarde illisible ou sans personnage : " + (read.Problem ?? "lance d'abord l'aventure une fois.");
            WorldEventDefinition? e = zone.Events.FirstOrDefault(x => x.Scar != null);
            if (e is null) return "Aucune faille à cicatrice dans la zone : rien n'a changé.";
            SavedCharacter hero = save.Character;
            SavedStory story = hero.Story;
            if (story.Quests.TryGetValue(ScarQuest, out QuestProgress? known) && known.IsDone) return "La quête des cicatrices est déjà finie pour ce personnage : rien n'a changé.";
            var quests = story.Quests.ToDictionary(q => q.Key, q => q.Value, StringComparer.Ordinal);
            quests[ScarQuest] = new QuestProgress("premiere");
            string[] facts = story.Facts.Concat(e.Scar!.BornFact is string born ? new[] { born } : Array.Empty<string>()).ToArray();
            hero = hero.WithStory(new SavedStory(facts, quests, story.Trust, story.Discoveries, ScarQuest, story.Guild, story.Hunted));
            string where = "";
            if (!hero.Scars.Any(s => s.Id.StartsWith(WorldScarRules.Key(zone.Id, e.Id, string.Empty), StringComparison.Ordinal)))
            {
                long day = new WorldClock(zone.Setup.Combat.TickMilliseconds).At(save.WorldTick).Day;
                WorldEventGroup first = e.Groups[0];
                hero = hero.WithScars(hero.Scars.Concat(new[] { new SavedScar(WorldScarRules.Key(zone.Id, e.Id, first.Id), 1, day, day) }));
                where = $" Une cicatrice t'attend {Where(first.Id)} (palier 1, sans écho rôdeur).";
            }
            string item = e.Scar.PurifyItem;
            var inventory = hero.Inventory.ToList();
            int i = inventory.FindIndex(x => x.Item == item && x.Properties.Count == 0 && x.Count <= 18);
            if (i >= 0) inventory[i] = new ItemStack(item, inventory[i].Count + 2);
            else inventory.Add(new ItemStack(item, 2));
            string shards = " Deux éclats de Seuil sont dans ton sac.";
            try
            {
                hero = hero.WithItems(inventory, hero.EquipmentInstances);
            }
            catch (ArgumentException)
            {
                shards = " Ton sac est plein : pas d'éclat de Seuil ajouté.";
            }
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, new WorldSave(save.Zone, save.WorldTick, save.Sessions, save.Consumed, hero, save.Migrated));
            return "Quête « Les cicatrices du Seuil » commencée et suivie : purifie une cicatrice, retourne voir Sœur Albe (à la chapelle, ou sur le parvis de 17 h à 18 h), puis purifie-en deux autres."
                + where + shards + $" Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Mêmes essais, la zone lue des données du dépôt (menu de l'éditeur).</summary>
        public static string Propagate(string path, string dataDirectory, string scenariosDirectory, string? stamp = null) =>
            Propagate(path, EventTrial.LoadZone(dataDirectory, scenariosDirectory), stamp);

        public static string StartQuest(string path, string dataDirectory, string scenariosDirectory, string? stamp = null) =>
            StartQuest(path, EventTrial.LoadZone(dataDirectory, scenariosDirectory), stamp);

        private static string Where(string group) => group switch
        {
            "ruines" => "dans les ruines",
            "lac" => "au bord du lac des Aubes",
            "pre" => "dans le pré, au nord du village",
            _ => "dans les Vallons",
        };
    }
}
