using System;
using System.IO;
using System.Linq;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Maison du joueur, sous-lot V2.53.3 (décisions de Damien du 20 novembre 2026) : N est la maison (H reste l'aide), R tourne le meuble en
/// mode décoration (l'ultime en combat, commandes de combat suspendues pendant la pose), on quitte l'aventure par le menu de la pause ;
/// une touche n'a qu'un usage par contexte ; au doigt, le bouton « Maison », et en mode décoration Tourner, Annuler et un toucher pour poser.
/// </summary>
[Trait("Category", "World")]
public class MaisonTouchesTests
{
    private static string Source(string relative) => File.ReadAllText(Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", relative));

    [Fact]
    public void Une_touche_un_seul_usage_par_contexte()
    {
        string game = Source("EdsGame.cs");
        foreach (KeyContext context in Enum.GetValues(typeof(KeyContext)))
        {
            var keys = KeyBindings.In(context);
            Assert.NotEmpty(keys);
            var twice = keys.GroupBy(k => k.Key).Where(g => g.Count() > 1).Select(g => g.Key + " : " + string.Join(", ", g.Select(k => k.Use))).ToList();
            Assert.True(twice.Count == 0, context + " : " + string.Join(" ; ", twice));
        }
        // Chaque touche de la table est bien lue par le jeu.
        foreach (string key in KeyBindings.Keys) Assert.Contains("KeyCode." + key, game, StringComparison.Ordinal);
        Assert.Contains(KeyBindings.In(KeyContext.Exploration), k => k.Key == "N" && k.Use == "maison");
        Assert.Contains(KeyBindings.In(KeyContext.Exploration), k => k.Key == "H" && k.Use == "aide");
        Assert.Contains(KeyBindings.In(KeyContext.Exploration), k => k.Key == "R" && k.Use == "ultime");
        Assert.Contains(KeyBindings.In(KeyContext.Decoration), k => k.Key == "R" && k.Use == "tourner le meuble");
    }

    [Fact]
    public void N_maison_H_aide_quitter_par_la_pause_et_combat_suspendu_pendant_la_pose()
    {
        string game = Source("EdsGame.cs");
        Assert.Contains("(Input.GetKeyDown(KeyCode.N) || _touch.HomePressed) && !talking", game, StringComparison.Ordinal);
        Assert.DoesNotContain("Input.GetKeyDown(KeyCode.N) || _touch.NextScenePressed", game, StringComparison.Ordinal);   // N ne quitte plus l'aventure
        Assert.Contains("Input.GetKeyDown(KeyCode.H) || _touch.HelpPressed", game, StringComparison.Ordinal);   // H reste l'aide
        Assert.Contains("if (_homeCarry == null) ReadCombatInput();", game, StringComparison.Ordinal);   // R ne lance pas l'ultime pendant la pose
        Assert.Contains("_hud.OnQuitAdventure(", game, StringComparison.Ordinal);
        Assert.Contains("_quitAdventure?.Invoke()", Source("HudView.cs"), StringComparison.Ordinal);
        Assert.Contains(HelpKeys.Adventure("F parler · N maison (chez soi, ou rentrer depuis un point sûr)"), k => k.Key == "N");
        Assert.Contains("N maison", V11TestData.Texts.Get("text.ui.help"), StringComparison.Ordinal);
        Assert.Equal("Quitter l'aventure", V11TestData.Texts.Get("text.ui.hud.settings.quit_adventure"));
    }

    [Fact]
    public void Commandes_tactiles_de_la_maison()
    {
        // Le bouton « Maison » remplace « Scène » dans l'aventure (quitter passe par le menu) ; l'entraînement garde « Scène ».
        var layout = new HudLayoutV21();
        layout.Update(HudLayoutV21.PhoneWidth, HudLayoutV21.PhoneHeight, UiInsets.None, true, HudMode.Adventure);
        Assert.True(layout.Has(HudButton.Home));
        Assert.False(layout.Has(HudButton.NextScene));
        layout.Update(HudLayoutV21.PhoneWidth, HudLayoutV21.PhoneHeight, UiInsets.None, true, HudMode.Training);
        Assert.False(layout.Has(HudButton.Home));
        Assert.True(layout.Has(HudButton.NextScene));
        Assert.Equal("text.ui.hud.btn.home", HudTextsV21.Button(HudButton.Home, HudMode.Adventure));
        // Mode décoration au doigt : Tourner et Annuler, un toucher (Began seulement) sur le sol pour poser, sans tourner la vue.
        string touch = Source("TouchControls.cs");
        Assert.Contains("f.Phase != FingerPhase.Began", touch, StringComparison.Ordinal);
        Assert.Contains("HomeFloorZone", touch, StringComparison.Ordinal);
        string game = Source("EdsGame.cs");
        Assert.Contains("_touch.HomeTurnPressed", game, StringComparison.Ordinal);
        Assert.Contains("_touch.HomeCancelPressed", game, StringComparison.Ordinal);
        Assert.Contains("_touch.HomeTap", game, StringComparison.Ordinal);
    }
}
