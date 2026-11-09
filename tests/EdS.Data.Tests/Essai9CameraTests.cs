using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 9 (Damien, 29 octobre 2026) : « la case où il y a les points de vie et l'endurance est trop grande, elle cache le
/// perso » ; « je n'aime pas les vues de caméra », « elle bouge mal » ; style choisi : World of Warcraft, souris sans clic par défaut,
/// « on peut le changer dans les réglages ».
/// </summary>
[Trait("Category", "Runtime")]
public class Essai9CameraTests
{
    private static string Unity(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root, "unity", "Assets", "EdS" }.Concat(path).ToArray())).Replace("\r\n", "\n");

    [Fact]
    public void Molette_et_pincement_rapprochent_dans_les_bornes()
    {
        Assert.Equal(8 * 0.85, CameraRules.Zoom(8, 1, false), 9);         // un cran : 15 % plus près
        Assert.Equal(8 / 0.85, CameraRules.Zoom(8, -1, false), 9);        // un cran en arrière
        Assert.Equal(CameraRules.MinDistance, CameraRules.Zoom(2, 40, false));
        Assert.Equal(CameraRules.MaxDistance, CameraRules.Zoom(20, -40, false));
        Assert.Equal(CameraRules.IndoorMaxDistance, CameraRules.Zoom(20, 0, true));   // dans une pièce, la caméra reste dans la maison
        Assert.Equal(CameraRules.DefaultDistance, CameraRules.Zoom(double.NaN, 1, false));
        // Pincement : toute la plage pour un geste complet, dans un sens comme dans l'autre.
        Assert.Equal(CameraRules.MinDistance, CameraRules.Pinch(CameraRules.MaxDistance, 1, false), 6);
        Assert.Equal(CameraRules.MaxDistance, CameraRules.Pinch(CameraRules.MinDistance, -1, false), 6);
        Assert.True(CameraRules.Pinch(8, 0.1, false) < 8 && CameraRules.Pinch(8, -0.1, false) > 8);
    }

    [Fact]
    public void Inclinaison_a_la_souris_bornee_et_inversable()
    {
        double up = CameraRules.Tilt(30, 1, 1, false);
        Assert.True(up < 30, "souris vers le haut : on lève les yeux, la caméra descend");
        Assert.True(CameraRules.Tilt(30, 1, 1, true) > 30, "inversé");
        Assert.Equal(30 - (30 - up) * 2, CameraRules.Tilt(30, 1, 2, false), 9);   // sensibilité double
        Assert.Equal(CameraRules.MinPitch, CameraRules.Tilt(0, 100, 1, false));
        Assert.Equal(CameraRules.MaxPitch, CameraRules.Tilt(60, -100, 1, false));
        Assert.Equal(CameraRules.MaxSensitivity, CameraRules.Sensitivity(9));
        Assert.Equal(CameraRules.MinSensitivity, CameraRules.Sensitivity(0));
        Assert.Equal(1, CameraRules.Sensitivity(double.NaN));
        Assert.Equal(CameraRules.DefaultPitch, CameraRules.Tilt(double.NaN, 1, 1, false));
    }

    [Fact]
    public void Position_autour_du_personnage_et_au_dessus_du_sol()
    {
        // Orientation 0 : la caméra est au sud du personnage (elle regarde vers le nord), au-dessus de lui.
        (double x, double y, double z) = CameraRules.Offset(0, 30, 10);
        Assert.Equal(0, x, 9);
        Assert.Equal(5, y, 9);
        Assert.Equal(-10 * Math.Cos(Math.PI / 6), z, 9);
        (x, _, z) = CameraRules.Offset(90, 0, 4);   // regard vers l'est : caméra à l'ouest
        Assert.Equal((-4, 0), (Math.Round(x, 9), Math.Round(z, 9)));
        // Par en dessous, la caméra se rapproche pour rester au-dessus du sol ; au-dessus, rien ne change.
        Assert.Equal(12, CameraRules.GroundSafeDistance(12, 20, 1.6));
        double held = CameraRules.GroundSafeDistance(12, -10, 1.6);
        Assert.True(held < 12);
        Assert.Equal(CameraRules.GroundClearance, 1.6 + Math.Sin(-10 * Math.PI / 180) * held, 6);
        Assert.InRange(CameraRules.TargetHeight(CameraRules.MinDistance), 1.59, 1.61);
        Assert.InRange(CameraRules.TargetHeight(CameraRules.MaxDistance), 1.19, 1.21);
        Assert.Equal(1, CameraRules.Behind(10));
        Assert.Equal(0, CameraRules.Behind(75));
        Assert.True(CameraRules.Behind(CameraRules.DefaultPitch) > 0.5, "au départ : derrière le personnage, reculer marche à reculons");
        // Relecture : à peine sous l'horizontale, aucune contrainte (la caméra ne saute plus à 1,6 m).
        Assert.Equal(12, CameraRules.GroundSafeDistance(12, -1e-8, 1.6));
        Assert.Equal(CameraRules.MaxPitch, CameraRules.Incline(70, 30));
        Assert.Equal(CameraRules.MinPitch, CameraRules.Incline(0, -30));
        Assert.Equal(35, CameraRules.Incline(30, 5), 9);
        Assert.Equal(350, CameraRules.Wrap(-10));
        Assert.Equal(10, CameraRules.Wrap(370));
        Assert.Equal(0, CameraRules.Wrap(double.NaN));
    }

    [Fact]
    public void Camera_qui_ne_tourne_jamais_toute_seule_et_reglages_dans_la_pause()
    {
        string rig = Unity("Game", "CameraRig.cs"), game = Unity("Game", "EdsGame.cs"), hud = Unity("Game", "HudView.cs");
        // Plus de rotation qui suit la marche ni de sauts d'un huitième de tour.
        Assert.DoesNotContain("LerpAngle(_yaw, playerYaw", rig, StringComparison.Ordinal);
        Assert.DoesNotContain("_yaw -= 45f", rig, StringComparison.Ordinal);
        Assert.Contains("if (Input.GetKey(Keys.TurnLeft)) _yaw -= turn;", rig, StringComparison.Ordinal);
        // Souris sans clic par défaut (choix du 26 octobre gardé), clic gauche maintenu au choix, hors de l'interface.
        Assert.Contains("ClickLook = PlayerPrefs.GetInt(ClickKey, 0) == 1;", rig, StringComparison.Ordinal);
        Assert.Contains("bool look = freeLook || _dragging || PointerInput.MouseLook(Input.touchCount, Input.GetMouseButton(2));", rig, StringComparison.Ordinal);
        Assert.Contains("_dragging = Input.touchCount == 0 && !Blocked && !OverInterface();", rig, StringComparison.Ordinal);
        // Relecture : en « clic maintenu », le curseur reste visible (pas de regard libre) et n'est caché que pendant la rotation ; un clic
        // dans un écran, un menu ou sur les commandes tactiles ne commence pas de rotation.
        Assert.Contains("|| _cursorReleased || _camera.ClickLook;", game, StringComparison.Ordinal);
        Assert.Contains("_camera.Blocked = covered || _touch.Active;", game, StringComparison.Ordinal);
        Assert.Contains("bool hidden = _freeLook || _camera.Dragging;", game, StringComparison.Ordinal);
        // Relecture : la hauteur visée se compte depuis les pieds (le joueur affiché est le milieu du corps, les pieds plus 1 m).
        Assert.Contains("Vector3 aim = player - Vector3.up + Vector3.up * (float)CameraRules.TargetHeight(_shownDistance);", rig, StringComparison.Ordinal);
        // Relecture : la distance de dehors est gardée à part dans une pièce, et l'aventure quittée la rend.
        Assert.Contains("if (value && !_indoors) _indoorDistance = (float)CameraRules.Zoom(_distance, 0, true);", rig, StringComparison.Ordinal);
        Assert.Contains("_camera.Indoors = false;", game, StringComparison.Ordinal);
        // Relecture : au doigt, l'inclinaison se règle aussi.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(game, @"_camera\.Nudge\(_touch\.YawDelta, _touch\.ViewDelta, _touch\.PitchDelta\);").Count);
        Assert.Contains("_pitch = (float)CameraRules.Tilt(_pitch, tilt, Sensitivity, InvertY);", rig, StringComparison.Ordinal);
        Assert.Contains("if (scroll != 0f) Zoom(CameraRules.Zoom(Wanted, scroll, _indoors));", rig, StringComparison.Ordinal);
        // Réglages montrés pendant la pause, reliés à la caméra.
        Assert.Contains("_hud.OnSettings(() => _camera.SetClickLook(!_camera.ClickLook), () => _camera.SetInvertY(!_camera.InvertY),", game, StringComparison.Ordinal);
        // Relecture : pas de réglages par-dessus une annonce (défaite, fin de session) ni sur l'écran de fin d'un combat d'entraînement.
        Assert.Contains("_hud.Settings(_paused && open && notice == null && !(_creator != null && _creator.Open), _camera.ClickLook, _camera.InvertY, _camera.Sensitivity);", game, StringComparison.Ordinal);
        Assert.Contains("_hud.Settings(_paused && !_combat.IsFinished, _camera.ClickLook, _camera.InvertY, _camera.Sensitivity);", game, StringComparison.Ordinal);
        Assert.Contains("_camera.Indoors = a.Indoors;", game, StringComparison.Ordinal);
        // Reculer à reculons quand la caméra est derrière le personnage (comme avant en vue 2).
        Assert.Contains("bool backward = _camera.Setting > 0.5f && z < 0f;", game, StringComparison.Ordinal);
        // Sceau réduit d'environ moitié, collé au bas de l'écran.
        Assert.Contains("public const float CrestScale = 0.42f;", hud, StringComparison.Ordinal);
        Assert.Contains("_crest.localScale = new Vector3(CrestScale, CrestScale, 1f);", hud, StringComparison.Ordinal);
        Assert.Contains("_crest = At(UiKit.Node(_bottom, \"Sceau\"), 500f, 322f, 290f, 320f, 0.5f, 1f);", hud, StringComparison.Ordinal);
        Assert.Contains("BuildSettings();", hud, StringComparison.Ordinal);
    }

    [Fact]
    public void Aide_des_touches_de_la_camera()
    {
        Assert.Contains(HelpKeys.Training, k => k.Key == "Molette" && k.Action == "Rapprocher la vue");
        Assert.Contains(HelpKeys.Training, k => k.Key == "C" && k.Action == "Vue derrière toi");
        Assert.Contains(HelpKeys.Adventure("F parler"), k => k.Key == "Échap" && k.Action == "Pause et réglages");
        Assert.DoesNotContain(HelpKeys.Training, k => k.Action.Contains("Vue 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Inclinaison_au_doigt()
    {
        // Relecture : le doigt de regard incline aussi la vue (descendre : la caméra monte) ; le pincement ne l'incline pas.
        var g = new TouchGestures();
        var buttons = Array.Empty<GestureButton>();
        g.Update(new[] { new FingerPoint(1, 1400, 500, FingerPhase.Began) }, buttons, 1920);
        g.Update(new[] { new FingerPoint(1, 1400, 550, FingerPhase.Moved) }, buttons, 1920);
        Assert.Equal(50 * TouchGestures.PitchPerPixel, g.PitchDelta, 4);
        Assert.Equal(0f, g.YawDelta);
        g.Update(new[] { new FingerPoint(1, 1400, 550, FingerPhase.Stationary) }, buttons, 1920);
        Assert.Equal(0f, g.PitchDelta);
        g.Update(new[] { new FingerPoint(1, 1400, 550, FingerPhase.Stationary), new FingerPoint(2, 1600, 550, FingerPhase.Began) }, buttons, 1920);
        g.Update(new[] { new FingerPoint(1, 1400, 500, FingerPhase.Moved), new FingerPoint(2, 1600, 600, FingerPhase.Moved) }, buttons, 1920);
        Assert.Equal(0f, g.PitchDelta);
        g.Reset();
        Assert.Equal(0f, g.PitchDelta);
    }

    [Fact]
    public void Un_ecran_qui_s_ouvre_annule_la_rotation_au_clic()
    {
        // Revue Codex de la PR #68 : avec le réglage « clic maintenu », ouvrir le menu M ou les talents T pendant une rotation laissait la
        // rotation en cours (Follow n'est plus appelé), donc le curseur caché. Couvrir le jeu l'annule, avant le choix du curseur.
        string rig = Unity("Game", "CameraRig.cs"), game = Unity("Game", "EdsGame.cs");
        Assert.Contains("if (value) _dragging = false;", rig, StringComparison.Ordinal);
        int blocked = game.IndexOf("_camera.Blocked = covered || _touch.Active;", StringComparison.Ordinal);
        int hidden = game.IndexOf("bool hidden = _freeLook || _camera.Dragging;", StringComparison.Ordinal);
        int talents = game.IndexOf("if (_talents != null)", hidden, StringComparison.Ordinal);
        Assert.True(blocked > 0 && hidden > blocked && talents > hidden);
    }
}
