namespace EdS.Game
{
    /// <summary>
    /// Sous-lot 12.8 : écran ouvert de l'aventure : inventaire et équipement (I), journal de quête et découvertes (J), atelier et métiers
    /// (K), carte (L, sous-lot 12.9d), compétences (B, sous-lot V1.5). Essai réaliste 6, étape 2 : ces écrans et le dialogue sont dessinés
    /// en uGUI par <see cref="AdventureBook"/> ; cette classe ne garde que l'écran ouvert.
    /// </summary>
    public sealed class AdventureScreens
    {
        // Sous-lot V1.1 : Merchant, la boutique d'un marchand (F en lui parlant) ; sous-lot V1.7 : Guild, le tableau de la maîtresse de guilde.
        public enum Panel { None, Inventory, Journal, Crafting, Map, Skills, Merchant, Guild }

        public Panel Open { get; private set; }

        public void Toggle(Panel panel) => Open = Open == panel ? Panel.None : panel;

        /// <summary>Essai réaliste 6 : ouvre <paramref name="panel"/> (onglet du panneau).</summary>
        public void Show(Panel panel) => Open = panel;

        public void Close() => Open = Panel.None;
    }
}
