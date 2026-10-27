using EdS.Runtime;

namespace EdS.Data.Tests;

/// <summary>
/// Essai réaliste 6 (essai de Damien du 27 octobre 2026 : « la charge rouge du sanglier ou les rond des autres personnages ») : formes des
/// annonces d'attaque au sol pour le shader « EdS/Annonce ».
/// </summary>
[Trait("Category", "Runtime")]
public class TelegraphShapesTests
{
    private static void Coherent(TelegraphMesh mesh)
    {
        int n = mesh.Vertices.Count;
        Assert.True(n > 0);
        Assert.Equal(n, mesh.Progress.Count);
        Assert.Equal(n, mesh.Side.Count);
        Assert.Equal(n, mesh.Edge.Count);
        Assert.Equal(0, mesh.Triangles.Count % 3);
        Assert.All(mesh.Triangles, t => Assert.InRange(t, 0, n - 1));
        Assert.All(mesh.Progress, p => Assert.InRange(p, 0.0, 1.0));
        Assert.All(mesh.Side, s => Assert.InRange(s, -1.0, 1.0));
        Assert.All(mesh.Vertices, v => Assert.Equal(0.0, v.Y));
        // Triangles tournés vers le haut (même convention que les autres formes du jeu : produit vectoriel (b - a) × (c - a) vers +y).
        for (int k = 0; k < mesh.Triangles.Count; k += 3)
        {
            StyleVector a = mesh.Vertices[mesh.Triangles[k]], b = mesh.Vertices[mesh.Triangles[k + 1]], c = mesh.Vertices[mesh.Triangles[k + 2]];
            double y = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z);
            Assert.True(y > 1e-9, $"triangle {k / 3} : {y}");
        }
    }

    private static int Nearest(TelegraphMesh mesh, double x, double z) =>
        Enumerable.Range(0, mesh.Vertices.Count).OrderBy(i => Math.Pow(mesh.Vertices[i].X - x, 2) + Math.Pow(mesh.Vertices[i].Z - z, 2)).First();

    [Fact]
    public void Cercle()
    {
        TelegraphMesh mesh = TelegraphShapes.Circle(3, -2, 4);
        Coherent(mesh);
        Assert.Equal(4, mesh.Reach);
        int center = Nearest(mesh, 3, -2), rim = Nearest(mesh, 7, -2);
        Assert.Equal((0.0, 4.0), (mesh.Progress[center], mesh.Edge[center]));
        Assert.Equal(1.0, mesh.Progress[rim], 9);
        Assert.Equal(0.0, mesh.Edge[rim], 9);
        Assert.All(mesh.Vertices, v => Assert.True(Math.Sqrt(Math.Pow(v.X - 3, 2) + Math.Pow(v.Z + 2, 2)) <= 4 + 1e-9));
        Assert.All(mesh.Edge, e => Assert.True(e >= -1e-9));
    }

    [Fact]
    public void Cone()
    {
        // Cône de 90° vers +z, long de 5 m.
        TelegraphMesh mesh = TelegraphShapes.Cone(0, 0, Math.PI / 2, Math.PI / 2, 5);
        Coherent(mesh);
        int apex = Nearest(mesh, 0, 0), tip = Nearest(mesh, 0, 5), side = Nearest(mesh, Math.Cos(Math.PI / 4) * 3, Math.Sin(Math.PI / 4) * 3);
        Assert.Equal(0.0, mesh.Edge[apex], 9);
        Assert.Equal(0.0, mesh.Edge[tip], 9);   // sur l'arc du bout
        Assert.Equal(1.0, mesh.Progress[tip], 9);
        Assert.Equal(0.0, mesh.Edge[side], 9);  // sur le rayon droit
        Assert.Equal(-1.0, mesh.Side[side], 9);
        // Vers le milieu, à 2,5 m : à 2,5 m du bout, et à environ 2,5 · sin 45° ≈ 1,77 m des côtés (au sommet de grille le plus proche).
        int middle = Nearest(mesh, 0, 2.5);
        Assert.InRange(mesh.Edge[middle], 1.5, 1.95);
        Assert.InRange(mesh.Side[middle], -0.06, 0.06);
        Assert.All(mesh.Vertices, v => Assert.True(v.Z >= -1e-9 && Math.Abs(v.X) <= v.Z + 1e-9));
    }

    [Fact]
    public void Bande_a_bouts_arrondis()
    {
        // Charge du sanglier : de (2, 1) vers +x sur 6 m, 0,8 m de part et d'autre.
        TelegraphMesh mesh = TelegraphShapes.Line(2, 1, 1, 0, 6, 0.8);
        Coherent(mesh);
        Assert.Equal(6.8, mesh.Reach, 9);
        int start = Nearest(mesh, 1.5, 1), middle = Nearest(mesh, 5, 1), end = Nearest(mesh, 8.5, 1), left = Nearest(mesh, 5, 1.8), corner = Nearest(mesh, 8.8, 1.8);
        Assert.Equal(0.0, mesh.Progress[start]);
        Assert.Equal((0.5, 0.8), (Math.Round(mesh.Progress[middle], 9), Math.Round(mesh.Edge[middle], 9)));
        Assert.Equal(1.0, mesh.Progress[end], 9);
        Assert.Equal(0.0, mesh.Edge[left], 9);
        Assert.Equal(1.0, mesh.Side[left], 9);   // la gauche de l'axe +x est +z
        Assert.True(mesh.Edge[corner] < -0.2, "coin hors du bout arrondi");
        // Axe non unitaire ou nul : normalisé, ou +x par défaut ; longueur nulle : un disque (avancée 1).
        Assert.Equal(TelegraphShapes.Line(0, 0, 3, 0, 4, 1).Vertices.Select(v => (v.X, v.Z)), TelegraphShapes.Line(0, 0, 1, 0, 4, 1).Vertices.Select(v => (v.X, v.Z)));
        Coherent(TelegraphShapes.Line(0, 0, 0, 0, 4, 1));
        Assert.All(TelegraphShapes.Line(0, 0, 0, 1, 0, 1).Progress, p => Assert.Equal(1.0, p));
    }

    [Fact]
    public void Tailles_bornees()
    {
        // Les cases restent d'environ 30 cm, sans dépasser des plafonds (une immense annonce reste dessinable).
        Assert.InRange(TelegraphShapes.Line(0, 0, 1, 0, 1000, 50).Vertices.Count, 1, 97 * 17);
        Assert.InRange(TelegraphShapes.Circle(0, 0, 1000).Vertices.Count, 1, 25 * 49);
        Assert.InRange(TelegraphShapes.Cone(0, 0, 0, Math.PI * 2, 1000).Vertices.Count, 1, 25 * 49);
        Coherent(TelegraphShapes.Circle(0, 0, 0));
        Coherent(TelegraphShapes.Cone(0, 0, 0, 0, 0));
    }
}
