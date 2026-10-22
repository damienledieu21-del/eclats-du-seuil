using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EdS.Data.Json;
using EdS.Data.Json.Text;
using EdS.Data.Validation;
using EdS.Domain.Combat;
using EdS.Domain.Numerics;
using EdS.Domain.Space;
using EdS.Simulation.Engine;

namespace EdS.Net
{
    /// <summary>
    /// Lot 10 : écriture et lecture des messages du protocole. L'écriture est déterministe (<see cref="JsonLineWriter"/>) ; la lecture
    /// passe par l'analyseur strict du sous-lot 9.1 et refuse tout écart : type inconnu ou de l'autre sens, champ inconnu, manquant ou
    /// en double, type JSON inattendu, nombre hors plage, identifiant, nom ou jeton mal formé, liste trop longue. La lecture ne lève
    /// jamais d'exception sur des octets reçus : elle rend null et la première raison.
    /// </summary>
    public static class MessageCodec
    {
        /// <summary>Préfixe des empreintes de journal échangées (FNV-1a sur 64 bits).</summary>
        public const string HashPrefix = "fnv1a64:";

        public static byte[] Encode(NetMessage message) => Encoding.UTF8.GetBytes(EncodeText(message ?? throw new ArgumentNullException(nameof(message))));

        public static string EncodeText(NetMessage message)
        {
            var w = new JsonLineWriter().BeginObject().Field("type", message.Type);
            switch (message)
            {
                case HelloMessage m:
                    w.Field("protocol", m.ProtocolVersion).Field("name", m.Name);
                    WriteBuild(w.Name("build"), m.Build);
                    break;
                case ReconnectMessage m:
                    w.Field("protocol", m.ProtocolVersion).Field("token", m.Token);
                    break;
                case BuildMessage m:
                    WriteBuild(w.Name("build"), m.Build);
                    break;
                case ReadyMessage m:
                    w.Field("ready", m.Ready);
                    break;
                case InputMessage m:
                    w.Field("guard", m.Guard).Field("parry", m.Parry).Field("slot", (long?)m.Slot).Field("target", m.Target).Field("ally", m.Ally)
                        .Field("heading", (long?)m.Heading);
                    break;
                case PingMessage m:
                    w.Field("clientTime", m.ClientTime);
                    break;
                case ResyncRequestMessage:
                case ByeMessage:
                    break;
                case WelcomeMessage m:
                    w.Field("protocol", m.ProtocolVersion).Field("seat", m.Seat).Field("token", m.Token).Field("tickMs", m.TickMs);
                    break;
                case LobbyMessage m:
                    w.Field("phase", PhaseId(m.Phase)).Name("seats").BeginArray();
                    foreach (SeatInfo seat in m.Seats)
                        w.BeginObject().Field("id", seat.Id).Field("player", seat.Player).Field("connected", seat.Connected)
                            .Field("ready", seat.Ready).Field("build", seat.Build).EndObject();
                    w.EndArray();
                    break;
                case StartMessage m:
                    w.Field("header", m.Header);
                    break;
                case TickMessage m:
                    w.Field("tick", m.Commands.Tick);
                    WriteCommands(w.Name("commands"), m.Commands.Commands);
                    w.Field("hash", m.Hash);
                    break;
                case StateMessage m:
                    w.Field("header", m.Header).Field("snapshot", m.Snapshot).Field("hash", m.Hash);
                    break;
                case RejectedMessage m:
                    w.Field("reason", m.Reason);
                    break;
                case PongMessage m:
                    w.Field("clientTime", m.ClientTime);
                    break;
                case EndMessage m:
                    w.Field("tick", m.Tick).Field("reason", m.Reason);
                    break;
                case ErrorMessage m:
                    w.Field("reason", m.Reason);
                    break;
                default:
                    throw new ArgumentException($"Message inconnu : {message.GetType().Name}.", nameof(message));
            }
            return w.EndObject().ToString();
        }

        private static void WriteBuild(JsonLineWriter w, BuildChoice build)
        {
            w.BeginObject().Field("reference", build.Reference);
            if (build.Talents != null)
            {
                w.Name("talents").BeginObject();
                foreach (KeyValuePair<string, int> talent in build.Talents.OrderBy(t => t.Key, StringComparer.Ordinal))
                    w.Field(talent.Key, talent.Value);
                w.EndObject();
            }
            w.EndObject();
        }

