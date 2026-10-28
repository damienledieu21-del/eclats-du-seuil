using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 5 (demandes de Damien du 27 octobre 2026 au matin) : formes faites par le code pour l'abri à poteaux, le muret de
/// pierres sèches et le pont cintré. Sans Unity : faces tournées vers l'extérieur, emprises respectées, mêmes formes à chaque lancement.
/// </summary>
[Trait("Category", "Runtime")]
public class StyleShapesTests
{
    private static StyleVector Normal(StyleMesh mesh, int triangle)
    {
        StyleVector a = mesh.Vertices[mesh.Triangles[3 * triangle]], b = mesh.Vertices[mesh.Triangles[3 * triangle + 1]], c = mesh.Vertices[mesh.Triangles[3 * triangle + 2]];
        return StyleVector.Cross(b - a, c - a);
    }

    private static StyleVector Centroid(StyleMesh mesh, int triangle) =>
        (mesh.Vertices[mesh.Triangles[3 * triangle]] + mesh.Vertices[mesh.Triangles[3 * triangle + 1]] + mesh.Vertices[mesh.Triangles[3 * triangle + 2]]) * (1.0 / 3);

    private static void WellFormed(StyleMesh mesh)
    {
        Assert.Equal(mesh.Vertices.Count, mesh.Uvs.Count);
        Assert.Equal(0, mesh.Triangles.Count % 3);
        Assert.All(mesh.Triangles, i => Assert.InRange(i, 0, mesh.Vertices.Count - 1));
        Assert.All(mesh.Vertices, v => Assert.True(double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z)));
        Assert.All(mesh.Uvs, uv => Assert.True(double.IsFinite(uv.U) && double.IsFinite(uv.V)));
    }

    [Fact]
    public void Pave_et_poutre_tournes_vers_l_exterieur()
    {
        var mesh = new StyleMesh();
        var center = new StyleVector(1, 2, 3);
        StyleShapes.Box(mesh, center, new StyleVector(0.5, 0, 0), new StyleVector(0, 0.25, 0), new StyleVector(0, 0, 1), 2);
        WellFormed(mesh);
        Assert.Equal(36, mesh.Vertices.Count);
        Assert.Equal(12, mesh.Triangles.Count / 3);
        for (int t = 0; t < 12; t++) Assert.True(StyleVector.Dot(Normal(mesh, t), Centroid(mesh, t) - center) > 0);
        (StyleVector min, StyleVector max) = mesh.Bounds();
        Assert.Equal(0.5, min.X, 9);
        Assert.Equal(1.75, min.Y, 9);
        Assert.Equal(4, max.Z, 9);

        // Texture à l'échelle du monde : sur la face du dessus, 1 m vaut une demi-répétition (tuile de 2 m).
        double spanU = mesh.Uvs.Max(uv => uv.U) - mesh.Uvs.Min(uv => uv.U);
        Assert.Equal(1, spanU, 9);

        // Poutre couchée en biais et poteau debout : longueur, section, faces vers l'extérieur.
        foreach ((StyleVector a, StyleVector b) in new[] { (new StyleVector(0, 1, 0), new StyleVector(3, 1.5, 4)), (new StyleVector(2, 0, 2), new StyleVector(2, 2.5, 2)) })
        {
            var beam = new StyleMesh();
            StyleShapes.Beam(beam, a, b, 0.2, 0.3, 1.5);
            WellFormed(beam);
            StyleVector mid = (a + b) * 0.5;
            for (int t = 0; t < 12; t++) Assert.True(StyleVector.Dot(Normal(beam, t), Centroid(beam, t) - mid) > 0);
        }
        var upright = new StyleMesh();
        StyleShapes.Beam(upright, new StyleVector(2, 0, 2), new StyleVector(2, 2.5, 2), 0.2, 0.3, 1.5);
        Assert.Equal(2.5, upright.Bounds().Max.Y - upright.Bounds().Min.Y, 9);
        var nothing = new StyleMesh();
        StyleShapes.Beam(nothing, new StyleVector(1, 1, 1), new StyleVector(1, 1, 1), 0.2, 0.2, 1);
        Assert.Empty(nothing.Vertices);
        Assert.Throws<ArgumentNullException>(() => StyleShapes.Box(null!, center, center, center, center, 1));
    }

    [Fact]
    public void Rondin_lisse_et_ferme()
    {
        var mesh = new StyleMesh();
        var a = new StyleVector(0, 1, 0);
        var b = new StyleVector(2, 1.2, 0);
        StyleShapes.Log(mesh, a, b, 0.06, 8, 1);
        WellFormed(mesh);
        StyleVector axis = (b - a).Normalized;
        for (int t = 0; t < mesh.Triangles.Count / 3; t++)
        {
            StyleVector c = Centroid(mesh, t), n = Normal(mesh, t);
            double along = StyleVector.Dot(c - a, axis);
            StyleVector radial = c - (a + axis * along);
            // Flancs : vers l'extérieur du rondin ; bouts : vers l'avant ou l'arrière.
            double outward = along < 1e-6 ? -StyleVector.Dot(n, axis) : along > (b - a).Length - 1e-6 ? StyleVector.Dot(n, axis) : StyleVector.Dot(n, radial);
            Assert.True(outward > 0);
        }
        Assert.InRange(mesh.Bounds().Max.Y, 1.2 + 0.05, 1.2 + 0.07);
    }

    [Fact]
    public void Muret_de_pierres_seches_dans_son_emprise()
    {
        // Haie ouest des Vallons : 18 m sur 1 m.
        StyleMesh wall = StyleShapes.DryStoneWall(-24, 19, -6, 20, "haie_ouest", 1.5);
        WellFormed(wall);
        (StyleVector min, StyleVector max) = wall.Bounds();
        Assert.InRange(min.X, -24.06, -23.95);
        Assert.InRange(max.X, -6.05, -5.94);
        Assert.InRange(min.Z, 19.0, 19.2);
        Assert.InRange(max.Z, 19.8, 20.0);
        Assert.InRange(min.Y, -0.12, -0.03);                 // pied un peu enterré
        Assert.InRange(max.Y, 1.0, 1.25);                    // corps de 0,85 m et pierres de chant
        int stones = wall.Vertices.Count / 24;
        Assert.InRange(stones, 300, 1000);
        Assert.True(wall.Vertices.Count < 65000);            // un seul maillage à index 16 bits
        // Plus large en bas qu'en haut (fruit du muret).
        double Width(double y0, double y1)
        {
            var at = wall.Vertices.Where(v => v.Y > y0 && v.Y < y1).ToList();
            return at.Max(v => v.Z) - at.Min(v => v.Z);
        }
        Assert.True(Width(-0.15, 0.15) > Width(0.55, 0.85) + 0.1);
        // Mêmes pierres à chaque lancement ; un autre muret a les siennes.
        StyleMesh again = StyleShapes.DryStoneWall(-24, 19, -6, 20, "haie_ouest", 1.5);
        Assert.Equal(wall.Vertices, again.Vertices);
        Assert.NotEqual(wall.Vertices.Take(48), StyleShapes.DryStoneWall(-24, 19, -6, 20, "haie_est", 1.5).Vertices.Take(48));
        // Muret le long de z ; emprise trop petite : rien.
        (StyleVector zMin, StyleVector zMax) = StyleShapes.DryStoneWall(3, -4, 4, 4, "muret", 1.5).Bounds();
        Assert.True(zMax.Z - zMin.Z > 7.9 && zMax.X - zMin.X < 1.05);
        Assert.Empty(StyleShapes.DryStoneWall(0, 0, 0.1, 0.05, "rien", 1.5).Vertices);
    }

    [Fact]
    public void Pont_cintre_en_bois()
    {
        // Pont des Vallons : la rivière (x 4 à 7) franchie de 3,4 à 7,6, entre ses deux bras (z -2 à 1,5).
        Assert.Equal(0.04, StyleShapes.DeckTop(3.4, 3.4, 7.6, 0.35), 9);
        Assert.Equal(0.39, StyleShapes.DeckTop(5.5, 3.4, 7.6, 0.35), 9);
        Assert.Equal(0.04, StyleShapes.DeckTop(7.6, 3.4, 7.6, 0.35), 9);
        Assert.Equal(0, StyleShapes.DeckTop(8, 3.4, 7.6, 0.35));
        Assert.Equal(0, StyleShapes.DeckTop(5, 7, 3, 0.35));
        Assert.Equal(0.39, StyleShapes.StandHeight(5.5, 0, 3.4, -2, 7.6, 1.5, 0.35), 9);
        Assert.Equal(0, StyleShapes.StandHeight(5.5, 2, 3.4, -2, 7.6, 1.5, 0.35));
        Assert.True(StyleShapes.DeckTop(4.5, 3.4, 7.6, 0.35) < StyleShapes.DeckTop(5, 3.4, 7.6, 0.35));

        StyleBridgeShapes bridge = StyleShapes.Bridge(3.4, -2, 7.6, 1.5, 0.35, "pont", 2.5, 1.5, 1.5);
        foreach (StyleMesh part in new[] { bridge.Wood, bridge.Bark, bridge.Stone })
        {
            WellFormed(part);
            Assert.NotEmpty(part.Triangles);
        }
        (StyleVector min, StyleVector max) = bridge.Wood.Bounds();
        Assert.InRange(min.X, 3.0, 3.2);                     // longerons prolongés dans les rives
        Assert.InRange(max.X, 7.8, 8.0);
        Assert.InRange(min.Z, -2.05, -1.9);
        Assert.InRange(max.Z, 1.4, 1.55);
        // Planches : leur dessus suit le cintre (le plus haut au milieu, sous les poteaux).
        double deck = bridge.Wood.Vertices.Where(v => Math.Abs(v.X - 5.5) < 0.15).Max(v => v.Y);
        Assert.InRange(deck, 0.37, 0.41);
        // Rambardes à hauteur d'appui au-dessus du tablier, sur les deux bords.
        (StyleVector railMin, StyleVector railMax) = bridge.Bark.Bounds();
        Assert.InRange(railMax.Y, 0.39 + 0.95, 0.39 + 1.05);
        Assert.True(railMin.Z < -1.85 && railMax.Z > 1.35);
        // Culées : du lit (1 m sous le sol) jusqu'au niveau du sol.
        (StyleVector stoneMin, StyleVector stoneMax) = bridge.Stone.Bounds();
        Assert.InRange(stoneMin.Y, -1.15, -1.0);
        Assert.InRange(stoneMax.Y, -0.05, 0.1);
        Assert.Equal(bridge.Wood.Vertices, StyleShapes.Bridge(3.4, -2, 7.6, 1.5, 0.35, "pont", 2.5, 1.5, 1.5).Wood.Vertices);
        Assert.Empty(StyleShapes.Bridge(3, 0, 3.2, 2, 0.35, "trop court", 2.5, 1.5, 1.5).Wood.Vertices);
    }

    [Fact]
    public void Abri_a_poteaux_et_toit_de_tuiles()
    {
        // Les quatre piliers des Vallons (0,5 m de côté, centres à x -16 et -12, z 8 et 12).
        var posts = new List<(double X, double Z)> { (-16, 8), (-12, 8), (-16, 12), (-12, 12) };
        StyleShelterShapes shelter = StyleShapes.Shelter(posts, "abri", 2.5, 2, 1.5);
        foreach (StyleMesh part in new[] { shelter.Frame, shelter.Stone, shelter.RoofTiles, shelter.RoofWood })
        {
            WellFormed(part);
            Assert.NotEmpty(part.Triangles);
        }
        // Le toit déborde des poteaux tout autour ; dessous à 2,55 m aux égouts, faîtage vers 4 m.
        Assert.True(shelter.X0 < -16.4 && shelter.X1 > -11.6 && shelter.Z0 < 7.4 && shelter.Z1 > 12.6);
        Assert.InRange(shelter.RoofBottom, 2.9, 3.0);
        Assert.InRange(shelter.RoofTop, 4.0, 4.4);
        (StyleVector min, StyleVector max) = shelter.RoofTiles.Bounds();
        Assert.InRange(min.Y, 2.4, 2.75);
        Assert.InRange(max.Y, 4.0, 4.4);
        // Tuiles : faces du dessus tournées vers le ciel (sauf la faîtière ronde), rangées le long du faîtage (v monte vers le faîtage).
        double up = 0, all = 0;
        for (int t = 0; t < shelter.RoofTiles.Triangles.Count / 3; t++)
        {
            StyleVector n = Normal(shelter.RoofTiles, t);
            all += n.Length;
            if (n.Y > 0) up += n.Length;
        }
        Assert.True(up > 0.8 * all);
        // Les deux pans viennent en premier (deux triangles chacun) : leur sommet le plus bas est à l'égout, le plus haut au faîtage.
        var slopes = shelter.RoofTiles.Vertices.Take(12).Select((v, i) => (v, i)).ToList();
        int low = slopes.OrderBy(p => p.v.Y).First().i, high = slopes.OrderBy(p => p.v.Y).Last().i;
        Assert.True(shelter.RoofTiles.Vertices[high].Y - shelter.RoofTiles.Vertices[low].Y > 1.5);
        Assert.True(shelter.RoofTiles.Uvs[high].V > shelter.RoofTiles.Uvs[low].V);
        // Poteaux sur leurs socles, du sol aux sablières.
        (StyleVector frameMin, StyleVector frameMax) = shelter.Frame.Bounds();
        Assert.InRange(frameMin.Y, 0.0, 0.2);
        Assert.InRange(frameMax.Y, 2.9, 3.0);
        Assert.InRange(shelter.Stone.Bounds().Max.Y, 0.1, 0.2);
        // Le faîtage suit le plus grand côté.
        StyleShelterShapes deep = StyleShapes.Shelter(new List<(double X, double Z)> { (0, 0), (3, 0), (0, 6), (3, 6) }, "long", 2.5, 2, 1.5);
        (StyleVector deepMin, StyleVector deepMax) = deep.RoofTiles.Bounds();
        double ridgeX = deep.RoofTiles.Vertices.Where(v => v.Y > deepMax.Y - 0.25).Average(v => v.X);
        Assert.InRange(ridgeX, 1.3, 1.7);
        Assert.True(deepMax.Z - deepMin.Z > 6.9);
        Assert.Empty(StyleShapes.Shelter(new List<(double X, double Z)> { (0, 0) }, "seul", 2.5, 2, 1.5).Frame.Vertices);
    }

    [Fact]
    public void Le_jeu_pose_l_abri_le_muret_le_pont_la_carriere_et_l_herbe_fleurie()
    {
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Lot93UnityPackageTests.Root }.Concat(parts).ToArray()));
        string view = Read("unity", "Assets", "EdS", "Game", "SpaceView.cs");
        string arena = Read("unity", "Assets", "EdS", "Game", "ArenaView.cs");
        string world = Read("unity", "Assets", "EdS", "Game", "StyleWorld.cs");
        string grass = Read("unity", "Assets", "EdS", "Game", "StyleGrass.cs");
        string shader = Read("unity", "Assets", "EdS", "Game", "Herbe.shader");
        string editor = Read("unity", "Assets", "EdS", "Editor", "EdsStyle.cs");
        // Abri : charpente et toit faits par le code sur les piliers ; le toit et ce qui est dessous sont retirés quand on y entre.
        Assert.Contains("StyleShelterShapes shelter = StyleShapes.Shelter(posts, \"abri\", woodTile, roofTile, stoneTile);", view, StringComparison.Ordinal);
        Assert.Contains("if (id.StartsWith(\"pilier\")) return;", view, StringComparison.Ordinal);
        Assert.Contains("AddOccluder(roof.transform, false, min, max);", view, StringComparison.Ordinal);
        Assert.Contains("AddOccluder(under.transform, false, min, max);", view, StringComparison.Ordinal);
        Assert.Contains("var min = new Vector3((float)shelter.X0, 3f, (float)shelter.Z0);", view, StringComparison.Ordinal);
        // Muret de pierres sèches à la place des haies, front de taille en rochers.
        Assert.Contains("StyleMesh wall = StyleShapes.DryStoneWall(x0, z0, x1, z1, id, tile);", view, StringComparison.Ordinal);
        Assert.Contains("if (id.StartsWith(\"front_de_taille\") && QuarryFace(id, points)) return;", view, StringComparison.Ordinal);
        // Pont cintré : les personnages montent avec le tablier, et redescendent hors du pont.
        Assert.Contains("StyleWorld.Deck(x0, z0, x1, z1, BridgeRise);", view, StringComparison.Ordinal);
        Assert.Contains("StyleWorld.Deck(0f, 0f, 0f, 0f, 0f);", view, StringComparison.Ordinal);
        Assert.Contains("Vector3 place = shown[v.Id] + Vector3.up * (1f + StyleWorld.StandHeight(shown[v.Id].x, shown[v.Id].z));", arena, StringComparison.Ordinal);
        Assert.Contains("1f + StyleWorld.StandHeight((float)predicted.Value.X, (float)predicted.Value.Z)", arena, StringComparison.Ordinal);
        Assert.Contains("(float)StyleShapes.StandHeight(x, z, _deckX0, _deckZ0, _deckX1, _deckZ1, _deckRise)", world, StringComparison.Ordinal);
        // Revue Codex de la PR #60 : les annonces d'attaque qui touchent le pont suivent son tablier.
        Assert.Contains("vertices.Select(v => new StyleVector(v.x, v.y, v.z)).ToList(), triangles, 0.3, 6, StyleWorld.Deck());", view, StringComparison.Ordinal);
        Assert.Contains("(float)v.Y + StyleWorld.StandHeight((float)v.X, (float)v.Z)", view, StringComparison.Ordinal);
        // Formes faites par le code : tangentes pour le relief, index 32 bits au besoin.
        Assert.Contains("if (shape.Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;", view, StringComparison.Ordinal);
        Assert.Contains("mesh.RecalculateTangents();", view, StringComparison.Ordinal);
        // Herbe : touffes fleuries, abaissement au loin, réglages par défaut repris une fois.
        Assert.Contains("Graphics.RenderMeshInstanced(parameters, cell.Flowers ? _flowers : _mesh, 0, cell.Items);", grass, StringComparison.Ordinal);
        Assert.Contains("material.SetFloat(\"_FadeEnd\", distance);", grass, StringComparison.Ordinal);
        Assert.Contains("positionWS = pivot + (positionWS - pivot) * float3(1.0, fade, 1.0);", shader, StringComparison.Ordinal);
        Assert.Contains("if (input.uv.y > 1.5)", shader, StringComparison.Ordinal);
        Assert.Contains("material.CopyPropertiesFromMaterial(new Material(shader));", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Annonce_redecoupee_pour_suivre_le_pont()
    {
        // Revue Codex de la PR #60 : une annonce qui touche le pont est redécoupée pour suivre son tablier sommet par sommet.
        var vertices = new List<StyleVector> { new StyleVector(0, 0, 0), new StyleVector(0, 0, 4), new StyleVector(6, 0, 0) };
        var triangles = new List<int> { 0, 1, 2 };
        double Area(IReadOnlyList<StyleVector> v, IReadOnlyList<int> t)
        {
            double sum = 0;
            for (int i = 0; i < t.Count; i += 3) sum += StyleVector.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).Y / 2;
            return sum;
        }
        (List<StyleVector> fine, List<int> fineTriangles) = StyleShapes.Subdivide(vertices, triangles, 0.3, 6, (-1, -1, 7, 5));
        Assert.True(fineTriangles.Count / 3 > 100);
        Assert.Equal(Area(vertices, triangles), Area(fine, fineTriangles), 9);   // même surface, même côté visible
        for (int i = 0; i < fineTriangles.Count; i += 3)
        {
            StyleVector a = fine[fineTriangles[i]], b = fine[fineTriangles[i + 1]], c = fine[fineTriangles[i + 2]];
            Assert.True(StyleVector.Cross(b - a, c - a).Y > 0);   // même sens que le triangle d'origine (normale vers le haut)
            Assert.True((a - b).Length <= 0.3 + 1e-9 && (b - c).Length <= 0.3 + 1e-9 && (c - a).Length <= 0.3 + 1e-9);
        }
        // Loin du pont : rien n'est redécoupé ; sans zone : tout l'est ; profondeur bornée.
        Assert.Single(Enumerable.Range(0, StyleShapes.Subdivide(vertices, triangles, 0.3, 6, (20, 20, 25, 25)).Triangles.Count / 3));
        Assert.Equal(4 * 4, StyleShapes.Subdivide(vertices, triangles, 0.01, 2).Triangles.Count / 3);
        Assert.Empty(StyleShapes.Subdivide(null!, triangles, 0.3).Vertices);
        // Triangle à cheval sur le bord du pont : seuls les morceaux qui le touchent sont fins.
        (List<StyleVector> half, List<int> halfTriangles) = StyleShapes.Subdivide(vertices, triangles, 0.3, 6, (-1, -1, 1, 5));
        Assert.True(halfTriangles.Count < fineTriangles.Count);
        Assert.Equal(Area(vertices, triangles), Area(half, halfTriangles), 9);
    }
}
