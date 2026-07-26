using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace PalworldHelper;

public partial class MainWindow
{
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BreedingJsonPath.TextChanged += (_, _) => UpdateBreedingDataBadge();
        RefreshObsoleteSummary();

        if (!string.IsNullOrWhiteSpace(_settings.BreedingJsonPath) && !File.Exists(_settings.BreedingJsonPath))
        {
            _settings.BreedingJsonPath = null;
            _settingsService.Save(_settings);
            try
            {
                LoadBreedingJson(BundledBreedingDataService.EnsureExtracted());
                BreedingStatus.Text = "✓ The selected custom JSON was missing. Default was loaded automatically.";
            }
            catch (Exception ex)
            {
                BreedingStatus.Text = "✗ Custom JSON is missing and Default could not be loaded: " + ex.Message;
            }
        }

        UpdateBreedingDataBadge();
    }

    private void UpdateBreedingDataBadge()
    {
        var isDefault = BundledBreedingDataService.IsDefault(BreedingJsonPath.Text);
        BreedingDataKindText.Text = isDefault ? "Default" : "Custom";
        BreedingDataKindBadge.Background = new SolidColorBrush(
            isDefault ? Color.FromRgb(37, 99, 235) : Color.FromRgb(5, 150, 105));
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MaintenanceStatus.Text = "Checking GitHub for the latest release …";
            var update = await ApplicationMaintenanceService.CheckForUpdateAsync();
            MaintenanceStatus.Text = update.Message;
            if (!update.UpdateAvailable) return;

            var answer = MessageBox.Show(
                this,
                $"PalworldHelper {update.LatestVersion} is available.\n\n" +
                "Download and install it now? The program will close and restart automatically.",
                "PalworldHelper update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            MaintenanceStatus.Text = "Downloading and preparing the update …";
            await ApplicationMaintenanceService.InstallUpdateAsync(update);
        }
        catch (Exception ex)
        {
            MaintenanceStatus.Text = "✗ Update failed: " + ex.Message;
        }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "This removes the PalworldHelper program files and all files created by PalworldHelper under %LOCALAPPDATA%\\PalworldHelper, including settings, downloaded saves, runtime files, and archived obsolete files.\n\n" +
            "Custom JSON files stored elsewhere are not deleted. Continue?",
            "Uninstall PalworldHelper",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            ApplicationMaintenanceService.StartUninstall();
        }
        catch (Exception ex)
        {
            MaintenanceStatus.Text = "✗ Uninstall could not be started: " + ex.Message;
        }
    }

    private void OpenObsoleteFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplicationMaintenanceService.OpenObsoleteDirectory();
            RefreshObsoleteSummary();
            MaintenanceStatus.Text = "✓ Obsolete folder opened.";
        }
        catch (Exception ex)
        {
            MaintenanceStatus.Text = "✗ Obsolete folder could not be opened: " + ex.Message;
        }
    }

    private void RefreshObsolete_Click(object sender, RoutedEventArgs e)
    {
        RefreshObsoleteSummary();
        MaintenanceStatus.Text = "✓ Obsolete folder status refreshed.";
    }

    private void DeleteObsolete_Click(object sender, RoutedEventArgs e)
    {
        var summary = ApplicationMaintenanceService.GetObsoleteSummary();
        if (!summary.Exists || summary.FileCount == 0 && summary.DirectoryCount == 0)
        {
            RefreshObsoleteSummary();
            MaintenanceStatus.Text = "No obsolete files to delete.";
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Delete the obsolete update archive?\n\n" +
            "This only removes old files archived during updates. Current app files, saves, settings, and custom JSON files are not deleted.",
            "Delete obsolete files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            ApplicationMaintenanceService.DeleteObsoleteDirectory();
            RefreshObsoleteSummary();
            MaintenanceStatus.Text = "✓ Obsolete folder deleted.";
        }
        catch (Exception ex)
        {
            MaintenanceStatus.Text = "✗ Obsolete folder could not be deleted: " + ex.Message;
        }
    }

    private void RefreshObsoleteSummary()
    {
        var summary = ApplicationMaintenanceService.GetObsoleteSummary();
        ObsoleteStatus.Text = summary.Exists
            ? string.Create(
                CultureInfo.CurrentCulture,
                $"Folder: {ApplicationMaintenanceService.ObsoleteDirectory}\nContains {summary.FileCount:N0} files in {summary.DirectoryCount:N0} folders ({FormatBytes(summary.Bytes)}).")
            : string.Create(
                CultureInfo.CurrentCulture,
                $"Folder: {ApplicationMaintenanceService.ObsoleteDirectory}\nNo obsolete folder exists yet.");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{size:N1} {units[unit]}");
    }

    private void CreateDebugSummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PalworldHelper",
                "debug");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, "PalworldHelper-debug-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
            File.WriteAllText(path, BuildDebugSummary(), Encoding.UTF8);
            Clipboard.SetText(File.ReadAllText(path, Encoding.UTF8));
            MaintenanceStatus.Text = $"✓ Debug summary created and copied to clipboard:\n{path}";
        }
        catch (Exception ex)
        {
            MaintenanceStatus.Text = "✗ Debug summary could not be created: " + ex.Message;
        }
    }

    private string BuildDebugSummary()
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var parserPath = Path.Combine(AppContext.BaseDirectory, "parser", "PalworldSaveParser.exe");
        var output = new StringBuilder()
            .AppendLine("PalworldHelper debug summary")
            .AppendLine("============================")
            .AppendLine(CultureInfo.InvariantCulture, $"Created: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
            .AppendLine(CultureInfo.InvariantCulture, $"App version: {appVersion}")
            .AppendLine(CultureInfo.InvariantCulture, $"Base directory: {AppContext.BaseDirectory}")
            .AppendLine(CultureInfo.InvariantCulture, $"OS: {Environment.OSVersion}")
            .AppendLine(CultureInfo.InvariantCulture, $".NET: {Environment.Version}")
            .AppendLine()
            .AppendLine("Current save")
            .AppendLine("------------")
            .AppendLine(CultureInfo.InvariantCulture, $"Local save path: {ValueOrNone(LocalSavePath.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Local save exists: {File.Exists(LocalSavePath.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Players folder: {ValueOrNone(FindPlayersDirectoryForSummary(LocalSavePath.Text))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Players folder exists: {Directory.Exists(FindPlayersDirectoryForSummary(LocalSavePath.Text))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Save status: {ValueOrNone(SaveStatus.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Parsed players: {_parsedSave?.Players.Count ?? 0:N0}")
            .AppendLine(CultureInfo.InvariantCulture, $"Parsed pals: {_parsedSave?.Pals.Count ?? 0:N0}")
            .AppendLine(CultureInfo.InvariantCulture, $"Storage values: {StorageSummary()}")
            .AppendLine()
            .AppendLine("Parser")
            .AppendLine("------")
            .AppendLine(CultureInfo.InvariantCulture, $"Parser reported: {ValueOrNone(_parsedSave?.Parser)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Parser path: {parserPath}")
            .AppendLine(CultureInfo.InvariantCulture, $"Parser exists: {File.Exists(parserPath)}")
            .AppendLine()
            .AppendLine("Breeding")
            .AppendLine("--------")
            .AppendLine(CultureInfo.InvariantCulture, $"Breeding JSON: {ValueOrNone(BreedingJsonPath.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Breeding JSON exists: {File.Exists(BreedingJsonPath.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Breeding results: {_breeding.ResultCount:N0}")
            .AppendLine(CultureInfo.InvariantCulture, $"Pal names: {_breeding.Names.Count:N0}")
            .AppendLine(CultureInfo.InvariantCulture, $"Selected source Pal: {ValueOrNone(StartPal.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Selected child Pal: {ValueOrNone(TargetPal.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Desired passives: {(_selectedPassiveSkills.Count == 0 ? "none" : string.Join(", ", _selectedPassiveSkills))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Saved breeding goals: {_settings.BreedingWishlist.Count:N0}")
            .AppendLine(CultureInfo.InvariantCulture, $"Breeding status: {ValueOrNone(BreedingStatus.Text)}")
            .AppendLine()
            .AppendLine("Owner filter")
            .AppendLine("------------")
            .AppendLine(CultureInfo.InvariantCulture, $"Configured player name: {ValueOrNone(PlayerName.Text)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Selected owner name: {ValueOrNone(_selectedOwnerName)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Show all owners: {_showAllOwners}")
            .AppendLine(CultureInfo.InvariantCulture, $"Current owner name: {ValueOrNone(CurrentOwnerName())}")
            .AppendLine()
            .AppendLine("Server profiles")
            .AppendLine("---------------");

        foreach (var profile in _settings.Profiles)
        {
            output
                .AppendLine(CultureInfo.InvariantCulture, $"- {ValueOrNone(profile.Name)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Host: {RedactHost(profile.Host)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Port: {profile.Port}")
                .AppendLine(CultureInfo.InvariantCulture, $"  User: {ValueOrNone(profile.Username)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Auth: {ValueOrNone(profile.Authentication)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Remote save path: {ValueOrNone(profile.RemoteSavePath)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Player name: {ValueOrNone(profile.PlayerName)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Has password: {!string.IsNullOrWhiteSpace(profile.EncryptedPassword)}")
                .AppendLine(CultureInfo.InvariantCulture, $"  Has SSH key path: {!string.IsNullOrWhiteSpace(profile.PrivateKeyPath)}");
        }

        return output
            .AppendLine()
            .AppendLine("Package files")
            .AppendLine("-------------")
            .AppendLine(CultureInfo.InvariantCulture, $"Default breeding JSON exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "palworld_breeding_results_v1.0_2026-07-24.json"))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Passive skills JSON exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "palworld_passive_skills.json"))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Character names JSON exists: {File.Exists(Path.Combine(AppContext.BaseDirectory, "palworld_character_names.json"))}")
            .AppendLine(CultureInfo.InvariantCulture, $"Third-party notices exist: {File.Exists(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md"))}")
            .ToString();
    }

    private string StorageSummary()
    {
        if (_parsedSave is null || _parsedSave.Pals.Count == 0) return "none";
        return string.Join(", ", _parsedSave.Pals
            .GroupBy(pal => string.IsNullOrWhiteSpace(pal.Storage) ? "World / base" : pal.Storage, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key}: {group.Count():N0}")));
    }

    private static string? FindPlayersDirectoryForSummary(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath)) return null;
        var levelDirectory = Path.GetDirectoryName(savePath);
        if (string.IsNullOrWhiteSpace(levelDirectory)) return null;
        var direct = Path.Combine(levelDirectory, "Players");
        if (Directory.Exists(direct)) return direct;
        var parent = Directory.GetParent(levelDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(parent)) return direct;
        return Path.Combine(parent, "Players");
    }

    private static string ValueOrNone(string? value)
        => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();

    private static string RedactHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(none)";
        var host = value.Trim();
        var dot = host.IndexOf('.');
        return dot <= 0 ? host : host[..dot] + ".…";
    }
}
