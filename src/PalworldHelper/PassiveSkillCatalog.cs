using System.IO;
using System.Text.Json;

namespace PalworldHelper;

public static class PassiveSkillCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<PassiveSkillOption> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "palworld_passive_skills.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, "tools", "save_parser", "palworld_passive_skills.json");
        }

        if (!File.Exists(path)) return [];

        using var input = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<PassiveSkillOption>>(input, SerializerOptions)?
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Id))
            .OrderByDescending(skill => skill.Rank)
            .ThenBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList() ?? [];
    }
}
