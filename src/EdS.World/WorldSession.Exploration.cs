using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Saves;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.World
{
    /// <summary>
    /// Sous-lot V2.29 (2) : brouillard de carte à dissiper (sujet 30) et points de vue (décision de Damien du 15 novembre 2026 : au sommet,
    /// à la première visite, 200 XP, une entrée au carnet et le brouillard dissipé autour ; le 16 novembre 2026 : sur toute la région,
    /// sous-lot V2.29.1). Seulement dans une zone à région, hors des
    /// intérieurs ; les cases vues sont gardées dans la sauvegarde (version 23, <see cref="SavedCharacter.Explored"/>).
    /// </summary>
    public sealed partial class WorldSession
    {
        private ExploredCells? _explored;

        /// <summary>Cases vues de la région de la zone (null : zone sans région, ou intérieur).</summary>
        public ExploredCells? Explored => _explored;

        private void StartExploration()
        {
            if (Definition?.Region is null || Definition.Indoor) return;
            _explored = new ExploredCells(Definition.Region, _character.Explored.TryGetValue(Definition.Id, out string hex) ? hex : null);
        }

        /// <summary>Cases vues autour du personnage présent et vivant, puis les points de vue atteints (une fois chacun, par la découverte).</summary>
        private void Explore(List<StoryEvent> story)
        {
            if (_explored is null) return;
            CombatantView me = Zone.Engine.View(CombatantId);
            if (!me.IsPresent || me.IsDead || !(me.Position is GroundVector p)) return;
            _explored.Reveal(p, ExploredCells.SightMeters);
            if (Narrative is null) return;
            for (int i = 0; i < Viewpoint.All.Count; i++)   // relecture de la PR #185 : sans allocation à chaque tick
            {
                Viewpoint v = Viewpoint.All[i];
                if (v.Zone != Definition!.Id || Narrative.HasDiscovered(v.Discovery)) continue;
                if (ZonePopulations.Beyond(p, Domain.Numerics.Fixed.Zero, v.Position, Domain.Numerics.Fixed.Zero, Domain.Numerics.Fixed.FromInt(Viewpoint.Range))) continue;
                if (!Narrative.Discover(v.Discovery, story)) continue;
                // Relecture de la PR #185 : l'expérience réellement ajoutée (au plafond, moins que les 200 promis), annoncée telle quelle.
                long before = _character.Xp;
                _character = _character.WithXp(Math.Min(SaveValidator.MaxXp, checked(before + v.Xp)));
                long added = _character.Xp - before;
                if (added > 0) story.Add(new StoryEvent(StoryEventKind.XpGiven, "xp", null, added));
                if (v.WholeRegion) _explored.RevealAll();   // sous-lot V2.29.1 : le sommet lève le brouillard sur toute la région
                else _explored.Reveal(v.Position, v.RevealMeters);
            }
        }

        /// <summary>Le personnage avec les cases vues de cette zone (les autres zones gardées telles quelles).</summary>
        private SavedCharacter WithExploration(SavedCharacter character)
        {
            if (_explored is null || Definition is null) return character;
            string? hex = _explored.ToHex();
            if (hex is null) return character;
            var map = character.Explored.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
            map[Definition.Id] = hex;
            return character.WithExplored(map);
        }
    }
}
