using System.Collections.Generic;
using System.IO;
using System.Linq;
using EdS.Game;
using EdS.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Relecture de la PR #122 (sous-lot V2.12) : le catalogue de style est dans <c>Resources</c>, donc tout modèle qu'il nomme part dans le jeu
    /// construit. Pour le téléphone (Android, iOS), les ressources « jamais sur téléphone » (<see cref="Wave5Fix.NotOnPhone"/> : grand loup,
    /// demi-cape, pont de pierre, tours en ruine, guirlande, tente d'étal, roue à eau, abri à bêtes, bâtiments lointains…) sont retirées du
    /// catalogue le temps de la construction, puis remises après : elles ne pèsent rien dans l'APK. Si la construction échoue (Unity n'appelle
    /// alors pas la fin), elles sont remises au prochain chargement des scripts, sinon un message demande de relancer la préparation.
    /// </summary>
    [InitializeOnLoad]
    public sealed class EdsPhoneBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Marque laissée pendant une construction pour téléphone (dossier Library, hors dépôt).</summary>
        private const string Marker = "Library/EdsPhoneBuild.txt";

        private static Wave5Look[] _removed;
        private static GameObject _direWolf;

        public int callbackOrder => 5;

        static EdsPhoneBuild()
        {
            // Une construction pour téléphone interrompue : les scripts rechargés ont perdu ce qui avait été retiré.
            if (File.Exists(Marker) && _removed == null)
            {
                File.Delete(Marker);
                Debug.LogWarning("Éclats du Seuil : une construction pour téléphone s'est arrêtée avant la fin ; relance « Préparer l'essai de style » "
                    + "pour remettre au catalogue les ressources réservées à l'ordinateur.");
            }
        }

        private static bool Phone => EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        public void OnPreprocessBuild(BuildReport report)
        {
            Restore();
            if (!Phone) return;
            StyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StyleCatalog>(EdsStyle.CatalogPath);
            if (catalog == null || catalog.Wave5 == null) return;
            List<Wave5Look> kept = catalog.Wave5.Where(l => l == null || !(Wave5.EntryOrNull(l.Role) is Wave5Entry e) || Wave5.Shown(e, true)).ToList();
            _removed = catalog.Wave5.Except(kept).ToArray();
            _direWolf = Wave5.Shown(Wave5.Entry(Wave5Role.DireWolf), true) ? null : catalog.DireWolf;
            if (_removed.Length == 0 && _direWolf == null)
            {
                _removed = null;
                return;
            }
            catalog.Wave5 = kept.ToArray();
            if (_direWolf != null) catalog.DireWolf = null;
            File.WriteAllText(Marker, _removed.Length + " ressources retirées pour le téléphone");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Éclats du Seuil : " + _removed.Length + " ressources réservées à l'ordinateur retirées de la construction pour téléphone.");
        }

        public void OnPostprocessBuild(BuildReport report) => Restore();

        /// <summary>Remet au catalogue ce qui avait été retiré pour le téléphone.</summary>
        private static void Restore()
        {
            if (_removed == null && _direWolf == null) return;
            StyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StyleCatalog>(EdsStyle.CatalogPath);
            if (catalog != null)
            {
                catalog.Wave5 = (catalog.Wave5 ?? new Wave5Look[0]).Concat(_removed ?? new Wave5Look[0]).ToArray();
                if (_direWolf != null) catalog.DireWolf = _direWolf;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            _removed = null;
            _direWolf = null;
            if (File.Exists(Marker)) File.Delete(Marker);
        }
    }
}
