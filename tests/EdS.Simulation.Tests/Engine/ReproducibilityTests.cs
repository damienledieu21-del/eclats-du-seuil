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

    /// <summary>Décisions aléatoires d'un combat : le résultat du tirage critique de chaque impact, dans l'ordre.</summary>
    private static string CriticalDecisions(long seed) =>
        string.Concat(Events(Run(Setup(Rotation(30_000), seed: seed, durationMs: 30_000)).Log).OfKind("damage")
            .Select(e => e.GetProperty("critical").GetBoolean() ? '1' : '0'));

    [Fact]
    public void Graines_differentes_decisions_aleatoires_differentes()
    {
        // Correctif 6.1 : on compare les décisions aléatoires elles-mêmes, pas le journal entier, dont l'en-tête contient
        // la graine et différait donc toujours, même avec un générateur constant.
        string reference = CriticalDecisions(1);
        Assert.True(reference.Length >= 10, "trop peu d'impacts pour juger les tirages");
        var decisions = Enumerable.Range(2, 10).Select(s => CriticalDecisions(s)).ToList();
        Assert.All(decisions, d => Assert.Equal(reference.Length, d.Length));   // même déroulé hors aléa
        Assert.Contains(decisions, d => d != reference);
        Assert.True(decisions.Distinct().Count() >= 5, "les graines devraient produire des séquences de critiques variées");
        Assert.Equal(reference, CriticalDecisions(1));                          // et la même graine, les mêmes décisions
    }

    [Fact]
    public void En_tete_complet()
    {
        var events = Run(Setup(new[] { Cmd(0, Frappe) }, seed: 7)).Log.Lines;
        string header = events[0];
        Assert.StartsWith("{\"type\":\"header\",\"format\":\"eds-combat-log\",\"formatVersion\":2,\"engineVersion\":\"0.7.0\",\"tickMs\":50,", header);
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

    internal const string ReferenceHash = "e1c2d43db7302ed61015f936326aa823375e3089f9f33e56bd3cfe6a53bab278";

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
