using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PalworldHelper;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly BreedingService _breeding = new();
    private AppSettings _settings;
    private ServerProfile? _current;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        RefreshProfiles();
        if (!string.IsNullOrWhiteSpace(_settings.BreedingJsonPath) && File.Exists(_settings.BreedingJsonPath)) LoadBreedingJson(_settings.BreedingJsonPath);
        else
        {
            var besideExe = Path.Combine(AppContext.BaseDirectory, "palworld_breeding_results.json");
            if (File.Exists(besideExe)) LoadBreedingJson(besideExe);
        }
    }

    private void RefreshProfiles()
    {
        ProfilesList.ItemsSource = null; ProfilesList.ItemsSource = _settings.Profiles;
        var selected = _settings.Profiles.FirstOrDefault(p => p.Id == _settings.SelectedProfileId) ?? _settings.Profiles.FirstOrDefault();
        if (selected is not null) ProfilesList.SelectedItem = selected;
    }

    private void LoadProfile(ServerProfile p)
    {
        _current = p; ProfileName.Text = p.Name; Host.Text = p.Host; Port.Text = p.Port.ToString(CultureInfo.InvariantCulture); Username.Text = p.Username;
        RemoteSavePath.Text = p.RemoteSavePath; PlayerName.Text = p.PlayerName; PrivateKeyPath.Text = p.PrivateKeyPath;
        Authentication.SelectedIndex = p.Authentication is "SSH key" or "SSH-Key" ? 1 : 0; Password.Password = SettingsService.Unprotect(p.EncryptedPassword);
    }

    private void ApplyForm(ServerProfile p)
    {
        p.Name = string.IsNullOrWhiteSpace(ProfileName.Text) ? "My Server" : ProfileName.Text.Trim(); p.Host = Host.Text.Trim();
        p.Port = int.TryParse(Port.Text, out var port) ? port : 22; p.Username = Username.Text.Trim(); p.RemoteSavePath = RemoteSavePath.Text.Trim();
        p.PlayerName = PlayerName.Text.Trim(); p.PrivateKeyPath = PrivateKeyPath.Text.Trim(); p.Authentication = Authentication.SelectedIndex == 1 ? "SSH key" : "Password";
        p.EncryptedPassword = SettingsService.Protect(Password.Password);
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ProfilesList.SelectedItem is ServerProfile p) LoadProfile(p); }
    private void NewProfile_Click(object sender, RoutedEventArgs e) { var p = new ServerProfile(); _settings.Profiles.Add(p); _current = p; _settings.SelectedProfileId = p.Id; RefreshProfiles(); ProfilesList.SelectedItem = p; }
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) { if (_current is null) return; _settings.Profiles.Remove(_current); _current = null; _settings.SelectedProfileId = null; _settingsService.Save(_settings); RefreshProfiles(); }
    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        _current ??= new ServerProfile(); if (!_settings.Profiles.Contains(_current)) _settings.Profiles.Add(_current); ApplyForm(_current);
        _settings.SelectedProfileId = _current.Id; _settingsService.Save(_settings); RefreshProfiles(); ProfilesList.SelectedItem = _current; ServerStatus.Text = "Profile saved.";
    }
    private void Authentication_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (PasswordPanel is null) return; var key = Authentication.SelectedIndex == 1; KeyPanel.Visibility = key ? Visibility.Visible : Visibility.Collapsed; PasswordPanel.Visibility = key ? Visibility.Collapsed : Visibility.Visible; }
    private void PickKey_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Title = "Select private SSH key", Filter = "All files|*.*" }; if (d.ShowDialog() == true) PrivateKeyPath.Text = d.FileName; }

    private ServerProfile RequireProfile()
    {
        _current ??= new ServerProfile(); ApplyForm(_current);
        if (string.IsNullOrWhiteSpace(_current.Host) || string.IsNullOrWhiteSpace(_current.Username)) throw new InvalidOperationException("Host and username are required.");
        return _current;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try { ServerStatus.Text = "Testing connection …"; await SftpService.TestAsync(RequireProfile(), Password.Password); ServerStatus.Text = "✓ SFTP connection successful."; }
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }
    private async void FindRemoteSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerStatus.Text = "Searching the server for Level.sav …";
            var profile = RequireProfile();
            var candidates = await SftpService.FindSaveFilesAsync(profile, Password.Password);

            if (candidates.Count == 0)
            {
                ServerStatus.Text = "✗ No Level.sav file was found. Enter the path manually.";
                return;
            }

            var selected = candidates[0];
            RemoteSavePath.Text = selected.Path;
            profile.RemoteSavePath = selected.Path;
            ServerStatus.Text = candidates.Count == 1
                ? $"✓ Save file found automatically:\n{selected.Path}"
                : $"✓ Found {candidates.Count} save files. The most recently modified file was selected:\n{selected.Path}";
        }
        catch (Exception ex)
        {
            ServerStatus.Text = "✗ " + ex.Message;
        }
    }

    private async void DownloadSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerStatus.Text = "Downloading Level.sav …";
            var path = await SftpService.DownloadSaveAsync(RequireProfile(), Password.Password);
            ServerStatus.Text = "✓ Save file downloaded:\n" + path;
            await InspectSaveAsync(path);
        }
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }

    private async void PickLocalSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a Palworld save file",
            Filter = "Palworld save files|*.sav|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            await InspectSaveAsync(dialog.FileName);
        }
    }

    private async Task InspectSaveAsync(string path)
    {
        try
        {
            LocalSavePath.Text = path;
            SaveStatus.Text = "Reading save file …";
            SaveMetadata.Text = string.Empty;
            SaveHexPreview.Text = string.Empty;
            SaveStrings.Text = string.Empty;

            var result = await SaveInspectionService.InspectAsync(path);
            SaveMetadata.Text = result.Metadata;
            SaveHexPreview.Text = result.HexPreview;
            SaveStrings.Text = result.ReadableStrings;
            SaveStatus.Text = "✓ Save file loaded. Raw file data is shown below.";
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "✗ " + ex.Message;
        }
    }

    private void PickBreedingJson_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "Select palworld_breeding_results.json", Filter = "JSON files|*.json|All files|*.*" };
        if (d.ShowDialog() == true) LoadBreedingJson(d.FileName);
    }
    private void LoadBreedingJson(string path)
    {
        try
        {
            _breeding.Load(path); BreedingJsonPath.Text = path; BreedingStatus.Text = $"✓ Loaded {_breeding.ResultCount:N0} results and {_breeding.Names.Count:N0} Pal names.";
            StartPal.ItemsSource = _breeding.Names; TargetPal.ItemsSource = _breeding.Names; _settings.BreedingJsonPath = path; _settingsService.Save(_settings);
        }
        catch (Exception ex) { BreedingStatus.Text = "✗ " + ex.Message; }
    }
    private void FindPath_Click(object sender, RoutedEventArgs e)
    {
        ResultList.Items.Clear(); var start = StartPal.Text?.Trim() ?? ""; var target = TargetPal.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(target)) { ResultList.Items.Add("Select both a source and target Pal."); return; }
        var path = _breeding.FindShortest(start, target);
        if (path is null) { ResultList.Items.Add("No breeding chain was found."); return; }
        if (path.Count == 0) { ResultList.Items.Add("Source and target are identical."); return; }
        for (var i = 0; i < path.Count; i++) ResultList.Items.Add($"{i + 1}.  {path[i].Parent} + {path[i].Mate}  →  {path[i].Child}");
    }
}
