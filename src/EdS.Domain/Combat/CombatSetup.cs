using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    // Description d'un combat (participants, commandes, règles, identité des données), consommée par le moteur
    // d'EdS.Simulation. Placée dans le domaine pour que la couche de données construise un combat sans dépendre
    // du moteur (lot 6).

    /// <summary>Participant d'un combat. Les identifiants et équipes sont en minuscules (<c>bastion</c>, <c>joueurs</c>).</summary>
    public abstract class CombatantSetup
    {
        public string Id { get; }
        public string Team { get; }

        /// <summary>Ordre des commandes simultanées : la plus grande initiative agit d'abord, puis l'identifiant (ordinal).</summary>
        public int Initiative { get; }

        protected CombatantSetup(string id, string team, int initiative)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Team = team ?? throw new ArgumentNullException(nameof(team));
            Initiative = initiative;
        }
    }

    /// <summary>Personnage joué : sa classe et son build (attributs explicites, talents, compétences équipées).</summary>
    public sealed class CharacterSetup : CombatantSetup
    {
        public ClassDefinition Class { get; }
        public CharacterBuild Build { get; }

        public CharacterSetup(string id, string team, ClassDefinition definition, CharacterBuild build, int initiative = 0)
            : base(id, team, initiative)
        {
            Class = definition ?? throw new ArgumentNullException(nameof(definition));
            Build = build ?? throw new ArgumentNullException(nameof(build));
        }
    }

    /// <summary>Mannequin d'entraînement : PV infinis, défenses configurables, n'agit jamais.</summary>
    public sealed class DummySetup : CombatantSetup
    {
        public Fixed Armor { get; }
        public Fixed MagicResistance { get; }

        /// <summary>Résistance aux altérations (lot 7.2, D12) : réduit la durée des contrôles ; 0 par défaut.</summary>
        public Fixed StatusResistance { get; }

        public DummySetup(string id, string team, Fixed armor, Fixed magicResistance, int initiative = 0, Fixed statusResistance = default)
            : base(id, team, initiative)
        {
            Armor = armor;
            MagicResistance = magicResistance;
            StatusResistance = statusResistance;
        }
    }

    /// <summary>Attaque d'un assaillant d'entraînement : dégâts bruts (avant défense), sans critique.</summary>
    public sealed class AssailantAttack
    {
        public string Id { get; }

        /// <summary>Dégâts bruts avant armure ou résistance magique (A21 : 100 en référence).</summary>
        public long RawDamage { get; }

        public DamageType DamageType { get; }

        /// <summary><see cref="SkillTargeting.Enemy"/> : la cible de l'assaillant ; <see cref="SkillTargeting.AllEnemies"/> : tous les ennemis vivants.</summary>
        public SkillTargeting Targeting { get; }

        /// <summary>Imparable : ignore garde et parade.</summary>
        public bool Unblockable { get; }

        /// <summary>
        /// Altérations appliquées (ou renouvelées) sur chaque cible touchée, après les dégâts de l'impact (lot 7.2 ; remplace le
        /// booléen <c>appliesFragility</c>). Pour un DOT, la puissance de l'assaillant est ses dégâts bruts.
        /// </summary>
        public IReadOnlyList<StatusApplication> Applies { get; }

        public AssailantAttack(string id, long rawDamage, DamageType damageType = DamageType.Physique,
            SkillTargeting targeting = SkillTargeting.Enemy, bool unblockable = false, IEnumerable<StatusApplication>? applies = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RawDamage = rawDamage;
            DamageType = damageType;
            Targeting = targeting;
            Unblockable = unblockable;
            Applies = Frozen.ListOrEmpty(applies, nameof(applies));
        }
    }

    /// <summary>
    /// Assaillant d'entraînement (A21) : PV infinis, n'obéit à aucune commande, attaque à intervalle fixe en enchaînant
    /// ses attaques dans l'ordre (puis recommence). Première attaque à <see cref="FirstAttackMs"/>.
    /// </summary>
    public sealed class AssailantSetup : CombatantSetup
    {
        public Fixed Armor { get; }
        public Fixed MagicResistance { get; }

        /// <summary>Résistance aux altérations (lot 7.2, D12) : réduit la durée des contrôles ; 0 par défaut.</summary>
        public Fixed StatusResistance { get; }

        /// <summary>Cible des attaques <see cref="SkillTargeting.Enemy"/> ; null si toutes ses attaques sont de zone.</summary>
        public string? Target { get; }

        public long FirstAttackMs { get; }
        public long IntervalMs { get; }
        public IReadOnlyList<AssailantAttack> Attacks { get; }

        public AssailantSetup(string id, string team, Fixed armor, Fixed magicResistance, string? target, long firstAttackMs, long intervalMs,
            IEnumerable<AssailantAttack> attacks, int initiative = 0, Fixed statusResistance = default)
            : base(id, team, initiative)
        {
            Armor = armor;
            MagicResistance = magicResistance;
            StatusResistance = statusResistance;
            Target = target;
            FirstAttackMs = firstAttackMs;
            IntervalMs = intervalMs;
            Attacks = Frozen.List(attacks, nameof(attacks));
        }
    }

    /// <summary>Nature d'une commande.</summary>
    public enum CombatAction
    {
        /// <summary>Lancer une compétence équipée.</summary>
        UseSkill = 0,

        /// <summary>Prendre la posture de garde (A1).</summary>
        GuardStart = 1,

        /// <summary>Quitter la garde.</summary>
        GuardStop = 2,

        /// <summary>Tenter une parade parfaite (A2).</summary>
        Parry = 3,
    }

    /// <summary>Identifiants stables des actions dans les fichiers et le journal.</summary>
    public static class CombatActionIds
    {
        public const string UseSkill = "skill";
        public const string GuardStart = "guardStart";
        public const string GuardStop = "guardStop";
        public const string Parry = "parry";

        /// <summary>Actions qu'on écrit dans le champ <c>action</c> d'une commande (la compétence s'écrit avec <c>skill</c>).</summary>
        public static IReadOnlyList<string> Defensive { get; } = Frozen.List(new[] { GuardStart, GuardStop, Parry }, nameof(Defensive));

        public static string Of(CombatAction action) => action switch
        {
            CombatAction.UseSkill => UseSkill,
            CombatAction.GuardStart => GuardStart,
            CombatAction.GuardStop => GuardStop,
            CombatAction.Parry => Parry,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        public static bool TryParseDefensive(string? id, out CombatAction action)
        {
            switch (id)
            {
                case GuardStart: action = CombatAction.GuardStart; return true;
                case GuardStop: action = CombatAction.GuardStop; return true;
                case Parry: action = CombatAction.Parry; return true;
                default: action = default; return false;
            }
        }
    }

    /// <summary>Commande horodatée en millisecondes depuis le début du combat : compétence, garde ou parade.</summary>
    public sealed class CombatCommand
    {
        public long AtMs { get; }
        public string Actor { get; }
        public CombatAction Action { get; }

        /// <summary>Compétence lancée ; null pour une garde ou une parade.</summary>
        public string? Skill { get; }

        /// <summary>Cible désignée ; null pour une compétence qui choisit ses cibles (zone), une garde ou une parade.</summary>
        public string? Target { get; }

        /// <summary>Commande « utiliser une compétence ».</summary>
        public CombatCommand(long atMs, string actor, string skill, string? target = null)
        {
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = CombatAction.UseSkill;
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Target = target;
        }

        /// <summary>Commande de garde ou de parade (sans compétence ni cible).</summary>
        public CombatCommand(long atMs, string actor, CombatAction action)
        {
            if (action == CombatAction.UseSkill) throw new ArgumentException("Une compétence se lance avec le constructeur qui la nomme.", nameof(action));
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Action = action;
        }
    }

    /// <summary>Identité d'un fichier de données utilisé (version de contenu et empreinte).</summary>
    public sealed class DataFileIdentity
    {
        public string Id { get; }
        public string ContentVersion { get; }
        public string Fingerprint { get; }

        public DataFileIdentity(string id, string contentVersion, string fingerprint)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        }
    }

    /// <summary>
    /// Identité des données d'un combat, inscrite dans l'en-tête du journal : un journal n'est rejouable
    /// qu'avec des données de même empreinte.
    /// </summary>
    public sealed class CombatDataIdentity
    {
        public DataFileIdentity Rules { get; }

        /// <summary>Classes utilisées, triées par identifiant.</summary>
        public IReadOnlyList<DataFileIdentity> Classes { get; }

        public CombatDataIdentity(DataFileIdentity rules, IEnumerable<DataFileIdentity> classes)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Classes = Frozen.List((classes ?? throw new ArgumentNullException(nameof(classes))).OrderBy(c => c.Id, StringComparer.Ordinal), nameof(classes));
        }
    }

    /// <summary>
    /// Tout ce qui définit un combat : règles, participants, commandes, graine et identité des données.
    /// Immuable ; le moteur n'en modifie rien, si bien qu'un même objet peut servir à plusieurs combats.
    /// </summary>
    public sealed class CombatSetup
    {
        public long Seed { get; }
        public long DurationMs { get; }
        public StatRules Stats { get; }
        public DamageRules Damage { get; }
        public ProgressionRules Progression { get; }
        public CombatRules Combat { get; }
        public IReadOnlyList<CombatantSetup> Combatants { get; }
        public IReadOnlyList<CombatCommand> Commands { get; }
        public CombatDataIdentity Data { get; }

        public CombatSetup(long seed, long durationMs, StatRules stats, DamageRules damage, ProgressionRules progression,
            CombatRules combat, IEnumerable<CombatantSetup> combatants, IEnumerable<CombatCommand> commands, CombatDataIdentity data)
        {
            Seed = seed;
            DurationMs = durationMs;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            // Copies figées (M05) : la liste validée est exactement celle que le moteur simulera.
            Combatants = Frozen.List(combatants, nameof(combatants));
            Commands = Frozen.List(commands, nameof(commands));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        // Correctif 6.1 : \z exige la fin de la chaîne (« $ » acceptait « bastion\n »).
        private static readonly Regex NamePattern = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <summary>Identifiant de combattant ou d'équipe valide : minuscules, chiffres et « _ ».</summary>
        public static bool IsValidName(string? name) => name != null && NamePattern.IsMatch(name);

        /// <summary>
        /// Incohérences qui empêchent de lancer le combat (références inconnues, durées hors tick, builds invalides…).
        /// Les chemins ont la forme du fichier de scénario (<c>combatants[1].id</c>, <c>commands[3].atMs</c>).
        /// Ce qui relève du jeu (recharge, endurance, cible morte) n'est pas une erreur ici : la commande est refusée en combat.
        /// </summary>
        public IReadOnlyList<RuleViolation> Validate()
        {
            var errors = new List<RuleViolation>();
            foreach (RuleViolation v in Combat.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.combat." + f).ToArray()));
            foreach (RuleViolation v in Stats.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.stats." + f).ToArray()));
            foreach (RuleViolation v in Damage.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.damage." + f).ToArray()));
            foreach (RuleViolation v in Progression.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.progression." + f).ToArray()));
            if (Combat.Statuses != null)
                foreach (RuleViolation v in Combat.Statuses.Validate(Combat.TickMilliseconds)) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules." + f).ToArray()));
            if (errors.Count > 0) return errors;   // la suite dépend de règles cohérentes

            // Lot 7.1 : admission. Un scénario hors limites de volume est refusé sans analyser ses éléments un par un.
            bool oversized = false;
            if (Combatants.Count > Combat.MaxCombatants)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{Combatants.Count} combattants, au plus {Combat.MaxCombatants} (combat.limits.maxCombatants).", "combatants"));
                oversized = true;
            }
            if (Commands.Count > Combat.MaxCommands)
            {
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{Commands.Count} commandes, au plus {Combat.MaxCommands} (combat.limits.maxCommands).", "commands"));
                oversized = true;
            }
            if (oversized) return errors;

            long tick = Combat.TickMilliseconds;
            if (Seed < 0) errors.Add(new RuleViolation("la graine doit être positive ou nulle.", "seed"));
            if (DurationMs <= 0 || DurationMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"durée hors de ]0, {Combat.MaxDurationMs}] ms (limite des règles).", "durationMs"));
            else if (DurationMs % tick != 0)
                errors.Add(new RuleViolation($"{DurationMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", "durationMs"));

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Combatants.Count; i++)
            {
                CombatantSetup c = Combatants[i];
                string path = $"combatants[{i}]";
                if (!IsValidName(c.Id))
                    errors.Add(new RuleViolation($"identifiant « {c.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", path + ".id"));
                else if (ids.ContainsKey(c.Id))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"identifiant « {c.Id} » déjà utilisé (combatants[{ids[c.Id]}]).", path + ".id"));
                else
                    ids[c.Id] = i;
                if (!IsValidName(c.Team))
                    errors.Add(new RuleViolation($"équipe « {c.Team} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", path + ".team"));

                switch (c)
                {
                    case CharacterSetup character:
                        ValidateCharacter(character, path, tick, errors);
                        break;
                    case DummySetup dummy:
                        Defense(dummy.Armor, "l'armure du mannequin", path + ".armor", errors);
                        Defense(dummy.MagicResistance, "la résistance magique du mannequin", path + ".magicResistance", errors);
                        Defense(dummy.StatusResistance, "la résistance aux altérations du mannequin", path + ".statusResistance", errors);
                        break;
                    case AssailantSetup assailant:
                        ValidateAssailantShape(assailant, path, tick, errors);
                        break;
                    default:
                        errors.Add(new RuleViolation($"type de combattant non pris en charge : {c.GetType().Name}.", path));
                        break;
                }
            }
            for (int i = 0; i < Combatants.Count; i++)
                if (Combatants[i] is AssailantSetup { Target: { } targetId } assailant)
                {
                    CombatantSetup? target = Combatants.FirstOrDefault(c => c.Id == targetId);
                    if (target is null)
                        errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {targetId} ».", $"combatants[{i}].target"));
                    else if (target.Team == assailant.Team)
                        errors.Add(new RuleViolation("la cible de l'assaillant doit être un ennemi.", $"combatants[{i}].target"));
                }
            if (errors.Count == 0) ValidateNumericLimits(errors);
            if (Combatants.Select(c => c.Team).Distinct(StringComparer.Ordinal).Count() < 2)
                errors.Add(new RuleViolation("un combat oppose au moins deux équipes.", "combatants"));

            var perActorTick = new Dictionary<(string Actor, long Tick), int>();
            for (int i = 0; i < Commands.Count; i++)
            {
                CombatCommand command = Commands[i];
                string path = $"commands[{i}]";
                if (command.AtMs < 0 || (DurationMs > 0 && command.AtMs >= DurationMs))
                    errors.Add(new RuleViolation($"instant {command.AtMs} ms hors du combat [0, {DurationMs}[ ms.", path + ".atMs"));
                else if (command.AtMs % tick != 0)
                    errors.Add(new RuleViolation($"{command.AtMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".atMs"));
                else
                {
                    // Une seule erreur par couple (acteur, tick), sur la première commande en excès, dans l'ordre du fichier.
                    var key = (command.Actor, command.AtMs / tick);
                    perActorTick.TryGetValue(key, out int count);
                    perActorTick[key] = ++count;
                    if (count == Combat.MaxCommandsPerActorPerTick + 1)
                        errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                            $"{count}ᵉ commande de « {command.Actor} » au tick {key.Item2} ({command.AtMs} ms), au plus {Combat.MaxCommandsPerActorPerTick} (combat.limits.maxCommandsPerActorPerTick).",
                            path + ".atMs"));
                }
                if (!ids.ContainsKey(command.Actor))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {command.Actor} ».", path + ".actor"));
                if (command.Target != null && !ids.ContainsKey(command.Target))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"combattant inconnu « {command.Target} ».", path + ".target"));
                if (!Enum.IsDefined(typeof(CombatAction), command.Action))
                    errors.Add(new RuleViolation("action inconnue.", path + ".action"));
                else if (command.Action == CombatAction.UseSkill)
                {
                    if (string.IsNullOrEmpty(command.Skill)) errors.Add(new RuleViolation("compétence obligatoire.", path + ".skill"));
                }
                else
                {
                    if (command.Skill != null) errors.Add(new RuleViolation("une garde ou une parade ne nomme pas de compétence.", path + ".skill"));
                    if (command.Target != null) errors.Add(new RuleViolation("une garde ou une parade n'a pas de cible.", path + ".target"));
                }
            }
            ValidateInitialQueue(errors);
            return errors;
        }

        /// <summary>
        /// Lot 7.1 : événements programmés à l'initialisation (une entrée par commande, un premier tour par assaillant qui
        /// attaque avant la fin) comparés à la capacité réelle de la file. Le moteur refait le même compte à sa construction ;
        /// un dépassement est refusé ici, avant tout journal.
        /// </summary>
        private void ValidateInitialQueue(List<RuleViolation> errors)
        {
            if (Combat.TickMilliseconds <= 0 || DurationMs <= 0) return;
            long initial = InitialEventCount();
            if (initial > Combat.MaxPendingEvents)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{initial} événements à programmer avant le premier tick (commandes et premiers tours d'assaillant), capacité de la file {Combat.MaxPendingEvents} (combat.limits.maxPendingEvents).",
                    "commands"));
        }

        /// <summary>Nombre d'événements que le moteur met en file à sa construction.</summary>
        public long InitialEventCount()
        {
            long durationTicks = DurationMs / Combat.TickMilliseconds;
            long assailantTurns = Combatants.OfType<AssailantSetup>().LongCount(a => a.FirstAttackMs >= 0 && a.FirstAttackMs / Combat.TickMilliseconds < durationTicks);
            return Commands.Count + assailantTurns;
        }

        private void ValidateAssailantShape(AssailantSetup a, string path, long tick, List<RuleViolation> errors)
        {
            Defense(a.Armor, "l'armure de l'assaillant", path + ".armor", errors);
            Defense(a.MagicResistance, "la résistance magique de l'assaillant", path + ".magicResistance", errors);
            Defense(a.StatusResistance, "la résistance aux altérations de l'assaillant", path + ".statusResistance", errors);
            if (a.FirstAttackMs < 0 || a.FirstAttackMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"instant hors de [0, {Combat.MaxDurationMs}] ms.", path + ".firstAttackMs"));
            else if (a.FirstAttackMs % tick != 0)
                errors.Add(new RuleViolation($"{a.FirstAttackMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".firstAttackMs"));
            if (a.IntervalMs <= 0 || a.IntervalMs > Combat.MaxDurationMs)
                errors.Add(new RuleViolation($"intervalle hors de ]0, {Combat.MaxDurationMs}] ms.", path + ".intervalMs"));
            else if (a.IntervalMs % tick != 0)
                errors.Add(new RuleViolation($"{a.IntervalMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".intervalMs"));
            if (a.Attacks.Count == 0) errors.Add(new RuleViolation("au moins une attaque.", path + ".attacks"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < a.Attacks.Count; k++)
            {
                AssailantAttack attack = a.Attacks[k];
                string attackPath = $"{path}.attacks[{k}]";
                if (!IsValidName(attack.Id))
                    errors.Add(new RuleViolation($"identifiant « {attack.Id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.", attackPath + ".id"));
                else if (!seen.Add(attack.Id))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"attaque « {attack.Id} » déjà définie.", attackPath + ".id"));
                if (attack.RawDamage < 0 || attack.RawDamage > CombatantStats.MaxRawDamage)
                    errors.Add(new RuleViolation($"dégâts bruts hors de [0, {CombatantStats.MaxRawDamage}].", attackPath + ".rawDamage"));
                if (!Enum.IsDefined(typeof(DamageType), attack.DamageType)) errors.Add(new RuleViolation("type de dégâts inconnu.", attackPath + ".damageType"));
                if (!Enum.IsDefined(typeof(SkillTargeting), attack.Targeting)) errors.Add(new RuleViolation("ciblage inconnu.", attackPath + ".targeting"));
                StatusApplication.ValidateList(attack.Applies, attackPath + ".applies", errors);
                for (int m = 0; m < attack.Applies.Count; m++)
                    if (Combat.Statuses.CheckApplication(attack.Applies[m], Combat.GuardBreakStatus) is { } problem)
                        errors.Add(new RuleViolation(problem.StartsWith("altération inconnue", StringComparison.Ordinal) ? RuleViolationKind.UnknownReference : RuleViolationKind.InvalidValue,
                            problem, $"{attackPath}.applies[{m}].status"));
            }
            if (a.Target is null && a.Attacks.Any(x => x.Targeting == SkillTargeting.Enemy))
                errors.Add(new RuleViolation("une attaque à cible unique exige une cible pour l'assaillant.", path + ".target"));
        }

        private void ValidateCharacter(CharacterSetup character, string path, long tick, List<RuleViolation> errors)
        {
            foreach (RuleViolation v in character.Class.Validate(Progression))
                errors.Add(new RuleViolation(v.Kind, $"classe « {character.Class.Id} » incohérente : {v.Message}", v.Fields.Select(f => path + ".class:" + f).ToArray()));
            foreach (BuildViolation v in BuildValidator.Validate(character.Build, character.Class, Progression))
                errors.Add(new RuleViolation($"build invalide ({v.Subject}) : {v.Message} [{v.Code}]", path + ".build"));
            foreach (SkillDefinition skill in character.Class.Skills)
            {
                if (skill.Combat is not { } combat) continue;
                if (combat.CooldownMs % tick != 0)
                    errors.Add(new RuleViolation($"recharge de « {skill.Name} » ({combat.CooldownMs} ms) non multiple de la durée du tick ({tick} ms).", path + ".class"));
                // Lot 7.2 : les altérations des compétences désignent le catalogue des règles, connu seulement ici.
                foreach (StatusApplication application in combat.Applies)
                    if (Combat.Statuses.CheckApplication(application, Combat.GuardBreakStatus) is { } problem)
                        errors.Add(new RuleViolation(problem.StartsWith("altération inconnue", StringComparison.Ordinal) ? RuleViolationKind.UnknownReference : RuleViolationKind.InvalidValue,
                            $"compétence « {skill.Id} » : {problem}", path + ".class"));
            }
        }

        private static void Defense(Fixed value, string label, string path, List<RuleViolation> errors)
        {
            if (value < Fixed.Zero || value > CombatantStats.MaxDefense)
                errors.Add(new RuleViolation($"{label} doit être comprise entre 0 et {CombatantStats.MaxDefense}.", path));
        }

        /// <summary>Profil offensif d'un combattant : ce qui entre dans la formule de dégâts pour une de ses attaques.</summary>
        private readonly struct Offense
        {
            public readonly string Label;
            public readonly DerivedStats Attacker;
            public readonly PowerType Power;
            public readonly DamageType Type;
            public readonly Fixed Coefficient;
            public readonly bool CanCrit;

            public Offense(string label, DerivedStats attacker, PowerType power, DamageType type, Fixed coefficient, bool canCrit)
            {
                Label = label;
                Attacker = attacker;
                Power = power;
                Type = type;
                Coefficient = coefficient;
                CanCrit = canCrit;
            }
        }

        /// <summary>
        /// Précontrôle numérique (correctif 6.1, M04 ; complété au correctif 6.1.1, CA-M01). Calcule à l'avance, pour chaque
        /// combattant, tout ce que le moteur calculera à sa construction (statistiques, PV, endurance, défenses, puissance de
        /// chaque attaque), puis vérifie, avant le premier tick et sans rien journaliser :
        /// <list type="number">
        /// <item>les défenses effectives réelles de <b>chaque</b> participant, personnages compris, pour les deux types de dégâts :
        /// au plus <see cref="CombatantStats.MaxDefense"/>, et dénominateur constante × 10 000 + défense brute représentable ;</item>
        /// <item>le pire impact de chaque attaque contre chaque défenseur ennemi réel (critique, Fragilité, sans garde ni parade) ;</item>
        /// <item>le pire impact contre une cible sans défense, qui majore tous les autres, cumulé sur toute la durée au rythme
        /// maximal d'événements comptés.</item>
        /// </list>
        /// Un dépassement produit un diagnostic <see cref="RuleViolationKind.NumericLimit"/> rattaché au combattant concerné.
        /// Ce précontrôle couvre les calculs connus du moteur ; un débordement non anticipé reste arrêté proprement en simulation
        /// (<c>simulationAborted</c>, raison <c>overflow</c>).
        /// </summary>
        private void ValidateNumericLimits(List<RuleViolation> errors)
        {
            int count = Combatants.Count;
            var defenses = new DerivedStats?[count];
            var offenses = new List<Offense>[count];
            for (int i = 0; i < count; i++)
            {
                CombatantSetup combatant = Combatants[i];
                string path = $"combatants[{i}]";
                offenses[i] = new List<Offense>();
                try
                {
                    switch (combatant)
                    {
                        case CharacterSetup character:
                            DerivedStats stats = CombatantStats.ForCharacter(Stats, character.Build);
                            stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                            stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                            defenses[i] = stats;
                            foreach (SkillDefinition skill in character.Class.Skills)
                                if (skill.Combat is { } combat)
                                {
                                    offenses[i].Add(new Offense($"compétence « {skill.Id} »", stats, combat.PowerType, combat.DamageType, combat.Coefficient, true));
                                    AddDotOffenses(offenses[i], $"compétence « {skill.Id} »", stats, combat.Applies);
                                }
                            break;
                        case DummySetup dummy:
                            defenses[i] = CombatantStats.ForDefenses(Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                            break;
                        case AssailantSetup assailant:
                            defenses[i] = CombatantStats.ForDefenses(Stats, assailant.Armor, assailant.MagicResistance, assailant.StatusResistance);
                            foreach (AssailantAttack attack in assailant.Attacks)
                            {
                                DerivedStats attackStats = CombatantStats.ForAssailantAttack(Stats, attack.RawDamage);
                                offenses[i].Add(new Offense($"attaque « {attack.Id} »", attackStats,
                                    CombatantStats.PowerOf(attack.DamageType), attack.DamageType, Fixed.One, false));
                                AddDotOffenses(offenses[i], $"attaque « {attack.Id} »", attackStats, attack.Applies);
                            }
                            break;
                    }
                }
                catch (OverflowException)
                {
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, "statistiques dérivées hors des limites numériques.",
                        combatant is CharacterSetup ? path + ".build.attributes" : path));
                    defenses[i] = null;
                    offenses[i].Clear();
                }
            }

            // 1. Défenses réelles de chaque participant et dénominateur de la formule, pour les deux types de dégâts.
            var usable = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (defenses[i] is not { } stats) continue;
                usable[i] = true;
                foreach (DamageType type in new[] { DamageType.Physique, DamageType.Magique })
                {
                    Fixed effective = stats.GetEffectiveDefense(type);
                    long constant = type == DamageType.Physique ? Damage.ArmorConstant : Damage.MagicResistanceConstant;
                    string field = DefenseField(i, type);
                    string label = type == DamageType.Physique ? "armure" : "résistance magique";
                    if (effective > CombatantStats.MaxDefense)
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"{label} effective calculée ({effective}) au-delà de la limite technique {CombatantStats.MaxDefense}" +
                            (Combatants[i] is CharacterSetup ? " (valeur de base des règles et apports du personnage)." : "."), field));
                        usable[i] = false;
                    }
                    else if (!Representable(() => checked(constant * Fixed.Scale + effective.Raw)))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"dénominateur de défense non représentable (constante {constant}, {label} {effective}).", field));
                        usable[i] = false;
                    }
                }
                // Lot 7.2 : résistance aux altérations, même enveloppe que les défenses ; le facteur K / (K + résistance) a le même
                // dénominateur que la formule de défense. La durée réduite (ticks de base ≤ 86 400 000 × facteur brut ≤ 10⁴) reste < 10¹².
                Fixed statusResistance = stats.StatusResistance;
                string statusField = Combatants[i] is CharacterSetup ? $"combatants[{i}]" : $"combatants[{i}].statusResistance";
                if (statusResistance > CombatantStats.MaxDefense)
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"résistance aux altérations effective calculée ({statusResistance}) au-delà de la limite technique {CombatantStats.MaxDefense}.", statusField));
                else if (!Representable(() => checked(Stats.StatusResistanceConstant * Fixed.Scale + statusResistance.Raw)))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"dénominateur de résistance aux altérations non représentable (constante {Stats.StatusResistanceConstant}, résistance {statusResistance}).", statusField));
            }

            // 2. Pire impact de chaque attaque contre chaque défenseur ennemi réel ; 3. majorant sans défense, cumulé sur la durée.
            DerivedStats undefended = CombatantStats.ForDefenses(Stats, Fixed.Zero, Fixed.Zero);
            // Lot 7.2 : majorant des bonus de dégâts reçus, toutes les altérations du catalogue actives à la fois.
            DamageBonuses worstBonuses = DamageBonuses.None.Add(BonusCategory.Alteration, Combat.Statuses.TotalDamageTakenIncrease());
            long maxImpacts = checked(DurationMs / Combat.TickMilliseconds * Combat.MaxEventsPerTick);
            for (int i = 0; i < count; i++)
            {
                string path = $"combatants[{i}]";
                long worstHit = 0;
                bool failed = false;
                foreach (Offense offense in offenses[i])
                {
                    if (failed) break;
                    if (!TryWorstHit(offense, undefended, worstBonuses, out long hit))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                            $"pire impact de {offense.Label} hors des limites numériques du moteur (critique, Fragilité, aucune défense).", path));
                        failed = true;
                        break;
                    }
                    worstHit = Math.Max(worstHit, hit);
                    for (int j = 0; j < count; j++)
                    {
                        if (!usable[j] || Combatants[j].Team == Combatants[i].Team) continue;
                        if (!TryWorstHit(offense, defenses[j]!, worstBonuses, out _))
                        {
                            errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                                $"pire impact de {offense.Label} contre « {Combatants[j].Id} » hors des limites numériques du moteur.", path));
                            failed = true;
                            break;
                        }
                    }
                }
                if (!failed && !Representable(() => checked(worstHit * maxImpacts)))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"cumul des dégâts sur la durée du combat hors des limites numériques (pire impact {worstHit} × {maxImpacts} événements comptés au plus).", path));
            }
        }

        /// <summary>
        /// Lot 7.2 : chaque tick d'un DOT est un impact sans critique dont la puissance figée vaut la puissance du type de l'altération
        /// (dégâts bruts pour un assaillant) × le coefficient de l'application. Il entre dans le pire impact comme une attaque.
        /// </summary>
        private void AddDotOffenses(List<Offense> offenses, string owner, DerivedStats attacker, IReadOnlyList<StatusApplication> applies)
        {
            foreach (StatusApplication application in applies)
                if (Combat.Statuses.TryGet(application.Status, out StatusDefinition? definition) && definition.IsDot
                    && definition.DamageType is { } type && application.Coefficient is { } coefficient)
                    offenses.Add(new Offense($"altération « {definition.Id} » de {owner}", attacker, CombatantStats.PowerOf(type), type, coefficient, false));
        }

        private string DefenseField(int index, DamageType type) => Combatants[index] is CharacterSetup
            ? $"combatants[{index}]"
            : $"combatants[{index}]." + (type == DamageType.Physique ? "armor" : "magicResistance");

        private bool TryWorstHit(Offense offense, DerivedStats defender, DamageBonuses bonuses, out long hit)
        {
            try
            {
                hit = DamageCalculator.Compute(new DamageInput(offense.Attacker, defender, offense.Power, offense.Type, offense.Coefficient,
                    bonuses, offense.CanCrit, Fixed.Zero), Damage).FinalDamage;
                return true;
            }
            catch (OverflowException)
            {
                hit = 0;
                return false;
            }
        }

        private static bool Representable(Func<long> computation)
        {
            try
            {
                computation();
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    /// <summary>Levée quand on construit un moteur avec un combat invalide ; <see cref="Violations"/> donne le détail.</summary>
    public sealed class CombatSetupException : ArgumentException
    {
        public IReadOnlyList<RuleViolation> Violations { get; }

        public CombatSetupException(IReadOnlyList<RuleViolation> violations)
            : base("Combat invalide : " + string.Join(" ; ", violations.Select(v => v.ToString())))
        {
            Violations = violations;
        }
    }
}
