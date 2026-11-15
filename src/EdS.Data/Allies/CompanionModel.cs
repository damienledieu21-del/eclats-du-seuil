using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EdS.Data.Classes;
using EdS.Data.Validation;
using EdS.Domain.Builds;
using EdS.Domain.Collections;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;

namespace EdS.Data.Allies
{
    // Compagnons (sujet 11 de la feuille de route, décisions de Damien du 14 novembre 2026, CADRAGE.md § 7 bis) : catalogue des compagnons
    // (kind « companions », schéma 1) : héros de l'histoire, mercenaires de la Guilde et bêtes, avec leurs stades, leur profil de combat
    // qui croît avec le niveau, leur attaque à deux, leur don et les traits tirés au hasard des mercenaires. Un seul validateur
    // (CompanionValidator) ; le profil d'un compagnon à un niveau donné devient un assaillant compagnon du moteur (CompanionCombat).

    /// <summary>Nature d'un compagnon (décision de Damien : héros de l'histoire, mercenaires de la Guilde, bêtes).</summary>
    public enum CompanionKind
    {
        Hero = 0,
        Mercenary = 1,
        Beast = 2,
    }

    /// <summary>Identifiants du fichier.</summary>
    public static class CompanionIds
    {
        public const string Hero = "hero";
        public const string Mercenary = "mercenary";
        public const string Beast = "beast";

        public static IReadOnlyList<string> Kinds { get; } = Frozen.List(new[] { Hero, Mercenary, Beast }, nameof(Kinds));

        public static bool TryParseKind(string? id, out CompanionKind kind)
        {
            switch (id)
            {
                case Hero: kind = CompanionKind.Hero; return true;
                case Mercenary: kind = CompanionKind.Mercenary; return true;
                case Beast: kind = CompanionKind.Beast; return true;
                default: kind = default; return false;
            }
        }

        /// <summary>
        /// Effets de don connus (dossier p. 36 à 110). Les dons des bêtes retenus par Damien : <c>treasure</c> (le chien trouve des trésors),
        /// <c>map</c> (le faucon révèle la carte), <c>saddlebags</c> (l'ours porte des sacoches). Les autres viennent des fiches du dossier
        /// (propositions de conception). Un don ne crée jamais de butin ni d'invulnérabilité : il révèle, signale ou porte.
        /// </summary>
        public static IReadOnlyList<string> GiftEffects { get; } = Frozen.List(new[]
        {
            "treasure", "map", "saddlebags", "track", "flank", "rescue", "appraisal", "ambush", "steadfast", "anchor", "retreat", "lastPinch",
            "trap", "meal",
        }, nameof(GiftEffects));

        /// <summary>Sources de recrutement : l'histoire (héros, Cendre), la Guilde (mercenaires, selon le rang), le dressage (bêtes).</summary>
        public static IReadOnlyList<string> Sources { get; } = Frozen.List(new[] { "story", "guild", "tame" }, nameof(Sources));
    }

    // --- Documents (avant validation) ------------------------------------------------------------------

    public sealed class CompanionDocument
    {
        public int? SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? ContentVersion { get; set; }
        public CompanionCatalogData? Data { get; set; }
    }

    public sealed class CompanionCatalogData
    {
        public BondRulesData? Bond { get; set; }
        public SlotRulesData? Slots { get; set; }
        public List<TraitData?>? Traits { get; set; }
        public List<CompanionData?>? Companions { get; set; }
    }

    public sealed class BondRulesData
    {
        public long? WindowMs { get; set; }
        public long? CoordinatedGain { get; set; }
        public long? ProtectGain { get; set; }
        public long? CampGain { get; set; }
    }

    public sealed class SlotRulesData
    {
        public long? Initial { get; set; }
        public long? Max { get; set; }
        public long? SecondSlotLevel { get; set; }
        public string? SecondSlotQuest { get; set; }
        public long? WorldBossInCombat { get; set; }
    }

