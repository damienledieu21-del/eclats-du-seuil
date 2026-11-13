using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Data.Story;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V2.7 : une ligne de la réputation (carte d'aventurier) : la faction, son palier et sa valeur.</summary>
    public sealed class ReputationEntry
    {
        public string Faction { get; }
        public string Name { get; }
        public int Value { get; }
        public string Tier { get; }
        public string TierName { get; }

        /// <summary>Rang du palier (0 : haï, 3 : neutre, 6 : héros).</summary>
        public int TierIndex { get; }

        /// <summary>Texte affiché (« La garde des Vallons : Apprécié (120) »).</summary>
        public string Text { get; }

        internal ReputationEntry(string faction, string name, int value, int tier, string tierName, string text)
        {
            Faction = faction;
            Name = name;
            Value = value;
            TierIndex = tier;
            Tier = Reputations.Tiers[tier];
            TierName = tierName;
            Text = text;
        }
    }

    public sealed partial class AdventureSession
    {
        /// <summary>Sous-lot V2.7 : réputation auprès de chaque faction, dans l'ordre des factions (neutre partout sans histoire).</summary>
        public IReadOnlyList<ReputationEntry> Reputations()
        {
            var list = new List<ReputationEntry>();
            foreach (string f in EdS.Data.Story.Reputations.Factions)
            {
                int value = World.Narrative?.Reputation(f) ?? 0;
                int tier = EdS.Data.Story.Reputations.TierOf(value);
                string name = Text(EdS.Data.Story.Reputations.FactionText(f));
                string tierName = Text(EdS.Data.Story.Reputations.TierText(EdS.Data.Story.Reputations.Tiers[tier]));
                list.Add(new ReputationEntry(f, name, value, tier, tierName, string.Format(Text("text.ui.reputation.row"), name, tierName, value)));
            }
            return list.AsReadOnly();
        }

        /// <summary>Sous-lot V2.7 : nombre de fois où le couvre-feu a reconduit le personnage (tests, interface).</summary>
        public int Expulsions { get; private set; }

        /// <summary>
        /// Sous-lot V2.7 (réponse de Damien du 7 novembre 2026) : si le couvre-feu tombe (<see cref="WorldSession.CurfewSafePoint"/>), hors
        /// dialogue et hors suite obligée, le personnage est reconduit au point sûr du couvre-feu, comme un voyage rapide (session d'arrivée
        /// construite, puis la partie sauvegardée, puis la session ouverte) ; une sauvegarde impossible ne change rien (réessayé au tick suivant).
        /// </summary>
        private void EnforceCurfew()
        {
            if (!(World.CurfewSafePoint is string safePoint) || Dialogue != null || MustContinue) return;
            WorldSave here = World.Save();
            var moved = new WorldSave(_zone.Id, here.WorldTick, here.Sessions, here.Consumed, here.Character, sectors: here.Sectors);
            WorldSession next = Build(moved, _zone, safePoint, null, ArrivalPlace(moved, _zone, safePoint));
            moved = moved.WithResume(next.Resume);   // sous-lot V2.28 : la partie relancée reprend au point sûr du couvre-feu
            try
            {
                Write(moved);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                Say("text.ui.save_failed", e.Message);
                return;
            }
            Expulsions = checked(Expulsions + 1);
            Commit(next);
            Say("text.ui.curfew", _zone.SafePoints.First(s => s.Id == safePoint).Name);
        }

        /// <summary>Sous-lot V2.7 : seul un changement de palier s'annonce (les petits changements se lisent sur la carte d'aventurier).</summary>
        private void ReputationMessage(StoryEvent e)
        {
            if (e.Kind != StoryEventKind.StandingChanged || e.Step is null) return;
            Say(e.Value > 0 ? "text.ui.reputation.up" : "text.ui.reputation.down", Text(EdS.Data.Story.Reputations.FactionText(e.Id)),
                Text(EdS.Data.Story.Reputations.TierText(e.Step)));
        }
    }
}
