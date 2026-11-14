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
            Resonance = "text.ui.hud.resonance", Infinite = "text.ui.hud.infinite", GuardBroken = "text.ui.hud.guard_broken",
            ParryOpen = "text.ui.hud.parry_open", GuardUp = "text.ui.hud.guard_up", ParryIn = "text.ui.hud.parry_in", GuardIdle = "text.ui.hud.guard_idle",
            Close = "text.ui.hud.close";

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

        /// <summary>Toutes les clés du HUD V2.1 (pour les tests et le contrôle au lancement).</summary>
        public static string[] AllKeys()
        {
            var keys = new System.Collections.Generic.List<string>
            {
                Health, Endurance, EnduranceShort, Resonance, Infinite, GuardBroken, ParryOpen, GuardUp, ParryIn, GuardIdle, Close,
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