        private static void WriteCommands(JsonLineWriter w, IReadOnlyList<CombatCommand> commands)
        {
            w.BeginArray();
            foreach (CombatCommand c in commands)
            {
                // Sous-lot 11.7 (protocole 2) : déplacement (direction), arrêt, sélection (cible) et départ sont transmis.
                w.BeginObject().Field("atMs", c.AtMs).Field("actor", c.Actor).Field("action", CombatActionIds.Of(c.Action));
                if (c.Action == CombatAction.UseSkill) w.Field("skill", c.Skill).Field("target", c.Target);
                else if (c.Action == CombatAction.Select) w.Field("target", c.Target);
                else if (c.Action == CombatAction.Move) w.Name("direction").BeginArray().Value(c.Direction!.Value.X).Value(c.Direction.Value.Z).EndArray();
                w.EndObject();
            }
            w.EndArray();
        }

        private static string PhaseId(RoomPhase phase) => phase switch
        {
            RoomPhase.Lobby => "lobby",
            RoomPhase.Combat => "combat",
            RoomPhase.Ended => "ended",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
        };

        // --- Lecture ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Lit un message attendu dans le sens <paramref name="direction"/> ; null avec la raison dans <paramref name="error"/> s'il est
        /// refusé. Ne lève jamais d'exception sur le contenu reçu.
        /// </summary>
        public static NetMessage? Decode(byte[] payload, MessageDirection direction, out string? error)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            var report = new ValidationReport();
            NetMessage? message = null;
            JsonInput.ParseDocument(payload, report, (input, root) => message = new Reader(input).Read(root, direction));
            if (message is null || !report.IsValid)
            {
                error = report.Errors.Count > 0 ? report.Errors[0].ToString() : "message invalide.";
                return null;
            }
            error = null;
            return message;
        }

        /// <summary>Jeton de reconnexion bien formé : 32 chiffres hexadécimaux minuscules.</summary>
        public static bool IsToken(string? token) =>
            token != null && token.Length == Protocol.TokenLength && token.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        /// <summary>Empreinte bien formée : <c>fnv1a64:</c> suivi de 16 chiffres hexadécimaux minuscules.</summary>
        public static bool IsHash(string? hash) =>
            hash != null && hash.Length == HashPrefix.Length + 16 && hash.StartsWith(HashPrefix, StringComparison.Ordinal)
            && hash.Skip(HashPrefix.Length).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        /// <summary>Identifiant accepté dans un message : 1 à 128 caractères ASCII parmi lettres, chiffres, « . », « _ » et « - ».</summary>
        public static bool IsId(string? id) =>
            id != null && id.Length >= 1 && id.Length <= Protocol.MaxIdLength
            && id.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-');

