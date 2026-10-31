using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Domain.Collections;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V1.7 (sauvegarde en version 10) : la carte d'aventurier du personnage, gardée avec son histoire (<see cref="SavedStory.Guild"/>) :
    /// rang, points de guilde, contrats réussis, titres gagnés et titre affiché, contrats en cours (avec leur avancement), contrats déjà pris
    /// le jour <see cref="Day"/> du monde (un contrat du tableau se prend une fois par jour) et créatures rares déjà comptées. Tout est trié
    /// (forme canonique). Absente : le personnage n'est pas inscrit.
    /// </summary>
    public sealed class SavedGuildCard
    {
        /// <summary>Points de guilde d'une carte, au plus (un gain au-delà s'arrête au plafond).</summary>
        public const long MaxPoints = 10_000_000;

        /// <summary>Contrats réussis comptés, au plus.</summary>
        public const long MaxContracts = 10_000_000;

        /// <summary>Titres, contrats pris dans la journée et créatures rares comptées d'une carte, au plus.</summary>
        public const int MaxTitles = 64, MaxTaken = 128, MaxHunted = 256;

        /// <summary>Contrats en cours, au plus (borne du fichier de la guilde : <c>maxActive</c>).</summary>
        public const int MaxActive = 8;

        public string Rank { get; }
        public long Points { get; }
        public long Contracts { get; }
        public IReadOnlyList<string> Titles { get; }

        /// <summary>Titre affiché (un des <see cref="Titles"/>), ou null.</summary>
        public string? Title { get; }

        /// <summary>Contrats en cours : avancement de leur objectif (créatures vaincues, objets promis), par identifiant (ordre ordinal).</summary>
        public IReadOnlyDictionary<string, long> Active { get; }

        /// <summary>Jour du monde du tableau dont viennent <see cref="Taken"/>.</summary>
        public long Day { get; }

        /// <summary>Contrats pris ce jour-là (en cours, réussis ou abandonnés) : ils ne se reprennent pas avant le lendemain.</summary>
        public IReadOnlyList<string> Taken { get; }

        /// <summary>Créatures rares (populations) dont la première victoire a déjà donné ses points.</summary>
        public IReadOnlyList<string> Hunted { get; }

        public SavedGuildCard(string rank, long points = 0, long contracts = 0, IEnumerable<string>? titles = null, string? title = null,
            IReadOnlyDictionary<string, long>? active = null, long day = 0, IEnumerable<string>? taken = null, IEnumerable<string>? hunted = null)
        {
            Rank = rank ?? throw new ArgumentNullException(nameof(rank));
            if (points < 0 || points > MaxPoints) throw new ArgumentOutOfRangeException(nameof(points), points, $"Points hors de [0, {MaxPoints}].");
            if (contracts < 0 || contracts > MaxContracts) throw new ArgumentOutOfRangeException(nameof(contracts), contracts, $"Contrats hors de [0, {MaxContracts}].");
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day), day, "Jour négatif.");
            Points = points;
            Contracts = contracts;
            Titles = Frozen.List(Sorted(titles), nameof(titles));
            if (title != null && !Titles.Contains(title)) throw new ArgumentException($"Titre affiché « {title} » absent des titres gagnés.", nameof(title));
            Title = title;
            Active = Frozen.Dictionary((active ?? new Dictionary<string, long>()).OrderBy(a => a.Key, StringComparer.Ordinal)
                .ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal), nameof(active));
            if (Active.Values.Any(p => p < 0 || p > SaveValidator.MaxProgress)) throw new ArgumentOutOfRangeException(nameof(active), "Avancement hors bornes.");
            Day = day;
            Taken = Frozen.List(Sorted(taken), nameof(taken));
            Hunted = Frozen.List(Sorted(hunted), nameof(hunted));
        }

        private static IEnumerable<string> Sorted(IEnumerable<string>? ids) =>
            (ids ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
    }
}
