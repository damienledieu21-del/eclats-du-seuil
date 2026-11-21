using System;
using System.Collections.Generic;
using EdS.World;

namespace EdS.Runtime
{
    public sealed partial class AdventureSession
    {
        private static readonly CaravanView[] NoCaravans = new CaravanView[0];

        /// <summary>
        /// Sous-lot V2.41.2 : les caravanes sur les routes de la zone (affichage seulement), aucune dans un intérieur ou une scène. Calculées à
        /// chaque appel (fonction pure du temps du monde et du registre).
        /// </summary>
        public IReadOnlyList<CaravanView> Caravans => World.InStoryZone && !InScene ? World.Caravans : NoCaravans;
    }
}
