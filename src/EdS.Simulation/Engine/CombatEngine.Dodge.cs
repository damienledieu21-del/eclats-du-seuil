using System;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Esquive (décision de Damien du 14 novembre 2026, qui revient sur L11-D4 ; choix « AAAA ») : un personnage d'un combat positionné
    /// bondit d'environ 3 m dans la direction donnée (le joystick), ou vers l'arrière de son orientation sans direction ; il est invulnérable
    /// au début du bond ; l'esquive coûte de l'endurance et a une recharge. Valeurs dans <c>combat.dodge</c> des règles (schéma 17) :
    /// <list type="bullet">
    /// <item>commande <c>dodge</c> acceptée au tick t (phase des commandes) : endurance dépensée, ligne <c>dodgeStarted</c> ;</item>
    /// <item>bond : dans la phase <c>movement</c> des ticks t à t + durée − 1, à la place de la marche, la distance répartie exactement sur
    /// ces ticks (somme des pas = distance), avec le décor et les emprises solides comme une marche (sous-pas d'au plus un demi-rayon,
    /// glissement) : un bond ne traverse pas un mur, il s'arrête avant. L'orientation ne change pas ; l'intention de marche reste ;</item>
    /// <item>invulnérabilité : pendant les ticks t à t + invulnérabilité − 1, un impact (coup dirigé, zone, impulsion d'une zone persistante,
    /// frappe de réaction) ou une application d'un ennemi est esquivé : ligne <c>evaded</c> à la place des dégâts, aucune altération, aucune
    /// poussée (<c>pushBlocked</c>, raison <c>evaded</c>) ; une altération déjà posée (DOT) continue ;</item>
    /// <item>recharge : nouvelle esquive refusée avant t + recharge.</item>
    /// </list>
    /// Refus atomiques, dans cet ordre : <c>actorDead</c>/<c>actorAbsent</c>, <c>actorCannotDodge</c> (pas un personnage, ou combat sans
    /// espace), contrôle qui bloque le déplacement (raison du contrôle), <c>guardActive</c> (comme une compétence : quitter la garde d'abord),
    /// <c>dodgeCooldown</c>, <c>insufficientEndurance</c>, <c>dodgeBlocked</c> (le premier pas du bond n'avancerait pas : mur, bord de l'aire
    /// ou emprise solide juste devant).
    /// </summary>
    public sealed partial class CombatEngine
    {
        // Durées arrondies au tick supérieur (exactes au tick de 50 ms des règles livrées) ; un bond dure au moins un tick (validation).
        private long DodgeDurationTicks => CeilTicks(_rules.DodgeDurationMs);
        private long DodgeInvulnerableTicks => CeilTicks(_rules.DodgeInvulnerableMs);
        private long DodgeCooldownTicks => CeilTicks(_rules.DodgeCooldownMs);

        private long CeilTicks(long ms) => IntMath.MulDiv(ms, 1, _tickMs, RoundingMode.Ceiling);

        /// <summary>Tick à partir duquel le combattant peut de nouveau esquiver (0 s'il n'a jamais esquivé).</summary>
        private long DodgeReadyTick(Fighter f) => f.DodgeStartTick is long start ? checked(start + DodgeCooldownTicks) : 0;

        /// <summary>Le combattant est-il invulnérable (esquive) à ce tick ?</summary>
        private bool Invulnerable(Fighter f, long tick) =>
            f.DodgeStartTick is long start && tick >= start && tick < checked(start + DodgeInvulnerableTicks);

        /// <summary>
        /// Un effet hostile de <paramref name="source"/> sur <paramref name="target"/> est-il esquivé à ce tick ? Seulement entre ennemis
        /// (jamais un soutien allié, jamais un effet sur soi), seulement pendant l'invulnérabilité de la cible.
        /// </summary>
        private bool Evades(long tick, Fighter source, Fighter target) =>
            !ReferenceEquals(source, target) && Invulnerable(target, tick) && Enemies(source, target);

        /// <summary>
        /// Motif de refus d'une esquive (ordre documenté ci-dessus), sans rien modifier ; sinon la direction unitaire du bond. Le contrôle de
        /// l'obstacle simule le premier pas du bond depuis les positions du moment (début de la phase des commandes).
        /// </summary>
        private string? CheckDodge(long tick, Fighter actor, GroundVector? direction, out long ux, out long uz)
        {
            ux = uz = 0;
            if (actor.Out) return OutReason(actor, "actor");
            if (actor.Character is null || !_spatial) return "actorCannotDodge";
            if (BlockReason(actor, StatusBlocks.Movement) is { } blocked) return blocked;
            if (actor.Guarding) return "guardActive";
            if (tick < DodgeReadyTick(actor)) return "dodgeCooldown";
            if (actor.Endurance < _rules.DodgeEnduranceCost) return "insufficientEndurance";
            Body body = actor.Body!;
            if (direction is { } d) Planar.TryNormalize(d.X.Raw, d.Z.Raw, out ux, out uz);   // non nulle (validation)
            else
            {
                ux = checked(-body.FacingX);
                uz = checked(-body.FacingZ);
            }
            int index = _fightersById.IndexOf(actor);
            int n = _fightersById.Count;
            var x = new long[n];
            var z = new long[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = _fightersById[i].Body!.X;
                z[i] = _fightersById[i].Body!.Z;
            }
            long step = DashLength(1);
            if (!DashAdvances(index, x, z, ux, uz, step, out _, out _)) return "dodgeBlocked";
            return null;
        }

        private void StartDodge(long tick, CommandItem item, Fighter actor, Provenance root, long ux, long uz)
        {
            if (_rules.DodgeEnduranceCost > 0) SpendEndurance(actor, _rules.DodgeEnduranceCost, tick);
            actor.DodgeStartTick = tick;
            actor.DodgeDirectionX = ux;
            actor.DodgeDirectionZ = uz;
            Emit(tick, CombatPhase.Commands, "dodgeStarted", root, w =>
            {
                w.Field("actor", actor.Id).Field("command", item.Index);
                WriteVector(w, "direction", ux, uz);
                w.Field("endurance", actor.Endurance).Field("endTick", checked(tick + DodgeDurationTicks))
                    .Field("invulnerableUntilTick", checked(tick + DodgeInvulnerableTicks)).Field("readyTick", DodgeReadyTick(actor));
            });
        }

        /// <summary>Longueur du pas <paramref name="k"/> (1 à durée) du bond : la distance répartie exactement, arrondis par défaut cumulés.</summary>
        private long DashLength(long k)
        {
            long ticks = DodgeDurationTicks, distance = _rules.DodgeDistance.Raw;
            return checked(IntMath.MulDiv(distance, k, ticks, RoundingMode.Floor) - IntMath.MulDiv(distance, k - 1, ticks, RoundingMode.Floor));
        }

        /// <summary>
        /// Pas du bond de ce combattant à ce tick : −1 s'il ne bondit pas ; 0 sous un contrôle qui bloque le déplacement (le bond est figé, ses
        /// ticks passent) ; sinon la longueur du pas.
        /// </summary>
        private long DashStep(Fighter f, long tick)
        {
            if (f.Out || f.DodgeStartTick is not long start || tick < start || tick >= checked(start + DodgeDurationTicks)) return -1;
            if (BlockReason(f, StatusBlocks.Movement) != null) return 0;
            return DashLength(checked(tick - start + 1));
        }

        /// <summary>
        /// Simule un pas de bond (mêmes règles qu'une marche : sous-pas, glissement, décor, bords, emprises solides) ; faux si le pas n'avance
        /// pas d'un huitième de sa longueur (il est alors annulé, comme un pas de marche : pas de tremblement contre un mur).
        /// </summary>
        private bool DashAdvances(int index, long[] x, long[] z, long ux, long uz, long step, out long nx, out long nz)
        {
            nx = x[index];
            nz = z[index];
            if (step <= 0 || !Planar.TryResize(ux, uz, step, out long dx, out long dz)) return false;
            var (px, pz) = Travel(index, x, z, dx, dz, step);
            if (checked(64 * Planar.LengthSquared(checked(px - x[index]), checked(pz - z[index]))) < checked(step * step)) return false;
            nx = px;
            nz = pz;
            return true;
        }
    }
}
