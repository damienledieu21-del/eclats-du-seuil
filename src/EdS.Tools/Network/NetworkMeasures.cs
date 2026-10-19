using System.Globalization;
using EdS.Data;
using EdS.Data.Json;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Net;
using EdS.Runtime;
using EdS.Simulation.Engine;
using EdS.Simulation.Randomness;

namespace EdS.Tools.Network;

/// <summary>Moment où le joueur mesuré presse la parade.</summary>
public enum ParryTiming
{
    /// <summary>À vue : 50 ms avant le coup affiché (± 25 ms), le bon moment en solo.</summary>
    OnSight = 0,

    /// <summary>
    /// Anticipée : le même moment avancé du temps aller-retour mesuré ; c'est ce que ferait, pour un joueur qui pare à vue, une
    /// télégraphie affichée en avance du ping.
    /// </summary>
    Anticipated = 1,
}

/// <summary>Réglages d'une partie mesurée (sous-lot 10.5).</summary>
public sealed class NetworkMeasureOptions
{
    /// <summary>Latence d'un sens, identique pour les deux joueurs.</summary>
    public int LatencyMs { get; init; }

    /// <summary>Gigue d'un sens (tirage uniforme de 0 à cette valeur, ajouté à la latence).</summary>
    public int JitterMs { get; init; }

    /// <summary>Graine du réseau simulé (délais tirés).</summary>
    public ulong Seed { get; init; } = 1;

    public ParryTiming Timing { get; init; } = ParryTiming.OnSight;

    /// <summary>Coupure du premier joueur, en secondes de combat ; null : aucune.</summary>
    public double? CutAtSeconds { get; init; }

    /// <summary>Durée de la coupure avant la nouvelle connexion.</summary>
    public double CutSeconds { get; init; } = 3;

    /// <summary>Pas du temps virtuel (une image de 10 ms).</summary>
    public double StepSeconds { get; init; } = 0.01;
}

/// <summary>Mesures d'une partie à deux joueurs sur le réseau simulé.</summary>
public sealed class NetworkMeasureResult
{
    public NetworkMeasureOptions Options { get; init; } = new();

    /// <summary>Parades pressées par les deux joueurs.</summary>
    public int ParryPresses { get; init; }

    /// <summary>Parades parfaites et ratées comptées par le serveur (résultat du combat).</summary>
    public long PerfectParries { get; init; }
    public long FailedParries { get; init; }

    /// <summary>Commandes refusées par le moteur du serveur (par exemple une parade pendant la récupération).</summary>
    public long CommandsRejected { get; init; }

    /// <summary>Délai entre la pression d'une parade et le tick du serveur qui l'applique.</summary>
    public double ActionDelayMeanMs { get; init; }
    public double ActionDelayP95Ms { get; init; }

    /// <summary>Retard moyen de l'affichage sur le serveur (ticks d'écart × 50 ms).</summary>
    public double DisplayLagMeanMs { get; init; }

    /// <summary>Temps aller-retour lissé mesuré par le premier joueur à la fin.</summary>
    public double RttMs { get; init; }

    public int Desyncs { get; init; }
    public int Resyncs { get; init; }

    /// <summary>Après une coupure : retour réussi, et délai entre la nouvelle connexion et la reprise du combat.</summary>
    public bool? Reconnected { get; init; }
    public double? ReconnectDelayMs { get; init; }
    public string? ReconnectError { get; init; }

    /// <summary>Empreintes finales des deux clients égales à celle du journal du serveur.</summary>
    public bool HashesMatch { get; init; }

    public string EndReason { get; init; } = string.Empty;
    public long EndTick { get; init; }
    public string? Winner { get; init; }

    /// <summary>Parades parfaites sur parades pressées.</summary>
    public double ParrySuccess => ParryPresses == 0 ? 0 : (double)PerfectParries / ParryPresses;
}

