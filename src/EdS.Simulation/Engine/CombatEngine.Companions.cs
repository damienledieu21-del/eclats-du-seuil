using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Compagnons (sujet 11 de la feuille de route, décisions de Damien du 14 novembre 2026) : un assaillant allié guidé par l'ordinateur,
    /// attaché à un personnage, son Éveillé (<see cref="CompanionSetup"/>). Combat positionné seulement.
    /// <list type="bullet">
    /// <item><b>Ordres</b> (commande <c>order</c>, ligne <c>orderGiven</c>) : suivre (par défaut), attendre, cibler, protéger, revenir. Un ordre
    /// accepté remplace le précédent ; « cibler » revient à « suivre » quand la cible tombe ou part, « revenir » quand il a rejoint l'Éveillé
    /// (ligne <c>orderEnded</c>, au début de la phase des commandes). Les traits d'un mercenaire ne changent jamais l'obéissance.</item>
    /// <item><b>Cible</b> : « cibler » vise la cible désignée ; « suivre » l'ennemi le plus proche de lui parmi ceux à la laisse de l'Éveillé ;
    /// « protéger » l'ennemi le plus proche de l'Éveillé, à sa laisse ; « attendre » l'ennemi le plus proche de lui, à sa laisse ;
    /// « revenir » aucune. Hors « cibler », il ne provoque ni une bête qui n'est pas engagée (paisible, endormie, qui erre) ni une bête qui
    /// fuit. Son Éveillé hors du combat, il garde sa place et ne vise que ce qui est à sa laisse.</item>
    /// <item><b>Placement</b> : sans cible (ou en revenant), il rejoint l'Éveillé jusqu'à la distance de suite ; avec une cible, il l'approche
    /// comme un ennemi à comportement ; « attendre » ne bouge pas ; pendant une attaque à deux, il s'arrête tourné vers la cible.</item>
    /// <item><b>Attaques</b> : ses tours suivent sa liste d'attaques ; il saute son attaque en revenant (<c>recalled</c>) et pendant une
    /// attaque à deux (<c>duo</c>). « Revenir » annule son attaque annoncée qui n'a pas encore frappé (<c>attackCancelled</c>, <c>recalled</c>).
    /// Ses coups sur une créature ne sont ni réduits ni plafonnés comme entre bêtes, et comptent pour le monde comme ceux d'un personnage.</item>
    /// <item><b>Lien</b> : en fin de tick, un tick où un coup a porté sur une cible que l'autre partenaire avait frappée dans la fenêtre de
    /// coordination donne le gain coordonné ; sous « protéger », un coup sur un ennemi qui visait l'Éveillé donne le gain de protection ; une
    /// seule ligne <c>bondGained</c> par compagnon et par tick, jauge bornée à 100.</item>
    /// <item><b>Attaque à deux</b> (commande <c>duo</c>, ligne <c>duoStarted</c>) : jauge pleine, deux partenaires présents et disponibles,
    /// cible commune ennemie à portée de chacun ; la jauge est dépensée au départ ; le coup part du compagnon à la fin de l'annonce
    /// (<c>duoLanded</c> puis l'impact), ou s'annule si un partenaire ou la cible n'est plus là (<c>duoCancelled</c>, sans rendre la jauge).
    /// L'Éveillé ne lance aucune compétence jusqu'à la fin de la récupération (temps global de recharge).</item>
    /// <item><b>Hors combat</b> : à 0 PV, un compagnon tombe (<c>died</c>) ; la progression le traite comme hors combat, jamais mort. Un
    /// compagnon ne garde pas seul son équipe en vie : une équipe dont il ne reste que des compagnons est vaincue.</item>
    /// </list>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>État d'un compagnon dans le combat (mutable, dans l'instantané).</summary>
        private sealed class CompanionState
        {
            public CompanionSetup Setup = null!;
            public Fighter Owner = null!;

            public CompanionOrder Order = CompanionOrder.Follow;

            /// <summary>« Cibler » : la cible désignée ; null sinon.</summary>
            public Fighter? OrderTarget;

            public int Bond;

            /// <summary>Tick du dernier ordre « revenir » accepté (annule les annonces antérieures) ; null s'il n'y en a pas eu.</summary>
            public long? RecalledTick;

            /// <summary>Dernier coup de l'Éveillé et dernier coup du compagnon : cible et tick (coordination).</summary>
            public string? OwnerHitTarget;
            public long OwnerHitTick;
            public string? SelfHitTarget;
            public long SelfHitTick;

            /// <summary>Gains notés pendant le tick, versés en fin de tick (toujours faux entre deux ticks).</summary>
            public bool CoordinatedThisTick, ProtectedThisTick;

            /// <summary>Attaque à deux en cours : cible, tick de l'impact (null après l'impact), fin de la récupération, provenance.</summary>
            public Fighter? DuoTarget;
            public long? DuoImpactTick;
            public long DuoReadyTick;
            public Provenance? DuoOrigin;

            /// <summary>Nombre d'attaques à deux lancées (identité des impacts).</summary>
            public long DuosStarted;
        }

        /// <summary>Le combat a-t-il au moins un compagnon ?</summary>
        private bool _hasCompanions;

        /// <summary>Compagnons : lie chaque compagnon à son Éveillé (combat validé), après la création de tous les combattants.</summary>
        private void LinkCompanions()
        {
            foreach (Fighter f in _fighters) LinkCompanion(f);
        }

        /// <summary>Compagnons : lie un compagnon à son Éveillé (déjà connu : validation du combat, ou du combat complété d'une arrivée).</summary>
        private void LinkCompanion(Fighter f)
        {
            if (f.Assailant?.Companion is not { } setup) return;
            f.Companion = new CompanionState { Setup = setup, Owner = _byId[setup.Owner], Bond = setup.Bond };
            if (setup.Duo is { } duo) f.AttackStats[duo.Id] = CombatantStats.ForAssailantAttack(_setup.Stats, duo.RawDamage);
            _hasCompanions = true;
        }

        /// <summary>Compagnons de <paramref name="owner"/>, dans l'ordre des identifiants.</summary>
        private IEnumerable<Fighter> CompanionsOf(Fighter owner)
        {
            if (!_hasCompanions) yield break;
            foreach (Fighter f in _fightersById)
                if (f.Companion is { } c && c.Owner == owner) yield return f;
        }

        // --- Commandes -----------------------------------------------------------------------------------------------------------

        /// <summary>Contrôles d'un ordre, sans rien modifier : Éveillé présent et vivant, compagnon présent et vivant, cible de « cibler » ennemie présente.</summary>
        private string? CheckOrder(Fighter actor, CombatCommand command)
        {
            if (actor.Out) return OutReason(actor, "actor");
            Fighter companion = _byId[command.Companion!];
            if (companion.Out) return OutReason(companion, "companion");
            if (command.Order == CompanionOrder.Target)
            {
                Fighter target = _byId[command.Target!];
                if (target.Out) return OutReason(target, "target");
                if (!Enemies(companion, target)) return "targetNotEnemy";
            }
            return null;
        }

        private void GiveOrder(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            CombatCommand command = item.Command;
            Fighter companion = _byId[command.Companion!];
            CompanionState state = companion.Companion!;
            state.Order = command.Order!.Value;
            state.OrderTarget = state.Order == CompanionOrder.Target ? _byId[command.Target!] : null;
            if (state.Order == CompanionOrder.Return) state.RecalledTick = tick;
            Emit(tick, CombatPhase.Commands, "orderGiven", root, w => w.Field("actor", actor.Id).Field("command", item.Index)
                .Field("companion", companion.Id).Field("order", CompanionOrderIds.Of(state.Order)).Field("target", state.OrderTarget?.Id));
        }

        /// <summary>
        /// Contrôles d'une attaque à deux, sans rien modifier : Éveillé présent, vivant, libre de lancer une compétence (contrôle, temps global
        /// de recharge) ; compagnon présent, vivant, libre d'attaquer, sans attaque à deux en cours, jauge pleine ; cible ennemie présente, à
        /// portée (bords) de chacun des deux.
        /// </summary>
        private string? CheckDuo(long tick, Fighter actor, CombatCommand command)
        {
            if (actor.Out) return OutReason(actor, "actor");
            if (BlockReason(actor, StatusBlocks.Skills) is { } blocked) return blocked;
            if (tick < actor.GlobalCooldownReadyTick) return "globalCooldown";
            Fighter companion = _byId[command.Companion!];
            CompanionState state = companion.Companion!;
            if (companion.Out) return OutReason(companion, "companion");
            if (BlockReason(companion, StatusBlocks.AssailantAttacks) is { } companionBlocked) return "companion" + char.ToUpperInvariant(companionBlocked[0]) + companionBlocked.Substring(1);
            if (tick < state.DuoReadyTick) return "duoInProgress";
            if (state.Bond < CompanionSetup.DuoCost) return "bondNotFull";
            Fighter target = _byId[command.Target!];
            if (target.Out) return OutReason(target, "target");
            if (!Enemies(companion, target)) return "targetNotEnemy";
            long range = state.Setup.Duo!.Range.Raw;
            if (EdgeDistance(actor.Body!, target.Body!) > range || EdgeDistance(companion.Body!, target.Body!) > range) return "targetOutOfRange";
            return null;
        }

        private void StartDuo(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            CombatCommand command = item.Command;
            Fighter companion = _byId[command.Companion!];
            CompanionState state = companion.Companion!;
            CompanionDuo duo = state.Setup.Duo!;
            long impactTick = checked(tick + duo.TelegraphMs / _tickMs);
            long readyTick = checked(tick + duo.RecoveryMs / _tickMs);
            state.Bond -= CompanionSetup.DuoCost;
            state.DuoTarget = _byId[command.Target!];
            state.DuoImpactTick = impactTick < _durationTicks ? impactTick : (long?)null;
            state.DuoReadyTick = readyTick;
            state.DuosStarted = checked(state.DuosStarted + 1);
            if (readyTick > actor.GlobalCooldownReadyTick) actor.GlobalCooldownReadyTick = readyTick;
            long started = Emit(tick, CombatPhase.Commands, "duoStarted", root, w => w.Field("actor", actor.Id).Field("command", item.Index)
                .Field("companion", companion.Id).Field("duo", duo.Id).Field("target", state.DuoTarget.Id).Field("bond", (long)state.Bond)
                .Field("impactTick", impactTick).Field("readyTick", readyTick));
            state.DuoOrigin = root.CausedBy(started);
        }

        // --- Ordres qui prennent fin -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Début de la phase des commandes : « cibler » dont la cible n'est plus là et « revenir » arrivé près de l'Éveillé reviennent à
        /// « suivre » (ligne <c>orderEnded</c>, réservée avant le changement).
        /// </summary>
        private void RunCompanionOrders(long tick)
        {
            if (!_hasCompanions) return;
            foreach (Fighter f in _fightersById)
            {
                if (f.Companion is not { } state || f.Out) continue;
                string? reason = state.Order switch
                {
                    CompanionOrder.Target when state.OrderTarget!.Out => OutReason(state.OrderTarget, "target"),
                    CompanionOrder.Return when !state.Owner.Out && EdgeDistance(f.Body!, state.Owner.Body!) <= state.Setup.FollowDistance.Raw => "arrived",
                    _ => null,
                };
                Reserve(Cost.Op + Cost.Line.If(reason != null));
                Charge(1);
                if (reason is null) continue;
                CompanionOrder ended = state.Order;
                state.Order = CompanionOrder.Follow;
                state.OrderTarget = null;
                Emit(tick, CombatPhase.Commands, "orderEnded", Provenance.NewRoot($"companion:{f.Id}:{tick}", f.Id), w => w
                    .Field("companion", f.Id).Field("order", CompanionOrderIds.Of(ended)).Field("reason", reason));
            }
        }

        // --- Cible et placement ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Ennemi qu'un compagnon peut prendre de lui-même : présent, vivant, positionné ; hors une bête paisible (non engagée) ou qui fuit.
        /// </summary>
        private bool CompanionPrey(Fighter companion, Fighter other) =>
            !other.Out && other.Body != null && Enemies(companion, other)
            && !(other.Behavior is { } behavior && (!other.Engaged || behavior.Flee));

        /// <summary>Cible d'un compagnon selon son ordre (voir l'en-tête du fichier) ; null : aucune.</summary>
        private Fighter? CompanionTarget(Fighter f)
        {
            CompanionState state = f.Companion!;
            switch (state.Order)
            {
                case CompanionOrder.Return:
                    return null;
                case CompanionOrder.Target:
                    return state.OrderTarget!.Out ? null : state.OrderTarget;
            }
            bool ownerHere = !state.Owner.Out && state.Owner.Body != null;
            // « protéger » : le plus proche de l'Éveillé ; « suivre » : le plus proche de lui, parmi ceux à la laisse de l'Éveillé ;
            // « attendre » (ou Éveillé hors du combat) : le plus proche de lui, à sa propre laisse.
            Body anchor = ownerHere && state.Order != CompanionOrder.Wait ? state.Owner.Body! : f.Body!;
            Body from = state.Order == CompanionOrder.Protect && ownerHere ? state.Owner.Body! : f.Body!;
            long leash = state.Setup.Leash.Raw;
            Fighter? best = null;
            long bestDistance = 0;
            foreach (Fighter other in _fightersById)
            {
                if (!CompanionPrey(f, other) || EdgeDistance(anchor, other.Body!) > leash) continue;
                long d = EdgeDistance(from, other.Body!);
                if (best is null || d < bestDistance)
                {
                    best = other;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Motif de saut de l'attaque d'un compagnon avant les motifs ordinaires : en revenant (<c>recalled</c>), pendant une attaque à deux
        /// (<c>duo</c>) ; null sinon (et pour tout autre combattant).
        /// </summary>
        private static string? CompanionIdle(long tick, Fighter f)
        {
            if (f.Companion is not { } state) return null;
            if (state.Order == CompanionOrder.Return) return "recalled";
            if (tick < state.DuoReadyTick) return "duo";
            return null;
        }

        /// <summary>
        /// Intention d'un compagnon dans la phase de déplacement, ou null pour l'approche ordinaire de sa cible <paramref name="j"/> (indice
        /// dans les combattants par identifiant, −1 sans cible).
        /// </summary>
        private Intention? CompanionIntention(int i, long tick, long[] x, long[] z, int j)
        {
            Fighter f = _fightersById[i];
            CompanionState state = f.Companion!;
            if (tick < state.DuoReadyTick)
            {
                int face = state.DuoTarget is { Out: false } t ? _fightersById.IndexOf(t) : -1;
                return new Intention(false, 0, 0, face);
            }
            if (state.Order == CompanionOrder.Wait) return new Intention(false, 0, 0, j);
            if (j >= 0 && state.Order != CompanionOrder.Return) return null;
            // Rejoindre l'Éveillé jusqu'à la distance de suite (bords) ; Éveillé hors du combat : garder sa place.
            Fighter owner = state.Owner;
            if (owner.Out || owner.Body is null) return Intention.Hold;
            int o = _fightersById.IndexOf(owner);
            long vx = checked(x[o] - x[i]), vz = checked(z[o] - z[i]);
            long d = IntMath.Sqrt(Planar.LengthSquared(vx, vz));
            long far = checked(f.Body!.Radius + owner.Body.Radius + state.Setup.FollowDistance.Raw);
            if (d <= checked(far + BandTolerance)) return new Intention(false, 0, 0, j);
            long maxStep = checked(d - far);
            long stopX = checked(x[i] + IntMath.MulDiv(vx, maxStep, d, RoundingMode.Floor));
            long stopZ = checked(z[i] + IntMath.MulDiv(vz, maxStep, d, RoundingMode.Floor));
            if (!SteerToward(i, x, z, x[o], z[o], stopX, stopZ, out long sx, out long sz)) return new Intention(false, 0, 0, j);
            return Planar.TryNormalize(checked(sx - x[i]), checked(sz - z[i]), out long ux, out long uz)
                ? new Intention(true, ux, uz, j, maxStep)
                : new Intention(false, 0, 0, j);
        }

        /// <summary>« Revenir » annule l'attaque annoncée d'un compagnon qui n'a pas encore frappé (annoncée au tick du rappel ou avant).</summary>
        private bool RecalledBefore(AttackImpact impact)
        {
            if (impact.Pulse != 0 || impact.Assailant.Companion?.RecalledTick is not long recalled) return false;
            long announced = checked(impact.ImpactTick - impact.Attack.TelegraphMs / _tickMs);
            return recalled >= announced;
        }

        // --- Lien ----------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Un impact de <paramref name="actor"/> sur <paramref name="target"/> a porté (même gardé, paré ou absorbé) : traces de coordination
        /// et gains du tick (versés en fin de tick).
        /// </summary>
        private void NoteCompanionHit(long tick, Fighter actor, Fighter target)
        {
            if (!_hasCompanions) return;
            if (actor.Companion is { } state)
            {
                long window = state.Setup.BondWindowMs / _tickMs;
                if (state.OwnerHitTarget == target.Id && checked(tick - state.OwnerHitTick) <= window) state.CoordinatedThisTick = true;
                if (state.Order == CompanionOrder.Protect && !state.Owner.Out && target.Assailant != null && SingleTarget(target) == state.Owner)
                    state.ProtectedThisTick = true;
                state.SelfHitTarget = target.Id;
                state.SelfHitTick = tick;
                return;
            }
            if (actor.Character is null) return;
            foreach (Fighter c in CompanionsOf(actor))
            {
                CompanionState s = c.Companion!;
                long window = s.Setup.BondWindowMs / _tickMs;
                if (s.SelfHitTarget == target.Id && checked(tick - s.SelfHitTick) <= window) s.CoordinatedThisTick = true;
                s.OwnerHitTarget = target.Id;
                s.OwnerHitTick = tick;
            }
        }

        /// <summary>Fin de tick : gains de lien du tick, une ligne <c>bondGained</c> par compagnon qui gagne réellement, jauge bornée.</summary>
        private void RunBond(long tick)
        {
            if (!_hasCompanions) return;
            foreach (Fighter f in _fightersById)
            {
                if (f.Companion is not { } state) continue;
                int gain = (state.CoordinatedThisTick ? state.Setup.CoordinatedGain : 0) + (state.ProtectedThisTick ? state.Setup.ProtectGain : 0);
                string reason = state.CoordinatedThisTick && state.ProtectedThisTick ? "both" : state.CoordinatedThisTick ? "coordinated" : "protected";
                int granted = f.Out ? 0 : Math.Min(gain, CompanionSetup.BondMax - state.Bond);
                Reserve(Cost.Op + Cost.Line.If(granted > 0));
                Charge(1);
                state.CoordinatedThisTick = state.ProtectedThisTick = false;
                if (granted <= 0) continue;
                state.Bond += granted;
                Emit(tick, CombatPhase.EndOfTick, "bondGained", Provenance.NewRoot($"bond:{f.Id}:{tick}", f.Id), w => w
                    .Field("companion", f.Id).Field("owner", state.Owner.Id).Field("amount", (long)granted).Field("bond", (long)state.Bond).Field("reason", reason));
            }
        }

        // --- Attaque à deux : impact --------------------------------------------------------------------------------------------

        /// <summary>
        /// Phase de résolution, après les actions des commandes : impact des attaques à deux de ce tick, compagnon par identifiant. Annulée
        /// (<c>duoCancelled</c>) si l'Éveillé, le compagnon ou la cible n'est plus là ; sinon <c>duoLanded</c> puis l'impact, réservés ensemble.
        /// </summary>
        private void ResolveDuos(long tick)
        {
            if (!_hasCompanions) return;
            foreach (Fighter f in _fightersById)
            {
                if (f.Companion is not { DuoImpactTick: long impactTick } state || impactTick != tick) continue;
                Fighter target = state.DuoTarget!;
                string? reason = f.Out ? OutReason(f, "companion") : state.Owner.Out ? OutReason(state.Owner, "actor") : target.Out ? OutReason(target, "target") : null;
                CompanionDuo duo = state.Setup.Duo!;
                Provenance origin = state.DuoOrigin!;
                if (reason != null)
                {
                    Reserve(Cost.Op + Cost.Line);
                    Charge(1);
                    state.DuoImpactTick = null;
                    Emit(tick, CombatPhase.Resolution, "duoCancelled", origin, w => w.Field("actor", state.Owner.Id).Field("companion", f.Id)
                        .Field("duo", duo.Id).Field("target", target.Id).Field("reason", reason));
                    continue;
                }
                var attack = new AssailantAttack(duo.Id, duo.RawDamage, duo.DamageType);
                var action = new PendingAction { Actor = f, Attack = attack, Target = target, GeneratesResonance = false, Origin = origin };
                ResolveAction(tick, action, new[] { target }, Cost.Op + Cost.Line, dequeue: false, () =>
                {
                    Charge(1);
                    state.DuoImpactTick = null;
                    long landed = Emit(tick, CombatPhase.Resolution, "duoLanded", origin, w => w.Field("actor", state.Owner.Id).Field("companion", f.Id)
                        .Field("duo", duo.Id).Field("target", target.Id));
                    action.Origin = origin.CausedBy(landed);
                });
            }
        }

        // --- Instantané (format 8) ---------------------------------------------------------------------------------------------

        private static void WriteCompanion(JsonLineWriter w, CompanionState c)
        {
            w.Name("companion").BeginObject().Field("order", CompanionOrderIds.Of(c.Order)).Field("orderTarget", c.OrderTarget?.Id)
                .Field("bond", (long)c.Bond).Field("recalledTick", c.RecalledTick)
                .Field("ownerHitTarget", c.OwnerHitTarget).Field("ownerHitTick", c.OwnerHitTick)
                .Field("selfHitTarget", c.SelfHitTarget).Field("selfHitTick", c.SelfHitTick)
                .Field("duoTarget", c.DuoTarget?.Id).Field("duoImpactTick", c.DuoImpactTick).Field("duoReadyTick", c.DuoReadyTick)
                .Field("duosStarted", c.DuosStarted);
            WriteProvenance(w, "duoOrigin", c.DuoOrigin);
            w.EndObject();
        }

        private Fighter? KnownFighter(SnapshotNode id, string what)
        {
            if (id.IsNull) return null;
            return _byId.TryGetValue(id.String, out Fighter? f) ? f : throw new FormatException($"{what} : combattant inconnu « {id.String} ».");
        }

        private void LoadCompanion(Fighter f, SnapshotNode n)
        {
            CompanionState c = f.Companion!;
            if (!CompanionOrderIds.TryParse(n["order"].String, out CompanionOrder order)) throw new FormatException($"Ordre inconnu pour « {f.Id} ».");
            c.Order = order;
            c.OrderTarget = KnownFighter(n["orderTarget"], "cible de l'ordre");
            c.Bond = n["bond"].Int;
            c.RecalledTick = n["recalledTick"].NullableLong;
            c.OwnerHitTarget = n["ownerHitTarget"].NullableString;
            c.OwnerHitTick = n["ownerHitTick"].Long;
            c.SelfHitTarget = n["selfHitTarget"].NullableString;
            c.SelfHitTick = n["selfHitTick"].Long;
            c.DuoTarget = KnownFighter(n["duoTarget"], "cible de l'attaque à deux");
            c.DuoImpactTick = n["duoImpactTick"].NullableLong;
            c.DuoReadyTick = n["duoReadyTick"].Long;
            c.DuosStarted = n["duosStarted"].Long;
            c.DuoOrigin = ReadProvenance(n["duoOrigin"]);
            c.CoordinatedThisTick = c.ProtectedThisTick = false;   // jamais vrais entre deux ticks
        }

        /// <summary>Récupération la plus longue des attaques à deux d'un personnage avec ses compagnons, en ticks (0 sans compagnon).</summary>
        private long DuoRecoveryTicks(Fighter owner)
        {
            long ticks = 0;
            foreach (Fighter c in CompanionsOf(owner))
                if (c.Companion!.Setup.Duo is { } duo) ticks = Math.Max(ticks, duo.RecoveryMs / _tickMs);
            return ticks;
        }

        /// <summary>
        /// Instantané : la recharge globale de <paramref name="owner"/> vient-elle d'une attaque à deux passée (commande <c>duo</c> de lui, à un
        /// tick passé, pour un compagnon qui en a une : recharge = tick + récupération) ?
        /// </summary>
        private bool DuoSetsGlobalCooldown(Fighter owner, long previous)
        {
            if (!_hasCompanions || owner.Character is null) return false;
            foreach (CombatCommand c in _setup.Commands)
            {
                if (c.Action != CombatAction.Duo || c.Actor != owner.Id || c.AtMs / _tickMs > previous || c.Companion is null) continue;
                if (_byId.TryGetValue(c.Companion, out Fighter? companion) && companion.Companion?.Setup.Duo is { } duo
                    && owner.GlobalCooldownReadyTick == checked(c.AtMs / _tickMs + duo.RecoveryMs / _tickMs)) return true;
            }
            return false;
        }

        /// <summary>
        /// Contrôles de cohérence de l'état d'un compagnon relu (au début du tick courant) ; null s'il est cohérent. Ordre et cible ensemble ;
        /// jauge dans [0, 100] ; traces de coup à des ticks passés, cibles connues ; rappel passé ; attaque à deux : au plus autant que de
        /// commandes duo passées de son Éveillé pour lui, impact à venir dans l'annonce, récupération dans sa durée, cible et provenance
        /// présentes ensemble.
        /// </summary>
        private string? CompanionProblem(Fighter f, CompanionState c)
        {
            long now = CurrentTick, previous = now - 1;
            if ((c.Order == CompanionOrder.Target) != (c.OrderTarget != null)) return "ordre et cible incohérents";
            if (c.OrderTarget != null && !Enemies(f, c.OrderTarget)) return "cible de l'ordre qui n'est pas un ennemi";
            if (c.Bond < 0 || c.Bond > CompanionSetup.BondMax) return "jauge de lien hors de [0, 100]";
            if (c.RecalledTick is long recalled && (recalled < 0 || recalled > previous)) return "rappel à un tick à venir";
            if (c.OwnerHitTick < 0 || c.OwnerHitTick > Math.Max(0, previous) || c.SelfHitTick < 0 || c.SelfHitTick > Math.Max(0, previous))
                return "trace de coup à un tick à venir";
            if ((c.OwnerHitTarget != null && !_byId.ContainsKey(c.OwnerHitTarget)) || (c.SelfHitTarget != null && !_byId.ContainsKey(c.SelfHitTarget)))
                return "trace de coup sur un combattant inconnu";
            long duos = _setup.Commands.LongCount(cmd => cmd.Action == CombatAction.Duo && cmd.Companion == f.Id && cmd.AtMs / _tickMs <= previous);
            if (c.DuosStarted < 0 || c.DuosStarted > duos) return "plus d'attaques à deux que de commandes";
            if (c.Setup.Duo is not { } duo)
                return c.DuosStarted == 0 && c.DuoTarget is null && c.DuoImpactTick is null && c.DuoReadyTick == 0 && c.DuoOrigin is null
                    ? null : "attaque à deux sans attaque à deux";
            if (c.DuosStarted == 0)
                return c.DuoTarget is null && c.DuoImpactTick is null && c.DuoReadyTick == 0 && c.DuoOrigin is null ? null : "attaque à deux sans commande";
            if (c.DuoTarget is null || c.DuoOrigin is null || !Enemies(f, c.DuoTarget)) return "attaque à deux sans cible ni provenance";
            if (c.DuoReadyTick < 0 || c.DuoReadyTick > checked(previous + duo.RecoveryMs / _tickMs)) return "récupération au-delà de sa durée";
            if (c.DuoImpactTick is long impact && (impact < now || impact > checked(previous + duo.TelegraphMs / _tickMs) || impact >= _durationTicks))
                return "impact hors de l'annonce";
            return null;
        }
    }
}
