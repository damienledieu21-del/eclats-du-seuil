namespace EdS.Data.Rules
{
    /// <summary>Forme attendue d'un paramètre dans un fichier de règles.</summary>
    public enum RuleValueKind
    {
        /// <summary>Nombre décimal à 4 décimales au plus (converti en <c>Fixed</c>).</summary>
        Decimal,

        /// <summary>Entier 32 bits.</summary>
        Int32,

        /// <summary>Entier 64 bits.</summary>
        Int64,

        /// <summary>
        /// Identifiant texte choisi dans une liste fermée ; sans liste (<see cref="RuleField.AllowedTokens"/> vide), identifiant
        /// libre dont la référence est vérifiée par le domaine (ex. <c>combat.guard.breakStatus</c>, une altération).
        /// </summary>
        Token,

        /// <summary>Liste d'entiers 32 bits (ex. niveaux de déblocage).</summary>
        Int32List,
    }
}
