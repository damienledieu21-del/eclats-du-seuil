using EdS.Data.Builds;
using EdS.Data.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Runtime;
using EdS.Simulation.Engine;
using FsCheck.Xunit;
using static EdS.Data.Tests.ScenarioTestData;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot 9.4 : éditeur de talents (<see cref="TalentEditor"/>) et séance jouée avec un build composé. Le build édité reste toujours
/// valide : chaque point ajouté ou retiré passe par <see cref="BuildValidator"/>, un refus ne change rien et donne sa raison.
/// </summary>
[Trait("Category", "Runtime")]
public class Lot94TalentEditorTests
{
    private const string GardeStable = "talent.bastion.rempart.garde_stable";
    private const string EchoDefensif = "talent.bastion.rempart.echo_defensif";
    private const string RempartEternel = "talent.bastion.rempart.rempart_eternel";
    private const string PeauDeFer = "talent.bastion.rempart.peau_de_fer";
    private const string SouffleDuRoc = "talent.bastion.rempart.souffle_du_roc";
    private const string PoidsDuRempart = "talent.bastion.rempart.poids_du_rempart";
    private const string MainLourde = "talent.bastion.contrecoup.main_lourde";

    private static TrainingSession Session(string? build = "build.bastion.rempart", long? durationMs = 60_000) =>
        TrainingSession.Load(DataDirectory, ScenarioPath("entrainement_garde_parade"), build, durationMs: durationMs);

    private static ClassDefinition Bastion => Session().Player.Class;

    private static IEnumerable<ReferenceBuild> ReferenceBuilds() => Session().AvailableBuilds();

    /// <summary>Un build de référence sans aucun talent (même niveau, attributs et compétences).</summary>
    private static CharacterBuild Empty(CharacterBuild from) =>
        new(from.ClassId, from.Level, from.Attributes, null, from.ActiveSkills, from.Ultimate, from.PassiveSkills);

    private static string[] TalentIds(ClassDefinition definition) => definition.Trees.SelectMany(t => t.Talents).Select(t => t.Id).ToArray();

    [Fact]
    public void Editeur_part_du_build_joue_et_compte_les_points()
    {
        TrainingSession session = Session();
        TalentEditor editor = session.CreateTalentEditor();
        Assert.Same(session.Player.Build, editor.Build);
        Assert.Equal((30, 29, 29L, 0L, 18), (editor.Level, editor.PointsAvailable, editor.PointsSpent, editor.PointsRemaining, editor.MajorTalentThreshold));
        Assert.Equal(new[] { "Rempart", "Contrecoup", "Gardien" }, editor.Trees.Select(t => t.Name));
        Assert.Equal(new[] { 19L, 2L, 8L }, editor.Trees.Select(editor.PointsInTree));
        Assert.Equal((3, 1, 0), (editor.Rank(GardeStable), editor.Rank(RempartEternel), editor.Rank(MainLourde)));
    }

    [Fact]
    public void Point_refuse_sans_rien_changer_avec_sa_raison()
    {
        TalentEditor editor = Session().CreateTalentEditor();
        CharacterBuild before = editor.Build;

        void Refused(BuildViolation? why, BuildViolationCode code, string message)
        {
            Assert.NotNull(why);
            Assert.Equal(code, why!.Code);
            Assert.Contains(message, why.Message, StringComparison.Ordinal);
            Assert.Same(before, editor.Build);
        }

        Refused(editor.WhyNotAdd(PeauDeFer), BuildViolationCode.RankOutOfRange, "déjà au rang maximal (5)");
        Refused(editor.Add(PeauDeFer), BuildViolationCode.RankOutOfRange, "déjà au rang maximal (5)");
        Refused(editor.Add(MainLourde), BuildViolationCode.TooManyPoints, "plus aucun point : 29 au niveau 30");
        Refused(editor.Remove(MainLourde), BuildViolationCode.RankOutOfRange, "aucun point à retirer de « Main lourde »");
        // Garde stable est exigée au rang 3 par Écho défensif.
        Refused(editor.Remove(GardeStable), BuildViolationCode.PrerequisiteNotMet, "« Écho défensif » exige « Garde stable » rang 3");
        // Le majeur Rempart éternel exige 18 points dans l'arbre : il en a exactement 18 hors majeur.
        Refused(editor.Remove(PeauDeFer), BuildViolationCode.MajorThresholdNotMet, "18 requis");
        Refused(editor.Add("talent.inconnu"), BuildViolationCode.UnknownTalent, "talent inconnu");
        Assert.Throws<ArgumentNullException>(() => editor.Add(null!));

        // Admis : le majeur, sans dépendant, se retire ; le point libéré se replace ailleurs.
        Assert.Null(editor.WhyNotRemove(RempartEternel));
        Assert.Null(editor.Remove(RempartEternel));
        Assert.Equal((0, 28L, 1L), (editor.Rank(RempartEternel), editor.PointsSpent, editor.PointsRemaining));
        Assert.Null(editor.Add(MainLourde));
        Assert.Equal((1, 0L), (editor.Rank(MainLourde), editor.PointsRemaining));
        Assert.True(BuildValidator.IsValid(editor.Build, editor.Class, Session().Setup.Progression));
    }

