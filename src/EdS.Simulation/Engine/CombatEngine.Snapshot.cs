using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Simulation.Randomness;

namespace EdS.Simulation.Engine
{
    /// <summary>
    /// Sous-lot 11.4d : instantané d'état d'un combat entre deux ticks (ARCHITECTURE_MONDE.md, section 5). Texte canonique (lignes JSON
    /// écrites par le moteur, entiers seulement), son empreinte, et le tick à simuler ensuite. Il contient tout l'état qui change en combat :
    /// combattants (présence, ressources, garde, parade, recharges, déclencheurs, résultats, tirages aléatoires, place), altérations,
    /// file d'événements (commandes et tours à venir, échéances, impacts annoncés, réactions reportées), compteurs. Ce qui se déduit du
    /// scénario (statistiques, règles effectives, définitions) n'y est pas : il se relit avec le combat complété au moment de l'instantané
    /// (<see cref="CombatEngine.CompletedSetup"/>), dont il porte l'empreinte.
    /// </summary>
    public sealed class CombatSnapshot
    {
        /// <summary>Version du format de l'instantané (indépendante de celle du journal).</summary>
        public const int FormatVersion = 1;

        /// <summary>Prochain tick à simuler.</summary>
        public long Tick { get; }

        /// <summary>Texte canonique, une ligne par élément, sans saut de ligne final.</summary>
        public string Text { get; }

        /// <summary>Empreinte du texte : <c>sha256:</c> suivi de 64 chiffres hexadécimaux.</summary>
        public string Fingerprint { get; }

        /// <summary>Empreinte de l'en-tête du combat complété à relire avec l'instantané (<see cref="CombatEngine.SetupFingerprint"/>).</summary>
        public string Setup { get; }

        private CombatSnapshot(long tick, string text, string setup)
        {
            Tick = tick;
            Text = text;
            Setup = setup;
            Fingerprint = CombatEngine.Sha256(text);
        }

        internal static CombatSnapshot Create(long tick, string text, string setup) => new CombatSnapshot(tick, text, setup);

        /// <summary>Relit un instantané depuis son texte (première ligne : format, versions, tick) ; l'état n'est vérifié qu'à <see cref="CombatEngine.Restore"/>.</summary>
        /// <exception cref="FormatException">Texte illisible ou d'un autre format.</exception>
        public static CombatSnapshot Parse(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            int end = text.IndexOf('\n');
            SnapshotNode head = SnapshotNode.Parse(end < 0 ? text : text.Substring(0, end));
            if (head["snapshot"].Int != FormatVersion) throw new FormatException($"Instantané au format {head["snapshot"].Long}, ce moteur lit le format {FormatVersion}.");
            return new CombatSnapshot(head["tick"].Long, text, head["setup"].String);
        }
    }

