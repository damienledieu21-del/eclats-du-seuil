using System.Globalization;
using System.Text.Json;
using EdS.Data;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Simulation.Engine;

namespace EdS.Tools.Network;

/// <summary>Esquive mesurée pour une latence et un temps de réaction (sous-lot 11.7).</summary>
public sealed class DodgeMeasureResult
{
    /// <summary>Latence d'un sens, identique pour les deux joueurs.</summary>
    public int LatencyMs { get; init; }

    /// <summary>Temps de réaction du joueur scripté, après l'affichage de l'annonce.</summary>
    public int ReactionMs { get; init; }

    /// <summary>Impacts de zone tombés sur la place visée d'un joueur (un par attaque annoncée).</summary>
    public int Attacks { get; init; }

    /// <summary>Impacts évités : le joueur visé n'était plus dans la zone au tick d'impact.</summary>
    public int Dodged { get; init; }

    /// <summary>Temps aller-retour lissé mesuré par le premier joueur à la fin.</summary>
    public double RttMs { get; init; }

    /// <summary>Empreintes finales des deux clients égales à celle du journal du serveur.</summary>
    public bool HashesMatch { get; init; }

    public int Desyncs { get; init; }

    public double DodgeRate => Attacks == 0 ? 0 : (double)Dodged / Attacks;
}

/// <summary>
/// Sous-lot 11.7 : esquive d'une attaque annoncée en réseau, selon la latence (H5 du plan du lot 11, « en réseau, mesure de l'esquive
/// de 0 à 200 ms »). Deux joueurs scriptés sur le réseau simulé, face à deux géants qui écrasent tour à tour la place de leur cible
/// (cercle de 2 m, annoncé 900 ms avant l'impact, scénario <c>coop/esquive_duo</c>). Le joueur voit l'annonce sur son client (état reçu
/// du serveur, sa propre place prédite) ; après son temps de réaction, il sort du cercle en s'éloignant du centre, puis revient à sa place.
/// Le serveur reste seul juge : un impact est esquivé si le joueur visé n'est plus dans la zone au tick d'impact (journal du serveur).
/// Joueur parfait à 0 ms de réaction ; 250 ms est un temps de réaction humain courant.
/// </summary>
public static class NetworkDodge
{
    /// <summary>Latences d'un sens mesurées par défaut.</summary>
    public static readonly IReadOnlyList<int> Latencies = new[] { 0, 50, 100, 150, 200 };

    /// <summary>Temps de réaction mesurés par défaut.</summary>
    public static readonly IReadOnlyList<int> Reactions = new[] { 0, 250 };

    /// <summary>Pas du temps virtuel (une image de 10 ms).</summary>
    public const double StepSeconds = 0.01;

    /// <summary>Marge prise au-delà du bord de la zone avant de s'arrêter (un pas de marche de part et d'autre de la place prédite).</summary>
    private const double SafetyMeters = 0.5;

    private sealed class Player
    {
        public NetworkClient Client = null!;
        public bool ReadySent;
        public bool HomeSet;
        public double HomeX, HomeZ;
        public readonly Dictionary<(string Actor, long Impact), double> FirstSeen = new();
    }

