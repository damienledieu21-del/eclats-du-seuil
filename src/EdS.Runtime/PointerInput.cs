namespace EdS.Runtime
{
    /// <summary>
    /// Essai du 12.9 (Damien, 25 octobre 2026 : « ça arrive quand je marche et que je clique sur agir, le bouclier se lève et consomme de
    /// l'énergie ») : sur un écran tactile, Unity simule des boutons de souris à partir des doigts posés ; deux doigts (stick et Agir) donnaient
    /// un clic droit, lu comme la garde du PC. Les boutons de souris ne comptent donc que lorsque aucun doigt n'est sur l'écran ; au doigt,
    /// seul le bouton Garde garde. Décision sans Unity, pour être testée (revue Codex de la PR #52).
    /// </summary>
    public static class PointerInput
    {
        /// <summary>Garde tenue : clic droit sans doigt sur l'écran, Maj, ou bouton Garde tactile tenu.</summary>
        public static bool Guard(int touchCount, bool rightMouse, bool shift, bool touchGuard) =>
            (touchCount == 0 && rightMouse) || shift || touchGuard;

        /// <summary>Caméra tournée à la souris : bouton du milieu sans doigt sur l'écran.</summary>
        public static bool MouseLook(int touchCount, bool middleMouse) => touchCount == 0 && middleMouse;
    }
}
