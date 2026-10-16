namespace EdS.Data.Validation
{
    /// <summary>Catégorie d'une erreur de validation, stable pour les outils et les tests.</summary>
    public enum ValidationCode
    {
        /// <summary>Le fichier n'a pas pu être lu.</summary>
        FileUnreadable,

        /// <summary>Le texte n'est pas du JSON valide (syntaxe, virgule finale, commentaire…).</summary>
        InvalidJson,

        /// <summary>Type JSON inattendu (texte au lieu d'un nombre, valeur au lieu d'un objet…).</summary>
        WrongType,

        /// <summary>Champ obligatoire absent.</summary>
        MissingField,

        /// <summary>Champ non prévu par le schéma (souvent une faute de frappe).</summary>
        UnknownField,

        /// <summary>Le même champ apparaît deux fois dans un objet.</summary>
        DuplicateField,

        /// <summary>Nombre mal écrit : exposant, plus de 4 décimales, décimale là où un entier est attendu.</summary>
        InvalidNumber,

        /// <summary>Nombre hors de la plage représentable.</summary>
        OutOfRange,

        /// <summary>Identifiant qui ne désigne rien de connu (type de dégâts, attribut, mode d'arrondi…).</summary>
        UnknownReference,

        /// <summary>Version de schéma non prise en charge par ce programme.</summary>
        UnsupportedSchemaVersion,

        /// <summary>Version de contenu qui n'est pas au format SemVer.</summary>
        InvalidContentVersion,

        /// <summary>Le fichier ne décrit pas le type de données attendu.</summary>
        WrongKind,

        /// <summary>Identifiant déjà utilisé par une autre définition.</summary>
        DuplicateId,

        /// <summary>Valeur bien formée mais refusée par les règles de gameplay (incohérence, borne).</summary>
        InvalidValue,

        /// <summary>
        /// Limite de volume dépassée (lot 7.1) : taille de fichier, nombre de combattants ou de commandes, commandes par
        /// acteur et par tick, capacité de la file, longueur d'un tableau, nombre d'erreurs rapportées.
        /// </summary>
        LimitExceeded,

        /// <summary>Fichier plus gros que la limite de taille des lecteurs (lot 7.1) ; refusé avant toute lecture du contenu.</summary>
        FileTooLarge,

        /// <summary>Avertissement (lot 7.3) : cycle statique entre déclencheurs, arrêté en combat par la règle de cycle et la profondeur.</summary>
        TriggerCycle,
    }
}
