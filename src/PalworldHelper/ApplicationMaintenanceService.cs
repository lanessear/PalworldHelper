using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PalworldHelper;

public sealed record UpdateCheckResult(bool UpdateAvailable, string CurrentVersion, string LatestVersion, string? AssetUrl, string Message);

public static class ApplicationMaintenanceService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/lanessear/PalworldHelper/releases/latest";
    private const string ReleaseAssetName = "PalworldHelper-win-x64.zip";
    private const string ManifestFileName = "palworldhelper.manifest.json";

    public static string ObsoleteDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PalworldHelper",
        "obsolete");

    public static (bool Exists, int DirectoryCount, int FileCount, long Bytes) GetObsoleteSummary()
    {
        if (!Directory.Exists(ObsoleteDirectory)) return (false, 0, 0, 0);
        var files = Directory.EnumerateFiles(ObsoleteDirectory, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .ToList();
        var directories = Directory.EnumerateDirectories(ObsoleteDirectory, "*", SearchOption.AllDirectories).Count();
        return (true, directories, files.Count, files.Sum(file => file.Length));
    }

    public static void DeleteObsoleteDirectory()
    {
        if (Directory.Exists(ObsoleteDirectory))
        {
            Directory.Delete(ObsoleteDirectory, recursive: true);
        }
    }

    public static void OpenObsoleteDirectory()
    {
        Directory.CreateDirectory(ObsoleteDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = ObsoleteDirectory,
            UseShellExecute = true
        });
    }

    public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var current = GetCurrentVersion();
        using var client = CreateClient();
        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() ?? "" : "";
        var latest = tag.Trim().TrimStart('v', 'V');
        string? assetUrl = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var nameNode)
                    && string.Equals(nameNode.GetString(), ReleaseAssetName, StringComparison.OrdinalIgnoreCase)
                    && asset.TryGetProperty("browser_download_url", out var urlNode))
                {
                    assetUrl = urlNode.GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(latest))
            return new(false, current, current, null, "The latest GitHub release has no version tag.");
        if (assetUrl is null)
            return new(false, current, latest, null, $"Release {latest} has no {ReleaseAssetName} asset.");

        var available = CompareVersions(latest, current) > 0;
        return new(available, current, latest, assetUrl,
            available ? $"Version {latest} is available." : $"Version {current} is current.");
    }

    public static async Task InstallUpdateAsync(UpdateCheckResult update, CancellationToken cancellationToken = default)
    {
        if (!update.UpdateAvailable || string.IsNullOrWhiteSpace(update.AssetUrl))
            throw new InvalidOperationException("No downloadable update is available.");

        var root = Path.Combine(Path.GetTempPath(), $"PalworldHelper-update-{Guid.NewGuid():N}");
        var zipPath = Path.Combine(root, ReleaseAssetName);
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(staging);

        using (var client = CreateClient())
        using (var response = await client.GetAsync(update.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = File.Create(zipPath);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
        ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var executable = Environment.ProcessPath ?? Path.Combine(installDirectory, "PalworldHelper.exe");
        var archiveDirectory = Path.Combine(
            ObsoleteDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var scriptPath = Path.Combine(root, "apply-update.ps1");
        var script = BuildUpdateScript(Environment.ProcessId, installDirectory, staging, archiveDirectory, executable, root);
        await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        StartPowerShell(scriptPath);
        Environment.Exit(0);
    }

    public static void StartUninstall()
    {
        var installDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PalworldHelper");
        var root = Path.Combine(Path.GetTempPath(), $"PalworldHelper-uninstall-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var scriptPath = Path.Combine(root, "uninstall.ps1");
        var script = BuildUninstallScript(Environment.ProcessId, installDirectory, localData, root);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
        StartPowerShell(scriptPath);
        Environment.Exit(0);
    }

    private static string BuildUpdateScript(int pid, string install, string staging, string archive, string executable, string tempRoot) => $$"""
$ErrorActionPreference = 'Stop'
Wait-Process -Id {{pid}} -ErrorAction SilentlyContinue
$install = '{{Escape(install)}}'
$staging = '{{Escape(staging)}}'
$archive = '{{Escape(archive)}}'
$oldManifestPath = Join-Path $install '{{ManifestFileName}}'
$newManifestPath = Join-Path $staging '{{ManifestFileName}}'
$oldFiles = @()
$newFiles = @()
if (Test-Path $oldManifestPath) { $oldFiles = @(Get-Content $oldManifestPath -Raw | ConvertFrom-Json) }
if (Test-Path $newManifestPath) { $newFiles = @(Get-Content $newManifestPath -Raw | ConvertFrom-Json) }
foreach ($relative in $oldFiles) {
  if ($newFiles -notcontains $relative) {
    $source = Join-Path $install $relative
    if (Test-Path $source) {
      $target = Join-Path $archive $relative
      New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
      Move-Item -Force $source $target
    }
  }
}
Get-ChildItem $staging -Recurse -File | ForEach-Object {
  $relative = $_.FullName.Substring($staging.Length).TrimStart('\')
  $target = Join-Path $install $relative
  New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
  Copy-Item -Force $_.FullName $target
}
Start-Process '{{Escape(executable)}}'
Start-Sleep -Seconds 2
Remove-Item -Recurse -Force '{{Escape(tempRoot)}}' -ErrorAction SilentlyContinue
""";

    private static string BuildUninstallScript(int pid, string install, string localData, string tempRoot) => $$"""
$ErrorActionPreference = 'SilentlyContinue'
Wait-Process -Id {{pid}} -ErrorAction SilentlyContinue
$install = '{{Escape(install)}}'
$manifest = Join-Path $install '{{ManifestFileName}}'
if (Test-Path $manifest) {
  $files = @(Get-Content $manifest -Raw | ConvertFrom-Json)
  foreach ($relative in $files) { Remove-Item -Force (Join-Path $install $relative) -ErrorAction SilentlyContinue }
} else {
  Remove-Item -Force (Join-Path $install 'PalworldHelper.exe') -ErrorAction SilentlyContinue
  Remove-Item -Force (Join-Path $install 'palworld_breeding_results_v1.0_2026-07-24.json') -ErrorAction SilentlyContinue
}
Remove-Item -Recurse -Force '{{Escape(localData)}}' -ErrorAction SilentlyContinue
Get-ChildItem $install -Directory -Recurse | Sort-Object FullName -Descending | Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item $install -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Remove-Item -Recurse -Force '{{Escape(tempRoot)}}' -ErrorAction SilentlyContinue
""";

    private static void StartPowerShell(string scriptPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\""
        });
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PalworldHelper", GetCurrentVersion()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static string GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static int CompareVersions(string left, string right)
    {
        static Version Parse(string value)
        {
            var core = value.Split('-', '+')[0];
            return Version.TryParse(core, out var version) ? version : new Version(0, 0, 0);
        }
        return Parse(left).CompareTo(Parse(right));
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
