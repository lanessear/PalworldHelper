using System.IO;
using System.Text.Json;

namespace PalworldHelper;

public sealed class BreedingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string mate, string child)>> _graph = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Names => _names.Values.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
    public int ResultCount { get; private set; }

    public void Load(string path)
    {
        var json = File.ReadAllText(path);
        var payload = JsonSerializer.Deserialize<BreedingPayload>(json, JsonOptions)
            ?? throw new InvalidDataException("JSON konnte nicht gelesen werden.");
        _names.Clear(); _graph.Clear(); ResultCount = 0;
        foreach (var r in payload.Results)
        {
            if (string.IsNullOrWhiteSpace(r.Parent1) || string.IsNullOrWhiteSpace(r.Parent2) || string.IsNullOrWhiteSpace(r.Child)) continue;
            AddName(r.Parent1); AddName(r.Parent2); AddName(r.Child);
            AddEdge(r.Parent1, r.Parent2, r.Child);
            if (!r.Parent1.Equals(r.Parent2, StringComparison.OrdinalIgnoreCase)) AddEdge(r.Parent2, r.Parent1, r.Child);
            ResultCount++;
        }
    }

    private void AddName(string name) => _names.TryAdd(name.Trim(), name.Trim());
    private void AddEdge(string from, string mate, string child)
    {
        if (!_graph.TryGetValue(from.Trim(), out var edges)) _graph[from.Trim()] = edges = [];
        edges.Add((mate.Trim(), child.Trim()));
    }

    public IReadOnlyList<BreedingStep>? FindShortest(string start, string target)
    {
        if (start.Equals(target, StringComparison.OrdinalIgnoreCase)) return [];
        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var previous = new Dictionary<string, (string prior, string mate)>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_graph.TryGetValue(current, out var edges)) continue;
            foreach (var edge in edges)
            {
                if (!visited.Add(edge.child)) continue;
                previous[edge.child] = (current, edge.mate);
                if (edge.child.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    var steps = new List<BreedingStep>();
                    var cursor = target;
                    while (!cursor.Equals(start, StringComparison.OrdinalIgnoreCase))
                    {
                        var p = previous[cursor];
                        steps.Add(new BreedingStep(p.prior, p.mate, cursor));
                        cursor = p.prior;
                    }
                    steps.Reverse();
                    return steps;
                }
                queue.Enqueue(edge.child);
            }
        }
        return null;
    }
}
