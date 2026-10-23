using System;
using System.IO;
using System.Text;
using EdS.Data.Json;
using EdS.Data.Saves;

namespace EdS.World
{
    /// <summary>Résultat d'une lecture de sauvegarde sur disque.</summary>
    public sealed class SaveReadResult
    {
        /// <summary>Sauvegarde lue ; null s'il n'y en a pas (nouvelle partie) ou si elle est illisible.</summary>
        public WorldSave? Save { get; }

        /// <summary>Le fichier existait mais il est illisible ou invalide (motif dans <see cref="Problem"/>), ainsi que sa copie.</summary>
        public bool Unreadable => Problem != null;

        public string? Problem { get; }

        /// <summary>La sauvegarde vient de la copie de secours (le fichier principal manquait ou était invalide).</summary>
        public bool FromBackup { get; }

        internal SaveReadResult(WorldSave? save, string? problem, bool fromBackup)
        {
            Save = save;
            Problem = problem;
            FromBackup = fromBackup;
        }
    }

    /// <summary>
    /// Sous-lot 12.1 (cadrage Q10, tranche : fichier local) : écriture atomique des sauvegardes. Le texte est d'abord écrit en entier dans un
    /// fichier voisin (<c>.tmp</c>), vidé sur le disque, puis il remplace la sauvegarde, dont la version précédente devient la copie de
    /// secours (<c>.bak</c>). Une coupure pendant l'écriture laisse donc toujours une sauvegarde complète : l'ancienne, ou sa copie.
    /// À la lecture, une sauvegarde absente ou invalide est remplacée par sa copie de secours valide.
    /// </summary>
    public static class SaveStore
    {
        public static string TemporaryPath(string path) => path + ".tmp";

        public static string BackupPath(string path) => path + ".bak";

        /// <summary>Écrit <paramref name="save"/> dans <paramref name="path"/> (texte canonique), de façon atomique.</summary>
        public static void Write(string path, WorldSave save)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Chemin de sauvegarde vide.", nameof(path));
            byte[] bytes = new UTF8Encoding(false).GetBytes(SaveJsonReader.Write(save ?? throw new ArgumentNullException(nameof(save))));
            string temporary = TemporaryPath(path), backup = BackupPath(path);
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (!File.Exists(path))
            {
                File.Move(temporary, path);
                return;
            }
            try
            {
                File.Replace(temporary, path, backup);
            }
            catch (PlatformNotSupportedException)
            {
                // Plateformes sans remplacement atomique (certains systèmes de fichiers mobiles) : la copie de secours est faite avant que
                // la sauvegarde principale soit retirée ; la lecture la reprend si la suite est interrompue.
                File.Copy(path, backup, true);
                File.Delete(path);
                File.Move(temporary, path);
            }
        }

        /// <summary>
        /// Lit la sauvegarde de <paramref name="path"/>, ou sa copie de secours si le fichier principal manque ou est invalide. Ni l'un ni
        /// l'autre : nouvelle partie (<see cref="SaveReadResult.Save"/> nul, sans problème).
        /// </summary>
        public static SaveReadResult Read(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Chemin de sauvegarde vide.", nameof(path));
            string backup = BackupPath(path);
            string? problem = null;
            if (File.Exists(path))
            {
                SaveLoadResult main = SaveJsonReader.ReadFile(path);
                if (main.IsValid) return new SaveReadResult(main.Save, null, false);
                problem = $"sauvegarde invalide : {main.Report.Errors[0].Path} : {main.Report.Errors[0].Message}";
            }
            if (File.Exists(backup))
            {
                SaveLoadResult copy = SaveJsonReader.ReadFile(backup);
                if (copy.IsValid) return new SaveReadResult(copy.Save, null, true);
                problem ??= $"copie de secours invalide : {copy.Report.Errors[0].Path} : {copy.Report.Errors[0].Message}";
            }
            return new SaveReadResult(null, problem, false);
        }
    }
}
