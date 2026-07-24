using System.Text.Json;
using PalworldHelper.Models;

namespace PalworldHelper.Services;

public sealed class BreedingDataService
{
    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<BreedingCombination>? _cache;

    public BreedingDataService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<IReadOnlyList<BreedingCombination>> GetCombinationsAsync()
    {
        if (_cache is not null) return _cache;
        await _gate.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;
            var path = Path.Combine(_environment.ContentRootPath, "data", "breeding.json");
            if (!File.Exists(path)) return _cache = [];
            await using var stream = File.OpenRead(path);
            var raw = await JsonSerializer.DeserializeAsync<List<BreedingCombination>>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return _cache = raw ?? [];
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> GetPalNamesAsync() =>
        (await GetCombinationsAsync()).SelectMany(x => new[] { x.Parent1, x.Parent2, x.Child })
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
}
