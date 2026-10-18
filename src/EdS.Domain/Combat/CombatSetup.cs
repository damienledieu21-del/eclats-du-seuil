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

        /// <summary>
        /// Lot 7.3 : déclencheurs permanents actifs avec ce build : talents de rang ≥ 1 (valeurs à leur rang), passifs équipés,
        /// actives et ultime équipées (Q2). Dans l'ordre de la classe ; le moteur trie les abonnés selon l'ordre total (§5.4).
        /// Une référence inconnue (build invalide) est ignorée : la validation du build la signale.
        /// </summary>
        public IEnumerable<BoundTrigger> ActiveTriggers()
        {
            foreach (TalentTreeDefinition tree in Class.Trees)
                foreach (TalentDefinition talent in tree.Talents)
                    if (Build.TalentRanks.TryGetValue(talent.Id, out int rank) && rank >= 1)
                        for (int k = 0; k < talent.Triggers.Count; k++)
                            yield return new BoundTrigger(TriggerOrigin.Talent, talent.Id, k, rank, talent.Triggers[k]);
            var equipped = new HashSet<string>(Build.ActiveSkills.Concat(Build.PassiveSkills), StringComparer.Ordinal);
            if (Build.Ultimate != null) equipped.Add(Build.Ultimate);
            foreach (SkillDefinition skill in Class.Skills)
                if (equipped.Contains(skill.Id))
                    for (int k = 0; k < skill.Triggers.Count; k++)
                        yield return new BoundTrigger(skill.Kind == SkillKind.Passive ? TriggerOrigin.Passive : TriggerOrigin.Skill, skill.Id, k, 1, skill.Triggers[k]);
        }

        /// <summary>
        /// Sous-lot 8.1 : modificateurs actifs avec ce build : talents de rang ≥ 1 (valeurs à leur rang), puis compétences
        /// <b>passives</b> équipées (rang 1), dans l'ordre de la classe. Une référence inconnue (build invalide) est ignorée : la
        /// validation du build la signale. Agrégés par <see cref="ModifierSet.For"/>, sans effet d'ordre.
        /// </summary>
        public IEnumerable<BoundModifier> ActiveModifiers()
        {
            foreach (TalentTreeDefinition tree in Class.Trees)
                foreach (TalentDefinition talent in tree.Talents)
                    if (Build.TalentRanks.TryGetValue(talent.Id, out int rank) && rank >= 1)
                        for (int k = 0; k < talent.Modifiers.Count; k++)
                            yield return new BoundModifier(talent.Id, k, rank, talent.Modifiers[k]);
            var passives = new HashSet<string>(Build.PassiveSkills, StringComparer.Ordinal);
            foreach (SkillDefinition skill in Class.Skills)
                if (skill.Kind == SkillKind.Passive && passives.Contains(skill.Id))
                    for (int k = 0; k < skill.Modifiers.Count; k++)
                        yield return new BoundModifier(skill.Id, k, 1, skill.Modifiers[k]);
        }
    }

    /// <summary>Sous-lot 8.1 : modificateur rattaché à son talent ou à sa compétence passive, avec le rang qui fixe sa valeur.</summary>
    public readonly struct BoundModifier
    {
        public string DefinitionId { get; }
        public int Index { get; }
        public int Rank { get; }
        public ModifierDefinition Definition { get; }

        public BoundModifier(string definitionId, int index, int rank, ModifierDefinition definition)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Index = index;
            Rank = rank;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        /// <summary>Valeur au rang du build (arithmétique vérifiée, bornée par la validation de la classe).</summary>
        public Fixed Value => Definition.Add.At(Rank);
    }

    /// <summary>
    /// Lot 7.3 : déclencheur rattaché à son origine (talent, compétence, assaillant) avec le rang qui fixe ses valeurs. L'identité
    /// journalisée est <c>définition/indice@porteur#instance</c> ; l'instance n'est connue qu'en combat (altérations).
    /// </summary>
    public readonly struct BoundTrigger
    {
        public TriggerOrigin Origin { get; }
        public string DefinitionId { get; }
        public int Index { get; }
        public int Rank { get; }
        public TriggerDefinition Definition { get; }

        public BoundTrigger(TriggerOrigin origin, string definitionId, int index, int rank, TriggerDefinition definition)
        {
            Origin = origin;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Index = index;
            Rank = rank;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public string Label => $"{DefinitionId}/{Index}";
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

        /// <summary>Déclencheurs déclarés par le scénario (lot 7.3, Q4), pour tester sans classe ; définition <c>assailant.{id}</c>.</summary>
        public IReadOnlyList<TriggerDefinition> Triggers { get; }

        /// <summary>Identifiant de définition des déclencheurs d'un assaillant, dans leur identité journalisée.</summary>
        public string TriggerDefinitionId => "assailant." + Id;

        public AssailantSetup(string id, string team, Fixed armor, Fixed magicResistance, string? target, long firstAttackMs, long intervalMs,
            IEnumerable<AssailantAttack> attacks, int initiative = 0, Fixed statusResistance = default, IEnumerable<TriggerDefinition>? triggers = null)
            : base(id, team, initiative)
        {
            Triggers = Frozen.ListOrEmpty(triggers, nameof(triggers));
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
        public IReadOnlyList<RuleViolation> Validate() => Validate(queueBound: true);

        /// <summary>
        /// Variante réservée aux tests du filet de sécurité <c>queueLimitExceeded</c> : sans le majorant de saturation, un combat
        /// valide peut atteindre la capacité de la file en cours de simulation.
        /// </summary>
        internal IReadOnlyList<RuleViolation> Validate(bool queueBound) => Validate(queueBound, numericLimits: true);

        /// <summary>
        /// Correctif 7.3.1 : <paramref name="numericLimits"/> faux omet aussi le précontrôle numérique, pour exercer en test la
        /// défense en profondeur du moteur (mutations préparées dans des variables vérifiées, arrêt <c>overflow</c> sans effet partiel).
        /// </summary>
        internal IReadOnlyList<RuleViolation> Validate(bool queueBound, bool numericLimits)
        {
            var errors = new List<RuleViolation>();
            foreach (RuleViolation v in Combat.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.combat." + f).ToArray()));
            foreach (RuleViolation v in Stats.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.stats." + f).ToArray()));
            foreach (RuleViolation v in Damage.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.damage." + f).ToArray()));
            foreach (RuleViolation v in Progression.Validate()) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules.progression." + f).ToArray()));
            if (Combat.Statuses != null)
                foreach (RuleViolation v in Combat.Statuses.Validate(Combat.TickMilliseconds)) errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules." + f).ToArray()));
            if (errors.Count == 0)
                foreach (RuleViolation v in Combat.Statuses!.ValidateTriggers(Combat.TickMilliseconds, Combat.GuardBreakStatus))
                    errors.Add(new RuleViolation(v.Kind, v.Message, v.Fields.Select(f => "rules." + f).ToArray()));
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
            if (errors.Count == 0) ValidateTriggerCount(errors);
            if (errors.Count == 0 && numericLimits) ValidateNumericLimits(errors);
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
            if (queueBound && errors.Count == 0) ValidateQueueSaturation(errors);
            return errors;
        }

        /// <summary>
        /// Correctif 7.2 : contrôle prudent de saturation de la file. Le majorant <see cref="ComputeQueueBound"/> doit tenir dans
        /// <c>combat.limits.maxPendingEvents</c> ; sinon le scénario est refusé avant tout journal (<c>LimitExceeded</c> sur
        /// <c>combatants</c>), au lieu d'un arrêt <c>queueLimitExceeded</c> en cours de combat.
        /// </summary>
        private void ValidateQueueSaturation(List<RuleViolation> errors)
        {
            if (Combat.TickMilliseconds <= 0 || DurationMs <= 0) return;
            QueueBound bound = ComputeQueueBound();
            if (bound.Total > Combat.MaxPendingEvents)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"majorant prudent de la file {bound.Total} (événements initiaux {bound.Initial}, instances de DOT {bound.Dots}, " +
                    $"échéances d'altérations {bound.Expiries}{(bound.Transient > 0 ? $", terme transitoire des déclencheurs {bound.Transient}" : "")}) " +
                    $"au-delà de sa capacité {Combat.MaxPendingEvents} (combat.limits.maxPendingEvents).",
                    "combatants"));
        }

        /// <summary>
        /// Détail du majorant de la file (correctif 7.2 ; terme transitoire et <see cref="Guaranteed"/> au sous-lot 7.3, C12).
        /// </summary>
        public readonly struct QueueBound
        {
            public long Initial { get; }
            public long Dots { get; }
            public long Expiries { get; }
            /// <summary>Places des notifications et réactions des déclencheurs (0 sans déclencheur).</summary>
            public long Transient { get; }
            /// <summary>
            /// Vrai sans déclencheur : le majorant borne la file. Faux dès qu'un déclencheur existe : les échéances et ticks de DOT
            /// créés par des réactions ne sont pas majorés, et l'arrêt <c>queueLimitExceeded</c> reste un filet atteignable (Q11).
            /// </summary>
            public bool Guaranteed { get; }
            public long Total => checked(Initial + Dots + Expiries + Transient);

            public QueueBound(long initial, long dots, long expiries, long transient = 0, bool guaranteed = true)
            {
                Initial = initial;
                Dots = dots;
                Expiries = expiries;
                Transient = transient;
                Guaranteed = guaranteed;
            }

            /// <summary>Résumé pour les rapports de la CLI (validate, simulate).</summary>
            public string Describe(long capacity) => Guaranteed
                ? $"majorant prudent {Total}/{capacity}, garanti"
                : $"majorant prudent {Total}/{capacity} dont {Transient} transitoires, NON garanti : les altérations posées par des déclencheurs ne sont pas majorées (arrêt queueLimitExceeded possible)";
        }

        /// <summary>
        /// Correctif 7.2 : majorant du nombre d'éléments que la file peut contenir à tout instant du combat, pour un combat valide.
        /// <list type="bullet">
        /// <item>Événements initiaux (commandes, premiers tours) : chaque commande retirée en programme au plus un (recharge ou
        /// fin de fenêtre de parade) et chaque tour d'assaillant le suivant ; leur nombre ne croît donc pas.</item>
        /// <item>DOT : un seul tick programmé par instance, une instance par (source, cible, altération) : somme, sur chaque
        /// porteur et chaque DOT qu'il porte, de ses cibles possibles.</item>
        /// <item>Altérations à échéance : une application mise en file vit au plus la durée de base D de l'altération. Pour
        /// <c>ignore</c>, une seule échéance vivante par cible ; pour <c>refresh</c> et <c>keepLonger</c>, au plus les applications
        /// faites pendant D ticks : par assaillant, ses tours dans cette fenêtre (cadence, cycle d'attaques) × ses cibles par
        /// impact ; par personnage, ses commandes de la compétence, bornées par sa recharge et le GCD ; pour <c>keepLonger</c>,
        /// au plus un renouvellement par cible et par tick. Une altération jamais appliquée (aucune commande, aucune attaque) ne
        /// compte pas. La rupture de garde (rien en mode ignore : elle remplace une commande guardStart retirée) et les états accordés à
        /// l'expiration (une échéance vivante par cible pour <c>ignore</c>, D par cible sinon) s'y ajoutent.</item>
        /// </list>
        /// Seules les compétences effectivement commandées sont comptées. Suppose un combat par ailleurs valide.
        /// </summary>
        public QueueBound ComputeQueueBound()
        {
            long tick = Combat.TickMilliseconds;
            long durationTicks = DurationMs / tick;
            long gcdTicks = Combat.GlobalCooldownMs / tick;
            StatusCatalog catalog = Combat.Statuses;
            var targets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);   // cibles possibles par altération
            var applications = new Dictionary<string, long>(StringComparer.Ordinal);         // applications en D ticks par altération
            long dots = 0;

            // Appelé une fois par porteur et par altération : ses cibles possibles et ses applications en D ticks.
            void Carry(StatusDefinition d, IEnumerable<string> possibleTargets, long inWindow)
            {
                if (inWindow == 0) return;   // jamais appliquée : ni cible, ni instance
                if (!targets.TryGetValue(d.Id, out HashSet<string>? set)) targets[d.Id] = set = new HashSet<string>(StringComparer.Ordinal);
                var hit = new HashSet<string>(possibleTargets, StringComparer.Ordinal);
                if (d.IsDot) dots = checked(dots + hit.Count);   // une instance par (porteur, cible)
                set.UnionWith(hit);
                applications.TryGetValue(d.Id, out long n);
                applications[d.Id] = checked(n + inWindow);
            }

            // Sous-lot 8.1 : règles effectives des personnages. Une application vit au plus base + le plus grand ajout positif
            // d'un auteur (durationDealt) + le plus grand ajout positif d'une cible (durationTaken), écrêtée à 24 h comme au moteur ;
            // un raccourci ne fait que réduire. Les mannequins et assaillants n'ont pas de modificateur.
            var characterRules = new List<CombatantRules>();
            foreach (CombatantSetup c in Combatants)
                if (c is CharacterSetup ch) characterRules.Add(CombatantRules.For(Combat, ModifierSet.For(ch)));
            long Window(StatusDefinition d)
            {
                long dealt = 0, taken = 0;
                foreach (CombatantRules r in characterRules)
                {
                    dealt = Math.Max(dealt, r.DurationDealtMs(d.Id));
                    taken = Math.Max(taken, r.DurationTakenMs(d.Id));
                }
                long ms = dealt == 0 && taken == 0 ? d.DurationMs : Math.Min(CombatRules.AbsoluteMaxDurationMs, checked(d.DurationMs + dealt + taken));
                return Math.Max(1, ms / tick);
            }
            var enemies = Combatants.ToDictionary(c => c.Id, c => Combatants.Where(o => o.Team != c.Team).Select(o => o.Id).ToArray(), StringComparer.Ordinal);

            foreach (CombatantSetup combatant in Combatants)
            {
                switch (combatant)
                {
                    case AssailantSetup a:
                    {
                        long interval = Math.Max(1, a.IntervalMs / tick);
                        long first = a.FirstAttackMs / tick;
                        long turns = first < durationTicks ? (durationTicks - 1 - first) / interval + 1 : 0;
                        int n = a.Attacks.Count;
                        foreach (string status in a.Attacks.SelectMany(x => x.Applies).Select(x => x.Status).Distinct(StringComparer.Ordinal))
                        {
                            if (!catalog.TryGet(status, out StatusDefinition? d)) continue;
                            long k = Math.Min(turns, (Window(d) - 1) / interval + 1);   // tours dans une fenêtre de D ticks
                            long perAttack = (k + n - 1) / n;                            // tours d'une même attaque parmi k consécutifs
                            var carrying = a.Attacks.Where(x => x.Applies.Any(ap => ap.Status == status)).ToList();
                            bool zone = carrying.Any(x => x.Targeting == SkillTargeting.AllEnemies);
                            string[] hit = zone ? enemies[a.Id] : (a.Target is null ? Array.Empty<string>() : new[] { a.Target });
                            Carry(d, hit, checked(Math.Min(k, perAttack * carrying.Count) * hit.Length));
                        }
                        break;
                    }
                    case CharacterSetup c:
                    {
                        CombatantRules rules = CombatantRules.For(Combat, ModifierSet.For(c));
                        foreach (string status in c.Class.Skills.Where(sk => sk.Combat != null).SelectMany(sk => sk.Combat!.Applies)
                                     .Select(x => x.Status).Distinct(StringComparer.Ordinal))
                        {
                            if (!catalog.TryGet(status, out StatusDefinition? d)) continue;
                            long inWindow = 0;
                            foreach (SkillDefinition skill in c.Class.Skills.Where(sk => sk.Combat != null && sk.Combat.Applies.Any(ap => ap.Status == status)))
                            {
                                long commands = Commands.LongCount(cmd => cmd.Actor == c.Id && cmd.Action == CombatAction.UseSkill && cmd.Skill == skill.Id);
                                long spacing = Math.Max(gcdTicks, rules.CooldownMs(skill, skill.Combat!) / tick);   // recharge effective (8.1)
                                long rate = spacing == 0 ? checked(Combat.MaxCommandsPerActorPerTick * Window(d)) : (Window(d) - 1) / spacing + 1;
                                long hits = skill.Combat!.Targeting == SkillTargeting.AllEnemies ? enemies[c.Id].Length : 1;
                                inWindow = checked(inWindow + checked(Math.Min(commands, rate) * hits));
                            }
                            Carry(d, enemies[c.Id], inWindow);
                        }
                        break;
                    }
                }
            }

            long expiries = 0;
            var granted = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            void Grant(string status, IEnumerable<string> on)
            {
                if (!granted.TryGetValue(status, out HashSet<string>? set)) granted[status] = set = new HashSet<string>(StringComparer.Ordinal);
                set.UnionWith(on);
            }
            foreach (var (status, set) in targets)
            {
                StatusDefinition d = catalog[status];
                if (d.GrantsOnExpiry is { } g) Grant(g, set);
                if (d.IsDot) continue;
                long window = Window(d);
                expiries = checked(expiries + d.Stacking switch
                {
                    StatusStacking.Ignore => set.Count,
                    StatusStacking.KeepLonger => Math.Min(applications[status], checked(set.Count * window)),
                    _ => applications[status],
                });
            }
            if (catalog.TryGet(Combat.GuardBreakStatus, out StatusDefinition? guardBreak))
            {
                // Seul un personnage qui prend la garde peut la voir brisée. En mode ignore, son unique échéance vivante occupe la
                // place d'une commande guardStart déjà retirée, qui ne programme rien : elle n'ajoute rien au majorant.
                string[] characters = Combatants.OfType<CharacterSetup>()
                    .Where(c => Commands.Any(cmd => cmd.Actor == c.Id && cmd.Action == CombatAction.GuardStart)).Select(c => c.Id).ToArray();
                if (guardBreak.Stacking != StatusStacking.Ignore)
                    expiries = checked(expiries + checked(characters.Length * Window(guardBreak)));
                if (guardBreak.GrantsOnExpiry is { } g) Grant(g, characters);
            }
            foreach (var (status, set) in granted)
                if (catalog.TryGet(status, out StatusDefinition? d))
                    expiries = checked(expiries + (d.Stacking == StatusStacking.Ignore ? set.Count : checked(set.Count * Window(d))));
            // Sous-lot 7.3 (§6.4, C12) : chaque mise en file coûte une unité de travail. Les notifications et réactions d'un tick
            // sont toutes retirées avant la fin de leur phase, sauf les réactions reportées au tick suivant : à tout instant, au
            // plus celles mises en file pendant le tick courant et celles reportées du tick précédent, soit 2 × maxWorkPerTick.
            // Les échéances et ticks de DOT créés par des réactions ne sont pas majorés : le majorant n'est plus une garantie.
            if (HasTriggers) return new QueueBound(InitialEventCount(), dots, expiries, checked(2L * Combat.MaxWorkPerTick), guaranteed: false);
            return new QueueBound(InitialEventCount(), dots, expiries);
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
            // Lot 7.3 (Q4) : déclencheurs d'un assaillant de scénario, forme et références.
            TriggerDefinition.ValidateList(a.Triggers, path + ".triggers", 1, false, errors);
            TriggerDefinition.ValidateReferences(a.Triggers, path + ".triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, errors);
        }

        private void ValidateCharacter(CharacterSetup character, string path, long tick, List<RuleViolation> errors)
        {
            IReadOnlyList<RuleViolation> classViolations = character.Class.Validate(Progression);
            foreach (RuleViolation v in classViolations)
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
            // Lot 7.3 : les déclencheurs de la classe désignent le catalogue et le tick des règles, connus seulement ici. Les chemins
            // internes à la classe sont rattachés au combattant, comme les autres incohérences classe-règles.
            var problems = new List<RuleViolation>();
            for (int t = 0; t < character.Class.Trees.Count; t++)
                for (int i = 0; i < character.Class.Trees[t].Talents.Count; i++)
                    TriggerDefinition.ValidateReferences(character.Class.Trees[t].Talents[i].Triggers, $"trees[{t}].talents[{i}].triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, problems);
            for (int k = 0; k < character.Class.Skills.Count; k++)
                TriggerDefinition.ValidateReferences(character.Class.Skills[k].Triggers, $"skills[{k}].triggers", Combat.Statuses, Combat.GuardBreakStatus, (int)tick, problems);
            // Sous-lot 8.1 : les modificateurs désignent aussi le catalogue (durées d'altération) et le tick (valeurs en ms). Une classe
            // arrêtée à un plafond global n'est pas examinée plus loin (plafond signalé seul) ; une liste déjà refusée par la classe
            // (modificateurs d'une active, valeur hors borne…) ne l'est pas non plus : un même modificateur n'est pas signalé deux fois.
            bool capped = classViolations.Any(v => v.Kind == RuleViolationKind.LimitExceeded && v.Fields.Any(f => f == "trees" || f == "skills"));
            bool Refused(string list) => classViolations.Any(v => v.Fields.Any(f =>
                f == list || f.StartsWith(list + "[", StringComparison.Ordinal) || f.StartsWith(list + ".", StringComparison.Ordinal)));
            // Décision du 18/10/2026 : le plafond de la réduction de garde est un paramètre des règles, vérifié ici aussi.
            void CheckModifiers(IReadOnlyList<ModifierDefinition> modifiers, string list, int maxRank, bool ranked)
            {
                if (modifiers.Count == 0 || capped || Refused(list)) return;
                ModifierDefinition.ValidateReferences(modifiers, list, Combat.Statuses, Combat.GuardBreakStatus, tick, problems);
                ModifierDefinition.ValidateCaps(modifiers, list, maxRank, ranked, Combat.MaxGuardDamageReduction, problems);
            }
            for (int t = 0; t < character.Class.Trees.Count; t++)
                for (int i = 0; i < character.Class.Trees[t].Talents.Count; i++)
                    CheckModifiers(character.Class.Trees[t].Talents[i].Modifiers, $"trees[{t}].talents[{i}].modifiers", character.Class.Trees[t].Talents[i].MaxRank, true);
            for (int k = 0; k < character.Class.Skills.Count; k++)
                CheckModifiers(character.Class.Skills[k].Modifiers, $"skills[{k}].modifiers", 1, false);
            foreach (RuleViolation v in problems)
                errors.Add(new RuleViolation(v.Kind, $"classe « {character.Class.Id} », {string.Join(", ", v.Fields)} : {v.Message}", path + ".class"));
        }

        /// <summary>Nombre de déclencheurs que ce combat peut activer (ceux de <see cref="TriggerWarnings"/>), sans les nommer.</summary>
        private long ActiveTriggerCount()
        {
            long count = Combat.Statuses.Definitions.Sum(d => (long)d.Triggers.Count);
            foreach (CombatantSetup c in Combatants)
                count += c switch
                {
                    CharacterSetup ch => ch.ActiveTriggers().LongCount(),
                    AssailantSetup a => a.Triggers.Count,
                    _ => 0,
                };
            return count;
        }

        /// <summary>
        /// Correctif 7.3.1 (A73-M03) : plafond global des déclencheurs actifs du combat (<see cref="TriggerGraph.MaxTriggers"/>), vérifié
        /// avant l'analyse statique des cycles et le précontrôle numérique.
        /// </summary>
        private void ValidateTriggerCount(List<RuleViolation> errors)
        {
            long count = ActiveTriggerCount();
            if (count > TriggerGraph.MaxTriggers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{count} déclencheurs actifs dans le combat (personnages, assaillants, altérations), au plus {TriggerGraph.MaxTriggers} (plafond technique provisoire).",
                    "combatants"));
        }

        /// <summary>Lot 7.3 : y a-t-il au moins un déclencheur susceptible d'être actif dans ce combat ?</summary>
        public bool HasTriggers =>
            Combatants.Any(c => c is AssailantSetup { Triggers: { Count: > 0 } } || (c is CharacterSetup ch && ch.ActiveTriggers().Any()))
            || Combat.Statuses.Definitions.Any(d => d.Triggers.Count > 0);

        /// <summary>
        /// Lot 7.3 (A15, Q1) : avertissements du contrôle statique des cycles, pour tous les déclencheurs que ce combat peut activer
        /// (talents et compétences équipées des personnages, assaillants, altérations du catalogue). Jamais une erreur.
        /// </summary>
        public IReadOnlyList<RuleViolation> TriggerWarnings()
        {
            if (ActiveTriggerCount() > TriggerGraph.MaxTriggers) return Array.Empty<RuleViolation>();   // refusé par Validate
            var named = new List<NamedTrigger>();
            for (int i = 0; i < Combatants.Count; i++)
            {
                switch (Combatants[i])
                {
                    case CharacterSetup c:
                        foreach (BoundTrigger b in c.ActiveTriggers())
                            named.Add(new NamedTrigger($"{b.Label}@{c.Id}", $"combatants[{i}].class", b.Definition));
                        break;
                    case AssailantSetup a:
                        for (int k = 0; k < a.Triggers.Count; k++)
                            named.Add(new NamedTrigger($"{a.TriggerDefinitionId}/{k}@{a.Id}", $"combatants[{i}].triggers[{k}]", a.Triggers[k]));
                        break;
                }
            }
            foreach (NamedTrigger t in Combat.Statuses.NamedTriggers())
                named.Add(new NamedTrigger(t.Label, "rules." + t.Path, t.Definition));
            return TriggerGraph.Cycles(named, Combat.Statuses);
        }

        private static void Defense(Fixed value, string label, string path, List<RuleViolation> errors)
        {
            if (value < Fixed.Zero || value > CombatantStats.MaxDefense)
                errors.Add(new RuleViolation($"{label} doit être comprise entre 0 et {CombatantStats.MaxDefense}.", path));
        }

        /// <summary>
        /// Profil offensif d'un combattant : ce qui entre dans la formule de dégâts pour une de ses attaques. Correctif 7.3.1 (A73-M01) :
        /// un impact direct passe par <see cref="DamageCalculator.Compute"/> (bonus de catégorie, critique) ; un tick de DOT
        /// (<see cref="Periodic"/>) par <see cref="DamageCalculator.ComputePeriodic"/>, avec la puissance figée réelle et le mode de
        /// résistance de l'altération. La formule directe n'est pas un majorant du DOT : un multiplicateur de catégorie maximal
        /// inférieur à 1 (jusqu'à 0) la rend plus petite.
        /// </summary>
        private readonly struct Offense
        {
            public readonly string Label;
            public readonly DerivedStats Attacker;
            public readonly PowerType Power;
            public readonly DamageType Type;
            public readonly Fixed Coefficient;
            public readonly bool CanCrit;
            public readonly bool Periodic;
            public readonly bool ReducedByDefense;

            /// <summary>Sous-lot 8.1 : majorant du bonus de dégâts infligés (catégorie talent) de l'attaquant ; impacts directs seulement.</summary>
            public readonly Fixed Talent;

            public Offense(string label, DerivedStats attacker, PowerType power, DamageType type, Fixed coefficient, bool canCrit, Fixed talent = default)
                : this(label, attacker, power, type, coefficient, canCrit, false, false, talent) { }

            private Offense(string label, DerivedStats attacker, PowerType power, DamageType type, Fixed coefficient, bool canCrit,
                bool periodic, bool reducedByDefense, Fixed talent = default)
            {
                Talent = talent;
                Label = label;
                Attacker = attacker;
                Power = power;
                Type = type;
                Coefficient = coefficient;
                CanCrit = canCrit;
                Periodic = periodic;
                ReducedByDefense = reducedByDefense;
            }

            /// <summary>Tick d'un DOT : puissance figée = puissance du type de l'altération × coefficient de l'application.</summary>
            public static Offense Dot(string label, DerivedStats attacker, StatusDefinition definition, Fixed coefficient)
            {
                DamageType type = definition.DamageType!.Value;
                return new Offense(label, attacker, CombatantStats.PowerOf(type), type, coefficient, false, true,
                    definition.Resistance == StatusResistanceMode.Defense);
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
                // Champ et message d'un dépassement : les attributs du build, sauf pendant l'application des modificateurs permanents aux
                // statistiques d'un personnage (sous-lot 8.1), rattachée à sa classe.
                string overflowPath = combatant is CharacterSetup ? path + ".build.attributes" : path;
                string overflowMessage = "statistiques dérivées hors des limites numériques.";
                try
                {
                    switch (combatant)
                    {
                        case CharacterSetup character:
                            // Sous-lot 8.1 : statistiques et règles effectives par les fonctions partagées avec le moteur (modificateurs
                            // permanents) ; coefficient effectif de chaque compétence ; bonus talent du pire impact direct (permanent et
                            // conditionnels positifs à leur facteur maximal), compétences et frappes de réaction.
                            ModifierSet modifiers = ModifierSet.For(character);
                            DerivedStats stats = CombatantStats.ForCharacter(Stats, character.Build, ModifierSet.Empty);
                            stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                            stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                            overflowPath = path + ".class";
                            overflowMessage = "statistiques dérivées hors des limites numériques avec les modificateurs permanents de la classe (maxHealth, endurance.max, statusResistance).";
                            stats = CombatantStats.ForCharacter(Stats, character.Build, modifiers);
                            stats.MaxHealth.RoundToInt(RoundingMode.Floor);
                            stats.MaxEndurance.RoundToInt(RoundingMode.Floor);
                            overflowPath = path + ".build.attributes";
                            overflowMessage = "statistiques dérivées hors des limites numériques.";
                            defenses[i] = stats;
                            CombatantRules rules = CombatantRules.For(Combat, modifiers);
                            // Sous-lot 8.2 (X4) : valeur d'un bouclier posé par ce personnage, au coefficient maximal d'une application,
                            // avec sa puissance de boucliers (même fonction que le moteur).
                            if (Combat.Statuses.HasShields)
                            {
                                overflowPath = path + ".class";
                                overflowMessage = $"valeur d'un bouclier hors des limites numériques (PV max × coefficient {SkillCombatDefinition.MaxCoefficient} × (1 + shield.potencyDealt)).";
                                Shields.Value(stats.MaxHealth.RoundToInt(RoundingMode.Floor), SkillCombatDefinition.MaxCoefficient, rules.ShieldPotencyDealt);
                                overflowPath = path + ".build.attributes";
                                overflowMessage = "statistiques dérivées hors des limites numériques.";
                            }
                            Fixed talent = rules.WorstDamageDealt(Combat.ResonanceMax);
                            foreach (SkillDefinition skill in character.Class.Skills)
                                if (skill.Combat is { } combat)
                                {
                                    offenses[i].Add(new Offense($"compétence « {skill.Id} »", stats, combat.PowerType, combat.DamageType, rules.Coefficient(skill, combat), true, talent));
                                    AddDotOffenses(offenses[i], $"compétence « {skill.Id} »", stats, combat.Applies);
                                }
                            // Lot 7.3 (§4.2) : frappes et DOT des réactions, à la puissance du porteur. Un personnage peut porter toute
                            // altération du catalogue : ses déclencheurs comptent pour chacun.
                            foreach (BoundTrigger bound in character.ActiveTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {bound.Label} »", stats, bound.Definition, bound.Rank, talent);
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", stats, named.Definition, 1, talent);
                            break;
                        case DummySetup dummy:
                            defenses[i] = CombatantStats.ForDefenses(Stats, dummy.Armor, dummy.MagicResistance, dummy.StatusResistance);
                            // Lot 7.3 : un mannequin peut porter une altération à déclencheurs ; ses réactions frappent à sa puissance de base.
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", defenses[i]!, named.Definition, 1);
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
                            // Lot 7.3 : déclencheurs de l'assaillant et des altérations qu'il peut porter, à sa puissance de base.
                            for (int k = 0; k < assailant.Triggers.Count; k++)
                                AddTriggerOffenses(offenses[i], $"déclencheur « {assailant.TriggerDefinitionId}/{k} »", defenses[i]!, assailant.Triggers[k], 1);
                            foreach (NamedTrigger named in Combat.Statuses.NamedTriggers())
                                AddTriggerOffenses(offenses[i], $"déclencheur « {named.Label} »", defenses[i]!, named.Definition, 1);
                            break;
                    }
                }
                catch (OverflowException)
                {
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, overflowMessage, overflowPath));
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
            // Lot 7.2 : majorant des bonus de dégâts reçus, toutes les altérations du catalogue actives à la fois. Sous-lot 8.2 (X3) : plus
            // les dégâts infligés de tous les renforcements du catalogue, même catégorie « altération ».
            DamageBonuses worstBonuses = DamageBonuses.None.Add(BonusCategory.Alteration,
                Combat.Statuses.TotalDamageTakenIncrease() + Combat.Statuses.TotalBuffDamageDealt());
            long maxImpacts = checked(DurationMs / Combat.TickMilliseconds * Combat.MaxEventsPerTick);
            for (int i = 0; i < count; i++)
            {
                string path = $"combatants[{i}]";
                long worstHit = 0;
                string? worstLabel = null;
                bool failed = false;
                foreach (Offense offense in offenses[i])
                {
                    if (failed) break;
                    if (!TryWorstHit(offense, undefended, worstBonuses, out long hit))
                    {
                        errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, offense.Periodic
                            ? $"pire tick de {offense.Label} hors des limites numériques du moteur (formule périodique, puissance figée, aucune défense)."
                            : $"pire impact de {offense.Label} hors des limites numériques du moteur (critique, Fragilité, aucune défense).", path));
                        failed = true;
                        break;
                    }
                    if (hit > worstHit || worstLabel is null)
                    {
                        worstHit = Math.Max(worstHit, hit);
                        worstLabel = (offense.Periodic ? "tick de " : "impact de ") + offense.Label;
                    }
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
                        $"cumul des dégâts sur la durée du combat hors des limites numériques (pire impact {worstHit}, {worstLabel}, × {maxImpacts} événements comptés au plus).", path));
            }
        }

        /// <summary>
        /// Lot 7.2 : chaque tick d'un DOT est un impact sans critique dont la puissance figée vaut la puissance du type de l'altération
        /// (dégâts bruts pour un assaillant) × le coefficient de l'application. Correctif 7.3.1 (A73-M01) : son pire impact est calculé
        /// avec la formule périodique du moteur (<see cref="Offense.Dot"/>), pas avec celle des dégâts directs.
        /// </summary>
        private void AddDotOffenses(List<Offense> offenses, string owner, DerivedStats attacker, IReadOnlyList<StatusApplication> applies)
        {
            foreach (StatusApplication application in applies)
                if (Combat.Statuses.TryGet(application.Status, out StatusDefinition? definition) && definition.IsDot
                    && definition.DamageType is { } type && application.Coefficient is { } coefficient)
                    offenses.Add(Offense.Dot($"altération « {definition.Id} » de {owner}", attacker, definition, coefficient));
        }

        /// <summary>
        /// Lot 7.3 : une frappe de réaction est un impact sans critique (Q7) de coefficient × puissance du porteur ; un DOT appliqué par
        /// réaction a pour puissance figée celle du porteur. Un porteur sans puissance (mannequin, assaillant) inflige le minimum de
        /// dégâts des règles : il ne majore rien.
        /// </summary>
        private void AddTriggerOffenses(List<Offense> offenses, string owner, DerivedStats attacker, TriggerDefinition trigger, int rank, Fixed talent = default)
        {
            foreach (TriggerEffect e in trigger.Effects)
            {
                if (e.Kind == TriggerEffectKind.Strike && e.Coefficient is { } coefficient && e.PowerType is { } power && e.DamageType is { } type)
                    offenses.Add(new Offense($"frappe du {owner}", attacker, power, type, coefficient.At(rank), false, talent));
                else if (e.Kind == TriggerEffectKind.ApplyStatus && e.Status != null && e.Coefficient is { } dot
                    && Combat.Statuses.TryGet(e.Status, out StatusDefinition? definition) && definition.IsDot && definition.DamageType != null)
                    offenses.Add(Offense.Dot($"altération « {definition.Id} » du {owner}", attacker, definition, dot.At(rank)));
            }
        }

        private string DefenseField(int index, DamageType type) => Combatants[index] is CharacterSetup
            ? $"combatants[{index}]"
            : $"combatants[{index}]." + (type == DamageType.Physique ? "armor" : "magicResistance");

        /// <summary>
        /// Pire impact d'une offensive contre un défenseur, calculé par la formule exacte du moteur : <see cref="DamageCalculator.Compute"/>
        /// pour un impact direct (bonus de catégorie, critique, sans garde ni parade) ; pour un tick de DOT, puissance figée calculée
        /// comme à l'application (puissance × coefficient, même arrondi) puis <see cref="DamageCalculator.ComputePeriodic"/>, sans bonus
        /// ni critique, réduite par la défense seulement si l'altération l'est.
        /// </summary>
        private bool TryWorstHit(Offense offense, DerivedStats defender, DamageBonuses bonuses, out long hit)
        {
            try
            {
                hit = offense.Periodic
                    ? DamageCalculator.ComputePeriodic(offense.Attacker.GetPower(offense.Power) * offense.Coefficient, defender, offense.Type,
                        offense.ReducedByDefense, Damage)
                    : DamageCalculator.Compute(new DamageInput(offense.Attacker, defender, offense.Power, offense.Type, offense.Coefficient,
                        offense.Talent == Fixed.Zero ? bonuses : bonuses.Add(BonusCategory.Talent, offense.Talent), offense.CanCrit, Fixed.Zero), Damage).FinalDamage;
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
