using System.IO;
using System.Text.Json;

namespace PalworldHelper;

public sealed class BreedingService
{
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<(string mate, string child)>> _graph = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ParentChoice>> _parentsByChild = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Names => _names.Values.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
    public int ResultCount { get; private set; }

    public void Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The breeding JSON must contain a JSON object.");

        _names.Clear();
        _graph.Clear();
        _parentsByChild.Clear();
        ResultCount = 0;

        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The breeding JSON does not contain a results array.");

        // Compact repository format: pals/names contains the Pal names and every result is
        // [parent1Index, parent2Index, childIndex].
        if (TryReadNameTable(root, out var nameTable) && IsCompactResultArray(results))
        {
            foreach (var result in results.EnumerateArray())
            {
                if (result.GetArrayLength() < 3) continue;
                var indexes = result.EnumerateArray().Take(3).Select(x => x.GetInt32()).ToArray();
                if (indexes.Any(x => x < 0 || x >= nameTable.Count)) continue;
                AddResult(nameTable[indexes[0]], nameTable[indexes[1]], nameTable[indexes[2]]);
            }
            return;
        }

        // Original IndexedDB/object format.
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object) continue;
            var parent1 = ReadString(result, "parent1", "Parent1");
            var parent2 = ReadString(result, "parent2", "Parent2");
            var child = ReadString(result, "child", "Child");
            AddResult(parent1, parent2, child);
        }

        if (ResultCount == 0)
            throw new InvalidDataException("No valid breeding combinations were found. Supported formats are the IndexedDB export and the compact repository JSON.");
    }

    private static bool TryReadNameTable(JsonElement root, out List<string> names)
    {
        names = [];
        JsonElement table;
        if (!(root.TryGetProperty("pals", out table) || root.TryGetProperty("names", out table)) || table.ValueKind != JsonValueKind.Array)
            return false;

        names = table.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()?.Trim() ?? string.Empty)
            .ToList();
        return names.Count > 0;
    }

    private static bool IsCompactResultArray(JsonElement results)
    {
        foreach (var item in results.EnumerateArray())
            return item.ValueKind == JsonValueKind.Array;
        return false;
    }

    private static string ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString()?.Trim() ?? string.Empty;
        return string.Empty;
    }

    private void AddResult(string parent1, string parent2, string child)
    {
        if (string.IsNullOrWhiteSpace(parent1) || string.IsNullOrWhiteSpace(parent2) || string.IsNullOrWhiteSpace(child)) return;
        if (parent1.Equals(parent2, StringComparison.OrdinalIgnoreCase)) return;
        AddName(parent1); AddName(parent2); AddName(child);
        AddEdge(parent1, parent2, child);
        AddEdge(parent2, parent1, child);
        if (!_parentsByChild.TryGetValue(child.Trim(), out var parents)) _parentsByChild[child.Trim()] = parents = [];
        parents.Add(new ParentChoice(parent1.Trim(), parent2.Trim()));
        ResultCount++;
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

    public IReadOnlyList<BreedingPlanStep>? FindShortestFromAvailable(string target, IReadOnlySet<string>? availableSpecies)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        var unrestricted = availableSpecies is null || availableSpecies.Count == 0;
        var startingSpecies = unrestricted
            ? Names.Where(species => !species.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList()
            : Names.Where(species => !species.Equals(target, StringComparison.OrdinalIgnoreCase) && availableSpecies!.Contains(species)).ToList();
        if (startingSpecies.Count == 0)
            return null;

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previous = new Dictionary<string, (string prior, string mate)>(StringComparer.OrdinalIgnoreCase);

        foreach (var species in startingSpecies)
        {
            if (visited.Add(species)) queue.Enqueue(species);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_graph.TryGetValue(current, out var edges)) continue;

            foreach (var edge in edges)
            {
                if (!unrestricted && !availableSpecies!.Contains(edge.mate)) continue;
                if (!visited.Add(edge.child)) continue;

                previous[edge.child] = (current, edge.mate);
                if (edge.child.Equals(target, StringComparison.OrdinalIgnoreCase))
                    return BuildPlan(target, previous, availableSpecies, unrestricted);

                queue.Enqueue(edge.child);
            }
        }

        return null;
    }

    public IReadOnlyList<BreedingPlanStep>? FindShortestFromStart(string start, string target, IReadOnlySet<string>? availableSpecies)
    {
        if (start.Equals(target, StringComparison.OrdinalIgnoreCase)) return [];

        var unrestricted = availableSpecies is null || availableSpecies.Count == 0;
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
                if (!unrestricted && !availableSpecies!.Contains(edge.mate)) continue;
                if (!visited.Add(edge.child)) continue;

                previous[edge.child] = (current, edge.mate);
                if (edge.child.Equals(target, StringComparison.OrdinalIgnoreCase))
                    return BuildPlan(target, previous, availableSpecies, unrestricted);

                queue.Enqueue(edge.child);
            }
        }

        return null;
    }

    public IReadOnlyList<IReadOnlyList<BreedingPlanStep>> FindAllShortestPathsFromAvailable(string target, IReadOnlySet<string>? availableSpecies)
    {
        if (string.IsNullOrWhiteSpace(target)) return [];

        var unrestricted = availableSpecies is null || availableSpecies.Count == 0;
        var startingSpecies = Names
            .Where(species => !species.Equals(target, StringComparison.OrdinalIgnoreCase))
            .Where(species => unrestricted || availableSpecies!.Contains(species))
            .ToList();

        if (startingSpecies.Count == 0)
            return [];

        var shortestPaths = new List<IReadOnlyList<BreedingPlanStep>>();
        var bestLength = int.MaxValue;
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in startingSpecies)
        {
            var pathsFromStart = FindAllShortestPathsFromStart(start, target, availableSpecies);
            foreach (var path in pathsFromStart)
            {
                if (path.Count < bestLength)
                {
                    bestLength = path.Count;
                    shortestPaths.Clear();
                }

                if (path.Count != bestLength) continue;

                var canonicalPath = string.Join("||", path.Select(step => $"{step.Parent}|{step.Mate}|{step.Child}|{step.ParentOwned}|{step.MateOwned}"));
                if (seenPaths.Add(canonicalPath))
                    shortestPaths.Add(path);
            }
        }

        return shortestPaths;
    }

    private IReadOnlyList<IReadOnlyList<BreedingPlanStep>> FindAllShortestPathsFromStart(string start, string target, IReadOnlySet<string>? availableSpecies)
    {
        if (start.Equals(target, StringComparison.OrdinalIgnoreCase)) return [[]];

        var unrestricted = availableSpecies is null || availableSpecies.Count == 0;
        var queue = new Queue<(string Current, IReadOnlyList<BreedingPlanStep> Path)>();
        var paths = new List<IReadOnlyList<BreedingPlanStep>>();
        var bestLength = int.MaxValue;

        queue.Enqueue((start, []));
        while (queue.Count > 0)
        {
            var (current, currentPath) = queue.Dequeue();
            if (currentPath.Count >= bestLength)
                continue;

            if (!_graph.TryGetValue(current, out var edges)) continue;

            foreach (var edge in edges)
            {
                if (!unrestricted && !availableSpecies!.Contains(edge.mate)) continue;
                var nextPath = currentPath.Append(new BreedingPlanStep(current, edge.mate, edge.child, unrestricted || availableSpecies?.Contains(current) == true, unrestricted || availableSpecies?.Contains(edge.mate) == true)).ToList();
                if (edge.child.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    if (nextPath.Count < bestLength)
                    {
                        bestLength = nextPath.Count;
                        paths.Clear();
                    }

                    if (nextPath.Count == bestLength)
                        paths.Add(nextPath);
                    continue;
                }

                queue.Enqueue((edge.child, nextPath));
            }
        }

        return paths;
    }

    public IReadOnlyList<ParentChoice> GetParentChoices(string child)
        => _parentsByChild.TryGetValue(child.Trim(), out var parents)
            ? parents.OrderBy(x => x.Parent1, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Parent2, StringComparer.CurrentCultureIgnoreCase).ToList()
            : [];

    private static List<BreedingPlanStep> BuildPlan(
        string target,
        Dictionary<string, (string prior, string mate)> previous,
        IReadOnlySet<string>? availableSpecies,
        bool unrestricted)
    {
        var steps = new List<BreedingPlanStep>();
        var cursor = target;
        while (previous.TryGetValue(cursor, out var item))
        {
            steps.Add(new BreedingPlanStep(
                item.prior,
                item.mate,
                cursor,
                unrestricted || availableSpecies?.Contains(item.prior) == true,
                unrestricted || availableSpecies?.Contains(item.mate) == true));
            cursor = item.prior;
        }

        steps.Reverse();
        return steps;
    }
}
