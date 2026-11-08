using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.9.3 : fenêtre du rapport de vérification de l'essai (résumé en haut, puis chaque section, problèmes d'abord). Les marques
    /// ✅ ⚠️ ❌ du fichier deviennent « OK », « REPLI », « BLOQUANT » en couleur (la police de l'éditeur n'a pas toujours ces symboles).
    /// </summary>
    public sealed class EdsVerificationWindow : EditorWindow
    {
        private EssaiReport _report;
        private Vector2 _scroll;

        /// <summary>
        /// Décision de Damien du 8 novembre 2026 : la fenêtre ne s'ouvre que s'il y a de l'orange ou du rouge ; tout vert, la Console dit
        /// « ✅ tout est prêt » (écrit par <see cref="EdsVerification"/>). Une fenêtre déjà ouverte est mise à jour.
        /// </summary>
        internal static void ShowIfNeeded(EssaiReport report)
        {
            if (report.Overall != EssaiLevel.Ok) Open(report);
            else if (HasOpenInstances<EdsVerificationWindow>()) Open(report);
        }

        internal static void Open(EssaiReport report)
        {
            EdsVerificationWindow window = GetWindow<EdsVerificationWindow>("Vérification de l'essai");
            window._report = report;
            window.minSize = new Vector2(520f, 360f);
            window.Show();
            window.Repaint();
        }

        private static string Plain(string text) => (text ?? "").Replace("✅", "[OK]").Replace("⚠️", "[REPLI]").Replace("❌", "[BLOQUANT]");

        private static Color ColorOf(EssaiLevel level) =>
            level == EssaiLevel.Ok ? new Color(0.45f, 0.85f, 0.45f, 1f) : level == EssaiLevel.Warning ? new Color(1f, 0.78f, 0.3f, 1f) : new Color(1f, 0.4f, 0.35f, 1f);

        private static string Word(EssaiLevel level) => level == EssaiLevel.Ok ? "OK" : level == EssaiLevel.Warning ? "REPLI" : "BLOQUANT";

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Relancer la vérification")) _report = EdsVerification.Run();
            if (GUILayout.Button("Ouvrir le fichier du rapport")) EditorUtility.OpenWithDefaultApp(EdsVerification.FullReportPath);
            GUILayout.EndHorizontal();
            if (_report == null)
            {
                GUILayout.Label("Pas encore de rapport : clique sur « Relancer la vérification ».", EditorStyles.wordWrappedLabel);
                return;
            }
            Color before = GUI.color;
            GUI.color = ColorOf(_report.Overall);
            GUILayout.Label(Plain(_report.Summary()), EditorStyles.wordWrappedLabel);
            GUI.color = before;
            GUILayout.Label("Fichier : " + EssaiManifest.ReportPath, EditorStyles.miniLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (IGrouping<string, EssaiLine> section in _report.Lines.GroupBy(l => l.Section))
            {
                GUILayout.Space(8f);
                GUILayout.Label(section.Key, EditorStyles.boldLabel);
                foreach (EssaiLine line in section.OrderByDescending(l => l.Level))
                {
                    GUI.color = ColorOf(line.Level);
                    GUILayout.Label(Word(line.Level) + "   " + line.Label + " : " + line.Detail, EditorStyles.wordWrappedLabel);
                    GUI.color = before;
                    if (line.Fix.Length > 0) GUILayout.Label("      → " + line.Fix, EditorStyles.wordWrappedMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
