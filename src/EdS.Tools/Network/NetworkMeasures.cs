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

    /// <summary>Pas du temps virtuel (une image de 10 ms) ; de <see cref="NetworkMeasures.MinStepSeconds"/> à 1 s.</summary>
    public double StepSeconds { get; init; } = 0.01;
}

/// <summary>Mesures d'une partie à deux joueurs sur le réseau simulé.</summary>
public sealed class NetworkMeasureResult
{
    public NetworkMeasureOptions Options { get; init; } = new();

    /// <summary>Places attribuées aux deux joueurs mesurés (identifiants des personnages), dans l'ordre d'arrivée.</summary>
    public IReadOnlyList<string> Seats { get; init; } = Array.Empty<string>();

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

    /// <summary>Tous les délais d'action mesurés, triés (revue Codex de la PR #22 : agrégés sur plusieurs graines par le rapport).</summary>
    public IReadOnlyList<double> ActionDelaysMs { get; init; } = Array.Empty<double>();

    /// <summary>Durée d'un tick du combat mesuré (règles des données), en millisecondes.</summary>
    public int TickMs { get; init; }

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

    /// <summary>
    /// Pas de temps le plus court accepté (1 ms, revue Codex de la PR #22, tour 4) : en dessous, une mesure demanderait des millions
    /// d'images sans rien gagner (le réseau simulé compte en millisecondes).
    /// </summary>
    public const double MinStepSeconds = 0.001;

    /// <summary>
    /// Latence et gigue d'un sens les plus grandes acceptées (5 s chacune, revue Codex de la PR #22, tour 7) : au-delà, la salle refuse de
    /// toute façon la présentation (5 s au plus) ; les bornes gardent les budgets d'images petits et sans débordement.
    /// </summary>
    public const int MaxDelayMs = 5_000;

    /// <summary>Coupure la plus longue acceptée (120 s, tour 7) : le serveur ne garde la place que 30 s.</summary>
    public const double MaxCutSeconds = 120;


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

