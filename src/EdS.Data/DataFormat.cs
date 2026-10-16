namespace EdS.Data
{
    /// <summary>
    /// Versions du format des fichiers de règles et de classes (voir PLAN_SIMULATEUR_V0_1.md, §2.3, et docs/DONNEES.md).
    /// <para>Politique pré-alpha (correctif 6.1) : tout changement incompatible incrémente la version de schéma ; une
    /// version antérieure est refusée explicitement, <b>sans migration garantie</b> ; le fichier se met à jour à la main.
    /// Les migrateurs automatiques n'arriveront qu'avec un format stabilisé.</para>
    /// <para>Historique : 1 = lots 3 à 5 ; 2 = lot 6 (blocs garde, parade, Charges, Résonance, Fragilité dans
    /// <c>rules.json</c>, données de combat imparable et Charges dans les classes), version introduite au correctif 6.1
    /// car le lot 6 avait changé ces formats sans incrémenter le numéro ; 3 (règles seulement) = lot 7.1, cinq limites
    /// d'admission et de budget dans <c>combat.limits</c>. Les classes restent en version 2 ; 4 (règles) et 3 (classes) = lot 7.2,
    /// bloc <c>statuses</c> (définitions des altérations), <c>stats.statusResistanceConstant</c>, <c>combat.guard.breakStatus</c>,
    /// suppression des champs de Fragilité et de durée de rupture du bloc <c>combat</c> ; <c>combat.applies</c> dans les classes ;
    /// 5 (règles) et 4 (classes) = lot 7.3, déclencheurs : <c>combat.limits.maxTriggerDepth</c> et <c>statuses[].triggers</c> dans
    /// les règles, <c>talents[].triggers</c> et <c>skills[].triggers</c> dans les classes ; 5 (classes) = sous-lot 8.1,
    /// modificateurs : <c>talents[].modifiers</c> et <c>skills[].modifiers</c> (passives seulement). Règles inchangées (5).</para>
    /// </summary>
    public static class DataFormat
    {
        /// <summary>Version de schéma courante des fichiers de règles (<c>rules.json</c>).</summary>
        public const int RulesSchemaVersion = 5;

        /// <summary>Version de schéma courante des fichiers de classes.</summary>
        public const int ClassSchemaVersion = 5;

        /// <summary>Version de contenu des règles livrées : provisoires, non équilibrées.</summary>
        public const string ProvisionalRulesContentVersion = "0.5.0-provisoire";
    }
}
