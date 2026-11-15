using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Json;
using EdS.Domain.Combat;
using EdS.Runtime;
using Xunit;

namespace EdS.Data.Tests;

/// <summary>
/// Compagnons, interface (décisions de Damien du 14 novembre 2026 ; dossier p. 35 : cinq ordres au plus, icône et mot, réponse visible) :
/// panneau du compagnon et rangée des six cases dans le HUD V2.1 (PC et téléphone, toutes les tailles visées), commandes au clavier (O,
/// 1 à 5, Y) et au doigt (<see cref="CompanionControls"/>), et ordres envoyés au moteur par le pont temps réel.
/// </summary>
[Trait("Category", "Companion")]
[Trait("Category", "Runtime")]
public class CompagnonsInterfaceTests
{
    private static readonly float[] PhoneWidths = { 1080f * 2960f / 1440f, 1920f, 2400f, 1440f };
    private static readonly float[] PcWidths = { 1920f, 1728f, 2520f, 1440f };
    private static readonly HudMode[] Modes = { HudMode.Training, HudMode.Network, HudMode.Adventure };

    private static IEnumerable<(string Where, HudLayoutV21 L)> Layouts()
    {
        foreach (bool phone in new[] { true, false })
            foreach (float w in phone ? PhoneWidths : PcWidths)
                foreach (HudMode mode in Modes)
                    foreach (UiInsets safe in phone ? new[] { UiInsets.None, new UiInsets(90f, 20f, 60f, 40f) } : new[] { UiInsets.None })   // l'encoche : au téléphone
                        foreach (bool dodge in new[] { false, true })
                        {
                            var l = new HudLayoutV21();
                            l.Update(w, 1080f, safe, phone, mode, dodge, companion: true);
                            yield return ($"{(phone ? "téléphone" : "PC")} {w}, {mode}, encoche {safe.Left}, esquive {dodge}", l);
                        }
    }

    [Fact]
    public void Panneau_et_six_cases_tiennent_sans_rien_recouvrir()
    {
        foreach (var (where, l) in Layouts())
        {
            Assert.True(l.CompanionShown, where);
            Assert.False(l.Overflow, where);
            UiBox panel = l[HudPart.Companion];
            Assert.True(panel.Inside(l.Safe), $"{where} : panneau {panel}");
            float min = (l.Phone ? HudLayoutV21.TouchMin * l.Unit : 56f) - 0.01f;
            for (int i = 0; i < 6; i++)
            {
                UiBox a = l.CompanionAction(i);
                Assert.True(a.W >= min && a.H >= min, $"{where} : case {i} {a} trop petite");
                Assert.True(a.Inside(panel), $"{where} : case {i} hors du panneau");
                for (int k = i + 1; k < 6; k++) Assert.False(a.Overlaps(l.CompanionAction(k)), $"{where} : cases {i} et {k}");
                foreach (HudButton b in Enum.GetValues<HudButton>().Where(l.Has).Where(b => !l.InDrawer(b)))
                    Assert.False(a.Overlaps(l.Button(b)), $"{where} : case {i} sur {b}");
                Assert.False(a.Overlaps(l[HudPart.Joystick]), $"{where} : case {i} sur le joystick");
            }
            foreach (HudPart p in new[] { HudPart.Vitals, HudPart.Effects, HudPart.Target, HudPart.Compass, HudPart.Announce, HudPart.WorldState, HudPart.Toasts,
                HudPart.Damage, HudPart.MiniMap, HudPart.Quests, HudPart.Group, HudPart.Prompt })
                Assert.False(panel.Overlaps(l[p]), $"{where} : panneau sur {p} {l[p]}");
        }
    }

    [Fact]
    public void Sans_compagnon_le_HUD_ne_change_pas()
    {
        foreach (bool phone in new[] { true, false })
        {
            var before = new HudLayoutV21();
            before.Update(1920f, 1080f, UiInsets.None, phone, HudMode.Adventure, true);
            Assert.False(before.CompanionShown);
            Assert.True(before[HudPart.Companion].IsEmpty);
            Assert.True(before.CompanionAction(0).IsEmpty);
            var with = new HudLayoutV21();
            with.Update(1920f, 1080f, UiInsets.None, phone, HudMode.Adventure, true, companion: true);
            foreach (HudButton b in Enum.GetValues<HudButton>()) Assert.Equal(before.Button(b).ToString(), with.Button(b).ToString());
        }
    }

