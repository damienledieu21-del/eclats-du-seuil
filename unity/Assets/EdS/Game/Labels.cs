using System.Collections.Generic;
using EdS.Runtime;
using UnityEngine;

namespace EdS.Game
{
    /// <summary>Sous-lot 9.3 : textes et couleurs affichés (altérations, retours de combat).</summary>
    public static class Labels
    {
        private static readonly Dictionary<string, string> States = new Dictionary<string, string>
        {
            ["stun"] = "Étourdi",
            ["stunImmunity"] = "Immunité à l'étourdissement",
            ["freeze"] = "Gelé",
            ["bleed"] = "Saignement",
            ["burn"] = "Brûlure",
            ["fragility"] = "Fragilité",
            ["guardBreak"] = "Garde brisée",
            ["taunt"] = "Provoqué",
            ["oathTaunt"] = "Provoqué (Serment)",
            ["bastionAbsoluteTaunt"] = "Provoqué (Bastion absolu)",
            ["steelBarrier"] = "Barrière d'acier",
            ["reactiveAegis"] = "Égide réactive",
            ["protectiveLink"] = "Lien protecteur",
            ["bastionAbsolute"] = "Bastion absolu",
            ["bastionAbsoluteGuard"] = "Garde inébranlable",
        };

        public static string State(string id) => States.TryGetValue(id, out string name) ? name : id;

        /// <summary>Texte flottant d'un retour, ou null s'il s'affiche dans la ligne de messages.</summary>
        public static string Floating(CombatFeedback f)
        {
            switch (f.Kind)
            {
                case FeedbackKind.DamageTaken: return "-" + f.Amount;
                case FeedbackKind.Absorbed: return "(" + f.Amount + " absorbés)";
                case FeedbackKind.Blocked: return "Bloqué";
                case FeedbackKind.PerfectParry: return "Parade parfaite !";
                case FeedbackKind.FailedParry: return "Parade ratée";
                case FeedbackKind.GuardBroken: return "Garde brisée !";
                case FeedbackKind.CriticalHit: return "Critique !";
                case FeedbackKind.Ultimate: return "Bastion absolu !";
                case FeedbackKind.Died: return "Hors de combat";
                default: return null;
            }
        }

        public static Color ColorOf(CombatFeedback f, bool onPlayer)
        {
            switch (f.Kind)
            {
                case FeedbackKind.DamageTaken: return onPlayer ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.85f, 0.4f);
                case FeedbackKind.Absorbed: return new Color(0.45f, 0.85f, 1f);
                case FeedbackKind.Blocked: return new Color(0.8f, 0.8f, 0.85f);
                case FeedbackKind.PerfectParry: return new Color(1f, 0.84f, 0.2f);
                case FeedbackKind.FailedParry: return new Color(1f, 0.55f, 0.2f);
                case FeedbackKind.GuardBroken: return new Color(1f, 0.2f, 0.2f);
                case FeedbackKind.CriticalHit: return new Color(1f, 0.95f, 0.3f);
                case FeedbackKind.Ultimate: return new Color(0.8f, 0.5f, 1f);
                default: return Color.white;
            }
        }
    }
}
