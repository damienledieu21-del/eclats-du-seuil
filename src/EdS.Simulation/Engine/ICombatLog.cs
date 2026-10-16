using System;
using System.Collections.Generic;
using System.IO;

namespace EdS.Simulation.Engine
{
    /// <summary>Destination du journal de combat : une ligne JSON par appel (format JSON Lines).</summary>
    public interface ICombatLog
    {
        void Append(string line);
    }

    /// <summary>Journal en mémoire (tests, rejeu).</summary>
    public sealed class MemoryCombatLog : ICombatLog
    {
        private readonly List<string> _lines = new List<string>();
        public MemoryCombatLog() => Lines = _lines.AsReadOnly();
        public IReadOnlyList<string> Lines { get; }
        public void Append(string line) => _lines.Add(line ?? throw new ArgumentNullException(nameof(line)));

        /// <summary>Texte complet, chaque ligne terminée par « \n » (jamais « \r\n », pour une comparaison octet par octet).</summary>
        public override string ToString() => string.Concat(_lines.ConvertAll(l => l + "\n"));
    }

    /// <summary>Journal écrit au fil de l'eau dans un flux texte, lignes terminées par « \n ».</summary>
    public sealed class TextWriterCombatLog : ICombatLog
    {
        private readonly TextWriter _writer;
        public TextWriterCombatLog(TextWriter writer) => _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        public void Append(string line)
        {
            _writer.Write(line);
            _writer.Write('\n');
        }
    }
}
