using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Numerics;
using EdS.Domain.Stats;

namespace EdS.Domain.Combat
{
    // Lot 7.3 : déclencheurs génériques (PLAN_LOT_7_3.md §4). Un déclencheur observe un type d'événement de jeu, filtre son
    // instantané, puis produit 1 à 4 effets génériques comme une seule réaction. Ce fichier ne contient que les données et leur
    // validation de gameplay : l'exécution est dans le moteur (EdS.Simulation).

    /// <summary>
    /// Type d'abonnement (§1.2) : l'événement de jeu observé. Les diagnostics (<c>stateBlocked</c>, refus, suppressions,
    /// annulations, enveloppe) n'ont aucun type d'abonnement : ils ne déclenchent jamais rien (C3, C4).
    /// </summary>
    public enum TriggerEvent
    {
        /// <summary><c>skillCast</c> ou <c>attackStarted</c> : après paiement, avant les impacts.</summary>
        SkillCast = 0,

        /// <summary><c>damage</c> d'un impact direct, vu par sa source.</summary>
        HitDealt = 1,

        /// <summary><c>damage</c> d'un impact direct, vu par sa cible.</summary>
        HitTaken = 2,

        /// <summary><c>blocked</c> : blocage normal par la garde.</summary>
        Block = 3,

        /// <summary><c>parrySucceeded</c> : parade parfaite réussie (une fois par fenêtre).</summary>
        PerfectParry = 4,

        /// <summary><c>guardBroken</c> : rupture de garde.</summary>
        GuardBroken = 5,

        /// <summary><c>resourceGained</c> naturel ; un gain produit par une réaction <c>gainResource</c> n'est jamais observable (Q8).</summary>
        ResourceGained = 6,

        /// <summary><c>stateApplied</c>, renouvellements compris.</summary>
        StateApplied = 7,

        /// <summary><c>stateExpired</c>.</summary>
        StateExpired = 8,

        /// <summary><c>stateRemoved</c> (retrait au décès).</summary>
        StateRemoved = 9,

        /// <summary><c>dotDamage</c> : tick d'une altération périodique.</summary>
        DotTick = 10,

        /// <summary><c>died</c> : observé par la source de l'effet mortel (le mort ne réagit pas).</summary>
        Death = 11,
    }

    /// <summary>Rôle du porteur dans l'événement observé.</summary>
    public enum TriggerRole
    {
        /// <summary>Le porteur est l'acteur (lancer, gain de ressource).</summary>
        Self = 0,

        /// <summary>Le porteur est la source (il inflige, il applique, il tue).</summary>
        Source = 1,

        /// <summary>Le porteur est la cible (il subit, il bloque, il pare, il porte l'altération).</summary>
        Target = 2,
    }

    /// <summary>Effet générique du premier palier (§4.2).</summary>
    public enum TriggerEffectKind
    {
        /// <summary>Impact complet en phase de résolution, défenses comprises, sans critique (Q7).</summary>
        Strike = 0,

        /// <summary>Application d'une altération du catalogue, dans la phase courante.</summary>
        ApplyStatus = 1,

        /// <summary>Gain de ressource du porteur, dans la phase courante ; jamais observable (Q8).</summary>
        GainResource = 2,
    }

    /// <summary>Cible d'un effet, résolue à partir de l'instantané de l'événement observé.</summary>
    public enum TriggerTarget
    {
        EventSource = 0,
        EventTarget = 1,

        /// <summary>Le porteur du déclencheur (applyStatus seulement ; implicite pour gainResource).</summary>
        Bearer = 2,

        /// <summary>Tous les ennemis vivants du porteur, dans l'ordre de leurs identifiants.</summary>
        AllEnemies = 3,
    }

    /// <summary>Ressource d'un gain de réaction.</summary>
    public enum TriggerResource
    {
        RampartCharges = 0,
        Resonance = 1,
        Endurance = 2,
    }

    /// <summary>Défense d'un impact, telle que journalisée dans <c>damage.defense</c>.</summary>
    public enum HitDefense
    {
        None = 0,
        Guard = 1,
        Parry = 2,
        Unblockable = 3,
        GuardBroken = 4,
    }

    /// <summary>
    /// Origine d'un déclencheur. L'ordre de déclaration est le 3ᵉ critère de l'ordre des abonnés (§5.4). <see cref="Equipment"/>
    /// est réservé (Q9) : aucun lecteur ne le produit en 7.3.
    /// </summary>
    public enum TriggerOrigin
    {
        Talent = 0,
        Passive = 1,
        Skill = 2,
        Equipment = 3,
        Status = 4,

        /// <summary>Déclencheurs déclarés par un assaillant de scénario (Q4), pour les tests et entraînements.</summary>
        Assailant = 5,
    }

