using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PalworldHelper;

public sealed record SaveCacheLoadResult(ParsedSave Save, bool CacheHit);

public static class SaveCacheService
{
    private const int CacheVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string CacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PalworldHelper", "cache");

    public static async Task<SaveCacheLoadResult> LoadOrParseAsync(string savePath, bool forceRefresh = false)
    {
        var fingerprint = BuildFingerprint(savePath);
        var cachePath = CachePath(savePath);

        if (!forceRefresh)
        {
            var cached = await TryLoadAsync(cachePath, fingerprint).ConfigureAwait(false);
            if (cached is not null) return new SaveCacheLoadResult(cached, CacheHit: true);
        }

        var parsed = await SaveParserService.ParseAsync(savePath).ConfigureAwait(false);
        await SaveAsync(cachePath, fingerprint, parsed).ConfigureAwait(false);
        return new SaveCacheLoadResult(parsed, CacheHit: false);
    }

    public static string CacheStatus(bool cacheHit)
        => cacheHit ? "loaded from cache" : "parsed and cached";

    private static async Task<ParsedSave?> TryLoadAsync(string cachePath, SaveCacheFingerprint fingerprint)
    {
        try
        {
            if (!File.Exists(cachePath)) return null;
            await using var file = File.OpenRead(cachePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var entry = await JsonSerializer.DeserializeAsync<SaveCacheEntry>(gzip, JsonOptions).ConfigureAwait(false);
            if (entry?.Version != CacheVersion || entry.Save is null) return null;
            return SameFingerprint(entry.Fingerprint, fingerprint) ? entry.Save : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveAsync(string cachePath, SaveCacheFingerprint fingerprint, ParsedSave parsed)
    {
        Directory.CreateDirectory(CacheDirectory);
        var temporaryPath = cachePath + ".tmp";
        await using (var file = File.Create(temporaryPath))
        await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
        {
            await JsonSerializer.SerializeAsync(gzip, new SaveCacheEntry(CacheVersion, fingerprint, parsed), JsonOptions).ConfigureAwait(false);
        }

        File.Move(temporaryPath, cachePath, overwrite: true);
    }

    private static SaveCacheFingerprint BuildFingerprint(string savePath)
    {
        var files = EnumerateSaveFiles(savePath)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new SaveCacheFile(info.FullName, info.Length, info.LastWriteTimeUtc.Ticks);
            })
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SaveCacheFingerprint(Path.GetFullPath(savePath), files);
    }

    private static IEnumerable<string> EnumerateSaveFiles(string savePath)
    {
        if (File.Exists(savePath)) yield return savePath;

        var levelDirectory = Path.GetDirectoryName(savePath);
        if (string.IsNullOrWhiteSpace(levelDirectory)) yield break;

        foreach (var path in SafeEnumerateFiles(levelDirectory, "*_dps.sav", SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }

        var playersDirectory = SaveParserService.FindPlayersDirectory(savePath);
        if (!string.IsNullOrWhiteSpace(playersDirectory))
        {
            foreach (var path in SafeEnumerateFiles(playersDirectory, "*.sav", SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory, string pattern, SearchOption option)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.EnumerateFiles(directory, pattern, option) : [];
        }
        catch
        {
            return [];
        }
    }

    private static bool SameFingerprint(SaveCacheFingerprint? left, SaveCacheFingerprint right)
    {
        if (left is null || !left.SavePath.Equals(right.SavePath, StringComparison.OrdinalIgnoreCase) || left.Files.Count != right.Files.Count)
        {
            return false;
        }

        return left.Files.Zip(right.Files).All(pair =>
            pair.First.Path.Equals(pair.Second.Path, StringComparison.OrdinalIgnoreCase)
            && pair.First.Size == pair.Second.Size
            && pair.First.LastWriteTimeUtcTicks == pair.Second.LastWriteTimeUtcTicks);
    }

    private static string CachePath(string savePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(savePath).ToUpperInvariant()));
        return Path.Combine(CacheDirectory, string.Create(CultureInfo.InvariantCulture, $"save-cache-v{CacheVersion}.{Convert.ToHexString(hash).ToLowerInvariant()}.json.gz"));
    }

    private sealed record SaveCacheEntry(int Version, SaveCacheFingerprint Fingerprint, ParsedSave Save);
    private sealed record SaveCacheFingerprint(string SavePath, List<SaveCacheFile> Files);
    private sealed record SaveCacheFile(string Path, long Size, long LastWriteTimeUtcTicks);
}