    public sealed class TraitData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Gesture { get; set; }
        public string? Line { get; set; }
    }

    public sealed class CompanionData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? Role { get; set; }
        public string? Summary { get; set; }
        public RecruitData? Recruit { get; set; }
        public bool? RandomTrait { get; set; }
        public GiftData? Gift { get; set; }
        public List<StageData?>? Stages { get; set; }
    }

    public sealed class RecruitData
    {
        public string? Source { get; set; }
        public string? GuildRank { get; set; }
        public long? Price { get; set; }
    }

    public sealed class GiftData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Effect { get; set; }
        public string? Description { get; set; }
    }

    public sealed class ScaledData
    {
        public long? Base { get; set; }
        public long? PerLevel { get; set; }
    }

    public sealed class StageData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public long? FromLevel { get; set; }
        public ScaledData? Health { get; set; }
        public ScaledData? Armor { get; set; }
        public ScaledData? MagicResistance { get; set; }
        public Fixed? Radius { get; set; }
        public Fixed? Speed { get; set; }
        public Fixed? MaxRange { get; set; }
        public Fixed? MinRange { get; set; }
        public long? FirstAttackMs { get; set; }
        public long? IntervalMs { get; set; }
        public Fixed? FollowDistance { get; set; }
        public Fixed? Leash { get; set; }
        public List<CompanionAttackData?>? Attacks { get; set; }
        public DuoDefinitionData? Duo { get; set; }
    }

    public sealed class CompanionAttackData
    {
        public string? Id { get; set; }
        public ScaledData? Damage { get; set; }
        public string? DamageType { get; set; }
        public long? TelegraphMs { get; set; }
        public Fixed? Range { get; set; }
    }

    public sealed class DuoDefinitionData
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public ScaledData? Damage { get; set; }
        public string? DamageType { get; set; }
        public long? TelegraphMs { get; set; }
        public long? RecoveryMs { get; set; }
        public Fixed? Range { get; set; }
    }

    // --- Définitions validées (immuables) ----------------------------------------------------------------

    /// <summary>Valeur qui croît avec le niveau : <c>base + perLevel × (niveau − 1)</c>, arithmétique vérifiée.</summary>
    public sealed class Scaled
    {
        public long Base { get; }
        public long PerLevel { get; }

        public Scaled(long @base, long perLevel)
        {
            Base = @base;
            PerLevel = perLevel;
        }

        public long At(int level) => checked(Base + PerLevel * (level - 1));
    }

    public sealed class BondRules
    {
        public long WindowMs { get; }
        public int CoordinatedGain { get; }
        public int ProtectGain { get; }

        /// <summary>Gain d'une halte au feu de camp avec le compagnon (hors combat, progression).</summary>
        public int CampGain { get; }

        public BondRules(long windowMs, int coordinatedGain, int protectGain, int campGain)
        {
            WindowMs = windowMs;
            CoordinatedGain = coordinatedGain;
            ProtectGain = protectGain;
            CampGain = campGain;
        }
    }

    /// <summary>
    /// Emplacements (décisions de Damien) : un compagnon, puis deux ; la deuxième place s'ouvre par l'histoire (quête de lien au feu de camp),
    /// avec un niveau minimum doux (environ 12) ; pendant le boss du monde, un seul compagnon par joueur au combat.
    /// </summary>
    public sealed class SlotRules
    {
        public int Initial { get; }
        public int Max { get; }
        public int SecondSlotLevel { get; }
        public string SecondSlotQuest { get; }
        public int WorldBossInCombat { get; }

        public SlotRules(int initial, int max, int secondSlotLevel, string secondSlotQuest, int worldBossInCombat)
        {
            Initial = initial;
            Max = max;
            SecondSlotLevel = secondSlotLevel;
            SecondSlotQuest = secondSlotQuest;
            WorldBossInCombat = worldBossInCombat;
        }
    }

    /// <summary>Trait d'un mercenaire (courageux, peureux, gourmand, chanceux) : gestes et répliques, jamais l'obéissance.</summary>
    public sealed class CompanionTrait
    {
        public string Id { get; }
        public string Name { get; }
        public string Gesture { get; }
        public string Line { get; }

        public CompanionTrait(string id, string name, string gesture, string line)
        {
            Id = id;
            Name = name;
            Gesture = gesture;
            Line = line;
        }
    }

    public sealed class CompanionGift
    {
        public string Id { get; }
        public string Name { get; }
        public string Effect { get; }
        public string Description { get; }

        public CompanionGift(string id, string name, string effect, string description)
        {
            Id = id;
            Name = name;
            Effect = effect;
            Description = description;
        }
    }

    public sealed class CompanionRecruit
    {
        public string Source { get; }
        public string? GuildRank { get; }
        public long Price { get; }

        public CompanionRecruit(string source, string? guildRank, long price)
        {
            Source = source;
            GuildRank = guildRank;
            Price = price;
        }
    }

    public sealed class CompanionAttackDefinition
    {
        public string Id { get; }
        public Scaled Damage { get; }
        public DamageType DamageType { get; }
        public long TelegraphMs { get; }
        public Fixed Range { get; }

        public CompanionAttackDefinition(string id, Scaled damage, DamageType damageType, long telegraphMs, Fixed range)
        {
            Id = id;
            Damage = damage;
            DamageType = damageType;
            TelegraphMs = telegraphMs;
            Range = range;
        }
    }

    public sealed class DuoDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public Scaled Damage { get; }
        public DamageType DamageType { get; }
        public long TelegraphMs { get; }
        public long RecoveryMs { get; }
        public Fixed Range { get; }

        public DuoDefinition(string id, string name, Scaled damage, DamageType damageType, long telegraphMs, long recoveryMs, Fixed range)
        {
            Id = id;
            Name = name;
            Damage = damage;
            DamageType = damageType;
            TelegraphMs = telegraphMs;
            RecoveryMs = recoveryMs;
            Range = range;
        }
    }

    /// <summary>Stade d'un compagnon (Cendre : louveteau, jeune loup, loup dressé ; un seul stade pour les autres), à partir d'un niveau.</summary>
    public sealed class CompanionStage
    {
        public string Id { get; }
        public string Name { get; }
        public int FromLevel { get; }
        public Scaled Health { get; }
        public Scaled Armor { get; }
        public Scaled MagicResistance { get; }
        public Fixed Radius { get; }
        public Fixed Speed { get; }
        public Fixed MaxRange { get; }
        public Fixed MinRange { get; }
        public long FirstAttackMs { get; }
        public long IntervalMs { get; }
        public Fixed FollowDistance { get; }
        public Fixed Leash { get; }
        public IReadOnlyList<CompanionAttackDefinition> Attacks { get; }

        /// <summary>Null : pas d'attaque à deux à ce stade (Cendre louveteau).</summary>
        public DuoDefinition? Duo { get; }

        public CompanionStage(string id, string name, int fromLevel, Scaled health, Scaled armor, Scaled magicResistance, Fixed radius, Fixed speed,
            Fixed maxRange, Fixed minRange, long firstAttackMs, long intervalMs, Fixed followDistance, Fixed leash,
            IEnumerable<CompanionAttackDefinition> attacks, DuoDefinition? duo)
        {
            Id = id;
            Name = name;
            FromLevel = fromLevel;
            Health = health;
            Armor = armor;
            MagicResistance = magicResistance;
            Radius = radius;
            Speed = speed;
            MaxRange = maxRange;
            MinRange = minRange;
            FirstAttackMs = firstAttackMs;
            IntervalMs = intervalMs;
            FollowDistance = followDistance;
            Leash = leash;
            Attacks = Frozen.List(attacks, nameof(attacks));
            Duo = duo;
        }
    }

    public sealed class CompanionSheet
    {
        public string Id { get; }
        public string Name { get; }
        public CompanionKind Kind { get; }
        public string Role { get; }
        public string Summary { get; }
        public CompanionRecruit Recruit { get; }

        /// <summary>Mercenaire : trait tiré au hasard au recrutement (et seulement lui).</summary>
        public bool RandomTrait { get; }

        public CompanionGift Gift { get; }

        /// <summary>Stades par niveau croissant ; le premier commence au niveau 1.</summary>
        public IReadOnlyList<CompanionStage> Stages { get; }

        public CompanionSheet(string id, string name, CompanionKind kind, string role, string summary, CompanionRecruit recruit, bool randomTrait,
            CompanionGift gift, IEnumerable<CompanionStage> stages)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Role = role;
            Summary = summary;
            Recruit = recruit;
            RandomTrait = randomTrait;
            Gift = gift;
            Stages = Frozen.List(stages, nameof(stages));
        }

        /// <summary>Stade atteint au niveau <paramref name="level"/> (le dernier dont le niveau de départ est atteint).</summary>
        public CompanionStage StageAt(int level)
        {
            CompanionStage stage = Stages[0];
            foreach (CompanionStage s in Stages)
                if (s.FromLevel <= level) stage = s;
            return stage;
        }
    }

    public sealed class CompanionCatalog
    {
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public BondRules Bond { get; }
        public SlotRules Slots { get; }
        public IReadOnlyList<CompanionTrait> Traits { get; }
        public IReadOnlyList<CompanionSheet> Companions { get; }
        private readonly Dictionary<string, CompanionSheet> _byId;

        public CompanionCatalog(int schemaVersion, string contentVersion, BondRules bond, SlotRules slots, IEnumerable<CompanionTrait> traits,
            IEnumerable<CompanionSheet> companions)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            Bond = bond;
            Slots = slots;
            Traits = Frozen.List(traits, nameof(traits));
            Companions = Frozen.List(companions, nameof(companions));
            _byId = Companions.ToDictionary(c => c.Id, StringComparer.Ordinal);
        }

        public bool TryGet(string id, out CompanionSheet definition) => _byId.TryGetValue(id ?? "", out definition!);

        public CompanionTrait? Trait(string? id) => Traits.FirstOrDefault(t => t.Id == id);
    }

    public sealed class CompanionLoadResult
    {
        public ValidationReport Report { get; }
        public CompanionCatalog? Catalog { get; }
        public bool IsValid => Report.IsValid && Catalog != null;

        public CompanionLoadResult(ValidationReport report, CompanionCatalog? catalog)
        {
            Report = report;
            Catalog = catalog;
        }
    }

    /// <summary>
    /// Compagnons : du catalogue au moteur. Le profil d'un compagnon à un niveau (son stade, ses valeurs croissantes) devient un assaillant
    /// compagnon de son Éveillé (<see cref="AssailantSetup.Companion"/>), avec la jauge de lien donnée et les règles de lien du catalogue.
    /// </summary>
    public static class CompanionCombat
    {
        /// <summary>Niveau le plus haut qu'un compagnon suit (borne technique, au-delà du jeu prévu).</summary>
        public const int MaxLevel = 100;

        public static AssailantSetup Build(CompanionCatalog catalog, CompanionSheet definition, int level, string combatantId, string owner, string team,
            int bond, CombatantPlacement placement, long? arrivalMs = null)
        {
            if (catalog is null) throw new ArgumentNullException(nameof(catalog));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (level < 1 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level), level, $"niveau hors de [1, {MaxLevel}].");
            CompanionStage s = definition.StageAt(level);
            var attacks = s.Attacks.Select(a => new AssailantAttack(a.Id, a.Damage.At(level), a.DamageType, SkillTargeting.Enemy, false, null,
                AttackShape.Target, a.TelegraphMs, a.Range)).ToArray();
            CompanionDuo? duo = s.Duo is { } d ? new CompanionDuo(d.Id, d.Damage.At(level), d.DamageType, d.TelegraphMs, d.RecoveryMs, d.Range) : null;
            var link = new CompanionSetup(owner, bond, s.FollowDistance, s.Leash, catalog.Bond.WindowMs, catalog.Bond.CoordinatedGain, catalog.Bond.ProtectGain, duo);
            var placed = new CombatantPlacement(placement.Position, s.Radius, placement.Facing, s.Speed);
            return new AssailantSetup(combatantId, team, Fixed.FromInt(s.Armor.At(level)), Fixed.FromInt(s.MagicResistance.At(level)), null,
                s.FirstAttackMs, s.IntervalMs, attacks, health: s.Health.At(level), placement: placed, arrivalMs: arrivalMs,
                behavior: new AssailantBehavior(s.MaxRange, s.MinRange), companion: link);
        }
    }

    /// <summary>
    /// Validateur unique du catalogue des compagnons (schéma 1) : en-tête, règles de lien et d'emplacements dans leurs bornes, traits uniques,
    /// compagnons (identifiant <c>companion.…</c> unique, nature, recrutement cohérent avec la nature, trait au hasard pour les mercenaires et
    /// eux seuls, don connu), stades par niveau croissant à partir de 1, valeurs positives aux niveaux 1 et <see cref="CompanionCombat.MaxLevel"/>,
    /// attaques à forme dirigée, attaque à deux bornée.
    /// </summary>
    public static class CompanionValidator
    {
        public const string Kind = "companions";

        /// <summary>Schéma 1 (compagnons, décisions de Damien du 14 novembre 2026).</summary>
        public const int CurrentSchemaVersion = 1;

        public const int MaxCompanions = 64;
        public const int MaxStages = 4;
        public const int MaxAttacks = 4;
        public const int MaxTraits = 16;
        public const int MaxTextLength = 400;
        public const long MaxPrice = 1_000_000;

        private static readonly Regex CompanionId = new Regex(@"^companion\.[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        /// <param name="story">Histoire du monde (relecture de la PR #175) : la quête de lien doit y exister ; null : non vérifiée.</param>
        public static CompanionLoadResult Validate(CompanionDocument document, ValidationReport? report = null, Story.StoryBook? story = null)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            ValidationReport r = report ?? new ValidationReport();
            if (!HeaderValidator.Validate(document.SchemaVersion, document.Kind, document.ContentVersion, Kind, CurrentSchemaVersion, r))
                return new CompanionLoadResult(r, null);
            if (document.Data is not { } d)
            {
                r.Add("$.data", ValidationCode.MissingField, "champ obligatoire absent (lien, emplacements, traits, compagnons).");
                return new CompanionLoadResult(r, null);
            }
            var c = new Checker(r, story);
            CompanionCatalog? catalog = c.Run(d, document.SchemaVersion!.Value, document.ContentVersion!);
            return new CompanionLoadResult(r, r.IsValid ? catalog : null);
        }

        private sealed class Checker
        {
            private readonly ValidationReport _r;
            private readonly Story.StoryBook? _story;

            public Checker(ValidationReport r, Story.StoryBook? story)
            {
                _r = r;
                _story = story;
            }

            private bool Present(object? value, string path, string what)
            {
                if (value != null) return true;
                if (!_r.HasErrorAtOrAbove(path)) _r.Add(path, ValidationCode.MissingField, $"champ obligatoire absent ({what}).");
                return false;
            }

            private string Text(string? value, string path, string what)
            {
                if (!Present(value, path, what)) return "";
                if (value!.Trim().Length == 0 || value.Length > MaxTextLength)
                    _r.Add(path, ValidationCode.InvalidValue, $"texte vide ou de plus de {MaxTextLength} caractères.");
                return value;
            }

            private long Range(long? value, string path, string what, long min, long max)
            {
                if (!Present(value, path, what)) return min;
                if (value < min || value > max) _r.Add(path, ValidationCode.OutOfRange, $"{value} hors de [{min}, {max}].");
                return value!.Value;
            }

            private Fixed Length(Fixed? value, string path, string what, Fixed min, Fixed max, bool positive = false)
            {
                if (!Present(value, path, what)) return min;
                if (value!.Value < min || value.Value > max || (positive && value.Value == Fixed.Zero))
                    _r.Add(path, ValidationCode.OutOfRange, $"{value} hors de {(positive ? "]" : "[")}{min}, {max}].");
                return value.Value;
            }

            private long Duration(long? value, string path, string what, long min, long max)
            {
                long v = Range(value, path, what, min, max);
                if (value is long ms && ms % 50 != 0 && !_r.HasErrorAtOrAbove(path))
                    _r.Add(path, ValidationCode.InvalidValue, $"{ms} ms n'est pas un multiple du tick (50 ms).");
                return v;
            }

            /// <summary>Valeur croissante positive : <paramref name="min"/> au niveau 1, au plus <paramref name="max"/> au niveau le plus haut.</summary>
            private Scaled Scale(ScaledData? s, string path, string what, long min, long max)
            {
                if (!Present(s, path, what)) return new Scaled(min, 0);
                long b = Range(s!.Base, path + ".base", "valeur au niveau 1", min, max);
                long p = Range(s.PerLevel, path + ".perLevel", "gain par niveau", 0, max);
                var scaled = new Scaled(b, p);
                if (_r.HasErrorAtOrAbove(path)) return scaled;
                try
                {
                    if (scaled.At(CompanionCombat.MaxLevel) > max)
                        _r.Add(path, ValidationCode.OutOfRange, $"valeur au niveau {CompanionCombat.MaxLevel} au-delà de {max}.");
                }
                catch (OverflowException)
                {
                    _r.Add(path, ValidationCode.OutOfRange, "valeur non représentable au niveau le plus haut.");
                }
                return scaled;
            }

            private DamageType Damage(string? id, string path)
            {
                if (Present(id, path, "physique ou magique") && ClassValidator.DamageTypes.TryGetValue(id!, out DamageType type)) return type;
                if (id != null) _r.Add(path, ValidationCode.UnknownReference, $"valeur inconnue « {id} » ; valeurs possibles : {string.Join(", ", ClassValidator.DamageTypes.Keys)}.");
                return DamageType.Physique;
            }

            private string Name(string? id, string path, string what)
            {
                if (Present(id, path, what) && !CombatSetup.IsValidName(id))
                    _r.Add(path, ValidationCode.InvalidValue, $"identifiant « {id} » invalide : minuscules, chiffres et « _ », commençant par une lettre.");
                return id ?? "";
            }

            public CompanionCatalog? Run(CompanionCatalogData d, int schemaVersion, string contentVersion)
            {
                BondRules? bond = null;
                if (Present(d.Bond, "$.data.bond", "règles de la jauge de lien"))
                {
                    BondRulesData b = d.Bond!;
                    bond = new BondRules(Duration(b.WindowMs, "$.data.bond.windowMs", "fenêtre de coordination", 50, CompanionSetup.MaxBondWindowMs),
                        (int)Range(b.CoordinatedGain, "$.data.bond.coordinatedGain", "gain d'un coup coordonné", 0, CompanionSetup.MaxGain),
                        (int)Range(b.ProtectGain, "$.data.bond.protectGain", "gain d'un coup qui protège", 0, CompanionSetup.MaxGain),
                        (int)Range(b.CampGain, "$.data.bond.campGain", "gain d'une halte au feu de camp", 0, CompanionSetup.MaxGain));
                }
                SlotRules? slots = null;
                if (Present(d.Slots, "$.data.slots", "emplacements"))
                {
                    SlotRulesData s = d.Slots!;
                    int max = (int)Range(s.Max, "$.data.slots.max", "emplacements au plus", 1, CompanionSetup.MaxPerOwner);
                    int initial = (int)Range(s.Initial, "$.data.slots.initial", "emplacements ouverts au départ", 1, max);
                    int level = (int)Range(s.SecondSlotLevel, "$.data.slots.secondSlotLevel", "niveau minimum doux de la deuxième place", 1, CompanionCombat.MaxLevel);
                    string quest = Text(s.SecondSlotQuest, "$.data.slots.secondSlotQuest", "quête de lien qui ouvre la deuxième place");
                    if (_story != null && s.SecondSlotQuest != null && !_story.HasQuest(quest))
                        _r.Add("$.data.slots.secondSlotQuest", ValidationCode.UnknownReference, $"quête « {quest} » absente de l'histoire.");
                    int boss = (int)Range(s.WorldBossInCombat, "$.data.slots.worldBossInCombat", "compagnons au combat pendant le boss du monde", 0, max);
                    slots = new SlotRules(initial, max, level, quest, boss);
                }
                var traits = new List<CompanionTrait>();
                if (Present(d.Traits, "$.data.traits", "traits des mercenaires"))
                {
                    if (d.Traits!.Count == 0 || d.Traits.Count > MaxTraits)
                        _r.Add("$.data.traits", ValidationCode.LimitExceeded, $"{d.Traits.Count} traits, de 1 à {MaxTraits}.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < d.Traits.Count; i++)
                    {
                        string p = $"$.data.traits[{i}]";
                        if (!Present(d.Traits[i], p, "trait")) continue;
                        TraitData t = d.Traits[i]!;
                        string id = Name(t.Id, p + ".id", "identifiant du trait");
                        if (t.Id != null && !seen.Add(t.Id)) _r.Add(p + ".id", ValidationCode.DuplicateId, $"trait « {t.Id} » déjà défini.");
                        traits.Add(new CompanionTrait(id, Text(t.Name, p + ".name", "nom"), Text(t.Gesture, p + ".gesture", "geste"), Text(t.Line, p + ".line", "réplique")));
                    }
                }
                var companions = new List<CompanionSheet>();
                if (Present(d.Companions, "$.data.companions", "compagnons"))
                {
                    if (d.Companions!.Count == 0 || d.Companions.Count > MaxCompanions)
                        _r.Add("$.data.companions", ValidationCode.LimitExceeded, $"{d.Companions.Count} compagnons, de 1 à {MaxCompanions}.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < d.Companions.Count; i++)
                    {
                        string p = $"$.data.companions[{i}]";
                        if (!Present(d.Companions[i], p, "compagnon")) continue;
                        CompanionSheet? def = Companion(d.Companions[i]!, p);
                        if (def is null) continue;
                        if (!seen.Add(def.Id)) _r.Add(p + ".id", ValidationCode.DuplicateId, $"compagnon « {def.Id} » déjà défini.");
                        companions.Add(def);
                    }
                }
                if (!_r.IsValid || bond is null || slots is null) return null;
                return new CompanionCatalog(schemaVersion, contentVersion, bond, slots, traits, companions);
            }

            private CompanionSheet? Companion(CompanionData c, string p)
            {
                string id = "";
                if (Present(c.Id, p + ".id", "identifiant « companion.… »"))
                {
                    id = c.Id!;
                    if (!CompanionId.IsMatch(id)) _r.Add(p + ".id", ValidationCode.InvalidValue, $"identifiant « {id} » invalide : « companion. » puis minuscules, chiffres et « _ ».");
                }
                string name = Text(c.Name, p + ".name", "nom");
                CompanionKind kind = CompanionKind.Hero;
                if (Present(c.Kind, p + ".kind", string.Join(", ", CompanionIds.Kinds)) && !CompanionIds.TryParseKind(c.Kind, out kind))
                    _r.Add(p + ".kind", ValidationCode.UnknownReference, $"nature inconnue « {c.Kind} » ; valeurs possibles : {string.Join(", ", CompanionIds.Kinds)}.");
                string role = Text(c.Role, p + ".role", "rôle");
                string summary = Text(c.Summary, p + ".summary", "présentation courte");
                CompanionRecruit recruit = new CompanionRecruit("story", null, 0);
                if (Present(c.Recruit, p + ".recruit", "recrutement"))
                {
                    RecruitData r = c.Recruit!;
                    string source = "story";
                    if (Present(r.Source, p + ".recruit.source", string.Join(", ", CompanionIds.Sources)))
                    {
                        source = r.Source!;
                        if (!CompanionIds.Sources.Contains(source))
                            _r.Add(p + ".recruit.source", ValidationCode.UnknownReference, $"source inconnue « {source} » ; valeurs possibles : {string.Join(", ", CompanionIds.Sources)}.");
                    }
                    // Un mercenaire se recrute à la Guilde, selon le rang, contre une paie ; les autres ne se paient pas.
                    bool guild = source == "guild";
                    if (guild != (kind == CompanionKind.Mercenary) && c.Kind != null && r.Source != null)
                        _r.Add(p + ".recruit.source", ValidationCode.InvalidValue, "un mercenaire, et lui seul, se recrute à la Guilde (guild).");
                    if (guild && !Guild.GuildRanks.IsRank(r.GuildRank))
                        _r.Add(p + ".recruit.guildRank", ValidationCode.UnknownReference, $"rang de Guilde attendu : {Guild.GuildRanks.Known}.");
                    if (!guild && r.GuildRank != null) _r.Add(p + ".recruit.guildRank", ValidationCode.InvalidValue, "seul un mercenaire demande un rang de Guilde.");
                    long price = r.Price ?? 0;
                    if (price < 0 || price > MaxPrice) _r.Add(p + ".recruit.price", ValidationCode.OutOfRange, $"{price} hors de [0, {MaxPrice}].");
                    if (!guild && price != 0) _r.Add(p + ".recruit.price", ValidationCode.InvalidValue, "seul un mercenaire se paie.");
                    recruit = new CompanionRecruit(source, r.GuildRank, price);
                }
                bool random = c.RandomTrait ?? false;
                if (random != (kind == CompanionKind.Mercenary) && c.Kind != null)
                    _r.Add(p + ".randomTrait", ValidationCode.InvalidValue, "les mercenaires, et eux seuls, ont un trait tiré au hasard (décision de Damien).");
                CompanionGift gift = new CompanionGift("", "", "", "");
                if (Present(c.Gift, p + ".gift", "don"))
                {
                    GiftData g = c.Gift!;
                    string effect = g.Effect ?? "";
                    if (Present(g.Effect, p + ".gift.effect", "effet du don") && !CompanionIds.GiftEffects.Contains(effect))
                        _r.Add(p + ".gift.effect", ValidationCode.UnknownReference, $"effet inconnu « {effect} » ; valeurs possibles : {string.Join(", ", CompanionIds.GiftEffects)}.");
                    gift = new CompanionGift(Name(g.Id, p + ".gift.id", "identifiant du don"), Text(g.Name, p + ".gift.name", "nom du don"), effect,
                        Text(g.Description, p + ".gift.description", "ce que fait le don"));
                }
                var stages = new List<CompanionStage>();
                if (Present(c.Stages, p + ".stages", "stades (au moins un)"))
                {
                    if (c.Stages!.Count == 0 || c.Stages.Count > MaxStages)
                        _r.Add(p + ".stages", ValidationCode.LimitExceeded, $"{c.Stages.Count} stades, de 1 à {MaxStages}.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < c.Stages.Count; i++)
                    {
                        string sp = $"{p}.stages[{i}]";
                        if (!Present(c.Stages[i], sp, "stade")) continue;
                        CompanionStage stage = Stage(c.Stages[i]!, sp);
                        if (!seen.Add(stage.Id)) _r.Add(sp + ".id", ValidationCode.DuplicateId, $"stade « {stage.Id} » déjà défini.");
                        int expected = stages.Count == 0 ? 1 : stages[stages.Count - 1].FromLevel + 1;
                        if (stages.Count == 0 ? stage.FromLevel != 1 : stage.FromLevel < expected)
                            _r.Add(sp + ".fromLevel", ValidationCode.InvalidValue, stages.Count == 0 ? "le premier stade commence au niveau 1." : "stades par niveau strictement croissant.");
                        stages.Add(stage);
                    }
                }
                return new CompanionSheet(id, name, kind, role, summary, recruit, random, gift, stages);
            }

            private CompanionStage Stage(StageData s, string p)
            {
                string id = Name(s.Id, p + ".id", "identifiant du stade");
                string name = Text(s.Name, p + ".name", "nom du stade");
                int from = (int)Range(s.FromLevel, p + ".fromLevel", "niveau de départ", 1, CompanionCombat.MaxLevel);
                Scaled health = Scale(s.Health, p + ".health", "PV", 1, AssailantSetup.MaxHealth);
                Scaled armor = Scale(s.Armor, p + ".armor", "armure", 0, 10_000);
                Scaled mr = Scale(s.MagicResistance, p + ".magicResistance", "résistance magique", 0, 10_000);
                Fixed radius = Length(s.Radius, p + ".radius", "rayon de l'emprise, en mètres", Fixed.FromRatio(1, 10), Fixed.FromInt(5));
                Fixed speed = Length(s.Speed, p + ".speed", "vitesse, en m/s", Fixed.FromRatio(1, 10), Fixed.FromInt(20));
                Fixed maxRange = Length(s.MaxRange, p + ".maxRange", "distance maximale à la cible, en mètres", Fixed.Zero, AssailantBehavior.MaxDistance);
                Fixed minRange = s.MinRange is null ? Fixed.Zero : Length(s.MinRange, p + ".minRange", "distance minimale à la cible", Fixed.Zero, maxRange);
                long first = Duration(s.FirstAttackMs, p + ".firstAttackMs", "première attaque, en ms", 0, 60_000);
                long interval = Duration(s.IntervalMs, p + ".intervalMs", "intervalle des attaques, en ms", 50, 60_000);
                Fixed follow = Length(s.FollowDistance, p + ".followDistance", "distance de suite", Fixed.Zero, CompanionSetup.MaxFollowDistance, positive: true);
                Fixed leash = Length(s.Leash, p + ".leash", "laisse", follow, AssailantBehavior.MaxDistance);
                var attacks = new List<CompanionAttackDefinition>();
                if (Present(s.Attacks, p + ".attacks", "attaques (au moins une)"))
                {
                    if (s.Attacks!.Count == 0 || s.Attacks.Count > MaxAttacks)
                        _r.Add(p + ".attacks", ValidationCode.LimitExceeded, $"{s.Attacks.Count} attaques, de 1 à {MaxAttacks}.");
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < s.Attacks.Count; i++)
                    {
                        string ap = $"{p}.attacks[{i}]";
                        if (!Present(s.Attacks[i], ap, "attaque")) continue;
                        CompanionAttackData a = s.Attacks[i]!;
                        string aid = Name(a.Id, ap + ".id", "identifiant de l'attaque");
                        if (a.Id != null && !seen.Add(a.Id)) _r.Add(ap + ".id", ValidationCode.DuplicateId, $"attaque « {a.Id} » déjà définie.");
                        attacks.Add(new CompanionAttackDefinition(aid, Scale(a.Damage, ap + ".damage", "dégâts bruts", 0, CombatantStats.MaxRawDamage),
                            Damage(a.DamageType, ap + ".damageType"), Duration(a.TelegraphMs ?? 0, ap + ".telegraphMs", "annonce, en ms", 0, 10_000),
                            Length(a.Range, ap + ".range", "portée, en mètres", SpaceLimits.MinRange, SpaceLimits.MaxRange)));
                    }
                }
                // Relecture de la PR #175 : un compagnon qui se tient au-delà de toutes ses portées ne frapperait jamais.
                if (attacks.Count > 0 && s.MaxRange != null && attacks.All(a => maxRange > a.Range) && !_r.HasErrorAtOrAbove(p + ".maxRange"))
                    _r.Add(p + ".maxRange", ValidationCode.InvalidValue, $"distance maximale {maxRange} au-delà de toutes les portées d'attaque du stade.");
                DuoDefinition? duo = null;
                if (s.Duo is { } d)
                {
                    string dp = p + ".duo";
                    string did = Name(d.Id, dp + ".id", "identifiant de l'attaque à deux");
                    if (attacks.Any(a => a.Id == did)) _r.Add(dp + ".id", ValidationCode.DuplicateId, $"« {did} » est déjà une attaque du stade.");
                    long telegraph = Duration(d.TelegraphMs, dp + ".telegraphMs", "annonce, en ms", 50, CompanionDuo.MaxPhaseMs);
                    long recovery = Duration(d.RecoveryMs, dp + ".recoveryMs", "récupération depuis le départ, en ms", 100, CompanionDuo.MaxPhaseMs);
                    if (recovery <= telegraph && !_r.HasErrorAtOrAbove(dp + ".recoveryMs"))
                        _r.Add(dp + ".recoveryMs", ValidationCode.InvalidValue, "la récupération couvre l'annonce et au moins un tick de plus.");
                    duo = new DuoDefinition(did, Text(d.Name, dp + ".name", "nom de l'attaque à deux"), Scale(d.Damage, dp + ".damage", "dégâts bruts", 0, CombatantStats.MaxRawDamage),
                        Damage(d.DamageType, dp + ".damageType"), telegraph, recovery, Length(d.Range, dp + ".range", "portée commune", SpaceLimits.MinRange, SpaceLimits.MaxRange));
                }
                return new CompanionStage(id, name, from, health, armor, mr, radius, speed, maxRange, minRange, first, interval, follow, leash, attacks, duo);
            }
        }
    }
}
