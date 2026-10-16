using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain;
using EdS.Domain.Builds;
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

        public DummySetup(string id, string team, Fixed armor, Fixed magicResistance, int initiative = 0)
            : base(id, team, initiative)
        {
            Armor = armor;
            MagicResistance = magicResistance;
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

        /// <summary>Applique (ou renouvelle) la Fragilité sur chaque cible touchée, après les dégâts de l'impact.</summary>
        public bool AppliesFragility { get; }

        public AssailantAttack(string id, long rawDamage, DamageType damageType = DamageType.Physique,
            SkillTargeting targeting = SkillTargeting.Enemy, bool unblockable = false, bool appliesFragility = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RawDamage = rawDamage;
            DamageType = damageType;
            Targeting = targeting;
            Unblockable = unblockable;
            AppliesFragility = appliesFragility;
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

        /// <summary>Cible des attaques <see cref="SkillTargeting.Enemy"/> ; null si toutes ses attaques sont de zone.</summary>
        public string? Target { get; }

        public long FirstAttackMs { get; }
        public long IntervalMs { get; }
        public IReadOnlyList<AssailantAttack> Attacks { get; }

        public AssailantSetup(string id, string team, Fixed armor, Fixed magicResistance, string? target, long firstAttackMs, long intervalMs,
            IEnumerable<AssailantAttack> attacks, int initiative = 0)
            : base(id, team, initiative)
        {
            Armor = armor;
            MagicResistance = magicResistance;
            Target = target;
            FirstAttackMs = firstAttackMs;
            IntervalMs = intervalMs;
            Attacks = (attacks ?? throw new ArgumentNullException(nameof(attacks))).ToArray();
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
        public static IReadOnlyList<string> Defensive { get; } = new[] { GuardStart, GuardStop, Parry };

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
            Classes = (classes ?? throw new ArgumentNullException(nameof(classes))).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
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
            Combatants = (combatants ?? throw new ArgumentNullException(nameof(combatants))).ToArray();
            Commands = (commands ?? throw new ArgumentNullException(nameof(commands))).ToArray();
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        private static readonly Regex NamePattern = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);

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
            if (errors.Count > 0) return errors;   // la suite dépend de règles cohérentes

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
                        if (dummy.Armor < Fixed.Zero) errors.Add(new RuleViolation("l'armure du mannequin ne peut pas être négative.", path + ".armor"));
                        if (dummy.MagicResistance < Fixed.Zero) errors.Add(new RuleViolation("la résistance magique du mannequin ne peut pas être négative.", path + ".magicResistance"));
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
            if (Combatants.Select(c => c.Team).Distinct(StringComparer.Ordinal).Count() < 2)
                errors.Add(new RuleViolation("un combat oppose au moins deux équipes.", "combatants"));

            for (int i = 0; i < Commands.Count; i++)
            {
                CombatCommand command = Commands[i];
                string path = $"commands[{i}]";
                if (command.AtMs < 0 || (DurationMs > 0 && command.AtMs >= DurationMs))
                    errors.Add(new RuleViolation($"instant {command.AtMs} ms hors du combat [0, {DurationMs}[ ms.", path + ".atMs"));
                else if (command.AtMs % tick != 0)
                    errors.Add(new RuleViolation($"{command.AtMs} ms n'est pas un multiple de la durée du tick ({tick} ms).", path + ".atMs"));
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
            return errors;
        }

        private void ValidateAssailantShape(AssailantSetup a, string path, long tick, List<RuleViolation> errors)
        {
            if (a.Armor < Fixed.Zero) errors.Add(new RuleViolation("l'armure de l'assaillant ne peut pas être négative.", path + ".armor"));
            if (a.MagicResistance < Fixed.Zero) errors.Add(new RuleViolation("la résistance magique de l'assaillant ne peut pas être négative.", path + ".magicResistance"));
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
                if (attack.RawDamage < 0) errors.Add(new RuleViolation("les dégâts bruts ne peuvent pas être négatifs.", attackPath + ".rawDamage"));
                if (!Enum.IsDefined(typeof(DamageType), attack.DamageType)) errors.Add(new RuleViolation("type de dégâts inconnu.", attackPath + ".damageType"));
                if (!Enum.IsDefined(typeof(SkillTargeting), attack.Targeting)) errors.Add(new RuleViolation("ciblage inconnu.", attackPath + ".targeting"));
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
                if (skill.Combat is { } combat && combat.CooldownMs % tick != 0)
                    errors.Add(new RuleViolation($"recharge de « {skill.Name} » ({combat.CooldownMs} ms) non multiple de la durée du tick ({tick} ms).", path + ".class"));
            try
            {
                DerivedStats.Calculate(Stats, character.Build.Attributes);
            }
            catch (OverflowException)
            {
                errors.Add(new RuleViolation("statistiques dérivées hors des limites numériques.", path + ".build.attributes"));
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