    /// <summary>Identifiants stables des énumérations de déclencheurs, dans les données et le journal.</summary>
    public static class TriggerIds
    {
        public static IReadOnlyDictionary<string, TriggerEvent> Events { get; } = Map(
            ("onSkillCast", TriggerEvent.SkillCast), ("onHitDealt", TriggerEvent.HitDealt), ("onHitTaken", TriggerEvent.HitTaken),
            ("onBlock", TriggerEvent.Block), ("onPerfectParry", TriggerEvent.PerfectParry), ("onGuardBroken", TriggerEvent.GuardBroken),
            ("onResourceGained", TriggerEvent.ResourceGained), ("onStateApplied", TriggerEvent.StateApplied),
            ("onStateExpired", TriggerEvent.StateExpired), ("onStateRemoved", TriggerEvent.StateRemoved),
            ("onDotTick", TriggerEvent.DotTick), ("onDeath", TriggerEvent.Death));

        public static IReadOnlyDictionary<string, TriggerRole> Roles { get; } = Map(
            ("self", TriggerRole.Self), ("source", TriggerRole.Source), ("target", TriggerRole.Target));

        public static IReadOnlyDictionary<string, TriggerEffectKind> Effects { get; } = Map(
            ("strike", TriggerEffectKind.Strike), ("applyStatus", TriggerEffectKind.ApplyStatus), ("gainResource", TriggerEffectKind.GainResource));

        public static IReadOnlyDictionary<string, TriggerTarget> Targets { get; } = Map(
            ("eventSource", TriggerTarget.EventSource), ("eventTarget", TriggerTarget.EventTarget),
            ("bearer", TriggerTarget.Bearer), ("allEnemies", TriggerTarget.AllEnemies));

        public static IReadOnlyDictionary<string, TriggerResource> Resources { get; } = Map(
            ("rampartCharges", TriggerResource.RampartCharges), ("resonance", TriggerResource.Resonance), ("endurance", TriggerResource.Endurance));

        public static IReadOnlyDictionary<string, HitDefense> Defenses { get; } = Map(
            ("none", HitDefense.None), ("guard", HitDefense.Guard), ("parry", HitDefense.Parry),
            ("unblockable", HitDefense.Unblockable), ("guardBroken", HitDefense.GuardBroken));

        public static IReadOnlyDictionary<string, TriggerOrigin> Origins { get; } = Map(
            ("talent", TriggerOrigin.Talent), ("passive", TriggerOrigin.Passive), ("skill", TriggerOrigin.Skill),
            ("equipment", TriggerOrigin.Equipment), ("status", TriggerOrigin.Status), ("assailant", TriggerOrigin.Assailant));

        public static string Of(TriggerEvent value) => Events.First(p => p.Value == value).Key;
        public static string Of(TriggerRole value) => Roles.First(p => p.Value == value).Key;
        public static string Of(TriggerEffectKind value) => Effects.First(p => p.Value == value).Key;
        public static string Of(TriggerTarget value) => Targets.First(p => p.Value == value).Key;
        public static string Of(TriggerResource value) => Resources.First(p => p.Value == value).Key;
        public static string Of(HitDefense value) => Defenses.First(p => p.Value == value).Key;
        public static string Of(TriggerOrigin value) => Origins.First(p => p.Value == value).Key;

