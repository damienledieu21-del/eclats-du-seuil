using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.World;
using static EdS.Data.Tests.V228b2PassageTests;

namespace EdS.Data.Tests;

/// <summary>
/// Sous-lot V2.28b (3) (PLAN_GRANDE_CARTE.md § 7) : l'affichage montre les créatures de tous les secteurs ouverts (<see cref="SectorCrowd"/>)
/// et le déplacement du personnage sans saut à travers un passage (<see cref="PassageGlide"/>) ; carte et décor de l'aire entière.
/// Zone d'essai du V2.28b (2a) : 512 × 256 m, deux secteurs (s00_00 à l'ouest, s01_00 à l'est), un loup dans chacun.
/// </summary>
[Trait("Category", "Sector")]
public class V228b3AffichageTests
{
    private const string West = "s00_00", East = "s01_00";

    private static RealtimeCombat Bridge(WorldSession w) =>
        new(w.Zone.Setup, w.Zone.Engine, w.CombatantId, () => w.Step(), Fixed.FromInt(20),
            () => new CombatBinding(w.Zone.Setup, w.Zone.Engine, w.CombatantId), () => w.RenewsSessions);

    private static SectorCrowd.Member? Shown(SectorCrowd crowd, string sector, string id) =>
        crowd.Members.FirstOrDefault(m => m.Sector == sector && m.Id == id && m.Shown);

    /// <summary>Marche vers l'est jusqu'à ce que <paramref name="until"/> soit vrai, la foule relue à chaque tick.</summary>
    private static void East_(RealtimeCombat c, SectorCrowd crowd, WorldSession w, Func<bool> until, int max = 4000)
    {
        c.SetMove(1, 0);
        for (int i = 0; i < max && !until(); i++)
        {
            c.StepOnce();
            crowd.Refresh(w);
        }
        Assert.True(until());
    }

    [Fact]
    public void Creatures_de_l_autre_secteur_ouvert_montrees_puis_echangees_au_passage()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        crowd.Refresh(w);
        Assert.Equal(new[] { West }, crowd.Sectors);   // l'est n'est pas encore ouvert
        Assert.Equal(0, crowd.ShownCount);
        East_(c, crowd, w, () => crowd.Sectors.Count == 2);
        Assert.Equal(new[] { West, East }, crowd.Sectors);
        Assert.Equal(West, crowd.OwnSector);
        SectorCrowd.Member wolf = Shown(crowd, East, "loup_est")!;
        Assert.NotNull(wolf);
        Assert.True(wolf.Enemy);
        Assert.Equal("s01_00/loup_est", wolf.Key);
        CombatantView v = w.Host!.SectorZone(East)!.Engine.View("loup_est");
        Assert.Equal((v.Position!.Value.X.Raw / 1e4, v.Position.Value.Z.Raw / 1e4), (wolf.Track.ToX, wolf.Track.ToZ));
        Assert.Null(Shown(crowd, West, "loup_ouest"));   // celui de la session du personnage est montré par la vue de sa session
        Assert.DoesNotContain(crowd.Members, m => m.Kind == CombatantKind.Character && m.Shown);

