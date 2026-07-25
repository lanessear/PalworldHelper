using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PalworldHelper;

public sealed class ParsedSave
{
    public string Parser { get; set; } = "";
    public int SaveType { get; set; }
    public int PlayerCount { get; set; }
    public int PalCount { get; set; }
    public List<ParsedPlayer> Players { get; set; } = [];
    public List<ParsedPal> Pals { get; set; } = [];
}

public sealed class ParsedPlayer
{
    public string Name { get; set; } = "";
    public string PlayerUid { get; set; } = "";
    public int Level { get; set; }
}

public sealed class ParsedPal
{
    public string Owner { get; set; } = "";
    public string OwnerPlayerUid { get; set; } = "";
    public string Species { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public string Gender { get; set; } = "";
    public List<string> PassiveSkills { get; set; } = [];
    public string InstanceId { get; set; } = "";
    public string PassiveSkillsText => string.Join(", ", PassiveSkills);
}

public static class SaveParserService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<ParsedSave> ParseAsync(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save file path is required.", nameof(savePath));
        if (!File.Exists(savePath)) throw new FileNotFoundException("The selected save file does not exist.", savePath);

        var parserPath = Path.Combine(AppContext.BaseDirectory, "parser", "PalworldSaveParser.exe");
        if (!File.Exists(parserPath))
        {
            throw new FileNotFoundException(
                "The Palworld save parser is missing. Re-extract the complete release package so the parser folder stays next to PalworldHelper.exe.",
                parserPath);
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"PalworldHelper-{Guid.NewGuid():N}.json");
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = parserPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
            process.StartInfo.ArgumentList.Add(savePath);
            process.StartInfo.ArgumentList.Add(outputPath);
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException(string.IsNullOrWhiteSpace(stderr) ? $"Parser exited with code {process.ExitCode}." : stderr.Trim());
            }

            await using var input = File.OpenRead(outputPath);
            return await JsonSerializer.DeserializeAsync<ParsedSave>(input, SerializerOptions).ConfigureAwait(false)
                ?? throw new InvalidDataException("The parser returned no save data.");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }
}
