using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Characters;
using EdS.Data.Saves;
using EdS.Domain.Builds;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>Sous-lot V1.5 : une place de la barre de compétences, ouverte ou fermée, et ce qu'elle contient.</summary>
    public sealed class SkillPlace
    {
        public SkillKind Kind { get; }

        /// <summary>Rang dans sa sorte (de 0 ; 0 pour l'ultime).</summary>
        public int Index { get; }

        /// <summary>Touche en combat : « 1 » à « 4 » pour les actives, « R » pour l'ultime, vide pour un passif.</summary>
        public string Key { get; }

        /// <summary>Niveau qui ouvre la place (règles de progression).</summary>
        public int OpenLevel { get; }

        /// <summary>Place ouverte au niveau du personnage.</summary>
        public bool IsOpen { get; }

        /// <summary>Compétence dans la place (à la prochaine halte), ou null.</summary>
        public string? Skill { get; }

        internal SkillPlace(SkillKind kind, int index, string key, int openLevel, bool isOpen, string? skill)
        {
            Kind = kind;
            Index = index;
            Key = key;
            OpenLevel = openLevel;
            IsOpen = isOpen;
            Skill = skill;
        }
    }

    /// <summary>Sous-lot V1.5 : une compétence de la classe telle que l'écran la montre (nom, sorte, coût, recharge, portée, description).</summary>
    public sealed class SkillCard
    {
        public string Id { get; }
        public string Name { get; }
        public SkillKind Kind { get; }

        /// <summary>Niveau où le personnage l'apprend (1 : dès le départ).</summary>
        public int Level { get; }

        /// <summary>Apprise au niveau du personnage.</summary>
        public bool Learned { get; }

        /// <summary>Place qu'elle occupe à la prochaine halte ; null : en réserve, ou pas encore apprise.</summary>
        public SkillPlace? Place { get; }

        /// <summary>Coût en endurance (0 sans coût, ou passive).</summary>
        public int EnduranceCost { get; }

        /// <summary>Recharge en secondes (0 sans recharge, ou passive).</summary>
        public double CooldownSeconds { get; }

        /// <summary>Portée en mètres, ou null (sans portée, ou passive).</summary>
        public double? RangeMeters { get; }

        /// <summary>Description (texte <c>text.skill.…</c>), vide si elle manque.</summary>
        public string Description { get; }

        internal SkillCard(SkillDefinition skill, int level, bool learned, SkillPlace? place, string description)
        {
            Id = skill.Id;
            Name = skill.Name;
            Kind = skill.Kind;
            Level = level;
            Learned = learned;
            Place = place;
            EnduranceCost = skill.Combat?.EnduranceCost ?? 0;
            CooldownSeconds = (skill.Combat?.CooldownMs ?? 0) / 1000.0;
            RangeMeters = skill.Combat?.Range is { } range ? range.Raw / 10000.0 : (double?)null;
            Description = description;
        }
    }

    /// <summary>
    /// Sous-lot V1.5 (écran des compétences) : ce que l'écran montre, calculé depuis le personnage tel qu'il entrera à la prochaine halte
    /// (<see cref="WorldSession.NextEntry"/>) : les places de la barre (actives, ultime, passives ; ouvertes ou fermées, avec le niveau qui
    /// les ouvre), les compétences apprises (avec leur place, ou en réserve) et celles à apprendre plus haut. Lecture seule : les changements
    /// passent par <see cref="AdventureSession.PlaceSkill"/>, donc par le cœur.
    /// </summary>
    public sealed class SkillBook
    {
        public int Level { get; }

        /// <summary>Les compétences se changent maintenant (hors combat, <see cref="AdventureSession.CanChangeSkills"/>).</summary>
        public bool CanChange => Locked is null;

        /// <summary>Pourquoi elles ne se changent pas maintenant (<see cref="AdventureSession.SkillsLocked"/>), ou null.</summary>
        public string? Locked { get; }

        /// <summary>Places de la barre : actives, l'ultime, puis les passives, chacune dans l'ordre de ses rangs.</summary>
        public IReadOnlyList<SkillPlace> Places { get; }

        /// <summary>Compétences apprises, dans l'ordre de la progression (départ, puis niveaux).</summary>
        public IReadOnlyList<SkillCard> Learned { get; }

        /// <summary>Compétences à apprendre plus haut, dans l'ordre des niveaux.</summary>
        public IReadOnlyList<SkillCard> Upcoming { get; }

        private SkillBook(int level, string? locked, IReadOnlyList<SkillPlace> places, IReadOnlyList<SkillCard> learned, IReadOnlyList<SkillCard> upcoming)
        {
            Level = level;
            Locked = locked;
            Places = places;
            Learned = learned;
            Upcoming = upcoming;
        }

        /// <summary>Carte d'une compétence (apprise ou à venir), ou null.</summary>
        public SkillCard? Card(string skill) => Learned.Concat(Upcoming).FirstOrDefault(c => c.Id == skill);

        /// <summary>
        /// Relecture indépendante de la PR #71 : une compétence apprise de la sorte <paramref name="kind"/> attend en réserve ; sinon, une place
        /// libre de cette sorte n'a rien à recevoir pour l'instant (aujourd'hui la 2e passive, au niveau 5).
        /// </summary>
        public bool HasReserve(SkillKind kind) => Learned.Any(c => c.Place == null && c.Kind == kind);

        /// <summary>Place de rang <paramref name="index"/> de la sorte <paramref name="kind"/>, ou null.</summary>
        public SkillPlace? Place(SkillKind kind, int index) => Places.FirstOrDefault(p => p.Kind == kind && p.Index == index);

        /// <summary>
        /// Écran de <paramref name="next"/> (le personnage à la prochaine halte) ; <paramref name="locked"/> : pourquoi les compétences ne se
        /// changent pas maintenant, null si elles se changent ; <paramref name="describe"/> donne la description d'une compétence d'après son
        /// identifiant (vide si aucune).
        /// </summary>
        public static SkillBook For(SavedCharacter next, CharacterProgression progression, ClassDefinition definition, ProgressionRules rules, string? locked,
            Func<string, string>? describe = null)
        {
            if (next is null) throw new ArgumentNullException(nameof(next));
            if (progression is null) throw new ArgumentNullException(nameof(progression));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (rules is null) throw new ArgumentNullException(nameof(rules));
            int level = PlayerCharacters.Level(next, progression);
            var places = new List<SkillPlace>();
            for (int i = 0; i < rules.ActiveSlotLevels.Count; i++)
                places.Add(new SkillPlace(SkillKind.Active, i, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), rules.ActiveSlotLevels[i],
                    rules.ActiveSlotLevels[i] <= level, i < next.ActiveSkills.Count ? next.ActiveSkills[i] : null));
            places.Add(new SkillPlace(SkillKind.Ultimate, 0, "R", rules.UltimateUnlockLevel, rules.UltimateUnlockLevel <= level, next.Ultimate));
            for (int i = 0; i < rules.PassiveSlotLevels.Count; i++)
                places.Add(new SkillPlace(SkillKind.Passive, i, "", rules.PassiveSlotLevels[i], rules.PassiveSlotLevels[i] <= level,
                    i < next.PassiveSkills.Count ? next.PassiveSkills[i] : null));

            ClassProgression p = progression.ForClass(next.ClassId);
            string Describe(string id) => describe?.Invoke(id) ?? "";
            SkillPlace? PlaceOf(string id) => places.FirstOrDefault(x => x.Skill == id);
            int LevelOf(string id) => p.SkillUnlocks.FirstOrDefault(u => u.Skill == id)?.Level ?? 1;
            var learned = new List<SkillCard>();
            foreach (string id in PlayerCharacters.Learned(next, progression))
                if (definition.TryGetSkill(id, out SkillDefinition skill)) learned.Add(new SkillCard(skill, LevelOf(id), true, PlaceOf(id), Describe(id)));
            // Une compétence dans la barre sans être apprise (sauvegarde d'avant, données changées) se montre aussi : on peut la remplacer.
            foreach (string id in next.ActiveSkills.Concat(next.Ultimate is null ? Array.Empty<string>() : new[] { next.Ultimate }).Concat(next.PassiveSkills))
                if (learned.All(c => c.Id != id) && definition.TryGetSkill(id, out SkillDefinition kept))
                    learned.Add(new SkillCard(kept, LevelOf(id), true, PlaceOf(id), Describe(id)));
            var upcoming = new List<SkillCard>();
            foreach (SkillUnlock u in PlayerCharacters.Upcoming(next, progression))
                if (definition.TryGetSkill(u.Skill, out SkillDefinition skill)) upcoming.Add(new SkillCard(skill, u.Level, false, null, Describe(u.Skill)));
            return new SkillBook(level, locked, places, learned, upcoming);
        }
    }

    /// <summary>
    /// Sous-lot V1.5 : une barre (actives, ultime, passives) et ses changements simples, sans jugement : la juger reste à
    /// <see cref="PlayerCharacters.CheckSkills"/>, le seul juge.
    /// </summary>
    public sealed class SkillBar
    {
        public IReadOnlyList<string> Actives { get; }
        public string? Ultimate { get; }
        public IReadOnlyList<string> Passives { get; }

        public SkillBar(IEnumerable<string> actives, string? ultimate, IEnumerable<string> passives)
        {
            Actives = (actives ?? throw new ArgumentNullException(nameof(actives))).ToList();
            Ultimate = ultimate;
            Passives = (passives ?? throw new ArgumentNullException(nameof(passives))).ToList();
        }

        /// <summary>Barre d'un personnage.</summary>
        public static SkillBar Of(SavedCharacter character) =>
            new SkillBar((character ?? throw new ArgumentNullException(nameof(character))).ActiveSkills, character.Ultimate, character.PassiveSkills);

        /// <summary>Même barre, mêmes compétences aux mêmes places.</summary>
        public bool SameAs(SkillBar other) =>
            other != null && Actives.SequenceEqual(other.Actives) && Ultimate == other.Ultimate && Passives.SequenceEqual(other.Passives);

        /// <summary>
        /// Met <paramref name="skill"/> à la place <paramref name="index"/> de la sorte <paramref name="kind"/> (0 pour l'ultime). Déjà dans
        /// cette sorte : elle échange sa place avec celle de la compétence visée (vers une place vide : elle passe à la fin, les places se
        /// remplissant dans l'ordre, sans trou). Sinon elle quitte la place qu'elle avait ailleurs et prend la place visée ; la compétence qui
        /// y était retourne en réserve (vers une place vide : elle s'ajoute à la fin).
        /// </summary>
        public SkillBar Place(SkillKind kind, int index, string skill)
        {
            if (skill is null) throw new ArgumentNullException(nameof(skill));
            if (index < 0 || (kind == SkillKind.Ultimate && index != 0)) throw new ArgumentOutOfRangeException(nameof(index), index, "Rang de place impossible.");
            var actives = Actives.ToList();
            var passives = Passives.ToList();
            string? ultimate = Ultimate;
            if (kind == SkillKind.Ultimate)
            {
                actives.Remove(skill);
                passives.Remove(skill);
                return new SkillBar(actives, skill, passives);
            }
            List<string> target = kind == SkillKind.Active ? actives : passives;
            int at = target.IndexOf(skill);
            if (at >= 0)
            {
                if (index < target.Count)
                {
                    target[at] = target[index];
                    target[index] = skill;
                }
                else
                {
                    target.RemoveAt(at);
                    target.Add(skill);
                }
                return new SkillBar(actives, ultimate, passives);
            }
            (kind == SkillKind.Active ? passives : actives).Remove(skill);
            if (ultimate == skill) ultimate = null;
            if (index < target.Count) target[index] = skill;
            else target.Add(skill);
            return new SkillBar(actives, ultimate, passives);
        }

        /// <summary>Retire la compétence de la place <paramref name="index"/> de la sorte <paramref name="kind"/> (les suivantes avancent) ; vide : rien ne change.</summary>
        public SkillBar Remove(SkillKind kind, int index)
        {
            if (index < 0 || (kind == SkillKind.Ultimate && index != 0)) throw new ArgumentOutOfRangeException(nameof(index), index, "Rang de place impossible.");
            if (kind == SkillKind.Ultimate) return new SkillBar(Actives, null, Passives);
            var target = (kind == SkillKind.Active ? Actives : Passives).ToList();
            if (index >= target.Count) return this;
            target.RemoveAt(index);
            return kind == SkillKind.Active ? new SkillBar(target, Ultimate, Passives) : new SkillBar(Actives, Ultimate, target);
        }
    }
}
