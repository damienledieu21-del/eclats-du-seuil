using System.Security.Cryptography;
using System.Text;
using EdS.Domain.Combat;
using EdS.Simulation.Engine;
using EdS.TestSupport;
using static EdS.Simulation.Tests.Engine.TestCombat;

namespace EdS.Simulation.Tests.Regression;

/// <summary>
/// Sous-lot 8.1 : non-régression du moteur sur des règles de test (<see cref="Lot81EngineBaselines"/>). Chaque journal du moteur 0.9.1,
/// figé dans Fixtures/logs-0.9.1 depuis le commit 405835d, est comparé au journal actuel : en-tête égal hors version du format, version
/// du moteur et identité des données ; lignes égales octet par octet, sauf celles dont l'événement a reçu des champs au lot 8, égales
/// une fois ces champs retirés ; et ces champs à leur valeur neutre (aucune Charge consommée, règles effectives = règles de base).
/// Sans modificateur, le passage des règles de base aux règles effectives du combattant ne change donc rien, y compris pour l'entretien
/// et la régénération en virgule fixe, le coût de blocage, la parade et les durées d'altération.
/// </summary>
[Trait("Category", "Regression")]
[Trait("Category", "Modifier")]
public class Lot81EngineBaselineTests
{
    public static TheoryData<string> Names() => new(Lot81EngineBaselines.All.Select(b => b.Name));

    private static string[] Historical(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs-0.9.1", name + ".jsonl"));

    [Theory]
    [MemberData(nameof(Names))]
    public void Journal_0_9_1_reproduit_sans_modificateur(string name)
    {
        CombatSetup setup = Lot81EngineBaselines.All.Single(b => b.Name == name).Setup();
        string[] before = Historical(name);
        IReadOnlyList<string> after = Run(setup).Log.Lines;
        Assert.Equal(before.Length, after.Count);
        Assert.Contains("\"formatVersion\":4,\"engineVersion\":\"0.9.1\"", before[0]);
        Assert.Contains($"\"formatVersion\":{CombatEngine.LogFormatVersion},\"engineVersion\":\"{CombatEngine.Version}\"", after[0]);
        Assert.Equal(Lot8LogFields.StableHeader(before[0]), Lot8LogFields.StableHeader(after[0]));
        string baseRules = Lot8LogFields.BaseRulesJson(setup.Combat);
        for (int i = 1; i < before.Length; i++)
        {
            Assert.Null(Lot8LogFields.NeutralProblem(after[i], _ => baseRules));
            Assert.Equal(Lot8LogFields.Comparable(before[i]), Lot8LogFields.Comparable(after[i]));
        }
    }

    [Fact]
    public void Journal_de_reference_0_9_1_identique_a_l_empreinte_historique()
    {
        // Le journal figé de la rotation est celui de l'ancienne empreinte ReferenceHash (moteur 0.9.1) : la référence de comparaison
        // est bien le journal historique.
        string log = string.Concat(Historical("rotation_20s").Select(l => l + "\n"));
        Assert.Equal("a67e3671093766c692e5bcbc96bbb8bd693dd308df4c2aad5f0f53e43bd9577f",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(log))).ToLowerInvariant());
    }
}