    [Fact]
    public void Prerequis_seuil_du_majeur_et_un_seul_majeur()
    {
        TrainingSession session = Session();
        // Deux points par niveau : assez de points pour tenter deux majeurs.
        var rich = new ProgressionRules { TalentPointsPerLevel = 2 };
        var editor = new TalentEditor(Empty(session.Player.Build), session.Player.Class, rich);
        Assert.Equal(58, editor.PointsAvailable);

        Assert.Null(editor.Add(GardeStable));
        Assert.Equal(BuildViolationCode.PrerequisiteNotMet, editor.WhyNotAdd(EchoDefensif)!.Code);
        Assert.Null(editor.Add(GardeStable));
        Assert.Null(editor.Add(GardeStable));
        Assert.Null(editor.Add(EchoDefensif));
        foreach (string id in new[] { PeauDeFer, SouffleDuRoc })
            for (int i = 0; i < 5; i++) Assert.Null(editor.Add(id));
        for (int i = 0; i < 3; i++) Assert.Null(editor.Add(PoidsDuRempart));
        // 17 points dans Rempart : un de moins que le seuil.
        BuildViolation tooEarly = editor.WhyNotAdd(RempartEternel)!;
        Assert.Equal(BuildViolationCode.MajorThresholdNotMet, tooEarly.Code);
        Assert.Contains("17 point(s) investi(s) dans « Rempart », 18 requis", tooEarly.Message, StringComparison.Ordinal);
        Assert.Null(editor.Add(PoidsDuRempart));
        Assert.Null(editor.Add(RempartEternel));

        // Second majeur : Contrecoup rempli jusqu'au seuil, puis refusé (un seul majeur par build).
        foreach ((string id, int ranks) in new[]
        {
            ("talent.bastion.contrecoup.riposte_precise", 3), ("talent.bastion.contrecoup.fureur_contenue", 2), (MainLourde, 5),
            ("talent.bastion.contrecoup.elan", 5), ("talent.bastion.contrecoup.echo_de_guerre", 3),
        })
            for (int i = 0; i < ranks; i++) Assert.Null(editor.Add(id));
        Assert.Equal(BuildViolationCode.TooManyMajorTalents, editor.Add("talent.bastion.contrecoup.jugement_du_colosse")!.Code);
        Assert.Equal(0, editor.Rank("talent.bastion.contrecoup.jugement_du_colosse"));
    }

