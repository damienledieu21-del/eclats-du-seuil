using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Simulation.Engine
{
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

    /// <summary>Commande « utiliser une compétence », horodatée en millisecondes depuis le début du combat.</summary>
    public sealed class CombatCommand
    {
        public long AtMs { get; }
        public string Actor { get; }
        public string Skill { get; }

        /// <summary>Cible désignée ; null pour une compétence qui choisit ses cibles (zone).</summary>
        public string? Target { get; }

        public CombatCommand(long atMs, string actor, string skill, string? target = null)
        {
            AtMs = atMs;
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Target = target;
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
                    default:
                        errors.Add(new RuleViolation($"type de combattant non pris en charge : {c.GetType().Name}.", path));
                        break;
                }
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
                if (string.IsNullOrEmpty(command.Skill))
                    errors.Add(new RuleViolation("compétence obligatoire.", path + ".skill"));
            }
            return errors;
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
