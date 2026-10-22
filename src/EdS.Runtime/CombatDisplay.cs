using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 10.3 : état à afficher calculé depuis un moteur (joueur, autres combattants), partagé par le pont temps réel (solo) et par
    /// le client réseau, qui affichent ainsi exactement la même chose. Lecture seule : rien n'est modifié dans le moteur.
    /// </summary>
    public static class CombatDisplay
    {
        /// <summary>État du joueur au début du prochain tick ; <paramref name="targetId"/> sert à l'indisponibilité « aucune cible ».</summary>
        public static PlayerState Player(CombatEngine engine, CombatRules rules, string playerId, string? targetId)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            CombatantView me = engine.View(playerId);
            long tick = engine.CurrentTick;
            double tickSeconds = engine.TickMilliseconds / 1000.0;
            CombatantRules effective = me.Rules!;
            var slots = new List<SkillSlot>();
            CharacterBuild build = me.Character!.Build;
            for (int slot = 0; slot <= RealtimeCombat.UltimateSlot; slot++)
            {
                if (slot < RealtimeCombat.UltimateSlot && slot >= build.ActiveSkills.Count) continue;
                SkillDefinition? skill = IntentionPlanner.SkillAt(me, slot);
                if (skill is null) continue;
                SkillCombatDefinition combat = skill.Combat!;
                long readyTick = Math.Max(me.CooldownReadyTick(skill.Id), me.GlobalCooldownReadyTick);
                double remaining = Math.Max(0, readyTick - tick) * tickSeconds;
                bool ultimate = slot == RealtimeCombat.UltimateSlot;
                // Le seul contrôle de disponibilité, partagé avec le serveur (revue Codex de la PR #19).
                string? unavailable = IntentionPlanner.Unavailable(me, skill, ultimate, tick, tickSeconds, rules, targetId != null);
                slots.Add(new SkillSlot(slot, skill.Id, skill.Name, ultimate, combat.EnduranceCost, effective.CooldownMs(skill, combat) / 1000.0,
                    remaining, unavailable));
            }
            return new PlayerState(me, rules.ResonanceMax, effective.RampartChargesMax, slots, targetId,
                Math.Max(0, me.ParryRecoveryUntilTick - tick) * tickSeconds);
        }

        /// <summary>
        /// Les autres combattants (ennemis et alliés), dans l'ordre du scénario. Le temps avant la prochaine attaque (télégraphie, jamais
        /// négatif) est diminué de <paramref name="telegraphLeadSeconds"/> (0 en solo ; en réseau, le temps aller-retour lissé : sous-lot
        /// 10.6, option B de la parade) et de <paramref name="elapsedSeconds"/>, le temps déjà écoulé dans le tick en cours (revue Codex de
        /// la PR #23, tour 3 : le compte à rebours et la barre s'écoulent en continu entre deux ticks au lieu d'avancer par sauts de 50 ms).
        /// Seul l'affichage change : le moteur n'est pas touché. Sous-lot 11.4d (revue Codex de la PR #32, 16e tour) : la liste comprend aussi
        /// les absents (attendus, refusés, partis), pour que l'affichage prépare leur corps avant une arrivée ; chaque affichage les saute
        /// (<c>View.IsPresent</c> faux) : corps cachés, ni barre ni fiche, jamais ciblés (<see cref="LivingEnemies"/>).
        /// </summary>
        public static IReadOnlyList<OtherState> Others(CombatEngine engine, string playerId, double telegraphLeadSeconds = 0, double elapsedSeconds = 0)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));
            if (double.IsNaN(telegraphLeadSeconds) || double.IsInfinity(telegraphLeadSeconds) || telegraphLeadSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(telegraphLeadSeconds), $"Avance de la télégraphie de {telegraphLeadSeconds} s : positive ou nulle et finie.");
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), $"Temps écoulé dans le tick de {elapsedSeconds} s : positif ou nul et fini.");
            double shift = telegraphLeadSeconds + elapsedSeconds;
            CombatantView me = engine.View(playerId);
            long tick = engine.CurrentTick;
            double tickSeconds = engine.TickMilliseconds / 1000.0;
            return engine.CombatantIds.Where(id => id != playerId).Select(id =>
            {
                CombatantView v = engine.View(id);
                double? next = v.NextAttackTick is long n ? Math.Max(0, Math.Max(0, n - tick) * tickSeconds - shift) : (double?)null;
                return new OtherState(v, v.Team != me.Team, next);
            }).ToList();
        }

        /// <summary>Ennemis vivants, dans l'ordre du scénario.</summary>
        public static IEnumerable<OtherState> LivingEnemies(CombatEngine engine, string playerId) =>
            Others(engine, playerId).Where(o => o.IsEnemy && !o.View.IsDead && o.View.IsPresent);   // sous-lot 11.4d : un absent ne s'affiche pas

        /// <summary>Personnages alliés vivants autres que le joueur, dans l'ordre du scénario (coopération, lot 10).</summary>
        public static IEnumerable<OtherState> LivingAllies(CombatEngine engine, string playerId) =>
            Others(engine, playerId).Where(o => !o.IsEnemy && !o.View.IsDead && o.View.IsPresent && o.View.Kind == CombatantKind.Character);
    }

    /// <summary>
    /// Sous-lot 10.3 (extrait du pont du 9.2) : retours à afficher (dégâts, blocage, parade…) déduits de la différence des compteurs du
    /// moteur d'un tick à l'autre. File bornée : au-delà de <see cref="MaxPending"/>, les plus anciens sont abandonnés.
    /// </summary>
    public sealed class FeedbackTracker
    {
        /// <summary>Retours conservés au plus.</summary>
        public const int MaxPending = 256;

        private readonly List<CombatFeedback> _pending = new List<CombatFeedback>();
        private readonly Dictionary<string, CombatantCounters> _counters = new Dictionary<string, CombatantCounters>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _dead = new Dictionary<string, bool>(StringComparer.Ordinal);

        public FeedbackTracker(CombatEngine engine) => Reset(engine);

        /// <summary>Repart de l'état actuel du moteur sans produire de retour (moteur reconstruit après une resynchronisation).</summary>
        public void Reset(CombatEngine engine)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));
            _counters.Clear();
            _dead.Clear();
            foreach (string id in engine.CombatantIds)
            {
                CombatantView view = engine.View(id);
                _counters[id] = view.Counters;
                _dead[id] = view.IsDead;
            }
        }

        public void Add(CombatFeedback feedback)
        {
            if (feedback is null) throw new ArgumentNullException(nameof(feedback));
            if (_pending.Count >= MaxPending) _pending.RemoveAt(0);
            _pending.Add(feedback);
        }

        /// <summary>Retours apparus depuis le dernier appel, dans l'ordre ; la file est vidée.</summary>
        public IReadOnlyList<CombatFeedback> Drain()
        {
            CombatFeedback[] drained = _pending.ToArray();
            _pending.Clear();
            return drained;
        }

        /// <summary>
        /// Retours du tick qui vient d'être simulé (à appeler après chaque <see cref="CombatEngine.Step"/>). Revue Codex de la PR #32 (29e
        /// tour) : un combattant entré par <see cref="CombatEngine.Join"/> depuis la création du suivi est découvert ici ; il part de
        /// compteurs nuls et vivant (son état à l'entrée), donc seuls ses vrais retours depuis son arrivée sont produits.
        /// </summary>
        public void Collect(CombatEngine engine)
        {
            if (engine is null) throw new ArgumentNullException(nameof(engine));
            foreach (string id in engine.CombatantIds)
            {
                CombatantView view = engine.View(id);
                if (!_counters.TryGetValue(id, out CombatantCounters? before))
                {
                    before = new CombatantCounters();
                    _dead[id] = false;
                }
                CombatantCounters after = view.Counters;
                void Count(long b, long a, FeedbackKind kind)
                {
                    if (a > b) Add(new CombatFeedback(kind, id, a - b, null));
                }
                Count(before.DamageTaken, after.DamageTaken, FeedbackKind.DamageTaken);
                Count(before.DamageAbsorbed, after.DamageAbsorbed, FeedbackKind.Absorbed);
                Count(before.Blocks, after.Blocks, FeedbackKind.Blocked);
                Count(before.PerfectParries, after.PerfectParries, FeedbackKind.PerfectParry);
                Count(before.FailedParries, after.FailedParries, FeedbackKind.FailedParry);
                Count(before.GuardBreaks, after.GuardBreaks, FeedbackKind.GuardBroken);
                Count(before.CriticalHits, after.CriticalHits, FeedbackKind.CriticalHit);
                Count(before.UltimatesCast, after.UltimatesCast, FeedbackKind.Ultimate);
                Count(before.CommandsRejected, after.CommandsRejected, FeedbackKind.Rejected);
                if (view.IsDead && !_dead[id]) Add(new CombatFeedback(FeedbackKind.Died, id, 0, null));
                _counters[id] = after;
                _dead[id] = view.IsDead;
            }
        }
    }
}