        private static IReadOnlyDictionary<string, T> Map<T>(params (string Id, T Value)[] entries)
        {
            var map = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var (id, value) in entries) map.Add(id, value);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, T>(map);
        }
    }

    /// <summary>
    /// Valeur numérique d'un effet, éventuellement par rang (§4.2) : <c>base + perRank × (rang − 1)</c>, en virgule fixe,
    /// calculée au chargement du combat. Seul un talent a des rangs ; ailleurs le rang vaut 1 et <see cref="PerRank"/> 0.
    /// </summary>
    public readonly struct RankedValue : IEquatable<RankedValue>
    {
        public Fixed Base { get; }
        public Fixed PerRank { get; }

        public RankedValue(Fixed @base, Fixed perRank = default)
        {
            Base = @base;
            PerRank = perRank;
        }

        public static implicit operator RankedValue(Fixed value) => new RankedValue(value);

        /// <summary>Valeur au rang donné (≥ 1) ; arithmétique vérifiée (bornée par la validation).</summary>
        public Fixed At(int rank) => Base + PerRank * (long)(rank - 1);

        public bool Equals(RankedValue other) => Base == other.Base && PerRank == other.PerRank;
        public override bool Equals(object? obj) => obj is RankedValue other && Equals(other);
        public override int GetHashCode() => Base.GetHashCode() * 31 + PerRank.GetHashCode();
        public override string ToString() => PerRank == Fixed.Zero ? Base.ToString() : $"{Base}+{PerRank}/rang";
    }

    /// <summary>Filtre facultatif d'un déclencheur ; chaque critère absent laisse tout passer. Lu seulement dans l'instantané.</summary>
    public sealed class TriggerFilter
    {
        public static readonly TriggerFilter None = new TriggerFilter();

        /// <summary>Compétences ou attaques (identifiants) ; vide = toutes.</summary>
        public IReadOnlyList<string> Skills { get; }

        public DamageType? DamageType { get; }

        /// <summary>Défenses de l'impact ; vide = toutes.</summary>
        public IReadOnlyList<HitDefense> Defenses { get; }

        /// <summary>Identifiant d'altération.</summary>
        public string? Status { get; }

        public StatusCategory? Category { get; }
        public TriggerResource? Resource { get; }
        public bool? Renewed { get; }

        public TriggerFilter(IEnumerable<string>? skills = null, DamageType? damageType = null, IEnumerable<HitDefense>? defenses = null,
            string? status = null, StatusCategory? category = null, TriggerResource? resource = null, bool? renewed = null)
        {
            Skills = Frozen.ListOrEmpty(skills, nameof(skills));
            DamageType = damageType;
            Defenses = Frozen.ListOrEmpty(defenses, nameof(defenses));
            Status = status;
            Category = category;
            Resource = resource;
            Renewed = renewed;
        }

        public bool IsEmpty => Skills.Count == 0 && DamageType is null && Defenses.Count == 0 && Status is null && Category is null
            && Resource is null && Renewed is null;
    }

    /// <summary>
    /// Un effet de réaction. Les champs non concernés par <see cref="Kind"/> restent nuls ; la validation refuse ceux qui ne
    /// conviennent pas (un <c>status</c> sur un <c>strike</c>, par exemple).
    /// </summary>
    public sealed class TriggerEffect
    {
        public TriggerEffectKind Kind { get; }

        /// <summary>Cible ; null pour <c>gainResource</c> (toujours le porteur).</summary>
        public TriggerTarget? Target { get; }

        /// <summary><c>strike</c> : coefficient de la puissance du porteur ; <c>applyStatus</c> d'un DOT : coefficient par tick.</summary>
        public RankedValue? Coefficient { get; }

        /// <summary><c>strike</c> : puissance du porteur utilisée (0 pour un porteur sans puissance : mannequin, assaillant).</summary>
        public PowerType? PowerType { get; }

        public DamageType? DamageType { get; }

        /// <summary><c>strike</c> : ignore garde et parade.</summary>
        public bool Unblockable { get; }

        /// <summary><c>applyStatus</c> : altération du catalogue.</summary>
        public string? Status { get; }

        public TriggerResource? Resource { get; }

        /// <summary><c>gainResource</c> : quantité entière (par rang pour un talent).</summary>
        public RankedValue? Amount { get; }

        public TriggerEffect(TriggerEffectKind kind, TriggerTarget? target = null, RankedValue? coefficient = null, PowerType? powerType = null,
            DamageType? damageType = null, bool unblockable = false, string? status = null, TriggerResource? resource = null, RankedValue? amount = null)
        {
            Kind = kind;
            Target = target;
            Coefficient = coefficient;
            PowerType = powerType;
            DamageType = damageType;
            Unblockable = unblockable;
            Status = status;
            Resource = resource;
            Amount = amount;
        }

        public static TriggerEffect Strike(TriggerTarget target, RankedValue coefficient, PowerType powerType = Stats.PowerType.Physique,
            DamageType damageType = Stats.DamageType.Physique, bool unblockable = false) =>
            new TriggerEffect(TriggerEffectKind.Strike, target, coefficient, powerType, damageType, unblockable);

        public static TriggerEffect ApplyStatus(TriggerTarget target, string status, RankedValue? coefficient = null) =>
            new TriggerEffect(TriggerEffectKind.ApplyStatus, target, coefficient, status: status);

        public static TriggerEffect GainResource(TriggerResource resource, RankedValue amount) =>
            new TriggerEffect(TriggerEffectKind.GainResource, resource: resource, amount: amount);
    }

    /// <summary>
    /// Déclencheur en données (§4.1) : type d'abonnement, rôle du porteur, filtre, recharge interne et 1 à 4 effets exécutés
    /// dans l'ordre comme une seule réaction. Immuable. Porté par un talent, une compétence (passive, active ou ultime), une
    /// altération ou un assaillant de scénario ; l'équipement est réservé (Q9).
    /// </summary>
    public sealed class TriggerDefinition
    {
        /// <summary>Nombre maximal de déclencheurs par définition-mère.</summary>
        public const int MaxPerOwner = 8;

        /// <summary>Nombre maximal d'effets par déclencheur.</summary>
        public const int MaxEffects = 4;

        /// <summary>Gain maximal d'une réaction, comme les coûts et gains des compétences (correctif 6.1).</summary>
        public const int MaxAmount = SkillCombatDefinition.MaxCost;

        public TriggerEvent On { get; }

        /// <summary>Rôle du porteur ; null = le rôle unique du type d'abonnement (voir <see cref="AllowedRoles"/>).</summary>
        public TriggerRole? Role { get; }

        public TriggerFilter Filter { get; }

        /// <summary>Recharge interne (§4.5) : intervalle minimal entre deux réactions acceptées ; 0 par défaut.</summary>
        public long CooldownMs { get; }

        public IReadOnlyList<TriggerEffect> Effects { get; }

        public TriggerDefinition(TriggerEvent on, IEnumerable<TriggerEffect> effects, TriggerRole? role = null, TriggerFilter? filter = null, long cooldownMs = 0)
        {
            On = on;
            Role = role;
            Filter = filter ?? TriggerFilter.None;
            CooldownMs = cooldownMs;
            Effects = Frozen.List(effects, nameof(effects));
        }

        /// <summary>Rôle effectif : le rôle déclaré, sinon le rôle unique du type ; null si le type en admet plusieurs.</summary>
        public TriggerRole? EffectiveRole => Role ?? (AllowedRoles(On) is { Count: 1 } roles ? roles[0] : (TriggerRole?)null);

        /// <summary>Rôles admis par type d'abonnement.</summary>
        public static IReadOnlyList<TriggerRole> AllowedRoles(TriggerEvent on) => on switch
        {
            TriggerEvent.SkillCast or TriggerEvent.ResourceGained => SelfOnly,
            TriggerEvent.HitDealt or TriggerEvent.Death => SourceOnly,
            TriggerEvent.HitTaken => TargetOnly,
            _ => SourceOrTarget,
        };

        private static readonly IReadOnlyList<TriggerRole> SelfOnly = Frozen.List(new[] { TriggerRole.Self }, nameof(SelfOnly));
        private static readonly IReadOnlyList<TriggerRole> SourceOnly = Frozen.List(new[] { TriggerRole.Source }, nameof(SourceOnly));
        private static readonly IReadOnlyList<TriggerRole> TargetOnly = Frozen.List(new[] { TriggerRole.Target }, nameof(TargetOnly));
        private static readonly IReadOnlyList<TriggerRole> SourceOrTarget = Frozen.List(new[] { TriggerRole.Source, TriggerRole.Target }, nameof(SourceOrTarget));

        private static readonly Regex AttackIdPattern = new Regex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        private static bool Allows(TriggerEvent on, string criterion) => criterion switch
        {
            "skills" => on is TriggerEvent.SkillCast or TriggerEvent.HitDealt or TriggerEvent.HitTaken or TriggerEvent.Block or TriggerEvent.PerfectParry,
            "damageType" => on is TriggerEvent.HitDealt or TriggerEvent.HitTaken or TriggerEvent.Block or TriggerEvent.PerfectParry or TriggerEvent.DotTick,
            "defense" => on is TriggerEvent.HitDealt or TriggerEvent.HitTaken,
            "status" or "category" => on is TriggerEvent.StateApplied or TriggerEvent.StateExpired or TriggerEvent.StateRemoved or TriggerEvent.DotTick,
            "resource" => on == TriggerEvent.ResourceGained,
            "renewed" => on == TriggerEvent.StateApplied,
            _ => false,
        };

        /// <summary>
        /// Contrôles de gameplay d'une liste de déclencheurs qui ne dépendent ni du catalogue des altérations ni de la durée du tick
        /// (§4.1) : nombre, type, rôle et filtre compatibles, effets bien formés, valeurs bornées à chaque rang. Chemins de la
        /// forme <c>{path}[i].effects[k].coefficient</c>. <paramref name="maxRank"/> vaut le rang maximal d'un talent, 1 ailleurs ;
        /// <paramref name="ranked"/> autorise les valeurs par rang (talents seulement).
        /// </summary>
        public static void ValidateList(IReadOnlyList<TriggerDefinition> triggers, string path, int maxRank, bool ranked, List<RuleViolation> errors)
        {
            if (triggers.Count > MaxPerOwner)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{triggers.Count} déclencheurs, au plus {MaxPerOwner} par définition.", path));
            int topRank = Math.Max(1, Math.Min(maxRank, TalentDefinition.MaxRankLimit));
            for (int i = 0; i < triggers.Count; i++)
                triggers[i].ValidateShape($"{path}[{i}]", topRank, ranked, errors);
        }

        private void ValidateShape(string p, int topRank, bool ranked, List<RuleViolation> errors)
        {
            if (!Enum.IsDefined(typeof(TriggerEvent), On))
            {
                errors.Add(new RuleViolation("type d'abonnement inconnu.", p + ".on"));
                return;
            }
            IReadOnlyList<TriggerRole> roles = AllowedRoles(On);
            string on = TriggerIds.Of(On);
            if (Role is { } role)
            {
                if (!roles.Contains(role))
                    errors.Add(new RuleViolation(On == TriggerEvent.Death && role == TriggerRole.Target
                        ? "onDeath : le mort ne réagit pas ; seul le rôle source (celui qui tue) est admis."
                        : $"rôle « {(Enum.IsDefined(typeof(TriggerRole), role) ? TriggerIds.Of(role) : role.ToString())} » incompatible avec {on} ; rôles admis : {string.Join(", ", roles.Select(TriggerIds.Of))}.", p + ".role"));
            }
            else if (roles.Count > 1)
                errors.Add(new RuleViolation($"{on} admet plusieurs rôles ({string.Join(", ", roles.Select(TriggerIds.Of))}) : rôle obligatoire.", p + ".role"));

            TriggerFilter f = Filter;
            void Criterion(bool present, string name)
            {
                if (present && !Allows(On, name))
                    errors.Add(new RuleViolation($"critère « {name} » sans objet pour {on}.", $"{p}.filter.{name}"));
            }
            Criterion(f.Skills.Count > 0, "skills");
            Criterion(f.DamageType != null, "damageType");
            Criterion(f.Defenses.Count > 0, "defense");
            Criterion(f.Status != null, "status");
            Criterion(f.Category != null, "category");
            Criterion(f.Resource != null, "resource");
            Criterion(f.Renewed != null, "renewed");
            var seenSkills = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < f.Skills.Count; k++)
            {
                string id = f.Skills[k];
                string kp = $"{p}.filter.skills[{k}]";
                if (!StableId.HasPrefix(id, "skill") && !(id != null && AttackIdPattern.IsMatch(id)))
                    errors.Add(new RuleViolation($"identifiant « {id} » invalide : compétence « skill.… » ou attaque d'assaillant attendue.", kp));
                else if (!seenSkills.Add(id!))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"« {id} » déjà cité.", kp));
            }
            if (f.DamageType is { } dt && !Enum.IsDefined(typeof(DamageType), dt)) errors.Add(new RuleViolation("type de dégâts inconnu.", p + ".filter.damageType"));
            var seenDefenses = new HashSet<HitDefense>();
            for (int k = 0; k < f.Defenses.Count; k++)
                if (!Enum.IsDefined(typeof(HitDefense), f.Defenses[k]) || !seenDefenses.Add(f.Defenses[k]))
                    errors.Add(new RuleViolation("défense inconnue ou déjà citée.", $"{p}.filter.defense[{k}]"));
            if (f.Category is { } cat && !Enum.IsDefined(typeof(StatusCategory), cat)) errors.Add(new RuleViolation("catégorie inconnue.", p + ".filter.category"));
            if (f.Status != null && f.Category != null)
                errors.Add(new RuleViolation("une altération précise ou une catégorie, pas les deux.", p + ".filter.status", p + ".filter.category"));
            if (f.Resource is { } res && (!Enum.IsDefined(typeof(TriggerResource), res) || res == TriggerResource.Endurance))
                errors.Add(new RuleViolation("ressource observable : rampartCharges ou resonance (la régénération d'endurance n'est pas un gain observable).", p + ".filter.resource"));

            if (CooldownMs < 0 || CooldownMs > CombatRules.AbsoluteMaxDurationMs)
                errors.Add(new RuleViolation($"recharge interne hors de [0, {CombatRules.AbsoluteMaxDurationMs}] ms.", p + ".cooldownMs"));

            if (Effects.Count == 0 || Effects.Count > MaxEffects)
                errors.Add(new RuleViolation($"de 1 à {MaxEffects} effets.", p + ".effects"));
            for (int k = 0; k < Effects.Count; k++)
                ValidateEffect(Effects[k], $"{p}.effects[{k}]", topRank, ranked, errors);
        }

        private void ValidateEffect(TriggerEffect e, string p, int topRank, bool ranked, List<RuleViolation> errors)
        {
            void Forbidden(bool present, string field)
            {
                if (present) errors.Add(new RuleViolation($"champ sans objet pour l'effet {TriggerIds.Of(e.Kind)}.", $"{p}.{field}"));
            }
            void Ranked(RankedValue value, string field, Fixed max, bool integer)
            {
                if (!ranked && value.PerRank != Fixed.Zero)
                    errors.Add(new RuleViolation("seul un talent a des valeurs par rang (perRank).", $"{p}.{field}"));
                Fixed low, high;
                try
                {
                    low = value.At(1);
                    high = value.At(ranked ? topRank : 1);
                }
                catch (OverflowException)
                {
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit, "valeur par rang hors des limites numériques.", $"{p}.{field}"));
                    return;
                }
                if (low < Fixed.Zero || high < Fixed.Zero || low > max || high > max)
                    errors.Add(new RuleViolation($"valeur hors de [0, {max}] à un rang possible (1 à {(ranked ? topRank : 1)}).", $"{p}.{field}"));
                else if (integer && (value.Base.Raw % Fixed.Scale != 0 || value.PerRank.Raw % Fixed.Scale != 0))
                    errors.Add(new RuleViolation("quantité entière attendue (base et perRank).", $"{p}.{field}"));
            }

            if (!Enum.IsDefined(typeof(TriggerEffectKind), e.Kind))
            {
                errors.Add(new RuleViolation("effet inconnu.", p + ".type"));
                return;
            }
            if (e.Target is { } t && !Enum.IsDefined(typeof(TriggerTarget), t)) errors.Add(new RuleViolation("cible inconnue.", p + ".target"));
            switch (e.Kind)
            {
                case TriggerEffectKind.Strike:
                    if (e.Target is null) errors.Add(new RuleViolation("cible obligatoire : eventSource, eventTarget ou allEnemies.", p + ".target"));
                    else if (e.Target == TriggerTarget.Bearer) errors.Add(new RuleViolation("un porteur ne se frappe pas lui-même : eventSource, eventTarget ou allEnemies.", p + ".target"));
                    if (e.Coefficient is { } c) Ranked(c, "coefficient", SkillCombatDefinition.MaxCoefficient, false);
                    else errors.Add(new RuleViolation("coefficient obligatoire.", p + ".coefficient"));
                    if (e.PowerType is null || !Enum.IsDefined(typeof(PowerType), e.PowerType.Value)) errors.Add(new RuleViolation("type de puissance obligatoire.", p + ".powerType"));
                    if (e.DamageType is null || !Enum.IsDefined(typeof(DamageType), e.DamageType.Value)) errors.Add(new RuleViolation("type de dégâts obligatoire.", p + ".damageType"));
                    Forbidden(e.Status != null, "status");
                    Forbidden(e.Resource != null, "resource");
                    Forbidden(e.Amount != null, "amount");
                    break;
                case TriggerEffectKind.ApplyStatus:
                    if (e.Target is null) errors.Add(new RuleViolation("cible obligatoire : eventSource, eventTarget, bearer ou allEnemies.", p + ".target"));
                    if (string.IsNullOrEmpty(e.Status)) errors.Add(new RuleViolation("altération obligatoire.", p + ".status"));
                    if (e.Coefficient is { } dot) Ranked(dot, "coefficient", SkillCombatDefinition.MaxCoefficient, false);
                    Forbidden(e.PowerType != null, "powerType");
                    Forbidden(e.DamageType != null, "damageType");
                    Forbidden(e.Unblockable, "unblockable");
                    Forbidden(e.Resource != null, "resource");
                    Forbidden(e.Amount != null, "amount");
                    break;
                case TriggerEffectKind.GainResource:
                    if (e.Target is { } gt && gt != TriggerTarget.Bearer) errors.Add(new RuleViolation("un gain de ressource revient toujours au porteur.", p + ".target"));
                    if (e.Resource is null || !Enum.IsDefined(typeof(TriggerResource), e.Resource.Value)) errors.Add(new RuleViolation("ressource obligatoire.", p + ".resource"));
                    if (e.Amount is { } a) Ranked(a, "amount", Fixed.FromInt(MaxAmount), true);
                    else errors.Add(new RuleViolation("quantité obligatoire.", p + ".amount"));
                    Forbidden(e.Coefficient != null, "coefficient");
                    Forbidden(e.PowerType != null, "powerType");
                    Forbidden(e.DamageType != null, "damageType");
                    Forbidden(e.Unblockable, "unblockable");
                    Forbidden(e.Status != null, "status");
                    break;
            }
            if (On == TriggerEvent.Death && e.Target == TriggerTarget.EventTarget)
                errors.Add(new RuleViolation("onDeath : la cible de l'événement est morte ; eventSource, bearer ou allEnemies.", p + ".target"));
        }

        /// <summary>
        /// Contrôles qui exigent le contexte d'un combat ou des règles (§4.1) : altérations du filtre et des effets présentes dans
        /// le catalogue et applicables (même règle que les compétences), recharge interne multiple du tick. <paramref name="tickMs"/>
        /// invalide ou nul : contrôle de multiple omis.
        /// </summary>
        public static void ValidateReferences(IReadOnlyList<TriggerDefinition> triggers, string path, StatusCatalog catalog, string guardBreakStatus,
            int tickMs, List<RuleViolation> errors)
        {
            bool tickValid = tickMs > 0 && 1000 % tickMs == 0;
            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDefinition d = triggers[i];
                string p = $"{path}[{i}]";
                if (tickValid && d.CooldownMs > 0 && d.CooldownMs % tickMs != 0)
                    errors.Add(new RuleViolation($"{d.CooldownMs} ms n'est pas un multiple de la durée du tick ({tickMs} ms).", p + ".cooldownMs"));
                if (d.Filter.Status is { } filtered && !catalog.TryGet(filtered, out _))
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"altération inconnue « {filtered} ».", p + ".filter.status"));
                for (int k = 0; k < d.Effects.Count; k++)
                {
                    TriggerEffect e = d.Effects[k];
                    if (e.Kind != TriggerEffectKind.ApplyStatus || string.IsNullOrEmpty(e.Status)) continue;
                    // Même règle que les compétences : DOT avec coefficient, aucune immunité, pas la rupture de garde. La valeur au rang 1
                    // suffit pour la présence du coefficient.
                    var application = new StatusApplication(e.Status!, e.Coefficient?.Base);
                    if (catalog.CheckApplication(application, guardBreakStatus) is { } problem)
                        errors.Add(new RuleViolation(problem.StartsWith("altération inconnue", StringComparison.Ordinal) ? RuleViolationKind.UnknownReference : RuleViolationKind.InvalidValue,
                            problem, $"{p}.effects[{k}].status"));
                }
            }
        }

        /// <summary>
        /// Types d'événements qu'une réaction de ce déclencheur peut produire, directement ou par l'altération qu'elle applique
        /// (graphe statique, A15). <c>gainResource</c> ne produit rien d'observable (Q8).
        /// </summary>
        public IEnumerable<TriggerEvent> ProducedEvents(StatusCatalog? catalog)
        {
            var produced = new SortedSet<TriggerEvent>();
            foreach (TriggerEffect e in Effects)
            {
                switch (e.Kind)
                {
                    case TriggerEffectKind.Strike:
                        produced.UnionWith(new[] { TriggerEvent.HitDealt, TriggerEvent.HitTaken, TriggerEvent.ResourceGained, TriggerEvent.Death });
                        if (!e.Unblockable) produced.UnionWith(new[] { TriggerEvent.Block, TriggerEvent.PerfectParry, TriggerEvent.GuardBroken });
                        break;
                    case TriggerEffectKind.ApplyStatus:
                        produced.UnionWith(new[] { TriggerEvent.StateApplied, TriggerEvent.StateExpired, TriggerEvent.StateRemoved });
                        if (catalog is null || e.Status is null || !catalog.TryGet(e.Status, out StatusDefinition? d) || d.IsDot)
                            produced.UnionWith(new[] { TriggerEvent.DotTick, TriggerEvent.Death });
                        break;
                }
            }
            return produced;
        }
    }

    /// <summary>Un déclencheur nommé pour le graphe statique : son libellé (<c>définition/indice</c>) et son chemin dans les données.</summary>
    public readonly struct NamedTrigger
    {
        public string Label { get; }
        public string Path { get; }
        public TriggerDefinition Definition { get; }

        public NamedTrigger(string label, string path, TriggerDefinition definition)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }

    /// <summary>
    /// Contrôle statique des cycles (A15, Q1). Graphe « déclencheur → types d'événements produits → déclencheurs abonnés »,
    /// sans tenir compte des rôles ni des filtres (majorant prudent). Un cycle est un <b>avertissement</b>, jamais une erreur :
    /// la règle de cycle et la profondeur maximale arrêtent toute chaîne en combat.
    /// </summary>
    public static class TriggerGraph
    {
        /// <summary>
        /// Correctif 7.3.1 (A73-M03) : plafond global des déclencheurs d'un même graphe statique (classe, catalogue ou combat), vérifié
        /// par les appelants avant l'analyse. Au-delà, la donnée est refusée (<see cref="RuleViolationKind.LimitExceeded"/>).
        /// </summary>
        public const int MaxTriggers = 4096;

        /// <summary>Libellés cités au plus dans un avertissement ; les suivants sont comptés.</summary>
        public const int MaxLabelsPerWarning = 8;

        /// <summary>
        /// Une alerte par composante cyclique (au moins deux déclencheurs mutuellement atteignables, ou un déclencheur qui s'atteint
        /// lui-même), dans l'ordre de la liste ; rattachée au chemin du premier déclencheur de la composante.
        /// <para>Correctif 7.3.1 (A73-M03) : composantes fortement connexes (Tarjan, parcours itératif sans récursion) du graphe biparti
        /// « déclencheur → type d'événement produit → déclencheur abonné ». Temps et mémoire linéaires en n × t (t types d'événements,
        /// fixe), au lieu d'une matrice d'atteignabilité n × n parcourue depuis chaque nœud (Θ(n³)). Les arcs ne relient jamais deux
        /// déclencheurs ni deux types directement : toute composante d'au moins deux nœuds contient un cycle, et deux déclencheurs
        /// sont dans la même composante si et seulement s'ils s'atteignent mutuellement, d'où les mêmes alertes qu'avant. Chaque
        /// composante cyclique contient au moins un type d'événement : il y a au plus t alertes, chacune citant au plus
        /// <see cref="MaxLabelsPerWarning"/> libellés.</para>
        /// </summary>
        public static IReadOnlyList<RuleViolation> Cycles(IReadOnlyList<NamedTrigger> triggers, StatusCatalog? catalog)
        {
            int n = triggers.Count;
            TriggerEvent[] events = (TriggerEvent[])Enum.GetValues(typeof(TriggerEvent));
            int eventCount = events.Length;
            var eventIndex = new Dictionary<TriggerEvent, int>();
            for (int e = 0; e < eventCount; e++) eventIndex[events[e]] = e;

            // Nœuds 0..n-1 : déclencheurs ; n..n+t-1 : types d'événements. Listes d'adjacence.
            int nodes = n + eventCount;
            var successors = new List<int>[nodes];
            for (int v = 0; v < nodes; v++) successors[v] = new List<int>();
            for (int i = 0; i < n; i++)
            {
                foreach (TriggerEvent produced in triggers[i].Definition.ProducedEvents(catalog)) successors[i].Add(n + eventIndex[produced]);
                successors[n + eventIndex[triggers[i].Definition.On]].Add(i);
            }

            // Tarjan itératif : pile d'appels explicite (nœud, prochain successeur à visiter).
            var index = new int[nodes];
            var low = new int[nodes];
            var onStack = new bool[nodes];
            var component = new int[nodes];
            for (int v = 0; v < nodes; v++) index[v] = -1;
            var stack = new Stack<int>();
            var calls = new Stack<(int Node, int Next)>();
            int counter = 0, components = 0;
            var sizes = new List<int>();
            for (int root = 0; root < nodes; root++)
            {
                if (index[root] >= 0) continue;
                calls.Push((root, 0));
                index[root] = low[root] = counter++;
                stack.Push(root);
                onStack[root] = true;
                while (calls.Count > 0)
                {
                    (int v, int next) = calls.Pop();
                    if (next < successors[v].Count)
                    {
                        calls.Push((v, next + 1));
                        int w = successors[v][next];
                        if (index[w] < 0)
                        {
                            index[w] = low[w] = counter++;
                            stack.Push(w);
                            onStack[w] = true;
                            calls.Push((w, 0));
                        }
                        else if (onStack[w]) low[v] = Math.Min(low[v], index[w]);
                        continue;
                    }
                    if (low[v] == index[v])
                    {
                        int size = 0, w;
                        do
                        {
                            w = stack.Pop();
                            onStack[w] = false;
                            component[w] = components;
                            size++;
                        }
                        while (w != v);
                        sizes.Add(size);
                        components++;
                    }
                    if (calls.Count > 0)
                    {
                        int parent = calls.Peek().Node;
                        low[parent] = Math.Min(low[parent], low[v]);
                    }
                }
            }

            // Une alerte par composante cyclique, dans l'ordre de son premier déclencheur ; membres dans l'ordre de la liste.
            var members = new Dictionary<int, List<int>>();
            var order = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int c = component[i];
                if (sizes[c] < 2) continue;
                if (!members.TryGetValue(c, out List<int>? list))
                {
                    members[c] = list = new List<int>();
                    order.Add(c);
                }
                list.Add(i);
            }
            var warnings = new List<RuleViolation>();
            foreach (int c in order)
            {
                List<int> list = members[c];
                int first = list[0];
                string cycle = list.Count == 1
                    ? $"{triggers[first].Label} → {triggers[first].Label}"
                    : string.Join(", ", list.Take(MaxLabelsPerWarning).Select(m => triggers[m].Label))
                      + (list.Count > MaxLabelsPerWarning ? $", … et {list.Count - MaxLabelsPerWarning} autres" : "");
                warnings.Add(new RuleViolation(
                    $"cycle statique de déclencheurs ({cycle}) : avertissement seulement, la règle de cycle et la profondeur maximale (combat.limits.maxTriggerDepth) arrêtent la chaîne en combat.",
                    triggers[first].Path));
            }
            return warnings;
        }
    }
}
