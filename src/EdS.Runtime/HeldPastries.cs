using System;
using System.Collections.Generic;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// V2.56.1 : la pâtisserie tenue en main par le héros (objet du catalogue et modèle Tripo de Firmin, <see cref="FirminPastries"/>), et si
    /// c'est le moment où il la mange. Affichage seulement : rien ne revient vers le moteur, la sauvegarde ou les données.
    /// </summary>
    public sealed class HeldPastry : IEquatable<HeldPastry>
    {
        public HeldPastry(string item, string model, bool eating)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Eating = eating;
        }

        /// <summary>Objet du catalogue (« item.couronne_des_moissons »).</summary>
        public string Item { get; }

        /// <summary>Code du modèle (« FIR01_N3 », « FIR08 »).</summary>
        public string Model { get; }

        /// <summary>Le héros est en train de la manger (sinon il la porte).</summary>
        public bool Eating { get; }

        public bool Equals(HeldPastry? other) => other != null && other.Item == Item && other.Model == Model && other.Eating == Eating;
        public override bool Equals(object? obj) => Equals(obj as HeldPastry);
        public override int GetHashCode() => unchecked((StringComparer.Ordinal.GetHashCode(Model) * 31) ^ (Eating ? 1 : 0));
    }

    /// <summary>
    /// V2.56.1 (décision de Damien du 19 novembre 2026 : « la pâtisserie tenue en main, avec les objets portés du héros ») : quelle
    /// pâtisserie le héros tient dans la main gauche. Règle retenue par défaut (proposition à valider, question de la PR) :
    /// <list type="bullet">
    /// <item>en la mangeant (objet de soin bu en combat ou dehors) : celle qu'il mange, pendant <see cref="EatSeconds"/> secondes, même en
    /// combat ou à cheval ;</item>
    /// <item>sinon, hors combat, à pied et hors scène : la plus réussie de <b>ses</b> pièces (marquées de sa main, celles qu'il garde pour le
    /// juge du concours), à égalité la première dans l'ordre de l'étal de Firmin ;</item>
    /// <item>rien d'autre : ni les copies achetées (soins emportés), ni la pâte feuilletée ni le levain (ingrédients).</item>
    /// </list>
    /// Sans Unity.
    /// </summary>
    public static class HeldPastries
    {
        /// <summary>Durée où la pâtisserie mangée reste en main (secondes d'affichage).</summary>
        public const float EatSeconds = 2.5f;

        /// <summary>Ingrédients du dossier de Firmin : jamais tenus en main.</summary>
        private static readonly HashSet<string> Ingredients = new HashSet<string>(StringComparer.Ordinal) { "item.pate_feuilletee", "item.levain" };

        /// <summary>La pâtisserie <paramref name="itemId"/> se tient-elle en main (modèle de Firmin, pas un ingrédient) ?</summary>
        public static bool IsHeld(string? itemId) => FirminPastries.Of(itemId) != null && !Ingredients.Contains(itemId!);

        /// <summary>
        /// La pâtisserie tenue : <paramref name="eating"/> (celle que le héros mange, une copie achetée : niveau maîtrisé) si c'en est une,
        /// sinon sa meilleure pièce marquée de <paramref name="me"/> hors combat, à pied et hors scène ; null sinon.
        /// </summary>
        public static HeldPastry? For(Inventory inventory, string? me, string? eating, bool fighting, bool mounted, bool inScene)
        {
            if (inventory is null) throw new ArgumentNullException(nameof(inventory));
            if (inScene) return null;
            if (IsHeld(eating) && FirminPastries.Model(eating, ItemQuality.Normal, false) is string eaten) return new HeldPastry(eating!, eaten, true);
            if (fighting || mounted || string.IsNullOrEmpty(me)) return null;
            ItemStack? best = null;
            int bestLevel = 0, bestRank = int.MaxValue;
            foreach (ItemStack s in inventory.Stacks)
            {
                if (s.SignedBy != me || !IsHeld(s.Item)) continue;
                int level = FirminPastries.Level(s.Quality, true), rank = Rank(s.Item);
                if (level > bestLevel || (level == bestLevel && rank < bestRank))
                {
                    best = s;
                    bestLevel = level;
                    bestRank = rank;
                }
            }
            return best != null && FirminPastries.Model(best.Item, best.Quality, true) is string model ? new HeldPastry(best.Item, model, false) : null;
        }

        private static int Rank(string item)
        {
            for (int i = 0; i < FirminPastries.All.Count; i++)
                if (FirminPastries.All[i].Item == item) return i;
            return int.MaxValue;
        }
    }
}
