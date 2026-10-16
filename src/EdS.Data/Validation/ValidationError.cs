namespace EdS.Data.Validation
{
    /// <summary>Une erreur rattachée à un emplacement précis du fichier (chemin JSON, ligne si connue).</summary>
    public sealed class ValidationError
    {
        /// <summary>Chemin JSON, par exemple <c>$.data.stats.critical.maxChanceBasisPoints</c>.</summary>
        public string Path { get; }

        public ValidationCode Code { get; }
        public string Message { get; }

        /// <summary>Ligne (à partir de 1) quand l'adaptateur la connaît, sinon null.</summary>
        public long? Line { get; }

        public ValidationError(string path, ValidationCode code, string message, long? line = null)
        {
            Path = path;
            Code = code;
            Message = message;
            Line = line;
        }

        public override string ToString() =>
            Line.HasValue ? $"{Path} (ligne {Line}) : {Message} [{Code}]" : $"{Path} : {Message} [{Code}]";
    }
}
