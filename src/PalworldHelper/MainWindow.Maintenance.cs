using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PalworldHelper;

public partial class MainWindow
{
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BreedingJsonPath.TextChanged += (_, _) => UpdateBreedingDataBadge();

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
}
