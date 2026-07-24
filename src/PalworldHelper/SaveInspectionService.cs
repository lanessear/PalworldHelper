using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PalworldHelper;

public sealed record SaveInspectionResult(
    string Metadata,
    string HexPreview,
    string ReadableStrings);

public static class SaveInspectionService
{
    private const int PreviewBytes = 512;
    private const int MaxReadableStrings = 500;
    private const int MinimumStringLength = 5;

    public static async Task<SaveInspectionResult> InspectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A save file path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected save file does not exist.", path);
        }

        var info = new FileInfo(path);
        var previewLength = (int)Math.Min(info.Length, PreviewBytes);
        var preview = new byte[previewLength];

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var totalRead = 0;
        while (totalRead < preview.Length)
        {
            var read = await stream.ReadAsync(preview.AsMemory(totalRead, preview.Length - totalRead)).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        var sha256 = Convert.ToHexString(hash).ToLowerInvariant();

        stream.Position = 0;
        var strings = await ExtractReadableStringsAsync(stream).ConfigureAwait(false);

        var metadata = new StringBuilder()
            .AppendLine($"Name: {info.Name}")
            .AppendLine($"Full path: {info.FullName}")
            .AppendLine($"Size: {info.Length.ToString("N0", CultureInfo.CurrentCulture)} bytes ({FormatBytes(info.Length)})")
            .AppendLine($"Last modified: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Created: {info.CreationTime:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"SHA-256: {sha256}")
            .AppendLine($"Readable strings shown: {strings.Count.ToString("N0", CultureInfo.CurrentCulture)}")
            .ToString();

        return new SaveInspectionResult(
            metadata,
            BuildHexPreview(preview.AsSpan(0, totalRead)),
            strings.Count == 0
                ? "No readable ASCII or UTF-16 strings were found in the inspected data."
                : string.Join(Environment.NewLine, strings));
    }

    private static async Task<List<string>> ExtractReadableStringsAsync(Stream stream)
    {
        var result = new List<string>();
        var buffer = new byte[1024 * 1024];
        var ascii = new StringBuilder();
        var utf16 = new StringBuilder();
        byte? pendingUtf16Byte = null;

        while (result.Count < MaxReadableStrings)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            for (var i = 0; i < read && result.Count < MaxReadableStrings; i++)
            {
                var value = buffer[i];
                if (IsPrintable(value))
                {
                    ascii.Append((char)value);
                }
                else
                {
                    FlushCandidate(ascii, result);
                }

                if (pendingUtf16Byte is null)
                {
                    pendingUtf16Byte = value;
                }
                else
                {
                    if (value == 0 && IsPrintable(pendingUtf16Byte.Value))
                    {
                        utf16.Append((char)pendingUtf16Byte.Value);
                    }
                    else
                    {
                        FlushCandidate(utf16, result);
                    }

                    pendingUtf16Byte = null;
                }
            }
        }

        FlushCandidate(ascii, result);
        FlushCandidate(utf16, result);

        return result
            .Distinct(StringComparer.Ordinal)
            .Take(MaxReadableStrings)
            .ToList();
    }

    private static bool IsPrintable(byte value) => value is >= 32 and <= 126;

    private static void FlushCandidate(StringBuilder candidate, List<string> result)
    {
        if (candidate.Length >= MinimumStringLength && result.Count < MaxReadableStrings)
        {
            result.Add(candidate.ToString());
        }

        candidate.Clear();
    }

    private static string BuildHexPreview(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return "The file is empty.";
        }

        var output = new StringBuilder();
        for (var offset = 0; offset < bytes.Length; offset += 16)
        {
            var rowLength = Math.Min(16, bytes.Length - offset);
            output.Append(offset.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");

            for (var i = 0; i < 16; i++)
            {
                if (i < rowLength)
                {
                    output.Append(bytes[offset + i].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                }
                else
                {
                    output.Append("   ");
                }

                if (i == 7)
                {
                    output.Append(' ');
                }
            }

            output.Append(" | ");
            for (var i = 0; i < rowLength; i++)
            {
                var value = bytes[offset + i];
                output.Append(IsPrintable(value) ? (char)value : '.');
            }

            output.AppendLine();
        }

        return output.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }
}
