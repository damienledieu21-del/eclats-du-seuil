namespace EdS.Data
{
    /// <summary>
    /// Versions du format des fichiers de configuration (voir PLAN_SIMULATEUR_V0_1.md, §2.3).
    /// Le chargeur JSON et la validation arrivent au lot 3.
    /// </summary>
    public static class DataFormat
    {
        /// <summary>Version de schéma courante ; tout changement incompatible l'incrémente et ajoute un migrateur.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Version de contenu des données d'équilibrage livrées : provisoires, non équilibrées.</summary>
        public const string ProvisionalContentVersion = "0.1.0-provisoire";
    }
}