/// <summary>
/// Sous-lot 10.5 : parties de coopération jouées par deux joueurs scriptés sur le réseau simulé (latence, gigue, coupures), avec la
/// salle et les clients du lot 10, en temps virtuel : mêmes réglages, mêmes résultats. Le joueur mesuré voit l'état reçu du serveur
/// et presse la parade selon <see cref="ParryTiming"/> ; il garde à l'approche d'un coup et lance ses compétences sinon.
/// </summary>
public static class NetworkMeasures
{
    /// <summary>
    /// Moment de parade visé à l'écran : 50 ms avant le coup affiché. En solo, la parade s'applique au tick en préparation et couvre les
    /// coups des trois ticks suivants : elle réussit si on la presse entre 100 ms avant le coup affiché et 50 ms après ; 50 ms en est le
    /// milieu, qu'un joueur entraîné en solo apprend.
    /// </summary>
    public const double ParryLeadSeconds = 0.05;

    /// <summary>Imprécision du joueur : chaque parade est pressée jusqu'à 25 ms plus tôt ou plus tard (tirage uniforme, graine de la partie).</summary>
    public const double AimSpreadSeconds = 0.025;

    /// <summary>Garde levée quand un coup arrive dans moins de ce délai.</summary>
    public const double GuardSeconds = 0.8;

    /// <summary>Pause minimale entre deux demandes de compétence (plus le temps aller-retour).</summary>
    public const double SkillPauseSeconds = 0.1;

    public const string FirstSeat = "bastion";

    private sealed class Player
    {
        public NetworkClient Client = null!;
        public SimulatedConnection Link = null!;
        public ParryTiming Timing;
        public Pcg32 Aim = null!;
        public readonly Dictionary<string, double> Offsets = new(StringComparer.Ordinal);
        public bool ReadySent;

        /// <summary>Première image de retour en combat après une resynchronisation.</summary>
        public double? ResumedAt;

        /// <summary>Prochaine tentative de compétence : le joueur attend d'en voir le résultat avant de recommencer.</summary>
        public double NextSkillAt;
        public readonly HashSet<string> Parried = new(StringComparer.Ordinal);
        public readonly List<double> Presses = new();
    }

