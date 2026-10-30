using System;
using EdS.Domain.Builds;
using EdS.Domain.Numerics;

namespace EdS.Domain.Combat
{
    /// <summary>
    /// Sous-lot 8.1 : règles effectives d'un combattant, valeurs résultantes des règles de base et de ses modificateurs permanents (X1),
    /// plus l'évaluation de ses modificateurs conditionnels (X2). Construites par une seule fonction (<see cref="For"/>), appelée par la
    /// validation du combat (précontrôle numérique, majorant de file) et par le moteur, si bien qu'un combat accepté ne peut pas diverger
    /// à sa construction. Neutres (règles de base) pour un mannequin ou un assaillant. Immuables.
    /// <para>Chaque valeur résultante = base + somme des modificateurs, <b>écrêtée</b> dans son intervalle de jeu (plafonds et planchers,
    /// jamais une erreur : une combinaison de choix légaux ne rend pas un combat invalide). Quand la base sort déjà de cet intervalle
    /// (règles de test), l'intervalle est élargi jusqu'à elle : sans modificateur, la valeur résultante est toujours la base.</para>
    /// </summary>
    public sealed class CombatantRules
    {
        // Plafonds de la réduction de garde (B1, combat.guard.maxDamageReduction) et de la réduction totale des dégâts subis (B2,
        // combat.damageTaken.maxReduction, avant l'absorption par un bouclier) : paramètres des règles depuis la décision du 18/10/2026,
        // communs à tous les combattants (identifiés par l'empreinte des règles, pas répétés au journal).
        private readonly Fixed _maxGuardReduction;
        private readonly Fixed _maxTotalReduction;

        /// <summary>Fenêtre de parade maximale atteinte par modificateur (500 ms, ramenée au multiple du tick inférieur).</summary>
        public const long MaxParryWindowMs = 500;

        /// <summary>Charges de rempart maximales atteintes par modificateur.</summary>
        public const int MaxRampartCharges = 10;

        /// <summary>
        /// Plancher du pourcentage de PV max résultant (−90 %). Un PV max calculé d'au moins 10 garde au moins 1 PV ; en dessous
        /// (règles de test), le plancher entier peut donner 0, que le moteur traite déjà (mort relevée à la fin du premier tick).
        /// </summary>
        public static readonly Fixed MinMaxHealthPercent = Fixed.FromRatio(-9, 10);

        /// <summary>Plafond du pourcentage de PV max résultant (+1 000 %).</summary>
        public static readonly Fixed MaxMaxHealthPercent = Fixed.FromInt(10);

        private static readonly Fixed MaxInt = Fixed.FromInt(int.MaxValue);

        public ModifierSet Modifiers { get; }
        public Fixed GuardDamageReduction { get; }
        public Fixed GuardUpkeepPerSecond { get; }
        public long GuardCostPerBlock { get; }
        public bool GuardNoUpkeepBreak { get; }

        /// <summary>Sous-lot 8.4 : garde sans entretien (<c>guard.noUpkeep</c> permanent) ; <see cref="GuardUpkeepPerSecond"/> vaut alors 0.</summary>
        public bool GuardNoUpkeep { get; }
        public Fixed GuardReductionAtZeroEndurance { get; }
        public long GuardBreakShortenPerChargeMs { get; }
        public long ParryWindowMs { get; }
        public long ParryFailureRecoveryMs { get; }
        public int RampartChargesMax { get; }
        public int ResonancePerDirectHit { get; }
        public Fixed EnduranceRegenPerSecond { get; }

        /// <summary>Somme des modificateurs permanents de dégâts infligés (catégorie talent), sans écrêtage (la catégorie borne).</summary>
        public Fixed DamageDealt { get; }

        /// <summary>Somme des modificateurs permanents de dégâts subis ; écrêtée dans [−1, 0] à l'évaluation (<see cref="DamageTakenAt"/>).</summary>
        public Fixed DamageTaken { get; }

        /// <summary>
        /// Sous-lot 8.2 (X4) : puissance des boucliers posés par ce combattant (<c>shield.potencyDealt</c>), somme écrêtée dans
        /// [−1, <see cref="MaxShieldPotency"/>] : la valeur d'un bouclier est multipliée par 1 + cette puissance, jamais négative.
        /// </summary>
        public Fixed ShieldPotencyDealt { get; }

