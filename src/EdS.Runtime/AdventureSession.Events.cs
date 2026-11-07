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
        /// <summary>
        /// Sous-lot V2.5 : clé du texte d'un moment d'un événement : celle de sa variante (« text.event.attaque_hameau.bandits.annonce »)
        /// si elle existe, sinon celle de l'événement (« text.event.attaque_hameau.annonce »), sinon null (rien à dire).
        /// </summary>
        public string? EventText(string eventId, string group, string moment)
        {
            string own = $"text.event.{eventId}.{group}.{moment}", common = $"text.event.{eventId}.{moment}";
            if (group.Length > 0 && Texts.Has(own)) return own;
            return Texts.Has(common) ? common : null;
        }

        /// <summary>Sous-lot V2.5 : mot du texte de chaque annonce d'un événement.</summary>
        public static string Moment(WorldEventNoticeKind kind) => kind switch
        {
            WorldEventNoticeKind.Announced => "annonce",
            WorldEventNoticeKind.Started => "debut",
            WorldEventNoticeKind.Wave => "vague",
            WorldEventNoticeKind.Won => "victoire",
            WorldEventNoticeKind.Lost => "defaite",
            WorldEventNoticeKind.Missed => "absence",
            WorldEventNoticeKind.Rewarded => "recompense",
            WorldEventNoticeKind.RewardWaiting => "attente",
            WorldEventNoticeKind.Thanks => "merci",
            WorldEventNoticeKind.ScarBorn => "cicatrice",
            WorldEventNoticeKind.ScarGrew => "cicatrice_grandit",
            WorldEventNoticeKind.ScarReceded => "cicatrice_recule",
            WorldEventNoticeKind.ScarHealed => "cicatrice_guerie",
            WorldEventNoticeKind.ScarPurified => "cicatrice_purifiee",
            WorldEventNoticeKind.ScarStirs => "cicatrice_rode",
            _ => "fin",
        };

        /// <summary>
        /// Sous-lot V2.5 : annonces d'événements depuis l'ouverture de l'aventure (la cloche du hameau et le cri de la faille de Unity suivent
        /// ce compteur et la dernière annonce). Affichage seulement.
        /// </summary>
        public int EventsAnnounced { get; private set; }

        /// <summary>Sous-lot V2.5 : dernière annonce d'un événement (pour le bandeau et le son), ou null.</summary>
        public WorldEventNotice? LastEventNotice { get; private set; }

        private void EventNotice(WorldEventNotice n)
        {
            // Sous-lot V2.6 : les annonces d'une cicatrice disent son palier ({0}) et son lieu ({1}).
            if (EventText(n.Event, n.Group, Moment(n.Kind)) is string key)
            {
                if (IsScarNotice(n.Kind)) Say(key, n.Wave, ScarPlace(n.Event, n.Group));
                else Say(key);
            }
            EventsAnnounced++;
            LastEventNotice = n;
            if (n.Kind == WorldEventNoticeKind.ScarPurified)
            {
                _dirty = true;
                if (n.Xp > 0) Say("text.ui.scar.purified", n.Xp);
            }
            if (n.Kind != WorldEventNoticeKind.Rewarded) return;
            _dirty = true;
            Say("text.ui.event.reward", n.Xp, MoneyText(n.Money));
            foreach ((string item, int count) in n.Items) Say("text.ui.reward", ItemName(item), count);
        }

        /// <summary>Sous-lot V2.5 : faille ouverte à portée (pour la refermer), ou null.</summary>
        public WorldEventView? RiftInReach => Me is GroundVector p && !World.IsDefeated
            ? World.WorldEvents.FirstOrDefault(v => v.Rift is GroundVector r && Within(p, r, RiftReach)) : null;

        /// <summary>Portée de la touche d'action pour une faille (un peu plus que la portée du monde, qui fait foi).</summary>
        public static readonly Fixed RiftReach = Fixed.FromInt(4);

        /// <summary>Sous-lot V2.5 : referme la faille à portée ; un message dit pourquoi si c'est refusé.</summary>
        public bool CloseRift()
        {
            if (!(RiftInReach is WorldEventView rift)) return false;
            RiftRefusal refusal = World.CloseRift(rift.Event.Id);
            if (refusal == RiftRefusal.TooFar) Say("text.ui.event.rift.too_far");
            else if (refusal == RiftRefusal.EchoesRemain) Say("text.ui.event.rift.echoes");
            else if (refusal == RiftRefusal.WavesRemain) Say("text.ui.event.rift.waves");
            if (refusal == RiftRefusal.None) _dirty = true;
            return refusal == RiftRefusal.None;
        }

        /// <summary>
        /// Sous-lot V2.5 : bandeau de l'événement le plus proche dans le temps (« En cours : L'attaque du hameau », « Bientôt : … »), ou null.
        /// </summary>
        public string? EventBanner
        {
            get
            {
                WorldEventView? v = World.WorldEvents.Where(x => x.Event.Settles || x.Phase == WorldEventPhase.Running).OrderBy(x => x.StartTick).FirstOrDefault();
                if (v is null || v.Phase == WorldEventPhase.Ending) return null;
                string name = Text($"text.event.{v.Event.Id}.name");
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, Text(v.Phase == WorldEventPhase.Running ? "text.ui.event.under_way" : "text.ui.event.coming"), name);
            }
        }
    }

    /// <summary>
    /// Sous-lot V2.5 : pour l'essai des événements (Unity, menu « Essai des événements »), avance la sauvegarde jusqu'à une occurrence d'un
    /// événement du monde, quelques minutes du monde avant son départ (on entend l'annonce puis la cloche). Seul le temps du monde avance ; une
    /// copie de la sauvegarde est gardée.
    /// </summary>
    public static class EventTrial
    {
        /// <summary>Zone des Vallons et ses événements, lue des données du dépôt (pour le menu de l'éditeur).</summary>
        public static ZoneDefinition LoadZone(string dataDirectory, string scenariosDirectory)
        {
            EdS.Data.Json.GameDataLoadResult loaded = EdS.Data.Json.GameDataLoader.Load(dataDirectory);
            EdS.Data.GameData data = loaded.Data ?? throw new System.IO.InvalidDataException("Données refusées.");
            EdS.Data.Items.ItemCatalog items = EdS.Data.Json.ItemJsonReader.ReadFile(EdS.Data.Json.ItemJsonReader.PathIn(dataDirectory), data).Catalog
                ?? throw new System.IO.InvalidDataException("Objets refusés.");
            ZoneLoadResult zone = EdS.Data.Json.ZoneJsonReader.ReadFile(EdS.Data.Json.ZoneJsonReader.PathIn(dataDirectory, AdventureSession.ZoneId), scenariosDirectory, data, items);
            return zone.Zone ?? throw new System.IO.InvalidDataException("Zone refusée : " + zone.Report);
        }

        /// <summary>Minutes du monde avant le départ où l'essai place la partie (l'annonce, si l'événement en a une, a déjà eu lieu).</summary>
        public const int LeadMinutes = 5;

        /// <summary>Prochaine occurrence de l'événement <paramref name="eventId"/> qui commence au moins <see cref="LeadMinutes"/> après le tick.</summary>
        public static EventOccurrence? NextOf(ZoneDefinition zone, string eventId, WorldClock clock, long fromTick)
        {
            if (zone is null) throw new ArgumentNullException(nameof(zone));
            if (clock is null) throw new ArgumentNullException(nameof(clock));
            if (!(zone.EventOf(eventId) is WorldEventDefinition e)) return null;
            long from = checked(WorldEventSchedule.Absolute(clock.At(fromTick)) + LeadMinutes);
            return WorldEventSchedule.Next(zone.Id, zone.Id, e, from);
        }

        /// <summary>Tick où l'essai place la partie : <see cref="LeadMinutes"/> minutes du monde avant le départ de <paramref name="o"/>.</summary>
        public static long TrialTick(WorldClock clock, EventOccurrence o) => WorldEventSchedule.TickAt(clock, checked(o.StartAt - LeadMinutes));

        /// <summary>
        /// Avance la sauvegarde <paramref name="path"/> jusqu'à la prochaine occurrence de <paramref name="eventId"/> ; rend le message à montrer
        /// (où aller, ce qui va se passer).
        /// </summary>
        public static string MoveSave(string path, ZoneDefinition zone, string eventId, string? stamp = null)
        {
            if (!System.IO.File.Exists(path)) return "Aucune sauvegarde : lance d'abord l'aventure une fois.";
            SaveReadResult read = SaveStore.Read(path);
            if (!(read.Save is WorldSave save)) return "Sauvegarde illisible : " + (read.Problem ?? "?");
            var clock = new WorldClock(zone.Setup.Combat.TickMilliseconds);
            if (!(NextOf(zone, eventId, clock, save.WorldTick) is EventOccurrence o)) return "Aucune occurrence de cet événement dans les jours qui viennent : rien n'a changé.";
            long tick = TrialTick(clock, o);
            string copy = DayTrial.KeepCopy(path, stamp);
            SaveStore.Write(path, DayTrial.Advanced(save, tick));
            string where = o.Event.Kind == WorldEventKind.Rift && o.Group.Position is GroundVector r
                ? $"La faille s'ouvre {Place(o.Group.Id)} (x {r.X}, z {r.Z}) : viens à bout des échos, puis approche-toi et appuie sur F pour la refermer."
                : o.Event.Kind == WorldEventKind.Raid
                    ? $"La cloche du hameau de la carrière va sonner ({(o.Group.Id == "loups" ? "des loups" : "des bandits")}) : fais halte au hameau de la carrière, ou voyage jusque-là."
                    : "Les loups vont sortir du bois près des sangliers des champs du sud.";
            if (o.Event.MinLevel > 1) where += $" Seulement pour un personnage de niveau {o.Event.MinLevel} ou plus (en dessous, rien ne se passe).";
            return $"Partie avancée au {clock.At(tick)} (tick {tick}), {LeadMinutes} minutes du monde avant le départ. {where} Copie d'avant l'essai : {System.IO.Path.GetFileName(copy)}.";
        }

        /// <summary>Même essai, la zone lue des données du dépôt (menu de l'éditeur, qui ne connaît pas les données).</summary>
        public static string MoveSave(string path, string dataDirectory, string scenariosDirectory, string eventId, string? stamp = null) =>
            MoveSave(path, LoadZone(dataDirectory, scenariosDirectory), eventId, stamp);

        private static string Place(string group) => group switch
        {
            "ruines" => "dans les ruines",
            "lac" => "au bord du lac des Aubes",
            "pre" => "dans le pré, au nord du village",
            _ => "dans les Vallons",
        };
    }
}