    public sealed partial class CombatEngine
    {
        /// <summary>Empreinte <c>sha256:</c> d'un texte (UTF-8).</summary>
        internal static string Sha256(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var hex = new StringBuilder("sha256:", 7 + 64);
                foreach (byte b in hash) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        /// <summary>Sous-lot 11.4d : empreinte de l'en-tête de journal d'un combat (version du moteur et identité des données comprises).</summary>
        public static string SetupFingerprint(CombatSetup setup) => Sha256(CombatLogHeader.Write(setup ?? throw new ArgumentNullException(nameof(setup))));

        /// <summary>
        /// Sous-lot 11.4d : instantané de l'état courant, entre deux ticks (avant de simuler <see cref="CurrentTick"/>). Le même état donne
        /// le même texte, octet par octet. À relire avec <see cref="CompletedSetup"/> pris au même moment.
        /// </summary>
        /// <exception cref="InvalidOperationException">Combat terminé.</exception>
        public CombatSnapshot Snapshot()
        {
            if (IsFinished) throw new InvalidOperationException("Combat terminé : aucun instantané.");
            CombatSetup completed = CompletedSetup();
            string setup = SetupFingerprint(completed);
            var lines = new List<string>();
            List<(long Tick, CombatPhase Phase, long Sequence, Scheduled Item)> entries = _queue.Entries();
            var cursor = _queue.Cursor;
            long effectiveInitial = _initialEvents < 0 ? -1 : checked(_initialEvents + _joined.Count + _submitted.Count);
            var head = new JsonLineWriter().BeginObject()
                .Field("snapshot", CombatSnapshot.FormatVersion).Field("engineVersion", Version).Field("formatVersion", LogFormatVersion)
                .Field("setup", setup).Field("tick", CurrentTick)
                .Field("seq", _sequence).Field("lastEid", _lastEid).Field("lastStatusInstance", _lastStatusInstance).Field("lastApplication", _lastApplication)
                .Field("deferredPending", _deferredPending).Field("peakWork", _peakWork).Field("peakEvents", _peakEvents).Field("peakPending", _peakPending)
                .Field("lastTickWork", LastTickWork).Field("lastTickEvents", LastTickEvents)
                .Field("commandCount", _commandCount).Field("initialEvents", effectiveInitial);
            head.Name("subscribed").BeginArray();
            for (int k = 0; k < _subscribed.Length; k++)
                if (_subscribed[k]) head.Value(k);
            head.EndArray();
            head.Name("queue").BeginObject().Field("started", cursor.Started).Field("tick", cursor.Tick).Field("phase", (long)cursor.Phase)
                .Field("next", cursor.NextSequence).Field("count", entries.Count).EndObject();
            head.Name("commandsPerTick").BeginArray();
            foreach (var entry in _commandsPerActorTick.Where(e => e.Key.Tick >= CurrentTick)
                .OrderBy(e => e.Key.Tick).ThenBy(e => e.Key.Actor, StringComparer.Ordinal))
                head.BeginArray().Value(entry.Key.Actor).Value(entry.Key.Tick).Value(entry.Value).EndArray();
            head.EndArray();
            var sequenceOf = new Dictionary<Scheduled, long>();
            foreach (var e in entries) sequenceOf[e.Item] = e.Sequence;
            head.Name("announced").BeginArray();
            foreach (AttackImpact impact in _announced) head.Value(sequenceOf[impact]);
            head.EndArray();
            lines.Add(head.EndObject().ToString());

            foreach (Fighter f in _fighters) lines.Add(WriteFighter(f));
            foreach (var e in entries)
            {
                var w = new JsonLineWriter().BeginObject().Field("entry", e.Sequence).Field("tick", e.Tick).Field("phase", (long)e.Phase);
                WriteItem(w, e.Item);
                lines.Add(w.EndObject().ToString());
            }
            return CombatSnapshot.Create(CurrentTick, string.Join("\n", lines), setup);
        }

        // --- Écriture ------------------------------------------------------------------------------------------------------------

        private static void WriteProvenance(JsonLineWriter w, string name, Provenance? p)
        {
            w.Name(name);
            if (p is null)
            {
                w.Value((string?)null);
                return;
            }
            w.BeginObject().Field("root", p.Root).Field("depth", p.Depth);
            w.Names("chain", p.ChainOldestFirst());
            w.Field("trigger", p.Trigger).Field("cause", p.Cause).Field("source", p.Source).Field("instance", p.SourceInstance)
                .Field("generation", p.SourceGeneration).EndObject();
        }

        private static string WriteFighter(Fighter f)
        {
            var w = new JsonLineWriter().BeginObject().Field("fighter", f.Id).Field("presence", (long)f.Presence)
                .Field("arrivedTick", f.ArrivedTick).Field("leftTick", f.LeftTick).Field("leaveCommand", f.LeaveCommand)
                .Field("dead", f.IsDead).Field("health", f.Health).Field("endurance", f.Endurance)
                .Field("regenFromTick", f.RegenFromTick).Field("regenGranted", f.RegenGranted).Field("globalCooldownReadyTick", f.GlobalCooldownReadyTick);
            w.Name("cooldowns").BeginArray();
            foreach (var c in f.CooldownReadyTick.OrderBy(c => c.Key, StringComparer.Ordinal)) w.BeginArray().Value(c.Key).Value(c.Value).EndArray();
            w.EndArray();
            w.Field("guarding", f.Guarding).Field("upkeepDue", f.GuardUpkeepDue).Field("upkeepRemainder", f.GuardUpkeepRemainder).Field("upkeepPaid", f.GuardUpkeepPaid)
                .Field("parryOpen", f.ParryOpen).Field("parryStartTick", f.ParryStartTick).Field("parryEndTick", f.ParryEndTick)
                .Field("parrySucceeded", f.ParrySucceeded).Field("parryRecoveryUntilTick", f.ParryRecoveryUntilTick)
                .Field("resonance", f.Resonance).Field("rampartCharges", f.RampartCharges)
                .Field("nextTurn", f.NextTurn).Field("nextAttack", f.NextAttack).Field("nextAttackTick", f.NextAttackTick);
            WriteProvenance(w, "lethalCause", f.LethalCause);
            Pcg32State random = f.Random.Capture();
            w.Name("random").BeginArray().Value(random.State.ToString("x16", CultureInfo.InvariantCulture))
                .Value(random.Increment.ToString("x16", CultureInfo.InvariantCulture)).EndArray();
            w.Name("body");
            if (f.Body is { } b)
            {
                w.BeginObject().Field("x", b.X).Field("z", b.Z).Field("facingX", b.FacingX).Field("facingZ", b.FacingZ).Field("moving", b.Moving)
                    .Field("directionX", b.DirectionX).Field("directionZ", b.DirectionZ).Field("selected", b.Selected?.Id).EndObject();
            }
            else w.Value((string?)null);
            w.Name("triggers").BeginArray();
            foreach (TriggerSlot slot in f.Triggers) w.Value(slot.ReadyTick);
            w.EndArray();
            CombatantResult r = f.Result;
            w.Name("result").BeginObject().Field("damageDealt", r.DamageDealt).Field("damageTaken", r.DamageTaken).Field("damageAbsorbed", r.DamageAbsorbed)
                .Field("damageTransferred", r.DamageTransferred).Field("damageRedirected", r.DamageRedirected).Field("damageMitigated", r.DamageMitigated)
                .Field("hits", r.Hits).Field("criticalHits", r.CriticalHits).Field("skillsCast", r.SkillsCast).Field("ultimatesCast", r.UltimatesCast)
                .Field("commandsRejected", r.CommandsRejected).Field("blocks", r.Blocks).Field("perfectParries", r.PerfectParries)
                .Field("failedParries", r.FailedParries).Field("guardBreaks", r.GuardBreaks).Field("guardTicks", r.GuardTicks)
                .Field("deathTick", r.DeathTick).Field("arrivedTick", r.ArrivedTick).Field("leftTick", r.LeftTick).EndObject();
            w.Name("statuses").BeginArray();
            foreach (StatusInstance s in f.Statuses.Values.OrderBy(s => s.Id))
            {
                w.BeginObject().Field("id", s.Id).Field("generation", s.Generation).Field("state", s.State).Field("keySource", s.Key.Source)
                    .Field("source", s.Source).Field("untilTick", s.UntilTick).Field("power", s.Power.Raw).Field("nextTick", s.NextTick)
                    .Field("absorb", s.Absorb).Field("appliedSeq", s.AppliedSeq);
                WriteProvenance(w, "depletedBy", s.DepletedBy);
                WriteProvenance(w, "origin", s.Origin);
                w.Name("slots");
                if (s.Slots is null) w.Value((string?)null);
                else
                {
                    w.BeginArray();
                    foreach (TriggerSlot slot in s.Slots) w.Value(slot.ReadyTick);
                    w.EndArray();
                }
                w.EndObject();
            }
            w.EndArray();
            return w.EndObject().ToString();
        }

        private void WriteItem(JsonLineWriter w, Scheduled item)
        {
            switch (item)
            {
                case ArrivalItem a:
                    w.Field("item", "arrival").Field("fighter", a.Fighter.Id);
                    break;
                case CommandItem c:
                    w.Field("item", "command").Field("index", c.Index);
                    break;
                case AssailantTurn t:
                    w.Field("item", "turn").Field("fighter", t.Fighter.Id).Field("turn", t.Turn);
                    break;
                case CooldownExpiry c:
                    w.Field("item", "cooldown").Field("fighter", c.Fighter.Id).Field("skill", c.Skill);
                    break;
                case StateExpiry e:
                    w.Field("item", "expiry").Field("fighter", e.Fighter.Id).Field("state", e.Key.State).Field("keySource", e.Key.Source)
                        .Field("instance", e.Instance).Field("generation", e.Generation);
                    break;
                case DotTick d:
                    w.Field("item", "dot").Field("fighter", d.Fighter.Id).Field("state", d.Key.State).Field("keySource", d.Key.Source).Field("instance", d.Instance);
                    break;
                case ParryWindowEnd p:
                    w.Field("item", "parryEnd").Field("fighter", p.Fighter.Id).Field("startTick", p.StartTick);
                    WriteProvenance(w, "origin", p.Origin);
                    break;
                case AttackImpact i:
                    w.Field("item", "impact").Field("assailant", i.Assailant.Id).Field("attack", i.Attack.Id).Field("target", i.Target?.Id);
                    w.Name("zone");
                    if (i.Zone is { } z)
                        w.BeginObject().Field("shape", (long)z.Shape).Field("originX", z.OriginX).Field("originZ", z.OriginZ).Field("axisX", z.AxisX)
                            .Field("axisZ", z.AxisZ).Field("reach", z.Reach).Field("angle", z.Angle).Field("halfWidth", z.HalfWidth).EndObject();
                    else w.Value((string?)null);
                    w.Field("pulse", i.Pulse).Field("impactTick", i.ImpactTick);
                    WriteProvenance(w, "origin", i.Origin);
                    break;
                case ReactionItem r:
                    w.Field("item", "reaction");
                    WriteSlot(w, r.Slot);
                    WriteObserved(w, r.Event);
                    WriteProvenance(w, "origin", r.Origin);
                    w.Field("deferredFrom", r.DeferredFrom);
                    break;
                default:
                    // Une notification est toujours traitée dans sa phase : elle ne reste jamais en file entre deux ticks.
                    throw new InvalidOperationException($"Élément inattendu en file entre deux ticks : {item.GetType().Name}.");
            }
        }

        /// <summary>
        /// Déclencheur d'une réaction reportée : porteur, origine, définition, indice, instance ; pour un déclencheur porté par une instance
        /// d'altération retirée depuis (réaction qui sera annulée, <c>originGone</c>), son rang et sa recharge aussi.
        /// </summary>
        private static void WriteSlot(JsonLineWriter w, TriggerSlot slot)
        {
            bool detached = slot.Status != null && !(slot.Status.Owner.Statuses.TryGetValue(slot.Status.Key, out StatusInstance? current) && current == slot.Status);
            w.Name("slot").BeginObject().Field("bearer", slot.Bearer.Id).Field("origin", (long)slot.Origin).Field("definition", slot.DefinitionId)
                .Field("index", slot.Index).Field("instance", slot.Instance).Field("detached", detached);
            if (detached) w.Field("rank", slot.Rank).Field("readyTick", slot.ReadyTick).Field("generation", slot.Status!.Generation);
            w.EndObject();
        }

        private static void WriteObserved(JsonLineWriter w, ObservedEvent e)
        {
            w.Name("event").BeginObject().Field("kind", (long)e.Kind).Field("also", e.Also is { } also ? (long)also : (long?)null)
                .Field("source", e.Source).Field("target", e.Target).Field("skill", e.Skill).Field("state", e.State)
                .Field("category", e.Category is { } c ? (long)c : (long?)null).Field("instance", e.Instance)
                .Field("damageType", e.DamageType is { } d ? (long)d : (long?)null).Field("defense", (long)e.Defense).Field("renewed", e.Renewed)
                .Field("resource", e.Resource is { } r ? (long)r : (long?)null).Field("eid", e.Eid).Field("tick", e.Tick).Field("phase", (long)e.Phase);
            WriteProvenance(w, "provenance", e.Provenance);
            w.EndObject();
        }

        // --- Lecture -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Sous-lot 11.4d : reconstruit un moteur dans l'état de <paramref name="snapshot"/>, sans rejouer le passé. <paramref name="setup"/>
        /// est le combat complété au moment de l'instantané (<see cref="CompletedSetup"/>) : même empreinte d'en-tête, sinon refus. Le moteur
        /// rendu continue exactement comme l'original (mêmes lignes de journal, même résultat) ; il écrit dans <paramref name="log"/> la suite
        /// du journal, sans en-tête. Ses commandes soumises et arrivées pilotées repartent de zéro (celles d'avant sont dans
        /// <paramref name="setup"/>). L'instantané relu est réécrit et comparé au texte reçu : un texte non canonique est refusé.
        /// </summary>
        /// <exception cref="FormatException">Instantané illisible, d'un autre moteur, d'un autre combat, ou incohérent.</exception>
        public static CombatEngine Restore(CombatSetup setup, CombatSnapshot snapshot, ICombatLog log)
        {
            if (setup is null) throw new ArgumentNullException(nameof(setup));
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            if (log is null) throw new ArgumentNullException(nameof(log));
            string expected = SetupFingerprint(setup);
            if (snapshot.Setup != expected)
                throw new FormatException($"Instantané d'un autre combat : en-tête {snapshot.Setup}, combat fourni {expected}.");
            CombatEngine engine;
            try
            {
                // Rien n'est écrit au journal à la construction d'un moteur relu.
                engine = new CombatEngine(setup, log, queueBound: true, numericLimits: true, snapshot);
            }
            // Un combat invalide reste une CombatSetupException (erreur de l'appelant) ; le reste vient du texte de l'instantané.
            catch (Exception e) when (e is not CombatSetupException && (e is KeyNotFoundException || e is InvalidCastException
                || e is IndexOutOfRangeException || e is ArgumentException || e is OverflowException || e is InvalidOperationException))
            {
                throw new FormatException($"Instantané incohérent avec le combat : {e.Message}", e);
            }
            string again = engine.Snapshot().Text;
            if (!string.Equals(again, snapshot.Text, StringComparison.Ordinal))
                throw new FormatException("Instantané non canonique : relu puis réécrit, il ne redonne pas le même texte.");
            return engine;
        }

        private void LoadSnapshot(CombatSnapshot snapshot)
        {
            string[] lines = snapshot.Text.Split('\n');
            SnapshotNode head = SnapshotNode.Parse(lines[0]);
            if (head["snapshot"].Int != CombatSnapshot.FormatVersion) throw new FormatException("Format d'instantané inconnu.");
            if (head["engineVersion"].String != Version || head["formatVersion"].Int != LogFormatVersion)
                throw new FormatException($"Instantané du moteur {head["engineVersion"].String} (format {head["formatVersion"].Long}), moteur actuel {Version} (format {LogFormatVersion}).");
            CurrentTick = head["tick"].Long;
            if (CurrentTick < 0 || CurrentTick >= _durationTicks) throw new FormatException($"Tick {CurrentTick} hors du combat.");
            _sequence = head["seq"].Long;
            _lastEid = head["lastEid"].Long;
            _lastStatusInstance = head["lastStatusInstance"].Long;
            _lastApplication = head["lastApplication"].Long;
            _deferredPending = head["deferredPending"].Long;
            _peakWork = head["peakWork"].Long;
            _peakEvents = head["peakEvents"].Long;
            _peakPending = head["peakPending"].Long;
            LastTickWork = head["lastTickWork"].Long;
            LastTickEvents = head["lastTickEvents"].Long;
            _commandCount = head["commandCount"].Int;
            _initialEvents = head["initialEvents"].Long;
            Array.Clear(_subscribed, 0, _subscribed.Length);
            foreach (SnapshotNode k in head["subscribed"].Items) _subscribed[k.Int] = true;
            foreach (SnapshotNode e in head["commandsPerTick"].Items)
                _commandsPerActorTick[(e.Items[0].String, e.Items[1].Long)] = e.Items[2].Int;

            int count = checked((int)head["queue"]["count"].Long);
            if (lines.Length != 1 + _fighters.Count + count) throw new FormatException($"{lines.Length} lignes, {1 + _fighters.Count + count} attendues.");
            for (int i = 0; i < _fighters.Count; i++) LoadFighter(_fighters[i], SnapshotNode.Parse(lines[1 + i]));

            var entries = new List<(long, CombatPhase, long, Scheduled)>();
            var bySequence = new Dictionary<long, Scheduled>();
            for (int i = 0; i < count; i++)
            {
                SnapshotNode e = SnapshotNode.Parse(lines[1 + _fighters.Count + i]);
                Scheduled item = LoadItem(e);
                long sequence = e["entry"].Long;
                entries.Add((e["tick"].Long, Phase(e["phase"].Int), sequence, item));
                bySequence[sequence] = item;
            }
            SnapshotNode queue = head["queue"];
            _queue.Load(entries, queue["started"].Bool, queue["tick"].Long, Phase(queue["phase"].Int), queue["next"].Long);
            foreach (SnapshotNode s in head["announced"].Items) _announced.Add((AttackImpact)bySequence[s.Long]);
        }

        private static CombatPhase Phase(int value) =>
            Enum.IsDefined(typeof(CombatPhase), value) ? (CombatPhase)value : throw new FormatException($"Phase inconnue : {value}.");

        private Fighter FighterOf(SnapshotNode id) => _byId[id.String];

        private static Provenance? ReadProvenance(SnapshotNode n) => n.IsNull ? null
            : Provenance.Restore(n["root"].String, n["depth"].Int, n["chain"].Items.Select(c => c.String).ToList(), n["trigger"].NullableString,
                n["cause"].NullableLong, n["source"].NullableString, n["instance"].NullableLong, n["generation"].NullableInt);

        private void LoadFighter(Fighter f, SnapshotNode n)
        {
            if (n["fighter"].String != f.Id) throw new FormatException($"Combattant « {n["fighter"].String} » à la place de « {f.Id} ».");
            int presence = n["presence"].Int;
            if (!Enum.IsDefined(typeof(Presence), presence)) throw new FormatException($"Présence inconnue : {presence}.");
            f.Presence = (Presence)presence;
            f.ArrivedTick = n["arrivedTick"].NullableLong;
            f.LeftTick = n["leftTick"].NullableLong;
            f.LeaveCommand = n["leaveCommand"].NullableInt;
            f.IsDead = n["dead"].Bool;
            f.Health = n["health"].NullableLong;
            f.Endurance = n["endurance"].Long;
            f.RegenFromTick = n["regenFromTick"].NullableLong;
            f.RegenGranted = n["regenGranted"].Long;
            f.GlobalCooldownReadyTick = n["globalCooldownReadyTick"].Long;
            foreach (SnapshotNode c in n["cooldowns"].Items) f.CooldownReadyTick[c.Items[0].String] = c.Items[1].Long;
            f.Guarding = n["guarding"].Bool;
            f.GuardUpkeepDue = n["upkeepDue"].Long;
            f.GuardUpkeepRemainder = n["upkeepRemainder"].Long;
            f.GuardUpkeepPaid = n["upkeepPaid"].Long;
            f.ParryOpen = n["parryOpen"].Bool;
            f.ParryStartTick = n["parryStartTick"].Long;
            f.ParryEndTick = n["parryEndTick"].Long;
            f.ParrySucceeded = n["parrySucceeded"].Bool;
            f.ParryRecoveryUntilTick = n["parryRecoveryUntilTick"].Long;
            f.Resonance = n["resonance"].Int;
            f.RampartCharges = n["rampartCharges"].Int;
            f.NextTurn = n["nextTurn"].Long;
            f.NextAttack = n["nextAttack"].Int;
            if (f.Assailant != null && (f.NextAttack < 0 || f.NextAttack >= f.Assailant.Attacks.Count)) throw new FormatException($"Attaque suivante invalide pour « {f.Id} ».");
            f.NextAttackTick = n["nextAttackTick"].NullableLong;
            f.LethalCause = ReadProvenance(n["lethalCause"]);
            IReadOnlyList<SnapshotNode> random = n["random"].Items;
            f.Random = Pcg32.Restore(new Pcg32State(ulong.Parse(random[0].String, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
                ulong.Parse(random[1].String, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)));
            SnapshotNode body = n["body"];
            if (body.IsNull != (f.Body is null)) throw new FormatException($"Place de « {f.Id} » incohérente avec le combat.");
            if (f.Body is { } b)
            {
                b.X = body["x"].Long;
                b.Z = body["z"].Long;
                b.FacingX = body["facingX"].Long;
                b.FacingZ = body["facingZ"].Long;
                b.Moving = body["moving"].Bool;
                b.DirectionX = body["directionX"].Long;
                b.DirectionZ = body["directionZ"].Long;
                b.Selected = body["selected"].IsNull ? null : FighterOf(body["selected"]);
            }
            IReadOnlyList<SnapshotNode> triggers = n["triggers"].Items;
            if (triggers.Count != f.Triggers.Count) throw new FormatException($"Déclencheurs de « {f.Id} » incohérents avec le combat.");
            for (int k = 0; k < triggers.Count; k++) f.Triggers[k].ReadyTick = triggers[k].Long;
            SnapshotNode r = n["result"];
            CombatantResult result = f.Result;
            result.DamageDealt = r["damageDealt"].Long;
            result.DamageTaken = r["damageTaken"].Long;
            result.DamageAbsorbed = r["damageAbsorbed"].Long;
            result.DamageTransferred = r["damageTransferred"].Long;
            result.DamageRedirected = r["damageRedirected"].Long;
            result.DamageMitigated = r["damageMitigated"].Long;
            result.Hits = r["hits"].Long;
            result.CriticalHits = r["criticalHits"].Long;
            result.SkillsCast = r["skillsCast"].Long;
            result.UltimatesCast = r["ultimatesCast"].Long;
            result.CommandsRejected = r["commandsRejected"].Long;
            result.Blocks = r["blocks"].Long;
            result.PerfectParries = r["perfectParries"].Long;
            result.FailedParries = r["failedParries"].Long;
            result.GuardBreaks = r["guardBreaks"].Long;
            result.GuardTicks = r["guardTicks"].Long;
            result.DeathTick = r["deathTick"].NullableLong;
            result.ArrivedTick = r["arrivedTick"].NullableLong;
            result.LeftTick = r["leftTick"].NullableLong;
            foreach (SnapshotNode s in n["statuses"].Items)
            {
                StatusDefinition definition = _statuses[s["state"].String];
                var key = new StatusKey(definition.Id, s["keySource"].NullableString);
                var status = new StatusInstance
                {
                    Id = s["id"].Long,
                    Generation = s["generation"].Int,
                    Definition = definition,
                    Key = key,
                    Owner = f,
                    Source = s["source"].NullableString,
                    UntilTick = s["untilTick"].Long,
                    Power = Fixed.FromRaw(s["power"].Long),
                    NextTick = s["nextTick"].Long,
                    Absorb = s["absorb"].Long,
                    DepletedBy = ReadProvenance(s["depletedBy"]),
                    AppliedSeq = s["appliedSeq"].Long,
                    Origin = ReadProvenance(s["origin"]) ?? throw new FormatException("Provenance d'altération absente."),
                };
                status.Slots = StatusSlots(f, status);
                SnapshotNode slots = s["slots"];
                if (slots.IsNull != (status.Slots is null) || (status.Slots != null && slots.Items.Count != status.Slots.Length))
                    throw new FormatException($"Déclencheurs de l'altération {status.Id} incohérents avec le catalogue.");
                if (status.Slots != null)
                    for (int k = 0; k < status.Slots.Length; k++) status.Slots[k].ReadyTick = slots.Items[k].Long;
                f.Statuses.Add(key, status);
            }
        }

        private Scheduled LoadItem(SnapshotNode e)
        {
            switch (e["item"].String)
            {
                case "arrival":
                    return new ArrivalItem { Fighter = FighterOf(e["fighter"]) };
                case "command":
                {
                    int index = e["index"].Int;
                    return new CommandItem { Index = index, Command = _setup.Commands[index] };
                }
                case "turn":
                    return new AssailantTurn { Fighter = FighterOf(e["fighter"]), Turn = e["turn"].Long };
                case "cooldown":
                    return new CooldownExpiry { Fighter = FighterOf(e["fighter"]), Skill = e["skill"].String };
                case "expiry":
                    return new StateExpiry
                    {
                        Fighter = FighterOf(e["fighter"]), Key = new StatusKey(e["state"].String, e["keySource"].NullableString),
                        Instance = e["instance"].Long, Generation = e["generation"].Int,
                    };
                case "dot":
                    return new DotTick { Fighter = FighterOf(e["fighter"]), Key = new StatusKey(e["state"].String, e["keySource"].NullableString), Instance = e["instance"].Long };
                case "parryEnd":
                    return new ParryWindowEnd { Fighter = FighterOf(e["fighter"]), StartTick = e["startTick"].Long, Origin = ReadProvenance(e["origin"])! };
                case "impact":
                {
                    Fighter assailant = FighterOf(e["assailant"]);
                    AssailantAttack attack = assailant.Assailant!.Attacks.Single(a => a.Id == e["attack"].String);
                    SnapshotNode z = e["zone"];
                    return new AttackImpact
                    {
                        Assailant = assailant,
                        Attack = attack,
                        Target = e["target"].IsNull ? null : FighterOf(e["target"]),
                        Zone = z.IsNull ? null : new AttackZone((AttackShape)z["shape"].Int, z["originX"].Long, z["originZ"].Long, z["axisX"].Long, z["axisZ"].Long,
                            z["reach"].Long, z["angle"].Int, z["halfWidth"].Long),
                        Pulse = e["pulse"].Long,
                        ImpactTick = e["impactTick"].Long,
                        Origin = ReadProvenance(e["origin"])!,
                    };
                }
                case "reaction":
                    return new ReactionItem
                    {
                        Slot = LoadSlot(e["slot"]),
                        Event = LoadObserved(e["event"]),
                        Origin = ReadProvenance(e["origin"])!,
                        DeferredFrom = e["deferredFrom"].NullableLong,
                    };
                default:
                    throw new FormatException($"Élément de file inconnu : {e["item"].String}.");
            }
        }

        /// <summary>
        /// Déclencheur d'une réaction reportée : permanent (liste du porteur), porté par une altération active (la même instance), ou porté
        /// par une instance retirée depuis : il est alors recréé détaché, avec une instance hors du porteur, que <see cref="Live"/> refuse.
        /// </summary>
        private TriggerSlot LoadSlot(SnapshotNode n)
        {
            Fighter bearer = FighterOf(n["bearer"]);
            var origin = (TriggerOrigin)n["origin"].Int;
            string definitionId = n["definition"].String;
            int index = n["index"].Int;
            long instance = n["instance"].Long;
            if (instance == 0)
                return bearer.Triggers.Single(s => s.Origin == origin && s.DefinitionId == definitionId && s.Index == index);
            if (!n["detached"].Bool)
            {
                StatusInstance live = bearer.Statuses.Values.Single(s => s.Id == instance);
                return live.Slots![index];
            }
            StatusDefinition definition = _statuses[definitionId];
            var gone = new StatusInstance
            {
                Id = instance, Generation = n["generation"].Int, Definition = definition, Key = new StatusKey(definitionId, null), Owner = bearer,
                Origin = Provenance.NewRoot("detached", null),
            };
            TriggerSlot slot = Slot(bearer, origin, definitionId, index, n["rank"].Int, definition.Triggers[index], gone);
            slot.ReadyTick = n["readyTick"].Long;
            return slot;
        }

        private static ObservedEvent LoadObserved(SnapshotNode n) => new ObservedEvent
        {
            Kind = (TriggerEvent)n["kind"].Int,
            Also = n["also"].IsNull ? (TriggerEvent?)null : (TriggerEvent)n["also"].Int,
            Source = n["source"].NullableString,
            Target = n["target"].NullableString,
            Skill = n["skill"].NullableString,
            State = n["state"].NullableString,
            Category = n["category"].IsNull ? (StatusCategory?)null : (StatusCategory)n["category"].Int,
            Instance = n["instance"].NullableLong,
            DamageType = n["damageType"].IsNull ? (Domain.Stats.DamageType?)null : (Domain.Stats.DamageType)n["damageType"].Int,
            Defense = (HitDefense)n["defense"].Int,
            Renewed = n["renewed"].Bool,
            Resource = n["resource"].IsNull ? (TriggerResource?)null : (TriggerResource)n["resource"].Int,
            Eid = n["eid"].Long,
            Tick = n["tick"].Long,
            Phase = (CombatPhase)n["phase"].Int,
            Provenance = ReadProvenance(n["provenance"])!,
            Candidates = new List<TriggerSlot>(),   // lus seulement à la notification, déjà traitée
        };
    }
}
