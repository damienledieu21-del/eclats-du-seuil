using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EdS.Data.Saves
{
    /// <summary>
    /// Sous-lot V1.8c (sujet 36, choix de Damien du 30 octobre 2026) : les trois places de bonus d'un personnage, actives en même temps, une
    /// seule de chaque sorte : un <b>plat</b> de la cuisine, un <b>élixir</b> de l'alchimie, une <b>huile ou pierre d'arme</b> de la forge.
    /// L'ordre est celui des places (et de l'écriture d'une sauvegarde).
    /// </summary>
    public enum BoonKind
    {
        /// <summary>Plat de la cuisine (la cuisine arrive après l'ouverture de l'alpha).</summary>
        Meal = 0,

        /// <summary>Élixir de l'alchimie.</summary>
        Elixir = 1,

        /// <summary>Huile ou pierre pour l'arme, de la forge.</summary>
        Weapon = 2,
    }

    /// <summary>Sous-lot V1.8c : noms écrits des places de bonus et bornes de leur durée.</summary>
    public static class BoonKinds
    {
        /// <summary>Places de bonus (une de chaque sorte).</summary>
        public const int Count = 3;

        /// <summary>Durée d'un bonus en minutes de jeu, au moins et au plus (choix de Damien : « de 30 minutes à 1 heure de jeu »).</summary>
        public const int MinMinutes = 30, MaxMinutes = 60;

        /// <summary>Ticks logiques (50 ms) par minute de jeu.</summary>
        public const int TicksPerMinute = 1_200;

        /// <summary>Temps qui reste à un bonus, au plus : une heure de jeu, en ticks logiques (72 000).</summary>
        public const int MaxTicks = MaxMinutes * TicksPerMinute;

        /// <summary>Noms écrits, dans l'ordre des places (données et sauvegardes).</summary>
        public static IReadOnlyList<string> Ids { get; } = new ReadOnlyCollection<string>(new[] { "meal", "elixir", "weapon" });

        /// <summary>Nom écrit d'une place.</summary>
        public static string Id(BoonKind kind) =>
            Enum.IsDefined(typeof(BoonKind), kind) ? Ids[(int)kind] : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Place de bonus inconnue.");

        /// <summary>Place d'un nom écrit ; faux pour un nom inconnu.</summary>
        public static bool TryParse(string? id, out BoonKind kind)
        {
            for (int i = 0; i < Ids.Count; i++)
                if (string.Equals(Ids[i], id, StringComparison.Ordinal))
                {
                    kind = (BoonKind)i;
                    return true;
                }
            kind = default;
            return false;
        }
    }

    /// <summary>
    /// Sous-lot V1.8c : un bonus actif (place, objet consommé, temps qui reste en ticks logiques de 50 ms). Le temps ne baisse que pendant
    /// que la partie tourne (un tick simulé de la session) : arrêté hors ligne, gardé tel quel dans la sauvegarde ; le bonus est gardé quand
    /// le personnage tombe. Immuable. L'effet vient de l'objet du catalogue, relu quand la partie reprend.
    /// </summary>
    public sealed class SavedBoon : IEquatable<SavedBoon>
    {
        public BoonKind Kind { get; }

        /// <summary>Objet consommé (identifiant « item.… » du catalogue).</summary>
        public string Item { get; }

        /// <summary>Ticks logiques qui restent, de 1 à <see cref="BoonKinds.MaxTicks"/>.</summary>
        public int Ticks { get; }

        /// <exception cref="ArgumentOutOfRangeException">Place inconnue, ou temps hors de [1, <see cref="BoonKinds.MaxTicks"/>].</exception>
        public SavedBoon(BoonKind kind, string item, int ticks)
        {
            if (!Enum.IsDefined(typeof(BoonKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Place de bonus inconnue.");
            if (ticks < 1 || ticks > BoonKinds.MaxTicks)
                throw new ArgumentOutOfRangeException(nameof(ticks), ticks, $"Temps d'un bonus hors de [1, {BoonKinds.MaxTicks}] ticks.");
            Kind = kind;
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Ticks = ticks;
        }

        /// <summary>Même bonus avec un autre temps qui reste.</summary>
        public SavedBoon WithTicks(int ticks) => new SavedBoon(Kind, Item, ticks);

        /// <summary>
        /// Bonus rangés dans l'ordre des places, une seule fois chacune (forme canonique d'un personnage).
        /// </summary>
        /// <exception cref="ArgumentException">Deux bonus de la même place, ou un bonus null.</exception>
        public static IReadOnlyList<SavedBoon> Canonical(IEnumerable<SavedBoon>? boons)
        {
            var list = (boons ?? Array.Empty<SavedBoon>()).ToList();
            if (list.Any(b => b is null)) throw new ArgumentException("Bonus null.", nameof(boons));
            if (list.Select(b => b.Kind).Distinct().Count() != list.Count) throw new ArgumentException("Deux bonus de la même place.", nameof(boons));
            return new ReadOnlyCollection<SavedBoon>(list.OrderBy(b => (int)b.Kind).ToList());
        }

        public bool Equals(SavedBoon? other) => other != null && other.Kind == Kind && other.Item == Item && other.Ticks == Ticks;
        public override bool Equals(object? obj) => Equals(obj as SavedBoon);
        public override int GetHashCode() => unchecked(((int)Kind * 397 + StringComparer.Ordinal.GetHashCode(Item)) * 31 + Ticks);
        public override string ToString() => $"{BoonKinds.Id(Kind)} {Item} {Ticks}";
    }
}
