using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PalworldHelper;

public static class BundledBreedingDataService
{
    private const string ResourcePrefix = "PalworldHelper.Embedded.BreedingData.part";
    private const string OutputFileName = "palworld_breeding_results_v1.0_2026-07-24.json";

    public static string EnsureExtracted()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
        {
            throw new InvalidOperationException("The bundled breeding dataset is missing from this build.");
        }

        var base64 = new StringBuilder();
        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Bundled resource '{resourceName}' could not be opened.");
            using var reader = new StreamReader(stream, Encoding.ASCII, false);
            base64.Append(reader.ReadToEnd().Trim());
        }

        var compressed = Convert.FromBase64String(base64.ToString());
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PalworldHelper",
            "data");
        Directory.CreateDirectory(dataDirectory);

        var outputPath = Path.Combine(dataDirectory, OutputFileName);
        var hashPath = outputPath + ".sha256";
        var embeddedHash = Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant();

        if (File.Exists(outputPath) && File.Exists(hashPath) &&
            string.Equals(File.ReadAllText(hashPath).Trim(), embeddedHash, StringComparison.OrdinalIgnoreCase))
        {
            return outputPath;
        }

        var temporaryPath = outputPath + ".tmp";
        using (var compressedStream = new MemoryStream(compressed, writable: false))
        using (var gzip = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var output = File.Create(temporaryPath))
        {
            gzip.CopyTo(output);
        }

        File.Move(temporaryPath, outputPath, overwrite: true);
        File.WriteAllText(hashPath, embeddedHash, Encoding.ASCII);
        return outputPath;
    }
}