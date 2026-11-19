using System.Collections.Generic;

namespace EdS.Runtime
{
    public static partial class LotCModels
    {
        /// <summary>Pâtisseries de Firmin (19 novembre 2026) : catégorie de leurs 16 modèles Tripo (<see cref="FirminPastries"/>).</summary>
        public const string FirminCategory = "09_Firmin";

        /// <summary>
        /// Pâtisseries de Firmin : modèle d'une pièce de <paramref name="itemId"/> de qualité <paramref name="quality"/> (« normal »,
        /// « superior », « masterwork »), cible <c>quality:objet/qualité</c> (pièce de la main du joueur, trois niveaux de réussite), sinon
        /// celui de l'objet (<see cref="ForItem"/>) ; null sans modèle.
        /// </summary>
        public static string? ForQuality(string? itemId, string? quality) =>
            itemId == null ? null : (quality != null ? Target("quality:" + itemId + "/" + quality) : null) ?? ForItem(itemId);

        /// <summary>
        /// Pâtisseries de Firmin : les 16 modèles du dossier (dix produits, dont trois pièces de concours en trois niveaux). Le niveau
        /// maîtrisé (ou le modèle unique) porte l'objet (<c>item:</c>, étal de Firmin) ; chaque niveau porte la qualité de fabrication qui le
        /// donne à une pièce de la main du joueur (<c>quality:</c>, <see cref="FirminPastries.Level"/>).
        /// </summary>
        private static IEnumerable<LotCModel> Firmin()
        {
            string[] qualities = { "normal", "superior", "masterwork" };
            foreach (FirminPastry p in FirminPastries.All)
            {
                if (!p.Levels)
                {
                    yield return Linked(p.Code, FirminCategory, p.Name, "item:" + p.Item);
                    continue;
                }
                for (int level = 1; level <= 3; level++)
                {
                    var targets = new List<string>();
                    if (level == 3) targets.Add("item:" + p.Item);
                    targets.Add("quality:" + p.Item + "/" + qualities[level - 1]);
                    yield return Linked(p.ModelCode(level), FirminCategory, p.Name + " N" + level, targets.ToArray());
                }
            }
        }
    }
}
