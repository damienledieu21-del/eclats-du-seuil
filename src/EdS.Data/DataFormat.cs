namespace EdS.Data
{
    /// <summary>
    /// Versions du format des fichiers de règles et de classes (voir PLAN_SIMULATEUR_V0_1.md, §2.3, et docs/DONNEES.md).
    /// <para>Politique pré-alpha (correctif 6.1) : tout changement incompatible incrémente la version de schéma ; une
    /// version antérieure est refusée explicitement, <b>sans migration garantie</b> ; le fichier se met à jour à la main.
    /// Les migrateurs automatiques n'arriveront qu'avec un format stabilisé.</para>
    /// <para>Historique : 1 = lots 3 à 5 ; 2 = lot 6 (blocs garde, parade, Charges, Résonance, Fragilité dans
    /// <c>rules.json</c>, données de combat imparable et Charges dans les classes), version introduite au correctif 6.1
    /// car le lot 6 avait changé ces formats sans incrémenter le numéro.</para>
    /// </summary>
    public static class DataFormat
    {
        /// <summary>Version de schéma courante des fichiers de règles et de classes.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>Version de contenu des données d'équilibrage livrées : provisoires, non équilibrées.</summary>
        public const string ProvisionalContentVersion = "0.2.0-provisoire";
    }
}
