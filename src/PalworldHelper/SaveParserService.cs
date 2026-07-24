using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
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
    public string Nickname { get; set; } = "";
    public int Level { get; set; }
    public string Gender { get; set; } = "";
    public List<string> PassiveSkills { get; set; } = [];
    public string InstanceId { get; set; } = "";
    public string PassiveSkillsText => string.Join(", ", PassiveSkills);
}

public static class SaveParserService
{
    private const string ParserResourceName = "PalworldHelper.Embedded.PalworldSaveParser.exe";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim ExtractionLock = new(1, 1);

    public static async Task<ParsedSave> ParseAsync(string savePath)
    {
        var parserPath = await EnsureParserExtractedAsync().ConfigureAwait(false);
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

    private static async Task<string> EnsureParserExtractedAsync()
    {
        await ExtractionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            await using var resource = assembly.GetManifestResourceStream(ParserResourceName)
                ?? throw new FileNotFoundException("The embedded Palworld save parser is missing. Please download a current PalworldHelper.exe release.");

            using var memory = new MemoryStream();
            await resource.CopyToAsync(memory).ConfigureAwait(false);
            var parserBytes = memory.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(parserBytes)).ToLowerInvariant()[..16];
            var runtimeDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PalworldHelper",
                "runtime",
                hash);
            Directory.CreateDirectory(runtimeDirectory);

            var parserPath = Path.Combine(runtimeDirectory, "PalworldSaveParser.exe");
            if (!File.Exists(parserPath) || new FileInfo(parserPath).Length != parserBytes.LongLength)
            {
                var temporaryPath = parserPath + ".tmp";
                await File.WriteAllBytesAsync(temporaryPath, parserBytes).ConfigureAwait(false);
                File.Move(temporaryPath, parserPath, true);
            }

            return parserPath;
        }
        finally
        {
            ExtractionLock.Release();
        }
    }
}