        /// <summary>Plafond de <see cref="ShieldPotencyDealt"/> (+1 000 %), comme le bonus de dégâts infligés.</summary>
        public static readonly Fixed MaxShieldPotency = Fixed.FromInt(10);

        /// <summary>
        /// Sous-lot 8.3 (X6) : part ajoutée à celle des protections posées par ce combattant (<c>protection.redirectRatio</c>), somme
        /// écrêtée dans [−1, 1] ; la part effective d'une protection (définition + cette part) est écrêtée dans [0, 1].
        /// </summary>
        public Fixed ProtectionRedirectRatio { get; }

        /// <summary>Sous-lot 8.3 (X6, Serment) : les dégâts transférés à ce combattant passent par sa garde quand il garde.</summary>
        public bool ProtectionRedirectThroughGuard { get; }

        /// <summary>Part effective transférée par une protection de cette définition posée par ce combattant, dans [0, 1].</summary>
        public Fixed RedirectRatio(StatusDefinition protection) => Fixed.Clamp(protection.RedirectRatio + ProtectionRedirectRatio, Fixed.Zero, Fixed.One);

        private readonly long _tickMs;

        private CombatantRules(CombatRules rules, ModifierSet m, ShieldGuard? shield)
        {
            Modifiers = m;
            Shield = shield;
            _tickMs = rules.TickMilliseconds;
            _maxGuardReduction = rules.MaxGuardDamageReduction;
            _maxTotalReduction = rules.MaxTotalDamageReduction;
            // Sous-lot V1.2 : le bouclier porté remplace les trois valeurs de base de la garde et de la parade ; les modificateurs s'y
            // ajoutent comme aux valeurs des règles, sous les mêmes écrêtages. Les valeurs du bouclier sont d'abord ramenées dans leurs
            // intervalles de jeu (garde au plus son plafond, coût positif, fenêtre d'un tick à 500 ms) : à la différence d'une base des règles
            // de test, un bouclier ne sort jamais des plafonds.
            long cap = MaxParryWindowMs - MaxParryWindowMs % _tickMs;
            Fixed guardBase = shield is null ? rules.GuardDamageReduction : Fixed.Clamp(shield.DamageReduction, Fixed.Zero, _maxGuardReduction);
            long costBase = shield is null ? rules.GuardCostPerBlock : Math.Max(0, shield.CostPerBlock);
            long windowBase = shield is null ? rules.ParryWindowMs : Math.Min(Math.Max(shield.ParryWindowMs, _tickMs), Math.Max(cap, _tickMs));
            GuardDamageReduction = Clamp(guardBase, m.Sum(ModifierField.GuardDamageReduction), Fixed.Zero, _maxGuardReduction);
            GuardNoUpkeep = m.Flag(ModifierField.GuardNoUpkeep);
            GuardUpkeepPerSecond = GuardNoUpkeep ? Fixed.Zero
                : Clamp(Fixed.FromInt(rules.GuardUpkeepPerSecond), m.Sum(ModifierField.GuardUpkeepPerSecond), Fixed.Zero, MaxInt);
            GuardCostPerBlock = Whole(Clamp(Fixed.FromInt(costBase), m.Sum(ModifierField.GuardCostPerBlock), Fixed.Zero, MaxInt));
            GuardNoUpkeepBreak = m.Flag(ModifierField.GuardNoUpkeepBreak);
            // Les deux combinaisons inertes du drapeau valent 0, pour que le journal ne les montre pas comme appliquées : la réduction à
            // 0 d'endurance ne sert que si la garde tient sans endurance (noUpkeepBreak) ; le raccourci de rupture, que si la garde peut
            // encore se briser (avec le drapeau, ni l'entretien ni un blocage ne la brisent, et aucune autre rupture n'existe). D'autres
            // valeurs peuvent rester sans effet selon le build (réduction à 0 d'endurance avec un coût de blocage nul, raccourci avec
            // 0 Charge max) : elles sont gardées et journalisées telles quelles. « Ramenée à » : jamais au-dessus de la réduction de
            // garde effective (0 d'endurance ne vaut pas mieux qu'une garde payée).
            GuardReductionAtZeroEndurance = !GuardNoUpkeepBreak ? Fixed.Zero : Fixed.Min(GuardDamageReduction,
                Clamp(Fixed.Zero, m.Sum(ModifierField.GuardReductionAtZeroEndurance), Fixed.Zero, _maxGuardReduction));
            GuardBreakShortenPerChargeMs = GuardNoUpkeepBreak ? 0
                : Whole(Clamp(Fixed.Zero, m.Sum(ModifierField.GuardBreakShortenPerCharge), Fixed.Zero, MaxDuration));
            long windowCap = MaxParryWindowMs - MaxParryWindowMs % _tickMs;
            ParryWindowMs = Whole(Clamp(Fixed.FromInt(windowBase), m.Sum(ModifierField.ParryWindowMs), Fixed.FromInt(_tickMs), Fixed.FromInt(windowCap)));
            ParryFailureRecoveryMs = Whole(Clamp(Fixed.FromInt(rules.ParryFailureRecoveryMs), m.Sum(ModifierField.ParryFailureRecoveryMs), Fixed.Zero, MaxDuration));
            RampartChargesMax = (int)Whole(Clamp(Fixed.FromInt(rules.RampartChargesMax), m.Sum(ModifierField.RampartChargesMax), Fixed.Zero, Fixed.FromInt(MaxRampartCharges)));
            ResonancePerDirectHit = (int)Whole(Clamp(Fixed.FromInt(rules.ResonancePerDirectHit), m.Sum(ModifierField.ResonancePerDirectHit), Fixed.Zero, MaxInt));
            EnduranceRegenPerSecond = Clamp(Fixed.FromInt(rules.EnduranceRegenPerSecond), m.Sum(ModifierField.EnduranceRegenPerSecond), Fixed.Zero, MaxInt);
            DamageDealt = m.Sum(ModifierField.DamageDealt);
            DamageTaken = m.Sum(ModifierField.DamageTaken);
            ShieldPotencyDealt = Fixed.Clamp(m.Sum(ModifierField.ShieldPotencyDealt), -Fixed.One, MaxShieldPotency);
            ProtectionRedirectRatio = Fixed.Clamp(m.Sum(ModifierField.ProtectionRedirectRatio), -Fixed.One, Fixed.One);
            ProtectionRedirectThroughGuard = m.Flag(ModifierField.ProtectionRedirectThroughGuard);
            _baseUpkeep = Fixed.FromInt(rules.GuardUpkeepPerSecond);
        }