        East_(c, crowd, w, () => w.Passages == 1);
        Assert.Equal(East, crowd.OwnSector);
        Assert.Null(Shown(crowd, East, "loup_est"));
        Assert.NotNull(Shown(crowd, West, "loup_ouest"));
        // Le combattant que le personnage a quitté (parti) n'est jamais montré comme une créature.
        Assert.DoesNotContain(crowd.Members, m => m.Shown && m.Id == "bastion");
        Assert.Equal(1, crowd.ShownCount);
    }

    [Fact]
    public void Une_relecture_par_tick_du_monde_et_glissement_continu()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        East_(c, crowd, w, () => Shown(crowd, East, "loup_est") != null);
        long reads = crowd.Reads;
        Assert.False(crowd.Refresh(w));   // même tick du monde : rien à relire
        Assert.Equal(reads, crowd.Reads);
        // Le loup de l'est (créature à comportement) bouge : chaque tick, la place d'où il part est celle du tick d'avant.
        SectorCrowd.Member wolf = Shown(crowd, East, "loup_est")!;
        for (int i = 0; i < 200; i++)
        {
            double toX = wolf.Track.ToX, toZ = wolf.Track.ToZ;
            c.SetMove(0, 0);
            c.StepOnce();
            Assert.True(crowd.Refresh(w));
            Assert.True(wolf.Shown);
            Assert.Equal((toX, toZ), (wolf.Track.FromX, wolf.Track.FromZ));
            Assert.InRange(Math.Abs(wolf.Track.X(0.5) - (toX + wolf.Track.ToX) / 2), 0, 1e-9);
        }
    }

    [Fact]
    public void Glissement_du_personnage_sans_saut_ni_trou_a_travers_le_passage()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        c.MaxTicksPerAdvance = 5;
        var glide = new PassageGlide();
        c.SetMove(1, 0);
        const double frame = 1.0 / 60;
        double? last = null, maxStep = 0;
        bool absentFrame = false;
        int frames = 0;
        // Avant le passage, puis 3 s après : chaque image, la place montrée avance d'au plus un pas d'image (5 m/s).
        int after = -1;
        while (frames < 20_000 && (after < 0 || frames - after < 180))
        {
            c.Advance(frame);
            CombatantView me = c.Player().View;
            absentFrame |= !me.IsPresent;
            Assert.True(glide.Follow(c.PlayerId, c.Tick, me), $"caché à l'image {frames} ({c.PlayerId}, tick {c.Tick})");
            double x = glide.Track.X(c.TickProgress);
            if (last is double l)
            {
                Assert.True(x >= l - 1e-9, $"recul à l'image {frames} : {l} puis {x}");
                maxStep = Math.Max(maxStep.Value, x - l);
            }
            last = x;
            frames++;
            if (after < 0 && w.Passages == 1) after = frames;
        }
        Assert.Equal(1, w.Passages);
        Assert.True(absentFrame, "le cas couvert : une image où le combattant d'arrivée n'est pas encore présent");
        // 5 m/s : 0,25 m par tick ; au plus deux ticks dans une image de 1/60 s arrondie (pas d'accumulation au-delà).
        Assert.InRange(maxStep!.Value, 0.01, 0.25 + 1e-9);
    }

    [Fact]
    public void Avant_la_correction_le_personnage_etait_cache_une_image()
    {
        // Contrôle : la règle d'avant (montré seulement s'il est présent) cache le personnage pendant le tick d'arrivée.
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        c.SetMove(1, 0);
        for (int i = 0; i < 4000 && w.Passages == 0; i++) c.StepOnce();
        CombatantView me = c.Player().View;
        Assert.False(me.IsPresent);
        Assert.True(PassageGlide.Arriving(me, c.Tick));
        Assert.True(PassageGlide.Shows(me, c.Tick));
        c.StepOnce();
        Assert.True(c.Player().View.IsPresent);
        Assert.False(PassageGlide.Arriving(c.Player().View, c.Tick));
    }

    [Fact]
    public void Prediction_bornee_quand_l_arrivee_n_est_pas_connue()
    {
        var glide = new PassageGlide();
        bool Follow(string id, long tick, double x, bool present) =>
            glide.Follow(id, tick, present, false, false, null, present ? GroundVector.FromRaw((long)(x * 1e4), 0) : null);
        Assert.True(Follow("h", 1, 10, true));
        Assert.True(Follow("h", 2, 10.25, true));
        // Absent sans place : le dernier pas est prolongé deux ticks, puis le personnage est caché.
        Assert.True(Follow("h_p1", 3, 0, false));
        Assert.Equal(10.5, glide.Track.ToX, 6);
        Assert.True(Follow("h_p1", 4, 0, false));
        Assert.Equal(10.75, glide.Track.ToX, 6);
        Assert.False(Follow("h_p1", 5, 0, false));
        // Revenu : posé d'un coup.
        Assert.True(Follow("h_p1", 6, 30, true));
        Assert.Equal((30.0, 30.0), (glide.Track.FromX, glide.Track.ToX));
    }

    [Fact]
    public void Combattant_parti_cache_tout_de_suite_sans_prolongation()
    {
        // Relecture indépendante de la PR #162 : un combattant parti n'est pas prolongé comme un arrivant inconnu.
        var glide = new PassageGlide();
        Assert.True(glide.Follow("h", 1, true, false, false, null, GroundVector.FromRaw(100_000, 0)));
        Assert.True(glide.Follow("h", 2, true, false, false, null, GroundVector.FromRaw(102_500, 0)));
        Assert.False(glide.Follow("h", 3, false, false, true, null, GroundVector.FromRaw(102_500, 0)));
        Assert.False(glide.Track.Known);
    }

    [Fact]
    public void Foule_cadencee_sur_le_tick_de_l_hote_meme_sans_combattant()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        East_(c, crowd, w, () => Shown(crowd, East, "loup_est") != null);
        w.Host!.RefuseForTest("bastion", "essai");   // plus de combattant suivi
        long reads = crowd.Reads;
        for (int i = 0; i < 40; i++)
        {
            c.StepOnce();
            Assert.Equal(w.Host.WorldTick, w.SectorsTick);
            Assert.True(crowd.Refresh(w));
        }
        Assert.Equal(reads + 40, crowd.Reads);
    }

    [Fact]
    public void Reprise_sans_saut_des_creatures_du_secteur_quitte()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        East_(c, crowd, w, () => w.Passages == 1);
        // La vue de l'ancienne session montrait le loup de l'ouest à la place (100, 40) : sa fiche en repart, puis glisse.
        crowd.Seed(w, West, "loup_ouest", 99.5, 40);
        c.StepOnce();
        Assert.True(crowd.Refresh(w));
        SectorCrowd.Member wolf = Shown(crowd, West, "loup_ouest")!;
        Assert.Equal((99.5, 40.0), (wolf.Track.FromX, wolf.Track.FromZ));
        // Sans effet : secteur du personnage, secteur inconnu.
        crowd.Seed(w, East, "loup_est", 1, 1);
        crowd.Seed(w, "s05_05", "x", 1, 1);
        Assert.Null(Shown(crowd, East, "loup_est"));
        Assert.True(crowd.TryLast(West, "loup_ouest", out double x, out double z));
        Assert.Equal((wolf.Track.ToX, wolf.Track.ToZ), (x, z));
        Assert.False(crowd.TryLast(East, "loup_est", out _, out _));
    }

    [Fact]
    public void Secteur_ferme_fiches_oubliees_et_partie_remplacee_repart_de_zero()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        East_(c, crowd, w, () => Shown(crowd, East, "loup_est") != null);
        // Retour loin à l'ouest : l'est se ferme après 60 s sans joueur à moins de 96 m.
        c.SetMove(-1, 0);
        for (int i = 0; i < 2400 && w.Host!.OpenSectors.Count > 1; i++)
        {
            if (c.Player().View.Position is GroundVector p && p.X.Raw < 60 * 10000) c.SetMove(0, 0);
            c.StepOnce();
            crowd.Refresh(w);
        }
        Assert.Single(w.Host!.OpenSectors);
        Assert.Empty(crowd.Members);
        WorldSession other = Open(TwoSectors.Value);
        crowd.Refresh(other);
        Assert.Equal(new[] { West }, crowd.Sectors);
    }

    [Fact]
    public void Aucune_allocation_par_image_ni_par_tick_une_fois_les_creatures_vues()
    {
        WorldSession w = Open(TwoSectors.Value);
        RealtimeCombat c = Bridge(w);
        var crowd = new SectorCrowd();
        var glide = new PassageGlide();
        East_(c, crowd, w, () => Shown(crowd, East, "loup_est") != null);
        c.SetMove(0, 0);
        CombatantView me = c.Player().View;
        long bytes = 0;
        double sum = 0;
        for (int tick = 0; tick < 200; tick++)
        {
            c.StepOnce();   // la simulation alloue (hors de la mesure)
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 3; frame++)
            {
                crowd.Refresh(w);   // une relecture à la première image du tick, rien ensuite
                glide.Follow("bastion", tick, me);
                foreach (SectorCrowd.Member m in (List<SectorCrowd.Member>)crowd.Members)
                    if (m.Shown) sum += m.Track.X(frame / 3.0) + m.Track.Z(frame / 3.0);
            }
            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.True(sum > 0);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void Lecture_des_places_identique_a_la_vue_et_sans_effet()
    {
        WorldSession w = Open(TwoSectors.Value);
        CombatEngine e = w.Zone.Engine;
        string before = string.Join("|", e.CombatantIds.Select(id => e.View(id).Position));
        Assert.Equal(e.CombatantIds.Count, e.CombatantCount);
        for (int i = 0; i < e.CombatantCount; i++)
        {
            CombatantPlace p = e.PlaceAt(i);
            CombatantView v = e.View(p.Id);
            Assert.Equal((v.Team, v.Kind, v.IsPresent, v.IsDead), (p.Team, p.Kind, p.IsPresent, p.IsDead));
            Assert.Equal(v.Position.HasValue, p.HasBody);
            if (v.Position is GroundVector at) Assert.Equal((at.X.Raw, at.Z.Raw, v.Facing!.Value.X.Raw, v.Radius!.Value.Raw), (p.X, p.Z, p.FacingX, p.Radius));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => e.PlaceAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => e.PlaceAt(e.CombatantCount));
        Assert.Equal(before, string.Join("|", e.CombatantIds.Select(id => e.View(id).Position)));
    }
}