    // --- Commandes ------------------------------------------------------------------------------------------------------

    private sealed class FakeClient : ICompanionCommands
    {
        public readonly List<string> Sent = new();
        public IReadOnlyList<CompanionState> List = Array.Empty<CompanionState>();
        public IReadOnlyList<CompanionState> Companions() => List;
        public bool Order(string companion, CompanionOrder order) { Sent.Add($"{companion}:{order}"); return true; }
        public bool Duo(string companion) { Sent.Add($"{companion}:duo"); return true; }
    }

    [Fact]
    public void Clavier_et_doigt_menu_des_ordres_et_attaque_a_deux()
    {
        Assert.Equal(6, CompanionControls.Labels.Count);
        var controls = new CompanionControls();
        Assert.Null(controls.DigitAction(1));   // menu fermé : les chiffres restent aux compétences
        controls.ToggleMenu();
        Assert.Equal(new CompanionAction?[] { CompanionAction.Follow, CompanionAction.Wait, CompanionAction.Target, CompanionAction.Protect, CompanionAction.Return, null },
            new[] { 1, 2, 3, 4, 5, 6 }.Select(controls.DigitAction));
        Assert.False(controls.Perform(new FakeClient(), CompanionAction.Wait));   // aucun compagnon
        Assert.False(controls.Perform(null, CompanionAction.Wait));
    }

    [Fact]
    public void Textes_du_HUD_dans_les_donnees_et_branchement_dans_Unity()
    {
        var texts = StoryJsonReader.ReadTextsFile(StoryJsonReader.TextsPathIn(ScenarioTestData.DataDirectory)).Texts!;
        foreach (string key in HudTextsV21.CompanionActions.Append(HudTextsV21.CompanionDown).Append(HudTextsV21.CompanionDuoReady))
            Assert.True(texts.Has(key), key);
        string game = System.IO.File.ReadAllText(System.IO.Path.Combine(Lot93UnityPackageTests.Root, "unity", "Assets", "EdS", "Game", "EdsGame.cs"));
        Assert.Contains("companion: _combat is ICompanionCommands", game);
        Assert.Contains("Input.GetKeyDown(KeyCode.O)", game);
        Assert.Contains("_hud.Companions(", game);
        Assert.Contains(HelpKeys.Training, k => k.Key == "Y" && k.Action == "Attaque à deux");
    }

    [Fact]
    public void Le_pont_temps_reel_envoie_ordres_et_attaque_a_deux_au_moteur()
    {
        var combat = new RealtimeCombat(ScenarioJsonReader.ReadFile(ScenarioTestData.ScenarioPath("espace_compagnon"), ScenarioTestData.ShippedData).Setup!, "bastion");
        var controls = new CompanionControls();
        CompanionState nyx = Assert.Single(combat.Companions());
        Assert.Equal(("nyx", CompanionOrder.Follow, 0.7), (nyx.Id, nyx.Order, nyx.Bond));
        Assert.False(nyx.DuoReady);
        controls.ToggleMenu();
        Assert.True(controls.Perform(combat, CompanionAction.Wait));
        Assert.False(controls.MenuOpen);   // un ordre ferme le menu
        Assert.False(controls.Perform(combat, CompanionAction.Duo));   // jauge pas pleine : rien ne part
        combat.Advance(0.1);
        Assert.Equal(CompanionOrder.Wait, combat.Companions()[0].Order);
        // « Cibler » vise la cible sélectionnée.
        combat.SelectTarget("loup_b");
        Assert.True(controls.Perform(combat, CompanionAction.Target));
        combat.Advance(0.1);
        Assert.Equal(("loup_b", CompanionOrder.Target), (combat.Companions()[0].View.CompanionOrderTarget, combat.Companions()[0].Order));
        // Un compagnon inconnu : refusé sans commande.
        Assert.False(combat.Order("loup_a", CompanionOrder.Follow));
    }
}
