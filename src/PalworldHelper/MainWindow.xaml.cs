using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace PalworldHelper;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly SftpService _sftp = new();
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
        _current = p; ProfileName.Text = p.Name; Host.Text = p.Host; Port.Text = p.Port.ToString(); Username.Text = p.Username;
        RemoteSavePath.Text = p.RemoteSavePath; PlayerName.Text = p.PlayerName; PrivateKeyPath.Text = p.PrivateKeyPath;
        Authentication.SelectedIndex = p.Authentication == "SSH-Key" ? 1 : 0; Password.Password = SettingsService.Unprotect(p.EncryptedPassword);
    }

    private void ApplyForm(ServerProfile p)
    {
        p.Name = string.IsNullOrWhiteSpace(ProfileName.Text) ? "Mein Server" : ProfileName.Text.Trim(); p.Host = Host.Text.Trim();
        p.Port = int.TryParse(Port.Text, out var port) ? port : 22; p.Username = Username.Text.Trim(); p.RemoteSavePath = RemoteSavePath.Text.Trim();
        p.PlayerName = PlayerName.Text.Trim(); p.PrivateKeyPath = PrivateKeyPath.Text.Trim(); p.Authentication = Authentication.SelectedIndex == 1 ? "SSH-Key" : "Passwort";
        p.EncryptedPassword = SettingsService.Protect(Password.Password);
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ProfilesList.SelectedItem is ServerProfile p) LoadProfile(p); }
    private void NewProfile_Click(object sender, RoutedEventArgs e) { var p = new ServerProfile(); _settings.Profiles.Add(p); _current = p; _settings.SelectedProfileId = p.Id; RefreshProfiles(); ProfilesList.SelectedItem = p; }
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) { if (_current is null) return; _settings.Profiles.Remove(_current); _current = null; _settings.SelectedProfileId = null; _settingsService.Save(_settings); RefreshProfiles(); }
    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        _current ??= new ServerProfile(); if (!_settings.Profiles.Contains(_current)) _settings.Profiles.Add(_current); ApplyForm(_current);
        _settings.SelectedProfileId = _current.Id; _settingsService.Save(_settings); RefreshProfiles(); ProfilesList.SelectedItem = _current; ServerStatus.Text = "Profil gespeichert.";
    }
    private void Authentication_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (PasswordPanel is null) return; var key = Authentication.SelectedIndex == 1; KeyPanel.Visibility = key ? Visibility.Visible : Visibility.Collapsed; PasswordPanel.Visibility = key ? Visibility.Collapsed : Visibility.Visible; }
    private void PickKey_Click(object sender, RoutedEventArgs e) { var d = new OpenFileDialog { Title = "Privaten SSH-Key auswählen", Filter = "Alle Dateien|*.*" }; if (d.ShowDialog() == true) PrivateKeyPath.Text = d.FileName; }

    private ServerProfile RequireProfile()
    {
        _current ??= new ServerProfile(); ApplyForm(_current);
        if (string.IsNullOrWhiteSpace(_current.Host) || string.IsNullOrWhiteSpace(_current.Username)) throw new InvalidOperationException("Host und Benutzername fehlen.");
        return _current;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try { ServerStatus.Text = "Teste Verbindung …"; await _sftp.TestAsync(RequireProfile(), Password.Password); ServerStatus.Text = "✓ SFTP-Verbindung erfolgreich."; }
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }
    private async void DownloadSave_Click(object sender, RoutedEventArgs e)
    {
        try { ServerStatus.Text = "Lade Level.sav herunter …"; var path = await _sftp.DownloadSaveAsync(RequireProfile(), Password.Password); ServerStatus.Text = "✓ Save heruntergeladen:\n" + path; }
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }

    private void PickBreedingJson_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "palworld_breeding_results.json auswählen", Filter = "JSON-Dateien|*.json|Alle Dateien|*.*" };
        if (d.ShowDialog() == true) LoadBreedingJson(d.FileName);
    }
    private void LoadBreedingJson(string path)
    {
        try
        {
            _breeding.Load(path); BreedingJsonPath.Text = path; BreedingStatus.Text = $"✓ {_breeding.ResultCount:N0} Ergebnisse, {_breeding.Names.Count:N0} Pal-Namen geladen.";
            StartPal.ItemsSource = _breeding.Names; TargetPal.ItemsSource = _breeding.Names; _settings.BreedingJsonPath = path; _settingsService.Save(_settings);
        }
        catch (Exception ex) { BreedingStatus.Text = "✗ " + ex.Message; }
    }
    private void FindPath_Click(object sender, RoutedEventArgs e)
    {
        ResultList.Items.Clear(); var start = StartPal.Text?.Trim() ?? ""; var target = TargetPal.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(target)) { ResultList.Items.Add("Bitte Start und Ziel auswählen."); return; }
        var path = _breeding.FindShortest(start, target);
        if (path is null) { ResultList.Items.Add("Keine Zuchtkette gefunden."); return; }
        if (path.Count == 0) { ResultList.Items.Add("Start und Ziel sind identisch."); return; }
        for (var i = 0; i < path.Count; i++) ResultList.Items.Add($"{i + 1}.  {path[i].Parent} + {path[i].Mate}  →  {path[i].Child}");
    }
}
