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
    /// modificateurs : <c>talents[].modifiers</c> et <c>skills[].modifiers</c> (passives seulement). Règles inchangées (5) ;
    /// 6 (règles) = lot 8 (décision du 18/10/2026 puis sous-lot 8.2) : plafonds <c>combat.guard.maxDamageReduction</c> et
    /// <c>combat.damageTaken.maxReduction</c>, auparavant constantes du moteur ; 7 (règles) et 6 (classes) = sous-lot 8.2 :
    /// catégories <c>buff</c> et <c>shield</c> et <c>statuses[].modifiers</c> dans les règles, clé de modificateur
    /// <c>shield.potencyDealt</c> dans les classes. Une version incrémentée même pour un ajout : un programme antérieur refuse
    /// ainsi d'emblée un fichier qui en use, au lieu de le lire comme un format qu'il croit connaître (revue Codex de la PR #6) ;
    /// 8 (règles) et 7 (classes) = sous-lot 8.3 : catégories <c>taunt</c> et <c>protection</c>, <c>statuses[].redirectRatio</c>, rôle
    /// <c>ally</c> des déclencheurs et <c>applies[].target</c> dans les règles ; ciblages <c>self</c>, <c>ally</c>, <c>allAllies</c>,
    /// compétences sans dégâts directs, <c>applies[].target</c>, rôle <c>ally</c> et clés <c>protection.*</c> dans les classes ;
    /// 9 (règles) et 8 (classes) = sous-lot 8.4 : <c>combat.triggers.areaStrikeMinCooldownMs</c> (L8-D2) et clé <c>guard.noUpkeep</c>
    /// des renforcements dans les règles ; <c>combat.rampartChargeSpendAll</c>, <c>combat.coefficientPerRampartCharge</c> (X8) et clé
    /// <c>guard.noUpkeep</c> dans les classes ; 10 (règles) = sous-lot 11.4a : <c>combat.movement.speed</c>,
    /// <c>combat.movement.guardSpeedFactor</c>, <c>combat.contact.playerCreature</c>, <c>combat.contact.crossingSpeedFactor</c> et
    /// <c>combat.contact.largeCreatureRadius</c> (déplacement et contact d'un combat positionné). Classes inchangées (8) ;
    /// 11 (règles) et 9 (classes) = sous-lot 11.4b, portées d'un combat positionné : <c>statuses[].range</c> (protection seulement)
    /// dans les règles ; <c>combat.range</c>, <c>combat.applies[].range</c> et <c>triggers[].effects[].range</c> dans les classes.</para>
    /// </summary>
    public static class DataFormat
    {
        /// <summary>Version de schéma courante des fichiers de règles (<c>rules.json</c>).</summary>
        public const int RulesSchemaVersion = 11;

        /// <summary>Version de schéma courante des fichiers de classes.</summary>
        public const int ClassSchemaVersion = 9;

        /// <summary>Version de schéma courante des fichiers de builds de référence (<c>builds/*.json</c>, sous-lot 8.5).</summary>
        public const int BuildsSchemaVersion = 1;

        /// <summary>
        /// Version de contenu des règles livrées : provisoires, non équilibrées. 0.7.0 : altérations du Bastion (sous-lot 8.4) ;
        /// 0.8.0 : altérations des talents du Gardien (sous-lot 8.5) ; 0.9.0 : déplacement et contact (sous-lot 11.4a) ; 0.10.0 :
        /// portée du lien protecteur (sous-lot 11.4b).
        /// </summary>
        public const string ProvisionalRulesContentVersion = "0.10.0-provisoire";
    }
}