    public static NetworkMeasureResult Run(GameData data, CombatSetup scenario, NetworkMeasureOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);
        // Revue Codex de la PR #22 : réglages vérifiés avant la simulation (un pas nul ferait tourner la boucle sans fin).
        if (!(options.StepSeconds > 0) || double.IsInfinity(options.StepSeconds) || options.StepSeconds > 1)
            throw new ArgumentOutOfRangeException(nameof(options), $"Pas de temps de {options.StepSeconds} s : entre 0 (exclu) et 1 s.");
        if (options.LatencyMs < 0 || options.JitterMs < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Latence et gigue positives ou nulles.");
        if (options.CutAtSeconds is double cutAtCheck && (!(cutAtCheck >= 0) || double.IsInfinity(cutAtCheck)))
            throw new ArgumentOutOfRangeException(nameof(options), $"Coupure à {cutAtCheck} s : instant positif ou nul et fini.");
        if (!(options.CutSeconds >= 0) || double.IsInfinity(options.CutSeconds))
            throw new ArgumentOutOfRangeException(nameof(options), $"Coupure de {options.CutSeconds} s : durée positive ou nulle et finie.");
        var net = new SimulatedNetwork(options.Seed);
        SimulatedListener listener = net.Listen("salle");
        int tokens = 0;
        var room = new CombatRoom(data, scenario, new RoomOptions { TokenSource = () => (++tokens).ToString("x32", CultureInfo.InvariantCulture) });
        var link = new LinkProfile(options.LatencyMs, options.JitterMs);
        var players = new List<Player>();
        foreach ((string name, string build) in new[] { ("Dam", "build.bastion.rempart"), ("Léa", "build.bastion.gardien") })
        {
            SimulatedConnection connection = net.Connect("salle", link);
            players.Add(new Player
            {
                Client = new NetworkClient(data, connection, name, new BuildChoice(build)),
                Link = connection,
                Timing = options.Timing,
                Aim = new Pcg32(options.Seed, (ulong)(2 * players.Count + 1)),
            });
        }

        double step = options.StepSeconds;
        double? combatStart = null, reconnectAt = null, reconnectedAt = null;
        bool cut = false;
        var tickTimes = new Dictionary<long, double>();
        long lastTick = 0;
        double lagSum = 0;
        long lagSamples = 0;
        double limit = scenario.DurationMs / 1000.0 + 30;
        for (double now = 0; now < limit; now += step)
        {
            net.Advance(step);
            while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
            room.Advance(step);
            if (room.Phase == RoomPhase.Combat)
            {
                combatStart ??= net.Now;
                for (long t = lastTick; t < room.Tick; t++) tickTimes[t] = net.Now;
                lastTick = room.Tick;
            }
            Player first = players[0];
            if (options.CutAtSeconds is double cutAt && !cut && combatStart is double started && net.Now - started >= cutAt)
            {
                cut = true;
                net.Cut(first.Link);
                reconnectAt = net.Now + options.CutSeconds;
            }
            if (reconnectAt is double at && net.Now >= at && first.Client.Status == ClientStatus.Disconnected)
            {
                first.Link = net.Connect("salle", link);
                first.Client.Reconnect(first.Link);
                reconnectAt = null;
                reconnectedAt = net.Now;
            }
            foreach (Player player in players)
            {
                Play(player, net.Now);
                player.Client.Advance(step);
            }
            if (room.Phase == RoomPhase.Combat && players[1].Client.Combat is { } watched)
            {
                lagSum += (room.Tick - watched.Tick) * (double)room.TickMs;
                lagSamples++;
            }
            if (combatStart != null && room.Phase == RoomPhase.Ended && players.All(p => p.Client.Status != ClientStatus.Combat
                && p.Client.Status != ClientStatus.Reconnecting))
                break;
        }

        CombatResult? result = room.LastResult;
        IReadOnlyList<string>? journal = room.LastJournal;
        IReadOnlyList<CombatCommand> commands = journal is null
            ? Array.Empty<CombatCommand>()
            : CombatLogReplayer.ReadSetup(journal[0], data, out _)?.Commands ?? Array.Empty<CombatCommand>();
        var delays = new List<double>();
        for (int i = 0; i < players.Count; i++)
        {
            string seat = players[i].Client.SeatId ?? "";
            var parries = commands.Where(c => c.Actor == seat && c.Action == CombatAction.Parry)
                .Select(c => tickTimes.TryGetValue(c.AtMs / room.TickMs, out double t) ? t : double.NaN).Where(t => !double.IsNaN(t)).ToList();
            // Chaque pression est rapprochée de la première parade appliquée par le serveur après elle, à moins d'une seconde (deux
            // pressions regroupées dans le même envoi partagent la même parade).
            parries.Sort();
            foreach (double press in players[i].Presses)
            {
                int index = parries.BinarySearch(press);
                if (index < 0) index = ~index;
                if (index < parries.Count && parries[index] - press <= 1.0) delays.Add((parries[index] - press) * 1000);
            }
        }
        delays.Sort();

        long Count(Func<CombatantResult, long> value) =>
            result?.Combatants.Where(c => c.Id == FirstSeat || c.Id == "bastion2").Sum(value) ?? 0;
        // Un joueur dont le retour a été refusé, ou pas encore tenté à la fin, s'arrête en plein combat : seules les empreintes des
        // joueurs qui ont vu la fin du combat sont comparées (au moins un).
        var finished = players.Where(p => p.Client.Combat is { IsFinished: true }).ToList();
        bool hashes = journal != null && finished.Count > 0 && finished.All(p => p.Client.Combat is { } c
            && c.Hash == HashingCombatLog.Of(new[] { c.Header }.Concat(journal.Skip(1))));
        NetworkClient firstClient = players[0].Client;
        return new NetworkMeasureResult
        {
            Options = options,
            ParryPresses = players.Sum(p => p.Presses.Count),
            PerfectParries = Count(c => c.PerfectParries),
            FailedParries = Count(c => c.FailedParries),
            CommandsRejected = Count(c => c.CommandsRejected),
            ActionDelayMeanMs = delays.Count == 0 ? 0 : delays.Average(),
            ActionDelayP95Ms = delays.Count == 0 ? 0 : delays[(int)Math.Min(delays.Count - 1, Math.Ceiling(0.95 * delays.Count) - 1)],
            DisplayLagMeanMs = lagSamples == 0 ? 0 : lagSum / lagSamples,
            RttMs = firstClient.SmoothedRttMs ?? 0,
            Desyncs = players.Sum(p => p.Client.Combat?.Desyncs ?? 0),
            Resyncs = players.Sum(p => p.Client.Combat?.Resyncs ?? 0),
            Reconnected = options.CutAtSeconds is null ? null : firstClient.Status != ClientStatus.Failed && firstClient.Combat?.Resyncs > 0,
            ReconnectDelayMs = reconnectedAt is double r && players[0].ResumedAt is double back ? (back - r) * 1000 : null,
            ReconnectError = options.CutAtSeconds is null ? null
                : firstClient.Status == ClientStatus.Failed ? firstClient.Error
                : reconnectedAt is null ? "combat terminé avant la nouvelle connexion." : null,
            HashesMatch = hashes,
            EndReason = result?.EndReason.ToString() ?? "aucun",
            EndTick = result?.EndTick ?? 0,
            Winner = result?.Winner,
        };
    }

