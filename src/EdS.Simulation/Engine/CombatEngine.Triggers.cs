using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Stats;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Lot 7.3 : déclencheurs. Index des abonnés construit au chargement du combat, notification des effets observables
    /// (<see cref="NotifyItem"/>), évaluation pure des candidats dans un ordre total, recharge interne, suppressions (cycle,
    /// profondeur) et réactions (<see cref="ReactionItem"/>). Tout passe par la file, en largeur : aucune récursion.
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>
        /// Déclencheur actif en combat : (définition, indice, porteur, instance), son rang et sa recharge interne. Permanent (talent,
        /// passif, compétence équipée, assaillant ; instance 0) ou porté par une instance d'altération (instance = son numéro).
        /// </summary>
        private sealed class TriggerSlot
        {
            public Fighter Bearer = null!;
            public TriggerOrigin Origin;
            public string DefinitionId = null!;
            public int Index;
            public int Rank;
            public TriggerDefinition Definition = null!;

            /// <summary>0 pour un déclencheur permanent ; numéro de l'instance d'altération qui le porte sinon.</summary>
            public long Instance;

            /// <summary>Instance d'altération qui porte le déclencheur ; null pour un déclencheur permanent.</summary>
            public StatusInstance? Status;

            /// <summary>Identité journalisée (§2.3) : <c>définition/indice@porteur#instance</c>.</summary>
            public string Identity = null!;

            /// <summary>Recharge interne (§4.5) : une réaction n'est acceptée que si tick ≥ ReadyTick ; consommée à l'acceptation.</summary>
            public long ReadyTick;

            /// <summary>Une réaction qui frappe s'exécute en phase de résolution (§4.2, §7) ; sinon dans la phase de la notification.</summary>
            public bool Strikes;
        }

        /// <summary>Ordre total des déclencheurs d'un même porteur (§5.4, critères 3 à 6).</summary>
        private static int CompareSlots(TriggerSlot a, TriggerSlot b)
        {
            int c = a.Origin.CompareTo(b.Origin);
            if (c == 0) c = string.CompareOrdinal(a.DefinitionId, b.DefinitionId);
            if (c == 0) c = a.Index.CompareTo(b.Index);
            if (c == 0) c = a.Instance.CompareTo(b.Instance);
            return c;
        }

        private static TriggerSlot Slot(Fighter bearer, TriggerOrigin origin, string definitionId, int index, int rank, TriggerDefinition definition,
            StatusInstance? status = null) => new TriggerSlot
            {
                Bearer = bearer,
                Origin = origin,
                DefinitionId = definitionId,
                Index = index,
                Rank = rank,
                Definition = definition,
                Instance = status?.Id ?? 0,
                Status = status,
                Identity = $"{definitionId}/{index}@{bearer.Id}#{status?.Id ?? 0}",
                Strikes = definition.Effects.Any(e => e.Kind == TriggerEffectKind.Strike),
            };

        /// <summary>
        /// Instantané immuable d'un effet observable (§1.4), construit à l'émission. Les conditions des déclencheurs ne lisent que
        /// lui : un même effet donne la même décision, quel que soit le moment de l'examen. <see cref="Kind"/> et
        /// <see cref="Also"/> sont les types d'abonnement qu'il sert (un impact sert <c>onHitDealt</c> et <c>onHitTaken</c>).
        /// </summary>
        private sealed class ObservedEvent
        {
            public TriggerEvent Kind;
            public TriggerEvent? Also;
            public string? Source;
            public string? Target;
            public string? Skill;
            public string? State;
            public StatusCategory? Category;
            public long? Instance;
            public DamageType? DamageType;
            public HitDefense Defense;
            public bool Renewed;
            public TriggerResource? Resource;

            // Renseignés par Emit.
            public long Eid;
            public long Tick;
            public CombatPhase Phase;

            /// <summary>Provenance de l'effet tel qu'émis (cause = son parent).</summary>
            public Provenance Provenance = null!;

            /// <summary>Candidats relevés à l'émission (§5.4), dans l'ordre total.</summary>
            public List<TriggerSlot> Candidates = null!;
        }

        /// <summary>Notification d'un effet observable : ajoutée en fin de la phase courante s'il a au moins un abonné possible.</summary>
        private sealed class NotifyItem : Scheduled
        {
            public ObservedEvent Event = null!;
        }

        /// <summary>Réaction acceptée d'un déclencheur à un effet : exécutée en fin de sa phase (résolution si elle frappe).</summary>
        private sealed class ReactionItem : Scheduled
        {
            public TriggerSlot Slot = null!;
            public ObservedEvent Event = null!;

            /// <summary>Provenance de ses effets : racine de l'effet observé, profondeur + 1, chaîne étendue, cause = effet observé.</summary>
            public Provenance Origin = null!;
        }

        private enum TriggerDecision { Rejected, Cycle, Depth, Accepted }

        // Index (§5.4) : abonnement possible par type, déclencheurs permanents par combattant (triés), ordre des porteurs.
        private readonly bool[] _subscribed = new bool[Enum.GetValues(typeof(TriggerEvent)).Length];
        private List<Fighter> _fightersByInitiative = null!;

        /// <summary>Réservé aux tests : réactions acceptées (tick, identité, eid observé), dans l'ordre d'acceptation.</summary>
        internal List<(long Tick, string Trigger, long Cause)>? ReactionTrace { get; set; }

        /// <summary>
        /// Construit l'index au chargement du combat : déclencheurs permanents de chaque combattant (talents de rang ≥ 1 et
        /// compétences équipées d'un personnage, liste d'un assaillant), triés une fois pour toutes ; types d'abonnement possibles,
        /// altérations du catalogue comprises. Aucun parcours de dictionnaire ne fixe un ordre.
        /// </summary>
        private void BuildTriggerIndex()
        {
            _fightersByInitiative = _fighters.OrderByDescending(f => f.Setup.Initiative).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
            foreach (Fighter f in _fighters)
            {
                var slots = new List<TriggerSlot>();
                if (f.Character is { } character)
                    foreach (BoundTrigger bound in character.ActiveTriggers())
                        slots.Add(Slot(f, bound.Origin, bound.DefinitionId, bound.Index, bound.Rank, bound.Definition));
                if (f.Assailant is { } assailant)
                    for (int k = 0; k < assailant.Triggers.Count; k++)
                        slots.Add(Slot(f, TriggerOrigin.Assailant, assailant.TriggerDefinitionId, k, 1, assailant.Triggers[k]));
                slots.Sort(CompareSlots);
                f.Triggers = slots;
                foreach (TriggerSlot slot in slots) _subscribed[(int)slot.Definition.On] = true;
            }
            foreach (StatusDefinition definition in _statuses.Definitions)
                foreach (TriggerDefinition trigger in definition.Triggers) _subscribed[(int)trigger.On] = true;
        }

        /// <summary>Déclencheurs portés par une nouvelle instance d'altération (identité : instance), recharges internes à zéro.</summary>
        private static TriggerSlot[]? StatusSlots(Fighter owner, StatusInstance status)
        {
            IReadOnlyList<TriggerDefinition> triggers = status.Definition.Triggers;
            if (triggers.Count == 0) return null;
            var slots = new TriggerSlot[triggers.Count];
            for (int k = 0; k < triggers.Count; k++) slots[k] = Slot(owner, TriggerOrigin.Status, status.State, k, 1, triggers[k], status);
            return slots;
        }

        private bool Subscribed(ObservedEvent e) => _subscribed[(int)e.Kind] || (e.Also is { } also && _subscribed[(int)also]);

        /// <summary>Coût d'une ligne de jeu observable : la ligne, et la mise en file de sa notification si le type a un abonné possible.</summary>
        private Cost Observable(TriggerEvent kind, TriggerEvent? also = null) =>
            Cost.Line + Cost.Enqueue.If(_subscribed[(int)kind] || (also is { } a && _subscribed[(int)a]));

        /// <summary>
        /// Candidats d'un effet, relevés à son émission (§5.4) dans l'ordre total : initiative du porteur décroissante, identifiant
        /// du porteur, puis origine, définition, indice et instance. Seuls les porteurs vivants qui tiennent le rôle demandé sont
        /// retenus. Une altération posée après l'effet ne l'observe donc pas ; une altération observe sa propre application. Les
        /// instances d'altération sont lues dans un dictionnaire, puis triées : l'ordre ne dépend jamais de son parcours.
        /// </summary>
        private List<TriggerSlot> Candidates(ObservedEvent e)
        {
            var candidates = new List<TriggerSlot>();
            foreach (Fighter f in _fightersByInitiative)
            {
                if (f.IsDead) continue;
                int start = candidates.Count;
                foreach (TriggerSlot slot in f.Triggers)
                    if (Matches(slot, e)) candidates.Add(slot);
                bool statusSlots = false;
                foreach (StatusInstance status in f.Statuses.Values)
                {
                    if (status.Slots is null) continue;
                    foreach (TriggerSlot slot in status.Slots)
                        if (Matches(slot, e))
                        {
                            candidates.Add(slot);
                            statusSlots = true;
                        }
                }
                if (statusSlots) candidates.Sort(start, candidates.Count - start, Comparer<TriggerSlot>.Create(CompareSlots));
            }
            return candidates;
        }

        /// <summary>
        /// Un candidat relevé est-il encore évaluable au traitement de la notification ? Un porteur mort, ou une instance d'altération
        /// porteuse retirée entre-temps, est ignoré sans coût (§4.6).
        /// </summary>
        private static bool Live(TriggerSlot slot) =>
            !slot.Bearer.IsDead && (slot.Status is not { } status || (status.Owner.Statuses.TryGetValue(status.Key, out StatusInstance? current) && current == status));

        /// <summary>Le déclencheur est-il abonné à ce type d'effet, avec un porteur qui tient le rôle demandé ?</summary>
        private static bool Matches(TriggerSlot slot, ObservedEvent e)
        {
            TriggerDefinition d = slot.Definition;
            if (d.On != e.Kind && d.On != e.Also) return false;
            string? holder = d.EffectiveRole == TriggerRole.Target ? e.Target : e.Source;   // self et source : l'auteur
            return string.Equals(holder, slot.Bearer.Id, StringComparison.Ordinal);
        }

        /// <summary>Condition pure (§1.4) : le filtre ne lit que l'instantané de l'effet.</summary>
        private static bool FilterAccepts(TriggerFilter f, ObservedEvent e)
        {
            if (f.Skills.Count > 0 && (e.Skill is null || !f.Skills.Contains(e.Skill, StringComparer.Ordinal))) return false;
            if (f.DamageType is { } damageType && e.DamageType != damageType) return false;
            if (f.Defenses.Count > 0 && !f.Defenses.Contains(e.Defense)) return false;
            if (f.Status != null && !string.Equals(f.Status, e.State, StringComparison.Ordinal)) return false;
            if (f.Category is { } category && e.Category != category) return false;
            if (f.Resource is { } resource && e.Resource != resource) return false;
            if (f.Renewed is { } renewed && e.Renewed != renewed) return false;
            return true;
        }

        /// <summary>
        /// Décision pour un candidat vivant, sans rien modifier, dans l'ordre du plan (§5.2) : condition, recharge interne, cycle,
        /// profondeur. Un rejet (condition fausse ou recharge) est silencieux ; un cycle ou une profondeur excessive est une
        /// suppression journalisée.
        /// </summary>
        private TriggerDecision Decide(long tick, TriggerSlot slot, ObservedEvent e)
        {
            if (!FilterAccepts(slot.Definition.Filter, e)) return TriggerDecision.Rejected;
            if (tick < slot.ReadyTick) return TriggerDecision.Rejected;
            if (TriggerChain.Contains(e.Provenance.Chain, slot.Identity)) return TriggerDecision.Cycle;
            if (e.Provenance.Depth + 1 > _rules.MaxTriggerDepth) return TriggerDecision.Depth;
            return TriggerDecision.Accepted;
        }

        /// <summary>
        /// Coût exact du traitement d'une notification : retrait, une évaluation par candidat vivant, une ligne par suppression, une
        /// mise en file par réaction acceptée (§6.1).
        /// </summary>
        private Cost NotifyCost(long tick, ObservedEvent e)
        {
            Cost cost = Cost.Op;
            foreach (TriggerSlot slot in e.Candidates)
            {
                if (!Live(slot)) continue;
                cost += Cost.Op;
                switch (Decide(tick, slot, e))
                {
                    case TriggerDecision.Cycle:
                    case TriggerDecision.Depth:
                        cost += Cost.Line;
                        break;
                    case TriggerDecision.Accepted:
                        cost += Cost.Enqueue;
                        break;
                }
            }
            return cost;
        }

        /// <summary>
        /// Traite une notification en tête de la file : réserve son coût exact, puis évalue chaque candidat dans l'ordre. Une réaction
        /// acceptée consomme la recharge interne et part en file : en fin de la phase courante, ou de la phase de résolution si elle
        /// frappe ; si cette phase est déjà passée, au tick suivant (report, §7). Les branches sœurs ne partagent que les budgets.
        /// </summary>
        private void ProcessNotify(long tick, CombatPhase phase, NotifyItem item)
        {
            ObservedEvent e = item.Event;
            Reserve(NotifyCost(tick, e));
            _queue.Dequeue();
            Charge(1);
            foreach (TriggerSlot slot in e.Candidates)
            {
                if (!Live(slot)) continue;
                Charge(1);
                TriggerDecision decision = Decide(tick, slot, e);
                switch (decision)
                {
                    case TriggerDecision.Cycle:
                    case TriggerDecision.Depth:
                        EmitDiagnostic(tick, phase, "triggerSuppressed", w => w
                            .Field("trigger", slot.Identity).Field("root", e.Provenance.Root).Field("cause", e.Eid)
                            .Field("depth", e.Provenance.Depth + 1).Field("reason", decision == TriggerDecision.Cycle ? "cycle" : "depth"));
                        break;
                    case TriggerDecision.Accepted:
                        slot.ReadyTick = checked(tick + slot.Definition.CooldownMs / _tickMs);
                        CombatPhase target = slot.Strikes ? CombatPhase.Resolution : phase;
                        long when = target < phase ? checked(tick + 1) : tick;
                        Provenance origin = slot.Status is { } status
                            ? e.Provenance.ReactionTo(e.Eid, slot.Identity, slot.Bearer.Id, status.Id, status.Generation)
                            : e.Provenance.ReactionTo(e.Eid, slot.Identity, slot.Bearer.Id);
                        Schedule(when, target, new ReactionItem { Slot = slot, Event = e, Origin = origin });
                        ReactionTrace?.Add((tick, slot.Identity, e.Eid));
                        break;
                }
            }
        }

        /// <summary>Étape 7.3.3 : une réaction acceptée est retirée de la file (une unité) ; ses effets arrivent à l'étape 7.3.4.</summary>
        private void ProcessReaction(long tick, CombatPhase phase, ReactionItem item)
        {
            Reserve(Cost.Op);
            _queue.Dequeue();
            Charge(1);
        }

        /// <summary>Traite un élément de déclencheur en tête de la file ; faux si l'élément est d'une autre nature.</summary>
        private bool ProcessTriggerItem(long tick, CombatPhase phase, Scheduled item)
        {
            switch (item)
            {
                case NotifyItem notify:
                    ProcessNotify(tick, phase, notify);
                    return true;
                case ReactionItem reaction:
                    ProcessReaction(tick, phase, reaction);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Vide les notifications et réactions de la phase en cours (phases qui ne sont pas pilotées par la file : commandes après le
        /// lot de commandes, résolution après les actions, effets périodiques après l'entretien, régénération, fin de tick). Renvoie
        /// le nombre d'éléments traités. Sans déclencheur, la file n'en contient jamais : rien ne change.
        /// </summary>
        private int DrainTriggers(long tick, CombatPhase phase)
        {
            int processed = 0;
            while (_queue.TryPeek(tick, phase, out Scheduled item))
            {
                if (!ProcessTriggerItem(tick, phase, item))
                    throw new InvalidOperationException($"Élément inattendu en file ({item.GetType().Name}) en phase {phase}.");
                processed++;
            }
            return processed;
        }
    }
}