    [Fact]
    public void Builds_de_reference_reconstruits_point_par_point_depuis_zero_puis_vides()
    {
        TrainingSession session = Session();
        foreach (ReferenceBuild reference in session.AvailableBuilds())
        {
            var editor = new TalentEditor(reference.Build, session.Player.Class, session.Setup.Progression);
            Assert.Null(editor.Clear());
            Assert.Equal(0L, editor.PointsSpent);

            // Un point à la fois, dans n'importe quel ordre admis : on atteint exactement le build de référence.
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach ((string id, int target) in reference.Build.TalentRanks)
                    if (editor.Rank(id) < target && editor.Add(id) is null) progress = true;
            }
            Assert.Equal(reference.Build.TalentRanks.OrderBy(e => e.Key), editor.Build.TalentRanks.OrderBy(e => e.Key));

            // Et l'on revient à zéro un point à la fois (le majeur et les dépendants d'abord, sinon refusés).
            progress = true;
            while (progress)
            {
                progress = false;
                foreach (string id in TalentIds(editor.Class))
                    if (editor.Rank(id) > 0 && editor.Remove(id) is null) progress = true;
            }
            Assert.Empty(editor.Build.TalentRanks);
        }
    }

    [Fact]
    public void Build_de_depart_invalide_refuse()
    {
        TrainingSession session = Session();
        CharacterBuild valid = session.Player.Build;
        var tooMany = new CharacterBuild(valid.ClassId, 10, valid.Attributes, valid.TalentRanks, valid.ActiveSkills, valid.Ultimate, valid.PassiveSkills);
        Assert.Contains("TooManyPoints", Assert.Throws<ArgumentException>(() => new TalentEditor(tooMany, session.Player.Class, session.Setup.Progression)).Message);
        TalentEditor editor = session.CreateTalentEditor();
        Assert.Throws<ArgumentException>(() => editor.Load(tooMany));
        Assert.Same(valid, editor.Build);
        editor.Load(session.AvailableBuilds().Single(b => b.Id == "build.bastion.niveau_10").Build);
        Assert.Equal((10, 9), (editor.Level, editor.PointsAvailable));
    }

    /// <summary>
    /// Suites d'opérations au hasard (ajouts, retraits, identifiants inconnus, vidage, rechargement) depuis un build de référence : le
    /// build reste valide, un refus ne change rien, un ajout ou un retrait admis change exactement un rang d'une unité.
    /// </summary>
    [Property(MaxTest = 60)]
    public bool Operations_au_hasard_gardent_un_build_valide(int seed)
    {
        TrainingSession session = Session();
        IReadOnlyList<ReferenceBuild> references = session.AvailableBuilds();
        var random = new Random(seed);
        var editor = new TalentEditor(references[random.Next(references.Count)].Build, session.Player.Class, session.Setup.Progression);
        string[] ids = TalentIds(editor.Class).Append("talent.inconnu").ToArray();
        for (int step = 0; step < 300; step++)
        {
            CharacterBuild before = editor.Build;
            int choice = random.Next(20);
            if (choice == 0)
            {
                if (editor.Clear() is not null) return false;
            }
            else if (choice == 1)
            {
                editor.Load(references[random.Next(references.Count)].Build);
            }
            else
            {
                string id = ids[random.Next(ids.Length)];
                int delta = choice % 2 == 0 ? 1 : -1;
                BuildViolation? predicted = delta > 0 ? editor.WhyNotAdd(id) : editor.WhyNotRemove(id);
                BuildViolation? why = delta > 0 ? editor.Add(id) : editor.Remove(id);
                if ((predicted is null) != (why is null)) return false;
                if (why is not null && !ReferenceEquals(before, editor.Build)) return false;
                if (why is null)
                {
                    int was = before.TalentRanks.TryGetValue(id, out int r) ? r : 0;
                    if (editor.Rank(id) != was + delta) return false;
                    if (editor.Build.TalentRanks.Count(e => e.Key != id && (!before.TalentRanks.TryGetValue(e.Key, out int b) || b != e.Value)) != 0) return false;
                }
            }
            if (!BuildValidator.IsValid(editor.Build, editor.Class, session.Setup.Progression)) return false;
            if (editor.PointsSpent > editor.PointsAvailable || editor.PointsRemaining < 0) return false;
        }
        return true;
    }

    [Fact]
    public void Seance_jouee_avec_un_build_compose_et_rejouee_a_l_identique()
    {
        TrainingSession session = Session("build.bastion.hybride", durationMs: 6_000);
        TalentEditor editor = session.CreateTalentEditor();
        Assert.Null(editor.Remove(SouffleDuRoc));
        Assert.Null(editor.Add(PoidsDuRempart));

        TrainingSession custom = session.WithCustomBuild(editor.Build, "Personnalisé");
        Assert.Equal(("Personnalisé", (ReferenceBuild?)null), (custom.BuildName, custom.Build));
        Assert.Same(editor.Build, custom.Player.Build);
        Assert.Equal(session.Setup.DurationMs, custom.Setup.DurationMs);
        Assert.Equal("Hybride sans majeur", session.BuildName);
        Assert.Equal("Rempart", custom.WithBuild("build.bastion.rempart").BuildName);
        Assert.Null(custom.WithBuild("build.bastion.rempart").CustomBuildName);

        // La partie se joue et son journal se rejoue octet par octet, build composé compris.
        RealtimeCombat combat = custom.Start();
        combat.SetGuard(true);
        for (int i = 0; !combat.IsFinished; i++)
        {
            if (i % 25 == 10) combat.UseSlot(i / 25 % 4);
            combat.StepOnce();
        }
        var log = new MemoryCombatLog();
        combat.WriteLog(log);
        Assert.Contains(PoidsDuRempart, log.Lines[0], StringComparison.Ordinal);
        Assert.Equal(ReplayStatus.Identical, CombatLogReplayer.Replay(log.Lines.ToList(), ShippedData).Status);
    }

    [Fact]
    public void Build_compose_invalide_ou_sans_nom_refuse()
    {
        TrainingSession session = Session();
        CharacterBuild valid = session.Player.Build;
        var tooMany = new CharacterBuild(valid.ClassId, 10, valid.Attributes, valid.TalentRanks, valid.ActiveSkills, valid.Ultimate, valid.PassiveSkills);
        Assert.Throws<CombatSetupException>(() => session.WithCustomBuild(tooMany, "Trop de points"));
        Assert.Throws<ArgumentException>(() => session.WithCustomBuild(valid, " "));
        Assert.Throws<ArgumentNullException>(() => session.WithCustomBuild(null!, "x"));
    }
}
