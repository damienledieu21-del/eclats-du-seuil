using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EdS.Domain.Builds;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Domain.Stats;
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
        /// <summary>Version du format de l'instantané (indépendante de celle du journal) : 2 depuis le sous-lot 11.5 (engagement des ennemis).</summary>
        public const int FormatVersion = 2;

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
                .Field("nextTurn", f.NextTurn).Field("nextAttack", f.NextAttack).Field("nextAttackTick", f.NextAttackTick)
                .Field("engaged", f.Engaged).Field("struck", f.Struck).Field("taunted", f.Taunted);
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
            var subscribed = new List<int>(head["subscribed"].Items.Select(k => k.Int));
            foreach (SnapshotNode e in head["commandsPerTick"].Items)
                _commandsPerActorTick[(e.Items[0].String, e.Items[1].Long)] = e.Items[2].Int;

            int count = checked((int)head["queue"]["count"].Long);
            if (lines.Length != 1 + _fighters.Count + count) throw new FormatException($"{lines.Length} lignes, {1 + _fighters.Count + count} attendues.");
            for (int i = 0; i < _fighters.Count; i++) LoadFighter(_fighters[i], SnapshotNode.Parse(lines[1 + i]));
            // Revue Codex de la PR #32 (3e tour) : les abonnements se déduisent des combattants entrés (présents, morts compris, ou partis)
            // et du catalogue ; ils sont recalculés, et la liste reçue doit être exactement celle-là.
            Array.Clear(_subscribed, 0, _subscribed.Length);
            foreach (Fighter f in _fighters)
                if (f.Presence == Presence.Present || f.Presence == Presence.Left) AddSubscriptions(f);
            foreach (StatusDefinition definition in _statuses.Definitions)
                foreach (TriggerDefinition trigger in definition.Triggers) _subscribed[(int)trigger.On] = true;
            if (!subscribed.SequenceEqual(Enumerable.Range(0, _subscribed.Length).Where(k => _subscribed[k])))
                throw new FormatException("Abonnements incohérents avec les combattants entrés et le catalogue.");

            var entries = new List<(long Tick, CombatPhase Phase, long Sequence, Scheduled Item)>();
            var bySequence = new Dictionary<long, Scheduled>();
            for (int i = 0; i < count; i++)
            {
                SnapshotNode e = SnapshotNode.Parse(lines[1 + _fighters.Count + i]);
                Scheduled item = LoadItem(e);
                long sequence = e["entry"].Long;
                entries.Add((e["tick"].Long, Phase(e["phase"].Int), sequence, item));
                bySequence[sequence] = item;
            }
            // Revue Codex de la PR #32 : la file est relue telle qu'entre deux ticks. Curseur : jamais demandé avant le premier tick ; sinon
            // à la fin (endOfTick) du tick précédent. Éléments : tous à venir (tick repris ou suivants).
            SnapshotNode queue = head["queue"];
            bool started = queue["started"].Bool;
            long cursorTick = queue["tick"].Long;
            CombatPhase cursorPhase = Phase(queue["phase"].Int);
            bool boundary = CurrentTick == 0
                ? !started && cursorTick == 0 && cursorPhase == CombatPhase.Setup
                : started && cursorTick == CurrentTick - 1 && cursorPhase == CombatPhase.EndOfTick;
            if (!boundary) throw new FormatException($"Curseur de file ({cursorTick}, {cursorPhase}) incohérent avec un instantané pris avant le tick {CurrentTick}.");
            foreach (var (tick, _, sequence, _) in entries)
                if (tick < CurrentTick) throw new FormatException($"Élément de file {sequence} au tick {tick}, déjà passé (instantané pris avant le tick {CurrentTick}).");
            foreach (var key in _commandsPerActorTick.Keys)
                if (key.Tick < CurrentTick) throw new FormatException($"Commandes de « {key.Actor} » comptées au tick {key.Tick}, déjà passé.");
            _queue.Load(entries, started, cursorTick, cursorPhase, queue["next"].Long);
            // Revue Codex de la PR #32 (2e tour) : chaque impact en file est une attaque annoncée, citée une seule fois, et inversement.
            var impacts = new HashSet<long>(entries.Where(x => x.Item is AttackImpact).Select(x => x.Sequence));
            var announced = new HashSet<long>();
            foreach (SnapshotNode s in head["announced"].Items)
            {
                long sequence = s.Long;
                if (!impacts.Contains(sequence) || !announced.Add(sequence))
                    throw new FormatException($"Attaque annoncée {sequence} : pas un impact en file, ou citée deux fois.");
                _announced.Add((AttackImpact)bySequence[sequence]);
            }
            if (announced.Count != impacts.Count) throw new FormatException("Impact en file absent des attaques annoncées.");
            ValidateLoaded(entries);
        }

        /// <summary>
        /// Revue Codex de la PR #32 (2e tour) : un instantané retouché peut se réécrire à l'identique (contrôle canonique) tout en décrivant un
        /// état que le moteur n'atteint jamais entre deux ticks ; il est refusé ici, avant tout tick. Contrôles : compteurs ; présence, arrivée
        /// et départ ; points de vie (absents si et seulement si infinis, dans [0, max], nuls si et seulement si mort) ; ressources dans leurs
        /// bornes ; ni altération, ni garde, ni parade, ni tour annoncé, ni marche chez un mort ou un absent ; altérations uniques, déjà
        /// numérotées, rangées sous la clé de leur cumul, pas encore échues ; place dans l'aire et hors du décor, orientation unitaire ;
        /// éléments de file à leur phase et d'un combattant du bon genre ; commandes à venir toutes en file, au tick de leur instant ;
        /// réactions en file toutes reportées du tick précédent ; zones verrouillées aux dimensions de leur attaque ; sous-lot 11.5 :
        /// engagement seulement d'un ennemi qui a un comportement, d'emblée sans distance d'engagement.
        /// </summary>
        /// <summary>
        /// Revue Codex de la PR #32 (7e tour) : plafond des compteurs cumulés relus (bilan, entretien, absorption, puissance) : très au-delà
        /// de tout combat (2⁵³ environ), il laisse aux incréments, tous vérifiés, une marge de plus de mille fois sa valeur.
        /// </summary>
        private const long CounterCeiling = long.MaxValue / 1024;

        /// <summary>
        /// 9e tour : une échéance (ou un tick de DOT) cite une instance numérotée par le moteur ; si l'altération est encore là, c'est sa clé,
        /// et une échéance n'en cite pas une génération à venir.
        /// </summary>
        private bool Numbered(Fighter f, StatusKey key, long instance, int? generation)
        {
            if (instance < 1 || instance > _lastStatusInstance || (generation is int g && g < 1)) return false;
            StatusInstance? live = f.Statuses.Values.FirstOrDefault(s => s.Id == instance);
            return live is null || (live.Key.Equals(key) && (generation is null || generation <= live.Generation));
        }

        private void ValidateLoaded(List<(long Tick, CombatPhase Phase, long Sequence, Scheduled Item)> entries)
        {
            static void Require(bool ok, string message)
            {
                if (!ok) throw new FormatException(message);
            }

            Require(_sequence >= 0 && _lastEid >= 0 && _lastStatusInstance >= 0 && _lastApplication >= 0 && _peakWork >= 0 && _peakEvents >= 0
                && _peakPending >= entries.Count && entries.Count <= _rules.MaxPendingEvents && LastTickWork >= 0 && LastTickEvents >= 0
                && (_initialEvents == -1 || _initialEvents == _setup.InitialEventCount()), "Compteurs de l'instantané impossibles.");
            // 14e tour : les mesures de charge restent sous les budgets (une réserve qui les dépasserait arrête le combat avant de compter), et
            // le dernier tick ne dépasse pas les pics (ils sont relevés avant lui).
            Require(_peakWork <= _rules.MaxWorkPerTick && _peakEvents <= _rules.MaxEventsPerTick && _peakPending <= _rules.MaxPendingEvents
                && LastTickWork <= _peakWork && LastTickEvents <= _peakEvents, "Mesures de charge de l'instantané au-delà des budgets.");
            // Revue Codex de la PR #32 (6e tour) : chaque ligne écrite coûte au moins une unité de travail ; la suite du combat (ticks restants,
            // plus la fin et une large marge) ne peut donc pas faire déborder les numéros de ligne, d'événement, d'altération et
            // d'application. Un compteur plus grand est refusé ; les incréments sont de toute façon vérifiés.
            long margin = checked((Math.Max(0, _durationTicks - CurrentTick) + 2) * Math.Max(_rules.MaxWorkPerTick, _rules.MaxEventsPerTick) + 1_000_000);
            long ceiling = long.MaxValue - margin;
            // 7e tour : le prochain numéro de la file aussi (chaque mise en file coûte une unité de travail).
            Require(_sequence <= ceiling && _lastEid <= ceiling && _lastStatusInstance <= ceiling && _lastApplication <= ceiling
                && _queue.Cursor.NextSequence <= ceiling, "Compteurs de l'instantané trop grands pour la suite du combat.");
            // 15e tour : chaque numéro d'événement a pris une ligne : le numéro de ligne n'est jamais en deçà du dernier événement.
            Require(_sequence >= _lastEid, $"Numéro de ligne {_sequence} en deçà du dernier événement {_lastEid}.");
            // Chaque nouvelle instance d'altération prend aussi un rang d'application : il n'y a jamais plus d'instances que de rangs.
            Require(_lastStatusInstance <= _lastApplication, $"{_lastStatusInstance} instances d'altération pour {_lastApplication} rangs d'application.");
            // 11e tour : le prochain numéro de la file n'est jamais négatif, même file vide (EventQueue.Load ne le recoupe qu'avec ses éléments).
            Require(_queue.Cursor.NextSequence >= 0, "Prochain numéro de la file négatif.");
            // Revue Codex de la PR #32 (3e tour) : le combat complété porte toutes les commandes comptées ; leur nombre et leur compte par
            // acteur et par tick à venir s'en déduisent exactement.
            Require(_commandCount == _setup.Commands.Count, $"{_commandCount} commandes comptées, {_setup.Commands.Count} dans le combat complété.");
            var expectedPerTick = new Dictionary<(string Actor, long Tick), int>();
            foreach (CombatCommand command in _setup.Commands)
            {
                var key = (command.Actor, command.AtMs / _tickMs);
                if (key.Item2 >= CurrentTick) expectedPerTick[key] = expectedPerTick.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            Require(expectedPerTick.Count == _commandsPerActorTick.Count
                && expectedPerTick.All(pair => _commandsPerActorTick.TryGetValue(pair.Key, out int n) && n == pair.Value),
                "Commandes comptées par acteur et par tick incohérentes avec le combat complété.");
            Require(_maxLoadedEid <= _lastEid && _maxLoadedInstance <= _lastStatusInstance,
                $"Provenance citant l'événement {_maxLoadedEid} ou l'altération {_maxLoadedInstance}, au-delà des derniers numérotés.");

            // File : chaque élément à sa phase, d'un combattant du bon genre.
            var commands = new HashSet<int>();
            long reactions = 0;
            var arrivals = new List<Fighter>();
            // Revue Codex de la PR #32 (9e tour) : une échéance périmée (altération renouvelée ou retirée, parade fermée plus tôt, tour d'un
            // mort) reste en file sans effet, mais elle coûte du travail quand elle tombe : chacune est unique, et ses numéros sont de ceux
            // que le moteur a donnés. Des copies ne peuvent donc pas gonfler la file.
            var expiryKeys = new HashSet<(Fighter, StatusKey, long, int)>();
            var dotTicks = new HashSet<(Fighter, StatusKey, long)>();
            var parryEnds = new HashSet<(Fighter, long)>();
            var reacted = new HashSet<(TriggerSlot, long?, string)>();
            foreach (var (tick, phase, sequence, item) in entries)
            {
                string at = $"Élément de file {sequence} ({item.GetType().Name}, tick {tick}, phase {phase})";
                switch (item)
                {
                    case ArrivalItem arrival:
                        Require(phase == CombatPhase.Setup && arrival.Fighter.Presence == Presence.Pending, $"{at} : arrivée d'un combattant qui n'est pas attendu.");
                        // Revue Codex de la PR #32 (4e tour) : au tick prévu par le combat complété.
                        Require(arrival.Fighter.Setup.ArrivalMs is long arrivalMs && tick == arrivalMs / _tickMs, $"{at} : arrivée hors du tick prévu par le combat.");
                        arrivals.Add(arrival.Fighter);
                        break;
                    case CommandItem command:
                        Require(phase == CombatPhase.Commands && tick == command.Command.AtMs / _tickMs && commands.Add(command.Index), $"{at} : commande hors de son tick ou en double.");
                        break;
                    case AssailantTurn turn:
                        Require(phase == CombatPhase.Commands && turn.Fighter.Assailant != null && turn.Turn >= 0 && turn.Turn < turn.Fighter.NextTurn,
                            $"{at} : tour impossible.");
                        break;
                    case CooldownExpiry cooldown:
                        // Revue Codex de la PR #32 (8e tour) : entre deux ticks, chaque échéance de recharge est celle d'une recharge en cours
                        // (une échéance périmée ne survit pas à son tick : relancer une compétence n'est possible qu'à partir de son échéance).
                        Require(phase == CombatPhase.Expirations && cooldown.Fighter.Character != null
                            && cooldown.Fighter.CooldownReadyTick.TryGetValue(cooldown.Skill, out long readyTick) && readyTick == tick,
                            $"{at} : recharge impossible ou sans recharge en cours.");
                        break;
                    case ParryWindowEnd end:
                        // Une fin de parade tombe à la fin de la fenêtre ouverte à son tick de départ (passé, au plus le dernier départ).
                        Require(phase == CombatPhase.Expirations && end.Fighter.Character != null && end.StartTick < CurrentTick
                            && end.StartTick <= end.Fighter.ParryStartTick && tick == checked(end.StartTick + end.Fighter.Rules.ParryWindowMs / _tickMs)
                            && parryEnds.Add((end.Fighter, end.StartTick)), $"{at} : fin de parade impossible ou en double.");
                        break;
                    case StateExpiry expiry:
                        Require(phase == CombatPhase.Expirations && Numbered(expiry.Fighter, expiry.Key, expiry.Instance, expiry.Generation)
                            && expiryKeys.Add((expiry.Fighter, expiry.Key, expiry.Instance, expiry.Generation)), $"{at} : échéance d'altération impossible ou en double.");
                        break;
                    case DotTick dot:
                        Require(phase == CombatPhase.Periodic && Numbered(dot.Fighter, dot.Key, dot.Instance, null) && dotTicks.Add((dot.Fighter, dot.Key, dot.Instance)),
                            $"{at} : tick de DOT impossible ou en double.");
                        break;
                    case AttackImpact impact:
                        Require(phase == CombatPhase.Resolution && impact.Pulse >= 0 && impact.Pulse < impact.Attack.Pulses,
                            $"{at} : impact impossible.");
                        // Revue Codex de la PR #32 (4e tour) : une attaque dirigée garde sa cible ; chaque impulsion tombe à son tick.
                        // 9e tour : la cible verrouillée d'un coup dirigé est un adversaire de l'assaillant (l'annonce ne garde pas d'autre
                        // preuve ; l'empreinte de l'instantané protège le reste).
                        Require(impact.Zone != null || (impact.Target is { } locked && locked.Team != impact.Assailant.Team), $"{at} : impact dirigé sans cible adverse.");
                        Require(impact.ImpactTick >= 0 && tick == checked(impact.ImpactTick + impact.Pulse * (impact.Attack.PulseMs / _tickMs)),
                            $"{at} : impulsion {impact.Pulse} hors de son tick (impact au tick {impact.ImpactTick}).");
                        ValidateZone(impact, at);
                        break;
                    case ReactionItem reaction:
                        // Une réaction du tick est traitée dans son tick ; entre deux ticks, seules attendent les frappes reportées du précédent.
                        Require(phase == CombatPhase.Resolution && tick == CurrentTick && reaction.DeferredFrom == CurrentTick - 1,
                            $"{at} : réaction qui n'est pas reportée du tick précédent.");
                        // 9e tour : un déclencheur ne réagit qu'une fois à un même effet observé.
                        Require(reacted.Add((reaction.Slot, reaction.Origin.Cause, reaction.Origin.Root)), $"{at} : réaction en double.");
                        // 10e tour : seule une frappe se reporte (une autre réaction s'exécute dans sa phase). Son déclencheur est abonné à
                        // l'effet observé (type, rôle, filtre), émis au tick précédent après la résolution ; sa recharge interne est repartie de
                        // ce tick ; sa provenance est exactement la réaction de ce déclencheur à cet effet.
                        Require(reaction.Slot.Strikes && Subscribes(reaction.Slot, reaction.Event) && FilterAccepts(reaction.Slot.Definition.Filter, reaction.Event)
                            && reaction.Event.Tick == CurrentTick - 1 && reaction.Event.Phase > CombatPhase.Resolution
                            && reaction.Slot.ReadyTick == checked(CurrentTick - 1 + reaction.Slot.Definition.CooldownMs / _tickMs),
                            $"{at} : réaction reportée incohérente avec son déclencheur ou l'effet observé.");
                        Require(ReactionOriginFits(reaction), $"{at} : provenance de réaction incohérente avec l'effet observé.");
                        reactions++;
                        break;
                    default:
                        Require(false, $"{at} : élément inattendu entre deux ticks.");
                        break;
                }
            }
            Require(_deferredPending == reactions, $"{_deferredPending} réactions reportées comptées, {reactions} en file.");
            for (int i = 0; i < _setup.Commands.Count; i++)
                Require(commands.Contains(i) == (_setup.Commands[i].AtMs / _tickMs >= CurrentTick), $"Commande {i} : en file si et seulement si elle est à venir.");

            // 9e tour : deux emprises solides présentes ne se chevauchent jamais (placement, marche, arrivée et charge les en empêchent).
            for (int i = 0; i < _fightersById.Count; i++)
                for (int j = i + 1; j < _fightersById.Count; j++)
                {
                    Fighter a = _fightersById[i], b = _fightersById[j];
                    if (a.Out || b.Out || a.Body is null || b.Body is null || !_solid[i, j]) continue;
                    Require(!Planar.CirclesOverlap(a.Body.X, a.Body.Z, a.Body.Radius, b.Body.X, b.Body.Z, b.Body.Radius),
                        $"Emprises de « {a.Id} » et « {b.Id} » qui se chevauchent.");
                }

            var instances = new HashSet<long>();
            var appliedSeqs = new HashSet<long>();   // 11e tour : chaque rang d'application est donné une fois (++_lastApplication)
            // Revue Codex de la PR #32 (5e tour) : un moteur neuf du même combat donne l'état de départ de chaque combattant. Un combattant pas
            // encore entré (attendu, ou refusé à son arrivée) y est encore, champ pour champ ; le flux de tirage de chacun est celui du combat.
            var reference = new CombatEngine(_setup, NullCombatLog.Instance);
            var initial = reference._fighters.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (Fighter f in _fighters)
            {
                string who = $"« {f.Id} »";
                // Revue Codex de la PR #32 (3e tour) : un assaillant présent et vivant a exactement son tour à venir en file, au tick de
                // NextAttackTick (aucun s'il n'en a plus) ; les tours d'un mort ou d'un absent sont périmés (sans effet).
                if (f.Assailant != null && !f.Out)
                {
                    var turns = entries.Where(e => e.Item is AssailantTurn t && t.Fighter == f).ToList();
                    Require(f.NextAttackTick is long next
                        ? turns.Count == 1 && turns[0].Tick == next && ((AssailantTurn)turns[0].Item).Turn == f.NextTurn - 1
                        : turns.Count == 0, $"Tours de {who} incohérents avec son prochain tour ({f.NextAttackTick?.ToString(CultureInfo.InvariantCulture) ?? "aucun"}).");
                }
                else if (f.Assailant != null)
                {
                    // 9e tour : un assaillant pas encore entré n'a aucun tour en file (le premier vient à son arrivée) ; un mort ou un parti en
                    // garde au plus un, périmé : celui qui était programmé.
                    var turns = entries.Where(e => e.Item is AssailantTurn t && t.Fighter == f).ToList();
                    bool absent = f.Presence == Presence.Pending || f.Presence == Presence.Refused;
                    Require(absent ? turns.Count == 0 : turns.Count <= 1 && turns.All(e => ((AssailantTurn)e.Item).Turn == f.NextTurn - 1),
                        $"Tours de {who}, mort ou absent, impossibles.");
                }
                // Une parade ouverte, commencée à un tick passé, se ferme à la fin de sa fenêtre : sa fin est en file, une seule fois (4e tour).
                if (f.ParryOpen)
                {
                    var ends = entries.Where(e => e.Item is ParryWindowEnd end && end.Fighter == f && end.StartTick == f.ParryStartTick).ToList();
                    Require(f.ParryStartTick < CurrentTick && f.ParryEndTick >= CurrentTick && ends.Count == 1 && ends[0].Tick == f.ParryEndTick,
                        $"Parade ouverte de {who} sans sa fin en file (ou en double).");
                }
                // Revue Codex de la PR #32 (4e tour) : une recharge en cours est celle d'une compétence du personnage, et son échéance est en
                // file une seule fois à son tick (une échéance en double écrirait deux fois cooldownReady).
                // 5e tour : une échéance ne tombe pas plus loin que sa durée à partir du tick précédent (recharge globale ou d'une compétence,
                // récupération d'une parade ratée, recharge d'un déclencheur) ; une fenêtre de parade ouverte a sa durée.
                long previous = CurrentTick - 1;
                Require(f.GlobalCooldownReadyTick <= Math.Max(0, previous + _globalCooldownTicks)
                    && f.ParryRecoveryUntilTick <= Math.Max(0, previous + f.Rules.ParryFailureRecoveryMs / _tickMs)
                    && (!f.ParryOpen || f.ParryEndTick == f.ParryStartTick + f.Rules.ParryWindowMs / _tickMs),
                    $"Recharge globale ou parade de {who} au-delà de sa durée.");
                // 15e tour (relecture d'ensemble) : une parade réussie est comptée ; une récupération de parade ratée suppose une parade ratée.
                Require((!f.ParrySucceeded || f.Result.PerfectParries >= 1) && (f.ParryRecoveryUntilTick == 0 || f.Result.FailedParries >= 1),
                    $"Parade de {who} incohérente avec son bilan.");
                // Relecture d'ensemble (avant le 19e tour) : la dernière parade a commencé à un tick passé et sa fenêtre a sa durée (aucune
                // parade : tout à 0) ; une récupération de parade ratée part de la fin de cette fenêtre, sauf si une parade a été reprise
                // après elle ; parades réussies et ratées au plus les commandes de parade passées.
                long window = f.Rules.ParryWindowMs / _tickMs, recovery = f.Rules.ParryFailureRecoveryMs / _tickMs;
                int parryCommands = _setup.Commands.Count(c => c.Action == CombatAction.Parry && c.Actor == f.Id && c.AtMs / _tickMs <= previous);
                Require(f.ParryStartTick >= 0 && f.ParryStartTick <= Math.Max(0, previous)
                    && (f.ParryEndTick == checked(f.ParryStartTick + window) || (f.ParryStartTick == 0 && f.ParryEndTick == 0 && !f.ParrySucceeded))
                    && (f.ParryRecoveryUntilTick == 0 || f.ParryRecoveryUntilTick == checked(f.ParryEndTick + recovery) || f.ParryStartTick >= f.ParryRecoveryUntilTick)
                    && checked(f.Result.PerfectParries + f.Result.FailedParries) <= parryCommands,
                    $"Parade de {who} incohérente avec ses commandes ou sa fenêtre.");
                // 7e tour : les déclencheurs portés par une altération ont les mêmes bornes que les déclencheurs permanents.
                foreach (TriggerSlot slot in f.Triggers.Concat(f.Statuses.Values.SelectMany(s => s.Slots ?? Array.Empty<TriggerSlot>())))
                    Require(slot.ReadyTick >= 0 && slot.ReadyTick <= Math.Max(0, previous + slot.Definition.CooldownMs / _tickMs), $"Recharge d'un déclencheur de {who} au-delà de sa durée.");
                // 13e tour : un lancer a toujours sa commande de compétence, de ce combattant et à son tick, dans le combat complété. La recharge
                // globale part du dernier lancer (son tick + sa durée), chaque recharge de compétence de son lancer (au plus le dernier) ; sans
                // lancer, ni l'une ni les autres.
                var casts = new HashSet<(long Tick, string Skill)>();
                foreach (CombatCommand command in _setup.Commands)
                    if (command.Action == CombatAction.UseSkill && command.Actor == f.Id && command.AtMs / _tickMs <= previous) casts.Add((command.AtMs / _tickMs, command.Skill!));
                long lastCast = checked(f.GlobalCooldownReadyTick - _globalCooldownTicks);
                // 17e tour : chaque commande passée a été acceptée ou refusée (CommandsRejected) ; les lancers comptés (SkillsCast) ne
                // dépassent donc pas les commandes de compétence passées, ni, avec les refus, l'ensemble des commandes passées. L'acceptation
                // d'une commande précise n'est pas dans l'instantané : au-delà, c'est son empreinte qui fait foi.
                int skillCommands = _setup.Commands.Count(c => c.Action == CombatAction.UseSkill && c.Actor == f.Id && c.AtMs / _tickMs <= previous);
                int pastCommands = _setup.Commands.Count(c => c.Actor == f.Id && c.AtMs / _tickMs <= previous);
                Require(f.Result.SkillsCast <= skillCommands && checked(f.Result.SkillsCast + f.Result.CommandsRejected) <= pastCommands,
                    $"Lancers ou refus de {who} au-delà de ses commandes passées.");
                Require(f.Result.SkillsCast == 0
                    ? f.GlobalCooldownReadyTick == 0 && f.CooldownReadyTick.Count == 0
                    : f.Character != null && casts.Any(c => c.Tick == lastCast), $"Recharge globale de {who} sans lancer à son origine.");
                foreach (var cooldown in f.CooldownReadyTick)
                {
                    long cooldownTicks = f.Character != null && f.Character.Class.TryGetSkill(cooldown.Key, out SkillDefinition skill) && skill.Combat is { } combat
                        ? f.Rules.CooldownMs(skill, combat) / _tickMs
                        : -1;
                    Require(cooldownTicks >= 0 && cooldown.Value >= 0 && cooldown.Value <= Math.Max(0, previous + cooldownTicks),
                        $"Recharge de {who} pour une compétence inconnue ou au-delà de sa durée : {cooldown.Key}.");
                    long castAt = cooldown.Value - cooldownTicks;
                    Require(castAt <= lastCast && casts.Contains((castAt, cooldown.Key)), $"Recharge de {who} ({cooldown.Key}) sans lancer à son origine.");
                    if (cooldown.Value < CurrentTick) continue;
                    int pending = entries.Count(e => e.Item is CooldownExpiry c && c.Fighter == f && c.Skill == cooldown.Key && e.Tick == cooldown.Value);
                    Require(pending == 1, $"Recharge de {who} ({cooldown.Key}) : {pending} échéance(s) en file au tick {cooldown.Value}.");
                }
                // Présence : une arrivée en file pour un combattant attendu, aucune sinon ; départ et arrivée déjà passés.
                Require(arrivals.Count(a => a == f) == (f.Presence == Presence.Pending ? 1 : 0), $"Arrivée de {who} incohérente avec sa présence.");
                // 8e tour : un combattant attendu l'est jusqu'à son tick d'arrivée ; un refus d'arrivée n'existe qu'après ce tick.
                long? arrivalTick = f.Setup.ArrivalMs is long due ? due / _tickMs : (long?)null;
                Require(f.Presence switch
                {
                    Presence.Pending => arrivalTick is long awaited && awaited >= CurrentTick,
                    Presence.Refused => arrivalTick is long refusedAt && refusedAt < CurrentTick,
                    _ => arrivalTick is null || arrivalTick < CurrentTick,
                }, $"Présence de {who} incohérente avec son tick d'arrivée.");
                Require(f.LeaveCommand is null, $"Départ de {who} encore demandé entre deux ticks.");
                // 16e tour : un refus d'arrivée n'existe que dans un combat positionné (place prise) ; un départ suit une commande leave de ce
                // combattant, à son tick, dans le combat complété.
                Require(f.Presence != Presence.Refused || _spatial, $"Arrivée de {who} refusée hors d'un combat positionné.");
                Require(f.LeftTick is not long left || _setup.Commands.Any(c => c.Action == CombatAction.Leave && c.Actor == f.Id && c.AtMs / _tickMs == left),
                    $"Départ de {who} sans commande de départ à son tick.");
                // 18e tour : un mort ne part pas (le départ est annulé, leaveCancelled) et un parti ne meurt plus.
                Require(!(f.IsDead && (f.Presence == Presence.Left || f.LeftTick != null)), $"{who}, mort, déclaré parti.");
                // 17e tour : un départ suit l'admission (la commande leave n'est reçue que d'un combattant présent, arrivé au plus tard à ce tick).
                Require(f.LeftTick is not long leftAt || leftAt >= (f.ArrivedTick ?? 0), $"Départ de {who} avant son arrivée.");
                Require(f.ArrivedTick is null || (f.Presence != Presence.Pending && f.Presence != Presence.Refused && f.ArrivedTick >= 0 && f.ArrivedTick < CurrentTick),
                    $"Arrivée de {who} au tick {f.ArrivedTick} impossible.");
                // 14e tour : un combattant admis est arrivé exactement à son tick prévu ; un combattant présent dès l'origine, attendu ou refusé
                // n'a pas de tick d'arrivée.
                bool admitted = f.Presence != Presence.Pending && f.Presence != Presence.Refused && f.Setup.ArrivalMs != null;
                Require(f.ArrivedTick == (admitted ? f.Setup.ArrivalMs / _tickMs : null), $"Arrivée de {who} au tick {f.ArrivedTick} : pas celle de son combat.");
                Require((f.LeftTick != null) == (f.Presence == Presence.Left) && (f.LeftTick is null || (f.LeftTick >= 0 && f.LeftTick < CurrentTick)),
                    $"Départ de {who} au tick {f.LeftTick} impossible.");
                Require(f.Result.ArrivedTick == f.ArrivedTick && f.Result.LeftTick == f.LeftTick, $"Bilan de présence de {who} incohérent.");
                // Points de vie : absents si et seulement si infinis, dans [0, max] ; nuls si et seulement si mort (morts relevés en fin de tick).
                Require((f.Health is null) == (f.MaxHealth is null) && (f.Health is null || (f.Health >= 0 && f.Health <= f.MaxHealth)),
                    $"Points de vie de {who} impossibles : {f.Health?.ToString(CultureInfo.InvariantCulture) ?? "infinis"} (max {f.MaxHealth?.ToString(CultureInfo.InvariantCulture) ?? "infini"}).");
                // Des points de vie nuls sans mort : seulement avant le premier relevé (tick 0) ou chez un combattant jamais entré.
                bool unscanned = f.Presence == Presence.Pending || f.Presence == Presence.Refused || (CurrentTick == 0 && f.Presence == Presence.Present);
                Require((f.IsDead ? f.Health == 0 && f.Presence != Presence.Pending && f.Presence != Presence.Refused : f.Health != 0 || unscanned)
                    && f.IsDead == (f.Result.DeathTick != null) && (f.Result.DeathTick is null || (f.Result.DeathTick >= 0 && f.Result.DeathTick < CurrentTick)),
                    $"Mort de {who} incohérente avec ses points de vie.");
                // Relecture d'ensemble (après le 18e tour) : sans soin, les PV ne baissent que des dégâts subis (après boucliers et transferts,
                // PrepareDamage) : un vivant a perdu exactement ses dégâts subis, un mort au moins ses PV maximaux (dégâts au-delà de 0).
                if (f.Health is long hp && f.MaxHealth is long maxHp)
                    Require(f.IsDead ? f.Result.DamageTaken >= maxHp - hp : f.Result.DamageTaken == maxHp - hp, $"Dégâts subis de {who} incohérents avec ses points de vie.");
                // 11e tour : la cause mortelle est notée quand les PV tombent à 0, et la mort suit en fin du même tick : un vivant n'en a pas.
                Require(f.IsDead || f.LethalCause is null, $"Cause mortelle de {who} vivant.");
                // Ressources.
                Require(f.Endurance >= 0 && f.Endurance <= f.MaxEndurance, $"Endurance de {who} hors de [0, {f.MaxEndurance}] : {f.Endurance}.");
                // Relecture d'ensemble : l'endurance ne baisse que par une dépense, qui fixe la reprise de la régénération (et remet le cumul à
                // 0) ; la reprise n'est effacée qu'endurance pleine. Une reprise à venir n'a encore rien accordé.
                Require(f.RegenFromTick != null || f.Endurance == f.MaxEndurance, $"Endurance de {who} entamée sans reprise de régénération.");
                Require(f.RegenFromTick is not long resume || resume < CurrentTick || f.RegenGranted == 0, $"Régénération de {who} accordée avant sa reprise.");
                Require(f.Character is null
                    ? f.Resonance == 0 && f.RampartCharges == 0 && !f.Guarding && !f.ParryOpen
                    : f.Resonance >= 0 && f.Resonance <= _rules.ResonanceMax && f.RampartCharges >= 0 && f.RampartCharges <= f.Rules.RampartChargesMax,
                    $"Résonance, charges ou garde de {who} impossibles.");
                Require(f.GlobalCooldownReadyTick >= 0 && f.RegenGranted >= 0 && f.NextTurn >= 0 && (f.NextAttackTick is null || f.NextAttackTick >= CurrentTick),
                    $"Échéances de {who} impossibles.");
                // Sous-lot 11.5 : seul un ennemi qui a un comportement s'engage ; sans distance d'engagement, il l'est d'emblée.
                Require(f.Behavior is null ? !f.Engaged : f.Engaged || f.Behavior.AggroRange != null, $"Engagement de {who} impossible.");
                // Revue Codex de la PR #33 (tour 6) : seul un assaillant est provoqué, et une provocation active a été reçue.
                Require(f.Assailant != null || !f.Taunted, $"Provocation reçue par {who} impossible.");
                Require(f.Taunted || !f.Statuses.Values.Any(s => s.Definition.IsTaunt), $"Provocation active de {who} non mémorisée.");
                // 7e tour : régénération accordée au plus ce que son délai permet jusqu'au tick précédent (sans régénération en cours, endurance
                // pleine, l'ancienne valeur reste sans effet jusqu'à la prochaine dépense, qui la remet à zéro) ; entretien de la garde (reliquat sous
                // l'unité, payé au plus le dû) ; compteurs du bilan positifs, sous un plafond qui laisse la marge de leurs incréments (vérifiés),
                // ticks gardés au plus les ticks joués.
                long grantable = f.RegenFromTick is long regenFrom && regenFrom <= previous
                    ? IntMath.MulDiv(f.Rules.EnduranceRegenPerSecond.Raw, checked((previous - regenFrom + 1) * _tickMs), 1000 * Fixed.Scale, RoundingMode.Floor)
                    : 0;
                Require((f.RegenFromTick is null || (f.RegenFromTick >= 0 && f.RegenFromTick <= Math.Max(0, previous + _regenDelayTicks)))
                    && f.RegenGranted <= (f.RegenFromTick is null ? CounterCeiling : Math.Max(0, grantable)), $"Régénération de {who} impossible.");
                // 12e tour : un personnage vivant, présent, sans garde ni altération qui bloque la régénération, dont la régénération avait
                // commencé au tick précédent, a reçu à ce tick tout ce que son délai permettait (RunRegeneration fixe le cumul accordé).
                if (!f.Out && f.Character != null && !f.Guarding && BlockReason(f, StatusBlocks.Regeneration) is null && f.RegenFromTick is long started && started <= previous)
                    Require(f.RegenGranted == grantable, $"Régénération de {who} impossible : {f.RegenGranted} accordés, {grantable} au tick précédent.");
                // 12e tour : l'entretien payé suit toujours l'entretien dû (tous deux partent de 0, un tick gardé les fixe à la même valeur, une
                // rupture n'en change aucun).
                Require(f.GuardUpkeepRemainder >= 0 && f.GuardUpkeepRemainder < 1000 * Fixed.Scale && f.GuardUpkeepPaid >= 0
                    && f.GuardUpkeepPaid == f.GuardUpkeepDue && f.GuardUpkeepDue <= CounterCeiling, $"Entretien de la garde de {who} impossible.");
                CombatantResult r = f.Result;
                Require(new[] { r.DamageDealt, r.DamageTaken, r.DamageAbsorbed, r.DamageTransferred, r.DamageRedirected, r.DamageMitigated, r.Hits,
                        r.CriticalHits, r.SkillsCast, r.UltimatesCast, r.CommandsRejected, r.Blocks, r.PerfectParries, r.FailedParries, r.GuardBreaks }
                    .All(v => v >= 0 && v <= CounterCeiling) && r.GuardTicks >= 0 && r.GuardTicks <= CurrentTick
                    // 18e tour : un coup critique est un coup, un ultime est une compétence lancée (le total est toujours compté avec lui).
                    && r.CriticalHits <= r.Hits && r.UltimatesCast <= r.SkillsCast, $"Bilan de {who} impossible.");
                // Relecture d'ensemble (3) : garde et parade ne commencent que sur commande. Une parade a sa commande à son tick de début ;
                // une garde tenue (ou rompue) suppose une commande de garde passée ; l'entretien n'avance que sur des ticks gardés.
                Require((f.ParryEndTick == 0 || _setup.Commands.Any(c => c.Action == CombatAction.Parry && c.Actor == f.Id && c.AtMs / _tickMs == f.ParryStartTick))
                    && (!f.Guarding && f.Result.GuardBreaks == 0 && f.Result.GuardTicks == 0
                        || _setup.Commands.Any(c => c.Action == CombatAction.GuardStart && c.Actor == f.Id && c.AtMs / _tickMs <= previous))
                    && (f.GuardUpkeepDue == 0 && f.GuardUpkeepRemainder == 0 || f.Result.GuardTicks >= 1),
                    $"Garde ou parade de {who} sans commande à son origine.");
                // Relecture d'ensemble (3) : seuls les assaillants ont des tours ; une mort suit l'arrivée.
                Require(f.Assailant != null || (f.NextTurn == 0 && f.NextAttack == 0 && f.NextAttackTick is null), $"Tours de {who}, qui n'attaque pas seul.");
                Require(f.Result.DeathTick is not long died || died >= (f.ArrivedTick ?? 0), $"Mort de {who} avant son arrivée.");
                // Un mort ou un absent n'a ni altération, ni garde, ni parade, ni tour annoncé, et ne marche pas.
                if (f.Out)
                    Require(f.Statuses.Count == 0 && !f.Guarding && !f.ParryOpen && f.NextAttackTick is null && f.Body is not { Moving: true },
                        $"{who}, mort ou absent, garde un état actif.");
                foreach (StatusInstance s in f.Statuses.Values)
                {
                    string status = $"Altération {s.Id} ({s.Definition.Id}) de {who}";
                    // 11e tour : une altération active a été posée au moins une fois (génération ≥ 1), et chaque pose ou renouvellement prend un
                    // rang d'application : sa génération ne dépasse pas le dernier rang, et deux altérations n'ont jamais le même rang.
                    Require(s.Id >= 1 && s.Id <= _lastStatusInstance && instances.Add(s.Id) && s.Generation >= 1 && s.Generation <= _lastApplication,
                        $"{status} : instance impossible ou en double.");
                    Require(s.AppliedSeq >= 1 && s.AppliedSeq <= _lastApplication && appliedSeqs.Add(s.AppliedSeq), $"{status} : rang d'application impossible ou en double.");
                    Require(string.Equals(s.Key.Source, s.Definition.Stacking == StatusStacking.PerSourceKeepStronger ? s.Source : null, StringComparison.Ordinal),
                        $"{status} : clé de cumul incohérente avec sa source.");
                    Require(s.UntilTick >= CurrentTick && (!s.Definition.IsDot || s.NextTick >= CurrentTick), $"{status} : déjà échue.");
                    // 10e tour : posée au plus tard au tick précédent, elle ne dure pas plus que la plus longue durée qu'elle a pu recevoir (base,
                    // plus grands ajouts positifs d'un auteur et de la cible, écrêtés à 24 h ; résistance et raccourcis ne font que réduire) ; le
                    // prochain tick d'un DOT tombe au plus une période après le tick précédent.
                    long longest = MaxStatusTicks(f, s.Definition);
                    Require(s.Definition.IsDot
                        ? s.NextTick <= checked(CurrentTick - 1 + s.Definition.PeriodMs / _tickMs) && s.UntilTick <= Math.Max(checked(CurrentTick - 1 + longest), s.NextTick)
                        : s.UntilTick <= checked(CurrentTick - 1 + longest), $"{status} : échéance au-delà de sa plus longue durée possible.");
                    // 11e tour : un bouclier épuisé est retiré en fin de tick (raison depleted) : entre deux ticks, un bouclier actif absorbe encore,
                    // et aucune altération ne garde l'effet qui l'aurait épuisée.
                    Require(s.Absorb >= 0 && s.Absorb <= CounterCeiling && (s.Definition.IsShield ? s.Absorb > 0 : s.Absorb == 0) && s.DepletedBy is null,
                        $"{status} : absorption impossible.");
                    // 20e tour : un bouclier absorbe au plus la valeur de la plus forte application possible de cette altération (l'absorption
                    // ne fait ensuite que décroître, un renouvellement gardant la plus forte des deux valeurs).
                    Require(!s.Definition.IsShield || s.Absorb <= MaxShieldAbsorb(s.Definition, s.Key.Source),
                        $"{status} : absorption au-delà du plus fort bouclier possible.");
                    // 19e tour : ApplyStatus fixe l'origine par WithSource(source).CausedBy(ligne).ForInstance(instance, génération) : même source,
                    // même instance, même génération, une cause.
                    Require(string.Equals(s.Origin.Source, s.Source, StringComparison.Ordinal) && s.Origin.SourceInstance == s.Id
                        && s.Origin.SourceGeneration == s.Generation && s.Origin.Cause != null, $"{status} : provenance différente de l'altération.");
                    // 7e tour : puissance d'un DOT positive (nulle pour une autre altération), sous le plafond.
                    Require(s.Power.Raw >= 0 && s.Power.Raw <= CounterCeiling && (s.Definition.IsDot || (s.Power == Fixed.Zero && s.NextTick == 0)),
                        $"{status} : puissance impossible.");
                    // Relecture d'ensemble (20e tour) : au plus la puissance de la plus forte application possible de ce DOT.
                    Require(!s.Definition.IsDot || s.Power <= MaxDotPower(s.Definition, s.Key.Source), $"{status} : puissance au-delà du plus fort DOT possible.");
                    // Son échéance (ou, pour un DOT, son prochain tick) est en file, une seule fois et à son tick (revue Codex de la PR #32,
                    // 4e tour : une échéance en double agirait) ; aucune échéance de l'autre genre ne la vise.
                    var expiries = entries.Where(e => e.Item is StateExpiry x && x.Fighter == f && x.Key.Equals(s.Key) && x.Instance == s.Id && x.Generation == s.Generation).ToList();
                    var dots = entries.Where(e => e.Item is DotTick d && d.Fighter == f && d.Key.Equals(s.Key) && d.Instance == s.Id).ToList();
                    Require(s.Definition.IsDot
                        ? expiries.Count == 0 && dots.Count == 1 && dots[0].Tick == s.NextTick
                        : dots.Count == 0 && expiries.Count == 1 && expiries[0].Tick == s.UntilTick, $"{status} : échéance absente de la file, en double ou hors de son tick.");
                    // La source d'une altération est un combattant du combat ; celle d'une provocation ou d'une protection est présente et vivante
                    // (sa mort ou son départ les retire).
                    Require(s.Source is null || _byId.ContainsKey(s.Source), $"{status} : source inconnue.");
                    Require(!s.Definition.IsSourceBound || s.Source != null && !_byId[s.Source].Out, $"{status} : provocation ou protection sans source présente.");
                    // Relecture d'ensemble : une provocation vient d'un adversaire de l'assaillant, une protection d'un allié du personnage, autre
                    // que lui (SourceFits).
                    Require(!s.Definition.IsSourceBound || (s.Definition.IsTaunt
                        ? f.Assailant != null && _byId[s.Source!].Team != f.Team
                        : f.Character != null && s.Source != f.Id && _byId[s.Source!].Team == f.Team), $"{status} : source qui ne convient pas.");
                }
                if (f.Body is { } b)
                {
                    Require(b.X >= checked(_minX + b.Radius) && b.X <= checked(_maxX - b.Radius) && b.Z >= checked(_minZ + b.Radius) && b.Z <= checked(_maxZ - b.Radius),
                        $"Place de {who} hors de l'aire.");
                    Require(!_obstacles.Any(o => o.OverlapsCircle(b.X, b.Z, b.Radius)), $"Place de {who} dans le décor.");
                    Require(IsUnit(b.FacingX, b.FacingZ) && (!b.Moving || IsUnit(b.DirectionX, b.DirectionZ)), $"Orientation ou direction de {who} non unitaire.");
                    // 15e tour : seul un personnage ou un assaillant marche (un mannequin ne reçoit jamais move).
                    Require(!b.Moving || f.Character != null || f.Assailant != null, $"Marche de {who}, qui ne marche jamais.");
                    // Relecture d'ensemble (4) : une marche suit une commande move passée de ce combattant ; une cible sélectionnée, une commande
                    // select passée de ce combattant qui la désigne. Sous-lot 11.5 : un ennemi à comportement marche sans commande (placement).
                    Require((!b.Moving || f.Behavior != null || _setup.Commands.Any(c => c.Action == CombatAction.Move && c.Actor == f.Id && c.AtMs / _tickMs <= previous))
                        && (b.Selected is not { } chosen || _setup.Commands.Any(c => c.Action == CombatAction.Select && c.Actor == f.Id && c.Target == chosen.Id
                            && c.AtMs / _tickMs <= previous)), $"Marche ou sélection de {who} sans commande à son origine.");
                }
                Fighter start = initial[f.Id];
                Require(f.Random.Capture().Increment == start.Random.Capture().Increment, $"Flux de tirage de {who} différent de celui du combat.");
                bool entered = f.Presence != Presence.Pending && f.Presence != Presence.Refused;
                // Revue Codex de la PR #33 (tour 4) : avant le premier tick, aucun combattant n'a encore rien fait (engagement compris).
                if (!entered || CurrentTick == 0)
                {
                    // Seules différences possibles : sa présence (refusé) et ses commandes refusées (un absent n'agit pas).
                    start.Presence = f.Presence;
                    start.Result.CommandsRejected = f.Result.CommandsRejected;
                    Require(f.Result.CommandsRejected >= 0 && string.Equals(WriteFighter(f), WriteFighter(start), StringComparison.Ordinal),
                        entered ? $"{who}, avant le premier tick, n'est plus dans son état de départ." : $"{who}, pas encore entré, n'est plus dans son état de départ.");
                }
                // Les tours d'un assaillant entré se déduisent de sa cadence : tours joués jusqu'à son dernier tick d'action (le précédent,
                // ou celui de sa mort ou de son départ), attaque suivante de sa liste, tour suivant programmé s'il tombe avant la fin.
                if (entered && f.Assailant is { } assailant)
                {
                    long first = checked((f.ArrivedTick ?? 0) + assailant.FirstAttackMs / _tickMs);
                    long interval = assailant.IntervalMs / _tickMs;
                    long last = Math.Min(f.Result.DeathTick ?? f.LeftTick ?? CurrentTick - 1, _durationTicks - 1);
                    long played = interval <= 0 || first > last ? 0 : (last - first) / interval + 1;
                    long next = checked(first + played * interval);
                    bool scheduled = next < _durationTicks;
                    Require(f.NextTurn == played + (scheduled ? 1 : 0) && f.NextAttack == played % assailant.Attacks.Count
                        && f.NextAttackTick == (f.Out || !scheduled ? (long?)null : next),
                        $"Tours de {who} incohérents avec sa cadence : {played} joués, prochain au tick {(scheduled ? next.ToString(CultureInfo.InvariantCulture) : "aucun")}.");
                }
            }
            // 19e tour : chaque montant de dégâts est crédité à sa source et réparti chez sa cible entre dégâts subis, absorbés et transférés ;
            // la part transférée est comptée redirigée chez le protecteur, qui en subit ou en absorbe au plus autant (sa garde peut réduire).
            // Sur tout le combat : transferts = redirections, et reçus (subis + absorbés + transférés) au moins les dégâts infligés crédités.
            long dealtTotal = 0, receivedTotal = 0, transferredTotal = 0, redirectedTotal = 0;
            foreach (Fighter f in _fightersById)
            {
                CombatantResult r = f.Result;
                dealtTotal = checked(dealtTotal + r.DamageDealt);
                receivedTotal = checked(receivedTotal + r.DamageTaken + r.DamageAbsorbed + r.DamageTransferred);
                transferredTotal = checked(transferredTotal + r.DamageTransferred);
                redirectedTotal = checked(redirectedTotal + r.DamageRedirected);
            }
            Require(transferredTotal == redirectedTotal && receivedTotal >= dealtTotal,
                $"Dégâts du bilan incohérents entre combattants : {dealtTotal} infligés, {receivedTotal} reçus, {transferredTotal} transférés, {redirectedTotal} redirigés.");
        }

        /// <summary>
        /// Revue Codex de la PR #32 (10e tour) : plus longue durée, en ticks, qu'une application de <paramref name="definition"/> à
        /// <paramref name="target"/> peut recevoir (<see cref="DurationTicks"/> sans résistance ni raccourci, avec les plus grands ajouts
        /// positifs de la cible et d'un auteur quelconque du combat).
        /// </summary>
        private long MaxStatusTicks(Fighter target, StatusDefinition definition)
        {
            long dealt = 0;
            foreach (Fighter dealer in _fightersById) dealt = Math.Max(dealt, dealer.Rules.DurationDealtMs(definition.Id));
            long added = checked(Math.Max(0, target.Rules.DurationTakenMs(definition.Id)) + dealt);
            long floor = definition.IsDot ? definition.PeriodMs : _tickMs;
            long ms = Math.Max(Math.Max(definition.DurationMs, floor), Math.Min(CombatRules.AbsoluteMaxDurationMs, checked(definition.DurationMs + added)));
            return ms / _tickMs;
        }

        /// <summary>
        /// Revue Codex de la PR #32 (20e tour) : plus fort coefficient d'une application de l'altération <paramref name="status"/> posée par
        /// <paramref name="source"/> : compétences de sa classe, attaques d'un assaillant, déclencheurs permanents à leur rang, déclencheurs
        /// portés par une altération du catalogue (rang 1, posés au nom de leur porteur).
        /// </summary>
        private Fixed StrongestApplication(Fighter source, string status)
        {
            Fixed coefficient = Fixed.Zero;
            foreach (StatusDefinition carrier in _statuses.Definitions)
                foreach (TriggerDefinition trigger in carrier.Triggers) coefficient = Fixed.Max(coefficient, StrongestCoefficient(trigger, status, 1));
            if (source.Character is { } character)
                foreach (SkillDefinition skill in character.Class.Skills)
                    if (skill.Combat is { } combat) coefficient = Fixed.Max(coefficient, StrongestCoefficient(combat.Applies, status));
            if (source.Assailant is { } assailant)
                foreach (AssailantAttack attack in assailant.Attacks) coefficient = Fixed.Max(coefficient, StrongestCoefficient(attack.Applies, status));
            foreach (TriggerSlot slot in source.Triggers) coefficient = Fixed.Max(coefficient, StrongestCoefficient(slot.Definition, status, slot.Rank));
            return coefficient;
        }

        /// <summary>
        /// Sources possibles d'une altération : la source de sa clé pour un cumul par source, n'importe quel combattant sinon (un
        /// renouvellement par une autre source garde la plus forte valeur).
        /// </summary>
        private IEnumerable<Fighter> PossibleSources(string? keySource) =>
            keySource is null ? _fightersById : _fightersById.Where(f => string.Equals(f.Id, keySource, StringComparison.Ordinal));

        /// <summary>
        /// Revue Codex de la PR #32 (20e tour) : plus forte absorption qu'un bouclier <paramref name="definition"/> a pu recevoir
        /// (<see cref="ShieldAmount"/> de la plus forte application par une source possible).
        /// </summary>
        private long MaxShieldAbsorb(StatusDefinition definition, string? keySource)
        {
            long best = 0;
            foreach (Fighter source in PossibleSources(keySource))
                best = Math.Max(best, ShieldAmount(source, definition, StrongestApplication(source, definition.Id)));
            return best;
        }

        /// <summary>
        /// Relecture d'ensemble (20e tour) : plus forte puissance qu'un DOT <paramref name="definition"/> a pu recevoir (<see cref="DotPower"/> :
        /// plus forte puissance du type de l'altération d'une source possible, toutes statistiques d'attaque comprises, × plus fort coefficient
        /// de ses applications ; un renouvellement garde la plus forte des deux puissances).
        /// </summary>
        private Fixed MaxDotPower(StatusDefinition definition, string? keySource)
        {
            PowerType type = CombatantStats.PowerOf(definition.DamageType!.Value);
            Fixed best = Fixed.Zero;
            foreach (Fighter source in PossibleSources(keySource))
            {
                Fixed power = source.Stats.GetPower(type);
                foreach (DerivedStats stats in source.AttackStats.Values) power = Fixed.Max(power, stats.GetPower(type));
                best = Fixed.Max(best, Fixed.Max(Fixed.Zero, power) * StrongestApplication(source, definition.Id));
            }
            return best;
        }

        private static Fixed StrongestCoefficient(IReadOnlyList<StatusApplication> applies, string status)
        {
            Fixed best = Fixed.Zero;
            foreach (StatusApplication a in applies)
                if (a.Coefficient is { } c && string.Equals(a.Status, status, StringComparison.Ordinal)) best = Fixed.Max(best, c);
            return best;
        }

        private static Fixed StrongestCoefficient(TriggerDefinition trigger, string status, int rank)
        {
            Fixed best = Fixed.Zero;
            foreach (TriggerEffect e in trigger.Effects)
                if (e.Kind == TriggerEffectKind.ApplyStatus && e.Coefficient is { } c && string.Equals(e.Status, status, StringComparison.Ordinal))
                    best = Fixed.Max(best, c.At(rank));
            return best;
        }

        /// <summary>
        /// Revue Codex de la PR #32 (10e tour) : le déclencheur d'une réaction est abonné à l'effet observé et son porteur y tient le rôle
        /// demandé (<see cref="Matches"/>, sans exiger qu'un allié visé soit encore vivant : il l'était à l'émission).
        /// </summary>
        private bool Subscribes(TriggerSlot slot, ObservedEvent e)
        {
            TriggerDefinition d = slot.Definition;
            if (d.On != e.Kind && d.On != e.Also) return false;
            if (d.EffectiveRole == TriggerRole.Ally)
                return e.Target != null && !string.Equals(e.Target, slot.Bearer.Id, StringComparison.Ordinal)
                    && _byId.TryGetValue(e.Target, out Fighter? ally) && ally.Team == slot.Bearer.Team;
            string? holder = d.EffectiveRole == TriggerRole.Target ? e.Target : e.Source;
            return string.Equals(holder, slot.Bearer.Id, StringComparison.Ordinal);
        }

        /// <summary>
        /// Revue Codex de la PR #32 (10e tour) : la provenance d'une réaction est celle que <see cref="ProcessNotify"/> lui donne
        /// (<see cref="Provenance.ReactionTo"/> de l'effet observé : même racine, profondeur + 1, chaîne étendue de son déclencheur, absent
        /// avant, cause = effet observé, source = porteur ; instance de l'altération porteuse, à une génération déjà atteinte).
        /// </summary>
        private static bool ReactionOriginFits(ReactionItem reaction)
        {
            Provenance origin = reaction.Origin, observed = reaction.Event.Provenance;
            TriggerSlot slot = reaction.Slot;
            List<string> chain = observed.ChainOldestFirst();
            if (chain.Contains(slot.Identity, StringComparer.Ordinal)) return false;   // cycle : jamais acceptée
            chain.Add(slot.Identity);
            bool instance = slot.Status is { } status
                ? origin.SourceInstance == status.Id && origin.SourceGeneration is int generation && generation >= 1 && generation <= status.Generation
                : origin.SourceInstance is null && origin.SourceGeneration is null;
            return instance && string.Equals(origin.Root, observed.Root, StringComparison.Ordinal) && origin.Depth == checked(observed.Depth + 1)
                && origin.ChainOldestFirst().SequenceEqual(chain, StringComparer.Ordinal) && string.Equals(origin.Trigger, slot.Identity, StringComparison.Ordinal)
                && origin.Cause == reaction.Event.Eid && string.Equals(origin.Source, slot.Bearer.Id, StringComparison.Ordinal);
        }

        /// <summary>Vecteur unitaire à l'arrondi près (<see cref="Planar.TryNormalize"/> : au plus une unité brute d'erreur sur la longueur).</summary>
        private static bool IsUnit(long x, long z)
        {
            if (Math.Abs(x) > Planar.Unit || Math.Abs(z) > Planar.Unit) return false;
            long squared = Planar.LengthSquared(x, z);
            return squared >= (Planar.Unit - 3) * (Planar.Unit - 3) && squared <= (Planar.Unit + 3) * (Planar.Unit + 3);
        }

        /// <summary>Zone verrouillée d'un impact : présente pour une attaque de zone seulement, à la forme et aux dimensions de l'attaque, dans l'aire.</summary>
        private void ValidateZone(AttackImpact impact, string at)
        {
            AssailantAttack attack = impact.Attack;
            if (impact.Zone is not { } z)
            {
                if (attack.IsZone) throw new FormatException($"{at} : zone absente.");
                return;
            }
            bool round = attack.Shape is AttackShape.Circle or AttackShape.Persistent;
            bool ok = attack.IsZone && impact.Target is null && z.Shape == attack.Shape
                && z.OriginX >= _minX && z.OriginX <= _maxX && z.OriginZ >= _minZ && z.OriginZ <= _maxZ
                && (round
                    ? z.AxisX == 0 && z.AxisZ == 0 && z.Reach == attack.Radius!.Value.Raw && z.Angle == 0 && z.HalfWidth == 0
                    : IsUnit(z.AxisX, z.AxisZ) && z.Reach == attack.Range!.Value.Raw && z.Angle == (attack.Angle ?? 0) && z.HalfWidth == (attack.Width?.Raw ?? 0) / 2);
            if (!ok) throw new FormatException($"{at} : zone incohérente avec l'attaque {attack.Id}.");
        }

        private static CombatPhase Phase(int value) =>
            Enum.IsDefined(typeof(CombatPhase), value) ? (CombatPhase)value : throw new FormatException($"Phase inconnue : {value}.");

        /// <summary>Revue Codex de la PR #32 (2e tour) : valeur d'énumération relue, refusée si elle ne nomme aucun cas.</summary>
        private static T Defined<T>(SnapshotNode n) where T : struct, Enum
        {
            int value = n.Int;
            if (!Enum.IsDefined(typeof(T), value)) throw new FormatException($"{typeof(T).Name} inconnu : {value}.");
            return (T)(object)value;
        }

        private static T? DefinedOrNull<T>(SnapshotNode n) where T : struct, Enum => n.IsNull ? (T?)null : Defined<T>(n);

        private static Provenance Required(Provenance? provenance) => provenance ?? throw new FormatException("Provenance absente.");

        private Fighter FighterOf(SnapshotNode id) => _byId[id.String];

        // Revue Codex de la PR #32 (3e tour) : plus grand événement et plus grande instance d'altération cités par l'état relu.
        private long _maxLoadedEid;
        private long _maxLoadedInstance;

        /// <summary>Instance d'altération citée par l'état relu (null : aucune) : numérotée, au plus la dernière.</summary>
        private long? Instance(long? instance)
        {
            if (instance < 1) throw new FormatException($"Altération non numérotée citée : {instance}.");
            _maxLoadedInstance = Math.Max(_maxLoadedInstance, instance ?? 0);
            return instance;
        }

        private Provenance? ReadProvenance(SnapshotNode n)
        {
            if (n.IsNull) return null;
            long? cause = n["cause"].NullableLong, instance = n["instance"].NullableLong;
            if (cause < 1 || instance < 1) throw new FormatException("Provenance citant un événement ou une altération non numérotés.");
            _maxLoadedEid = Math.Max(_maxLoadedEid, cause ?? 0);
            _maxLoadedInstance = Math.Max(_maxLoadedInstance, instance ?? 0);
            // Revue Codex de la PR #32 (10e tour) : une réaction n'est acceptée que sous la profondeur maximale des règles.
            if (n["depth"].Int > _rules.MaxTriggerDepth) throw new FormatException($"Provenance au-delà de la profondeur maximale ({_rules.MaxTriggerDepth}).");
            return Provenance.Restore(n["root"].String, n["depth"].Int, n["chain"].Items.Select(c => c.String).ToList(), n["trigger"].NullableString,
                cause, n["source"].NullableString, instance, n["generation"].NullableInt);
        }

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
            f.Engaged = n["engaged"].Bool;
            f.Struck = n["struck"].Bool;
            f.Taunted = n["taunted"].Bool;
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
                        Instance = Instance(e["instance"].Long)!.Value, Generation = e["generation"].Int,
                    };
                case "dot":
                    return new DotTick { Fighter = FighterOf(e["fighter"]), Key = new StatusKey(e["state"].String, e["keySource"].NullableString), Instance = Instance(e["instance"].Long)!.Value };
                case "parryEnd":
                    return new ParryWindowEnd { Fighter = FighterOf(e["fighter"]), StartTick = e["startTick"].Long, Origin = Required(ReadProvenance(e["origin"])) };
                case "impact":
                {
                    Fighter assailant = FighterOf(e["assailant"]);
                    AssailantAttack attack = (assailant.Assailant ?? throw new FormatException($"Impact d'un combattant qui n'est pas un assaillant : « {assailant.Id} »."))
                        .Attacks.Single(a => a.Id == e["attack"].String);
                    SnapshotNode z = e["zone"];
                    return new AttackImpact
                    {
                        Assailant = assailant,
                        Attack = attack,
                        Target = e["target"].IsNull ? null : FighterOf(e["target"]),
                        Zone = z.IsNull ? null : new AttackZone(Defined<AttackShape>(z["shape"]), z["originX"].Long, z["originZ"].Long, z["axisX"].Long, z["axisZ"].Long,
                            z["reach"].Long, z["angle"].Int, z["halfWidth"].Long),
                        Pulse = e["pulse"].Long,
                        ImpactTick = e["impactTick"].Long,
                        Origin = Required(ReadProvenance(e["origin"])),
                    };
                }
                case "reaction":
                    return new ReactionItem
                    {
                        Slot = LoadSlot(e["slot"]),
                        Event = LoadObserved(e["event"]),
                        Origin = Required(ReadProvenance(e["origin"])),
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
            TriggerOrigin origin = Defined<TriggerOrigin>(n["origin"]);
            string definitionId = n["definition"].String;
            int index = n["index"].Int;
            long instance = n["instance"].Long;
            if (instance != 0) Instance(instance);
            if (instance == 0)
                return bearer.Triggers.Single(s => s.Origin == origin && s.DefinitionId == definitionId && s.Index == index);
            if (!n["detached"].Bool)
            {
                StatusInstance live = bearer.Statuses.Values.Single(s => s.Id == instance);
                return live.Slots![index];
            }
            StatusDefinition definition = _statuses[definitionId];
            // Revue Codex de la PR #32 (11e tour) : l'instance retirée avait été posée (génération ≥ 1), au plus au dernier rang d'application.
            int generation = n["generation"].Int;
            if (generation < 1 || generation > _lastApplication) throw new FormatException($"Génération impossible pour un déclencheur détaché de « {bearer.Id} » : {generation}.");
            var gone = new StatusInstance
            {
                Id = instance, Generation = generation, Definition = definition, Key = new StatusKey(definitionId, null), Owner = bearer,
                Origin = Provenance.NewRoot("detached", null),
            };
            TriggerSlot slot = Slot(bearer, origin, definitionId, index, n["rank"].Int, definition.Triggers[index], gone);
            slot.ReadyTick = n["readyTick"].Long;
            // Revue Codex de la PR #32 (7e tour) : mêmes bornes que les autres recharges de déclencheurs.
            if (slot.ReadyTick < 0 || slot.ReadyTick > Math.Max(0, CurrentTick - 1 + slot.Definition.CooldownMs / _tickMs))
                throw new FormatException($"Recharge d'un déclencheur détaché de « {bearer.Id} » au-delà de sa durée.");
            return slot;
        }

        private ObservedEvent LoadObserved(SnapshotNode n)
        {
            foreach (string field in new[] { "source", "target" })
                if (n[field].NullableString is string id && !_byId.ContainsKey(id)) throw new FormatException($"Événement observé citant un combattant inconnu : {id}.");
            long eid = n["eid"].Long;
            if (eid < 1) throw new FormatException($"Événement observé non numéroté : {eid}.");
            _maxLoadedEid = Math.Max(_maxLoadedEid, eid);
            Instance(n["instance"].NullableLong);
            return Observed(n);
        }

        private ObservedEvent Observed(SnapshotNode n) => new ObservedEvent
        {
            Kind = Defined<TriggerEvent>(n["kind"]),
            Also = DefinedOrNull<TriggerEvent>(n["also"]),
            Source = n["source"].NullableString,
            Target = n["target"].NullableString,
            Skill = n["skill"].NullableString,
            State = n["state"].NullableString,
            Category = DefinedOrNull<StatusCategory>(n["category"]),
            Instance = n["instance"].NullableLong,
            DamageType = DefinedOrNull<Domain.Stats.DamageType>(n["damageType"]),
            Defense = Defined<HitDefense>(n["defense"]),
            Renewed = n["renewed"].Bool,
            Resource = DefinedOrNull<TriggerResource>(n["resource"]),
            Eid = n["eid"].Long,
            Tick = n["tick"].Long,
            Phase = Phase(n["phase"].Int),
            Provenance = Required(ReadProvenance(n["provenance"])),
            Candidates = new List<TriggerSlot>(),   // lus seulement à la notification, déjà traitée
        };
    }
}
