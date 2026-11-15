using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Pêche et carnet, relecture indépendante de la PR #181 : dans l'aventure, la même touche lance et ferre au poste ; loin du poste, la
/// ligne est ramenée (message) et la touche sert au reste.
/// </summary>
[Trait("Category", "World")]
public class PecheAventureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "eds_peche_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private static GroundVector Position(AdventureSession a) => a.World.Zone.Engine.View(AdventureSession.PlayerId).Position!.Value;

    private static bool WalkTo(AdventureSession a, double x, double z, double within)
    {
        for (int i = 0; i < 2000; i++)
        {
            GroundVector p = Position(a);
            double dx = x - p.X.Raw / 1e4, dz = z - p.Z.Raw / 1e4;
            if (Math.Sqrt(dx * dx + dz * dz) <= within)
            {
                a.Combat.SetMove(0, 0);
                a.Advance(0.05);
                return true;
            }
            a.Combat.SetMove(dx, dz);
            a.Advance(0.05);
        }
        return false;
    }

    [Fact]
    public void Au_poste_la_touche_lance_loin_du_poste_elle_ramene_la_ligne()
    {
        AdventureSession a = AdventureSession.Load(DataDirectory, Path.Combine(AppContext.BaseDirectory, "scenarios"), Path.Combine(_folder, "partie.json"));
        a.Advance(0.05);
        a.DrainMessages();
        Assert.True(WalkTo(a, -9, -54, 1.5));   // le ponton de l'ouest (au gué, les herbes de la rivière passent avant)
        Assert.Equal("ponton_ouest", a.FishingSpotInReach?.Id);
        bool done = a.Interact();
        IReadOnlyList<string> said = a.DrainMessages();
        Assert.False(done, string.Join(" | ", said));   // sans appât : refusé, avec son message
        Assert.Null(a.World.CastSpot);
        Assert.Contains(said, m => m.Contains("appât", StringComparison.Ordinal));
        a.World.Inventory.Add("item.ver_de_terre", 3);
        Assert.True(a.Interact());
        Assert.NotNull(a.World.CastSpot);
        Assert.Equal(2, a.World.Inventory.Count("item.ver_de_terre"));
        a.DrainMessages();
        // Le personnage s'éloigne, ligne à l'eau : la touche la ramène, avec un message.
        Assert.True(WalkTo(a, -9, -42, 1.5));
        Assert.False(a.World.CastInReach);
        a.Interact();
        Assert.Null(a.World.CastSpot);
        Assert.Contains("Tu ramènes ta ligne.", a.DrainMessages());
        Assert.Equal(1L, a.World.Fishing.Casts);
        Assert.False(a.CancelLine());   // plus de ligne à l'eau
    }
}
