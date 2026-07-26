using System.IO;
using System.Text.Json;

namespace PalworldHelper;

public sealed class PalNameCatalog
{
    private readonly Dictionary<string, string> _displayByName = new(StringComparer.OrdinalIgnoreCase);

    private PalNameCatalog()
    {
    }

    public static PalNameCatalog Load()
    {
        var catalog = new PalNameCatalog();
        foreach (var path in CandidatePaths().Where(File.Exists))
        {
            catalog.LoadFile(path);
            break;
        }

        return catalog;
    }

    public string DisplayName(string name)
        => string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : _displayByName.TryGetValue(name.Trim(), out var displayName)
                ? displayName
                : name.Trim();

    public string CanonicalName(string input, IEnumerable<string> availableNames)
    {
        var value = input.Trim();
        var exact = availableNames.FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        return availableNames.FirstOrDefault(name => DisplayName(name).Equals(value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }

    private void LoadFile(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object) return;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var internalName = property.Name.Trim();
            var displayName = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(internalName) || string.IsNullOrWhiteSpace(displayName)) continue;
            _displayByName.TryAdd(internalName, displayName);
            _displayByName.TryAdd(displayName, displayName);
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, "palworld_character_names.json");
        yield return Path.Combine(baseDirectory, "parser", "palworld_character_names.json");
        yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "tools", "save_parser", "palworld_character_names.json"));
        yield return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "tools", "save_parser", "palworld_character_names.json"));
    }
}
