using System;
using EdS.Data.Story;

namespace EdS.Runtime
{
    /// <summary>
    /// Interface V2.1 : clés des textes du HUD et des commandes au doigt (<c>data/v0/world/texts/fr.json</c>, famille <c>text.ui.hud</c>).
    /// Les libellés viennent des données de textes, jamais du code : une clé absente s'affiche telle quelle (<see cref="TextCatalog.Get"/>)
    /// et les tests exigent qu'elles soient toutes présentes.
    /// </summary>
    public static class HudTextsV21
    {
        public const string Health = "text.ui.hud.health", Endurance = "text.ui.hud.endurance", EnduranceShort = "text.ui.hud.endurance_short",
            Infinite = "text.ui.hud.infinite", GuardBroken = "text.ui.hud.guard_broken",
            ParryOpen = "text.ui.hud.parry_open", GuardUp = "text.ui.hud.guard_up", ParryIn = "text.ui.hud.parry_in", GuardIdle = "text.ui.hud.guard_idle",
            Unblockable = "text.ui.hud.unblockable", Seconds = "text.ui.hud.seconds", Level = "text.ui.hud.level", UltimateKey = "text.ui.hud.ultimate_key",
            SettingsTitle = "text.ui.hud.settings.title", CameraClick = "text.ui.hud.settings.camera_click", CameraFree = "text.ui.hud.settings.camera_free",
            InvertOn = "text.ui.hud.settings.invert_on", InvertOff = "text.ui.hud.settings.invert_off", Sensitivity = "text.ui.hud.settings.sensitivity",
            Creator = "text.ui.hud.settings.creator", Minus = "text.ui.hud.settings.minus", Plus = "text.ui.hud.settings.plus";

        /// <summary>Huit directions de la boussole, dans le sens des aiguilles d'une montre depuis le nord (0°, 45°, …).</summary>
        public static readonly string[] Compass =
        {
            "text.ui.hud.compass.n", "text.ui.hud.compass.ne", "text.ui.hud.compass.e", "text.ui.hud.compass.se",
            "text.ui.hud.compass.s", "text.ui.hud.compass.so", "text.ui.hud.compass.o", "text.ui.hud.compass.no",
        };

        /// <summary>Clé du libellé d'un bouton tactile ; les compétences 1 à 6 gardent leur chiffre (pas de texte).</summary>
        public static string? Button(HudButton button, HudMode mode)
        {
            switch (button)
            {
                case HudButton.Parry: return "text.ui.hud.btn.parry";
                case HudButton.Guard: return "text.ui.hud.btn.guard";
                case HudButton.Ultimate: return "text.ui.hud.btn.ultimate";
                case HudButton.Potion: return "text.ui.hud.btn.potion";
                case HudButton.Target: return "text.ui.hud.btn.target";
                case HudButton.Act: return "text.ui.hud.btn.act";
                case HudButton.Ally: return "text.ui.hud.btn.ally";
                case HudButton.Pause: return "text.ui.hud.btn.pause";
                case HudButton.Menu: return "text.ui.hud.btn.menu";
                case HudButton.View: return "text.ui.hud.btn.view";
                case HudButton.Bag: return "text.ui.hud.btn.bag";
                case HudButton.Journal: return "text.ui.hud.btn.journal";
                case HudButton.Talents: return "text.ui.hud.btn.talents";
                case HudButton.Workshop: return "text.ui.hud.btn.workshop";
                case HudButton.Restart: return mode == HudMode.Adventure ? "text.ui.hud.btn.halt" : "text.ui.hud.btn.restart";
                case HudButton.NextScene: return "text.ui.hud.btn.next_scene";
                case HudButton.Help: return "text.ui.hud.btn.help";
                case HudButton.More: return "text.ui.hud.btn.more";
                default: return null;
            }
        }

        /// <summary>
        /// Largeur estimée d'un texte en Barlow Condensed (au plus une demi-capitale par lettre), espacement des lettres en centièmes
        /// d'em compris : sert à poser les onglets du livre à la taille de leur texte et aux tests qui vérifient qu'un libellé tient.
        /// </summary>
        public static float EstimateWidth(string text, float size, float spacing = 0f) =>
            string.IsNullOrEmpty(text) ? 0f : text.Length * (0.5f + spacing / 100f) * size;

        /// <summary>Onglets du livre : taille du texte, espacement, marge de chaque onglet, abscisse du premier, bord droit disponible.</summary>
        public const float TabFont = 21f, TabSpacing = 12f, TabPadding = 18f, TabStart = 540f, TabEnd = 1460f;

        /// <summary>Clés des onglets du livre, dans l'ordre (AdventureBook).</summary>
        public static readonly string[] BookTabs =
        {
            "text.ui.tab.character", "text.ui.tab.skills", "text.ui.tab.talents", "text.ui.tab.journal", "text.ui.tab.crafting", "text.ui.tab.map",
        };

        /// <summary>Toutes les clés du HUD V2.1 (pour les tests et le contrôle au lancement).</summary>
        public static string[] AllKeys()
        {
            var keys = new System.Collections.Generic.List<string>
            {
                Health, Endurance, EnduranceShort, Infinite, GuardBroken, ParryOpen, GuardUp, ParryIn, GuardIdle, Unblockable, Seconds, Level,
                UltimateKey, SettingsTitle, CameraClick, CameraFree, InvertOn, InvertOff, Sensitivity, Creator, Minus, Plus,
            };
            keys.AddRange(Compass);
            foreach (HudButton b in (HudButton[])Enum.GetValues(typeof(HudButton)))
                foreach (HudMode m in (HudMode[])Enum.GetValues(typeof(HudMode)))
                {
                    string? k = Button(b, m);
                    if (k != null && !keys.Contains(k)) keys.Add(k);
                }
            return keys.ToArray();
        }
    }
}
