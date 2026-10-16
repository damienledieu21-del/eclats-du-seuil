using System.Security.Cryptography;
using System.Text;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using FsCheck.Xunit;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Engine;

[Trait("Category", "Engine")]
public class ReproducibilityTests
{
    private static IEnumerable<CombatCommand> Rotation(long durationMs)
    {
        for (long t = 0; t < durationMs; t += 1000)
        {
            yield return Cmd(t, Frappe);
            yield return Cmd(t, Heurt);
            yield return Cmd(t, Onde, target: null);
            yield return Cmd(t, Gratuite);
        }
    }

    [Fact]
    public void Meme_graine_meme_journal_octet_par_octet()
    {
        CombatSetup setup = Setup(Rotation(30_000), durationMs: 30_000);
        string first = Run(setup).Log.ToString();
        string second = Run(setup).Log.ToString();
        Assert.Equal(first, second);
        Assert.Equal(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
    }

    [Property(MaxTest = 1000)]
    public bool Reproductible_pour_toute_graine(long seed)
    {
        seed = seed == long.MinValue ? 0 : Math.Abs(seed);
        CombatSetup setup = Setup(Rotation(5_000), seed: seed, durationMs: 5_000);
        return Run(setup).Log.ToString() == Run(setup).Log.ToString();
    }

    [Fact]
    public void Graines_differentes_journaux_differents()
    {
        // Sur 30 s, les tirages critiques diffèrent presque toujours : on vérifie qu'au moins une graine sur dix s'écarte.
        string reference = Run(Setup(Rotation(30_000), seed: 1, durationMs: 30_000)).Log.ToString();
        Assert.Contains(Enumerable.Range(2, 10), s => Run(Setup(Rotation(30_000), seed: s, durationMs: 30_000)).Log.ToString() != reference);
    }

    [Fact]
    public void En_tete_complet()
    {
        var events = Run(Setup(new[] { Cmd(0, Frappe) }, seed: 7)).Log.Lines;
        string header = events[0];
        Assert.StartsWith("{\"type\":\"header\",\"format\":\"eds-combat-log\",\"formatVersion\":1,\"engineVersion\":\"0.6.0\",\"tickMs\":50,", header);
        Assert.Contains("\"fingerprint\":\"sha256:regles\"", header);
        Assert.Contains("\"fingerprint\":\"sha256:classe\"", header);
        Assert.Contains("\"seed\":7", header);
        Assert.Contains("\"commands\":[{\"atMs\":0,\"actor\":\"bastion\",\"skill\":\"skill.essai.frappe\",\"target\":\"mannequin\"}]", header);
    }

    [Fact]
    public void Journal_de_reference_stable()
    {
        // Empreinte d'un journal complet : tout changement du moteur, de la formule ou du format la modifie
        // (à mettre à jour sciemment, avec une hausse de CombatEngine.Version si le comportement change).
        string log = Run(Setup(Rotation(20_000), seed: 2026, durationMs: 20_000)).Log.ToString();
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(log))).ToLowerInvariant();
        Assert.Equal(ReferenceHash, hash);
    }

    internal const string ReferenceHash = "56f6029cd3759980238e3dadb26ac688aff5a1427e29eda69a1cb70fc84f184d";

    [Fact]
    public void Ajouter_un_combattant_ne_decale_pas_les_tirages_des_autres()
    {
        var commands = Enumerable.Range(0, 20).Select(i => Cmd(i * 1000L, Gratuite)).ToArray();
        var alone = Run(Setup(commands, durationMs: 20_000)).Log;
        var withOther = Run(Setup(commands, new CombatantSetup[] { Character(), Dummy(), Character("autre", "joueurs") }, durationMs: 20_000)).Log;
        IEnumerable<bool> Crits(MemoryCombatLog log) => Events(log).OfKind("damage").Where(e => e.Str("source") == "bastion").Select(e => e.GetProperty("critical").GetBoolean());
        Assert.Equal(Crits(alone), Crits(withOther));
    }
}
