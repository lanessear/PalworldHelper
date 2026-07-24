using PalworldHelper.Models;

namespace PalworldHelper.Services;

public sealed class BreedingEngine(BreedingDataService data)
{
    public async Task<IReadOnlyList<BreedingRoute>> FindRoutesAsync(
        string target,
        IReadOnlyList<string> passives,
        IReadOnlyList<OwnedPal> owned,
        bool useOnlyOwned,
        int maxDepth,
        int maxResults)
    {
        var combinations = await data.GetCombinationsAsync();
        var byChild = combinations.GroupBy(x => x.Child, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var ownedSpecies = owned.Select(x => x.DisplayName).Concat(owned.Select(x => x.SpeciesId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var carriers = owned.Where(p => passives.All(wanted => p.PassiveSkills.Contains(wanted, StringComparer.OrdinalIgnoreCase)))
            .Select(p => p.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (passives.Count == 0) carriers = ownedSpecies.ToArray();

        var routes = new List<BreedingRoute>();
        foreach (var carrier in carriers.DefaultIfEmpty("Any owned Pal"))
        {
            var queue = new Queue<(string Current, List<BreedingStep> Steps, HashSet<string> Seen)>();
            queue.Enqueue((target, [], new(StringComparer.OrdinalIgnoreCase) { target }));
            while (queue.Count > 0 && routes.Count < maxResults * 8)
            {
                var (current, steps, seen) = queue.Dequeue();
                if (steps.Count >= maxDepth || !byChild.TryGetValue(current, out var pairs)) continue;
                foreach (var pair in pairs)
                {
                    var p1Owned = ownedSpecies.Contains(pair.Parent1);
                    var p2Owned = ownedSpecies.Contains(pair.Parent2);
                    if (useOnlyOwned && !p1Owned && !p2Owned) continue;
                    var nextSteps = new List<BreedingStep>(steps)
                    {
                        new(pair.Parent1, pair.Parent2, pair.Child, p1Owned, p2Owned, steps.Count + 1)
                    };
                    var hasCarrier = pair.Parent1.Equals(carrier, StringComparison.OrdinalIgnoreCase)
                                  || pair.Parent2.Equals(carrier, StringComparison.OrdinalIgnoreCase)
                                  || (carrier == "Any owned Pal" && (p1Owned || p2Owned));
                    if (hasCarrier)
                    {
                        var missing = nextSteps.SelectMany(s => new[] { (s.Parent1, s.Parent1Owned), (s.Parent2, s.Parent2Owned) })
                            .Where(x => !x.Item2).Select(x => x.Item1).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                        routes.Add(new BreedingRoute(target, passives, nextSteps.AsEnumerable().Reverse().ToArray(), missing,
                            Math.Max(1, nextSteps.Count * (passives.Count == 0 ? 1 : 4)), carrier));
                    }
                    foreach (var parent in new[] { pair.Parent1, pair.Parent2 })
                    {
                        if (seen.Contains(parent)) continue;
                        var nextSeen = new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase) { parent };
                        queue.Enqueue((parent, nextSteps, nextSeen));
                    }
                }
            }
        }

        return routes
            .DistinctBy(r => string.Join('|', r.Steps.Select(s => $"{s.Parent1}+{s.Parent2}>{s.Child}")), StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r.MissingParents).ThenBy(r => r.Steps.Count).ThenBy(r => r.EstimatedEggs)
            .Take(maxResults).ToArray();
    }
}
