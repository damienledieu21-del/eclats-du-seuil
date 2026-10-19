using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Builds;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot 9.4 : édition des talents d'un build, point par point, pour l'interface de talents. Le build édité est toujours valide :
    /// chaque point ajouté ou retiré donne un build candidat jugé par <see cref="BuildValidator"/> (le seul validateur) ; s'il est refusé,
    /// rien ne change et la raison est rendue. Le niveau, les attributs et les compétences équipées restent ceux du build de départ.
    /// </summary>
    public sealed class TalentEditor
    {
        private readonly ClassDefinition _class;
        private readonly ProgressionRules _progression;

        /// <summary>Build édité, toujours valide pour la classe et les règles de progression.</summary>
        public CharacterBuild Build { get; private set; }

        /// <summary>Commence l'édition depuis un build valide (sinon <see cref="ArgumentException"/> avec les raisons du validateur).</summary>
        public TalentEditor(CharacterBuild build, ClassDefinition definition, ProgressionRules progression)
        {
            _class = definition ?? throw new ArgumentNullException(nameof(definition));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Build = Checked(build ?? throw new ArgumentNullException(nameof(build)), nameof(build));
        }

        public ClassDefinition Class => _class;

        /// <summary>Arbres de talents de la classe, dans l'ordre de sa définition.</summary>
        public IReadOnlyList<TalentTreeDefinition> Trees => _class.Trees;

        public int Level => Build.Level;

        /// <summary>Points de talent au niveau du build (règles de progression).</summary>
        public int PointsAvailable => _progression.TalentPoints(Build.Level);

        public long PointsSpent => Build.TalentRanks.Values.Where(r => r > 0).Sum(r => (long)r);

        public long PointsRemaining => PointsAvailable - PointsSpent;

        /// <summary>Points à investir dans un arbre, hors talents qui dépendent du majeur, avant d'acquérir ce majeur.</summary>
        public int MajorTalentThreshold => _progression.MajorTalentThreshold;

        public int Rank(string talentId) => Build.TalentRanks.TryGetValue(talentId ?? throw new ArgumentNullException(nameof(talentId)), out int rank) ? rank : 0;

        public long PointsInTree(TalentTreeDefinition tree) => BuildValidator.PointsInTree(Build, tree ?? throw new ArgumentNullException(nameof(tree)));

        /// <summary>Raison pour laquelle un point de plus serait refusé ; null s'il est admis.</summary>
        public BuildViolation? WhyNotAdd(string talentId) => Candidate(talentId, +1, out _);

        /// <summary>Raison pour laquelle un point de moins serait refusé ; null s'il est admis.</summary>
        public BuildViolation? WhyNotRemove(string talentId) => Candidate(talentId, -1, out _);

        /// <summary>Ajoute un point ; refusé (rien ne change), rend la raison ; admis, rend null.</summary>
        public BuildViolation? Add(string talentId) => Apply(talentId, +1);

        /// <summary>Retire un point ; refusé (rien ne change), rend la raison ; admis, rend null.</summary>
        public BuildViolation? Remove(string talentId) => Apply(talentId, -1);

        /// <summary>Retire tous les points (même niveau, attributs et compétences) ; refusé si une compétence équipée exige un talent.</summary>
        public BuildViolation? Clear()
        {
            CharacterBuild candidate = With(new Dictionary<string, int>(StringComparer.Ordinal));
            BuildViolation? why = BuildValidator.Validate(candidate, _class, _progression).FirstOrDefault();
            if (why is null) Build = candidate;
            return why;
        }

        /// <summary>Repart d'un autre build valide (par exemple un build de référence) ; sinon <see cref="ArgumentException"/>, rien ne change.</summary>
        public void Load(CharacterBuild build) => Build = Checked(build ?? throw new ArgumentNullException(nameof(build)), nameof(build));

        private CharacterBuild Checked(CharacterBuild build, string parameter)
        {
            IReadOnlyList<BuildViolation> violations = BuildValidator.Validate(build, _class, _progression);
            if (violations.Count > 0)
                throw new ArgumentException("Build invalide : " + string.Join(" ; ", violations.Select(v => v.ToString())), parameter);
            return build;
        }

        private BuildViolation? Apply(string talentId, int delta)
        {
            BuildViolation? why = Candidate(talentId, delta, out CharacterBuild? candidate);
            if (why is null) Build = candidate!;
            return why;
        }

        /// <summary>Build candidat avec un rang modifié de <paramref name="delta"/>, et la première raison de refus (null s'il est valide).</summary>
        private BuildViolation? Candidate(string talentId, int delta, out CharacterBuild? candidate)
        {
            if (talentId is null) throw new ArgumentNullException(nameof(talentId));
            candidate = null;
            if (!_class.TryGetTalent(talentId, out TalentDefinition talent, out _))
                return new BuildViolation(BuildViolationCode.UnknownTalent, talentId, "talent inconnu pour cette classe.");
            int rank = Rank(talentId);
            // Raisons évidentes, dites simplement ; le validateur reste juge de tout le reste.
            if (delta > 0 && rank >= talent.MaxRank)
                return new BuildViolation(BuildViolationCode.RankOutOfRange, talentId, $"« {talent.Name} » est déjà au rang maximal ({talent.MaxRank}).");
            if (delta > 0 && PointsRemaining <= 0)
                return new BuildViolation(BuildViolationCode.TooManyPoints, "talents", $"plus aucun point : {PointsAvailable} au niveau {Build.Level}, tous dépensés.");
            if (delta < 0 && rank <= 0)
                return new BuildViolation(BuildViolationCode.RankOutOfRange, talentId, $"aucun point à retirer de « {talent.Name} ».");

            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> entry in Build.TalentRanks) ranks[entry.Key] = entry.Value;
            int next = checked(rank + delta);
            if (next == 0) ranks.Remove(talentId);
            else ranks[talentId] = next;
            CharacterBuild built = With(ranks);
            BuildViolation? why = BuildValidator.Validate(built, _class, _progression).FirstOrDefault();
            if (why is null) candidate = built;
            return why;
        }

        private CharacterBuild With(IReadOnlyDictionary<string, int> ranks) =>
            new CharacterBuild(Build.ClassId, Build.Level, Build.Attributes, ranks, Build.ActiveSkills, Build.Ultimate, Build.PassiveSkills);
    }
}