        /// <summary>
        /// Texte libre accepté (nom de joueur, de build, raison) : de 1 à <paramref name="max"/> caractères, sans caractère de contrôle,
        /// sans demi-paire de substitution, sans espace en tête ou en fin.
        /// </summary>
        public static bool IsText(string? text, int max)
        {
            if (text is null || text.Length < 1 || text.Length > max || text.Trim().Length != text.Length) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsControl(c)) return false;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])) return false;
                    i++;
                }
                else if (char.IsLowSurrogate(c)) return false;
            }
            return true;
        }

        /// <summary>Lecture d'un message : chaque champ obligatoire, de son type, aux valeurs bornées.</summary>
        private sealed class Reader
        {
            private readonly JsonInput _in;

            public Reader(JsonInput input) => _in = input;

            private void Fail(string path, ValidationCode code, string message) => _in.Report.Add(path, code, message, _in.LineOf(path));

            private void Missing(string path, string field) => Fail(path, ValidationCode.MissingField, $"champ « {field} » obligatoire.");

            public NetMessage? Read(JsonElement root, MessageDirection direction)
            {
                if (!_in.RequireKind(root, JsonValueKind.Object, "$", "objet")) return null;
                string? type = null;
                foreach (JsonProperty property in root.EnumerateObject())
                    if (property.Name == "type")
                    {
                        type = _in.Text(property.Value, "$.type");
                        break;
                    }
                if (type is null)
                {
                    if (_in.Report.IsValid) Missing("$", "type");
                    return null;
                }
                MessageDirection? expected = DirectionOf(type);
                if (expected is null)
                {
                    Fail("$.type", ValidationCode.UnknownReference, $"type de message inconnu « {Short(type)} ».");
                    return null;
                }
                if (expected != direction)
                {
                    Fail("$.type", ValidationCode.WrongKind, $"message « {type} » refusé dans ce sens.");
                    return null;
                }
                return type switch
                {
                    HelloMessage.Id => ReadHello(root),
                    ReconnectMessage.Id => ReadReconnect(root),
                    BuildMessage.Id => ReadBuildMessage(root),
                    ReadyMessage.Id => ReadReady(root),
                    InputMessage.Id => ReadInput(root),
                    PingMessage.Id => ReadPing(root),
                    ResyncRequestMessage.Id => ReadEmpty(root, new ResyncRequestMessage()),
                    ByeMessage.Id => ReadEmpty(root, new ByeMessage()),
                    WelcomeMessage.Id => ReadWelcome(root),
                    LobbyMessage.Id => ReadLobby(root),
                    StartMessage.Id => ReadStart(root),
                    TickMessage.Id => ReadTick(root),
                    StateMessage.Id => ReadState(root),
                    RejectedMessage.Id => ReadReason(root, r => new RejectedMessage(r)),
                    PongMessage.Id => ReadPong(root),
                    EndMessage.Id => ReadEnd(root),
                    ErrorMessage.Id => ReadReason(root, r => new ErrorMessage(r)),
                    _ => null,
                };
            }

            private static MessageDirection? DirectionOf(string type) => type switch
            {
                HelloMessage.Id or ReconnectMessage.Id or BuildMessage.Id or ReadyMessage.Id or InputMessage.Id or PingMessage.Id
                    or ResyncRequestMessage.Id or ByeMessage.Id => MessageDirection.ClientToServer,
                WelcomeMessage.Id or LobbyMessage.Id or StartMessage.Id or TickMessage.Id or StateMessage.Id or RejectedMessage.Id
                    or PongMessage.Id or EndMessage.Id or ErrorMessage.Id => MessageDirection.ServerToClient,
                _ => null,
            };

            private static string Short(string text) => text.Length <= 40 ? text : text.Substring(0, 40) + "…";

            /// <summary>Lit les champs d'un objet ; <c>type</c> est déjà lu. Rend false si l'objet n'en est pas un.</summary>
            private bool Fields(JsonElement element, string path, Dictionary<string, Action<JsonElement, string>> fields, bool root = true)
            {
                if (root) fields["type"] = (_, _) => { };
                return _in.ReadFields(element, path, fields);
            }

            private int? Int(JsonElement v, string p, int min, int max)
            {
                int? n = _in.Int32(v, p);
                if (n is int x && (x < min || x > max))
                {
                    Fail(p, ValidationCode.OutOfRange, $"{x} hors de [{min}, {max}].");
                    return null;
                }
                return n;
            }

            private long? Long(JsonElement v, string p, long min, long max)
            {
                long? n = _in.Int64(v, p);
                if (n is long x && (x < min || x > max))
                {
                    Fail(p, ValidationCode.OutOfRange, $"{x} hors de [{min}, {max}].");
                    return null;
                }
                return n;
            }

            private string? Id(JsonElement v, string p, bool nullable = false)
            {
                string? s = nullable ? _in.NullableText(v, p) : _in.Text(v, p);
                if (s != null && !IsId(s))
                {
                    Fail(p, ValidationCode.InvalidValue, $"identifiant invalide (1 à {Protocol.MaxIdLength} caractères parmi a-z, A-Z, 0-9, « . », « _ », « - »).");
                    return null;
                }
                return s;
            }

            private string? FreeText(JsonElement v, string p, int max, bool nullable = false)
            {
                string? s = nullable ? _in.NullableText(v, p) : _in.Text(v, p);
                if (s != null && !IsText(s, max))
                {
                    Fail(p, ValidationCode.InvalidValue, $"texte invalide (1 à {max} caractères, sans caractère de contrôle ni espace en tête ou en fin).");
                    return null;
                }
                return s;
            }

            private string? Token(JsonElement v, string p)
            {
                string? s = _in.Text(v, p);
                if (s != null && !IsToken(s))
                {
                    Fail(p, ValidationCode.InvalidValue, $"jeton invalide ({Protocol.TokenLength} chiffres hexadécimaux minuscules).");
                    return null;
                }
                return s;
            }

            private string? Hash(JsonElement v, string p)
            {
                string? s = _in.Text(v, p);
                if (s != null && !IsHash(s))
                {
                    Fail(p, ValidationCode.InvalidValue, $"empreinte invalide (« {HashPrefix} » suivi de 16 chiffres hexadécimaux minuscules).");
                    return null;
                }
                return s;
            }

            private string? Header(JsonElement v, string p) => Block(v, p, "en-tête");

            /// <summary>Texte d'un bloc transporté tel quel (en-tête du journal, instantané d'état) : ni vide, ni plus long qu'un message.</summary>
            private string? Block(JsonElement v, string p, string what)
            {
                string? s = _in.Text(v, p);
                if (s != null && (s.Length == 0 || s.Length > Protocol.MaxServerMessageBytes))
                {
                    Fail(p, ValidationCode.InvalidValue, what + " vide ou trop long.");
                    return null;
                }
                return s;
            }

            private NetMessage? ReadEmpty(JsonElement root, NetMessage message) =>
                Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>()) ? message : null;

            private BuildChoice? ReadBuild(JsonElement element, string path)
            {
                string? reference = null;
                Dictionary<string, int>? talents = null;
                bool talentsOk = true;
                bool ok = Fields(element, path, new Dictionary<string, Action<JsonElement, string>>
                {
                    ["reference"] = (v, p) => reference = Id(v, p),
                    ["talents"] = (v, p) =>
                    {
                        if (!_in.RequireKind(v, JsonValueKind.Object, p, "objet"))
                        {
                            talentsOk = false;
                            return;
                        }
                        if (v.EnumerateObject().Count > Protocol.MaxTalents)
                        {
                            Fail(p, ValidationCode.LimitExceeded, $"au plus {Protocol.MaxTalents} talents.");
                            talentsOk = false;
                            return;
                        }
                        talents = new Dictionary<string, int>(StringComparer.Ordinal);
                        foreach (JsonProperty property in _in.UniqueProperties(v, p))
                        {
                            string tp = p + "." + property.Name;
                            if (!IsId(property.Name))
                            {
                                Fail(tp, ValidationCode.InvalidValue, "identifiant de talent invalide.");
                                talentsOk = false;
                                continue;
                            }
                            if (Int(property.Value, tp, 1, 100) is int rank) talents[property.Name] = rank;
                            else talentsOk = false;
                        }
                    },
                }, root: false);
                if (!ok || !talentsOk) return null;
                if (reference is null)
                {
                    Missing(path, "reference");
                    return null;
                }
                return new BuildChoice(reference, talents);
            }

            private NetMessage? ReadHello(JsonElement root)
            {
                int? protocol = null;
                string? name = null;
                BuildChoice? build = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["protocol"] = (v, p) => protocol = Int(v, p, 1, int.MaxValue),
                    ["name"] = (v, p) => name = FreeText(v, p, Protocol.MaxNameLength),
                    ["build"] = (v, p) => build = ReadBuild(v, p),
                })) return null;
                if (protocol is null || name is null || build is null)
                {
                    if (_in.Report.IsValid) Missing("$", protocol is null ? "protocol" : name is null ? "name" : "build");
                    return null;
                }
                return new HelloMessage(protocol.Value, name, build);
            }

            private NetMessage? ReadReconnect(JsonElement root)
            {
                int? protocol = null;
                string? token = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["protocol"] = (v, p) => protocol = Int(v, p, 1, int.MaxValue),
                    ["token"] = (v, p) => token = Token(v, p),
                })) return null;
                if (protocol is null || token is null)
                {
                    if (_in.Report.IsValid) Missing("$", protocol is null ? "protocol" : "token");
                    return null;
                }
                return new ReconnectMessage(protocol.Value, token);
            }

            private NetMessage? ReadBuildMessage(JsonElement root)
            {
                BuildChoice? build = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>> { ["build"] = (v, p) => build = ReadBuild(v, p) })) return null;
                if (build is null)
                {
                    if (_in.Report.IsValid) Missing("$", "build");
                    return null;
                }
                return new BuildMessage(build);
            }

            private NetMessage? ReadReady(JsonElement root)
            {
                bool? ready = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>> { ["ready"] = (v, p) => ready = _in.Boolean(v, p) })) return null;
                if (ready is null)
                {
                    if (_in.Report.IsValid) Missing("$", "ready");
                    return null;
                }
                return new ReadyMessage(ready.Value);
            }

            private NetMessage? ReadInput(JsonElement root)
            {
                bool? guard = null, parry = null;
                int? slot = null, heading = null;
                string? target = null, ally = null;
                bool hasSlot = false, hasTarget = false, hasAlly = false, hasHeading = false;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["heading"] = (v, p) =>
                    {
                        hasHeading = true;
                        if (v.ValueKind != JsonValueKind.Null) heading = Int(v, p, 0, EdS.Runtime.MoveInput.Headings - 1) ?? -1;
                    },
                    ["guard"] = (v, p) => guard = _in.Boolean(v, p),
                    ["parry"] = (v, p) => parry = _in.Boolean(v, p),
                    ["slot"] = (v, p) =>
                    {
                        hasSlot = true;
                        if (v.ValueKind != JsonValueKind.Null) slot = Int(v, p, 0, Protocol.MaxSlot) ?? -1;
                    },
                    ["target"] = (v, p) =>
                    {
                        hasTarget = true;
                        target = Id(v, p, nullable: true);
                    },
                    ["ally"] = (v, p) =>
                    {
                        hasAlly = true;
                        ally = Id(v, p, nullable: true);
                    },
                })) return null;
                if (guard is null || parry is null || !hasSlot || !hasTarget || !hasAlly || !hasHeading)
                {
                    if (_in.Report.IsValid)
                        Missing("$", guard is null ? "guard" : parry is null ? "parry" : !hasSlot ? "slot" : !hasTarget ? "target" : !hasAlly ? "ally" : "heading");
                    return null;
                }
                if (slot == -1 || heading == -1 || !_in.Report.IsValid) return null;
                return new InputMessage(guard.Value, parry.Value, slot, target, ally, heading);
            }

            private NetMessage? ReadPing(JsonElement root)
            {
                long? time = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>> { ["clientTime"] = (v, p) => time = _in.Int64(v, p) })) return null;
                if (time is null)
                {
                    if (_in.Report.IsValid) Missing("$", "clientTime");
                    return null;
                }
                return new PingMessage(time.Value);
            }

            private NetMessage? ReadPong(JsonElement root)
            {
                long? time = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>> { ["clientTime"] = (v, p) => time = _in.Int64(v, p) })) return null;
                if (time is null)
                {
                    if (_in.Report.IsValid) Missing("$", "clientTime");
                    return null;
                }
                return new PongMessage(time.Value);
            }

            private NetMessage? ReadWelcome(JsonElement root)
            {
                int? protocol = null, tickMs = null;
                string? seat = null, token = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["protocol"] = (v, p) => protocol = Int(v, p, 1, int.MaxValue),
                    ["seat"] = (v, p) => seat = Id(v, p),
                    ["token"] = (v, p) => token = Token(v, p),
                    ["tickMs"] = (v, p) => tickMs = Int(v, p, 1, 60_000),
                })) return null;
                if (protocol is null || seat is null || token is null || tickMs is null)
                {
                    if (_in.Report.IsValid) Missing("$", protocol is null ? "protocol" : seat is null ? "seat" : token is null ? "token" : "tickMs");
                    return null;
                }
                return new WelcomeMessage(protocol.Value, seat, token, tickMs.Value);
            }

            private SeatInfo? ReadSeat(JsonElement element, string path)
            {
                string? id = null, player = null, build = null;
                bool? connected = null, ready = null;
                bool hasPlayer = false, hasBuild = false;
                if (!Fields(element, path, new Dictionary<string, Action<JsonElement, string>>
                {
                    ["id"] = (v, p) => id = Id(v, p),
                    ["player"] = (v, p) =>
                    {
                        hasPlayer = true;
                        player = FreeText(v, p, Protocol.MaxNameLength, nullable: true);
                    },
                    ["connected"] = (v, p) => connected = _in.Boolean(v, p),
                    ["ready"] = (v, p) => ready = _in.Boolean(v, p),
                    ["build"] = (v, p) =>
                    {
                        hasBuild = true;
                        build = FreeText(v, p, Protocol.MaxIdLength, nullable: true);
                    },
                }, root: false)) return null;
                if (id is null || connected is null || ready is null || !hasPlayer || !hasBuild)
                {
                    if (_in.Report.IsValid)
                        Missing(path, id is null ? "id" : connected is null ? "connected" : ready is null ? "ready" : !hasPlayer ? "player" : "build");
                    return null;
                }
                return new SeatInfo(id, player, connected.Value, ready.Value, build);
            }

            private NetMessage? ReadLobby(JsonElement root)
            {
                RoomPhase? phase = null;
                List<SeatInfo?>? seats = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["phase"] = (v, p) =>
                    {
                        string? text = _in.Text(v, p);
                        phase = text switch { "lobby" => RoomPhase.Lobby, "combat" => RoomPhase.Combat, "ended" => RoomPhase.Ended, _ => null };
                        if (text != null && phase is null) Fail(p, ValidationCode.UnknownReference, "phase inconnue (lobby, combat, ended).");
                    },
                    ["seats"] = (v, p) => seats = _in.ReadLimitedList(v, p, Protocol.MaxSeats, n => $"{n} places, au plus {Protocol.MaxSeats}.", ReadSeat),
                })) return null;
                if (phase is null || seats is null)
                {
                    if (_in.Report.IsValid) Missing("$", phase is null ? "phase" : "seats");
                    return null;
                }
                if (seats.Any(s => s is null)) return null;
                return new LobbyMessage(phase.Value, seats.Select(s => s!).ToArray());
            }

            private NetMessage? ReadStart(JsonElement root)
            {
                string? header = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>> { ["header"] = (v, p) => header = Header(v, p) })) return null;
                if (header is null)
                {
                    if (_in.Report.IsValid) Missing("$", "header");
                    return null;
                }
                return new StartMessage(header);
            }

            private CombatCommand? ReadCommand(JsonElement element, string path)
            {
                long? atMs = null;
                string? actor = null, action = null, skill = null, target = null;
                bool hasSkill = false, hasTarget = false, hasDirection = false;
                GroundVector? direction = null;
                if (!Fields(element, path, new Dictionary<string, Action<JsonElement, string>>
                {
                    ["direction"] = (v, p) =>
                    {
                        hasDirection = true;
                        direction = Direction(v, p);
                    },
                    ["atMs"] = (v, p) => atMs = Long(v, p, 0, long.MaxValue / 2),
                    ["actor"] = (v, p) => actor = Id(v, p),
                    ["action"] = (v, p) => action = _in.Text(v, p),
                    ["skill"] = (v, p) =>
                    {
                        hasSkill = true;
                        skill = Id(v, p);
                    },
                    ["target"] = (v, p) =>
                    {
                        hasTarget = true;
                        target = Id(v, p, nullable: true);
                    },
                }, root: false)) return null;
                if (atMs is null || actor is null || action is null)
                {
                    if (_in.Report.IsValid) Missing(path, atMs is null ? "atMs" : actor is null ? "actor" : "action");
                    return null;
                }
                if (action == CombatActionIds.UseSkill)
                {
                    if (!hasSkill || !hasTarget || hasDirection)
                    {
                        if (_in.Report.IsValid)
                        {
                            if (hasDirection) Fail(path, ValidationCode.UnknownField, "« direction » réservée à l'action « move ».");
                            else Missing(path, !hasSkill ? "skill" : "target");
                        }
                        return null;
                    }
                    return skill is null ? null : new CombatCommand(atMs.Value, actor, skill, target);
                }
                // Sous-lot 11.7 (protocole 2) : marche (direction), arrêt, sélection (cible, ou null), départ.
                if (action == CombatActionIds.Move || action == CombatActionIds.Select)
                {
                    bool move = action == CombatActionIds.Move;
                    if (hasSkill || (move ? hasTarget : hasDirection))
                    {
                        Fail(path, ValidationCode.UnknownField, move ? "« direction » seulement pour « move »." : "« target » seulement pour « select ».");
                        return null;
                    }
                    if (move ? !hasDirection : !hasTarget)
                    {
                        if (_in.Report.IsValid) Missing(path, move ? "direction" : "target");
                        return null;
                    }
                    if (!_in.Report.IsValid) return null;
                    return move ? (direction is { } d ? new CombatCommand(atMs.Value, actor, d) : null) : CombatCommand.SelectTarget(atMs.Value, actor, target);
                }
                CombatAction plain;
                if (action == CombatActionIds.Stop) plain = CombatAction.Stop;
                else if (action == CombatActionIds.Leave) plain = CombatAction.Leave;
                else if (!CombatActionIds.TryParseDefensive(action, out plain))
                {
                    Fail(path + ".action", ValidationCode.UnknownReference, "action inconnue (skill, guardStart, guardStop, parry, move, stop, select, leave).");
                    return null;
                }
                if (hasSkill || hasTarget || hasDirection)
                {
                    Fail(path, ValidationCode.UnknownField, "« skill », « target » et « direction » réservés aux actions « skill », « select » et « move ».");
                    return null;
                }
                return new CombatCommand(atMs.Value, actor, plain);
            }

            private NetMessage? ReadTick(JsonElement root)
            {
                long? tick = null;
                List<CombatCommand?>? commands = null;
                string? hash = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["tick"] = (v, p) => tick = Long(v, p, 0, long.MaxValue / 2),
                    ["commands"] = (v, p) => commands = _in.ReadLimitedList(v, p, Protocol.MaxCommandsPerTick,
                        n => $"{n} commandes, au plus {Protocol.MaxCommandsPerTick} par tick.", ReadCommand),
                    ["hash"] = (v, p) => hash = Hash(v, p),
                })) return null;
                if (tick is null || commands is null || hash is null)
                {
                    if (_in.Report.IsValid) Missing("$", tick is null ? "tick" : commands is null ? "commands" : "hash");
                    return null;
                }
                if (commands.Any(c => c is null)) return null;
                return new TickMessage(new TickCommands(tick.Value, commands.Select(c => c!).ToArray()), hash);
            }

            /// <summary>Sous-lot 11.7 : direction d'un déplacement, [x, z] en décimaux de 4 chiffres au plus, composantes dans [−1, 1], non nulle.</summary>
            private GroundVector? Direction(JsonElement v, string p)
            {
                if (!_in.RequireKind(v, JsonValueKind.Array, p, "tableau [x, z]")) return null;
                IReadOnlyList<JsonElement> items = v.EnumerateArray();
                if (items.Count != 2)
                {
                    Fail(p, ValidationCode.InvalidValue, "direction attendue sous la forme [x, z].");
                    return null;
                }
                Fixed? x = _in.Decimal(items[0], p + "[0]"), z = _in.Decimal(items[1], p + "[1]");
                if (x is null || z is null) return null;
                var direction = new GroundVector(x.Value, z.Value);
                if (!SpaceLimits.IsDirection(direction))
                {
                    Fail(p, ValidationCode.OutOfRange, "direction : composantes dans [−1, 1], non toutes deux nulles.");
                    return null;
                }
                return direction;
            }

            private NetMessage? ReadState(JsonElement root)
            {
                string? header = null, snapshot = null, hash = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["header"] = (v, p) => header = Header(v, p),
                    ["snapshot"] = (v, p) => snapshot = Block(v, p, "instantané"),
                    ["hash"] = (v, p) => hash = Hash(v, p),
                })) return null;
                if (header is null || snapshot is null || hash is null)
                {
                    if (_in.Report.IsValid) Missing("$", header is null ? "header" : snapshot is null ? "snapshot" : "hash");
                    return null;
                }
                return new StateMessage(header, snapshot, hash);
            }

            private NetMessage? ReadReason(JsonElement root, Func<string, NetMessage> create)
            {
                string? reason = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["reason"] = (v, p) => reason = FreeText(v, p, Protocol.MaxReasonLength),
                })) return null;
                if (reason is null)
                {
                    if (_in.Report.IsValid) Missing("$", "reason");
                    return null;
                }
                return create(reason);
            }

            private NetMessage? ReadEnd(JsonElement root)
            {
                long? tick = null;
                string? reason = null;
                if (!Fields(root, "$", new Dictionary<string, Action<JsonElement, string>>
                {
                    ["tick"] = (v, p) => tick = Long(v, p, 0, long.MaxValue / 2),
                    ["reason"] = (v, p) => reason = FreeText(v, p, Protocol.MaxReasonLength),
                })) return null;
                if (tick is null || reason is null)
                {
                    if (_in.Report.IsValid) Missing("$", tick is null ? "tick" : "reason");
                    return null;
                }
                return new EndMessage(tick.Value, reason);
            }
        }
    }
}
