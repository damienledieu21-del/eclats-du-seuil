using System.Linq;
using EdS.Data.Saves;
using EdS.Domain.Space;
using EdS.Simulation.Engine;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// V2.56.1 : la pâtisserie tenue en main (décision de Damien du 19 novembre 2026, « avec les objets portés du héros ») : celle qu'on mange
/// un moment, sinon la meilleure de ses pièces hors combat, à pied et hors scène ; jamais les copies achetées ni les ingrédients.
/// </summary>
[Trait("Category", "World")]
public class V2561PatisserieEnMainTests
{
    private const string Me = "moi", Other = "autre";
    private const string Couronne = "item.couronne_des_moissons", Tarte = "item.tarte_renversee_aux_pommes", Croissant = "item.croissant_au_beurre";

    private static Inventory Bag(params ItemStack[] stacks)
    {
        var bag = new Inventory(ItemsCatalog);
        foreach (ItemStack s in stacks) Assert.Equal(InventoryRefusal.None, bag.Add(s));
        return bag;
    }

    [Fact]
    public void Rien_sans_piece_de_sa_main()
    {
        Assert.Null(HeldPastries.For(Bag(), Me, null, false, false, false));
        // Copies achetées (sans marque), pièce d'un autre : rien en main ; les ingrédients ne se tiennent jamais.
        Inventory bag = Bag(new ItemStack(Croissant, 3), new ItemStack(Couronne, 1, quality: ItemQuality.Masterwork, signedBy: Other));
        Assert.Null(HeldPastries.For(bag, Me, null, false, false, false));
        Assert.False(HeldPastries.IsHeld("item.levain"));
        Assert.False(HeldPastries.IsHeld("item.pate_feuilletee"));
        Assert.False(HeldPastries.IsHeld("item.pain_inconnu"));
        Assert.False(HeldPastries.IsHeld(null));
        Assert.True(HeldPastries.IsHeld(Croissant));
    }

    [Fact]
    public void La_plus_reussie_de_ses_pieces_puis_l_ordre_de_l_etal()
    {
        Inventory bag = Bag(new ItemStack(Tarte, 1, quality: ItemQuality.Superior, signedBy: Me), new ItemStack(Couronne, 1, signedBy: Me));
        HeldPastry? held = HeldPastries.For(bag, Me, null, false, false, false);
        Assert.NotNull(held);
        Assert.Equal(Tarte, held!.Item);
        Assert.Equal("FIR02_N2", held.Model);   // tarte supérieure : niveau correct
        Assert.False(held.Eating);
        // À égalité de niveau, la couronne passe devant la tarte (ordre de l'étal).
        bag = Bag(new ItemStack(Tarte, 1, quality: ItemQuality.Masterwork, signedBy: Me), new ItemStack(Couronne, 1, quality: ItemQuality.Masterwork, signedBy: Me));
        Assert.Equal(new HeldPastry(Couronne, "FIR01_N3", false), HeldPastries.For(bag, Me, null, false, false, false));
        // Une fouace ratée de sa main : niveau raté.
        Assert.Equal("FIR03_N1", HeldPastries.For(Bag(new ItemStack("item.fouace_des_semailles", 1, signedBy: Me)), Me, null, false, false, false)!.Model);
    }

    [Fact]
    public void Rangee_en_combat_a_cheval_en_scene()
    {
        Inventory bag = Bag(new ItemStack(Couronne, 1, signedBy: Me));
        Assert.Null(HeldPastries.For(bag, Me, null, true, false, false));
        Assert.Null(HeldPastries.For(bag, Me, null, false, true, false));
        Assert.Null(HeldPastries.For(bag, Me, null, false, false, true));
        Assert.Null(HeldPastries.For(bag, null, null, false, false, false));
    }

