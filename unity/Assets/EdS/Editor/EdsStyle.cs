using System.Collections.Generic;
using System.Linq;
using EdS.Runtime;
using UnityEditor;
using UnityEngine;

namespace EdS.Game.Editor
{
    /// <summary>
    /// Essai de style (décision de Damien du 25 octobre 2026) : menu « Éclats du Seuil → Préparer l'essai de style ». Il parcourt les packs
    /// déposés dans <c>Assets/Style</c> (n'importe quels sous-dossiers), choisit les modèles et les sons par leur nom (<see cref="StyleRules"/>),
    /// règle l'import des modèles animés (animations « Legacy », attente, marche et course en boucle) et écrit le catalogue
    /// <c>Assets/Style/Resources/StyleCatalog.asset</c>, que le jeu lit au lancement. Relançable : il remplit de nouveau le catalogue.
    /// </summary>
    public static class EdsStyle
    {
        public const string Root = "Assets/Style";
        public const string CatalogFolder = Root + "/Resources";
        public const string CatalogPath = CatalogFolder + "/" + StyleCatalog.ResourceName + ".asset";

        [MenuItem("Éclats du Seuil/Préparer l'essai de style")]
        private static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                EditorUtility.DisplayDialog("Essai de style", "Dossier « " + Root + " » introuvable : dézippe d'abord les packs dans unity/Assets/Style.", "OK");
                return;
            }
            List<string> models = AssetDatabase.FindAssets("t:Model", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            List<string> sounds = AssetDatabase.FindAssets("t:AudioClip", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();

            string hero = StyleRules.Candidates(models, StyleSlot.Hero).FirstOrDefault();
            string wolf = StyleRules.Candidates(models, StyleSlot.Wolf).FirstOrDefault();
            string boar = StyleRules.Candidates(models, StyleSlot.Boar).FirstOrDefault();
            foreach (string animated in new[] { hero, wolf, boar }.Where(p => p != null)) Legacy(animated);

            if (!AssetDatabase.IsValidFolder(CatalogFolder)) AssetDatabase.CreateFolder(Root, "Resources");
            StyleCatalog catalog = AssetDatabase.LoadAssetAtPath<StyleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StyleCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Hero = Load(hero);
            catalog.Wolf = Load(wolf);
            catalog.Boar = Load(boar);
            catalog.Trees = StyleRules.Candidates(models, StyleSlot.Tree).Take(8).Select(Load).Where(m => m != null).ToArray();
            catalog.Rocks = StyleRules.Candidates(models, StyleSlot.Rock).Take(6).Select(Load).Where(m => m != null).ToArray();
            catalog.Plants = StyleRules.Candidates(models, StyleSlot.Plant).Take(8).Select(Load).Where(m => m != null).ToArray();
            catalog.Hit = Sound(sounds, StyleSound.Hit);
            catalog.Block = Sound(sounds, StyleSound.Block);
            catalog.Parry = Sound(sounds, StyleSound.Parry);
            catalog.Death = Sound(sounds, StyleSound.Death);
            catalog.Potion = Sound(sounds, StyleSound.Potion);
            catalog.Gather = Sound(sounds, StyleSound.Gather);
            catalog.Craft = Sound(sounds, StyleSound.Craft);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int found = new[] { catalog.Hit, catalog.Block, catalog.Parry, catalog.Death, catalog.Potion, catalog.Gather, catalog.Craft }.Count(c => c != null);
            string report = "Héros : " + Name(hero) + "\nLoup : " + Name(wolf) + "\nSanglier : " + Name(boar)
                + "\nArbres : " + catalog.Trees.Length + ", rochers : " + catalog.Rocks.Length + ", plantes : " + catalog.Plants.Length
                + "\nSons : " + found + " sur 7"
                + "\n\n" + models.Count + " modèles et " + sounds.Count + " sons trouvés dans Assets/Style. Appuie sur Play."
                + "\nUn modèle qui regarde de travers se tourne dans le catalogue (Assets/Style/Resources/StyleCatalog : Hero Yaw…).";
            Debug.Log("Essai de style\n" + report);
            EditorUtility.DisplayDialog("Essai de style", report, "OK");
        }

        /// <summary>Animations « Legacy » (lues par le composant Animation), attente, marche et course en boucle.</summary>
        private static void Legacy(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.animationType != ModelImporterAnimationType.Legacy || !importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            ModelImporterClipAnimation[] clips = importer.clipAnimations != null && importer.clipAnimations.Length > 0
                ? importer.clipAnimations : importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                bool loop = StyleRules.Loops(clip.name);
                clip.loopTime = loop;
                clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static GameObject Load(string path) => path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;

        private static AudioClip Sound(List<string> sounds, StyleSound sound) =>
            StyleRules.PickSound(sounds, sound) is string path ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;

        private static string Name(string path) => path != null ? System.IO.Path.GetFileName(path) : "aucun (forme simple)";
    }
}
