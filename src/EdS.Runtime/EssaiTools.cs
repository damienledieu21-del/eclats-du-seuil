using System;
using System.IO;
using System.IO.Compression;

namespace EdS.Runtime
{
    /// <summary>Issue d'une sauvegarde du dossier des ressources (V1.10.1).</summary>
    public enum BackupOutcome
    {
        /// <summary>Zip complet, relu, renommé.</summary>
        Success,
        /// <summary>Le dossier source n'existe pas : rien n'est écrit.</summary>
        SourceMissing,
        /// <summary>Le zip ou son brouillon existe déjà : rien n'est écrasé.</summary>
        TargetExists,
        /// <summary>Écriture ou relecture en échec : un brouillon <c>.incomplet</c> peut rester, laissé tel quel.</summary>
        Incomplete,
        /// <summary>Zip complet et relu, mais le renommage final a échoué : il reste sous son nom <c>.incomplet</c>.</summary>
        CompleteNotRenamed,
    }

    /// <summary>Résultat de la préparation de la scène de démarrage.</summary>
    public enum StartupSceneState { Ready, Cancelled, Failed }

    /// <summary>Suite de « Préparer et lancer l'essai » après la scène de démarrage.</summary>
    public enum LaunchStep { Continue, StopSilently, StopWithMessage }

    /// <summary>Résultat d'une sauvegarde : issue, fichier final ou brouillon laissé, nombre d'entrées, message pour l'utilisateur.</summary>
    public sealed class BackupResult
    {
        public BackupResult(BackupOutcome outcome, string? path, int entries, string message)
        {
            Outcome = outcome; Path = path; Entries = entries; Message = message;
        }

        public BackupOutcome Outcome { get; }
        public string? Path { get; }
        public int Entries { get; }
        public string Message { get; }
        public bool Succeeded => Outcome == BackupOutcome.Success;
    }

    /// <summary>
    /// V1.10.1 : logique pure (sans Unity) des outils d'essai, testée. Ne supprime et n'écrase jamais rien : le zip s'écrit sous
    /// <c>.incomplet</c>, est relu (ouverture, nombre d'entrées), puis renommé.
    /// </summary>
    public static class EssaiTools
    {
        public const string DraftSuffix = ".incomplet";

        /// <summary>Nom horodaté <c>Style-AAAA-MM-JJ_HH-MM-SS.zip</c>.</summary>
        public static string BackupFileName(DateTime now) => "Style-" + now.ToString("yyyy-MM-dd_HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture) + ".zip";

        /// <summary>Le chemin passe par un dossier OneDrive (le zip partirait dans le nuage).</summary>
        public static bool IsInOneDrive(string path) => path != null && path.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Zippe <paramref name="source"/> (entrées sous « Style/ ») dans <paramref name="targetFolder"/>. <paramref name="progress"/> reçoit (fait, total).
        /// </summary>
        public static BackupResult Backup(string source, string targetFolder, DateTime now, Action<int, int>? progress = null)
        {
            string? draft = null;
            try
            {
                if (!Directory.Exists(source))
                    return new BackupResult(BackupOutcome.SourceMissing, null, 0, "Échec : le dossier des ressources est introuvable (" + source + "). Rien n'a été copié ni supprimé.");
                Directory.CreateDirectory(targetFolder);
                string target = System.IO.Path.Combine(targetFolder, BackupFileName(now));
                string tmp = target + DraftSuffix;
                if (File.Exists(target) || File.Exists(tmp))
                    return new BackupResult(BackupOutcome.TargetExists, File.Exists(target) ? target : tmp, 0,
                        "Échec : « " + (File.Exists(target) ? target : tmp) + " » existe déjà (attends une seconde, ou supprime-le toi-même). Rien n'a été écrasé.");
                string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                draft = tmp;
                using (ZipArchive zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (i % 20 == 0) progress?.Invoke(i, files.Length);
                        string entry = "Style/" + files[i].Substring(source.Length).TrimStart('/', '\\').Replace('\\', '/');
                        zip.CreateEntryFromFile(files[i], entry, CompressionLevel.Fastest);
                    }
                int found;
                using (ZipArchive check = ZipFile.OpenRead(tmp)) found = check.Entries.Count;
                if (found != files.Length)
                    return new BackupResult(BackupOutcome.Incomplete, tmp, found, "Échec : le zip contient " + found + " fichiers au lieu de " + files.Length
                        + ". Il est laissé en place, incomplet : " + tmp + "\nRien du projet n'a été supprimé.");
                try
                {
                    File.Move(tmp, target);
                }
                catch (Exception moveError)
                {
                    return new BackupResult(BackupOutcome.CompleteNotRenamed, tmp, found, "Zip complet (" + found + " fichiers) mais non renommé : " + moveError.Message
                        + "\nIl est ici, utilisable après avoir enlevé « " + DraftSuffix + " » du nom : " + tmp);
                }
                return new BackupResult(BackupOutcome.Success, target, found, "Réussi : " + found + " fichiers sauvegardés (dossier Assets/Style) dans\n" + target
                    + "\n(hors du projet ; rien n'a été supprimé)."
                    + (IsInOneDrive(target) ? "\nAttention : ce dossier est dans OneDrive, le zip sera envoyé dans le nuage (prend de la place en ligne)." : ""));
            }
            catch (Exception e)
            {
                return new BackupResult(BackupOutcome.Incomplete, draft, 0, "Échec : " + e.Message
                    + (draft != null && File.Exists(draft) ? "\nFichier incomplet laissé en place (tu peux le supprimer toi-même) : " + draft : "")
                    + "\nVérifie la place libre sur le disque et les droits du dossier. Rien du projet n'a été supprimé.");
            }
        }

        /// <summary>
        /// Décision après la scène de démarrage : annulée par l'utilisateur = arrêt immédiat et sans rien d'autre (pas de seconde question,
        /// pas de préparation) ; échec ou scène absente = arrêt avec message ; sinon on continue.
        /// </summary>
        public static LaunchStep AfterStartupScene(StartupSceneState state, bool sceneAssetExists)
        {
            if (state == StartupSceneState.Cancelled) return LaunchStep.StopSilently;
            return state == StartupSceneState.Ready && sceneAssetExists ? LaunchStep.Continue : LaunchStep.StopWithMessage;
        }

        /// <summary>
        /// Faut-il ouvrir la scène de démarrage seule (avec la question d'enregistrement) avant de lancer le jeu ? Oui sauf si elle est déjà
        /// la seule scène ouverte.
        /// </summary>
        public static bool ShouldOpenStartupSceneAlone(string activeScenePath, int loadedSceneCount, string startupScenePath)
            => activeScenePath != startupScenePath || loadedSceneCount != 1;
    }
}
