using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EdS.Net
{
    /// <summary>
    /// Revue Codex de la PR #19 : dossier des journaux rejouables d'une salle (<c>combat-001.jsonl</c>, <c>combat-002.jsonl</c>…). Un
    /// journal n'en écrase jamais un autre : la numérotation reprend après le plus grand numéro déjà présent (un serveur relancé sur le même
    /// dossier continue la série), et chaque fichier est créé en exclusivité (un fichier apparu entre-temps fait passer au numéro suivant).
    /// </summary>
    public sealed class JournalFolder
    {
        public const string Prefix = "combat-";
        public const string Extension = ".jsonl";

        /// <summary>Numéros essayés au plus pour un journal avant d'abandonner.</summary>
        public const int MaxAttempts = 1000;

        public JournalFolder(string directory)
        {
            if (directory is null) throw new ArgumentNullException(nameof(directory));
            Directory = Path.GetFullPath(directory);
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; }

        /// <summary>Écrit un journal (une ligne par entrée, fin de ligne « \n », UTF-8 sans marque) ; rend le chemin du fichier créé.</summary>
        public string Write(IReadOnlyList<string> lines)
        {
            if (lines is null) throw new ArgumentNullException(nameof(lines));
            long number = NextNumber();
            for (int attempt = 0; attempt < MaxAttempts; attempt++, number++)
            {
                string path = Path.Combine(Directory, Prefix + number.ToString("000", CultureInfo.InvariantCulture) + Extension);
                if (File.Exists(path)) continue;
                FileStream stream;
                try
                {
                    stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                }
                catch (IOException) when (File.Exists(path))
                {
                    continue;   // créé entre-temps par un autre serveur : numéro suivant
                }
                using (stream)
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.NewLine = "\n";
                    foreach (string line in lines) writer.WriteLine(line);
                }
                return path;
            }
            throw new IOException($"Aucun nom de journal libre dans {Directory} après {MaxAttempts} essais.");
        }

        /// <summary>Numéro qui suit le plus grand des journaux présents (1 pour un dossier sans journal).</summary>
        private long NextNumber()
        {
            long max = 0;
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory, Prefix + "*" + Extension))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                string digits = name.Substring(Prefix.Length);
                if (digits.Length > 0 && digits.Length <= 15 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long n) && n > max)
                    max = n;
            }
            return checked(max + 1);
        }
    }
}