    public static DodgeMeasureResult Run(GameData data, CombatSetup scenario, int latencyMs, int reactionMs, ulong seed = 1)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(scenario);
        if (latencyMs < 0 || latencyMs > NetworkMeasures.MaxDelayMs)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), $"Latence de 0 à {NetworkMeasures.MaxDelayMs} ms.");
        if (reactionMs < 0 || reactionMs > 2_000) throw new ArgumentOutOfRangeException(nameof(reactionMs), "Temps de réaction de 0 à 2 000 ms.");
        if (scenario.Space is null) throw new ArgumentException("Scénario sans espace : l'esquive demande un combat positionné.", nameof(scenario));
        var net = new SimulatedNetwork(seed);
        SimulatedListener listener = net.Listen("salle");
        int tokens = 0;
        var room = new CombatRoom(data, scenario, new RoomOptions { TokenSource = () => (++tokens).ToString("x32", CultureInfo.InvariantCulture) });
        if (room.SeatIds.Count != 2)
            throw new ArgumentException($"Scénario à {room.SeatIds.Count} personnage(s) : la mesure d'esquive en demande deux.", nameof(scenario));
        var link = new LinkProfile(latencyMs, 0);
        var players = new List<Player>();
        foreach ((string name, string build) in new[] { ("Dam", "build.bastion.rempart"), ("Léa", "build.bastion.gardien") })
            players.Add(new Player { Client = new NetworkClient(data, net.Connect("salle", link), name, new BuildChoice(build)) });

        bool started = false;
        long frames = checked((long)Math.Ceiling((scenario.DurationMs / 1000.0 + 30 + 8.0 * latencyMs / 1000) / StepSeconds));
        for (long frame = 0; frame < frames; frame++)
        {
            net.Advance(StepSeconds);
            while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
            room.Advance(StepSeconds);
            foreach (Player player in players)
            {
                Play(player, scenario, reactionMs / 1000.0);
                player.Client.Advance(StepSeconds);
            }
            if (room.Phase == RoomPhase.Combat) started = true;
            if (started && room.Phase != RoomPhase.Combat && players.All(p => p.Client.Status != ClientStatus.Combat)) break;
            if (!started && players.All(p => p.Client.Status == ClientStatus.Failed)) break;
        }
        if (!started || room.LastJournal is null)
            throw new InvalidOperationException("Le combat de la mesure d'esquive n'a pas eu lieu jusqu'au bout.");

        // Chaque impact d'un cercle sur la place du joueur visé à l'annonce (attackStarted : assaillant, tick d'impact, cible ; la cible peut
        // changer d'une attaque à l'autre, par une provocation) : esquivé si ce joueur n'est pas parmi les touchés (attackLanded).
        var aimed = new Dictionary<(string Actor, long Tick), string>();
        int attacks = 0, dodged = 0;
        foreach (string line in room.LastJournal.Skip(1))
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement e = doc.RootElement;
            string? kind = e.GetProperty("event").GetString();
            if (kind == "attackStarted" && e.TryGetProperty("shape", out JsonElement shape) && shape.GetString() == "circle"
                && e.TryGetProperty("target", out JsonElement aimedAt) && aimedAt.ValueKind == JsonValueKind.String)
                aimed[(e.GetProperty("actor").GetString()!, e.GetProperty("impactTick").GetInt64())] = aimedAt.GetString()!;
            if (kind != "attackLanded") continue;
            if (!aimed.TryGetValue((e.GetProperty("actor").GetString()!, e.GetProperty("tick").GetInt64()), out string? target)) continue;
            attacks++;
            if (!e.GetProperty("targets").EnumerateArray().Any(t => t.GetString() == target)) dodged++;
        }
        string server = HashingCombatLog.Of(new[] { players[0].Client.Combat!.Header }.Concat(room.LastJournal.Skip(1)));
        return new DodgeMeasureResult
        {
            LatencyMs = latencyMs,
            ReactionMs = reactionMs,
            Attacks = attacks,
            Dodged = dodged,
            RttMs = players[0].Client.SmoothedRttMs ?? 0,
            HashesMatch = players.All(p => p.Client.Combat?.Hash == server),
            Desyncs = players.Sum(p => p.Client.Combat?.Desyncs ?? 0),
        };
    }

    /// <summary>
    /// Le joueur scripté : prêt en salle ; en combat, il sort d'un cercle annoncé qui le menace (après sa réaction), attend sur place tant
    /// qu'un cercle annoncé couvre sa place de départ, puis y revient.
    /// </summary>
    private static void Play(Player player, CombatSetup scenario, double reactionSeconds)
    {
        NetworkClient client = player.Client;
        if (client.Status == ClientStatus.Lobby && !player.ReadySent)
        {
            client.SetReady(true);
            player.ReadySent = true;
        }
        if (client.Status != ClientStatus.Combat || client.Combat is not { } combat || combat.IsFinished) return;
        CombatantView me = combat.Player().View;
        if (me.Position is not { } position || me.Radius is not { } radius || me.IsDead) return;
        if (!player.HomeSet)
        {
            CombatantSetup home = scenario.Combatants.Single(c => c.Id == me.Id);
            player.HomeX = home.Placement!.Position.X.Raw / 10_000.0;
            player.HomeZ = home.Placement.Position.Z.Raw / 10_000.0;
            player.HomeSet = true;
        }
        (double x, double z) = combat.PredictedPosition() ?? (position.X.Raw / 10_000.0, position.Z.Raw / 10_000.0);
        double r = radius.Raw / 10_000.0;
        AttackZone? threat = null;
        bool homeCovered = false;
        foreach (AnnouncedAttack attack in combat.Announced())
        {
            if (attack.Zone is not { Shape: AttackShape.Circle } zone) continue;
            var key = (attack.Actor, attack.ImpactTick);
            if (!player.FirstSeen.TryGetValue(key, out double seen)) player.FirstSeen[key] = seen = client.Now;
            if (client.Now - seen < reactionSeconds) continue;   // pas encore vue par le joueur
            double cx = zone.OriginX / 10_000.0, cz = zone.OriginZ / 10_000.0, keep = zone.Reach / 10_000.0 + r + SafetyMeters;
            if ((x - cx) * (x - cx) + (z - cz) * (z - cz) < keep * keep) threat ??= zone;
            if ((player.HomeX - cx) * (player.HomeX - cx) + (player.HomeZ - cz) * (player.HomeZ - cz) < keep * keep) homeCovered = true;
        }
        if (threat is not null)
        {
            double dx = x - threat.OriginX / 10_000.0, dz = z - threat.OriginZ / 10_000.0;
            Walk(combat, dx, dz);
            return;
        }
        if (homeCovered)
        {
            combat.SetMove(0, 0);
            return;
        }
        double hx = player.HomeX - x, hz = player.HomeZ - z;
        if (hx * hx + hz * hz > 0.3 * 0.3) Walk(combat, hx, hz);
        else combat.SetMove(0, 0);
    }

    /// <summary>Marche dans la direction (<paramref name="dx"/>, <paramref name="dz"/>), ramenée à une saisie pleine (sinon la zone morte
    /// de <see cref="MoveInput"/> arrêterait un joueur tout près du centre) ; direction nulle : vers l'est.</summary>
    private static void Walk(NetworkCombat combat, double dx, double dz)
    {
        double length = Math.Sqrt(dx * dx + dz * dz);
        if (length < 1e-6) combat.SetMove(1, 0);
        else combat.SetMove(dx / length, dz / length);
    }

    /// <summary>Toutes les latences et tous les temps de réaction demandés.</summary>
    public static IReadOnlyList<DodgeMeasureResult> Measure(GameData data, CombatSetup scenario, IEnumerable<int>? latencies = null, IEnumerable<int>? reactions = null) =>
        (reactions ?? Reactions).SelectMany(reaction => (latencies ?? Latencies).Select(latency => Run(data, scenario, latency, reaction))).ToList();

    /// <summary>Rapport Markdown des mesures d'esquive.</summary>
    public static void Write(TextWriter output, IReadOnlyList<DodgeMeasureResult> results, string scenarioName)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(results);
        CultureInfo fr = CultureInfo.InvariantCulture;
        output.WriteLine($"# Esquive en réseau (sous-lot 11.7, scénario {scenarioName})");
        output.WriteLine();
        output.WriteLine("Cercle de 2 m annoncé 900 ms avant l'impact sur la place du joueur visé ; le joueur scripté sort du cercle dès qu'il voit l'annonce");
        output.WriteLine("sur son client (après son temps de réaction), en s'éloignant du centre à la vitesse de marche (5 m/s). Le serveur juge l'impact.");
        output.WriteLine();
        output.WriteLine("| Latence d'un sens | Réaction | Temps aller-retour | Impacts | Esquivés | Taux | Empreintes | Divergences |");
        output.WriteLine("|---:|---:|---:|---:|---:|---:|:---:|---:|");
        foreach (DodgeMeasureResult r in results)
            output.WriteLine(string.Format(fr, "| {0} ms | {1} ms | {2:0} ms | {3} | {4} | {5:0} % | {6} | {7} |",
                r.LatencyMs, r.ReactionMs, r.RttMs, r.Attacks, r.Dodged, r.DodgeRate * 100, r.HashesMatch ? "identiques" : "**différentes**", r.Desyncs));
        output.WriteLine();
        output.WriteLine("Lecture : sortir d'un cercle de 2 m demande 0,5 s de marche ; l'annonce arrive au client une demi-latence après le serveur et l'ordre de");
        output.WriteLine("marche une autre demi-latence plus tard. Il reste donc environ 900 − 500 − réaction − aller-retour − un tick pour esquiver.");
    }
}