        private readonly Fixed _baseUpkeep;

        /// <summary>
        /// Sous-lot 8.2 (X3) : entretien de garde par seconde avec les renforcements actifs du porteur (<paramref name="temporary"/>,
        /// somme de leurs <c>guard.upkeepPerSecond</c>) : base des règles + modificateurs permanents + renforcements, écrêtée comme
        /// <see cref="GuardUpkeepPerSecond"/> ; sans renforcement, exactement <see cref="GuardUpkeepPerSecond"/>. Sous-lot 8.4 : 0 avec
        /// <c>guard.noUpkeep</c>, permanent ou porté par un renforcement actif (<paramref name="temporaryNoUpkeep"/>).
        /// </summary>
        public Fixed GuardUpkeepPerSecondWith(Fixed temporary, bool temporaryNoUpkeep = false) =>
            GuardNoUpkeep || temporaryNoUpkeep ? Fixed.Zero
                : temporary == Fixed.Zero ? GuardUpkeepPerSecond
                : Clamp(_baseUpkeep, Modifiers.Sum(ModifierField.GuardUpkeepPerSecond) + temporary, Fixed.Zero, MaxInt);

        private static readonly Fixed MaxDuration = Fixed.FromInt(CombatRules.AbsoluteMaxDurationMs);

        /// <summary>Règles effectives d'un combattant ; <see cref="ModifierSet.Empty"/> donne les règles de base.</summary>
        public static CombatantRules For(CombatRules rules, ModifierSet modifiers) => For(rules, modifiers, null);

        /// <summary>
        /// Sous-lot V1.2 : règles effectives d'un combattant qui porte le bouclier <paramref name="shield"/> (null : aucun, les valeurs des
        /// règles). La garde du bouclier est supposée jouable (<see cref="ShieldGuard.Check"/>, appelée par la validation du combat) ; une
        /// valeur hors des bornes de jeu reste écrêtée comme une base des règles de test.
        /// </summary>
        public static CombatantRules For(CombatRules rules, ModifierSet modifiers, ShieldGuard? shield) =>
            new CombatantRules(rules ?? throw new ArgumentNullException(nameof(rules)), modifiers ?? throw new ArgumentNullException(nameof(modifiers)), shield);

