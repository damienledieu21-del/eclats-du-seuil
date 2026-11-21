using System;
using System.Collections.Generic;

namespace EdS.Runtime
{
    /// <summary>V2.56.3 : où se porte l'accessoire de fête d'un habitant.</summary>
    public enum FeastAccessoryPlace
    {
        /// <summary>Tenu dans la main droite.</summary>
        Hand = 0,

        /// <summary>Sur le visage (masque).</summary>
        Face = 1,

        /// <summary>Sur la poitrine (réservé : aucun accessoire ne s'y porte depuis la décision du 21 novembre 2026).</summary>
        Chest = 2,
    }

    /// <summary>V2.56.3 : l'accessoire d'une fête d'Orsédia : modèle Tripo des fêtes (<see cref="FeastLooks"/>) et place sur le corps.</summary>
    public sealed class FeastAccessory
    {
        internal FeastAccessory(int feast, string code, string name, FeastAccessoryPlace place)
        {
            Feast = feast;
            Code = code;
            Name = name;
            Place = place;
        }

        /// <summary>Numéro de la fête (1 à 12, janvier à décembre).</summary>
        public int Feast { get; }

        /// <summary>Code du modèle (« F03_R01 », dossier <see cref="FeastLooks.Folder"/>).</summary>
        public string Code { get; }

        /// <summary>Nom du dossier des fêtes.</summary>
        public string Name { get; }

        public FeastAccessoryPlace Place { get; }
    }

    /// <summary>
    /// V2.56.3 (décisions de Damien du 19 novembre 2026 : « un accessoire par fête des habitants », et du 21 novembre 2026, option B) :
    /// un jour de fête d'Orsédia, les habitants qui portent la tenue de la fête (<see cref="HumanWardrobe.FeastTints"/> : ni gardes, ni
    /// bandits, ni le héros) tiennent en main <b>le plat de la fête</b> (P01) ; la récompense n'est pas portée, pour qu'elle reste rare.
    /// Exception : au Carnaval, le <b>masque de l'Autre Rôle</b> (R01), vrai accessoire de costume, se porte sur le visage. Affichage
    /// seulement. Sans Unity.
    /// </summary>
    public static class FeastAccessories
    {
        /// <summary>Les douze accessoires, dans l'ordre des fêtes.</summary>
        public static IReadOnlyList<FeastAccessory> All { get; } = new[]
        {
            new FeastAccessory(1, "F01_P01", "Tourte des vœux", FeastAccessoryPlace.Hand),
            new FeastAccessory(2, "F02_P01", "Pain des compagnons", FeastAccessoryPlace.Hand),
            new FeastAccessory(3, "F03_R01", "Masque de l'Autre Rôle", FeastAccessoryPlace.Face),   // exception : vrai accessoire de costume
            new FeastAccessory(4, "F04_P01", "Brioche du griffon", FeastAccessoryPlace.Hand),
            new FeastAccessory(5, "F05_P01", "Tourte des sept ateliers", FeastAccessoryPlace.Hand),
            new FeastAccessory(6, "F06_P01", "Galette des braises", FeastAccessoryPlace.Hand),
            new FeastAccessory(7, "F07_P01", "Pâté du Grand Tournoi", FeastAccessoryPlace.Hand),
            new FeastAccessory(8, "F08_P01", "Tartelettes de lune", FeastAccessoryPlace.Hand),
            new FeastAccessory(9, "F09_P01", "Galette des vendanges", FeastAccessoryPlace.Hand),
            new FeastAccessory(10, "F10_P01", "Tourte des Échos", FeastAccessoryPlace.Hand),
            new FeastAccessory(11, "F11_P01", "Poires de la veillée", FeastAccessoryPlace.Hand),
            new FeastAccessory(12, "F12_P01", "Bûche du Givre", FeastAccessoryPlace.Hand),
        };

        /// <summary>L'accessoire de la fête n° <paramref name="feast"/> (1 à 12), ou null.</summary>
        public static FeastAccessory? Of(int feast) => feast >= 1 && feast <= All.Count ? All[feast - 1] : null;

        /// <summary>
        /// L'accessoire porté par un habitant de tenue <paramref name="outfit"/> pendant la fête n° <paramref name="feast"/> (0 : pas de fête
        /// d'Orsédia en cours), ou null : mêmes habitants que ceux qui prennent les teintes de la fête (ni gardes, ni bandits, ni le héros).
        /// </summary>
        public static FeastAccessory? For(int feast, HumanOutfit outfit) =>
            outfit == HumanOutfit.Guard || outfit == HumanOutfit.Bandit || outfit == HumanOutfit.Hero ? null : Of(feast);

        /// <summary>
        /// L'accessoire à poser sur un habitant au moment de l'habiller (revue Codex de la PR #237) : rien pour le héros
        /// (<paramref name="hero"/>), sans la vraie tenue de fête (<paramref name="festiveClothes"/>) ou hors fête d'Orsédia
        /// (<paramref name="festival"/>) ; sinon <see cref="For"/>.
        /// </summary>
        public static FeastAccessory? Worn(int feast, HumanOutfit outfit, bool hero, bool festiveClothes, bool festival) =>
            hero || !festiveClothes || !festival ? null : For(feast, outfit);

        /// <summary>
        /// Vrai si l'os nommé <paramref name="name"/> porte l'accessoire de place <paramref name="place"/> (noms des Universal Base
        /// Characters, de Quaternius et de Mixamo) : la tête pour le masque, la main droite pour le plat.
        /// </summary>
        public static bool IsBone(string? name, FeastAccessoryPlace place)
        {
            string n = name ?? string.Empty, low = n.ToLowerInvariant();
            switch (place)
            {
                case FeastAccessoryPlace.Face:
                    return low == "head" || low.EndsWith(":head", StringComparison.Ordinal);
                case FeastAccessoryPlace.Chest:
                    return low == "spine_03" || low == "chest" || low == "spine.003" || n.EndsWith("Spine2", StringComparison.Ordinal);
                default:
                    return n.EndsWith("RightHand", StringComparison.Ordinal) || low == "hand_r" || low == "hand.r"
                        || low.EndsWith(":hand_r", StringComparison.Ordinal);
            }
        }
    }
}
