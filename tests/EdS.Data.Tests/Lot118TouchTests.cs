using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 11.8 (revue Codex de la PR #36, 6e tour) : traitement des doigts des commandes tactiles (<see cref="TouchGestures"/>, sans
/// Unity) : stick du pouce gauche, boutons tenus et touchés, regard et pincement sur la moitié droite, doigt perdu quand le jeu est
/// suspendu ; et taille des boutons sur l'écran du téléphone visé.
/// </summary>
[Trait("Category", "Runtime")]
public class Lot118TouchTests
{
    private const float Width = 2220f;   // Galaxy Note8 à l'horizontale (18,5:9), écran virtuel de 1 080 de haut
    private const int Guard = 0, Parry = 1, Skill = 2;

    private static readonly GestureButton[] Buttons =
    {
        new GestureButton(Guard, 2046, 906, 150, 150),
        new GestureButton(Parry, 1872, 906, 150, 150),
        new GestureButton(Skill, 1877, 742, 140, 140),
    };

    private static FingerPoint F(int id, float x, float y, FingerPhase phase) => new FingerPoint(id, x, y, phase);

    private static void Frame(TouchGestures g, params FingerPoint[] fingers) => g.Update(fingers, Buttons, Width);

    [Fact]
    public void Boutons_d_au_moins_9_mm_sur_le_Note8()
    {
        // 1 440 pixels à 522 ppp à l'horizontale : environ 70 mm pour les 1 080 pixels virtuels.
        double millimetresPerVirtual = 1440 / 522.0 * 25.4 / TouchGestures.VirtualHeight;
        Assert.True(TouchGestures.SmallButton * millimetresPerVirtual >= 9.0, $"{TouchGestures.SmallButton * millimetresPerVirtual:F2} mm");
        Assert.True(TouchGestures.BigButton >= TouchGestures.SmallButton);
        string touch = File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "TouchControls.cs"));
        Assert.Contains("Big = TouchGestures.BigButton, Small = TouchGestures.SmallButton", touch, StringComparison.Ordinal);
        Assert.Contains("_gestures.Update(Fingers(), _zones, Hud.Width)", touch, StringComparison.Ordinal);
    }

    [Fact]
    public void Stick_du_pouce_gauche_depuis_l_endroit_ou_il_se_pose()
    {
        var g = new TouchGestures();
        Frame(g, F(1, 300, 800, FingerPhase.Began));
        Assert.True(g.StickActive);
        Assert.Equal((300f, 800f), (g.StickOriginX, g.StickOriginY));
        Assert.Equal((0f, 0f), (g.MoveX, g.MoveY));
        // À mi-rayon vers la droite et le haut de l'écran : demi-vitesse, y vers le haut.
        Frame(g, F(1, 360, 740, FingerPhase.Moved));
        Assert.Equal(0.5f, g.MoveX, 4);
        Assert.Equal(0.5f, g.MoveY, 4);
        // Au-delà du rayon : longueur ramenée à 1, même direction.
        Frame(g, F(1, 300, 400, FingerPhase.Moved));
        Assert.Equal(0f, g.MoveX, 4);
        Assert.Equal(1f, g.MoveY, 4);
        Frame(g, F(1, 300, 400, FingerPhase.Stationary));
        Assert.Equal(1f, g.MoveY, 4);
        // Un second doigt sur la moitié gauche ne prend pas le stick.
        Frame(g, F(1, 300, 400, FingerPhase.Stationary), F(2, 600, 900, FingerPhase.Began));
        Assert.Equal((300f, 800f), (g.StickOriginX, g.StickOriginY));
        Frame(g, F(1, 300, 400, FingerPhase.Ended), F(2, 600, 900, FingerPhase.Stationary));
        Assert.False(g.StickActive);
        Assert.Equal((0f, 0f), (g.MoveX, g.MoveY));
    }

    [Fact]
    public void Boutons_touches_une_image_et_tenus_jusqu_au_dernier_doigt_leve()
    {
        var g = new TouchGestures();
        Frame(g, F(1, 2100, 950, FingerPhase.Began));   // garde
        Assert.True(g.WasTapped(Guard) && g.IsHeld(Guard));
        Assert.False(g.StickActive);
        Assert.Equal(0f, g.YawDelta);
        Frame(g, F(1, 2100, 950, FingerPhase.Stationary), F(2, 1900, 800, FingerPhase.Began));   // compétence par-dessus
        Assert.False(g.WasTapped(Guard));
        Assert.True(g.IsHeld(Guard));
        Assert.True(g.WasTapped(Skill));
        // Bord compris ; un second doigt sur le même bouton : tenu jusqu'au dernier levé.
        Frame(g, F(1, 2100, 950, FingerPhase.Stationary), F(2, 1900, 800, FingerPhase.Ended), F(3, 2046, 906, FingerPhase.Began));
        Assert.False(g.IsHeld(Skill));
        Assert.True(g.WasTapped(Guard));
        Frame(g, F(1, 2100, 950, FingerPhase.Ended), F(3, 2046, 906, FingerPhase.Stationary));
        Assert.True(g.IsHeld(Guard));
        Frame(g, F(3, 2046, 906, FingerPhase.Canceled));
        Assert.False(g.IsHeld(Guard));
        // Un doigt qui glisse sur un bouton après s'être posé ailleurs ne le touche pas.
        Frame(g, F(4, 1500, 500, FingerPhase.Began));
        Frame(g, F(4, 1950, 980, FingerPhase.Moved));
        Assert.False(g.IsHeld(Parry) || g.WasTapped(Parry));
    }

    [Fact]
    public void Regard_au_doigt_droit_et_pincement_entre_les_vues()
    {
        var g = new TouchGestures();
        Frame(g, F(1, 1400, 500, FingerPhase.Began));
        Frame(g, F(1, 1440, 520, FingerPhase.Moved));
        Assert.Equal(40 * TouchGestures.YawPerPixel, g.YawDelta, 4);
        Frame(g, F(1, 1440, 520, FingerPhase.Stationary));
        Assert.Equal(0f, g.YawDelta);
        // Deuxième doigt : pincement ; l'écart qui grandit va vers la vue 2, le regard est suspendu.
        Frame(g, F(1, 1440, 520, FingerPhase.Stationary), F(2, 1640, 520, FingerPhase.Began));
        Assert.Equal(0f, g.ViewDelta);
        Frame(g, F(1, 1380, 520, FingerPhase.Moved), F(2, 1640, 520, FingerPhase.Stationary));
        Assert.Equal(60 / TouchGestures.PinchPixelsPerView, g.ViewDelta, 4);
        Assert.Equal(0f, g.YawDelta);
        Frame(g, F(1, 1440, 520, FingerPhase.Moved), F(2, 1640, 520, FingerPhase.Stationary));
        Assert.True(g.ViewDelta < 0, "rapprochés : vers la vue 1");
        // Le doigt de regard levé, l'autre reprend le regard aussitôt.
        Frame(g, F(1, 1440, 520, FingerPhase.Ended), F(2, 1640, 520, FingerPhase.Stationary));
        Assert.Equal(0f, g.ViewDelta);
        Frame(g, F(2, 1600, 520, FingerPhase.Moved));
        Assert.Equal(-40 * TouchGestures.YawPerPixel, g.YawDelta, 4);
        // Un doigt sur la moitié gauche ne tourne pas la vue.
        var left = new TouchGestures();
        Frame(left, F(1, 200, 500, FingerPhase.Began));
        Frame(left, F(1, 400, 500, FingerPhase.Moved));
        Assert.Equal(0f, left.YawDelta);
    }

    [Fact]
    public void Doigt_perdu_quand_le_jeu_est_suspendu_relache_et_reset()
    {
        // Android peut suspendre le jeu sans livrer la fin des doigts : un doigt mémorisé absent de l'image est relâché.
        var g = new TouchGestures();
        Frame(g, F(1, 300, 800, FingerPhase.Began), F(2, 2100, 950, FingerPhase.Began), F(3, 1400, 500, FingerPhase.Began));
        Frame(g, F(1, 360, 740, FingerPhase.Moved), F(2, 2100, 950, FingerPhase.Stationary), F(3, 1400, 500, FingerPhase.Stationary));
        Assert.True(g.StickActive && g.IsHeld(Guard));
        Frame(g);
        Assert.False(g.StickActive);
        Assert.False(g.IsHeld(Guard));
        Assert.Equal((0f, 0f), (g.MoveX, g.MoveY));
        // Un nouveau doigt au même identifiant repart de zéro (nouvelle origine du stick, pas de saut du regard).
        Frame(g, F(1, 500, 600, FingerPhase.Began), F(3, 1600, 500, FingerPhase.Began));
        Assert.Equal((500f, 600f), (g.StickOriginX, g.StickOriginY));
        Frame(g, F(1, 500, 600, FingerPhase.Stationary), F(3, 1610, 500, FingerPhase.Moved));
        Assert.Equal(10 * TouchGestures.YawPerPixel, g.YawDelta, 4);
        // Reset (menu ouvert, perte du focus) : plus rien n'est tenu ni demandé.
        Frame(g, F(1, 500, 600, FingerPhase.Stationary), F(3, 1610, 500, FingerPhase.Stationary), F(4, 2100, 950, FingerPhase.Began));
        Assert.True(g.WasTapped(Guard));
        g.Reset();
        Assert.False(g.StickActive || g.IsHeld(Guard) || g.WasTapped(Guard));
        Assert.Throws<ArgumentOutOfRangeException>(() => g.Update(Array.Empty<FingerPoint>(), Buttons, 0));
        Assert.Throws<ArgumentNullException>(() => g.Update(null!, Buttons, Width));
    }
}