        /// <summary>Sous-lot V1.2 : garde du bouclier porté, ou null (valeurs des règles).</summary>
        public ShieldGuard? Shield { get; }

        /// <summary>base + somme, écrêtée dans [min(bas, base), max(haut, base)].</summary>
        private static Fixed Clamp(Fixed @base, Fixed sum, Fixed low, Fixed high) =>
            sum == Fixed.Zero ? @base : Fixed.Clamp(@base + sum, Fixed.Min(low, @base), Fixed.Max(high, @base));

        private static long Whole(Fixed value) => value.Raw / Fixed.Scale;

        /// <summary>Ajout (ms) aux durées des applications directes de cette altération par ce combattant.</summary>
        public long DurationDealtMs(string status) => Whole(Modifiers.Sum(ModifierField.StatusDurationDealt, status));

        /// <summary>Ajout (ms) aux durées de toute application de cette altération sur ce combattant.</summary>
        public long DurationTakenMs(string status) => Whole(Modifiers.Sum(ModifierField.StatusDurationTaken, status));

        /// <summary>Coefficient effectif d'une compétence : base + modificateurs, écrêté dans [0, <see cref="SkillCombatDefinition.MaxCoefficient"/>].</summary>
        public Fixed Coefficient(SkillDefinition skill, SkillCombatDefinition combat) => Coefficient(skill, combat, 0);

        /// <summary>
        /// Sous-lot 8.4 (X8) : coefficient effectif d'un lancement qui a dépensé <paramref name="chargesSpent"/> Charges. L'ajout par Charge
        /// ne porte la base du lancement qu'au plus à <see cref="SkillCombatDefinition.MaxCoefficient"/> (revue Codex de la PR #8 : 0,6 + 2 × 100
        /// donne 100, pas 200,6) ; une base déjà au-delà (définition non validée, contrat du sous-lot 8.1) n'est ni réduite ni augmentée. Les
        /// modificateurs s'ajoutent ensuite sous le même écrêtage que <see cref="Coefficient(SkillDefinition, SkillCombatDefinition)"/>. Sans débordement : coefficient par Charge ≤ 100 et
        /// Charges dépensées ≤ 2³¹ − 1, soit au plus 2,2 × 10¹⁵ en valeur brute, loin de 2⁶³ (arithmétique vérifiée de toute façon). Le
        /// précontrôle numérique borne les dégâts avec le maximum de Charges du combattant.
        /// </summary>
        public Fixed Coefficient(SkillDefinition skill, SkillCombatDefinition combat, int chargesSpent) =>
            Clamp(combat.Coefficient + Fixed.Min(combat.CoefficientPerRampartCharge * chargesSpent,
                    Fixed.Max(Fixed.Zero, SkillCombatDefinition.MaxCoefficient - combat.Coefficient)),
                Modifiers.Sum(ModifierField.SkillCoefficient, skill.Id), Fixed.Zero, SkillCombatDefinition.MaxCoefficient);

        /// <summary>Recharge effective d'une compétence (ms) : base + modificateurs, écrêtée dans [0, 24 h] ; multiple du tick (validé).</summary>
        public long CooldownMs(SkillDefinition skill, SkillCombatDefinition combat) =>
            Whole(Clamp(Fixed.FromInt(combat.CooldownMs), Modifiers.Sum(ModifierField.SkillCooldownMs, skill.Id), Fixed.Zero, MaxDuration));

        /// <summary>
        /// Valeur des modificateurs d'un champ de dégâts sur l'état courant du porteur (X2) : permanent + chaque conditionnel dont les
        /// conditions tiennent, multiplié par les Charges ou les paliers de Résonance. Sans débordement possible : valeurs bornées par la
        /// validation, Charges ≤ max(base, 10), Résonance ≤ 2³¹.
        /// </summary>
        private Fixed Evaluate(ModifierField field, Fixed permanent, bool guarding, long charges, long resonance)
        {
            Fixed total = permanent;
            foreach (ConditionalModifier c in Modifiers.Conditional)
            {
                if (c.Field != field) continue;
                if ((c.Conditions & ModifierConditions.WhileGuarding) != 0 && !guarding) continue;
                long factor = (c.Conditions & ModifierConditions.PerRampartCharge) != 0 ? charges
                    : (c.Conditions & ModifierConditions.PerResonanceStep) != 0 ? resonance / ModifierKeys.ResonanceStep : 1;
                total += c.Value * factor;
            }
            return total;
        }

