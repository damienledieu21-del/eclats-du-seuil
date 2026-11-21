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

        /// <summary>Sur la poitrine (médaillon, rosette).</summary>
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
    /// V2.56.3 (décision de Damien du 19 novembre 2026 : « un accessoire par fête des habitants, plus tard, avec les costumes en 3D ») :
    /// un jour de fête d'Orsédia, les habitants qui portent la tenue de la fête (<see cref="HumanWardrobe.FeastTints"/> : ni gardes, ni
    /// bandits, ni le héros) portent aussi son accessoire. Faute de modèle d'accessoire propre (les costumes C01 ne sont pas modélisés), le
    /// choix par défaut (question posée dans la PR) est la <b>récompense de la fête</b> (R01, déjà modélisée) : le masque sur le visage, le
    /// médaillon et la rosette sur la poitrine, les autres en main. Affichage seulement. Sans Unity.
    /// </summary>
    public static class FeastAccessories
    {
        /// <summary>Les douze accessoires, dans l'ordre des fêtes.</summary>
        public static IReadOnlyList<FeastAccessory> All { get; } = new[]
        {
            new FeastAccessory(1, "F01_R01", "Lanterne du premier vœu", FeastAccessoryPlace.Hand),
            new FeastAccessory(2, "F02_R01", "Médaillon des compagnons", FeastAccessoryPlace.Chest),
            new FeastAccessory(3, "F03_R01", "Masque de l'Autre Rôle", FeastAccessoryPlace.Face),
            new FeastAccessory(4, "F04_R01", "Œuf souvenir des Semailles", FeastAccessoryPlace.Hand),
            new FeastAccessory(5, "F05_R01", "Rosette du maître artisan", FeastAccessoryPlace.Chest),
            new FeastAccessory(6, "F06_R01", "Clochette de la Ronde", FeastAccessoryPlace.Hand),
            new FeastAccessory(7, "F07_R01", "Coupe des Vallons", FeastAccessoryPlace.Hand),
            new FeastAccessory(8, "F08_R01", "Cloche d'un éclat de lune", FeastAccessoryPlace.Hand),
            new FeastAccessory(9, "F09_R01", "Coupe de la dernière gerbe", FeastAccessoryPlace.Hand),
            new FeastAccessory(10, "F10_R01", "Clé des Échos", FeastAccessoryPlace.Hand),
            new FeastAccessory(11, "F11_R01", "Livre miniature des veillées", FeastAccessoryPlace.Hand),
            new FeastAccessory(12, "F12_R01", "Boule à neige des Vallons", FeastAccessoryPlace.Hand),
        };

        /// <summary>L'accessoire de la fête n° <paramref name="feast"/> (1 à 12), ou null.</summary>
        public static FeastAccessory? Of(int feast) => feast >= 1 && feast <= All.Count ? All[feast - 1] : null;

        /// <summary>
        /// L'accessoire porté par un habitant de tenue <paramref name="outfit"/> pendant la fête n° <paramref name="feast"/> (0 : pas de fête
        /// d'Orsédia en cours), ou null : mêmes habitants que ceux qui prennent les teintes de la fête (ni gardes, ni bandits, ni le héros).
        /// </summary>
        public static FeastAccessory? For(int feast, HumanOutfit outfit) =>
            outfit == HumanOutfit.Guard || outfit == HumanOutfit.Bandit || outfit == HumanOutfit.Hero ? null : Of(feast);
    }
}