    [Fact]
    public void Celle_qu_on_mange_passe_devant_meme_en_combat()
    {
        Inventory bag = Bag(new ItemStack(Couronne, 1, signedBy: Me));
        HeldPastry? eating = HeldPastries.For(bag, Me, Croissant, true, true, false);
        Assert.Equal(new HeldPastry(Croissant, "FIR08", true), eating);
        // Une pièce de concours achetée (sans marque) est maîtrisée.
        Assert.Equal("FIR01_N3", HeldPastries.For(Bag(), Me, Couronne, true, false, false)!.Model);
        // Une potion (pas une pâtisserie) ne se tient pas : la pièce portée reste, hors combat.
        Assert.Equal(Couronne, HeldPastries.For(bag, Me, "item.potion_de_soin", false, false, false)!.Item);
        Assert.Null(HeldPastries.For(bag, Me, "item.potion_de_soin", true, false, false));
        // En scène, rien, même en mangeant.
        Assert.Null(HeldPastries.For(bag, Me, Croissant, false, false, true));
    }

    [Fact]
    public void Egalite_et_entree_nulle()
    {
        Assert.Throws<System.ArgumentNullException>(() => HeldPastries.For(null!, Me, null, false, false, false));
        Assert.NotEqual(new HeldPastry(Croissant, "FIR08", true), new HeldPastry(Croissant, "FIR08", false));
        Assert.Equal(new HeldPastry(Croissant, "FIR08", true).GetHashCode(), new HeldPastry(Croissant, "FIR08", true).GetHashCode());
    }

    [Fact]
    public void Repas_compte_seulement_quand_le_moteur_retire_une_charge()
    {
        // Revue Codex de la PR #234 : Eaten et Meals suivent la consommation confirmée par le moteur, pas la commande soumise.
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "eds_v2561_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            AdventureSession a = AdventureSession.Load(ScenarioTestData.DataDirectory, ScenariosDirectory, System.IO.Path.Combine(folder, "sauvegarde.json"));
            a.Advance(0.05);   // nouvelle partie : deux potions emportées (l'ouverture), PV pleins
            const string Potion = "item.potion_de_soin";
            CombatantView Me() => a.World.Zone.Engine.View(a.World.CombatantId);
            Assert.Equal(2, Me().ConsumableCharges[Potion]);
            // PV pleins : la commande part, le moteur refuse ; rien n'est mangé, même après plusieurs appuis.
            for (int i = 0; i < 3; i++)
            {
                Assert.True(a.Drink());
                a.Advance(0.05);
                a.Advance(0.05);
            }
            Assert.Equal((null, 0, 2), (a.Eaten, a.Meals, Me().ConsumableCharges[Potion]));
            // Le loup le plus proche blesse le héros ; la potion bue est comptée une fois.
            for (int i = 0; i < 4000 && Me().Health == Me().MaxHealth; i++)
            {
                GroundVector me = Me().Position!.Value;
                CombatantView? wolf = null;
                foreach (CombatantView v in a.World.Zone.Engine.CombatantIds.Select(a.World.Zone.Engine.View))
                    if (v.Id != a.World.CombatantId && v.IsPresent && !v.IsDead && v.Id.Contains("loup") && v.Position is GroundVector p
                        && (wolf is null || Dist2(me, p) < Dist2(me, wolf.Position!.Value))) wolf = v;
                Assert.NotNull(wolf);
                GroundVector w = wolf!.Position!.Value;
                a.Combat.SetMove((w.X.Raw - me.X.Raw) / 1e4, (w.Z.Raw - me.Z.Raw) / 1e4);
                a.Advance(0.05);
            }
            a.Combat.SetMove(0, 0);
            Assert.True(Me().Health < Me().MaxHealth);
            Assert.True(a.Drink());
            a.Advance(0.05);
            a.Advance(0.05);
            Assert.Equal((Potion, 1, 1), (a.Eaten, a.Meals, Me().ConsumableCharges[Potion]));
            // Aussitôt après : recharge (ou PV pleins) : refusé, le repas n'est pas prolongé.
            Assert.True(a.Drink());
            a.Advance(0.05);
            a.Advance(0.05);
            Assert.Equal((Potion, 1, 1), (a.Eaten, a.Meals, Me().ConsumableCharges[Potion]));
        }
        finally
        {
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
        }
    }

    private static long Dist2(GroundVector a, GroundVector b)
    {
        long dx = b.X.Raw - a.X.Raw, dz = b.Z.Raw - a.Z.Raw;
        return dx * dx + dz * dz;
    }
}