        /// <summary>Bonus de dégâts infligés (catégorie talent) de l'attaquant dans l'état donné.</summary>
        public Fixed DamageDealtAt(bool guarding, long charges, long resonance) =>
            Evaluate(ModifierField.DamageDealt, DamageDealt, guarding, charges, resonance);

        /// <summary>Modificateur de dégâts subis du défenseur dans l'état donné, écrêté dans [−1, 0] : une réduction seulement.</summary>
        public Fixed DamageTakenAt(bool guarding, long charges, long resonance) =>
            Fixed.Clamp(Evaluate(ModifierField.DamageTaken, DamageTaken, guarding, charges, resonance), -Fixed.One, Fixed.Zero);

        /// <summary>
        /// Sous-lot 8.2 (X3) : modificateur de dégâts subis avec les renforcements actifs du défenseur (<paramref name="temporary"/>, somme
        /// de leurs <c>damageTaken</c>, négative ou nulle) : permanent + conditionnels + renforcements, écrêté dans [−1, 0], avant le plafond
        /// de la réduction totale. Sans renforcement, exactement <see cref="DamageTakenAt(bool, long, long)"/>.
        /// </summary>
        public Fixed DamageTakenAt(bool guarding, long charges, long resonance, Fixed temporary) =>
            temporary == Fixed.Zero ? DamageTakenAt(guarding, charges, resonance)
                : Fixed.Clamp(Evaluate(ModifierField.DamageTaken, DamageTaken, guarding, charges, resonance) + temporary, -Fixed.One, Fixed.Zero);

        /// <summary>
        /// Majorant du bonus de dégâts infligés sur tout le combat (précontrôle) : permanent + chaque conditionnel positif à son
        /// facteur maximal (Charges max effectives, ⌊Résonance max / palier⌋). Égal à 0 sans modificateur.
        /// </summary>
        public Fixed WorstDamageDealt(int resonanceMax)
        {
            Fixed worst = DamageDealt;
            foreach (ConditionalModifier c in Modifiers.Conditional)
            {
                if (c.Field != ModifierField.DamageDealt || c.Value <= Fixed.Zero) continue;
                long factor = (c.Conditions & ModifierConditions.PerRampartCharge) != 0 ? RampartChargesMax
                    : (c.Conditions & ModifierConditions.PerResonanceStep) != 0 ? Math.Max(0, resonanceMax) / ModifierKeys.ResonanceStep : 1;
                worst += c.Value * factor;
            }
            return worst;
        }

        /// <summary>
        /// Réduction totale d'un impact direct (B2, multiplicative) avec le plafond des règles du combat
        /// (<see cref="CombatRules.MaxTotalDamageReduction"/>).
        /// </summary>
        public Fixed TotalReduction(Fixed defense, Fixed damageTaken) => TotalReduction(defense, damageTaken, _maxTotalReduction);

        /// <summary>
        /// Réduction totale d'un impact direct (B2, multiplicative) : r = min(plafond, 1 − (1 − d)(1 + v)), d = réduction de la défense
        /// (garde, ou 0), v = modificateur de dégâts subis dans [−1, 0], plafond = <paramref name="maxTotal"/> (85 % dans les règles
        /// livrées). Une seule multiplication arrondie. v = 0 donne r = d exactement, et le plafond ne descend jamais sous d. La parade
        /// (hors plafond) n'y passe pas : sur un impact paré, la réduction est celle de la parade.
        /// </summary>
        public static Fixed TotalReduction(Fixed defense, Fixed damageTaken, Fixed maxTotal)
        {
            if (damageTaken == Fixed.Zero) return defense;
            Fixed reduction = Fixed.One - (Fixed.One - defense) * (Fixed.One + damageTaken);
            return Fixed.Min(reduction, Fixed.Max(maxTotal, defense));
        }
    }
}
