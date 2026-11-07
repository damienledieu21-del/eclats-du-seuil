using System.Collections.Generic;
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

        /// <summary>Sous-lot V2.7 : seul un changement de palier s'annonce (les petits changements se lisent sur la carte d'aventurier).</summary>
        private void ReputationMessage(StoryEvent e)
        {
            if (e.Kind != StoryEventKind.StandingChanged || e.Step is null) return;
            Say(e.Value > 0 ? "text.ui.reputation.up" : "text.ui.reputation.down", Text(EdS.Data.Story.Reputations.FactionText(e.Id)),
                Text(EdS.Data.Story.Reputations.TierText(e.Step)));
        }
    }
}
