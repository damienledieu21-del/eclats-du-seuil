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
        Assert.StartsWith($"{{\"type\":\"header\",\"format\":\"eds-combat-log\",\"formatVersion\":{CombatEngine.LogFormatVersion},\"engineVersion\":\"{CombatEngine.Version}\",\"tickMs\":50,", header);
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

    // Sous-lot 8.1 : moteur 0.10.0, format 5 ; le journal 0.9.1 (ancienne empreinte a67e3671…) est figé dans Fixtures/logs-0.9.1 et
    // reproduit au gameplay près des champs ajoutés (Lot81EngineBaselineTests). Lot 8 (décision du 18/10/2026) : moteur 0.11.0, seul
    // l'en-tête change (ancienne empreinte 6321d723…). Sous-lot 8.2 : moteur 0.12.0, format 6 (absorbed et absorbedBy sur les dégâts,
    // damageAbsorbed au bilan, shield.potencyDealt dans les règles effectives), sans bouclier : gameplay inchangé (ancienne empreinte 6cefb445…).
    // Sous-lot 8.3 : moteur 0.13.0, format 7 (transferred sur les dégâts, damageTransferred et damageRedirected au bilan, clés protection.*
    // dans les règles effectives), sans protection : gameplay inchangé (ancienne empreinte 9294c3e3…). Sous-lot 8.4 : moteur 0.14.0, format 8
    // (guard.noUpkeep dans les règles effectives) ; classe d'essai inchangée : gameplay inchangé (ancienne empreinte 0f1be953…). Sous-lot
    // 11.1 : moteur 0.15.0, format 9 (PV facultatifs des assaillants) ; le mannequin n'en a pas : seul l'en-tête change (ancienne empreinte
    // 234e3c83…). Sous-lot 11.4a : moteur 0.16.0, format 10 ; combat sans espace : seul l'en-tête change (ancienne empreinte 4d08cf79…).
    // Sous-lot 11.4b : moteur 0.17.0, format 11 (portées) ; combat sans espace : seul l'en-tête change (ancienne empreinte ed17e941…).
    // Sous-lot 11.4c : moteur 0.18.0, format 12 (annonces, garde de face) ; seul l'en-tête change (ancienne empreinte fa85733d…).
    // Sous-lot 11.4d : moteur 0.19.0, format 13 (arrivées et départs) ; seul l'en-tête change (ancienne empreinte 61557b5e…).
    // Sous-lot 11.5 : moteur 0.20.0, format 14 (comportements) ; seul l'en-tête change (ancienne empreinte b92f284c…).
    // Sous-lot 11.9 : moteur 0.21.0, format 15 (obstacle bas, poussée, arrêt pendant l'annonce, meute) ; seul l'en-tête change (ancienne
    // empreinte a35b9a5b…). Sous-lot 12.1 : moteur 0.22.0, format 16 (zone) ; combat fermé : seul l'en-tête change (ancienne empreinte
    // cf174eb2…).
    internal const string ReferenceHash = "2dc514da6df9952135a5ccfbac72a3d5d4b9dc492cefb8095ce249c6a77d61c5";

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
