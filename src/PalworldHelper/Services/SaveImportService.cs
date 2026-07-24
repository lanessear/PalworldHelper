using System.Diagnostics;
using System.Text.Json;
using PalworldHelper.Models;

namespace PalworldHelper.Services;

public sealed class SaveImportService(IWebHostEnvironment environment)
{
    public async Task<IReadOnlyList<OwnedPal>> ImportAsync(string levelSavPath, long profileId, string playerName, CancellationToken ct)
    {
        var converter = FindConverter();
        if (converter is null)
            throw new InvalidOperationException("No bundled Palworld save converter was found. See tools/palworld-save-tools/README.md.");

        var jsonPath = Path.Combine(Path.GetDirectoryName(levelSavPath)!, "Level.sav.json");
        var psi = new ProcessStartInfo
        {
            FileName = converter,
            WorkingDirectory = Path.GetDirectoryName(converter)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(levelSavPath);
        psi.ArgumentList.Add("--to-json");
        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add(jsonPath);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start save converter.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0) throw new InvalidOperationException("Save converter failed: " + await stderrTask);
        return await ImportConvertedJsonAsync(jsonPath, profileId, playerName, ct);
    }

    public async Task<IReadOnlyList<OwnedPal>> ImportConvertedJsonAsync(string jsonPath, long profileId, string playerName, CancellationToken ct)
    {
        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var playerUid = FindPlayerUid(document.RootElement, playerName);
        if (string.IsNullOrEmpty(playerUid)) throw new InvalidOperationException($"Player '{playerName}' was not found in converted save data.");
        var pals = new List<OwnedPal>();
        Walk(document.RootElement, element =>
        {
            if (element.ValueKind != JsonValueKind.Object) return;
            var owner = GetString(element, "OwnerPlayerUId", "OwnerPlayerUID", "owner_player_uid");
            if (!Normalize(owner).Equals(Normalize(playerUid), StringComparison.OrdinalIgnoreCase)) return;
            var species = GetString(element, "CharacterID", "character_id");
            if (string.IsNullOrWhiteSpace(species) || species.Contains("Player", StringComparison.OrdinalIgnoreCase)) return;
            var instance = GetString(element, "InstanceId", "InstanceID", "instance_id");
            if (string.IsNullOrEmpty(instance)) instance = Guid.NewGuid().ToString("N");
            pals.Add(new OwnedPal(0, profileId, instance, species, species,
                GetString(element, "NickName", "Nickname", "nick_name") ?? string.Empty,
                GetInt(element, "Level", "level"), GetString(element, "Gender", "gender") ?? string.Empty,
                GetInt(element, "Rank", "rank") ?? 0, GetInt(element, "Talent_HP", "TalentHP"),
                GetInt(element, "Talent_Shot", "TalentAttack"), GetInt(element, "Talent_Defense", "TalentDefense"),
                GetStringArray(element, "PassiveSkillList", "PassiveSkills")));
        });
        return pals.DistinctBy(x => x.InstanceId).ToArray();
    }

    private string? FindConverter()
    {
        var dir = Path.Combine(environment.ContentRootPath, "tools", "palworld-save-tools");
        return new[] { "palworld-save-tools.exe", "convert.exe" }.Select(x => Path.Combine(dir, x)).FirstOrDefault(File.Exists);
    }

    private static string? FindPlayerUid(JsonElement root, string playerName)
    {
        string? result = null;
        Walk(root, element =>
        {
            if (result is not null || element.ValueKind != JsonValueKind.Object) return;
            var nick = GetString(element, "NickName", "Nickname", "nick_name", "PlayerName", "player_name");
            if (!string.Equals(nick, playerName, StringComparison.OrdinalIgnoreCase)) return;
            result = GetString(element, "PlayerUId", "PlayerUID", "player_uid", "OwnerPlayerUId");
        });
        return result;
    }

    private static void Walk(JsonElement element, Action<JsonElement> visitor)
    {
        visitor(element);
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var p in element.EnumerateObject()) Walk(p.Value, visitor);
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) Walk(item, visitor);
    }

    private static JsonElement? GetProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in element.EnumerateObject())
            if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) return Unwrap(property.Value);
        return null;
    }

    private static JsonElement Unwrap(JsonElement value)
    {
        for (var i = 0; i < 8 && value.ValueKind == JsonValueKind.Object; i++)
        {
            if (value.TryGetProperty("value", out var inner) || value.TryGetProperty("Value", out inner)) value = inner;
            else break;
        }
        return value;
    }
    private static string? GetString(JsonElement e, params string[] names)
    {
        var p = GetProperty(e, names); if (p is null) return null;
        return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
    }
    private static int? GetInt(JsonElement e, params string[] names)
    {
        var p = GetProperty(e, names); if (p is null) return null;
        return p.Value.TryGetInt32(out var value) ? value : int.TryParse(p.Value.ToString(), out value) ? value : null;
    }
    private static IReadOnlyList<string> GetStringArray(JsonElement e, params string[] names)
    {
        var p = GetProperty(e, names); if (p is null) return [];
        if (p.Value.ValueKind == JsonValueKind.Array) return p.Value.EnumerateArray().Select(x => x.ToString()).Where(x => x.Length > 0).ToArray();
        return [];
    }
    private static string Normalize(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
