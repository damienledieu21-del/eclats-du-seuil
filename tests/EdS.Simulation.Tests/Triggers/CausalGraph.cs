using System.Text.Json;

namespace EdS.TestSupport;

/// <summary>
/// Lot 7.3 : graphe causal reconstruit à partir du seul journal (format 4). Chaque ligne de jeu porte <c>eid</c> et <c>cause</c> ;
/// l'effet racine porte <c>root</c> ; un effet de profondeur ≥ 1 porte <c>depth</c> et <c>trigger</c>. La racine, la profondeur
/// et la chaîne de chaque effet se déduisent en remontant <c>cause</c>. <see cref="Build"/> vérifie au passage tous les invariants
/// de la provenance et lève une exception (message lisible) au premier écart. Fichier partagé par les projets de tests.
/// </summary>
public sealed class CausalGraph
{
    /// <summary>Champs de provenance du format 4 (lot 7.3), absents des diagnostics.</summary>
    public static readonly string[] ProvenanceFields = { "eid", "cause", "root", "depth", "trigger", "deferredFromTick" };

    public sealed record Node(long Eid, long Seq, string Kind, long? Cause, string Root, int Depth, IReadOnlyList<string> Chain, string? Trigger);

    private readonly Dictionary<long, Node> _nodes = new();
    private readonly Dictionary<string, Node> _roots = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<long, Node> Nodes => _nodes;

    /// <summary>Effet racine de chaque racine (une seule ligne avec <c>cause: null</c> par racine).</summary>
    public IReadOnlyDictionary<string, Node> Roots => _roots;

    /// <summary>Nombre de diagnostics (lignes sans eid).</summary>
    public int Diagnostics { get; private set; }

    public Node this[long eid] => _nodes[eid];

    /// <summary>Lignes de journal après l'en-tête, ou journal complet (l'en-tête, de type « header », est ignoré).</summary>
    public static CausalGraph Build(IEnumerable<string> lines) =>
        Build(lines.Select(l => JsonDocument.Parse(l).RootElement.Clone()).Where(e => !(e.TryGetProperty("type", out JsonElement type) && type.GetString() == "header")));

    public static CausalGraph Build(IEnumerable<JsonElement> events)
    {
        var graph = new CausalGraph();
        long lastEid = 0;
        foreach (JsonElement e in events)
        {
            string kind = e.GetProperty("event").GetString()!;
            long seq = e.GetProperty("seq").GetInt64();
            if (!e.TryGetProperty("eid", out JsonElement eidElement))
            {
                foreach (string field in ProvenanceFields)
                    if (e.TryGetProperty(field, out _)) Fail(seq, $"diagnostic « {kind} » avec un champ de provenance « {field} »");
                graph.Diagnostics++;
                continue;
            }
            long eid = eidElement.GetInt64();
            if (eid <= lastEid) Fail(seq, $"eid {eid} non croissant (précédent {lastEid})");
            lastEid = eid;
            if (!e.TryGetProperty("cause", out JsonElement causeElement)) Fail(seq, "ligne de jeu sans cause");
            long? cause = causeElement.ValueKind == JsonValueKind.Null ? null : causeElement.GetInt64();
            bool hasRoot = e.TryGetProperty("root", out JsonElement rootElement);
            int depth = e.TryGetProperty("depth", out JsonElement depthElement) ? depthElement.GetInt32() : 0;
            string? trigger = e.TryGetProperty("trigger", out JsonElement triggerElement) ? triggerElement.GetString() : null;
            if (depth == 0 && (e.TryGetProperty("depth", out _) || trigger != null)) Fail(seq, "depth et trigger écrits seulement à partir de la profondeur 1");
            if (depth > 0 && trigger is null) Fail(seq, "profondeur ≥ 1 sans trigger");

            Node node;
            if (cause is null)
            {
                if (!hasRoot) Fail(seq, "effet racine sans root");
                if (depth != 0) Fail(seq, "effet racine de profondeur non nulle");
                string root = rootElement.GetString()!;
                node = new Node(eid, seq, kind, null, root, 0, Array.Empty<string>(), null);
                if (!graph._roots.TryAdd(root, node)) Fail(seq, $"racine « {root} » déjà ouverte par l'eid {graph._roots[root].Eid}");
            }
            else
            {
                if (hasRoot) Fail(seq, "root écrit sur un effet qui n'est pas racine");
                if (!graph._nodes.TryGetValue(cause.Value, out Node? parent)) Fail(seq, $"cause {cause} inconnue ou postérieure");
                IReadOnlyList<string> chain;
                if (depth == parent!.Depth)
                {
                    if (trigger != parent.Trigger) Fail(seq, $"conséquence directe avec un autre déclencheur ({trigger} ≠ {parent.Trigger})");
                    chain = parent.Chain;
                }
                else if (depth == parent.Depth + 1)
                {
                    if (parent.Chain.Contains(trigger!)) Fail(seq, $"cycle : {trigger} déjà dans la chaîne {string.Join(" → ", parent.Chain)}");
                    chain = parent.Chain.Append(trigger!).ToArray();
                }
                else
                {
                    Fail(seq, $"profondeur {depth} incompatible avec celle du parent ({parent.Depth})");
                    return graph;
                }
                node = new Node(eid, seq, kind, cause, parent.Root, depth, chain, trigger);
            }
            if (node.Chain.Count != node.Depth) Fail(seq, "longueur de chaîne différente de la profondeur");
            graph._nodes.Add(eid, node);
        }
        return graph;
    }

    /// <summary>Ancêtres d'un effet, du parent immédiat à l'effet racine.</summary>
    public IEnumerable<Node> Ancestors(long eid)
    {
        for (long? cause = _nodes[eid].Cause; cause is long c; cause = _nodes[c].Cause) yield return _nodes[c];
    }

    private static void Fail(long seq, string message) => throw new InvalidOperationException($"Graphe causal invalide à la ligne seq {seq} : {message}.");
}
