using System;
using System.Collections.Generic;
using System.Linq;
using EdS.Data.Json;
using EdS.Data.Rebuilds;
using EdS.Data.Saves;
using EdS.Data.Zones;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.World;
using Xunit;
using static EdS.Data.Tests.ScenarioTestData;
using static EdS.Data.Tests.V11TestData;

namespace EdS.Data.Tests;

/// <summary>
/// Lieux reconstruits, sous-lot V2.43.3 (décor selon l'état) : pièces de chaque état (rien en ruine ; échafaudage, tas de bois qui se vide,
/// chariot vide, éclats et outils en chantier ; chariot chargé, caisse et tonneau une fois reconstruit), posées de façon déterministe dans
/// chacun des cinq lieux, hors des obstacles et sans se chevaucher.
/// </summary>
[Trait("Category", "World")]
public class DecorDesChantiersTests
{
    private static readonly Lazy<ZoneDefinition> Vallons = new(() => Zone());
    private static readonly Lazy<RebuildBook> Book5 = new(() =>
        RebuildJsonReader.ReadFile(RebuildJsonReader.PathIn(DataDirectory), Vallons.Value, ItemsCatalog, Book(Vallons.Value)).Book!);

    [Fact]
    public void Pieces_de_chaque_etat()
    {
        Assert.Empty(RebuildDecor.PiecesFor(RebuildPhase.Ruin, 0));
        Assert.Equal(new[] { RebuildDecor.Scaffold, RebuildDecor.WoodFull, RebuildDecor.EmptyCart, RebuildDecor.Chips, RebuildDecor.Tools },
            RebuildDecor.PiecesFor(RebuildPhase.Building, 0));
        Assert.Contains(RebuildDecor.WoodHalf, RebuildDecor.PiecesFor(RebuildPhase.Building, 1));
        Assert.Contains(RebuildDecor.WoodLow, RebuildDecor.PiecesFor(RebuildPhase.Building, 2));
        Assert.Equal(new[] { RebuildDecor.LoadedCart, RebuildDecor.Crate, RebuildDecor.Barrel }, RebuildDecor.PiecesFor(RebuildPhase.Rebuilt, 3));
        Assert.DoesNotContain(RebuildDecor.Scaffold, RebuildDecor.PiecesFor(RebuildPhase.Rebuilt, 3));   // l'échafaudage part
    }

    [Theory]
    [InlineData(RebuildPhase.Building, 0)]
    [InlineData(RebuildPhase.Building, 2)]
    [InlineData(RebuildPhase.Rebuilt, 3)]
    public void Chaque_lieu_a_tout_son_decor_hors_obstacles(RebuildPhase phase, int stage)
    {
        IReadOnlyList<SpaceObstacle> obstacles = Vallons.Value.Setup.Space!.Obstacles;
        foreach (RebuildSite site in Book5.Value.Sites)
        {
            IReadOnlyList<DecorSpot> spots = RebuildDecor.Layout(site, phase, stage, obstacles);
            Assert.Equal(RebuildDecor.PiecesFor(phase, stage), spots.Select(s => s.Piece));   // toutes posées, dans l'ordre
            foreach (DecorSpot s in spots)
            {
                Assert.False(RebuildDecor.Blocked(s.X, s.Z, RebuildDecor.Radius(s.Piece), obstacles), $"{site.Id} {s.Piece}");
                Assert.True(s.X >= site.Place.Min.X.Raw / 1e4 && s.X <= site.Place.Max.X.Raw / 1e4 && s.Z >= site.Place.Min.Z.Raw / 1e4 && s.Z <= site.Place.Max.Z.Raw / 1e4, $"{site.Id} {s.Piece}");
            }
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.True(Math.Sqrt(Math.Pow(spots[i].X - spots[j].X, 2) + Math.Pow(spots[i].Z - spots[j].Z, 2))
                        >= RebuildDecor.Radius(spots[i].Piece) + RebuildDecor.Radius(spots[j].Piece), $"{site.Id} {spots[i].Piece} {spots[j].Piece}");
            // Déterministe : la même pose à chaque fois.
            Assert.Equal(spots, RebuildDecor.Layout(site, phase, stage, obstacles));
        }
    }

    [Fact]
    public void Un_obstacle_ecarte_les_pieces_et_rien_en_ruine()
    {
        RebuildSite hameau = Book5.Value.Of("hameau")!;
        Assert.Empty(RebuildDecor.Layout(hameau, RebuildPhase.Ruin, 0, Vallons.Value.Setup.Space!.Obstacles));
        // Un obstacle qui couvre tout le lieu : aucune place libre, rien n'est posé (jamais dans un mur).
        var wall = new SpaceObstacle("mur", new[]
        {
            new GroundVector(hameau.Place.Min.X, hameau.Place.Min.Z), new GroundVector(hameau.Place.Max.X, hameau.Place.Min.Z),
            new GroundVector(hameau.Place.Max.X, hameau.Place.Max.Z), new GroundVector(hameau.Place.Min.X, hameau.Place.Max.Z),
        });
        Assert.Empty(RebuildDecor.Layout(hameau, RebuildPhase.Building, 0, new[] { wall }));
    }
}
