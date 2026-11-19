using System.Collections.Generic;
using System.Linq;
using EdS.Data.Merchants;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        /// <summary>Pâtisseries de Firmin : modèle de la dernière pièce présentée au concours et jour du monde de la présentation (pas sauvegardé).</summary>
        private (string Code, long Day)? _presented;

        /// <summary>
        /// Pâtisseries de Firmin : modèle (lot C) de la pièce que le joueur a présentée au concours aujourd'hui, au niveau de réussite de sa
        /// qualité (<see cref="FirminPastries.Level"/>), ou null (rien de présenté ce jour du monde). Affichage seulement, pas sauvegardé.
        /// </summary>
        public string? PresentedPastry => _presented is (string code, long day) && day == World.Zone.Time.Day ? code : null;

        /// <summary>
        /// Pâtisseries de Firmin : modèles posés sur l'étal du fournil, d'après ce que la boulangerie a encore en vente maintenant (offres du
        /// jour, jours et saisons compris ; une offre épuisée ou pas encore cuite n'y est pas) ; vide sans marché ni boulangerie.
        /// </summary>
        public IReadOnlyList<string> FirminShowcase()
        {
            if (World.Market is null || !World.Market.Book.TryGetMerchant(FirminPastries.Bakery, out _)) return new string[0];
            IEnumerable<MerchantOffer> offers = World.Stock(FirminPastries.Bakery).Where(o => o.Left is null || o.Left > 0);
            return FirminPastries.Showcase(offers.Select(o => o.Item));
        }
    }
}
