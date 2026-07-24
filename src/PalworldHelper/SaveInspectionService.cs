using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PalworldHelper;

public sealed record SaveInspectionResult(string Metadata, string HexPreview, string ReadableStrings);

public static class SaveInspectionService
{
    private const int PreviewBytes = 512;

    public static async Task<SaveInspectionResult> InspectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A save file path is required.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("The selected save file does not exist.", path);

        var info = new FileInfo(path);
        var preview = new byte[(int)Math.Min(info.Length, PreviewBytes)];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var totalRead = await stream.ReadAsync(preview).ConfigureAwait(false);
        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);

        var parsed = await SaveParserService.ParseAsync(path).ConfigureAwait(false);
        var metadata = new StringBuilder()
            .AppendLine($"Name: {info.Name}")
            .AppendLine($"Full path: {info.FullName}")
            .AppendLine($"Size: {info.Length.ToString("N0", CultureInfo.CurrentCulture)} bytes ({FormatBytes(info.Length)})")
            .AppendLine($"Last modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"SHA-256: {Convert.ToHexString(hash).ToLowerInvariant()}")
            .AppendLine($"Parser: {parsed.Parser}")
            .AppendLine($"Players: {parsed.PlayerCount.ToString("N0", CultureInfo.CurrentCulture)}")
            .AppendLine($"Pals: {parsed.PalCount.ToString("N0", CultureInfo.CurrentCulture)}")
            .ToString();

        return new SaveInspectionResult(metadata, BuildHexPreview(preview.AsSpan(0, totalRead)), FormatParsedData(parsed));
    }

    private static string FormatParsedData(ParsedSave save)
    {
        var output = new StringBuilder().AppendLine("PLAYERS").AppendLine(new string('=', 80));
        foreach (var player in save.Players.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            output.AppendLine($"{player.Name} | Level {player.Level} | UID {player.PlayerUid}");
        }

        output.AppendLine().AppendLine("PALS").AppendLine(new string('=', 80));
        foreach (var pal in save.Pals.OrderBy(p => p.Owner, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.Species, StringComparer.CurrentCultureIgnoreCase))
        {
            var nickname = string.IsNullOrWhiteSpace(pal.Nickname) ? "" : $" ({pal.Nickname})";
            var passives = pal.PassiveSkills.Count == 0 ? "no passive skills" : string.Join(", ", pal.PassiveSkills);
            output.AppendLine($"{pal.Owner}: {pal.Species}{nickname} | Level {pal.Level} | {pal.Gender} | {passives}");
        }
        return output.ToString();
    }

    private static string BuildHexPreview(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return "The file is empty.";
        var output = new StringBuilder();
        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            var rowLength = Math.Min(16, bytes.Length - offset);
            output.Append(offset.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");
            for (var i = 0; i < 16; i++) output.Append(i < rowLength ? bytes[offset + i].ToString("X2", CultureInfo.InvariantCulture) + " " : "   ");
            output.Append(" | ");
            for (var i = 0; i < rowLength; i++) output.Append(bytes[offset + i] is >= 32 and <= 126 ? (char)bytes[offset + i] : '.');
            output.AppendLine();
        }
        return output.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }
}