    private static void Play(Player player, double now)
    {
        NetworkClient client = player.Client;
        if (client.Status == ClientStatus.Lobby && !player.ReadySent && client.Combat is null)
        {
            client.SetReady(true);
            player.ReadySent = true;
        }
        if (client.Status != ClientStatus.Combat || client.Combat is not { IsFinished: false } combat) return;
        if (combat.Resyncs > 0) player.ResumedAt ??= now;
        PlayerState me = combat.Player();
        if (me.View.IsDead) return;
        var threats = combat.Others()
            .Where(o => o.IsEnemy && !o.View.IsDead && o.View.NextAttack != null && o.View.NextAttackTick is long && o.NextAttackSeconds is double
                && (o.View.CurrentTarget == combat.PlayerId || o.View.NextAttack.Targeting != SkillTargeting.Enemy))
            .ToList();
        double soonest = threats.Count == 0 ? double.PositiveInfinity : threats.Min(o => o.NextAttackSeconds!.Value);
        combat.SetGuard(soonest <= GuardSeconds);
        double lead = ParryLeadSeconds + (player.Timing == ParryTiming.Anticipated ? (client.SmoothedRttMs ?? 0) / 1000 : 0);
        // Temps avant le coup tel que le joueur le perçoit : le compte à rebours du dernier tick reçu, qui continue de s'écouler entre
        // deux ticks (affichage interpolé), à la précision d'une image.
        double elapsed = Math.Min(1.0, combat.TickProgress) * combat.TickSeconds;
        foreach (OtherState threat in threats)
        {
            if (threat.View.NextAttack!.Unblockable) continue;
            string key = threat.Id + "@" + threat.View.NextAttackTick;
            if (player.Parried.Contains(key)) continue;
            if (!player.Offsets.TryGetValue(key, out double offset))
            {
                offset = (player.Aim.NextUInt() / (double)uint.MaxValue * 2 - 1) * AimSpreadSeconds;
                player.Offsets[key] = offset;
            }
            if (threat.NextAttackSeconds!.Value - elapsed > lead + offset + 1e-9) continue;
            player.Parried.Add(key);
            player.Offsets.Remove(key);
            combat.Parry();
            // Deux coups annoncés au même instant : une seule pression (une seule parade part au serveur).
            if (player.Presses.Count == 0 || player.Presses[player.Presses.Count - 1] != now) player.Presses.Add(now);
        }
        if (soonest <= GuardSeconds || now < player.NextSkillAt) return;
        SkillSlot? ready = me.Slots.FirstOrDefault(s => s.Unavailable == null);
        if (ready is null) return;
        // Comme un joueur : une compétence, puis attendre de voir le lancer (un aller-retour et un tick) avant d'en demander une autre ;
        // sinon l'affichage en retard la montre encore prête et le serveur refuse les demandes en double.
        combat.UseSlot(ready.Slot);
        player.NextSkillAt = now + SkillPauseSeconds + (client.SmoothedRttMs ?? 0) / 1000;
    }
}
