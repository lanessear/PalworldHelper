using System.IO;

namespace PalworldHelper;

public static class BundledBreedingDataService
{
    public const string DefaultFileName = "palworld_breeding_results.default.json";

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, DefaultFileName);

    public static string EnsureExtracted()
    {
        if (!File.Exists(DefaultPath))
        {
            throw new FileNotFoundException(
                $"The default breeding dataset '{DefaultFileName}' is missing next to PalworldHelper.exe. " +
                "Re-extract the complete release package or select a compatible custom JSON file.",
                DefaultPath);
        }

        return DefaultPath;
    }

    public static bool IsDefault(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(DefaultPath),
            StringComparison.OrdinalIgnoreCase);
    }
}
