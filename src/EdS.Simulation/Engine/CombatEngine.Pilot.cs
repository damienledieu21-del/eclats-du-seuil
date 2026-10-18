using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Combat;

namespace EdS.Simulation.Engine
{
    /// <summary>Nature d'un combattant vue par un pilote (sous-lot 8.6).</summary>
    public enum CombatantKind
    {
        Character = 0,
        Dummy = 1,
        Assailant = 2,
    }

    /// <summary>
    /// Sous-lot 8.6 : instantané en lecture seule d'un combattant au début du tick courant, avant ses expirations et ses commandes, pour
    /// un pilote de mesure (<c>EdS.Simulation.Pilot</c>). Une altération qui expire à ce tick n'y figure plus ; une fenêtre de parade qui
    /// se ferme à ce tick non plus. Aucune référence à l'état mutable du moteur n'est exposée.
    /// </summary>
    public sealed class CombatantView
    {
        public string Id { get; internal set; } = string.Empty;
        public string Team { get; internal set; } = string.Empty;
        public CombatantKind Kind { get; internal set; }
        public bool IsDead { get; internal set; }

        /// <summary>PV ; null pour un mannequin ou un assaillant aux PV infinis.</summary>
        public long? Health { get; internal set; }
        public long? MaxHealth { get; internal set; }

        // Personnage seulement (valeurs neutres sinon).
        public CharacterSetup? Character { get; internal set; }
        public CombatantRules? Rules { get; internal set; }
        public long Endurance { get; internal set; }
        public long MaxEndurance { get; internal set; }
        public int Resonance { get; internal set; }
        public int RampartCharges { get; internal set; }
        public bool Guarding { get; internal set; }

        /// <summary>Fenêtre de parade encore ouverte à ce tick (elle se ferme à <c>ParryEndTick</c>).</summary>
        public bool ParryOpen { get; internal set; }
        public long ParryRecoveryUntilTick { get; internal set; }
        public long GlobalCooldownReadyTick { get; internal set; }
        public IReadOnlyDictionary<string, long> CooldownReadyTicks { get; internal set; } = new Dictionary<string, long>();

        /// <summary>Compétences, garde et parade non bloquées par une altération encore active à ce tick.</summary>
        public bool CanCast { get; internal set; }
        public bool CanGuard { get; internal set; }
        public bool CanParry { get; internal set; }

        /// <summary>Altérations actives à ce tick (identifiants, ordre ordinal, sans doublon).</summary>
        public IReadOnlyList<string> States { get; internal set; } = Array.Empty<string>();

        // Assaillant seulement.
        public AssailantSetup? Assailant { get; internal set; }

        /// <summary>Tick du prochain tour programmé ; null s'il n'y en a plus avant la fin.</summary>
        public long? NextAttackTick { get; internal set; }
        public AssailantAttack? NextAttack { get; internal set; }

        /// <summary>Cible actuelle de ses attaques à cible unique (provocation la plus récente comprise).</summary>
        public string? CurrentTarget { get; internal set; }

        /// <summary>Ses attaques sont bloquées à ce tick (contrôle actif).</summary>
        public bool AttacksBlocked { get; internal set; }

        /// <summary>Tick de disponibilité d'une compétence (0 si jamais lancée).</summary>
        public long CooldownReadyTick(string skill) => CooldownReadyTicks.TryGetValue(skill, out long tick) ? tick : 0;
    }

    public sealed partial class CombatEngine
    {
        private readonly Dictionary<(string Actor, long Tick), int> _commandsPerActorTick = new Dictionary<(string, long), int>();
        private readonly List<CombatCommand> _submitted = new List<CombatCommand>();
        private int _commandCount;

        /// <summary>Identifiants des combattants, dans l'ordre du scénario.</summary>
        public IReadOnlyList<string> CombatantIds => _fighters.Select(f => f.Id).ToArray();

        /// <summary>Durée du tick en millisecondes.</summary>
        public long TickMilliseconds => _tickMs;

        /// <summary>Description du combat simulé.</summary>
        public CombatSetup Setup => _setup;

        /// <summary>Sous-lot 8.6 : commandes soumises pendant la simulation (<see cref="Submit"/>), dans l'ordre de soumission.</summary>
        public IReadOnlyList<CombatCommand> SubmittedCommands => _submitted.AsReadOnly();

        private void CountSubmitted(CombatCommand command)
        {
            var key = (command.Actor, command.AtMs / _tickMs);
            _commandsPerActorTick[key] = _commandsPerActorTick.TryGetValue(key, out int n) ? n + 1 : 1;
            _commandCount++;
        }

