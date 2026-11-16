using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Sous-lot V2.54 : l'icône de l'application (icône 1024 du dossier « Logo définitif », <c>Assets/EdS/Icone/eds_icone_1024.png</c>) dans les
    /// réglages du lecteur : l'icône par défaut (PC) et, sur Android, les icônes à une couche (classique et ronde ; l'icône adaptative à deux
    /// couches n'est pas touchée, Unity la déduit de l'icône par défaut). Chaque taille demandée par Unity reçoit l'image (Unity la réduit).
    /// Appliquée à l'ouverture du projet seulement si l'icône par défaut est vide (un choix fait à la main n'est jamais écrasé), ou par le
    /// menu « Éclats du Seuil → Appliquer l'icône de l'application » (qui remplace). <c>ProjectSettings/ProjectSettings.asset</c> n'est pas
    /// suivi par git : le dépôt ne change aucun réglage du lecteur, le script les pose chez chaque développeur.
    /// </summary>
    [InitializeOnLoad]
    public static class EdsAppIcon
    {
        private const string IconPath = "Assets/EdS/Icone/eds_icone_1024.png";

        static EdsAppIcon()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("Éclats du Seuil/Appliquer l'icône de l'application")]
        private static void ApplyFromMenu() => Apply(true);

        private static void Apply(bool replace)
        {
            try
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                if (icon == null)
                {
                    if (replace) Debug.LogWarning("Éclats du Seuil : icône introuvable (" + IconPath + ").");
                    return;
                }
                Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application);
                bool empty = current.Length == 0 || Array.TrueForAll(current, t => t == null);
                if (!empty && !replace) return;
                Fill(NamedBuildTarget.Unknown, icon);
                Fill(NamedBuildTarget.Android, icon);
                foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKindsForNamedBuildTarget(NamedBuildTarget.Android))
                {
                    PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                    bool single = icons.Length > 0;
                    foreach (PlatformIcon p in icons) single &= p.maxLayerCount == 1;
                    if (!single) continue;
                    foreach (PlatformIcon p in icons) p.SetTexture(icon, 0);
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Éclats du Seuil : icône de l'application appliquée (PC et Android).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Éclats du Seuil : icône de l'application non appliquée : " + e.Message);
            }
        }

        private static void Fill(NamedBuildTarget target, Texture2D icon)
        {
            int count = PlayerSettings.GetIcons(target, IconKind.Application).Length;
            if (count == 0) count = 1;
            var all = new Texture2D[count];
            for (int i = 0; i < count; i++) all[i] = icon;
            PlayerSettings.SetIcons(target, all, IconKind.Application);
        }
    }
}
