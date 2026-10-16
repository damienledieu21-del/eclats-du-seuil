using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    /// <summary>Catégorie d'une altération (lot 7.2) : le moteur ne connaît que des catégories et des effets génériques.</summary>
    public enum StatusCategory
    {
        /// <summary>Dégâts périodiques (brûlure, saignement).</summary>
        Dot = 0,

        /// <summary>Contrôle : bloque des actions ou modifie le déplacement (étourdissement, gel, rupture de garde).</summary>
        Control = 1,

        /// <summary>Modificateur de dégâts reçus (Fragilité).</summary>
        Modifier = 2,

        /// <summary>Immunité temporaire à d'autres altérations.</summary>
        Immunity = 3,
    }

    /// <summary>Comportement d'une réapplication (A13).</summary>
    public enum StatusStacking
    {
        /// <summary>Une instance par cible ; la réapplication repart de maintenant + durée (Fragilité).</summary>
        Refresh = 0,

        /// <summary>DOT : une instance par (cible, source) ; puissance maximale conservée, fin repoussée, cadence d'origine.</summary>
        PerSourceKeepStronger = 1,

        /// <summary>Une instance par cible ; la fin la plus tardive l'emporte (contrôles).</summary>
        KeepLonger = 2,

        /// <summary>Une instance par cible ; la réapplication est ignorée tant que l'instance est active.</summary>
        Ignore = 3,
    }

    /// <summary>Ce qui réduit une altération.</summary>
    public enum StatusResistanceMode
    {
        None = 0,

        /// <summary>DOT : chaque tick est réduit par la défense du type de dégâts (armure ou résistance magique), relue à chaque tick.</summary>
        Defense = 1,

        /// <summary>Contrôle : la durée est réduite par la résistance aux altérations, K / (K + résistance).</summary>
        Duration = 2,
    }

    /// <summary>Actions bloquées tant que l'altération est active.</summary>
    [Flags]
    public enum StatusBlocks
    {
        None = 0,
        Skills = 1,
        Guard = 2,
        Parry = 4,
        Regeneration = 8,
        AssailantAttacks = 16,
    }

    /// <summary>Postures interrompues à l'application.</summary>
    [Flags]
    public enum StatusInterrupts
    {
        None = 0,
        Guard = 1,
        Parry = 2,
    }

    /// <summary>Identifiants stables des énumérations d'altérations, dans les données et le journal.</summary>
    public static class StatusIds
    {
        public static IReadOnlyDictionary<string, StatusCategory> Categories { get; } = Map(
            ("dot", StatusCategory.Dot), ("control", StatusCategory.Control), ("modifier", StatusCategory.Modifier), ("immunity", StatusCategory.Immunity));

        public static IReadOnlyDictionary<string, StatusStacking> Stackings { get; } = Map(
            ("refresh", StatusStacking.Refresh), ("perSourceKeepStronger", StatusStacking.PerSourceKeepStronger),
            ("keepLonger", StatusStacking.KeepLonger), ("ignore", StatusStacking.Ignore));

        public static IReadOnlyDictionary<string, StatusResistanceMode> Resistances { get; } = Map(
            ("none", StatusResistanceMode.None), ("defense", StatusResistanceMode.Defense), ("duration", StatusResistanceMode.Duration));

        public static IReadOnlyDictionary<string, StatusBlocks> Blocks { get; } = Map(
            ("skills", StatusBlocks.Skills), ("guard", StatusBlocks.Guard), ("parry", StatusBlocks.Parry),
            ("regeneration", StatusBlocks.Regeneration), ("assailantAttacks", StatusBlocks.AssailantAttacks));

        public static IReadOnlyDictionary<string, StatusInterrupts> Interrupts { get; } = Map(
            ("guard", StatusInterrupts.Guard), ("parry", StatusInterrupts.Parry));

        public static string Of(StatusCategory value) => Categories.First(p => p.Value == value).Key;
        public static string Of(StatusStacking value) => Stackings.First(p => p.Value == value).Key;
        public static string Of(StatusResistanceMode value) => Resistances.First(p => p.Value == value).Key;

        /// <summary>Identifiants des drapeaux présents, dans l'ordre de déclaration.</summary>
        public static IEnumerable<string> Of(StatusBlocks value) => Blocks.Where(p => (value & p.Value) != 0).Select(p => p.Key);
        public static IEnumerable<string> Of(StatusInterrupts value) => Interrupts.Where(p => (value & p.Value) != 0).Select(p => p.Key);

        private static IReadOnlyDictionary<string, T> Map<T>(params (string Id, T Value)[] entries)
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var (id, value) in entries) map.Add(id, value);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, T>(map);
        }
    }

    /// <summary>
    /// Définition d'une altération (lot 7.2), lue dans <c>rules.json</c> (bloc <c>statuses</c>). Immuable. Le moteur n'agit
    /// que sur ses effets : catégorie, durée, période, cumul, résistance, blocages, interruptions, bonus de dégâts reçus,
    /// modificateur de déplacement, état accordé à l'expiration et immunités.
    /// </summary>
    public sealed class StatusDefinition
    {
        /// <summary>Bonus maximal de dégâts directs reçus (10 = +1000 %), au niveau du plafond de catégorie des règles livrées.</summary>
        public static readonly Fixed MaxDamageTakenIncrease = Fixed.FromInt(10);

        public string Id { get; }
        public StatusCategory Category { get; }
        public long DurationMs { get; }

        /// <summary>DOT seulement : période entre deux ticks ; 0 sinon.</summary>
        public long PeriodMs { get; }

        public StatusStacking Stacking { get; }
        public StatusResistanceMode Resistance { get; }

        /// <summary>DOT seulement : type des dégâts, qui choisit la puissance de la source et la défense de la cible.</summary>
        public DamageType? DamageType { get; }

        public StatusBlocks Blocks { get; }
        public StatusInterrupts Interrupts { get; }

        /// <summary>Motif de refus d'une commande bloquée (<c>stunned</c>, <c>guardBroken</c>) ; obligatoire si l'altération bloque.</summary>
        public string? BlockReason { get; }

        /// <summary>Bonus de la catégorie « altération » sur les dégâts directs reçus (Fragilité : 0.1).</summary>
        public Fixed DamageTakenIncrease { get; }

        /// <summary>Modificateur de vitesse de déplacement (gel : −0.3). Enregistré seulement : le simulateur n'a pas d'espace.</summary>
        public Fixed MovementSpeedModifier { get; }

        /// <summary>Altération appliquée quand celle-ci expire (pas quand elle est retirée) : étourdissement → immunité.</summary>
        public string? GrantsOnExpiry { get; }

        /// <summary>Immunité : altérations dont l'application est bloquée.</summary>
        public IReadOnlyList<string> ImmuneTo { get; }

        public StatusDefinition(string id, StatusCategory category, long durationMs, StatusStacking stacking,
            StatusResistanceMode resistance = StatusResistanceMode.None, long periodMs = 0, DamageType? damageType = null,
            StatusBlocks blocks = StatusBlocks.None, StatusInterrupts interrupts = StatusInterrupts.None, string? blockReason = null,
            Fixed damageTakenIncrease = default, Fixed movementSpeedModifier = default, string? grantsOnExpiry = null,
            IEnumerable<string>? immuneTo = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Category = category;
            DurationMs = durationMs;
            Stacking = stacking;
            Resistance = resistance;
            PeriodMs = periodMs;
            DamageType = damageType;
            Blocks = blocks;
            Interrupts = interrupts;
            BlockReason = blockReason;
            DamageTakenIncrease = damageTakenIncrease;
            MovementSpeedModifier = movementSpeedModifier;
            GrantsOnExpiry = grantsOnExpiry;
            ImmuneTo = Frozen.ListOrEmpty(immuneTo, nameof(immuneTo));
        }

        public bool IsDot => Category == StatusCategory.Dot;
    }

    /// <summary>Altération portée par une compétence ou une attaque d'assaillant, appliquée à chaque cible touchée après les dégâts.</summary>
    public sealed class StatusApplication
    {
        public string Status { get; }

        /// <summary>DOT : coefficient de la puissance de la source par tick ; null pour les autres catégories (sans paramètre en 7.2).</summary>
        public Fixed? Coefficient { get; }

        public StatusApplication(string status, Fixed? coefficient = null)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            Coefficient = coefficient;
        }

        /// <summary>Contrôles propres à une liste d'applications (coefficients, doublons), sans le catalogue.</summary>
        public static void ValidateList(IReadOnlyList<StatusApplication> applies, string path, List<RuleViolation> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < applies.Count; i++)
            {
                StatusApplication a = applies[i];
                string itemPath = $"{path}[{i}]";
                if (string.IsNullOrEmpty(a.Status)) errors.Add(new RuleViolation("altération obligatoire.", itemPath + ".status"));
                else if (!seen.Add(a.Status))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"altération « {a.Status} » déjà appliquée par cette action.", itemPath + ".status"));
                if (a.Coefficient is { } c && (c < Fixed.Zero || c > Builds.SkillCombatDefinition.MaxCoefficient))
                    errors.Add(new RuleViolation($"coefficient hors de [0, {Builds.SkillCombatDefinition.MaxCoefficient}].", itemPath + ".coefficient"));
            }
        }
    }

    /// <summary>
    /// Catalogue des altérations (lot 7.2). L'ordre des définitions fait partie du contenu : il fixe la priorité des motifs
    /// de refus quand plusieurs altérations bloquent la même action (étourdissement avant rupture de garde dans les données livrées).
    /// </summary>
    public sealed class StatusCatalog
    {
        /// <summary>Nombre maximal de définitions (enveloppe technique : borne les instances par cible).</summary>
        public const int MaxDefinitions = 64;

        /// <summary>Plafond de la constante de résistance aux altérations, comme les constantes de défense (10⁹).</summary>
        public const long MaxResistanceConstant = DamageRules.MaxDefenseConstant;

        private static readonly Regex IdPattern = new Regex(@"^[a-z][A-Za-z0-9]*\z", RegexOptions.CultureInvariant);

        /// <summary>
        /// Valeurs initiales du sous-lot 7.2 (décisions du 16 octobre), <b>provisoires</b> : brûlure et saignement 6 s / 2 s,
        /// étourdissement 1,5 s puis immunité 3 s, gel 4 s à −30 % de déplacement, Fragilité et rupture de garde inchangées.
        /// </summary>
        public static readonly StatusCatalog Provisional = new StatusCatalog(new[]
        {
            new StatusDefinition("burn", StatusCategory.Dot, 6000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
                periodMs: 2000, damageType: Stats.DamageType.Magique),
            new StatusDefinition("bleed", StatusCategory.Dot, 6000, StatusStacking.PerSourceKeepStronger, StatusResistanceMode.Defense,
                periodMs: 2000, damageType: Stats.DamageType.Physique),
            new StatusDefinition("stun", StatusCategory.Control, 1500, StatusStacking.Ignore, StatusResistanceMode.Duration,
                blocks: StatusBlocks.Skills | StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.AssailantAttacks,
                interrupts: StatusInterrupts.Guard | StatusInterrupts.Parry, blockReason: "stunned", grantsOnExpiry: "stunImmunity"),
            new StatusDefinition("stunImmunity", StatusCategory.Immunity, 3000, StatusStacking.Ignore, immuneTo: new[] { "stun" }),
            new StatusDefinition("freeze", StatusCategory.Control, 4000, StatusStacking.KeepLonger, StatusResistanceMode.Duration,
                movementSpeedModifier: Fixed.Parse("-0.3")),
            new StatusDefinition("fragility", StatusCategory.Modifier, 6000, StatusStacking.Refresh, damageTakenIncrease: Fixed.FromRatio(1, 10)),
            new StatusDefinition("guardBreak", StatusCategory.Control, 3000, StatusStacking.Ignore,
                blocks: StatusBlocks.Guard | StatusBlocks.Parry | StatusBlocks.Regeneration, blockReason: "guardBroken"),
        });

        private readonly Dictionary<string, StatusDefinition> _byId = new Dictionary<string, StatusDefinition>(StringComparer.Ordinal);

        public IReadOnlyList<StatusDefinition> Definitions { get; }

        public StatusCatalog(IEnumerable<StatusDefinition> definitions)
        {
            Definitions = Frozen.List(definitions, nameof(definitions));
            foreach (StatusDefinition d in Definitions)
                if (!_byId.ContainsKey(d.Id)) _byId.Add(d.Id, d);   // un doublon est signalé par Validate
        }

        public bool TryGet(string id, out StatusDefinition definition) => _byId.TryGetValue(id, out definition!);

        public StatusDefinition this[string id] => _byId[id];

        /// <summary>Majorant de la somme des bonus de dégâts reçus : toutes les altérations actives à la fois.</summary>
        public Fixed TotalDamageTakenIncrease()
        {
            Fixed total = Fixed.Zero;
            foreach (StatusDefinition d in Definitions) total += d.DamageTakenIncrease;
            return total;
        }

        /// <summary>
        /// Incohérences du catalogue ; chemins de la forme <c>statuses[2].periodMs</c>. <paramref name="tickMs"/> est la durée du
        /// tick des règles ; si elle est invalide, les contrôles de multiple sont omis (l'erreur est signalée sur le tick).
        /// </summary>
        public IReadOnlyList<RuleViolation> Validate(int tickMs)
        {
            var errors = new List<RuleViolation>();
            bool tickValid = tickMs > 0 && 1000 % tickMs == 0;
            if (Definitions.Count > MaxDefinitions)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{Definitions.Count} altérations, au plus {MaxDefinitions}.", "statuses"));
            var firstIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < Definitions.Count; i++)
            {
                StatusDefinition d = Definitions[i];
                string p = $"statuses[{i}]";
                if (!IdPattern.IsMatch(d.Id))
                    errors.Add(new RuleViolation($"identifiant « {d.Id} » invalide : lettres et chiffres, commençant par une minuscule.", p + ".id"));
                else if (firstIndex.TryGetValue(d.Id, out int first))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"altération « {d.Id} » déjà définie (statuses[{first}]).", p + ".id"));
                else
                    firstIndex[d.Id] = i;

                if (!Enum.IsDefined(typeof(StatusCategory), d.Category)) errors.Add(new RuleViolation("catégorie inconnue.", p + ".category"));
                if (!Enum.IsDefined(typeof(StatusStacking), d.Stacking)) errors.Add(new RuleViolation("mode de cumul inconnu.", p + ".stacking"));
                if (!Enum.IsDefined(typeof(StatusResistanceMode), d.Resistance)) errors.Add(new RuleViolation("résistance inconnue.", p + ".resistance"));

                Duration(d.DurationMs, p + ".durationMs", tickMs, tickValid, errors);
                if (d.IsDot)
                {
                    if (d.PeriodMs <= 0)
                        errors.Add(new RuleViolation("une altération périodique exige une période strictement positive.", p + ".periodMs"));
                    else if (tickValid && d.PeriodMs % tickMs != 0)
                        errors.Add(new RuleViolation($"{d.PeriodMs} ms n'est pas un multiple de la durée du tick ({tickMs} ms).", p + ".periodMs"));
                    else if (d.DurationMs > 0 && d.DurationMs % d.PeriodMs != 0)
                        errors.Add(new RuleViolation($"la durée ({d.DurationMs} ms) doit être un multiple de la période ({d.PeriodMs} ms).", p + ".durationMs", p + ".periodMs"));
                    if (d.DamageType is null || !Enum.IsDefined(typeof(DamageType), d.DamageType.Value))
                        errors.Add(new RuleViolation("une altération périodique exige un type de dégâts.", p + ".damageType"));
                    if (d.Stacking != StatusStacking.PerSourceKeepStronger)
                        errors.Add(new RuleViolation("une altération périodique se cumule par source (perSourceKeepStronger).", p + ".stacking"));
                    if (d.Resistance == StatusResistanceMode.Duration)
                        errors.Add(new RuleViolation("la résistance d'une altération périodique est none ou defense : la résistance aux altérations ne réduit que la durée des contrôles.", p + ".resistance"));
                }
                else
                {
                    if (d.PeriodMs != 0) errors.Add(new RuleViolation("seule une altération périodique a une période.", p + ".periodMs"));
                    if (d.DamageType != null) errors.Add(new RuleViolation("seule une altération périodique a un type de dégâts.", p + ".damageType"));
                    if (d.Stacking == StatusStacking.PerSourceKeepStronger)
                        errors.Add(new RuleViolation("le cumul par source est réservé aux altérations périodiques.", p + ".stacking"));
                    if (d.Resistance == StatusResistanceMode.Defense)
                        errors.Add(new RuleViolation("la résistance par la défense est réservée aux altérations périodiques.", p + ".resistance"));
                    if (d.Resistance == StatusResistanceMode.Duration && d.Category != StatusCategory.Control)
                        errors.Add(new RuleViolation("la résistance aux altérations ne réduit que la durée des contrôles.", p + ".resistance"));
                }

                if (d.Blocks != StatusBlocks.None && d.BlockReason is null)
                    errors.Add(new RuleViolation("une altération qui bloque des actions exige un motif de refus.", p + ".blockReason"));
                if (d.BlockReason != null && (d.Blocks == StatusBlocks.None || !IdPattern.IsMatch(d.BlockReason)))
                    errors.Add(new RuleViolation(d.Blocks == StatusBlocks.None
                        ? "un motif de refus n'a de sens que si l'altération bloque des actions."
                        : $"motif « {d.BlockReason} » invalide : lettres et chiffres, commençant par une minuscule.", p + ".blockReason"));
                if (d.DamageTakenIncrease < Fixed.Zero || d.DamageTakenIncrease > StatusDefinition.MaxDamageTakenIncrease)
                    errors.Add(new RuleViolation($"doit être comprise entre 0 et {StatusDefinition.MaxDamageTakenIncrease}.", p + ".damageTakenIncrease"));
                if (d.MovementSpeedModifier < -Fixed.One || d.MovementSpeedModifier > Fixed.One)
                    errors.Add(new RuleViolation("doit être compris entre -1 et 1.", p + ".movementSpeedModifier"));

                if (d.Category == StatusCategory.Immunity && d.ImmuneTo.Count == 0)
                    errors.Add(new RuleViolation("une immunité désigne au moins une altération.", p + ".immuneTo"));
                if (d.Category != StatusCategory.Immunity && d.ImmuneTo.Count > 0)
                    errors.Add(new RuleViolation("seule une immunité bloque d'autres altérations.", p + ".immuneTo"));
            }

            // Références : après les identifiants, pour signaler une référence inconnue même déclarée plus loin.
            for (int i = 0; i < Definitions.Count; i++)
            {
                StatusDefinition d = Definitions[i];
                string p = $"statuses[{i}]";
                if (d.GrantsOnExpiry is { } granted)
                {
                    if (!_byId.TryGetValue(granted, out StatusDefinition? target))
                        errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {granted} ».", p + ".grantsOnExpiry"));
                    else if (granted == d.Id)
                        errors.Add(new RuleViolation("une altération ne peut pas s'accorder elle-même.", p + ".grantsOnExpiry"));
                    else if (target.GrantsOnExpiry != null)
                        errors.Add(new RuleViolation($"« {granted} » accorde déjà une autre altération : profondeur 1 au plus, sans chaîne.", p + ".grantsOnExpiry"));
                    else if (target.IsDot)
                        errors.Add(new RuleViolation("une altération périodique ne peut pas être accordée : elle n'a pas de source qui fixe sa puissance.", p + ".grantsOnExpiry"));
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int k = 0; k < d.ImmuneTo.Count; k++)
                {
                    string id = d.ImmuneTo[k];
                    string kp = $"{p}.immuneTo[{k}]";
                    if (!seen.Add(id))
                        errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"« {id} » déjà cité.", kp));
                    else if (!_byId.TryGetValue(id, out StatusDefinition? target))
                        errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {id} ».", kp));
                    else if (target.Category == StatusCategory.Immunity)
                        errors.Add(new RuleViolation("une immunité ne peut pas bloquer une immunité (aucun cycle possible).", kp));
                }
            }
            return errors;
        }

        private static void Duration(long value, string path, int tickMs, bool tickValid, List<RuleViolation> errors)
        {
            if (value <= 0)
                errors.Add(new RuleViolation("doit être strictement positive.", path));
            else if (value > CombatRules.AbsoluteMaxDurationMs)
                errors.Add(new RuleViolation($"ne peut pas dépasser {CombatRules.AbsoluteMaxDurationMs} ms (24 h).", path));
            else if (tickValid && value % tickMs != 0)
                errors.Add(new RuleViolation($"{value} ms n'est pas un multiple de la durée du tick ({tickMs} ms).", path));
        }

        /// <summary>
        /// Une application désigne-t-elle une altération applicable ? Les immunités et l'altération de la rupture de garde
        /// ne sont appliquées que par leur mécanique ; un DOT exige un coefficient, les autres catégories n'en prennent pas.
        /// </summary>
        public string? CheckApplication(StatusApplication application, string guardBreakStatus)
        {
            if (!TryGet(application.Status, out StatusDefinition? d)) return $"altération inconnue « {application.Status} ».";
            if (d.Category == StatusCategory.Immunity) return $"« {d.Id} » est une immunité : elle n'est accordée que par l'expiration d'une autre altération.";
            if (d.Id == guardBreakStatus) return $"« {d.Id} » n'est appliquée que par la mécanique de garde (combat.guard.breakStatus).";
            if (d.IsDot && application.Coefficient is null) return $"« {d.Id} » est périodique : coefficient obligatoire.";
            if (!d.IsDot && application.Coefficient != null) return $"« {d.Id} » n'est pas périodique : pas de coefficient en 7.2.";
            return null;
        }
    }
}