/// <summary>Sous-lot V2.28b (3) : la carte et le décor d'une aventure à deux secteurs sont ceux de l'aire entière, gardés au passage.</summary>
[Trait("Category", "Sector")]
public sealed class V228b3CarteTests : IDisposable
{
    private readonly V228b2bAventureTests _setup = new();

    public void Dispose() => _setup.Dispose();

    [Fact]
    public void Carte_de_l_aire_entiere_gardee_au_passage()
    {
        (string data, string scenarios) = _setup.TwoSectorVallons();
        AdventureSession a = _setup.AtEastThreshold(data, scenarios);
        AdventureMap map = a.Map;
        Assert.NotNull(a.World.AreaSpace);
        Assert.Same(a.World.AreaSpace, map.Space);
        Assert.True(map.MaxX > a.World.Zone.Setup.Space!.Max.X.Raw / 1e4 - 1e-9);
        for (int i = 0; i < 4000 && a.World.Passages == 0; i++)
        {
            a.Combat.SetMove(1, 0);
            a.Advance(0.05);
        }
        Assert.Equal(1, a.World.Passages);
        Assert.Same(map, a.Map);   // ni carte repeinte ni décor refait
        Assert.False(CombatDisplay.SameSpace(a.World.Zone.Setup.Space, map.Space));
    }

    [Fact]
    public void Vallons_livres_rien_ne_change()
    {
        // Les Vallons tiennent dans le secteur du cœur : décor et carte de la session (AreaSpace nul), aucune créature d'un autre secteur.
        WorldSession w = Open(V228bSoloSecteursTests.Vallons(true));
        Assert.True(w.OnSectors);
        Assert.Null(w.AreaSpace);
        var crowd = new SectorCrowd();
        for (int i = 0; i < 40; i++)
        {
            w.Step();
            crowd.Refresh(w);
        }
        Assert.Single(crowd.Sectors);
        Assert.Equal(0, crowd.ShownCount);
        Assert.Empty(crowd.Members);
    }
}
