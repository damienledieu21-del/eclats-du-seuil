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

        /// <summary>Identifiant texte choisi dans une liste fermée.</summary>
        Token,
    }
}
