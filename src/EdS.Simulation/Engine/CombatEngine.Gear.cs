using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot V1.8d (décision de Damien : effet immédiat des bonus) : « bonus modifié » (commande <c>gear</c>), le bonus d'équipement d'un
    /// personnage changé en cours de combat.
    /// <list type="bullet">
    /// <item>Traité au <b>début de son tick</b> (phase <c>setup</c>, dans l'ordre de la file avec les arrivées), avant les expirations et les
    /// commandes : tout le tick, et les suivants, voient les nouvelles statistiques. La commande porte le <b>total</b> (équipement de l'entrée et
    /// bonus actifs), jamais une différence : un remplacement est un seul changement, sans trou ni double compte.</item>
    /// <item>Refusé (<c>commandRejected</c>, rien ne change) pour un mort ou un absent (<c>actorDead</c>, <c>actorAbsent</c>), et si les PV
    /// maximaux tomberaient sous 1 (<c>maxHealthTooLow</c>, impossible avec les règles livrées). Une garde, un étourdissement ou une rupture ne
    /// l'empêchent pas : un bonus qui finit en plein combat finit à son tick.</item>
    /// <item><b>Valeurs courantes, règle proportionnelle</b> : PV et endurance gardent la même part de leur maximum, arrondie par défaut
    /// (<c>⌊ valeur × nouveau max / ancien max ⌋</c>) ; une jauge pleine reste pleine ; un vivant garde au moins 1 PV. Un bonus ne soigne donc
    /// pas plus que sa part, ne tue jamais à sa fin et ne ressuscite pas. Une hausse puis la baisse qui la défait ne rendent rien (au plus un
    /// point perdu à l'arrondi) ; une baisse puis la hausse qui la défait peuvent perdre jusqu'à environ ancien maximum / nouveau maximum
    /// points (arrondi par défaut sur la jauge réduite), et n'en gagner que par le plancher de 1 PV.</item>
    /// <item>Statistiques recalculées par la même fonction qu'à l'entrée (<see cref="CombatantStats.ForCharacter"/>) ; le précontrôle numérique
    /// couvre l'enveloppe de tous les bonus du combat (<see cref="CombatSetup.WorstGear"/>). Ligne <c>gearChanged</c> (journal au format 21).</item>
    /// </list>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Phase où une commande est traitée : <see cref="CombatPhase.Setup"/> pour un bonus modifié, <see cref="CombatPhase.Commands"/> sinon.</summary>
        internal static CombatPhase PhaseOf(CombatCommand command) => command.Action == CombatAction.Gear ? CombatPhase.Setup : CombatPhase.Commands;

        /// <summary>
        /// Nouvelle valeur d'une jauge (PV ou endurance) quand son maximum passe de <paramref name="max"/> à <paramref name="newMax"/> : même part
        /// du maximum, arrondie par défaut ; pleine, elle reste pleine ; <paramref name="keepOne"/> : une valeur positive garde au moins 1.
        /// </summary>
        internal static long Rescale(long value, long max, long newMax, bool keepOne)
        {
            if (value >= max) return newMax;   // pleine (ou maximum nul) : pleine au nouveau maximum
            long scaled = IntMath.MulDiv(value, newMax, max, RoundingMode.Floor);
            return keepOne && value > 0 && scaled < 1 ? 1 : scaled;
        }

        /// <summary>Traite un bonus modifié au début de son tick : refus atomique, ou nouvelles statistiques, maximums et valeurs courantes.</summary>
        private void ChangeGear(long tick, CommandItem item)
        {
            Fighter actor = _byId[item.Command.Actor];
            GearBonus gear = item.Command.Gear!;
            // Tout est calculé avant la réserve et la moindre mutation (un débordement arrêterait le combat sans rien avoir changé).
            string? reason = CheckAlive(actor);
            DerivedStats? stats = null;
            long maxHealth = 0, health = 0, maxEndurance = 0, endurance = 0;
            if (reason is null)
            {
                stats = CombatantStats.ForCharacter(_setup.Stats, actor.Character!.Build, ModifierSet.For(actor.Character), gear);
                maxHealth = stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                maxEndurance = stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                if (maxHealth < 1) reason = "maxHealthTooLow";
                else
                {
                    health = Rescale(actor.Health!.Value, actor.MaxHealth!.Value, maxHealth, keepOne: true);
                    endurance = Rescale(actor.Endurance, actor.MaxEndurance, maxEndurance, keepOne: false);
                }
            }
            long shift = reason is null ? checked(actor.GearHealthShift + (maxHealth - health) - (actor.MaxHealth!.Value - actor.Health!.Value)) : 0;
            Reserve(Cost.Line);   // retiré de la file par RunArrivals (une opération), comme une commande
            if (Reject(tick, CombatPhase.Setup, item, actor, reason)) return;
            actor.Gear = gear;
            actor.Stats = stats!;
            actor.MaxHealth = maxHealth;
            actor.Health = health;
            actor.MaxEndurance = maxEndurance;
            actor.Endurance = endurance;
            actor.GearHealthShift = shift;
            Emit(tick, CombatPhase.Setup, "gearChanged", Provenance.NewRoot($"command:{item.Index}", actor.Id), w =>
            {
                w.Field("actor", actor.Id).Field("command", item.Index);
                WriteGear(w, gear);
                w.Field("maxHealth", maxHealth).Field("health", health).Field("maxEndurance", maxEndurance).Field("endurance", endurance);
            });
        }

        /// <summary>Bonus d'équipement sous la forme des fichiers (<c>gear</c> : seulement ses champs non nuls ; objet vide sans bonus).</summary>
        internal static void WriteGear(JsonLineWriter w, GearBonus gear)
        {
            w.Name("gear").BeginObject();
            for (int g = 0; g < GearBonus.Fields.Count; g++)
                if (gear[g] != 0) w.Field(GearBonus.Fields[g], gear[g]);
            w.EndObject();
        }
    }
}
