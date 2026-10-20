using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;
using EdS.Simulation.Combat;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Moteur de combat : horloge logique en ticks, file d'événements ordonnée, commandes validées atomiquement,
    /// coûts, recharges, temps global de recharge, dégâts directs, régénération d'endurance (lot 5) ; garde, parade
    /// parfaite, Charges de rempart, Résonance, Fragilité, rupture de garde et assaillant d'entraînement (lot 6) ; altérations
    /// génériques définies en données (lot 7.2) : DOT, contrôles, modificateurs et immunités, voir <see cref="StatusDefinition"/>.
    /// Aucune lecture de l'horloge système, aucun flottant, aucun état partagé entre combats : tout l'état appartient
    /// à l'instance. Même combat (règles, participants, commandes, graine) = même journal, octet par octet.
    /// <para>Ordre d'un tick (A10) : expirations → commandes (initiative décroissante, puis identifiant) → déplacement (combat positionné
    /// seulement, sous-lot 11.4a, voir CombatEngine.Movement.cs) → résolution des actions et dégâts → effets périodiques (entretien de la
    /// garde) → régénération → morts et fin de combat.</para>
    /// <para>Budgets (lot 7.1) : chaque traitement élémentaire (une échéance, le retrait d'une commande, une commande, une
    /// action complète avec toutes ses cibles, le passage d'un combattant dans une phase balayée) calcule son coût exact
    /// en travail, lignes et places de file à partir de l'état courant, puis le <b>réserve avant toute mutation</b>. Si la
    /// réserve dépasse un budget, le combat s'arrête proprement sans que ce traitement ait rien modifié. Voir
    /// docs/LIMITES_LOT_7.md pour le modèle de coût et les limites d'atomicité restantes (débordement numérique).</para>
    /// </summary>
    public sealed partial class CombatEngine
    {
        /// <summary>Version du moteur, inscrite au journal : un rejeu exige la même version.</summary>
        public const string Version = "0.16.0";

        public const string LogFormat = "eds-combat-log";

        /// <summary>
        /// Version du format du journal. 2 (correctif 6.1) : <c>stateApplied</c> et <c>stateExpired</c> portent
        /// <c>instance</c> et <c>generation</c> ; nouvel événement <c>stateRemoved</c>. 3 (lot 7.2) : toutes les lignes d'état portent
        /// <c>category</c> et <c>source</c> ; nouveaux événements <c>stateBlocked</c> et <c>dotDamage</c> ; <c>attackStarted</c> porte
        /// <c>applies</c> (liste des altérations) au lieu de <c>appliesFragility</c> ; garde interrompue (<c>guardStopped</c>, raison
        /// <c>interrupted</c>) ; motifs de refus et de saut issus des altérations. 4 (lot 7.3) : chaque ligne de jeu porte <c>eid</c>
        /// (identité de l'effet) et <c>cause</c> (eid du parent, null pour l'effet racine) ; l'effet racine porte <c>root</c> ; un effet
        /// de profondeur ≥ 1 porte <c>depth</c> et <c>trigger</c>. Les diagnostics n'en portent aucun. 5 (sous-lot 8.1) : la ligne
        /// <c>combatant</c> porte <c>statusResistance</c> et <c>rules</c> (règles effectives d'un personnage, null sinon) ;
        /// <c>guardBroken</c> porte <c>rampartChargesSpent</c>. 6 (sous-lot 8.2) : <c>damage</c> et <c>dotDamage</c> portent
        /// <c>absorbed</c> et <c>absorbedBy</c> (boucliers consommés) ; <c>stateApplied</c> d'un bouclier porte <c>absorb</c> ;
        /// <c>stateRemoved</c> peut avoir la raison <c>depleted</c> ; <c>stateBlocked</c> la raison <c>empty</c> ; <c>combatantSummary</c>
        /// porte <c>damageAbsorbed</c> ; les règles effectives (<c>rules</c>) portent <c>shield.potencyDealt</c>. 7 (sous-lot 8.3) :
        /// <c>damage</c> et <c>dotDamage</c> portent <c>transferred</c> ; ligne <c>damageTransferred</c> (part transférée au protecteur,
        /// non notifiée) ; <c>stateApplied</c> d'une protection porte <c>redirectRatio</c> ; raisons <c>notApplicable</c> (stateBlocked) et
        /// <c>sourceDied</c> (stateRemoved) ; <c>combatantSummary</c> porte <c>damageTransferred</c> et <c>damageRedirected</c> ; les règles
        /// effectives portent <c>protection.redirectRatio</c> et <c>protection.redirectThroughGuard</c>. 8 (sous-lot 8.4) : les règles
        /// effectives portent <c>guard.noUpkeep</c> ; <c>rampartChargeCost</c> de <c>skillCast</c> donne les Charges réellement dépensées
        /// (toutes les Charges détenues pour une compétence qui les dépense toutes, comme Contrecoup). 9 (sous-lot 11.1) : un assaillant de
        /// l'en-tête peut porter <c>health</c> (PV de départ et maximaux d'un assaillant mortel) ; sa ligne <c>combatant</c> porte alors ses
        /// PV, et il meurt à 0 PV (<c>died</c>, une fois) : ses tours suivants sont abandonnés sans ligne, la victoire peut revenir aux
        /// joueurs (<c>combatEnded</c>, <c>teamDefeated</c>). 10 (sous-lot 11.4a) : combat positionné. Le scénario de l'en-tête peut porter
        /// <c>space</c> (aire et obstacles), chaque combattant <c>position</c>, <c>facing</c>, <c>radius</c> et, pour un assaillant, <c>speed</c>,
        /// et les commandes <c>move</c> (avec <c>direction</c>) et <c>stop</c> ; la ligne <c>combatant</c> porte alors position, orientation,
        /// rayon et vitesse ; nouvelles lignes <c>moveStarted</c> et <c>moveStopped</c> (commandes acceptées), <c>moved</c> (phase
        /// <c>movement</c>, une par tick où quelque chose bouge, sans eid), raison de refus <c>notMoving</c> et position finale sur
        /// <c>combatantSummary</c>. Un combat sans espace écrit
        /// exactement les mêmes lignes qu'au format 9. Un journal d'un autre format est refusé au rejeu.
        /// </summary>
        public const int LogFormatVersion = 10;

        // Identifiants stables du journal.
        public const string Resonance = "resonance";
        public const string RampartCharges = "rampartCharges";
        public const string EnduranceResource = "endurance";

        private sealed class Fighter
        {
            public CombatantSetup Setup = null!;
            public CharacterSetup? Character;
            public AssailantSetup? Assailant;
            public DerivedStats Stats = null!;

            /// <summary>Sous-lot 8.1 : règles effectives (règles de base et modificateurs X1, évaluation des X2) ; neutres hors personnage.</summary>
            public CombatantRules Rules = null!;
            public readonly Dictionary<string, DerivedStats> AttackStats = new Dictionary<string, DerivedStats>(StringComparer.Ordinal);
            public int NextAttack;
            public HashSet<string> Castable = new HashSet<string>(StringComparer.Ordinal);
            public long? Health;
            public long? MaxHealth;
            public long Endurance;
            public long MaxEndurance;

            /// <summary>Premier tick compté pour la régénération (dernière dépense + délai) ; null = rien à régénérer.</summary>
            public long? RegenFromTick;
            public long RegenGranted;

            public long GlobalCooldownReadyTick;
            public readonly Dictionary<string, long> CooldownReadyTick = new Dictionary<string, long>(StringComparer.Ordinal);

            public bool Guarding;

            /// <summary>
            /// Entretien dû depuis le début du combat, en points entiers (M01), et son reliquat en unités brutes × ms (moins de
            /// 1000 × échelle). Sous-lot 8.2 : cumulé tick par tick au taux du moment (renforcements compris) ; à taux constant, égal à
            /// ⌊ coût/s × ticks gardés × durée du tick / 1000 ⌋ comme avant.
            /// </summary>
            public long GuardUpkeepDue;
            public long GuardUpkeepRemainder;

            /// <summary>Entretien déjà payé depuis le début du combat : le reliquat fractionnaire n'est jamais perdu (M01).</summary>
            public long GuardUpkeepPaid;

            public bool ParryOpen;
            public long ParryStartTick;
            public long ParryEndTick;             // fenêtre [début, fin[
            public bool ParrySucceeded;
            public long ParryRecoveryUntilTick;   // nouvelle parade refusée tant que tick < valeur

            /// <summary>
            /// Altérations actives (lot 7.2), une instance par clé : (altération, source) pour un cumul par source, (altération) sinon ;
            /// voir <see cref="StatusInstance"/>.
            /// </summary>
            public readonly Dictionary<StatusKey, StatusInstance> Statuses = new Dictionary<StatusKey, StatusInstance>();

            public int Resonance;
            public int RampartCharges;

            /// <summary>Numéro du prochain tour d'un assaillant (racine <c>assailantTurn:&lt;id&gt;:&lt;n&gt;</c>), sauts compris.</summary>
            public long NextTurn;

            /// <summary>Sous-lot 8.6 : tick du prochain tour programmé d'un assaillant ; null s'il n'y en a plus avant la fin (vue du pilote).</summary>
            public long? NextAttackTick;

            /// <summary>
            /// Lot 7.3 (A6) : provenance du premier effet qui a amené les points de vie à 0 (impact ou tick de DOT), cause = cet effet ;
            /// la ligne <c>died</c> et les retraits qui en découlent en héritent.
            /// </summary>
            public Provenance? LethalCause;

            /// <summary>Lot 7.3 : déclencheurs permanents (talents, compétences équipées, assaillant), triés (§5.4).</summary>
            public List<TriggerSlot> Triggers = null!;

            public Pcg32 Random = null!;
            public bool IsDead;

            /// <summary>Sous-lot 11.4 : état spatial dans un combat positionné ; null sinon.</summary>
            public Body? Body;
            public readonly CombatantResult Result = new CombatantResult();

            public string Id => Setup.Id;
            public string Team => Setup.Team;
        }

        /// <summary>Clé d'une instance d'altération sur une cible : la source n'y figure que pour un cumul par source (DOT, A13).</summary>
        private readonly struct StatusKey : IEquatable<StatusKey>
        {
            public readonly string State;
            public readonly string? Source;

            public StatusKey(string state, string? source)
            {
                State = state;
                Source = source;
            }

            public bool Equals(StatusKey other) => string.Equals(State, other.State, StringComparison.Ordinal) && string.Equals(Source, other.Source, StringComparison.Ordinal);
            public override bool Equals(object? obj) => obj is StatusKey other && Equals(other);
            public override int GetHashCode() => unchecked(StringComparer.Ordinal.GetHashCode(State) * 31 + (Source is null ? 0 : StringComparer.Ordinal.GetHashCode(Source)));
        }

        /// <summary>
        /// Instance d'un état temporaire (correctif 6.1, M02). <see cref="Id"/> est unique dans le combat et jamais réutilisé ;
        /// <see cref="Generation"/> augmente à chaque renouvellement. Une échéance programmée porte le couple (instance,
        /// génération) en vigueur au moment où elle a été programmée : elle n'agit que si ce couple est toujours celui de
        /// l'état actif. Une échéance périmée (renouvellement, retrait à la mort, réapplication après expiration) ne
        /// déclenche donc jamais de transition, et un état non cumulable expire exactement une fois.
        /// </summary>
        private sealed class StatusInstance
        {
            public long Id;
            public int Generation;
            public StatusDefinition Definition = null!;
            public StatusKey Key;

            /// <summary>Combattant qui porte l'instance.</summary>
            public Fighter Owner = null!;

            /// <summary>Combattant qui a posé (ou renouvelé en dernier) l'instance ; null pour une rupture de garde par l'entretien.</summary>
            public string? Source;

            /// <summary>
            /// Hors DOT : actif tant que tick &lt; UntilTick, retiré en phase 1 du tick UntilTick. DOT : fin de l'instance ; le dernier
            /// tick est le premier dont le suivant dépasserait cette fin, et il expire l'instance en phase 4 après ses dégâts.
            /// </summary>
            public long UntilTick;

            /// <summary>DOT : puissance figée par tick (puissance de la source × coefficient), la plus forte conservée au renouvellement.</summary>
            public Fixed Power;

            /// <summary>DOT : tick du prochain tick de dégâts (cadence d'origine, jamais décalée par un renouvellement, D4).</summary>
            public long NextTick;

            /// <summary>Sous-lot 8.2 (X4), bouclier : valeur restante ; épuisé à 0, retiré en fin de tick (<c>depleted</c>).</summary>
            public long Absorb;

            /// <summary>Sous-lot 8.2 : provenance de l'effet qui a épuisé le bouclier (cause de son retrait) ; null tant qu'il reste une valeur.</summary>
            public Provenance? DepletedBy;

            /// <summary>
            /// Sous-lot 8.3 : rang de la dernière application acceptée (nouvelle ou renouvellement), unique dans le combat : la provocation
            /// et la protection les plus récentes l'emportent.
            /// </summary>
            public long AppliedSeq;

            /// <summary>
            /// Lot 7.3 (A1, A2) : provenance de la dernière application acceptée, cause = son <c>stateApplied</c>, avec l'instance et la
            /// génération courantes. Un renouvellement la remplace ; un refus n'y touche pas. Ticks de DOT, expiration et état accordé
            /// la lisent au moment où ils agissent (A3 à A5).
            /// </summary>
            public Provenance Origin = null!;

            /// <summary>Lot 7.3 : déclencheurs portés par l'instance (identité : numéro d'instance) ; null si l'altération n'en a pas.</summary>
            public TriggerSlot[]? Slots;

            public string State => Definition.Id;
        }

        private abstract class Scheduled { }

        private sealed class CommandItem : Scheduled
        {
            public int Index;
            public CombatCommand Command = null!;
        }

        private sealed class AssailantTurn : Scheduled
        {
            public Fighter Fighter = null!;
            public long Turn;
        }

        private sealed class CooldownExpiry : Scheduled
        {
            public Fighter Fighter = null!;
            public string Skill = null!;
        }

        private sealed class StateExpiry : Scheduled
        {
            public Fighter Fighter = null!;
            public StatusKey Key;
            public long Instance;
            public int Generation;
        }

        /// <summary>Tick d'un DOT (lot 7.2) : un seul élément par instance, qui se reprogramme lui-même en phase 4.</summary>
        private sealed class DotTick : Scheduled
        {
            public Fighter Fighter = null!;
            public StatusKey Key;
            public long Instance;
        }

        private sealed class ParryWindowEnd : Scheduled
        {
            public Fighter Fighter = null!;
            public long StartTick;

            /// <summary>Provenance héritée de la commande <c>parry</c> qui a ouvert la fenêtre, cause = <c>parryStarted</c>.</summary>
            public Provenance Origin = null!;
        }

        private sealed class PendingAction
        {
            public Fighter Actor = null!;
            public SkillDefinition? Skill;
            public SkillCombatDefinition? Combat;
            public AssailantAttack? Attack;
            public Fighter? Target;

            /// <summary>Provenance des conséquences de l'action (impacts, gains), cause = <c>skillCast</c> ou <c>attackStarted</c>.</summary>
            public Provenance Origin = null!;

            /// <summary>Faux pour une action qui dépense de la Résonance : pas de génération récursive (A5).</summary>
            public bool GeneratesResonance = true;

            /// <summary>Sous-lot 8.4 (X8) : Charges dépensées au lancement, pour le coefficient par Charge de la compétence.</summary>
            public int ChargesSpent;

            /// <summary>Lot 7.3 : frappe d'une réaction (effet <c>strike</c>), au rang du déclencheur ; ni compétence ni attaque.</summary>
            public TriggerEffect? Strike;
            public int StrikeRank = 1;

            public SkillTargeting Targeting => Combat?.Targeting ?? Attack?.Targeting ?? SkillTargeting.Enemy;
            public bool Unblockable => Combat?.Unblockable ?? Attack?.Unblockable ?? Strike!.Unblockable;
            public IReadOnlyList<StatusApplication> Applies => Combat?.Applies ?? Attack?.Applies ?? (IReadOnlyList<StatusApplication>)Array.Empty<StatusApplication>();

            /// <summary>Sous-lot 8.3 (X5) : l'action inflige-t-elle des dégâts directs ? Faux pour une compétence de soutien.</summary>
            public bool Damaging => Combat?.HasDamage ?? true;

            /// <summary>Sous-lot 8.3 : applications portées sur chaque cible de l'action (sans cible propre).</summary>
            public IEnumerable<StatusApplication> TargetApplies => Applies.Where(a => a.Target == ApplicationTarget.Targets);
            public DamageType DamageType => Combat?.DamageType ?? Attack?.DamageType ?? Strike!.DamageType!.Value;
        }

        /// <summary>Un budget du tick ou la capacité de la file serait dépassé par le traitement suivant (lot 7.1).</summary>
        private sealed class BudgetExceeded : Exception
        {
            public string Reason { get; }
            public long Limit { get; }

            public BudgetExceeded(string reason, long limit)
            {
                Reason = reason;
                Limit = limit;
            }
        }

        /// <summary>
        /// Coût d'un traitement (lot 7.1) : unités de travail, lignes de simulation comptées, places de file prises.
        /// Une opération (retrait de la file, impact, application ou retrait d'état, passage d'un combattant) vaut une
        /// unité ; une ligne vaut une unité et une ligne ; une mise en file vaut une unité et une place.
        /// </summary>
        private readonly struct Cost
        {
            public readonly long Work;
            public readonly long Events;
            public readonly long Queue;

            public Cost(long work, long events, long queue)
            {
                Work = work;
                Events = events;
                Queue = queue;
            }

            public static readonly Cost None = new Cost(0, 0, 0);
            public static readonly Cost Op = new Cost(1, 0, 0);
            public static readonly Cost Line = new Cost(1, 1, 0);
            public static readonly Cost Enqueue = new Cost(1, 0, 1);

            public static Cost operator +(Cost a, Cost b) =>
                new Cost(checked(a.Work + b.Work), checked(a.Events + b.Events), checked(a.Queue + b.Queue));

            public static Cost operator *(long k, Cost c) => new Cost(checked(k * c.Work), checked(k * c.Events), checked(k * c.Queue));

            public Cost If(bool condition) => condition ? this : None;

            /// <summary>Majorant composante par composante (lot 7.3, réservation majorée d'une réaction).</summary>
            public static Cost Max(Cost a, Cost b) => new Cost(Math.Max(a.Work, b.Work), Math.Max(a.Events, b.Events), Math.Max(a.Queue, b.Queue));
        }

        private readonly CombatSetup _setup;
        private readonly CombatRules _rules;
        private readonly ICombatLog _log;
        private readonly List<Fighter> _fighters;          // ordre du scénario
        private readonly List<Fighter> _fightersById;      // ordre ordinal des identifiants
        private readonly Dictionary<string, Fighter> _byId;
        private readonly EventQueue<Scheduled> _queue = new EventQueue<Scheduled>();
        private readonly long _tickMs;
        private readonly long _durationTicks;
        private readonly long _globalCooldownTicks;
        private readonly long _regenDelayTicks;
        private readonly CombatantRules _baseRules;
        private readonly StatusCatalog _statuses;
        private readonly Dictionary<string, int> _statusOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        private long _sequence;
        private long _lastEid;
        private long _lastStatusInstance;
        private long _lastApplication;

        /// <summary>Sous-lot 8.3 : le catalogue contient-il une protection ? (majorant des frappes de réaction, ligne de transfert)</summary>
        private readonly bool _hasProtections;
        private long _eventsThisTick;
        private long _workThisTick;
        private bool _countEvents;

        // Réserve du traitement en cours (lot 7.1) : le travail et les lignes ne peuvent pas dépasser ces valeurs, ni les
        // mises en file ce nombre. Hors traitement, la réserve est vide : toute consommation non réservée est une erreur.
        private long _scopeWork;
        private long _scopeEvents;
        private long _scopeEnqueues;

        private long _peakWork;
        private long _peakEvents;
        private long _peakPending;

        public long CurrentTick { get; private set; }

        /// <summary>Travail consommé par le dernier tick simulé (lot 7.1, diagnostic hors journal).</summary>
        public long LastTickWork { get; private set; }

        /// <summary>Lignes de simulation comptées au dernier tick simulé (lot 7.1, diagnostic hors journal).</summary>
        public long LastTickEvents { get; private set; }
        public bool IsFinished => Result != null;
        public CombatResult? Result { get; private set; }

        /// <exception cref="CombatSetupException">Le combat est invalide (voir <see cref="CombatSetup.Validate"/>).</exception>
        public CombatEngine(CombatSetup setup, ICombatLog log) : this(setup, log, queueBound: true) { }

        /// <summary>Réservé aux tests : <paramref name="queueBound"/> faux omet le majorant de saturation (correctif 7.2) pour
        /// exercer l'arrêt <c>queueLimitExceeded</c>, devenu un filet de sécurité inatteignable pour un combat validé.</summary>
        internal CombatEngine(CombatSetup setup, ICombatLog log, bool queueBound) : this(setup, log, queueBound, numericLimits: true) { }

        /// <summary>Réservé aux tests (correctif 7.3.1) : <paramref name="numericLimits"/> faux omet le précontrôle numérique pour
        /// exercer l'arrêt <c>overflow</c> et vérifier qu'aucun impact n'est appliqué à moitié.</summary>
        internal CombatEngine(CombatSetup setup, ICombatLog log, bool queueBound, bool numericLimits)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            var violations = setup.Validate(queueBound, numericLimits);
            if (violations.Count > 0) throw new CombatSetupException(violations);

            _rules = setup.Combat;
            _tickMs = _rules.TickMilliseconds;
            _durationTicks = setup.DurationMs / _tickMs;
            _globalCooldownTicks = _rules.GlobalCooldownMs / _tickMs;
            _regenDelayTicks = _rules.EnduranceRegenDelayMs / _tickMs;
            _baseRules = CombatantRules.For(_rules, ModifierSet.Empty);
            _statuses = _rules.Statuses;
            for (int i = 0; i < _statuses.Definitions.Count; i++) _statusOrder[_statuses.Definitions[i].Id] = i;
            _hasProtections = _statuses.Definitions.Any(d => d.IsProtection);

            _fighters = setup.Combatants.Select(CreateFighter).ToList();
            _fightersById = _fighters.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            _byId = _fighters.ToDictionary(f => f.Id, StringComparer.Ordinal);
            BuildTriggerIndex();
            InitSpace();

            for (int i = 0; i < setup.Commands.Count; i++)
            {
                CombatCommand command = setup.Commands[i];
                Schedule(command.AtMs / _tickMs, CombatPhase.Commands, new CommandItem { Index = i, Command = command });
            }
            foreach (Fighter f in _fighters)
                if (f.Assailant is { } assailant && assailant.FirstAttackMs / _tickMs < _durationTicks)
                {
                    f.NextAttackTick = assailant.FirstAttackMs / _tickMs;
                    Schedule(f.NextAttackTick.Value, CombatPhase.Commands, new AssailantTurn { Fighter = f, Turn = f.NextTurn++ });
                }
            for (int i = 0; i < setup.Commands.Count; i++) CountSubmitted(setup.Commands[i]);

            _log.Append(CombatLogHeader.Write(setup));
            foreach (Fighter f in _fighters) LogCombatant(f);
        }

        /// <summary>Simule un combat complet et renvoie son résultat.</summary>
        public static CombatResult Run(CombatSetup setup, ICombatLog log)
        {
            var engine = new CombatEngine(setup, log);
            while (engine.Step()) { }
            return engine.Result!;
        }

        private Fighter CreateFighter(CombatantSetup setup)
        {
            var f = new Fighter { Setup = setup, Random = new Pcg32(unchecked((ulong)_setup.Seed), StreamOf(setup.Id)), Body = CreateBody(setup) };
            f.Result.Id = setup.Id;
            f.Result.Team = setup.Team;
            switch (setup)
            {
                case CharacterSetup character:
                    f.Character = character;
                    // Sous-lot 8.1 : mêmes fonctions que la validation (précontrôle) : modificateurs agrégés, statistiques, règles.
                    ModifierSet modifiers = ModifierSet.For(character);
                    f.Stats = CombatantStats.ForCharacter(_setup.Stats, character.Build, modifiers);
                    f.Rules = CombatantRules.For(_rules, modifiers);
                    f.MaxHealth = f.Stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                    f.Health = f.MaxHealth;
                    f.MaxEndurance = f.Stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                    f.Endurance = f.MaxEndurance;
                    foreach (string skill in character.Build.ActiveSkills) f.Castable.Add(skill);
                    if (character.Build.Ultimate != null) f.Castable.Add(character.Build.Ultimate);
                    break;
                case DummySetup dummy:
                    f.Rules = _baseRules;
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                    break;
                case AssailantSetup assailant:
                    f.Assailant = assailant;
                    f.Rules = _baseRules;
                    // Sous-lot 11.1 : un assaillant mortel a des PV ; sans PV (null), il reste l'assaillant d'entraînement (A21).
                    f.MaxHealth = assailant.Health;
                    f.Health = assailant.Health;
                    f.Stats = CombatantStats.ForDefenses(_setup.Stats, assailant.Armor, assailant.MagicResistance, assailant.StatusResistance);
                    foreach (AssailantAttack attack in assailant.Attacks)
                        f.AttackStats[attack.Id] = CombatantStats.ForAssailantAttack(_setup.Stats, attack.RawDamage);
                    break;
            }
            return f;
        }

        /// <summary>Flux PCG32 propre à chaque combattant (FNV-1a 64 de son identifiant) : ajouter un combattant ne décale pas les tirages des autres.</summary>
        internal static ulong StreamOf(string id)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(id))
                unchecked { hash = (hash ^ b) * 1099511628211UL; }
            return hash;
        }

        /// <summary>Avance d'un tick. Renvoie faux quand le combat est terminé.</summary>
        public bool Step()
        {
            if (IsFinished) return false;
            long tick = CurrentTick;
            _eventsThisTick = 0;
            _workThisTick = 0;
            _scopeWork = _scopeEvents = _scopeEnqueues = 0;
            _countEvents = true;
            try
            {
                RunExpirations(tick);
                List<PendingAction> actions = RunCommands(tick);
                RunMovement(tick);   // sous-lot 11.4 : combat positionné seulement
                RunResolution(tick, actions);
                RunPeriodic(tick);
                RunRegeneration(tick);
                RunEndOfTick(tick);
                CloseScope();
            }
            catch (BudgetExceeded e)
            {
                Abort(tick, e.Reason, e.Limit);
            }
            catch (OverflowException)
            {
                Abort(tick, "overflow", null);
            }
            RecordTickPeaks();
            LastTickWork = _workThisTick;
            LastTickEvents = _eventsThisTick;
            if (!IsFinished) CurrentTick++;
            return !IsFinished;
        }

        // --- Phase 1 : expirations ---------------------------------------------------------------

        private void RunExpirations(long tick)
        {
            while (_queue.TryPeek(tick, CombatPhase.Expirations, out Scheduled item))
            {
                if (ProcessTriggerItem(tick, CombatPhase.Expirations, item)) continue;
                ReserveAndDequeue(Cost.Op + ExpirationCost(tick, item));
                Charge(1);
                switch (item)
                {
                    case CooldownExpiry expiry:
                        if (expiry.Fighter.CooldownReadyTick.TryGetValue(expiry.Skill, out long ready) && ready == tick)
                            Emit(tick, CombatPhase.Expirations, "cooldownReady", Provenance.NewRoot($"cooldown:{expiry.Fighter.Id}:{expiry.Skill}:{tick}", expiry.Fighter.Id),
                                w => w.Field("actor", expiry.Fighter.Id).Field("skill", expiry.Skill));
                        break;
                    case ParryWindowEnd end:
                        CloseParryWindow(tick, end);
                        break;
                    case StateExpiry state:
                        ExpireState(tick, state);
                        break;
                }
            }
        }

        /// <summary>
        /// Coût exact d'une échéance hors retrait : une ligne si elle agit, rien si elle est périmée ; une expiration d'altération
        /// ajoute le retrait de l'état et l'application éventuelle de l'état accordé (<see cref="StatusDefinition.GrantsOnExpiry"/>).
        /// </summary>
        private Cost ExpirationCost(long tick, Scheduled item) => item switch
        {
            CooldownExpiry e => Cost.Line.If(e.Fighter.CooldownReadyTick.TryGetValue(e.Skill, out long ready) && ready == tick),
            ParryWindowEnd e => Cost.Line.If(e.Fighter.ParryOpen && e.Fighter.ParryStartTick == e.StartTick),
            StateExpiry e => IsCurrent(e, out StatusInstance? status) ? EndStatusCost(tick, status) : Cost.None,
            _ => Cost.None,
        };

        private static bool IsCurrent(StateExpiry expiry, out StatusInstance status) =>
            expiry.Fighter.Statuses.TryGetValue(expiry.Key, out status!) && status.Id == expiry.Instance && status.Generation == expiry.Generation;

        private void CloseParryWindow(long tick, ParryWindowEnd end)
        {
            Fighter f = end.Fighter;
            if (!f.ParryOpen || f.ParryStartTick != end.StartTick) return;   // fenêtre déjà fermée (rupture, mort)
            f.ParryOpen = false;
            long? recoveryUntil = null;
            if (!f.ParrySucceeded)
            {
                f.ParryRecoveryUntilTick = checked(tick + f.Rules.ParryFailureRecoveryMs / _tickMs);
                recoveryUntil = f.ParryRecoveryUntilTick;
                f.Result.FailedParries++;
            }
            Emit(tick, CombatPhase.Expirations, "parryEnded", end.Origin, w => w
                .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "failed").Field("recoveryUntilTick", recoveryUntil));
        }

        private void ExpireState(long tick, StateExpiry expiry)
        {
            // Échéance périmée : état renouvelé (autre génération), retiré, ou remplacé par une nouvelle instance.
            if (!IsCurrent(expiry, out StatusInstance? status)) return;
            EndStatus(tick, CombatPhase.Expirations, expiry.Fighter, status);
        }

        // --- Phase 2 : commandes -----------------------------------------------------------------

        private List<PendingAction> RunCommands(long tick)
        {
            var batch = new List<(Fighter Actor, int Index, Scheduled Item)>();
            while (_queue.TryPeek(tick, CombatPhase.Commands, out Scheduled item))
            {
                ReserveAndDequeue(Cost.Op);
                Charge(1);
                if (item is CommandItem command) batch.Add((_byId[command.Command.Actor], command.Index, item));
                else batch.Add((((AssailantTurn)item).Fighter, -1, item));   // le tour d'un assaillant passe avant ses éventuelles commandes
            }

            var actions = new List<PendingAction>();
            // Tri stable : initiative décroissante, identifiant ordinal, puis ordre du scénario.
            foreach (var entry in batch
                .OrderByDescending(c => c.Actor.Setup.Initiative)
                .ThenBy(c => c.Actor.Id, StringComparer.Ordinal)
                .ThenBy(c => c.Index))
            {
                Reserve(entry.Item is CommandItem planned ? CommandCost(tick, planned) : AssailantTurnCost(tick, entry.Actor));
                PendingAction? action = entry.Item is CommandItem command ? Execute(tick, command) : AssailantAttacks(tick, (AssailantTurn)entry.Item);
                if (action != null) actions.Add(action);
            }
            DrainTriggers(tick, CombatPhase.Commands);
            return actions;
        }

        /// <summary>Coût exact d'une commande, calculé avec les mêmes contrôles que son exécution, sans rien modifier.</summary>
        private Cost CommandCost(long tick, CommandItem item)
        {
            Fighter actor = _byId[item.Command.Actor];
            switch (item.Command.Action)
            {
                case CombatAction.GuardStart:
                case CombatAction.GuardStop:
                case CombatAction.Move:   // sous-lot 11.4 : moveStarted, moveStopped ou refus
                case CombatAction.Stop:
                    return Cost.Line;
                case CombatAction.Parry:
                    return CheckParry(tick, actor) is null ? Cost.Enqueue + Cost.Line : Cost.Line;
                default:
                    var (skill, target, resonanceCost) = CastContext(item, actor);
                    if (CheckSkill(tick, actor, skill, target, resonanceCost) != null) return Cost.Line;
                    SkillCombatDefinition combat = skill!.Combat!;
                    return Cost.Enqueue.If(actor.Rules.CooldownMs(skill, combat) / _tickMs > 0) + Observable(TriggerEvent.SkillCast) + GainCost(actor, combat.RampartChargeGain)
                        + GainCost(actor, combat.ResonanceGain);   // sous-lot 8.4
            }
        }

        /// <summary>
        /// Coût exact du tour d'un assaillant : prochain tour s'il tombe avant la fin, et une ligne (<c>attackSkipped</c>, diagnostic,
        /// ou <c>attackStarted</c>, observable).
        /// </summary>
        private Cost AssailantTurnCost(long tick, Fighter assailant)
        {
            if (assailant.IsDead) return Cost.None;   // sous-lot 11.1 : tour périmé, abandonné sans ligne (voir AssailantAttacks)
            AssailantSetup setup = assailant.Assailant!;
            AssailantAttack attack = setup.Attacks[assailant.NextAttack];
            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? SingleTarget(assailant) : null;
            bool skipped = BlockReason(assailant, StatusBlocks.AssailantAttacks) != null || target is { IsDead: true };
            return Cost.Enqueue.If(checked(tick + setup.IntervalMs / _tickMs) < _durationTicks) + (skipped ? Cost.Line : Observable(TriggerEvent.SkillCast));
        }

        private (SkillDefinition? Skill, Fighter? Target, int ResonanceCost) CastContext(CommandItem item, Fighter actor)
        {
            CombatCommand command = item.Command;
            Fighter? target = command.Target != null ? _byId[command.Target] : null;
            SkillDefinition? skill = null;
            actor.Character?.Class.TryGetSkill(command.Skill!, out skill);
            bool isUltimate = skill != null && actor.Character?.Build.Ultimate == skill.Id;
            return (skill, target, isUltimate ? _rules.UltimateResonanceCost : 0);
        }

        /// <summary>Valide une commande sans rien modifier, puis seulement l'applique (refus atomique).</summary>
        private PendingAction? Execute(long tick, CommandItem item)
        {
            CombatCommand command = item.Command;
            Fighter actor = _byId[command.Actor];
            Provenance root = Provenance.NewRoot($"command:{item.Index}", actor.Id);
            switch (command.Action)
            {
                case CombatAction.GuardStart:
                    if (Reject(tick, item, actor, CheckGuardStart(tick, actor))) return null;
                    actor.Guarding = true;
                    Emit(tick, CombatPhase.Commands, "guardStarted", root, w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.GuardStop:
                    if (Reject(tick, item, actor, CheckAlive(actor) ?? (actor.Guarding ? null : "guardNotActive"))) return null;
                    actor.Guarding = false;
                    ResumeRegeneration(actor, tick);
                    Emit(tick, CombatPhase.Commands, "guardStopped", root, w => w.Field("actor", actor.Id).Field("command", item.Index).Field("endurance", actor.Endurance));
                    return null;

                case CombatAction.Parry:
                    if (Reject(tick, item, actor, CheckParry(tick, actor))) return null;
                    actor.ParryOpen = true;
                    actor.ParryStartTick = tick;
                    actor.ParryEndTick = checked(tick + actor.Rules.ParryWindowMs / _tickMs);   // ≥ 1 tick (CombatantRules)
                    actor.ParrySucceeded = false;
                    var end = new ParryWindowEnd { Fighter = actor, StartTick = tick };
                    Schedule(actor.ParryEndTick, CombatPhase.Expirations, end);
                    long started = Emit(tick, CombatPhase.Commands, "parryStarted", root, w => w
                        .Field("actor", actor.Id).Field("command", item.Index).Field("windowEndTick", actor.ParryEndTick).Field("guarding", actor.Guarding));
                    end.Origin = root.CausedBy(started);
                    return null;

                // Sous-lot 11.4 : intention de déplacement, appliquée par la phase « movement » de ce tick.
                case CombatAction.Move:
                    if (Reject(tick, item, actor, CheckMove(actor))) return null;
                    StartMoving(tick, item, actor, root);
                    return null;

                case CombatAction.Stop:
                    if (Reject(tick, item, actor, CheckMove(actor) ?? (actor.Body!.Moving ? null : "notMoving"))) return null;
                    StopMoving(tick, item, actor, root);
                    return null;

                default:
                    return CastSkill(tick, item, actor, root);
            }
        }

        private PendingAction? CastSkill(long tick, CommandItem item, Fighter actor, Provenance root)
        {
            CombatCommand command = item.Command;
            var (skill, target, resonanceCost) = CastContext(item, actor);

            if (Reject(tick, item, actor, CheckSkill(tick, actor, skill, target, resonanceCost))) return null;

            SkillCombatDefinition combat = skill!.Combat!;
            if (combat.EnduranceCost > 0) SpendEndurance(actor, combat.EnduranceCost, tick);
            actor.Resonance -= resonanceCost;
            // Sous-lot 8.4 (X8) : toutes les Charges détenues (au moins le coût, vérifié par CheckSkill) ou exactement le coût.
            int chargesSpent = combat.RampartChargesSpent(actor.RampartCharges);
            actor.RampartCharges -= chargesSpent;
            actor.GlobalCooldownReadyTick = checked(tick + _globalCooldownTicks);
            long cooldownTicks = actor.Rules.CooldownMs(skill, combat) / _tickMs;
            long readyTick = checked(tick + cooldownTicks);
            if (cooldownTicks > 0)
            {
                actor.CooldownReadyTick[skill.Id] = readyTick;
                Schedule(readyTick, CombatPhase.Expirations, new CooldownExpiry { Fighter = actor, Skill = skill.Id });
            }
            actor.Result.SkillsCast++;
            // Sous-lot 8.6 (X10) : la compétence de l'emplacement d'ultime, même à coût de Résonance nul (revue Codex de la PR #10).
            if (actor.Character?.Build.Ultimate == skill.Id) actor.Result.UltimatesCast++;
            long cast = Emit(tick, CombatPhase.Commands, "skillCast", root, new ObservedEvent { Kind = TriggerEvent.SkillCast, Source = actor.Id, Target = command.Target, Skill = skill.Id }, w => w
                .Field("actor", actor.Id).Field("skill", skill.Id).Field("target", command.Target).Field("command", item.Index)
                .Field("enduranceCost", combat.EnduranceCost).Field("endurance", actor.Endurance)
                .Field("resonanceCost", resonanceCost).Field("resonance", actor.Resonance)
                .Field("rampartChargeCost", chargesSpent).Field("rampartCharges", actor.RampartCharges)
                .Field("cooldownReadyTick", readyTick).Field("globalCooldownReadyTick", actor.GlobalCooldownReadyTick));
            Provenance consequences = root.CausedBy(cast);
            Gain(tick, CombatPhase.Commands, actor, RampartCharges, combat.RampartChargeGain, "skill", consequences);
            Gain(tick, CombatPhase.Commands, actor, Resonance, combat.ResonanceGain, "skill", consequences);   // sous-lot 8.4 (Provocation)
            return new PendingAction { Actor = actor, Skill = skill, Combat = combat, Target = target, GeneratesResonance = resonanceCost == 0, Origin = consequences,
                ChargesSpent = chargesSpent };
        }

        private bool Reject(long tick, CommandItem item, Fighter actor, string? reason)
        {
            if (reason is null) return false;
            CombatCommand command = item.Command;
            actor.Result.CommandsRejected++;
            EmitDiagnostic(tick, CombatPhase.Commands, "commandRejected", w => w
                .Field("actor", actor.Id).Field("action", CombatActionIds.Of(command.Action)).Field("skill", command.Skill)
                .Field("target", command.Target).Field("command", item.Index).Field("reason", reason));
            return true;
        }

        private static string? CheckAlive(Fighter actor)
        {
            if (actor.IsDead) return "actorDead";
            if (actor.Character is null) return "actorCannotAct";
            return null;
        }

        /// <summary>
        /// Lot 7.2 (D11) : motif de refus d'une action bloquée par une altération active, juste après les motifs de mort ; si
        /// plusieurs altérations bloquent l'action, la première dans l'ordre du catalogue l'emporte (étourdissement avant rupture).
        /// </summary>
        private string? BlockReason(Fighter f, StatusBlocks action)
        {
            StatusDefinition? first = null;
            foreach (StatusInstance status in f.Statuses.Values)
                if ((status.Definition.Blocks & action) != 0 && (first is null || _statusOrder[status.State] < _statusOrder[first.Id]))
                    first = status.Definition;
            return first?.BlockReason;
        }

        private string? CheckGuardStart(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Guard) is { } blocked) return blocked;
            if (actor.Guarding) return "guardAlreadyActive";
            return null;
        }

        private string? CheckParry(long tick, Fighter actor)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Parry) is { } blocked) return blocked;
            if (actor.ParryOpen) return "parryActive";
            if (tick < actor.ParryRecoveryUntilTick) return "parryRecovery";
            return null;
        }

        /// <summary>Motif de refus d'une compétence, dans un ordre fixe et documenté ; null si la commande est acceptée.</summary>
        private string? CheckSkill(long tick, Fighter actor, SkillDefinition? skill, Fighter? target, int resonanceCost)
        {
            if (CheckAlive(actor) is { } reason) return reason;
            if (BlockReason(actor, StatusBlocks.Skills) is { } blocked) return blocked;
            if (skill is null || !actor.Castable.Contains(skill.Id)) return "skillNotEquipped";
            if (skill.Combat is not { } combat) return "skillNotImplemented";
            if (actor.Guarding) return "guardActive";
            if (tick < actor.GlobalCooldownReadyTick) return "globalCooldown";
            if (actor.CooldownReadyTick.TryGetValue(skill.Id, out long ready) && tick < ready) return "cooldown";
            switch (combat.Targeting)
            {
                case SkillTargeting.Enemy:
                    if (target is null) return "targetRequired";
                    if (target.Team == actor.Team) return "targetNotEnemy";
                    if (target.IsDead) return "targetDead";
                    break;
                // Sous-lot 8.3 (X5) : un allié désigné, le lanceur compris ; un hostile ou un mort est refusé.
                case SkillTargeting.Ally:
                    if (target is null) return "targetRequired";
                    if (target.Team != actor.Team) return "targetNotAlly";
                    if (target.IsDead) return "targetDead";
                    break;
                default:   // tous les ennemis, soi, tous les alliés : aucune cible désignée
                    if (target != null) return "targetNotAllowed";
                    break;
            }
            if (actor.Endurance < combat.EnduranceCost) return "insufficientEndurance";
            if (actor.Resonance < resonanceCost) return "insufficientResonance";
            if (actor.RampartCharges < combat.RampartChargeCost) return "insufficientRampartCharges";
            return null;
        }

        /// <summary>Tour d'un assaillant : attaque suivante de sa liste, puis tour suivant programmé.</summary>
        private PendingAction? AssailantAttacks(long tick, AssailantTurn turn)
        {
            Fighter assailant = turn.Fighter;
            // Sous-lot 11.1 : la mort arrête les tours. Le tour déjà programmé est périmé, comme une échéance d'état retiré : il n'agit
            // pas, n'écrit rien et ne programme pas de suivant.
            if (assailant.IsDead) return null;
            AssailantSetup setup = assailant.Assailant!;
            AssailantAttack attack = setup.Attacks[assailant.NextAttack];
            assailant.NextAttack = (assailant.NextAttack + 1) % setup.Attacks.Count;
            long next = checked(tick + setup.IntervalMs / _tickMs);
            assailant.NextAttackTick = next < _durationTicks ? next : (long?)null;
            if (next < _durationTicks) Schedule(next, CombatPhase.Commands, new AssailantTurn { Fighter = assailant, Turn = assailant.NextTurn++ });

            Fighter? target = attack.Targeting == SkillTargeting.Enemy ? SingleTarget(assailant) : null;
            // Lot 7.2 : un assaillant sous contrôle saute son attaque ; l'attaque suivante de sa liste et sa cadence ne changent pas.
            if (BlockReason(assailant, StatusBlocks.AssailantAttacks) is { } blocked)
            {
                EmitDiagnostic(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id).Field("reason", blocked));
                return null;
            }
            if (target is { IsDead: true })
            {
                EmitDiagnostic(tick, CombatPhase.Commands, "attackSkipped", w => w
                    .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target.Id).Field("reason", "targetDead"));
                return null;
            }
            Provenance root = Provenance.NewRoot($"assailantTurn:{assailant.Id}:{turn.Turn}", assailant.Id);
            long started = Emit(tick, CombatPhase.Commands, "attackStarted", root, new ObservedEvent { Kind = TriggerEvent.SkillCast, Source = assailant.Id, Target = target?.Id, Skill = attack.Id }, w => w
                .Field("actor", assailant.Id).Field("attack", attack.Id).Field("target", target?.Id)
                .Field("rawDamage", attack.RawDamage).Field("unblockable", attack.Unblockable).Names("applies", attack.Applies.Select(a => a.Status)));
            return new PendingAction { Actor = assailant, Attack = attack, Target = target, GeneratesResonance = false, Origin = root.CausedBy(started) };
        }

        /// <summary>
        /// Sous-lot 8.3 (X7) : cible des attaques à cible unique d'un assaillant : la source de sa provocation la plus récente (dernière
        /// application ou renouvellement), sinon la cible du scénario. Une provocation est retirée à la mort de sa source : la source
        /// retenue est toujours vivante.
        /// </summary>
        private Fighter SingleTarget(Fighter assailant)
        {
            StatusInstance? taunt = null;
            foreach (StatusInstance status in assailant.Statuses.Values)
                if (status.Definition.IsTaunt && (taunt is null || status.AppliedSeq > taunt.AppliedSeq)) taunt = status;
            return taunt is null ? _byId[assailant.Assailant!.Target!] : _byId[taunt.Source!];
        }

        /// <summary>
        /// Sous-lot 8.3 (X5) : cibles d'une action, dans l'ordre de leurs identifiants pour un ciblage de groupe : la cible désignée
        /// (ennemi ou allié), le lanceur lui-même, ou tous les ennemis ou tous les alliés vivants (le lanceur compris).
        /// </summary>
        private IReadOnlyList<Fighter> ActionTargets(PendingAction action) => action.Targeting switch
        {
            SkillTargeting.Enemy or SkillTargeting.Ally => new[] { action.Target! },
            SkillTargeting.Self => new[] { action.Actor },
            SkillTargeting.AllAllies => _fightersById.Where(f => f.Team == action.Actor.Team && !f.IsDead).ToArray(),
            _ => _fightersById.Where(f => f.Team != action.Actor.Team && !f.IsDead).ToArray(),
        };

        /// <summary>Sous-lot 8.3 (X5) : cibles d'une application à cible propre, relatives à l'acteur, dans l'ordre de leurs identifiants.</summary>
        private IReadOnlyList<Fighter> OwnTargets(Fighter actor, ApplicationTarget target) => target switch
        {
            ApplicationTarget.Self => new[] { actor },
            ApplicationTarget.AllAllies => _fightersById.Where(f => f.Team == actor.Team && !f.IsDead).ToArray(),
            _ => _fightersById.Where(f => f.Team != actor.Team && !f.IsDead).ToArray(),
        };

        /// <summary>Postures (garde, parade) d'une cible laissées par les étapes déjà comptées d'une même action : coût exact des suivantes.</summary>
        private sealed class Postures : Dictionary<Fighter, (bool Guarding, bool ParryOpen)>
        {
            public (bool Guarding, bool ParryOpen) Of(Fighter f) => TryGetValue(f, out var p) ? p : (f.Guarding, f.ParryOpen);
        }

        private void RunResolution(long tick, List<PendingAction> actions)
        {
            foreach (PendingAction action in actions)
            {
                IReadOnlyList<Fighter> targets = ActionTargets(action);
                // A5 : une attaque directe réussie rapporte de la Résonance une seule fois, quel que soit le nombre de cibles. Sous-lot 8.3 :
                // une compétence de soutien n'a pas d'impact direct, donc pas de Résonance.
                bool damaging = action.Damaging;
                bool resonance = damaging && targets.Count > 0 && action.Actor.Character != null && action.GeneratesResonance;
                // Lot 7.1 : l'action entière (toutes ses cibles, et sous-lot 8.3 ses applications à cible propre) est réservée avant le
                // premier impact. Les cibles d'impact étant distinctes, l'impact sur l'une ne change pas le coût prévu pour les suivantes ;
                // les postures qu'une étape laisse à une cible sont reportées sur les étapes suivantes de la même action (Postures).
                var postures = new Postures();
                Cost cost = resonance ? GainCost(action.Actor, action.Actor.Rules.ResonancePerDirectHit) : Cost.None;
                foreach (Fighter target in targets)
                    cost += damaging ? HitCost(tick, action, target, postures) : SupportCost(tick, action, target, postures);
                var own = action.Applies.Where(a => a.Target != ApplicationTarget.Targets).Select(a => (Application: a, Targets: OwnTargets(action.Actor, a.Target))).ToList();
                foreach (var (application, ownTargets) in own)
                    foreach (Fighter target in ownTargets) cost += ApplicationCost(tick, action, application, target, postures);
                TraceReservation("action", tick, cost, removing: 0);
                Reserve(cost);
                foreach (Fighter target in targets)
                {
                    if (damaging) Hit(tick, action, target);
                    else Support(tick, action, target);
                }
                if (resonance) Gain(tick, CombatPhase.Resolution, action.Actor, Resonance, action.Actor.Rules.ResonancePerDirectHit, "directHit", action.Origin);
                foreach (var (application, ownTargets) in own)
                    foreach (Fighter target in ownTargets) Apply(tick, CombatPhase.Resolution, action, application, target, action.Origin);
            }
            // Lot 7.3 : puis les notifications et réactions de la phase, dans l'ordre de la file (réactions reportées du tick
            // précédent d'abord, puis celles de ce tick).
            DrainTriggers(tick, CombatPhase.Resolution);
        }

        /// <summary>
        /// Coût exact d'un impact, déduit de l'état de la cible avant l'impact, avec les mêmes branches que <see cref="Hit"/> :
        /// défense, puis chaque altération portée dans l'ordre, avec l'état de garde et de parade laissé par les étapes précédentes
        /// (une rupture de garde ou un étourdissement change ce que les suivantes interrompent).
        /// </summary>
        private Cost HitCost(long tick, PendingAction action, Fighter target, Postures? postures = null)
        {
            Cost cost = Cost.Op + Observable(TriggerEvent.HitDealt, TriggerEvent.HitTaken);   // impact, ligne « damage »
            // Sous-lot 8.3 (X6) : une protection active sur la cible transfère une part de l'impact : ligne « damageTransferred », non
            // notifiée, même si la part est nulle (le coût ne dépend jamais du montant des dégâts).
            cost += Cost.Line.If(ActiveProtection(target) != null);
            bool guarding = target.Guarding, parryOpen = target.ParryOpen;
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            bool parried = false;
            if (defending && action.Unblockable) { }
            else if (defending && target.ParryOpen)
            {
                parried = true;
                if (!target.ParrySucceeded)
                    cost += Observable(TriggerEvent.PerfectParry) + GainCost(target, _rules.RampartChargesPerParry) + GainCost(target, _rules.ResonancePerPerfectParry);
            }
            else if (defending)
            {
                if (GuardBreaksOnBlock(target))
                {
                    cost += BreakGuardCost(tick, target, action.Actor.Id, impact: true);
                    guarding = parryOpen = false;
                }
                else cost += Observable(TriggerEvent.Block) + GainCost(target, _rules.ResonancePerBlock);
            }
            foreach (StatusApplication application in action.TargetApplies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (parried)
                {
                    cost += Cost.Line;   // stateBlocked « parried »
                    continue;
                }
                StatusPlan plan = Plan(tick, target, definition, action.Actor.Id, null, action.Actor, shieldAmount: ShieldAmount(action.Actor, definition, application.Coefficient));
                cost += ApplyCost(plan, definition, guarding, parryOpen);
                if (plan.Applies)
                {
                    if ((definition.Interrupts & StatusInterrupts.Guard) != 0) guarding = false;
                    if ((definition.Interrupts & StatusInterrupts.Parry) != 0) parryOpen = false;
                }
            }
            if (postures != null) postures[target] = (guarding, parryOpen);
            return cost;
        }

        /// <summary>
        /// Sous-lot 8.3 (X5) : coût exact d'une compétence de soutien sur une cible : ni impact ni défense, chaque application portée
        /// dans l'ordre, avec les postures laissées par les précédentes.
        /// </summary>
        private Cost SupportCost(long tick, PendingAction action, Fighter target, Postures postures)
        {
            Cost cost = Cost.None;
            foreach (StatusApplication application in action.TargetApplies) cost += ApplicationCost(tick, action, application, target, postures);
            return cost;
        }

        /// <summary>Coût exact d'une application de l'action sur une cible (refus ou application et interruptions), postures reportées.</summary>
        private Cost ApplicationCost(long tick, PendingAction action, StatusApplication application, Fighter target, Postures postures)
        {
            StatusDefinition definition = _statuses[application.Status];
            var (guarding, parryOpen) = postures.Of(target);
            StatusPlan plan = Plan(tick, target, definition, action.Actor.Id, null, action.Actor, shieldAmount: ShieldAmount(action.Actor, definition, application.Coefficient));
            Cost cost = ApplyCost(plan, definition, guarding, parryOpen);
            if (plan.Applies)
            {
                if ((definition.Interrupts & StatusInterrupts.Guard) != 0) guarding = false;
                if ((definition.Interrupts & StatusInterrupts.Parry) != 0) parryOpen = false;
            }
            postures[target] = (guarding, parryOpen);
            return cost;
        }

        /// <summary>Sous-lot 8.3 (X5) : compétence de soutien sur une cible : ses applications, dans l'ordre, causées par le lancer.</summary>
        private void Support(long tick, PendingAction action, Fighter target)
        {
            foreach (StatusApplication application in action.TargetApplies) Apply(tick, CombatPhase.Resolution, action, application, target, action.Origin);
        }

        /// <summary>Application d'une altération portée par une action : source et auteur = l'acteur ; DOT à sa puissance, bouclier à ses PV max.</summary>
        private void Apply(long tick, CombatPhase phase, PendingAction action, StatusApplication application, Fighter target, Provenance origin)
        {
            StatusDefinition definition = _statuses[application.Status];
            Fixed power = definition.IsDot ? DotPower(AttackerStats(action), definition, application) : Fixed.Zero;
            ApplyStatus(tick, phase, target, definition, action.Actor.Id, power, origin, dealer: action.Actor,
                shieldAmount: ShieldAmount(action.Actor, definition, application.Coefficient));
        }

        /// <summary>
        /// Somme des bonus de dégâts directs reçus des altérations actives (catégorie « altération », A19) : chaque définition compte
        /// une fois, même portée par plusieurs sources, ce qui garde le majorant du précontrôle (somme du catalogue).
        /// </summary>
        private Fixed DamageTakenIncrease(Fighter target)
        {
            Fixed total = Fixed.Zero;
            HashSet<string>? seen = null;
            foreach (StatusInstance status in target.Statuses.Values)
                if (status.Definition.DamageTakenIncrease != Fixed.Zero && (seen ??= new HashSet<string>(StringComparer.Ordinal)).Add(status.State))
                    total += status.Definition.DamageTakenIncrease;
            return total;
        }

        private void Hit(long tick, PendingAction action, Fighter target) => Hit(tick, CombatPhase.Resolution, action, target);

        private void Hit(long tick, CombatPhase phase, PendingAction action, Fighter target)
        {
            Charge(1);
            Fighter actor = action.Actor;
            Provenance origin = action.Origin;
            long? parryEid = null, blockEid = null;
            bool critical = action.Combat != null && CriticalRoll.Roll(actor.Random, actor.Stats);   // l'assaillant ne critique jamais (aucun tirage)
            Fixed increase = DamageTakenIncrease(target);
            bool fragile = increase != Fixed.Zero;
            DamageBonuses bonuses = fragile ? DamageBonuses.None.Add(BonusCategory.Alteration, increase) : DamageBonuses.None;
            // Sous-lot 8.2 (X3) : dégâts infligés des renforcements actifs de l'attaquant, même catégorie « altération ».
            Fixed buffDealt = BuffSum(actor, ModifierField.DamageDealt);
            if (buffDealt != Fixed.Zero) bonuses = bonuses.Add(BonusCategory.Alteration, buffDealt);

            // Défense de la cible : parade (prioritaire) ou garde ; jamais les deux. Un impact imparable ignore l'une et l'autre.
            string defense = "none";
            Fixed mitigation = Fixed.Zero;
            bool parryReward = false, blockReward = false;
            bool defending = target.Character != null && !target.IsDead && (target.ParryOpen || target.Guarding);
            if (defending && action.Unblockable)
            {
                defense = "unblockable";
            }
            else if (defending && target.ParryOpen)
            {
                defense = "parry";
                mitigation = _rules.ParryDamageReduction;
                if (!target.ParrySucceeded)
                {
                    target.ParrySucceeded = true;   // récompense une seule fois par tentative
                    target.Result.PerfectParries++;
                    parryReward = true;
                    parryEid = Emit(tick, phase, "parrySucceeded", origin, HitObserved(TriggerEvent.PerfectParry, action, target, HitDefense.Parry), w => w.Field("actor", target.Id).Field("source", actor.Id));
                }
            }
            else if (defending)
            {
                if (GuardBreaksOnBlock(target))
                {
                    BreakGuard(tick, phase, target, "insufficientEndurance", actor.Id, origin, impact: true);
                    defense = "guardBroken";
                }
                else
                {
                    // Sous-lot 8.1 (guard.noUpkeepBreak) : sans endurance suffisante, la garde tient ; le combattant paie ce qui lui
                    // reste et la réduction est ramenée à guard.reductionAtZeroEndurance. Ligne « blocked » normale, coût réellement payé.
                    bool weakened = target.Endurance < target.Rules.GuardCostPerBlock;
                    long blockCost = weakened ? target.Endurance : target.Rules.GuardCostPerBlock;
                    if (!weakened || blockCost > 0) SpendEndurance(target, blockCost, tick);   // garde affaiblie sans endurance : pas une dépense
                    target.Result.Blocks++;
                    defense = "guard";
                    mitigation = weakened ? target.Rules.GuardReductionAtZeroEndurance : target.Rules.GuardDamageReduction;
                    blockReward = true;
                    blockEid = Emit(tick, phase, "blocked", origin, HitObserved(TriggerEvent.Block, action, target, HitDefense.Guard), w => w
                        .Field("actor", target.Id).Field("source", actor.Id).Field("enduranceCost", blockCost).Field("endurance", target.Endurance));
                }
            }

            // Sous-lot 8.1 (X1, X2, B2) : modificateurs de dégâts lus au calcul des dégâts, sur l'état courant (après la défense de cet
            // impact, avant ses gains), sans ligne ni notification. Dégâts subis du défenseur : réduction totale multiplicative
            // plafonnée à 85 %, hors parade ; dégâts infligés de l'attaquant : catégorie talent. Sans modificateur, rien ne change.
            // Sous-lot 8.2 (X3) : réduction temporaire des renforcements actifs du défenseur, sous le même plafond total.
            if (defense != "parry")
                mitigation = target.Rules.TotalReduction(mitigation,
                    target.Rules.DamageTakenAt(target.Guarding, target.RampartCharges, target.Resonance, BuffSum(target, ModifierField.DamageTaken)));
            Fixed dealt = actor.Rules.DamageDealtAt(actor.Guarding, actor.RampartCharges, actor.Resonance);
            if (dealt != Fixed.Zero) bonuses = bonuses.Add(BonusCategory.Talent, dealt);

            DerivedStats attacker = AttackerStats(action);
            DamageInput input = action.Combat is { } combat
                ? new DamageInput(attacker, target.Stats, combat.PowerType, combat.DamageType, actor.Rules.Coefficient(action.Skill!, combat, action.ChargesSpent), bonuses, critical, mitigation)
                : action.Attack is { } attack
                    ? new DamageInput(attacker, target.Stats, CombatantStats.PowerOf(attack.DamageType), attack.DamageType, Fixed.One, bonuses, false, mitigation)
                    // Lot 7.3 : frappe de réaction, sans critique ni tirage (Q6, Q7), puissance du porteur.
                    : new DamageInput(attacker, target.Stats, action.Strike!.PowerType!.Value, action.Strike.DamageType!.Value,
                        action.Strike.Coefficient!.Value.At(action.StrikeRank), bonuses, false, mitigation);
            long amount = DamageCalculator.Compute(input, _setup.Damage).FinalDamage;
            // Sous-lot 8.6 (X10) : dégâts atténués par la défense de cet impact (garde, parade, réductions de dégâts subis) = montant sans
            // réduction − montant ; même tirage critique, mêmes bonus. Hors journal ; préparé avant toute affectation.
            long mitigated = checked(target.Result.DamageMitigated + Unmitigated(input, mitigation, amount));

            // Correctif 7.3.1 (A73-M01) : nouvelles valeurs préparées et vérifiées avant toute affectation ; un débordement arrête
            // le combat sans impact à moitié appliqué. Sous-lot 8.2 (X4) : les boucliers absorbent après la défense, avant les PV.
            // Sous-lot 8.3 (X6) : une protection active transfère d'abord sa part au protecteur ; les boucliers de la cible absorbent le reste.
            Transfer transfer = PlanTransfer(target, amount);
            Absorption absorption = PlanAbsorption(target, amount - transfer.Amount);
            DamageUpdate update = PrepareDamage(target, actor, amount, absorption.Total, transfer.Amount);
            TransferUpdate received = PrepareTransfer(transfer);
            long overkill = update.Overkill;
            target.Health = update.Health;
            target.Result.DamageTaken = update.Taken;
            target.Result.DamageAbsorbed = update.Absorbed;
            target.Result.DamageTransferred = update.Transferred;
            target.Result.DamageMitigated = mitigated;
            actor.Result.DamageDealt = update.Dealt!.Value;
            actor.Result.Hits = update.Hits;
            if (critical) actor.Result.CriticalHits++;
            ApplyAbsorption(absorption);
            ApplyTransfer(transfer, received);
            ObservedEvent hit = HitObserved(TriggerEvent.HitDealt, action, target, TriggerIds.Defenses[defense]);
            hit.Also = TriggerEvent.HitTaken;
            long damage = Emit(tick, phase, "damage", origin, hit, w =>
            {
                w.Field("source", actor.Id).Field("target", target.Id).Field("skill", action.Skill?.Id).Field("attack", action.Attack?.Id)
                    .Field("critical", critical).Field("fragile", fragile).Field("defense", defense).Field("amount", amount);
                WriteAbsorption(w, absorption);
                w.Field("transferred", transfer.Amount);
                w.Field("overkill", overkill).Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken);
            });
            Provenance impact = origin.CausedBy(damage);
            NoteDepleted(absorption, impact);
            NoteLethal(target, impact);
            EmitTransfer(tick, phase, actor.Id, target, transfer, received, impact);

            if (parryReward)
            {
                Provenance parried = origin.CausedBy(parryEid!.Value).WithSource(target.Id);
                Gain(tick, phase, target, RampartCharges, _rules.RampartChargesPerParry, "perfectParry", parried);
                Gain(tick, phase, target, Resonance, _rules.ResonancePerPerfectParry, "perfectParry", parried);
            }
            if (blockReward) Gain(tick, phase, target, Resonance, _rules.ResonancePerBlock, "block", origin.CausedBy(blockEid!.Value).WithSource(target.Id));

            // Lot 7.2 : altérations portées, après les dégâts, dans l'ordre de la liste. Un DOT fige sa puissance maintenant.
            // Correctif 7.2 : un impact paré (parade parfaite, réussie à cet impact ou plus tôt dans la même fenêtre) n'applique
            // aucune altération (stateBlocked « parried ») ; un impact bloqué par la garde ou imparable les applique.
            foreach (StatusApplication application in action.TargetApplies)
            {
                StatusDefinition definition = _statuses[application.Status];
                if (defense == "parry")
                {
                    EmitDiagnostic(tick, phase, "stateBlocked", w => w
                        .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                        .Field("source", actor.Id).Field("reason", "parried"));
                    continue;
                }
                Fixed power = definition.IsDot ? DotPower(attacker, definition, application) : Fixed.Zero;
                ApplyStatus(tick, phase, target, definition, actor.Id, power, impact, dealer: actor,
                    shieldAmount: ShieldAmount(actor, definition, application.Coefficient));
            }
        }

        /// <summary>Sous-lot 8.6 (X10) : part d'un impact retirée par sa réduction (garde, parade, dégâts subis) ; 0 sans réduction.</summary>
        private long Unmitigated(DamageInput input, Fixed mitigation, long amount)
        {
            if (mitigation == Fixed.Zero) return 0;
            var raw = new DamageInput(input.Attacker, input.Defender, input.PowerType, input.DamageType, input.SkillCoefficient, input.Bonuses, input.IsCritical);
            return Math.Max(0, checked(DamageCalculator.Compute(raw, _setup.Damage).FinalDamage - amount));
        }

        /// <summary>Instantané d'un impact (§1.4) : auteur, cible, compétence ou attaque, type de dégâts, défense.</summary>
        private static ObservedEvent HitObserved(TriggerEvent kind, PendingAction action, Fighter target, HitDefense defense) => new ObservedEvent
        {
            Kind = kind,
            Source = action.Actor.Id,
            Target = target.Id,
            Skill = action.Skill?.Id ?? action.Attack?.Id,
            DamageType = action.DamageType,
            Defense = defense,
        };

        /// <summary>Statistiques offensives d'une action : celles du personnage, ou celles de l'attaque d'assaillant (dégâts bruts).</summary>
        private static DerivedStats AttackerStats(PendingAction action) =>
            action.Attack != null ? action.Actor.AttackStats[action.Attack.Id] : action.Actor.Stats;

        /// <summary>
        /// Puissance figée d'un DOT (A12) : puissance du type de l'altération (physique pour un saignement, magique pour une brûlure ;
        /// dégâts bruts pour un assaillant) × coefficient de l'application, arrondie à 4 décimales. Sans critique ni bonus (décision 4).
        /// </summary>
        private static Fixed DotPower(DerivedStats attacker, StatusDefinition definition, StatusApplication application) =>
            attacker.GetPower(CombatantStats.PowerOf(definition.DamageType!.Value)) * application.Coefficient!.Value;

        // --- Altérations (lot 7.2) ----------------------------------------------------------------

        /// <summary>Issue d'une application, calculée sans rien modifier ; sert au coût réservé et à l'exécution.</summary>
        private enum StatusOutcome { New, Renew, Immune, Resisted, Active, NotLonger, Empty, NotApplicable }

        private readonly struct StatusPlan
        {
            public readonly StatusOutcome Outcome;
            public readonly StatusKey Key;
            public readonly StatusInstance? Existing;
            public readonly long DurationTicks;

            public StatusPlan(StatusOutcome outcome, StatusKey key, StatusInstance? existing, long durationTicks)
            {
                Outcome = outcome;
                Key = key;
                Existing = existing;
                DurationTicks = durationTicks;
            }

            public bool Applies => Outcome == StatusOutcome.New || Outcome == StatusOutcome.Renew;

            public string Reason => Outcome switch
            {
                StatusOutcome.Immune => "immune",
                StatusOutcome.Resisted => "resisted",
                StatusOutcome.Active => "active",
                StatusOutcome.Empty => "empty",
                StatusOutcome.NotApplicable => "notApplicable",
                _ => "notLonger",
            };
        }

        /// <summary>
        /// Décide d'une application (cycle de vie, lot 7.2), dans cet ordre : immunité active (<c>immune</c>) ; durée réduite à
        /// 0 tick par la résistance (<c>resisted</c>, D7) ; nouvelle instance si la clé est libre ; sinon le mode de cumul :
        /// <c>refresh</c> et <c>perSourceKeepStronger</c> renouvellent, <c>keepLonger</c> renouvelle seulement si la nouvelle fin est
        /// plus tardive (<c>notLonger</c> sinon), <c>ignore</c> refuse (<c>active</c>). <paramref name="excluding"/> est une instance
        /// en cours de retrait, ignorée (expiration qui accorde un état). Sous-lot 8.2 : un bouclier de valeur nulle
        /// (<paramref name="shieldAmount"/>, source sans PV max ou coefficient nul) est refusé (<c>empty</c>), juste après l'immunité.
        /// </summary>
        private StatusPlan Plan(long tick, Fighter target, StatusDefinition definition, string? source, StatusInstance? excluding,
            Fighter? dealer, long shortenMs = 0, long shieldAmount = 0)
        {
            var key = new StatusKey(definition.Id, definition.Stacking == StatusStacking.PerSourceKeepStronger ? source : null);
            target.Statuses.TryGetValue(key, out StatusInstance? existing);
            if (existing == excluding) existing = null;
            // Sous-lot 8.3 (X6, X7) : une provocation ne vise qu'un assaillant, posée par un de ses ennemis vivants ; une protection, un
            // allié personnage autre que sa source vivante. Sinon : refus « notApplicable », avant tout autre motif.
            if (definition.IsSourceBound && !SourceFits(target, definition, source)) return new StatusPlan(StatusOutcome.NotApplicable, key, existing, 0);
            foreach (StatusInstance status in target.Statuses.Values)
                if (status != excluding && status.Definition.ImmuneTo.Contains(definition.Id))
                    return new StatusPlan(StatusOutcome.Immune, key, existing, 0);
            if (definition.IsShield && shieldAmount <= 0) return new StatusPlan(StatusOutcome.Empty, key, existing, 0);
            long ticks = DurationTicks(definition, target, dealer, shortenMs);
            if (ticks == 0) return new StatusPlan(StatusOutcome.Resisted, key, existing, 0);
            if (existing is null) return new StatusPlan(StatusOutcome.New, key, null, ticks);
            return definition.Stacking switch
            {
                StatusStacking.Ignore => new StatusPlan(StatusOutcome.Active, key, existing, ticks),
                StatusStacking.KeepLonger when checked(tick + ticks) <= existing.UntilTick => new StatusPlan(StatusOutcome.NotLonger, key, existing, ticks),
                _ => new StatusPlan(StatusOutcome.Renew, key, existing, ticks),
            };
        }

        /// <summary>Sous-lot 8.3 : la source d'une provocation ou d'une protection convient-elle à sa cible ?</summary>
        private bool SourceFits(Fighter target, StatusDefinition definition, string? source)
        {
            if (source is null || !_byId.TryGetValue(source, out Fighter? from) || from.IsDead) return false;
            return definition.IsTaunt
                ? target.Assailant != null && from.Team != target.Team
                : target.Character != null && from != target && from.Team == target.Team;
        }

        /// <summary>
        /// Durée en ticks d'une application (A14, A23) : durée de base, réduite pour un contrôle à résistance <c>duration</c> par le
        /// facteur r = K / (K + résistance aux altérations effective), calculé en <c>Fixed</c> arrondi vers le bas, puis
        /// ⌊ ticks × r ⌋. Ticks de base ≤ 86 400 000 et r brut ≤ 10⁴ : produit &lt; 10¹², sans débordement.
        /// <para>Sous-lot 8.1 : la durée de base reçoit d'abord, en ms, <c>status.&lt;id&gt;.durationDealt</c> de l'auteur d'une
        /// application directe (<paramref name="dealer"/>, null pour une rupture de garde ou un état accordé), <c>durationTaken</c> de la
        /// cible et le raccourci de rupture (<c>guard.breakShortenPerCharge</c>) ; le total est écrêté dans [1 tick, 24 h] (une période au
        /// moins pour un DOT), ce qui garde la borne ci-dessus. Les ajouts sont des multiples du tick (et de la période d'un DOT, par
        /// validation) et 24 h est un multiple du tick ; un DOT écrêté à 24 h peut ne plus être un multiple de sa période, et sa fin est
        /// alors ramenée au dernier tick périodique couvert (<see cref="DotEffectiveEnd"/>). Seule la résistance peut donner 0 tick
        /// (<c>resisted</c>).</para>
        /// </summary>
        private long DurationTicks(StatusDefinition definition, Fighter target, Fighter? dealer, long shortenMs)
        {
            long durationMs = definition.DurationMs;
            long adjust = checked(target.Rules.DurationTakenMs(definition.Id) + (dealer?.Rules.DurationDealtMs(definition.Id) ?? 0) - shortenMs);
            // Sous-lot 8.1 : durée modifiée écrêtée dans [1 tick (une période pour un DOT), 24 h] : un modificateur raccourcit, il
            // n'annule jamais (seule la résistance peut ramener une application à 0 tick, « resisted »).
            if (adjust != 0)
                durationMs = Math.Max(definition.IsDot ? definition.PeriodMs : _tickMs, Math.Min(CombatRules.AbsoluteMaxDurationMs, checked(durationMs + adjust)));
            long baseTicks = durationMs / _tickMs;
            if (definition.Resistance != StatusResistanceMode.Duration) return baseTicks;
            long constantRaw = checked(_setup.Stats.StatusResistanceConstant * Fixed.Scale);
            long factorRaw = IntMath.MulDiv(Fixed.Scale, constantRaw, checked(constantRaw + target.Stats.StatusResistance.Raw), RoundingMode.Floor);
            return IntMath.MulDiv(baseTicks, factorRaw, Fixed.Scale, RoundingMode.Floor);
        }

        /// <summary>
        /// Coût exact d'une application planifiée : refusée, une ligne <c>stateBlocked</c> ; sinon l'application (1), l'échéance ou le
        /// premier tick mis en file (sauf renouvellement d'un DOT, dont l'élément de file existe déjà), la ligne <c>stateApplied</c>,
        /// et une ligne par posture interrompue encore active.
        /// </summary>
        private Cost ApplyCost(StatusPlan plan, StatusDefinition definition, bool guarding, bool parryOpen)
        {
            if (!plan.Applies) return Cost.Line;
            bool requeue = !(definition.IsDot && plan.Outcome == StatusOutcome.Renew);
            return Cost.Op + Cost.Enqueue.If(requeue) + Observable(TriggerEvent.StateApplied)
                + Cost.Line.If(guarding && (definition.Interrupts & StatusInterrupts.Guard) != 0)
                + Cost.Line.If(parryOpen && (definition.Interrupts & StatusInterrupts.Parry) != 0);
        }

        /// <summary>
        /// Applique une altération (lot 7.2) selon <see cref="Plan"/>. Une instance nouvelle reçoit un identifiant jamais réutilisé ;
        /// un renouvellement garde l'instance et passe à la génération suivante, ce qui périme l'échéance précédente (M02). Un DOT
        /// renouvelé garde sa cadence et la plus forte puissance de sa source ; sa fin est repoussée (D4, décision 11). Les postures
        /// interrompues (garde, parade) le sont après la ligne <c>stateApplied</c>.
        /// </summary>
        private void ApplyStatus(long tick, CombatPhase phase, Fighter target, StatusDefinition definition, string? source, Fixed power,
            Provenance origin, StatusInstance? excluding = null, Fighter? dealer = null, long shortenMs = 0, long shieldAmount = 0)
        {
            StatusPlan plan = Plan(tick, target, definition, source, excluding, dealer, shortenMs, shieldAmount);
            if (!plan.Applies)
            {
                EmitDiagnostic(tick, phase, "stateBlocked", w => w
                    .Field("combatant", target.Id).Field("state", definition.Id).Field("category", StatusIds.Of(definition.Category))
                    .Field("source", source).Field("reason", plan.Reason));
                return;
            }
            Charge(1);
            bool renewed = plan.Outcome == StatusOutcome.Renew;
            StatusInstance status = plan.Existing ?? new StatusInstance { Id = ++_lastStatusInstance, Definition = definition, Key = plan.Key, Owner = target };
            if (!renewed)
            {
                target.Statuses[plan.Key] = status;
                status.Slots = StatusSlots(target, status);
            }
            status.Generation = checked(status.Generation + 1);
            status.Source = source;
            status.AppliedSeq = ++_lastApplication;   // sous-lot 8.3 : la provocation ou la protection la plus récente l'emporte
            if (definition.IsDot)
            {
                status.Power = renewed ? Fixed.Max(status.Power, power) : power;
                if (!renewed)
                {
                    status.NextTick = checked(tick + definition.PeriodMs / _tickMs);
                    Schedule(status.NextTick, CombatPhase.Periodic, new DotTick { Fighter = target, Key = plan.Key, Instance = status.Id });
                }
                status.UntilTick = DotEffectiveEnd(status.NextTick, checked(tick + plan.DurationTicks), definition);
            }
            else
            {
                status.UntilTick = checked(tick + plan.DurationTicks);
                Schedule(status.UntilTick, CombatPhase.Expirations,
                    new StateExpiry { Fighter = target, Key = plan.Key, Instance = status.Id, Generation = status.Generation });
                // Sous-lot 8.2 (X4, A7) : un bouclier par source ; un renouvellement garde la plus forte des valeurs (restante ou nouvelle)
                // et repart de maintenant + durée. Un bouclier épuisé non encore retiré repart ainsi de la nouvelle valeur.
                if (definition.IsShield)
                {
                    status.Absorb = renewed ? Math.Max(status.Absorb, shieldAmount) : shieldAmount;
                    status.DepletedBy = null;
                }
            }
            Provenance applying = origin.WithSource(source);
            long applied = Emit(tick, phase, "stateApplied", applying, StateObserved(TriggerEvent.StateApplied, target, status, renewed), w =>
            {
                StateFields(w, target, status).Field("untilTick", status.UntilTick).Field("renewed", renewed);
                if (definition.IsDot) w.Field("power", status.Power).Field("nextTick", status.NextTick);
                if (definition.IsShield) w.Field("absorb", status.Absorb);
                if (definition.IsProtection) w.Field("redirectRatio", _byId[status.Source!].Rules.RedirectRatio(definition));
                if (definition.MovementSpeedModifier != Fixed.Zero) w.Field("movementSpeedModifier", definition.MovementSpeedModifier);
            });
            status.Origin = applying.CausedBy(applied).ForInstance(status.Id, status.Generation);

            if ((definition.Interrupts & StatusInterrupts.Guard) != 0 && target.Guarding)
            {
                target.Guarding = false;
                ResumeRegeneration(target, tick);
                Emit(tick, phase, "guardStopped", status.Origin, w => w
                    .Field("actor", target.Id).Field("command", (long?)null).Field("endurance", target.Endurance)
                    .Field("reason", "interrupted").Field("state", definition.Id));
            }
            if ((definition.Interrupts & StatusInterrupts.Parry) != 0 && target.ParryOpen)
            {
                target.ParryOpen = false;
                Emit(tick, phase, "parryEnded", status.Origin, w => w
                    .Field("actor", target.Id).Field("outcome", target.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
        }

        /// <summary>
        /// Correctif 7.2 : fin effective d'un DOT, alignée sur sa cadence. C'est le dernier tick périodique (à partir du prochain
        /// tick programmé, de période en période) qui ne dépasse pas la fin demandée (application + durée) ; l'instance inflige ses
        /// derniers dégâts et expire à ce tick, en phase périodique. Une nouvelle application a toujours une fin alignée (durée
        /// multiple de la période) ; un renouvellement hors cadence voit sa fin ramenée au dernier tick qu'elle couvre.
        /// <c>stateApplied</c> journalise cette fin effective (<c>untilTick</c>) et le prochain tick (<c>nextTick</c>).
        /// </summary>
        private long DotEffectiveEnd(long nextTick, long requestedEnd, StatusDefinition definition)
        {
            long period = definition.PeriodMs / _tickMs;
            return checked(nextTick + (requestedEnd - nextTick) / period * period);
        }

        /// <summary>Instantané d'une ligne d'état (§1.4) : porteur (cible), source, altération, catégorie, instance, renouvellement.</summary>
        private static ObservedEvent StateObserved(TriggerEvent kind, Fighter target, StatusInstance status, bool renewed = false) => new ObservedEvent
        {
            Kind = kind,
            Source = status.Source,
            Target = target.Id,
            State = status.State,
            Category = status.Definition.Category,
            Instance = status.Id,
            DamageType = status.Definition.DamageType,
            Renewed = renewed,
        };

        /// <summary>Champs communs des lignes d'état (format 3) : cible, altération, catégorie, source, instance, génération.</summary>
        private static JsonLineWriter StateFields(JsonLineWriter w, Fighter target, StatusInstance status) => w
            .Field("combatant", target.Id).Field("state", status.State).Field("category", StatusIds.Of(status.Definition.Category))
            .Field("source", status.Source).Field("instance", status.Id).Field("generation", status.Generation);

        /// <summary>Coût de la fin d'une instance par expiration : retrait (1), ligne <c>stateExpired</c>, état accordé éventuel.</summary>
        private Cost EndStatusCost(long tick, StatusInstance status)
        {
            Cost cost = Cost.Op + Observable(TriggerEvent.StateExpired);
            Fighter target = status.Owner;
            if (status.Definition.GrantsOnExpiry is { } granted)
            {
                StatusDefinition definition = _statuses[granted];
                cost += ApplyCost(Plan(tick, target, definition, status.Source, status, null), definition, target.Guarding, target.ParryOpen);
            }
            return cost;
        }

        /// <summary>
        /// Fin d'une instance par expiration (phase 1, ou phase 4 pour le dernier tick d'un DOT) : retrait, ligne <c>stateExpired</c>,
        /// reprise de la régénération si l'altération la bloquait, puis l'état accordé (<c>stun</c> → <c>stunImmunity</c>, A14),
        /// avec la même source. Profondeur 1 : un état accordé n'accorde rien (validé dans le catalogue).
        /// </summary>
        private void EndStatus(long tick, CombatPhase phase, Fighter target, StatusInstance status)
        {
            target.Statuses.Remove(status.Key);
            Charge(1);
            long expired = Emit(tick, phase, "stateExpired", status.Origin, StateObserved(TriggerEvent.StateExpired, target, status), w => StateFields(w, target, status));
            if ((status.Definition.Blocks & StatusBlocks.Regeneration) != 0) ResumeRegeneration(target, tick);
            if (status.Definition.GrantsOnExpiry is { } granted)
                ApplyStatus(tick, phase, target, _statuses[granted], status.Source, Fixed.Zero, status.Origin.CausedBy(expired));
        }

        /// <summary>
        /// Réduction de garde d'un blocage à cet instant : affaiblie (<c>guard.reductionAtZeroEndurance</c>) quand la garde tient sans
        /// pouvoir payer le blocage (<c>guard.noUpkeepBreak</c>), pleine sinon. Même condition que le traitement d'un impact bloqué.
        /// </summary>
        private static Fixed GuardReductionNow(Fighter f) =>
            f.Rules.GuardNoUpkeepBreak && f.Endurance < f.Rules.GuardCostPerBlock ? f.Rules.GuardReductionAtZeroEndurance : f.Rules.GuardDamageReduction;

        /// <summary>Sous-lot 8.1 : la garde se brise-t-elle sur ce blocage ? Endurance insuffisante, sauf avec <c>guard.noUpkeepBreak</c>.</summary>
        private static bool GuardBreaksOnBlock(Fighter target) => target.Endurance < target.Rules.GuardCostPerBlock && !target.Rules.GuardNoUpkeepBreak;

        /// <summary>
        /// Sous-lot 8.1 (<c>guard.breakShortenPerCharge</c>) : Charges consommées par une rupture <b>par impact</b> (toutes, si le
        /// combattant a ce modificateur ; aucune sinon, ni pour l'entretien) ; lues avant la rupture, à l'identique pour le coût.
        /// </summary>
        private static int ChargesSpentByBreak(Fighter f, bool impact) => impact && f.Rules.GuardBreakShortenPerChargeMs > 0 ? f.RampartCharges : 0;

        private Cost BreakGuardCost(long tick, Fighter f, string? source, bool impact)
        {
            StatusDefinition definition = _statuses[_rules.GuardBreakStatus];
            long shortenMs = checked(ChargesSpentByBreak(f, impact) * f.Rules.GuardBreakShortenPerChargeMs);
            return Observable(TriggerEvent.GuardBroken) + Cost.Line.If(f.ParryOpen) + ApplyCost(Plan(tick, f, definition, source, null, null, shortenMs), definition, false, false);
        }

        /// <summary>
        /// Rupture de garde (A1, A19) : fin de la garde et de toute fenêtre de parade, puis l'altération de rupture
        /// (<c>combat.guard.breakStatus</c>) : ni garde ni parade ni régénération pendant sa durée. Sous-lot 8.1 : une rupture par
        /// impact consomme les Charges selon <see cref="ChargesSpentByBreak"/> (champ <c>rampartChargesSpent</c>) et raccourcit
        /// l'altération de <c>guard.breakShortenPerCharge</c> par Charge consommée.
        /// </summary>
        private void BreakGuard(long tick, CombatPhase phase, Fighter f, string reason, string? source, Provenance origin, bool impact)
        {
            int spent = ChargesSpentByBreak(f, impact);
            long shortenMs = checked(spent * f.Rules.GuardBreakShortenPerChargeMs);
            f.Guarding = false;
            f.RampartCharges -= spent;
            f.Result.GuardBreaks++;
            long broken = Emit(tick, phase, "guardBroken", origin, new ObservedEvent { Kind = TriggerEvent.GuardBroken, Source = source, Target = f.Id }, w => w
                .Field("actor", f.Id).Field("reason", reason).Field("endurance", f.Endurance).Field("rampartChargesSpent", spent));
            Provenance consequences = origin.CausedBy(broken);
            if (f.ParryOpen)
            {
                f.ParryOpen = false;
                Emit(tick, phase, "parryEnded", consequences, w => w
                    .Field("actor", f.Id).Field("outcome", f.ParrySucceeded ? "success" : "interrupted").Field("recoveryUntilTick", (long?)null));
            }
            ApplyStatus(tick, phase, f, _statuses[_rules.GuardBreakStatus], source, Fixed.Zero, consequences, shortenMs: shortenMs);
        }

        // --- Ressources --------------------------------------------------------------------------

        /// <summary>
        /// Coût d'un gain : une ligne s'il a lieu (montant positif, personnage), même si tout est perdu au plafond, et sa notification
        /// pour un gain naturel. Lot 7.3 (Q8) : un gain produit par une réaction <c>gainResource</c> est journalisé mais ne notifie
        /// pas <c>onResourceGained</c>, quelle que soit la profondeur.
        /// </summary>
        private Cost GainCost(Fighter f, long amount, bool observable = true) =>
            (observable ? Observable(TriggerEvent.ResourceGained) : Cost.Line).If(amount > 0 && f.Character != null);

        /// <summary>
        /// Gain plafonné d'une ressource (A4, A5) ; l'excédent est perdu et journalisé. Un gain ne déclenche jamais d'autre gain par
        /// les règles de base. Lot 7.3 : l'endurance n'est gagnée ainsi que par une réaction ; <paramref name="observable"/> faux pour
        /// un gain de réaction (Q8) : journalisé, jamais notifié.
        /// </summary>
        private void Gain(long tick, CombatPhase phase, Fighter f, string resource, long amount, string source, Provenance origin, bool observable = true)
        {
            if (amount <= 0 || f.Character is null) return;
            long before, max;
            TriggerResource kind;
            switch (resource)
            {
                case Resonance:
                    (before, max, kind) = (f.Resonance, _rules.ResonanceMax, TriggerResource.Resonance);
                    break;
                case RampartCharges:
                    (before, max, kind) = (f.RampartCharges, f.Rules.RampartChargesMax, TriggerResource.RampartCharges);
                    break;
                default:
                    (before, max, kind) = (f.Endurance, f.MaxEndurance, TriggerResource.Endurance);
                    break;
            }
            long after = Math.Min(max, checked(before + amount));
            switch (kind)
            {
                case TriggerResource.Resonance: f.Resonance = (int)after; break;
                case TriggerResource.RampartCharges: f.RampartCharges = (int)after; break;
                default: f.Endurance = after; break;
            }
            Emit(tick, phase, "resourceGained", origin, observable ? new ObservedEvent { Kind = TriggerEvent.ResourceGained, Source = f.Id, Resource = kind } : null, w => w
                .Field("actor", f.Id).Field("resource", resource).Field("source", source)
                .Field("amount", after - before).Field("wasted", before + amount - after).Field("value", after));
        }

        private void SpendEndurance(Fighter f, long amount, long tick)
        {
            f.Endurance -= amount;
            f.RegenFromTick = checked(tick + _regenDelayTicks);
            f.RegenGranted = 0;
        }

        /// <summary>
        /// Revue du 8.3 : retrait en fin de tick (bouclier épuisé, provocation ou protection dont la source meurt) d'une altération qui
        /// bloquait la régénération : comme à son expiration, rien n'est accordé au titre des ticks suspendus. Le retrait suit la phase de
        /// régénération du tick, restée bloquée : la reprise compte à partir du tick suivant.
        /// </summary>
        private static void ResumeAfterEndOfTickRemoval(Fighter owner, StatusInstance status, long tick)
        {
            if ((status.Definition.Blocks & StatusBlocks.Regeneration) != 0) ResumeRegeneration(owner, checked(tick + 1));
        }

        /// <summary>
        /// Fin d'une garde ou d'une rupture : la régénération reprend dès qu'une seconde complète s'est écoulée depuis la
        /// dernière dépense, mais aucun point n'est accordé au titre des ticks suspendus.
        /// </summary>
        private static void ResumeRegeneration(Fighter f, long tick)
        {
            if (f.RegenFromTick is long from && from < tick)
            {
                f.RegenFromTick = tick;
                f.RegenGranted = 0;
            }
        }

        // --- Phases 4 et 5 : effets périodiques, régénération ------------------------------------

        /// <summary>
        /// A1 : entretien de la garde. Un tick est gardé si la garde est active à cette phase (une garde prise puis quittée
        /// dans le même tick ne compte pas ; prise, quittée et reprise, elle compte une fois). L'entretien dû est
        /// ⌊ coût/s × ticks gardés depuis le début du combat × durée du tick / 1000 ⌋ : cumulé sur toutes les gardes du
        /// combat, il ne dépend que du nombre de ticks réellement gardés, quelle que soit leur répartition (M01).
        /// S'il ne peut être payé, la garde est brisée et ce tick n'est pas compté : le reliquat reste dû.
        /// <para>Sous-lot 8.1 : coût/s effectif du combattant (décimal, à 4 décimales, même arrondi par défaut). Avec
        /// <c>guard.noUpkeepBreak</c>, un entretien impayable ne brise pas la garde : le combattant paie ce qui lui reste, le tick est
        /// compté et le reste du dû est abandonné (ligne « guardUpkeep » avec le montant réellement payé, éventuellement 0).</para>
        /// </summary>
        private void RunPeriodic(long tick)
        {
            // Lot 7.2 : ticks de DOT d'abord, dans l'ordre de la file (ordre de programmation), puis l'entretien des gardes.
            while (_queue.TryPeek(tick, CombatPhase.Periodic, out Scheduled item))
            {
                if (ProcessTriggerItem(tick, CombatPhase.Periodic, item)) continue;
                var dot = (DotTick)item;
                ReserveAndDequeue(Cost.Op + DotTickCost(tick, dot));
                Charge(1);
                RunDotTick(tick, dot);
            }

            foreach (Fighter f in _fightersById)
            {
                if (!f.Guarding || f.IsDead)
                {
                    Reserve(Cost.Op);
                    Charge(1);
                    continue;
                }
                var (due, remainder) = UpkeepAfterTick(f);
                long amount = due - f.GuardUpkeepPaid;
                bool shortfall = amount > 0 && f.Endurance < amount;
                bool breaks = shortfall && !f.Rules.GuardNoUpkeepBreak;
                Reserve(Cost.Op + (breaks ? BreakGuardCost(tick, f, null, impact: false) : Cost.Line.If(amount > 0)));
                Charge(1);
                Provenance upkeep = Provenance.NewRoot($"periodic:{f.Id}:{f.Id}:{tick}", f.Id);
                if (breaks)
                {
                    BreakGuard(tick, CombatPhase.Periodic, f, "upkeep", null, upkeep, impact: false);
                    continue;
                }
                f.GuardUpkeepDue = due;
                f.GuardUpkeepRemainder = remainder;
                f.Result.GuardTicks++;   // sous-lot 8.6 (X10) : tick gardé (non brisé par l'entretien)
                if (amount <= 0) continue;
                long paid = shortfall ? f.Endurance : amount;
                if (paid > 0) SpendEndurance(f, paid, tick);   // rien à payer : pas une dépense (le délai de régénération ne repart pas)
                f.GuardUpkeepPaid = due;
                Emit(tick, CombatPhase.Periodic, "guardUpkeep", upkeep, w => w.Field("actor", f.Id).Field("amount", paid).Field("endurance", f.Endurance));
            }
            DrainTriggers(tick, CombatPhase.Periodic);
        }

        /// <summary>
        /// Entretien dû si ce tick est gardé, sans rien modifier : l'entretien dû et son reliquat avancent du coût/s effectif du moment
        /// × durée du tick (unités brutes × ms), le reliquat passant en points entiers par 1000 × échelle. À coût constant, l'entretien dû
        /// après n ticks gardés vaut exactement ⌊ coût/s × n × durée du tick / 1000 ⌋ (calcul des lots 7 et 8.1). Sous-lot 8.2 (X3) : le
        /// coût/s comprend les renforcements actifs du porteur (<c>guard.upkeepPerSecond</c>, écrêté à 0). Sans débordement : coût/s brut
        /// &lt; 2,2·10¹³, × durée du tick ≤ 1000 ms ; l'entretien dû cumulé ≤ coût/s × 24 h.
        /// </summary>
        private (long Due, long Remainder) UpkeepAfterTick(Fighter f)
        {
            const long unit = 1000 * Fixed.Scale;
            // Sous-lot 8.4 : un renforcement actif à guard.noUpkeep (Bastion absolu) annule l'entretien tant qu'il dure.
            Fixed rate = f.Rules.GuardUpkeepPerSecondWith(BuffSum(f, ModifierField.GuardUpkeepPerSecond), BuffSum(f, ModifierField.GuardNoUpkeep) > Fixed.Zero);
            long total = checked(f.GuardUpkeepRemainder + checked(rate.Raw * _tickMs));
            return (checked(f.GuardUpkeepDue + total / unit), total % unit);
        }

        private static bool IsCurrent(DotTick dot, out StatusInstance status) =>
            dot.Fighter.Statuses.TryGetValue(dot.Key, out status!) && status.Id == dot.Instance;

        /// <summary>Nouvelles valeurs d'un impact ou d'un tick de DOT, calculées sans rien modifier.</summary>
        private readonly struct DamageUpdate
        {
            public readonly long? Health;
            public readonly long Overkill;
            public readonly long Taken;
            public readonly long Absorbed;
            public readonly long Transferred;
            public readonly long? Dealt;
            public readonly long Hits;

            public DamageUpdate(long? health, long overkill, long taken, long absorbed, long transferred, long? dealt, long hits)
            {
                Health = health;
                Overkill = overkill;
                Taken = taken;
                Absorbed = absorbed;
                Transferred = transferred;
                Dealt = dealt;
                Hits = hits;
            }
        }

        /// <summary>
        /// Correctif 7.3.1 (A73-M01) : prépare dans des variables locales, avec opérations vérifiées, les PV de la cible, ses dégâts
        /// subis et les dégâts infligés de la source (et son compteur de coups, pour un impact direct). Toutes les opérations qui
        /// peuvent déborder sont faites ici ; l'appelant n'affecte qu'ensuite. Un débordement (configuration hors du précontrôle)
        /// arrête donc le combat (<c>overflow</c>) avant toute mutation de l'impact : ni PV, ni compteur, ni ligne partiels.
        /// <para>Sous-lot 8.2 (X4) : <paramref name="absorbed"/> (0 ≤ absorbé ≤ montant) est retenu par les boucliers de la cible : seul le
        /// reste atteint ses PV et ses dégâts subis ; les dégâts infligés de la source comptent le montant entier.</para>
        /// </summary>
        private static DamageUpdate PrepareDamage(Fighter target, Fighter? source, long amount, long absorbed, long transferred = 0)
        {
            long lost = checked(amount - transferred - absorbed);
            long? health = target.Health;
            long overkill = 0;
            if (health is long current)
            {
                overkill = Math.Max(0, checked(lost - current));
                health = Math.Max(0, checked(current - lost));
            }
            long taken = checked(target.Result.DamageTaken + lost);
            long totalAbsorbed = checked(target.Result.DamageAbsorbed + absorbed);
            long totalTransferred = checked(target.Result.DamageTransferred + transferred);
            long? dealt = source is null ? (long?)null : checked(source.Result.DamageDealt + amount);
            long hits = source is null ? 0 : checked(source.Result.Hits + 1);
            return new DamageUpdate(health, overkill, taken, totalAbsorbed, totalTransferred, dealt, hits);
        }

        // --- Protections et transferts (sous-lot 8.3) ---------------------------------------------

        /// <summary>
        /// Sous-lot 8.3 (X6) : protection en vigueur sur un combattant : la plus récemment appliquée (ou renouvelée) ; null sans
        /// protection. Sa source, retirée à sa mort avec elle, est toujours vivante.
        /// </summary>
        private static StatusInstance? ActiveProtection(Fighter target)
        {
            StatusInstance? best = null;
            foreach (StatusInstance status in target.Statuses.Values)
                if (status.Definition.IsProtection && (best is null || status.AppliedSeq > best.AppliedSeq)) best = status;
            return best;
        }

        /// <summary>Transfert planifié d'un impact ou d'un tick de DOT vers le protecteur, sans rien modifier.</summary>
        private readonly struct Transfer
        {
            public readonly StatusInstance? Protection;
            public readonly Fighter? Protector;
            public readonly Fixed Ratio;

            /// <summary>Part transférée, ⌊ montant × part ⌋ ; le protégé garde le reste (conservation à l'arrondi près).</summary>
            public readonly long Amount;

            /// <summary>Le transfert passe par la garde du protecteur (protection.redirectThroughGuard, protecteur en garde).</summary>
            public readonly bool Guarded;

            /// <summary>Part reçue par le protecteur après sa garde éventuelle, avant ses boucliers.</summary>
            public readonly long Taken;

            public readonly Absorption Absorption;

            public Transfer(StatusInstance protection, Fighter protector, Fixed ratio, long amount, bool guarded, long taken, Absorption absorption)
            {
                Protection = protection;
                Protector = protector;
                Ratio = ratio;
                Amount = amount;
                Guarded = guarded;
                Taken = taken;
                Absorption = absorption;
            }
        }

        /// <summary>
        /// Sous-lot 8.3 (X6) : part d'un montant de dégâts transférée par la protection en vigueur sur la cible : ⌊ montant × part ⌋, la
        /// part effective étant celle de la définition plus le <c>protection.redirectRatio</c> du protecteur, écrêtée dans [0, 1]. Le
        /// protecteur la reçoit sans ses modificateurs de dégâts subis ; avec <c>protection.redirectThroughGuard</c> et en garde, réduite
        /// par sa réduction de garde effective (sans coût, sans ligne ni notification de blocage) ; puis ses boucliers absorbent. Jamais
        /// retransférée. Revue du 8.3 : la réduction effective est celle d'un blocage au même instant, donc la réduction à 0 d'endurance
        /// quand la garde tient sans pouvoir payer un blocage (<c>guard.noUpkeepBreak</c>) ; sans ce drapeau, un transfert ne paie rien
        /// et ne brise pas la garde : réduction pleine.
        /// </summary>
        private Transfer PlanTransfer(Fighter target, long amount)
        {
            if (ActiveProtection(target) is not { } protection) return default;
            Fighter protector = _byId[protection.Source!];
            Fixed ratio = protector.Rules.RedirectRatio(protection.Definition);
            long part = amount <= 0 ? 0 : (Fixed.FromInt(amount) * ratio).RoundToInt(RoundingMode.Floor);
            bool guarded = protector.Rules.ProtectionRedirectThroughGuard && protector.Guarding;
            long taken = guarded ? (Fixed.FromInt(part) * (Fixed.One - GuardReductionNow(protector))).RoundToInt(RoundingMode.Floor) : part;
            return new Transfer(protection, protector, ratio, part, guarded, taken, PlanAbsorption(protector, taken));
        }

        /// <summary>Nouvelles valeurs du protecteur après un transfert, préparées et vérifiées avant toute affectation (A73-M01).</summary>
        private readonly struct TransferUpdate
        {
            public readonly long? Health;
            public readonly long Overkill;
            public readonly long Taken;
            public readonly long Absorbed;
            public readonly long Redirected;

            public TransferUpdate(long? health, long overkill, long taken, long absorbed, long redirected)
            {
                Health = health;
                Overkill = overkill;
                Taken = taken;
                Absorbed = absorbed;
                Redirected = redirected;
            }
        }

        private static TransferUpdate PrepareTransfer(Transfer transfer)
        {
            if (transfer.Protector is not { } protector) return default;
            long lost = checked(transfer.Taken - transfer.Absorption.Total);
            long? health = protector.Health;
            long overkill = 0;
            if (health is long current)
            {
                overkill = Math.Max(0, checked(lost - current));
                health = Math.Max(0, checked(current - lost));
            }
            return new TransferUpdate(health, overkill, checked(protector.Result.DamageTaken + lost),
                checked(protector.Result.DamageAbsorbed + transfer.Absorption.Total), checked(protector.Result.DamageRedirected + transfer.Amount));
        }

        private static void ApplyTransfer(Transfer transfer, TransferUpdate update)
        {
            if (transfer.Protector is not { } protector) return;
            protector.Health = update.Health;
            protector.Result.DamageTaken = update.Taken;
            protector.Result.DamageAbsorbed = update.Absorbed;
            protector.Result.DamageRedirected = update.Redirected;
            ApplyAbsorption(transfer.Absorption);
        }

        /// <summary>
        /// Ligne <c>damageTransferred</c> (format 7), causée par la ligne de dégâts du protégé, jamais notifiée (A1–A12 : ni réaction ni
        /// retransfert) : source des dégâts, protégé, protecteur, protection, part, part après la garde, absorption par les boucliers du
        /// protecteur, puis ses PV. Écrite dès qu'une protection est en vigueur, même pour une part nulle.
        /// </summary>
        private void EmitTransfer(long tick, CombatPhase phase, string? source, Fighter target, Transfer transfer, TransferUpdate update, Provenance cause)
        {
            if (transfer.Protector is not { } protector) return;
            StatusInstance protection = transfer.Protection!;
            Provenance transferred = cause.WithSource(protector.Id);
            long eid = Emit(tick, phase, "damageTransferred", transferred, w =>
            {
                w.Field("source", source).Field("protected", target.Id).Field("target", protector.Id).Field("state", protection.State)
                    .Field("instance", protection.Id).Field("ratio", transfer.Ratio).Field("amount", transfer.Amount)
                    .Field("defense", transfer.Guarded ? "guard" : "none").Field("taken", transfer.Taken);
                WriteAbsorption(w, transfer.Absorption);
                w.Field("overkill", update.Overkill).Field("health", protector.Health).Field("damageTaken", protector.Result.DamageTaken);
            });
            Provenance received = transferred.CausedBy(eid);
            NoteDepleted(transfer.Absorption, received);
            NoteLethal(protector, received);
        }


        // --- Renforcements et boucliers (sous-lot 8.2) ---------------------------------------------

        /// <summary>
        /// Sous-lot 8.2 (X3) : somme d'un champ des modificateurs des renforcements actifs d'un combattant (une instance au plus par
        /// définition : pas de cumul par source). Addition exacte, indépendante de l'ordre.
        /// </summary>
        private static Fixed BuffSum(Fighter f, ModifierField field)
        {
            Fixed total = Fixed.Zero;
            foreach (StatusInstance status in f.Statuses.Values)
                if (status.Definition.IsBuff) total += status.Definition.ModifierSum(field);
            return total;
        }

        /// <summary>Sous-lot 8.2 (X4) : valeur d'un bouclier posé par <paramref name="source"/> (0 hors bouclier ; voir <see cref="Shields.Value"/>).</summary>
        private static long ShieldAmount(Fighter source, StatusDefinition definition, Fixed? coefficient) =>
            definition.IsShield && coefficient is { } c ? Shields.Value(source.MaxHealth, c, source.Rules.ShieldPotencyDealt) : 0;

        /// <summary>Absorption planifiée d'un montant de dégâts : boucliers consommés, dans l'ordre, et part prise à chacun.</summary>
        private readonly struct Absorption
        {
            public readonly long Total;
            public readonly List<(StatusInstance Shield, long Taken)>? Takes;

            public Absorption(long total, List<(StatusInstance, long)>? takes)
            {
                Total = total;
                Takes = takes;
            }
        }

        /// <summary>
        /// Sous-lot 8.2 (X4, A6, A7) : absorption d'un impact ou d'un tick de DOT par les boucliers de la cible, sans rien modifier. Les
        /// boucliers non épuisés sont consommés dans l'ordre de leur création (numéro d'instance croissant : FIFO, un renouvellement
        /// garde sa place), chacun jusqu'à sa valeur restante ; le reste du montant atteint les PV.
        /// </summary>
        private static Absorption PlanAbsorption(Fighter target, long amount)
        {
            if (amount <= 0 || !HasAbsorb(target)) return default;   // chemin courant sans bouclier : ni tri ni allocation
            List<(StatusInstance, long)>? takes = null;
            long left = amount;
            foreach (StatusInstance shield in target.Statuses.Values.Where(x => x.Definition.IsShield && x.Absorb > 0).OrderBy(x => x.Id))
            {
                long taken = Math.Min(left, shield.Absorb);
                (takes ??= new List<(StatusInstance, long)>()).Add((shield, taken));
                left -= taken;
                if (left == 0) break;
            }
            return new Absorption(amount - left, takes);
        }

        private static bool HasAbsorb(Fighter target)
        {
            foreach (StatusInstance status in target.Statuses.Values)
                if (status.Definition.IsShield && status.Absorb > 0) return true;
            return false;
        }

        private static void ApplyAbsorption(Absorption absorption)
        {
            if (absorption.Takes is null) return;
            foreach (var (shield, taken) in absorption.Takes) shield.Absorb -= taken;
        }

        /// <summary>Un bouclier épuisé retient l'effet qui l'a épuisé : cause de son retrait en fin de tick.</summary>
        private static void NoteDepleted(Absorption absorption, Provenance effect)
        {
            if (absorption.Takes is null) return;
            foreach (var (shield, _) in absorption.Takes)
                if (shield.Absorb == 0 && shield.DepletedBy is null) shield.DepletedBy = effect;
        }

        /// <summary>
        /// Champs <c>absorbed</c> (total) et <c>absorbedBy</c> (format 6) : pour chaque bouclier consommé, son instance, son altération,
        /// sa source, la part absorbée et la valeur restante. Liste vide sans bouclier.
        /// </summary>
        private static void WriteAbsorption(JsonLineWriter w, Absorption absorption)
        {
            w.Field("absorbed", absorption.Total).Name("absorbedBy").BeginArray();
            if (absorption.Takes != null)
                foreach (var (shield, taken) in absorption.Takes)
                    w.BeginObject().Field("instance", shield.Id).Field("state", shield.State).Field("source", shield.Source)
                        .Field("amount", taken).Field("remaining", shield.Absorb).EndObject();
            w.EndArray();
        }

        /// <summary>Le tick en cours est-il le dernier de l'instance (le suivant dépasserait sa fin) ?</summary>
        private static bool IsLastDotTick(long tick, StatusInstance status) => tick >= status.UntilTick;

        /// <summary>
        /// Coût exact d'un tick de DOT hors retrait de la file : périmé (instance retirée à la mort), rien ; sinon l'impact (1) et la
        /// ligne <c>dotDamage</c>, puis soit le tick suivant mis en file, soit la fin de l'instance (retrait, ligne, état accordé).
        /// </summary>
        private Cost DotTickCost(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return Cost.None;
            return Cost.Op + Observable(TriggerEvent.DotTick) + Cost.Line.If(ActiveProtection(dot.Fighter) != null)   // 8.3 : transfert
                + (IsLastDotTick(tick, status) ? EndStatusCost(tick, status) : Cost.Enqueue);
        }

        /// <summary>
        /// Tick de DOT (A12, décisions 1, 2, 4) : dégâts = puissance figée réduite par la défense <b>actuelle</b> de la cible pour le
        /// type de l'altération (armure pour un saignement, résistance magique pour une brûlure), sans critique, ni garde, ni parade,
        /// ni Fragilité (dégâts directs seulement), ni Résonance, ni compteur de coups. Sous-lot 8.1 : réduits par les modificateurs de
        /// dégâts subis de la cible (<c>damageTaken</c>, plafond de 85 %), pas par ceux de dégâts infligés de la source. Crédités à la source même morte. Le dernier
        /// tick inflige ses dégâts puis expire l'instance dans la même étape : 6 s / 2 s donnent exactement 3 ticks.
        /// </summary>
        private void RunDotTick(long tick, DotTick dot)
        {
            if (!IsCurrent(dot, out StatusInstance? status)) return;
            Charge(1);
            Fighter target = dot.Fighter;
            StatusDefinition definition = status.Definition;
            // Sous-lot 8.1 : modificateurs de dégâts subis de la cible, lus sur son état au tick (réduction ≤ 85 %, sans garde ni
            // parade, A12). Sans modificateur, aucune réduction.
            // Sous-lot 8.2 (X3) : renforcements actifs de la cible compris, sous le même plafond total.
            Fixed mitigation = target.Rules.TotalReduction(Fixed.Zero,
                target.Rules.DamageTakenAt(target.Guarding, target.RampartCharges, target.Resonance, BuffSum(target, ModifierField.DamageTaken)));
            long amount = DamageCalculator.ComputePeriodic(status.Power, target.Stats, definition.DamageType!.Value,
                definition.Resistance == StatusResistanceMode.Defense, _setup.Damage, mitigation);
            // Sous-lot 8.6 (X10) : part retirée par les réductions de dégâts subis, préparée avant toute affectation.
            long mitigated = mitigation == Fixed.Zero ? target.Result.DamageMitigated : checked(target.Result.DamageMitigated + Math.Max(0, checked(
                DamageCalculator.ComputePeriodic(status.Power, target.Stats, definition.DamageType!.Value, definition.Resistance == StatusResistanceMode.Defense,
                    _setup.Damage, Fixed.Zero) - amount)));
            // Correctif 7.3.1 (A73-M01) : PV et compteurs préparés et vérifiés avant toute affectation (voir PrepareDamage). Sous-lot 8.2
            // (A6) : les boucliers de la cible absorbent aussi les DOT.
            Fighter? source = status.Source != null && _byId.TryGetValue(status.Source, out Fighter? s) ? s : null;
            // Sous-lot 8.3 (X6) : une protection transfère aussi sa part d'un tick de DOT.
            Transfer transfer = PlanTransfer(target, amount);
            Absorption absorption = PlanAbsorption(target, amount - transfer.Amount);
            DamageUpdate update = PrepareDamage(target, source, amount, absorption.Total, transfer.Amount);
            TransferUpdate received = PrepareTransfer(transfer);
            long overkill = update.Overkill;
            target.Health = update.Health;
            target.Result.DamageTaken = update.Taken;
            target.Result.DamageAbsorbed = update.Absorbed;
            target.Result.DamageTransferred = update.Transferred;
            target.Result.DamageMitigated = mitigated;
            if (source != null) source.Result.DamageDealt = update.Dealt!.Value;
            ApplyAbsorption(absorption);
            ApplyTransfer(transfer, received);
            ObservedEvent dotObserved = StateObserved(TriggerEvent.DotTick, target, status);
            long dotEid = Emit(tick, CombatPhase.Periodic, "dotDamage", status.Origin, dotObserved, w =>
            {
                w.Field("source", status.Source).Field("target", target.Id).Field("state", status.State).Field("instance", status.Id)
                    .Field("amount", amount);
                WriteAbsorption(w, absorption);
                w.Field("transferred", transfer.Amount);
                w.Field("overkill", overkill).Field("health", target.Health).Field("damageTaken", target.Result.DamageTaken);
            });
            Provenance dotImpact = status.Origin.CausedBy(dotEid);
            NoteDepleted(absorption, dotImpact);
            NoteLethal(target, dotImpact);
            EmitTransfer(tick, CombatPhase.Periodic, status.Source, target, transfer, received, dotImpact);
            if (IsLastDotTick(tick, status))
            {
                EndStatus(tick, CombatPhase.Periodic, target, status);
                return;
            }
            status.NextTick = checked(tick + definition.PeriodMs / _tickMs);
            Schedule(status.NextTick, CombatPhase.Periodic, new DotTick { Fighter = target, Key = status.Key, Instance = status.Id });
        }

        /// <summary>
        /// A3 : la régénération démarre après un délai complet sans dépense. Le tick t compte comme régénéré s'il commence
        /// au moins <c>délai</c> après la dernière dépense ; le total accordé est
        /// ⌊ régénération/s × ticks régénérés × durée du tick / 1000 ⌋, sans dérive ni flottant, plafonné au maximum.
        /// A1 : suspendue pendant la garde et sous une altération qui bloque la régénération (rupture de garde).
        /// </summary>
        private void RunRegeneration(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                long regenTicks = f.RegenFromTick is long from ? tick - from + 1 : 0;
                bool eligible = f.Character != null && !f.IsDead && f.RegenFromTick != null && !f.Guarding
                    && BlockReason(f, StatusBlocks.Regeneration) is null && regenTicks > 0;
                // Sous-lot 8.1 : régénération/s effective du combattant (décimale), même arrondi par défaut.
                long target = eligible
                    ? IntMath.MulDiv(f.Rules.EnduranceRegenPerSecond.Raw, checked(regenTicks * _tickMs), 1000 * Fixed.Scale, RoundingMode.Floor)
                    : 0;
                long gain = eligible ? Math.Min(target - f.RegenGranted, f.MaxEndurance - f.Endurance) : 0;
                Reserve(Cost.Op + Cost.Line.If(gain > 0));
                Charge(1);
                if (!eligible) continue;
                f.RegenGranted = target;
                if (gain > 0)
                {
                    f.Endurance += gain;
                    Emit(tick, CombatPhase.Regeneration, "enduranceRegenerated", Provenance.NewRoot($"regeneration:{f.Id}:{tick}", f.Id), w => w.Field("actor", f.Id).Field("amount", gain).Field("endurance", f.Endurance));
                }
                if (f.Endurance >= f.MaxEndurance) f.RegenFromTick = null;   // plein : plus rien à régénérer jusqu'à la prochaine dépense
            }
            DrainTriggers(tick, CombatPhase.Regeneration);
        }

        // --- Phase 6 : morts et fin de combat ----------------------------------------------------

        /// <summary>
        /// Phase 6 (lot 7.3, §5.4) : point fixe. Relevé des morts, puis notifications et réactions de la phase, puis nouveau relevé,
        /// jusqu'à ce que la phase soit vide ; la fin de combat est évaluée ensuite. Sans déclencheur, un seul relevé (inchangé).
        /// </summary>
        private void RunEndOfTick(long tick)
        {
            do ScanDeaths(tick);
            while (DrainTriggers(tick, CombatPhase.EndOfTick) > 0);

            var aliveTeams = _fighters.Where(f => !f.IsDead).Select(f => f.Team).Distinct(StringComparer.Ordinal).ToList();
            if (aliveTeams.Count <= 1)
                Finish(tick, CombatEndReason.TeamDefeated, null, aliveTeams.Count == 1 ? aliveTeams[0] : null);
            else if (tick >= _durationTicks - 1)
                Finish(tick, CombatEndReason.DurationReached, null, null);
        }

        private void ScanDeaths(long tick)
        {
            foreach (Fighter f in _fightersById)
            {
                bool dies = !f.IsDead && f.Health == 0;
                // Sous-lot 8.2 (X4) : un bouclier épuisé est retiré ici, en fin de tick (raison depleted), dans l'ordre des instances ; chez un
                // mort, il l'est avec les autres états (raison death).
                List<StatusInstance>? depleted = dies || f.IsDead ? null : Depleted(f);
                // Sous-lot 8.3 : à sa mort, ses provocations et protections posées sur les autres sont retirées (raison sourceDied).
                List<StatusInstance> bound = dies ? BoundTo(f) : new List<StatusInstance>();
                Cost scan = Cost.Op + (dies ? Observable(TriggerEvent.Death) + checked(f.Statuses.Count + bound.Count) * (Cost.Op + Observable(TriggerEvent.StateRemoved))
                    : (depleted?.Count ?? 0) * (Cost.Op + Observable(TriggerEvent.StateRemoved)));
                if (depleted != null) TraceReservation("depleted", tick, scan, removing: 0);
                Reserve(scan);
                Charge(1);
                if (depleted != null)
                    foreach (StatusInstance shield in depleted)
                    {
                        f.Statuses.Remove(shield.Key);
                        Charge(1);
                        ResumeAfterEndOfTickRemoval(f, shield, tick);
                        Provenance cause = shield.DepletedBy ?? shield.Origin;
                        Emit(tick, CombatPhase.EndOfTick, "stateRemoved", cause, StateObserved(TriggerEvent.StateRemoved, f, shield),
                            w => StateFields(w, f, shield).Field("reason", "depleted"));
                    }
                if (!dies) continue;
                f.IsDead = true;
                f.Result.DeathTick = tick;   // sous-lot 8.6 (X10)
                f.Guarding = false;
                f.ParryOpen = false;
                f.NextAttackTick = null;     // sous-lot 11.1 : un assaillant mort n'a plus de tour (vue du pilote, télégraphie)
                if (f.Body != null) f.Body.Moving = false;   // sous-lot 11.4 : un mort ne marche plus et n'est plus solide pour personne
                // A6 : provenance de l'effet mortel ; sans effet mortel (points de vie nuls dès le départ), racine propre.
                Provenance lethal = f.LethalCause ?? Provenance.NewRoot($"endOfTick:{f.Id}:{tick}", null);
                long died = Emit(tick, CombatPhase.EndOfTick, "died", lethal,
                    new ObservedEvent { Kind = TriggerEvent.Death, Source = lethal.Source, Target = f.Id }, w => w.Field("combatant", f.Id));
                // M02 : la mort retire les états actifs (ordre ordinal de l'altération, puis de la source) ; leurs échéances et
                // ticks deviennent périmés ; aucun état n'est accordé sur un retrait. Lot 7.3 : leurs déclencheurs disparaissent.
                foreach (StatusInstance status in f.Statuses.Values
                    .OrderBy(x => x.State, StringComparer.Ordinal).ThenBy(x => x.Source ?? string.Empty, StringComparer.Ordinal).ToList())
                {
                    f.Statuses.Remove(status.Key);
                    Charge(1);
                    Emit(tick, CombatPhase.EndOfTick, "stateRemoved", lethal.CausedBy(died), StateObserved(TriggerEvent.StateRemoved, f, status),
                        w => StateFields(w, f, status).Field("reason", "death"));
                }
                foreach (StatusInstance status in bound)
                {
                    status.Owner.Statuses.Remove(status.Key);
                    Charge(1);
                    ResumeAfterEndOfTickRemoval(status.Owner, status, tick);
                    Emit(tick, CombatPhase.EndOfTick, "stateRemoved", lethal.CausedBy(died), StateObserved(TriggerEvent.StateRemoved, status.Owner, status),
                        w => StateFields(w, status.Owner, status).Field("reason", "sourceDied"));
                }
            }
        }

        /// <summary>
        /// Sous-lot 8.3 : provocations et protections dont <paramref name="source"/> est la source, sur les autres combattants vivants, par
        /// identifiant de porteur puis par altération (ordre ordinal).
        /// </summary>
        private List<StatusInstance> BoundTo(Fighter source)
        {
            var bound = new List<StatusInstance>();
            foreach (Fighter f in _fightersById)
            {
                if (f == source || f.IsDead) continue;
                foreach (StatusInstance status in f.Statuses.Values.Where(x => x.Definition.IsSourceBound && x.Source == source.Id).OrderBy(x => x.State, StringComparer.Ordinal))
                    bound.Add(status);
            }
            return bound;
        }

        /// <summary>Sous-lot 8.2 : boucliers épuisés d'un combattant (valeur 0), par numéro d'instance ; null s'il n'y en a pas.</summary>
        private static List<StatusInstance>? Depleted(Fighter f)
        {
            List<StatusInstance>? depleted = null;
            foreach (StatusInstance status in f.Statuses.Values)
                if (status.Definition.IsShield && status.Absorb == 0) (depleted ??= new List<StatusInstance>()).Add(status);
            depleted?.Sort((a, b) => a.Id.CompareTo(b.Id));
            return depleted;
        }

        private void Abort(long tick, string reason, long? limit)
        {
            _countEvents = false;
            EmitDiagnostic(tick, CombatPhase.EndOfTick, "simulationAborted", w => w.Field("reason", reason).Field("limit", limit));
            Finish(tick, CombatEndReason.Aborted, reason, null);
        }

        /// <summary>
        /// Fin du combat : résumé de chaque combattant. Charges et Résonance appartiennent à cette instance de combat :
        /// elles sont remises à zéro ici et chaque combat repart de zéro (A4).
        /// </summary>
        private void Finish(long tick, CombatEndReason reason, string? abortReason, string? winner)
        {
            _countEvents = false;
            foreach (Fighter f in _fighters)
            {
                bool character = f.Character != null;
                f.Result.Health = f.Health;
                f.Result.MaxHealth = f.MaxHealth;
                f.Result.Endurance = character ? f.Endurance : (long?)null;
                f.Result.Resonance = character ? f.Resonance : (int?)null;
                f.Result.RampartCharges = character ? f.RampartCharges : (int?)null;
                f.Result.IsDead = f.IsDead;
                if (f.Body is { } body) f.Result.Position = Domain.Space.GroundVector.FromRaw(body.X, body.Z);   // sous-lot 11.4
                CombatantResult r = f.Result;
                Body? placed = f.Body;
                EmitDiagnostic(tick, CombatPhase.EndOfTick, "combatantSummary", w =>
                {
                    w.Field("combatant", r.Id).Field("team", r.Team).Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken)
                        .Field("damageAbsorbed", r.DamageAbsorbed).Field("damageTransferred", r.DamageTransferred).Field("damageRedirected", r.DamageRedirected)
                        .Field("hits", r.Hits).Field("criticalHits", r.CriticalHits).Field("skillsCast", r.SkillsCast)
                        .Field("commandsRejected", r.CommandsRejected).Field("blocks", r.Blocks).Field("perfectParries", r.PerfectParries)
                        .Field("failedParries", r.FailedParries).Field("guardBreaks", r.GuardBreaks)
                        .Field("health", r.Health).Field("endurance", r.Endurance)
                        .Field("resonance", r.Resonance).Field("rampartCharges", r.RampartCharges).Field("dead", r.IsDead);
                    if (placed != null) WriteVector(w, "position", placed.X, placed.Z);   // sous-lot 11.4 (format 10) : combat positionné
                });
                f.Resonance = 0;
                f.RampartCharges = 0;
            }
            long durationMs = checked((tick + 1) * _tickMs);
            string reasonId = reason switch
            {
                CombatEndReason.DurationReached => "durationReached",
                CombatEndReason.TeamDefeated => "teamDefeated",
                _ => "aborted",
            };
            EmitDiagnostic(tick, CombatPhase.EndOfTick, "combatEnded", w => w
                .Field("reason", reasonId).Field("winner", winner).Field("ticks", tick + 1).Field("durationMs", durationMs)
                .Field("deferredDropped", _deferredPending));
            RecordTickPeaks();
            Result = new CombatResult(reason, abortReason, tick, durationMs, winner, _fighters.Select(f => f.Result).ToArray(),
                new CombatLoadPeaks(_peakWork, _peakEvents, _peakPending));
        }

        // --- Journal -----------------------------------------------------------------------------

        private void LogCombatant(Fighter f)
        {
            DerivedStats s = f.Stats;
            bool character = f.Character != null;
            string type = character ? "character" : f.Assailant != null ? "assailant" : "dummy";
            EmitDiagnostic(0, CombatPhase.Setup, "combatant", w =>
            {
                w.Field("combatant", f.Id).Field("team", f.Team).Field("type", type)
                    .Field("initiative", f.Setup.Initiative).Field("health", f.Health).Field("maxHealth", f.MaxHealth)
                    .Field("endurance", character ? f.Endurance : (long?)null).Field("maxEndurance", character ? f.MaxEndurance : (long?)null)
                    .Field("resonance", character ? f.Resonance : (long?)null).Field("rampartCharges", character ? f.RampartCharges : (long?)null)
                    .Field("physicalPower", s.PhysicalPower).Field("agilePower", s.AgilePower).Field("magicPower", s.MagicPower)
                    .Field("armor", s.Armor).Field("magicResistance", s.MagicResistance).Field("statusResistance", s.StatusResistance)
                    .Field("criticalChanceBasisPoints", s.CriticalChanceBasisPoints).Field("criticalMultiplier", s.CriticalMultiplier);
                w.Name("rules");
                if (character) WriteRules(w, f);
                else w.Value((string?)null);
                // Sous-lot 11.4 (format 10) : place de départ, seulement dans un combat positionné.
                if (f.Body is { } body)
                {
                    WriteVector(w, "position", body.X, body.Z);
                    WriteVector(w, "facing", body.FacingX, body.FacingZ);
                    w.Field("radius", Fixed.FromRaw(body.Radius)).Field("speed", Fixed.FromRaw(body.Speed));
                }
            });
        }

        /// <summary>
        /// Sous-lot 8.1 (format 5) : règles effectives d'un personnage. Valeurs résultantes des clés de règle scalaires (les clés de
        /// statistiques, <c>endurance.max</c>, <c>maxHealth</c> et <c>statusResistance</c>, sont dans les champs du combattant) ; puis
        /// chaque clé paramétrée visée par un modificateur actif, dans l'ordre ordinal (coefficient et recharge effectifs pour une
        /// compétence, ajout en ms pour une durée d'altération) ; puis les modificateurs conditionnels agrégés (<c>conditional</c>).
        /// </summary>
        private static void WriteRules(JsonLineWriter w, Fighter f)
        {
            CombatantRules r = f.Rules;
            w.BeginObject()
                .Field("guard.damageReduction", r.GuardDamageReduction).Field("guard.upkeepPerSecond", r.GuardUpkeepPerSecond)
                .Field("guard.costPerBlock", r.GuardCostPerBlock).Field("guard.noUpkeepBreak", r.GuardNoUpkeepBreak).Field("guard.noUpkeep", r.GuardNoUpkeep)
                .Field("guard.reductionAtZeroEndurance", r.GuardReductionAtZeroEndurance).Field("guard.breakShortenPerCharge", r.GuardBreakShortenPerChargeMs)
                .Field("parry.windowMs", r.ParryWindowMs).Field("parry.failureRecoveryMs", r.ParryFailureRecoveryMs)
                .Field("rampartCharges.max", r.RampartChargesMax).Field("resonance.perDirectHit", r.ResonancePerDirectHit)
                .Field("endurance.regenPerSecond", r.EnduranceRegenPerSecond)
                .Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken).Field("shield.potencyDealt", r.ShieldPotencyDealt)
                .Field("protection.redirectRatio", r.ProtectionRedirectRatio).Field("protection.redirectThroughGuard", r.ProtectionRedirectThroughGuard);
            foreach (ModifierKey key in r.Modifiers.Keys)
            {
                switch (key.Field)
                {
                    case ModifierField.StatusDurationDealt:
                        w.Field(key.ToString(), r.DurationDealtMs(key.Parameter!));
                        break;
                    case ModifierField.StatusDurationTaken:
                        w.Field(key.ToString(), r.DurationTakenMs(key.Parameter!));
                        break;
                    case ModifierField.SkillCoefficient when f.Character!.Class.TryGetSkill(key.Parameter!, out SkillDefinition skill) && skill.Combat is { } combat:
                        w.Field(key.ToString(), r.Coefficient(skill, combat));
                        break;
                    case ModifierField.SkillCooldownMs when f.Character!.Class.TryGetSkill(key.Parameter!, out SkillDefinition skill) && skill.Combat is { } combat:
                        w.Field(key.ToString(), r.CooldownMs(skill, combat));
                        break;
                }
            }
            w.Name("conditional").BeginArray();
            foreach (ConditionalModifier c in r.Modifiers.Conditional)
            {
                w.BeginObject().Field("key", ModifierKeys.Specs[(int)c.Field].Name);
                w.Name("conditions").BeginArray();
                foreach (ModifierCondition condition in ModifierKeys.Of(c.Conditions)) w.Value(ModifierKeys.Of(condition));
                w.EndArray().Field("value", c.Value).EndObject();
            }
            w.EndArray().EndObject();
        }

        private static string PhaseId(CombatPhase phase) => phase switch
        {
            CombatPhase.Setup => "setup",
            CombatPhase.Expirations => "expirations",
            CombatPhase.Commands => "commands",
            CombatPhase.Movement => "movement",
            CombatPhase.Resolution => "resolution",
            CombatPhase.Periodic => "periodic",
            CombatPhase.Regeneration => "regeneration",
            _ => "endOfTick",
        };

        // --- Budgets (lot 7.1) ---------------------------------------------------------------------

        /// <summary>
        /// Réserve le coût exact du traitement suivant, avant toute mutation. Ordre de vérification fixe : travail, file,
        /// lignes ; l'arrêt porte la raison du premier budget qui échoue.
        /// </summary>
        private void Reserve(Cost cost) => Reserve(cost, cost);

        /// <summary>
        /// Lot 7.3 : réservation majorée. Les budgets sont vérifiés contre <paramref name="bound"/> (coût maximal du traitement
        /// entier, par exemple une réaction et tous ses effets), mais seule la partie exacte <paramref name="exact"/> est ouverte ;
        /// les réservations exactes suivantes du même traitement tiennent alors toujours dans les budgets.
        /// </summary>
        private void Reserve(Cost exact, Cost bound) => Reserve(exact, bound, removing: 0);

        /// <summary>
        /// Correctif 7.3.1 (A73-M04) : traitement d'un événement programmé, en tête de la file. Réserve son coût, puis le retire. Le
        /// retrait est certain dès que la réservation passe (aucune mutation ne la précède) : la place qu'il libère est créditée au
        /// contrôle de capacité, qui porte sur l'occupation nette (file − 1 + insertions) et non sur le nombre d'insertions. Seul point
        /// de retrait des événements programmés : expirations, commandes, ticks de DOT, notifications et réactions.
        /// </summary>
        private Scheduled ReserveAndDequeue(Cost exact, Cost bound)
        {
            Reserve(exact, bound, removing: 1);
            return _queue.Dequeue();
        }

        private Scheduled ReserveAndDequeue(Cost cost) => ReserveAndDequeue(cost, cost);

        /// <summary>Occupation de la file après le retrait certain de <paramref name="removing"/> élément(s) en tête.</summary>
        private long OccupancyAfter(int removing) => _queue.Count - removing;

        private void Reserve(Cost exact, Cost bound, int removing)
        {
            CloseScope();
            if (checked(_workThisTick + bound.Work) > _rules.MaxWorkPerTick) throw new BudgetExceeded("workBudgetExceeded", _rules.MaxWorkPerTick);
            if (checked(OccupancyAfter(removing) + bound.Queue) > _rules.MaxPendingEvents) throw new BudgetExceeded("queueLimitExceeded", _rules.MaxPendingEvents);
            if (checked(_eventsThisTick + bound.Events) > _rules.MaxEventsPerTick) throw new BudgetExceeded("eventBudgetExceeded", _rules.MaxEventsPerTick);
            Cost cost = exact;
            _scopeWork = _workThisTick + cost.Work;
            _scopeEvents = _eventsThisTick + cost.Events;
            _scopeEnqueues = cost.Queue;
        }

        /// <summary>
        /// Termine le traitement réservé. Un dépassement de la réserve lève aussitôt une erreur (voir <see cref="Charge"/>) ;
        /// en Debug, une réserve non entièrement consommée en lève une aussi, ce qui vérifie l'exactitude des coûts prévus
        /// sur toute la suite de tests.
        /// </summary>
        private void CloseScope()
        {
#if DEBUG
            if (_workThisTick != _scopeWork || _eventsThisTick != _scopeEvents || _scopeEnqueues != 0)
                throw new InvalidOperationException(
                    $"Coût réservé inexact : travail {_workThisTick}/{_scopeWork}, lignes {_eventsThisTick}/{_scopeEvents}, mises en file restantes {_scopeEnqueues}.");
#endif
            _scopeWork = _workThisTick;
            _scopeEvents = _eventsThisTick;
            _scopeEnqueues = 0;
        }

        private void Charge(long work)
        {
            if (!_countEvents) return;
            _workThisTick += work;
            if (_workThisTick > _scopeWork)
                throw new InvalidOperationException($"Travail consommé au-delà de la réserve ({_workThisTick} > {_scopeWork}).");
        }

        /// <summary>Met un élément en file ; pendant un tick, cela coûte une unité de travail et une place réservées.</summary>
        private void Schedule(long tick, CombatPhase phase, Scheduled item)
        {
            if (_countEvents)
            {
                Charge(1);
                if (--_scopeEnqueues < 0) throw new InvalidOperationException("Mise en file non réservée.");
            }
            _queue.Enqueue(tick, phase, item);
            if (_queue.Count > _peakPending) _peakPending = _queue.Count;
        }

        private void RecordTickPeaks()
        {
            if (_workThisTick > _peakWork) _peakWork = _workThisTick;
            if (_eventsThisTick > _peakEvents) _peakEvents = _eventsThisTick;
        }

        /// <summary>
        /// Lot 7.3 (A6) : retient le premier effet qui amène les points de vie d'un combattant vivant à 0 ; la mort, constatée en
        /// phase 6, en hérite. Les impacts suivants du même tick sur un combattant déjà à 0 ne changent rien.
        /// </summary>
        private static void NoteLethal(Fighter target, Provenance impact)
        {
            if (target.Health == 0 && !target.IsDead && target.LethalCause is null) target.LethalCause = impact;
        }

        /// <summary>
        /// Écrit une ligne de jeu (lot 7.3) : un effet, avec son identité <c>eid</c> (compteur du combat, dans l'ordre de création,
        /// distinct de <c>seq</c>) et sa provenance compacte : <c>cause</c> toujours (null pour l'effet racine), <c>root</c> sur l'effet
        /// racine seulement, <c>depth</c> et <c>trigger</c> à partir de la profondeur 1. Renvoie l'eid, cause de ses conséquences.
        /// </summary>
        private long Emit(long tick, CombatPhase phase, string kind, Provenance origin, Action<JsonLineWriter> fields) =>
            Emit(tick, phase, kind, origin, null, fields);

        /// <summary>
        /// Variante pour un effet observable (lot 7.3) : après la ligne, l'instantané reçoit l'eid et la provenance, et sa notification
        /// est mise en file en fin de la phase courante si son type a un abonné possible (coût réservé par <see cref="Observable"/>).
        /// </summary>
        private long Emit(long tick, CombatPhase phase, string kind, Provenance origin, ObservedEvent? observed, Action<JsonLineWriter> fields)
        {
            long eid = ++_lastEid;
            WriteLine(tick, phase, kind, w =>
            {
                w.Field("eid", eid).Field("cause", origin.Cause);
                if (origin.Cause is null) w.Field("root", origin.Root);
                if (origin.Depth > 0) w.Field("depth", origin.Depth).Field("trigger", origin.Trigger);
                if (_deferredFrom is long from) w.Field("deferredFromTick", from);
                fields(w);
            });
            if (observed != null && Subscribed(observed))
            {
                observed.Eid = eid;
                observed.Tick = tick;
                observed.Phase = phase;
                observed.Provenance = origin;
                observed.Candidates = Candidates(observed);
                Schedule(tick, phase, new NotifyItem { Event = observed });
            }
            return eid;
        }

        /// <summary>
        /// Écrit un diagnostic ou une ligne d'enveloppe (lot 7.3, §1.1) : ni eid ni provenance, jamais présenté à un abonné. Une
        /// méthode distincte de <see cref="Emit"/> garantit qu'aucun diagnostic ne devient un effet observable.
        /// </summary>
        private void EmitDiagnostic(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields) => WriteLine(tick, phase, kind, fields);

        private void WriteLine(long tick, CombatPhase phase, string kind, Action<JsonLineWriter> fields)
        {
            if (_countEvents)
            {
                Charge(1);
                if (++_eventsThisTick > _scopeEvents)
                    throw new InvalidOperationException($"Ligne écrite au-delà de la réserve ({_eventsThisTick} > {_scopeEvents}).");
            }
            var w = new JsonLineWriter().BeginObject()
                .Field("tick", tick).Field("seq", _sequence++).Field("phase", PhaseId(phase)).Field("event", kind);
            fields(w);
            _log.Append(w.EndObject().ToString());
        }
    }
}
