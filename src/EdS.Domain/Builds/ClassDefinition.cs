using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Collections;

namespace EdS.Domain.Builds
{
    /// <summary>
    /// Définition immuable d'une classe : ses arbres de talents et ses compétences équipables.
    /// Le moteur de talents est générique : aucune règle ne dépend d'une classe particulière.
    /// </summary>
    public sealed class ClassDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<TalentTreeDefinition> Trees { get; }
        public IReadOnlyList<SkillDefinition> Skills { get; }

        private readonly Dictionary<string, (TalentDefinition Talent, TalentTreeDefinition Tree)> _talents;
        private readonly Dictionary<string, SkillDefinition> _skills;

        public ClassDefinition(string id, string name, IReadOnlyList<TalentTreeDefinition> trees, IReadOnlyList<SkillDefinition> skills)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Trees = Frozen.List(trees, nameof(trees));
            Skills = Frozen.List(skills, nameof(skills));

            // Index tolérant aux doublons (signalés par Validate) : la première définition gagne.
            _talents = new Dictionary<string, (TalentDefinition, TalentTreeDefinition)>(StringComparer.Ordinal);
            // Index construits depuis les copies figées : ils décrivent toujours exactement Trees et Skills.
            foreach (TalentTreeDefinition tree in Trees)
                foreach (TalentDefinition talent in tree.Talents)
                    if (!_talents.ContainsKey(talent.Id)) _talents[talent.Id] = (talent, tree);
            _skills = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            foreach (SkillDefinition skill in Skills)
                if (!_skills.ContainsKey(skill.Id)) _skills[skill.Id] = skill;
        }

        public bool TryGetTalent(string id, out TalentDefinition talent, out TalentTreeDefinition tree)
        {
            if (_talents.TryGetValue(id, out var found))
            {
                talent = found.Talent;
                tree = found.Tree;
                return true;
            }
            talent = null!;
            tree = null!;
            return false;
        }

        public bool TryGetSkill(string id, out SkillDefinition skill) => _skills.TryGetValue(id, out skill!);

        /// <summary>
        /// Talents qui dépendent, directement ou par chaîne de prérequis, du talent donné.
        /// Ils ne peuvent pas avoir été acquis avant lui et ne comptent donc pas dans son seuil.
        /// </summary>
        public ISet<string> DependentsOf(string talentId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var entry in _talents.Values)
                {
                    if (result.Contains(entry.Talent.Id)) continue;
                    if (entry.Talent.Prerequisites.Any(p => p.TalentId == talentId || result.Contains(p.TalentId)))
                    {
                        result.Add(entry.Talent.Id);
                        added = true;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Vérifie la cohérence de la définition : identifiants, références, rangs, cycles de prérequis,
        /// et faisabilité de chaque talent majeur avec les règles de progression.
        /// Les chemins des erreurs ont la forme des fichiers de données (<c>trees[0].talents[2].maxRank</c>).
        /// </summary>
        /// <summary>
        /// Correctif 7.3.1 (A73-M03) : plafonds globaux d'une classe, vérifiés avant tout autre contrôle et avant l'analyse statique des
        /// cycles. Enveloppes techniques provisoires (la classe livrée a 18 talents et 11 compétences), pas des choix de conception.
        /// </summary>
        public const int MaxTalents = 256, MaxSkills = 256, MaxTriggers = 512;

        /// <summary>Sous-lot 8.1 : plafond global des modificateurs d'une classe (talents et compétences passives), comme les déclencheurs.</summary>
        public const int MaxModifiers = 512;

        public IReadOnlyList<RuleViolation> Validate(ProgressionRules progression)
        {
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            var errors = new List<RuleViolation>();
            // Plafonds globaux d'abord : au-delà, rien d'autre n'est examiné (le volume ne peut pas immobiliser la validation).
            int talents = Trees.Sum(t => t.Talents.Count);
            int triggers = Trees.Sum(t => t.Talents.Sum(x => x.Triggers.Count)) + Skills.Sum(x => x.Triggers.Count);
            long modifiers = Trees.Sum(t => t.Talents.Sum(x => (long)x.Modifiers.Count)) + Skills.Sum(x => (long)x.Modifiers.Count);
            if (talents > MaxTalents)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{talents} talents, au plus {MaxTalents} par classe (plafond technique provisoire).", "trees"));
            if (Skills.Count > MaxSkills)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded, $"{Skills.Count} compétences, au plus {MaxSkills} par classe (plafond technique provisoire).", "skills"));
            if (triggers > MaxTriggers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{triggers} déclencheurs (talents et compétences), au plus {MaxTriggers} par classe (plafond technique provisoire).", "skills"));
            if (modifiers > MaxModifiers)
                errors.Add(new RuleViolation(RuleViolationKind.LimitExceeded,
                    $"{modifiers} modificateurs (talents et compétences), au plus {MaxModifiers} par classe (plafond technique provisoire).", "skills"));
            if (errors.Count > 0) return errors;
            SkillDefinition? SkillOf(string id) => TryGetSkill(id, out SkillDefinition? s) ? s : null;
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);   // identifiant → chemin de sa première définition

            void CheckId(string id, string prefix, string path)
            {
                if (!StableId.HasPrefix(id, prefix))
                    errors.Add(new RuleViolation($"identifiant « {id} » invalide : « {prefix}.… » en minuscules, chiffres et « _ » attendu.", path));
                else if (ids.TryGetValue(id, out string? first))
                    errors.Add(new RuleViolation(RuleViolationKind.DuplicateId, $"identifiant « {id} » déjà utilisé ({first}).", path));
                else
                    ids[id] = path;
            }

            void CheckName(string name, string path)
            {
                if (string.IsNullOrWhiteSpace(name)) errors.Add(new RuleViolation("nom vide.", path));
            }

            CheckId(Id, "class", "id");
            CheckName(Name, "name");

            if (Trees.Count != progression.TreesPerClass)
                errors.Add(new RuleViolation($"{Trees.Count} arbre(s) ; une classe en a {progression.TreesPerClass}.", "trees"));

            for (int t = 0; t < Trees.Count; t++)
            {
                TalentTreeDefinition tree = Trees[t];
                string treePath = $"trees[{t}]";
                CheckId(tree.Id, "tree", treePath + ".id");
                CheckName(tree.Name, treePath + ".name");
                if (tree.Talents.Count == 0) errors.Add(new RuleViolation("un arbre doit contenir au moins un talent.", treePath + ".talents"));

                for (int i = 0; i < tree.Talents.Count; i++)
                {
                    TalentDefinition talent = tree.Talents[i];
                    string path = $"{treePath}.talents[{i}]";
                    CheckId(talent.Id, "talent", path + ".id");
                    CheckName(talent.Name, path + ".name");
                    if (talent.MaxRank < 1 || talent.MaxRank > TalentDefinition.MaxRankLimit)
                        errors.Add(new RuleViolation($"le rang maximum doit être compris entre 1 et {TalentDefinition.MaxRankLimit}.", path + ".maxRank"));
                    if (talent.IsMajor && talent.MaxRank != 1)
                        errors.Add(new RuleViolation("un talent majeur a un seul rang (1 point pour l'acquérir).", path + ".maxRank"));
                    // Lot 7.3 : déclencheurs du talent, valeurs bornées à chacun de ses rangs.
                    Combat.TriggerDefinition.ValidateList(talent.Triggers, path + ".triggers", talent.MaxRank, true, errors);
                    // Sous-lot 8.1 : modificateurs du talent, valeurs bornées à chacun de ses rangs.
                    Combat.ModifierDefinition.ValidateList(talent.Modifiers, path + ".modifiers", talent.MaxRank, true, SkillOf, errors);
                }
            }

            // Références : après l'indexation de tous les identifiants.
            for (int t = 0; t < Trees.Count; t++)
            {
                TalentTreeDefinition tree = Trees[t];
                for (int i = 0; i < tree.Talents.Count; i++)
                {
                    TalentDefinition talent = tree.Talents[i];
                    string path = $"trees[{t}].talents[{i}].prerequisites";
                    CheckRequirements(talent.Prerequisites, path, errors, talent.Id);

                    if (talent.IsMajor)
                    {
                        ISet<string> dependents = DependentsOf(talent.Id);
                        long available = tree.Talents.Where(x => !x.IsMajor && !dependents.Contains(x.Id) && x.MaxRank > 0).Sum(x => (long)x.MaxRank);
                        if (available < progression.MajorTalentThreshold)
                            errors.Add(new RuleViolation(
                                $"talent majeur inaccessible : la branche n'offre que {available} point(s) hors talents majeurs, il en faut {progression.MajorTalentThreshold}.",
                                $"trees[{t}].talents[{i}]"));
                    }
                }
            }

            CheckCycles(errors);

            for (int s = 0; s < Skills.Count; s++)
            {
                SkillDefinition skill = Skills[s];
                string path = $"skills[{s}]";
                CheckId(skill.Id, "skill", path + ".id");
                CheckName(skill.Name, path + ".name");
                if (!Enum.IsDefined(typeof(SkillKind), skill.Kind))
                    errors.Add(new RuleViolation("type de compétence inconnu.", path + ".kind"));
                if (!progression.IsValidLevel(skill.MinLevel))
                    errors.Add(new RuleViolation($"niveau minimal hors de [1, {progression.MaxLevel}].", path + ".minLevel"));
                if (skill.Kind == SkillKind.Ultimate && skill.MinLevel < progression.UltimateUnlockLevel)
                    errors.Add(new RuleViolation($"une ultime ne peut pas être disponible avant le niveau {progression.UltimateUnlockLevel}.", path + ".minLevel"));
                CheckRequirements(skill.TalentRequirements, path + ".talentRequirements", errors, null);
                if (skill.Combat is { } combat) CheckCombat(skill, combat, path + ".combat", errors);
                Combat.TriggerDefinition.ValidateList(skill.Triggers, path + ".triggers", 1, false, errors);
                // Sous-lot 8.1 : seuls les talents et les compétences passives portent des modificateurs (rang 1, sans perRank).
                if (skill.Kind != SkillKind.Passive && skill.Modifiers.Count > 0)
                    errors.Add(new RuleViolation("seuls les talents et les compétences passives portent des modificateurs.", path + ".modifiers"));
                else
                    Combat.ModifierDefinition.ValidateList(skill.Modifiers, path + ".modifiers", 1, false, SkillOf, errors);
            }

            return errors;
        }

        /// <summary>Lot 7.3 : déclencheurs de la classe nommés pour le graphe statique (A15), dans l'ordre du fichier.</summary>
        public IReadOnlyList<Combat.NamedTrigger> NamedTriggers()
        {
            var named = new List<Combat.NamedTrigger>();
            for (int t = 0; t < Trees.Count; t++)
                for (int i = 0; i < Trees[t].Talents.Count; i++)
                    for (int k = 0; k < Trees[t].Talents[i].Triggers.Count; k++)
                        named.Add(new Combat.NamedTrigger($"{Trees[t].Talents[i].Id}/{k}", $"trees[{t}].talents[{i}].triggers[{k}]", Trees[t].Talents[i].Triggers[k]));
            for (int s = 0; s < Skills.Count; s++)
                for (int k = 0; k < Skills[s].Triggers.Count; k++)
                    named.Add(new Combat.NamedTrigger($"{Skills[s].Id}/{k}", $"skills[{s}].triggers[{k}]", Skills[s].Triggers[k]));
            return named;
        }

        private static void CheckCombat(SkillDefinition skill, SkillCombatDefinition combat, string path, List<RuleViolation> errors)
        {
            if (skill.Kind == SkillKind.Passive)
                errors.Add(new RuleViolation("une compétence passive ne se lance pas : pas de données de combat.", path));
            if (combat.HasDamage && !Enum.IsDefined(typeof(Stats.PowerType), combat.PowerType))
                errors.Add(new RuleViolation("type de puissance inconnu.", path + ".powerType"));
            if (combat.HasDamage && !Enum.IsDefined(typeof(Stats.DamageType), combat.DamageType))
                errors.Add(new RuleViolation("type de dégâts inconnu.", path + ".damageType"));
            if (!Enum.IsDefined(typeof(SkillTargeting), combat.Targeting))
                errors.Add(new RuleViolation("ciblage inconnu.", path + ".targeting"));
            // Sous-lot 8.3 (X5) : une compétence qui vise soi, un allié ou tous les alliés est de soutien (aucun dégât direct) ; une
            // compétence de soutien n'a pas d'imparable (aucun impact à défendre).
            else if (combat.Targeting.IsFriendly() && combat.HasDamage)
                errors.Add(new RuleViolation("une compétence qui vise soi, un allié ou tous les alliés n'inflige pas de dégâts : powerType, damageType et coefficient absents.", path + ".targeting"));
            if (!combat.HasDamage && combat.Unblockable)
                errors.Add(new RuleViolation("une compétence sans dégâts directs n'a aucun impact à rendre imparable : unblockable vaut false.", path + ".unblockable"));
            if (combat.HasDamage && (combat.Coefficient < Numerics.Fixed.Zero || combat.Coefficient > SkillCombatDefinition.MaxCoefficient))
                errors.Add(new RuleViolation($"coefficient hors de [0, {SkillCombatDefinition.MaxCoefficient}].", path + ".coefficient"));
            if (combat.EnduranceCost < 0 || combat.EnduranceCost > SkillCombatDefinition.MaxCost)
                errors.Add(new RuleViolation($"coût en endurance hors de [0, {SkillCombatDefinition.MaxCost}].", path + ".enduranceCost"));
            if (combat.RampartChargeCost < 0 || combat.RampartChargeCost > SkillCombatDefinition.MaxCost)
                errors.Add(new RuleViolation($"coût en Charges de rempart hors de [0, {SkillCombatDefinition.MaxCost}].", path + ".rampartChargeCost"));
            if (combat.RampartChargeGain < 0 || combat.RampartChargeGain > SkillCombatDefinition.MaxCost)
                errors.Add(new RuleViolation($"gain de Charges de rempart hors de [0, {SkillCombatDefinition.MaxCost}].", path + ".rampartChargeGain"));
            // Sous-lot 8.4 (X8) : coefficient par Charge dépensée, borné comme un coefficient ; sans dégâts directs ou sans dépense de
            // Charges possible, il serait sans effet.
            if (combat.CoefficientPerRampartCharge < Numerics.Fixed.Zero || combat.CoefficientPerRampartCharge > SkillCombatDefinition.MaxCoefficient)
                errors.Add(new RuleViolation($"coefficient par Charge hors de [0, {SkillCombatDefinition.MaxCoefficient}].", path + ".coefficientPerRampartCharge"));
            else if (combat.CoefficientPerRampartCharge > Numerics.Fixed.Zero && !combat.HasDamage)
                errors.Add(new RuleViolation("coefficient par Charge sans dégâts directs : sans effet.", path + ".coefficientPerRampartCharge"));
            else if (combat.CoefficientPerRampartCharge > Numerics.Fixed.Zero && combat.RampartChargeCost == 0 && !combat.RampartChargeSpendAll)
                errors.Add(new RuleViolation("coefficient par Charge sans Charge dépensée (rampartChargeCost nul, rampartChargeSpendAll faux) : sans effet.",
                    path + ".coefficientPerRampartCharge"));
            // Sous-lot 8.4 : gain de Résonance au lancement, jamais sur une ultime (elle dépense la Résonance, A5).
            if (combat.ResonanceGain < 0 || combat.ResonanceGain > SkillCombatDefinition.MaxCost)
                errors.Add(new RuleViolation($"gain de Résonance hors de [0, {SkillCombatDefinition.MaxCost}].", path + ".resonanceGain"));
            else if (combat.ResonanceGain > 0 && skill.Kind == SkillKind.Ultimate)
                errors.Add(new RuleViolation("une ultime dépense la Résonance : aucun gain de Résonance à son lancement.", path + ".resonanceGain"));
            if (combat.CooldownMs < 0 || combat.CooldownMs > Combat.CombatRules.AbsoluteMaxDurationMs)
                errors.Add(new RuleViolation($"recharge hors de [0, {Combat.CombatRules.AbsoluteMaxDurationMs}] ms.", path + ".cooldownMs"));
            // Sous-lot 11.4b : portée bornée ; une compétence sur soi n'en a pas.
            if (combat.Range is { } range)
            {
                if (!Space.SpaceLimits.IsRange(range))
                    errors.Add(new RuleViolation(RuleViolationKind.NumericLimit,
                        $"portée hors de [{Space.SpaceLimits.MinRange}, {Space.SpaceLimits.MaxRange}] m (borne technique).", path + ".range"));
                else if (combat.Targeting == SkillTargeting.Self)
                    errors.Add(new RuleViolation("une compétence sur soi n'a pas de portée.", path + ".range"));
            }
            // Lot 7.2 : forme des altérations portées ; leur existence est vérifiée avec le catalogue des règles (CombatSetup.Validate).
            Combat.StatusApplication.ValidateList(combat.Applies, path + ".applies", errors);
        }

        private void CheckRequirements(IReadOnlyList<TalentRequirement> requirements, string path, List<RuleViolation> errors, string? ownerId)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 0; r < requirements.Count; r++)
            {
                TalentRequirement requirement = requirements[r];
                string rPath = $"{path}[{r}]";
                if (!TryGetTalent(requirement.TalentId, out TalentDefinition required, out _))
                {
                    errors.Add(new RuleViolation(RuleViolationKind.UnknownReference, $"talent inconnu « {requirement.TalentId} ».", rPath + ".talent"));
                    continue;
                }
                if (!seen.Add(requirement.TalentId))
                    errors.Add(new RuleViolation($"le talent « {requirement.TalentId} » est exigé deux fois.", rPath + ".talent"));
                if (requirement.TalentId == ownerId)
                    errors.Add(new RuleViolation("un talent ne peut pas s'exiger lui-même.", rPath + ".talent"));
                if (requirement.MinRank < 1 || requirement.MinRank > required.MaxRank)
                    errors.Add(new RuleViolation($"rang exigé {requirement.MinRank} hors de [1, {required.MaxRank}] pour « {required.Name} ».", rPath + ".rank"));
                if (required.IsMajor && ownerId != null && TryGetTalent(ownerId, out TalentDefinition owner, out _) && owner.IsMajor)
                    errors.Add(new RuleViolation("un talent majeur ne peut pas exiger un autre talent majeur.", rPath + ".talent"));
            }
        }

        private void CheckCycles(List<RuleViolation> errors)
        {
            // Parcours en profondeur : 0 = non visité, 1 = en cours, 2 = terminé.
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);

            void Visit(string id, Stack<string> chain)
            {
                state[id] = 1;
                chain.Push(id);
                TryGetTalent(id, out TalentDefinition talent, out _);
                foreach (TalentRequirement p in talent.Prerequisites)
                {
                    if (!_talents.ContainsKey(p.TalentId) || p.TalentId == id) continue;
                    state.TryGetValue(p.TalentId, out int s);
                    if (s == 0) Visit(p.TalentId, chain);
                    else if (s == 1 && reported.Add(p.TalentId))
                    {
                        string cycle = string.Join(" → ", chain.Reverse().SkipWhile(x => x != p.TalentId).Concat(new[] { p.TalentId }));
                        errors.Add(new RuleViolation($"cycle de prérequis : {cycle}.", PathOf(p.TalentId) + ".prerequisites"));
                    }
                }
                chain.Pop();
                state[id] = 2;
            }

            foreach (string id in _talents.Keys)
                if (!state.ContainsKey(id)) Visit(id, new Stack<string>());
        }

        private string PathOf(string talentId)
        {
            for (int t = 0; t < Trees.Count; t++)
                for (int i = 0; i < Trees[t].Talents.Count; i++)
                    if (Trees[t].Talents[i].Id == talentId) return $"trees[{t}].talents[{i}]";
            return "trees";
        }
    }
}