        /// <summary>Joueur dont la connexion a été coupée par la mesure.</summary>
        public bool WasCut;
    }

    public static NetworkMeasureResult Run(GameData data, CombatSetup scenario, NetworkMeasureOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);
        // Revue Codex de la PR #22 : réglages vérifiés avant la simulation (un pas nul ferait tourner la boucle sans fin).
        if (!(options.StepSeconds >= MinStepSeconds) || options.StepSeconds > 1)
            throw new ArgumentOutOfRangeException(nameof(options), $"Pas de temps de {options.StepSeconds} s : de {MinStepSeconds} à 1 s.");
        if (options.LatencyMs < 0 || options.JitterMs < 0 || options.LatencyMs > MaxDelayMs || options.JitterMs > MaxDelayMs)
            throw new ArgumentOutOfRangeException(nameof(options), $"Latence et gigue de 0 à {MaxDelayMs} ms.");
        if (options.CutAtSeconds is double cutAtCheck && (!(cutAtCheck >= 0) || double.IsInfinity(cutAtCheck)))
            throw new ArgumentOutOfRangeException(nameof(options), $"Coupure à {cutAtCheck} s : instant positif ou nul et fini.");
        // Revue Codex de la PR #22 (tour 6) : une coupure doit tomber pendant le combat, sinon la mesure annoncerait un retour manqué.
        if (options.CutAtSeconds is double cutAtLimit && cutAtLimit * 1000 >= scenario.DurationMs)
            throw new ArgumentOutOfRangeException(nameof(options), $"Coupure à {cutAtLimit} s : avant la fin du combat ({scenario.DurationMs / 1000.0} s).");
        if (!(options.CutSeconds >= 0) || options.CutSeconds > MaxCutSeconds)
            throw new ArgumentOutOfRangeException(nameof(options), $"Coupure de {options.CutSeconds} s : de 0 à {MaxCutSeconds} s.");
        // Revue Codex de la PR #22 : un moment de parade inconnu serait calculé d'une façon et affiché d'une autre.
        if (options.Timing != ParryTiming.OnSight && options.Timing != ParryTiming.Anticipated)
            throw new ArgumentOutOfRangeException(nameof(options), $"Moment de parade inconnu : {(int)options.Timing}.");
        var net = new SimulatedNetwork(options.Seed);
        SimulatedListener listener = net.Listen("salle");
        int tokens = 0;
        var roomOptions = new RoomOptions { TokenSource = () => (++tokens).ToString("x32", CultureInfo.InvariantCulture) };
        var room = new CombatRoom(data, scenario, roomOptions);
        // Revue Codex de la PR #22 (tour 5) : la salle simule au plus MaxTicksPerAdvance ticks par appel et abandonne le reste ;
        // un pas plus long ferait perdre du temps de combat et rendrait un résultat partiel.
        if (options.StepSeconds * 1000 > roomOptions.MaxTicksPerAdvance * (double)room.TickMs)
            throw new ArgumentOutOfRangeException(nameof(options),
                $"Pas de temps de {options.StepSeconds} s : au plus {roomOptions.MaxTicksPerAdvance} ticks de {room.TickMs} ms par image.");
        // Deux joueurs scriptés : le scénario doit avoir exactement deux places (personnages Bastion).
        if (room.SeatIds.Count != 2)
            throw new ArgumentException($"Scénario à {room.SeatIds.Count} personnage(s) : les mesures du réseau en demandent deux (deux joueurs).", nameof(scenario));
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
        // Revue Codex de la PR #22 (tours 4 et 6) : un nombre entier d'images, qui finit toujours. Deux budgets : l'entrée dans la salle
        // (présentation, accueil, prêt : quelques allers-retours, bornés d'après la latence), puis le combat, compté depuis son début
        // (durée du scénario, plus 30 s pour les fins de coupure).
        double startSeconds = 10 + 8.0 * ((double)options.LatencyMs + options.JitterMs) / 1000;   // 90 s au plus (bornes ci-dessus)
        // Le combat dure le scénario, ou jusqu'au retour d'un joueur coupé s'il est plus tard ; plus 30 s.
        double combatSeconds = Math.Max(scenario.DurationMs / 1000.0, options.CutAtSeconds is double cutAtEnd ? cutAtEnd + options.CutSeconds : 0) + 30;
        long frameLimit = checked((long)Math.Ceiling(startSeconds / step));
        bool combatBudget = false;
        for (long frame = 0; frame < frameLimit; frame++)
        {
            net.Advance(step);
            while (listener.TryAccept(out INetConnection connection)) room.Accept(connection);
            room.Advance(step);
            // Tour 7 : deux joueurs refusés avant le combat (présentation trop lente, par exemple) : inutile d'attendre la fin du budget.
            if (combatStart is null && players.All(p => p.Client.Status == ClientStatus.Failed)) break;
            if (room.Phase == RoomPhase.Combat)
            {
                combatStart ??= net.Now;
                if (!combatBudget)
                {
                    combatBudget = true;
                    frameLimit = checked(frame + 1 + (long)Math.Ceiling(combatSeconds / step));
                }
                for (long t = lastTick; t < room.Tick; t++) tickTimes[t] = net.Now;
                lastTick = room.Tick;
            }
            Player first = players[0];
            if (options.CutAtSeconds is double cutAt && !cut && combatStart is double started && net.Now - started >= cutAt)
            {
                cut = true;
                first.WasCut = true;
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
            if (combatStart != null && room.Phase == RoomPhase.Ended && players.All(Done)) break;
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

        if (combatStart is null)
            throw new InvalidOperationException("Le combat n'a pas commencé : " + string.Join(" ; ", players.Select(p => $"{p.Client.Name} : {p.Client.Status} {p.Client.Error}")));
        // Revue Codex de la PR #22 (tour 6) : jamais de résultat partiel rendu comme une mesure. Combat fini chez le serveur, clients sortis
        // du combat, coupure demandée bien faite : sinon, erreur.
        if (result is null || !players.All(Done))
            throw new InvalidOperationException("Mesure incomplète : combat pas terminé dans le temps imparti (" +
                string.Join(" ; ", players.Select(p => $"{p.Client.Name} : {p.Client.Status}")) + ").");
        if (options.CutAtSeconds != null && !cut)
            throw new InvalidOperationException($"Mesure incomplète : coupure à {options.CutAtSeconds} s de combat jamais atteinte.");
        // Revue Codex de la PR #22 : compteurs des places réellement attribuées aux joueurs mesurés (quels que soient leurs identifiants).
        var seats = new HashSet<string>(players.Select(p => p.Client.SeatId!), StringComparer.Ordinal);
        long Count(Func<CombatantResult, long> value) =>
            result?.Combatants.Where(c => seats.Contains(c.Id)).Sum(value) ?? 0;
        // Un joueur dont le retour a été refusé, ou pas encore tenté à la fin, s'arrête en plein combat : seules les empreintes des
        // joueurs qui ont vu la fin du combat sont comparées (au moins un).
        var finished = players.Where(p => p.Client.Combat is { IsFinished: true }).ToList();
        bool hashes = journal != null && finished.Count > 0 && finished.All(p => p.Client.Combat is { } c
            && c.Hash == HashingCombatLog.Of(new[] { c.Header }.Concat(journal.Skip(1))));
        NetworkClient firstClient = players[0].Client;
        return new NetworkMeasureResult
        {
            Options = options,
            Seats = players.Select(p => p.Client.SeatId!).ToArray(),
            ParryPresses = players.Sum(p => p.Presses.Count),
            PerfectParries = Count(c => c.PerfectParries),
            FailedParries = Count(c => c.FailedParries),
            CommandsRejected = Count(c => c.CommandsRejected),
            ActionDelayMeanMs = delays.Count == 0 ? 0 : delays.Average(),
            ActionDelayP95Ms = Percentile95(delays),
            ActionDelaysMs = delays.AsReadOnly(),
            TickMs = room.TickMs,
            DisplayLagMeanMs = lagSamples == 0 ? 0 : lagSum / lagSamples,
            RttMs = firstClient.SmoothedRttMs ?? 0,
            Desyncs = players.Sum(p => p.Client.Combat?.Desyncs ?? 0),
            Resyncs = players.Sum(p => p.Client.Combat?.Resyncs ?? 0),
            Reconnected = options.CutAtSeconds is null ? null : firstClient.Status != ClientStatus.Failed && firstClient.Combat?.Resyncs > 0,
            ReconnectDelayMs = reconnectedAt is double r && players[0].ResumedAt is double back ? (back - r) * 1000 : null,
            ReconnectError = options.CutAtSeconds is null ? null
                : firstClient.Status == ClientStatus.Failed ? firstClient.Error
                : reconnectedAt is null ? "combat terminé avant la nouvelle connexion."
                : firstClient.Combat?.Resyncs > 0 ? null
                : "combat terminé pendant la coupure.",
            HashesMatch = hashes,
            EndReason = result?.EndReason.ToString() ?? "aucun",
            EndTick = result?.EndTick ?? 0,
            Winner = result?.Winner,
        };
    }

    /// <summary>
    /// Revue Codex de la PR #22 (tour 6) : un joueur a fini sa partie quand il a vu la fin du combat (revenu dans la salle, combat local
    /// terminé), quand son retour a été refusé, ou, coupé, quand il revient dans la salle après la fin. Avec une forte latence, le serveur
    /// peut avoir fini le combat avant que les joueurs n'en voient le début : il faut les attendre.
    /// </summary>
    private static bool Done(Player p) =>
        p.Client.Status == ClientStatus.Failed
        || (p.Client.Status == ClientStatus.Lobby && p.Client.Combat is { } combat && (combat.IsFinished || p.WasCut));

    /// <summary>95e centile (rang supérieur) d'une liste triée ; 0 si elle est vide.</summary>
    public static double Percentile95(IReadOnlyList<double> sorted)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        return sorted.Count == 0 ? 0 : sorted[(int)Math.Min(sorted.Count - 1, Math.Ceiling(0.95 * sorted.Count) - 1)];
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
