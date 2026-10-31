using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EdS.Data.Crafting;
using EdS.Data.Items;
using EdS.Data.Saves;
using EdS.World;

namespace EdS.Runtime
{
    /// <summary>
    /// Sous-lot V1.4 : la forge de Maëlle, la qualité et l'usure, vues par le client : le petit jeu de forge (ses notes vont au cœur), la
    /// qualité et l'usure dans la fiche d'un objet, la réparation à la forge et chez l'armurier, avec son prix. Le cœur reste seul juge.
    /// </summary>
    public sealed partial class AdventureSession
    {
        /// <summary>Frappes du petit jeu de forge (trois).</summary>
        public int StrikeCount => _crafting.Forge.Strikes;

        /// <summary>Un petit jeu de forge pour une recette à qualité (le client le joue, puis envoie ses notes à <see cref="Forge"/>).</summary>
        public ForgeGame NewForgeGame() => new ForgeGame(_crafting.Forge.Strikes);

        /// <summary>
        /// Chances de la recette pour une adresse <paramref name="skill"/> (0 à 100, la moyenne des frappes) : rater, chef-d'œuvre, qualité
        /// supérieure (pour cent).
        /// </summary>
        /// <exception cref="ArgumentException">Recette inconnue.</exception>
        public (int Fail, int Masterwork, int Superior) ForgeChances(string recipe, int skill) => World.Workshop!.Chances(_crafting.Recipe(recipe), skill);

        /// <summary>
        /// Forge une recette à qualité avec les notes du petit jeu (<paramref name="notes"/>, une par frappe, de 0 à 100) ; un message dit le
        /// résultat (qualité, échec et matériaux rendus) ou le refus.
        /// </summary>
        public CraftRefusal Forge(string recipe, IReadOnlyList<int> notes)
        {
            if (notes is null) throw new ArgumentNullException(nameof(notes));
            CraftRefusal refusal = World.Craft(recipe, new EdS.World.ForgeStrikes(notes), out CraftEvent? done);
            if (done != null) Crafted(done);
            else Refused(refusal);
            return refusal;
        }

        /// <summary>Nom affiché d'une qualité de fabrication (« Supérieure », « Chef-d'œuvre ») ; vide pour la qualité normale.</summary>
        public string QualityName(ItemQuality quality) =>
            quality == ItemQuality.Superior ? Text("text.ui.quality.superior") : quality == ItemQuality.Masterwork ? Text("text.ui.quality.masterwork") : "";

        /// <summary>
        /// Lignes de la fiche d'une instance : sa qualité (et la signature d'un chef-d'œuvre), puis son usure (solidité qui reste sur la
        /// solidité pleine, et « usée : plus de bonus » à zéro) ; rien pour ce qui ne s'use pas ni ne prend de qualité.
        /// </summary>
        public IReadOnlyList<string> InstanceLines(ItemStack stack)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            var lines = new List<string>();
            if (!_items.TryGetItem(stack.Item, out ItemDefinition item)) return lines;
            if (stack.Quality == ItemQuality.Masterwork)
                lines.Add(string.Format(CultureInfo.InvariantCulture, Text("text.ui.quality.signed"), QualityName(stack.Quality), DisplayNames.Of(stack.SignedBy ?? "")));
            else if (stack.Quality == ItemQuality.Superior) lines.Add(QualityName(stack.Quality));
            if (_items.Wears(item))
            {
                int left = _items.WearLeft(stack);
                lines.Add(string.Format(CultureInfo.InvariantCulture, Text(left == 0 ? "text.ui.wear.broken" : "text.ui.wear.left"), left, _items.WearMax));
            }
            return lines;
        }

        /// <summary>Prix de la réparation de tout l'équipement entamé, en texte (« 1 a 20 c ») ; 0 s'il n'y a rien à réparer.</summary>
        public long RepairPrice => World.Workshop is null ? 0 : World.RepairPrice();

        /// <summary>Pièces entamées, portées et dans le sac.</summary>
        public int DamagedPieces => World.Inventory.Damaged().Count;

        /// <summary>On peut réparer à la forge (au lieu de réparation, hors combat), quelle que soit la boutique ouverte.</summary>
        public bool CanRepairAtForge => World.Workshop != null && World.CanRepairHere(null) == RepairRefusal.None;

        /// <summary>La boutique ouverte est celle d'un marchand qui répare (l'armurier), et il répare maintenant (à portée, ouverte).</summary>
        public bool CanRepairAtShop => World.Workshop != null && Shop is string shop && World.CanRepairHere(shop) == RepairRefusal.None;

        /// <summary>
        /// Répare tout l'équipement entamé : chez le marchand de la boutique ouverte s'il répare maintenant
        /// (<see cref="WorldSession.CanRepairHere"/> sans refus), sinon à la forge (relecture indépendante de la PR #80 : une boutique ouverte
        /// mais hors de portée n'empêche pas de réparer à la forge) ; un message dit le prix payé ou le refus. Les bonus reviennent à la
        /// prochaine halte.
        /// </summary>
        public RepairRefusal Repair()
        {
            string? merchant = CanRepairAtShop ? Shop : null;
            RepairRefusal refusal = World.Repair(merchant, out long paid);
            if (refusal == RepairRefusal.None)
            {
                _dirty = true;
                Say("text.ui.repaired", MoneyText(paid));
            }
            else Say("text.ui.repair." + refusal.ToString().ToLowerInvariant());
            return refusal;
        }
    }
}