        /// <summary>Vue en lecture seule d'un combattant au début du tick courant (sous-lot 8.6).</summary>
        public CombatantView View(string id)
        {
            if (!_byId.TryGetValue(id ?? throw new ArgumentNullException(nameof(id)), out Fighter? f))
                throw new ArgumentException($"Combattant inconnu : {id}.", nameof(id));
            long tick = CurrentTick;
            bool Blocked(StatusBlocks action) => f.Statuses.Values.Any(s => (s.Definition.Blocks & action) != 0 && s.UntilTick > tick);
            var view = new CombatantView
            {
                Id = f.Id,
                Team = f.Team,
                Kind = f.Character != null ? CombatantKind.Character : f.Assailant != null ? CombatantKind.Assailant : CombatantKind.Dummy,
                IsDead = f.IsDead,
                Health = f.Health,
                MaxHealth = f.MaxHealth,
                States = f.Statuses.Values.Where(s => s.UntilTick > tick).Select(s => s.State).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            };
            if (f.Character != null)
            {
                view.Character = f.Character;
                view.Rules = f.Rules;
                view.Endurance = f.Endurance;
                view.MaxEndurance = f.MaxEndurance;
                view.Resonance = f.Resonance;
                view.RampartCharges = f.RampartCharges;
                view.Guarding = f.Guarding;
                view.ParryOpen = f.ParryOpen && f.ParryEndTick > tick;
                view.ParryRecoveryUntilTick = f.ParryRecoveryUntilTick;
                view.GlobalCooldownReadyTick = f.GlobalCooldownReadyTick;
                view.CooldownReadyTicks = new Dictionary<string, long>(f.CooldownReadyTick, StringComparer.Ordinal);
                view.CanCast = !Blocked(StatusBlocks.Skills);
                view.CanGuard = !Blocked(StatusBlocks.Guard);
                view.CanParry = !Blocked(StatusBlocks.Parry);
            }
            if (f.Assailant is { } assailant)
            {
                view.Assailant = assailant;
                view.NextAttackTick = f.NextAttackTick;
                view.NextAttack = assailant.Attacks[f.NextAttack];
                view.CurrentTarget = ViewTarget(f, tick);
                view.AttacksBlocked = Blocked(StatusBlocks.AssailantAttacks);
            }
            return view;
        }

        /// <summary>
        /// Cible des attaques à cible unique vue au début du tick (<see cref="SingleTarget"/> après les expirations du tick) : la source de
        /// la provocation active la plus récente, sinon la cible du scénario (null si l'assaillant n'a que des attaques de zone).
        /// </summary>
        private static string? ViewTarget(Fighter assailant, long tick)
        {
            StatusInstance? taunt = null;
            foreach (StatusInstance status in assailant.Statuses.Values)
                if (status.Definition.IsTaunt && status.UntilTick > tick && (taunt is null || status.AppliedSeq > taunt.AppliedSeq)) taunt = status;
            return taunt?.Source ?? assailant.Assailant!.Target;
        }

        /// <summary>
        /// Sous-lot 8.6 : soumet une commande pour le tick courant, avant qu'il soit simulé (pilote de mesure). Elle est traitée exactement
        /// comme une commande du scénario de même rang : son numéro suit ceux du scénario puis des commandes déjà soumises, si bien qu'un
        /// combat rejoué avec le scénario complété de <see cref="SubmittedCommands"/> est identique. Mêmes plafonds d'admission que le
        /// scénario (commandes au total, par acteur et par tick) ; l'acteur et la cible doivent exister.
        /// </summary>
        public void Submit(CombatCommand command)
        {
            if (command is null) throw new ArgumentNullException(nameof(command));
            if (IsFinished) throw new InvalidOperationException("Combat terminé : plus aucune commande.");
            if (!_byId.ContainsKey(command.Actor)) throw new ArgumentException($"Acteur inconnu : {command.Actor}.", nameof(command));
            if (command.Target != null && !_byId.ContainsKey(command.Target)) throw new ArgumentException($"Cible inconnue : {command.Target}.", nameof(command));
            if (command.AtMs % _tickMs != 0 || command.AtMs / _tickMs != CurrentTick)
                throw new ArgumentException($"Commande à {command.AtMs} ms : seul le tick courant ({CurrentTick}) est admis.", nameof(command));
            var key = (command.Actor, CurrentTick);
            if (_commandsPerActorTick.TryGetValue(key, out int n) && n >= _rules.MaxCommandsPerActorPerTick)
                throw new InvalidOperationException($"Plus de {_rules.MaxCommandsPerActorPerTick} commandes de « {command.Actor} » au tick {CurrentTick}.");
            if (_commandCount >= _rules.MaxCommands)
                throw new InvalidOperationException($"Plus de {_rules.MaxCommands} commandes (combat.limits.maxCommands).");
            CountSubmitted(command);
            // Hors traitement, comme les commandes du scénario mises en file à la construction : aucun budget du tick n'est consommé.
            _queue.Enqueue(CurrentTick, CombatPhase.Commands, new CommandItem { Index = _setup.Commands.Count + _submitted.Count, Command = command });
            if (_queue.Count > _peakPending) _peakPending = _queue.Count;
            _submitted.Add(command);
        }
    }
}
