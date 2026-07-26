using Microsoft.Win32;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace PalworldHelper;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly BreedingService _breeding = new();
    private readonly PalNameCatalog _palNames = PalNameCatalog.Load();
    private readonly List<PassiveSkillOption> _passiveSkillOptions = PassiveSkillCatalog.Load().ToList();
    private readonly List<string> _selectedPassiveSkills = [];
    private List<SavePlayerRow> _savePlayerRows = [];
    private List<SavePalRow> _savePalRows = [];
    private string? _selectedOwnerName;
    private bool _showAllOwners;
    private AppSettings _settings;
    private ServerProfile? _current;
    private ParsedSave? _parsedSave;

    private sealed record LocalSaveCandidate(string SavePath, bool HasPlayersFolder, DateTime LastWriteTimeUtc);

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        PassiveSkillPicker.ItemsSource = _passiveSkillOptions;
        RefreshBreedingWishlist();
        RenderPassiveSkillTags();
        RefreshProfiles();

        LoadInitialBreedingData();

        if (!string.IsNullOrWhiteSpace(_settings.LocalSavePath) && File.Exists(_settings.LocalSavePath))
        {
            LocalSavePath.Text = _settings.LocalSavePath;
            Loaded += async (_, _) => await InspectSaveAsync(_settings.LocalSavePath, persistPath: false);
        }
    }

    private void LoadInitialBreedingData()
    {
        if (!string.IsNullOrWhiteSpace(_settings.BreedingJsonPath) && File.Exists(_settings.BreedingJsonPath))
        {
            LoadBreedingJson(_settings.BreedingJsonPath);
            return;
        }

        try
        {
            LoadBreedingJson(BundledBreedingDataService.EnsureExtracted());
        }
        catch (Exception ex)
        {
            BreedingStatus.Text = "✗ Bundled breeding data could not be loaded: " + ex.Message;
        }
    }

    private void RefreshProfiles()
    {
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = _settings.Profiles;
        var selected = _settings.Profiles.FirstOrDefault(p => p.Id == _settings.SelectedProfileId) ?? _settings.Profiles.FirstOrDefault();
        if (selected is not null) ProfilesList.SelectedItem = selected;
    }

    private void LoadProfile(ServerProfile p)
    {
        _current = p;
        ProfileName.Text = p.Name;
        Host.Text = p.Host;
        Port.Text = p.Port.ToString(CultureInfo.InvariantCulture);
        Username.Text = p.Username;
        RemoteSavePath.Text = p.RemoteSavePath;
        PlayerName.Text = p.PlayerName;
        PrivateKeyPath.Text = p.PrivateKeyPath;
        Authentication.SelectedIndex = p.Authentication is "SSH key" or "SSH-Key" ? 1 : 0;
        Password.Password = SettingsService.Unprotect(p.EncryptedPassword);
    }

    private void ApplyForm(ServerProfile p)
    {
        p.Name = string.IsNullOrWhiteSpace(ProfileName.Text) ? "My Server" : ProfileName.Text.Trim();
        p.Host = Host.Text.Trim();
        p.Port = int.TryParse(Port.Text, out var port) ? port : 22;
        p.Username = Username.Text.Trim();
        p.RemoteSavePath = RemoteSavePath.Text.Trim();
        p.PlayerName = PlayerName.Text.Trim();
        p.PrivateKeyPath = PrivateKeyPath.Text.Trim();
        p.Authentication = Authentication.SelectedIndex == 1 ? "SSH key" : "Password";
        p.EncryptedPassword = SettingsService.Protect(Password.Password);
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfilesList.SelectedItem is ServerProfile p) LoadProfile(p);
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = new ServerProfile();
        _settings.Profiles.Add(p);
        _current = p;
        _settings.SelectedProfileId = p.Id;
        RefreshProfiles();
        ProfilesList.SelectedItem = p;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        _settings.Profiles.Remove(_current);
        _current = null;
        _settings.SelectedProfileId = null;
        _settingsService.Save(_settings);
        RefreshProfiles();
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        _current ??= new ServerProfile();
        if (!_settings.Profiles.Contains(_current)) _settings.Profiles.Add(_current);
        ApplyForm(_current);
        _settings.SelectedProfileId = _current.Id;
        _settingsService.Save(_settings);
        RefreshProfiles();
        ProfilesList.SelectedItem = _current;
        ServerStatus.Text = "Profile saved.";
    }

    private void Authentication_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PasswordPanel is null) return;
        var key = Authentication.SelectedIndex == 1;
        KeyPanel.Visibility = key ? Visibility.Visible : Visibility.Collapsed;
        PasswordPanel.Visibility = key ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PickKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Select private SSH key", Filter = "All files|*.*" };
        if (dialog.ShowDialog() == true) PrivateKeyPath.Text = dialog.FileName;
    }

    private ServerProfile RequireProfile()
    {
        _current ??= new ServerProfile();
        ApplyForm(_current);
        if (string.IsNullOrWhiteSpace(_current.Host) || string.IsNullOrWhiteSpace(_current.Username))
            throw new InvalidOperationException("Host and username are required.");
        return _current;
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerStatus.Text = "Testing connection …";
            await SftpService.TestAsync(RequireProfile(), Password.Password);
            ServerStatus.Text = "✓ SFTP connection successful.";
        }
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
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }

    private async void DownloadSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerStatus.Text = "Downloading Level.sav …";
            var path = await SftpService.DownloadSaveAsync(RequireProfile(), Password.Password);
            ServerStatus.Text = "✓ Save file downloaded and stored locally:\n" + path;
            await InspectSaveAsync(path, persistPath: true);
        }
        catch (Exception ex) { ServerStatus.Text = "✗ " + ex.Message; }
    }

    private async void PickLocalSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Level.sav",
            Filter = "Palworld world save|Level.sav|Palworld save files|*.sav|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            await InspectSaveAsync(dialog.FileName, persistPath: true);
    }

    private async void FindLocalSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveStatus.Text = "Searching local Palworld save folders …";
            var candidates = FindLocalSaveCandidates();
            if (candidates.Count == 0)
            {
                var root = LocalPalworldSaveRoot();
                SaveStatus.Text = Directory.Exists(root)
                    ? $"No Level.sav files were found under {root}."
                    : $"Local Palworld save folder was not found: {root}";
                return;
            }

            var selected = candidates
                .OrderByDescending(candidate => candidate.HasPlayersFolder)
                .ThenByDescending(candidate => candidate.LastWriteTimeUtc)
                .First();
            var note = candidates.Count == 1
                ? "Found one local save."
                : string.Create(
                    CultureInfo.CurrentCulture,
                    $"Found {candidates.Count:N0} local saves. Using the newest save with player data when available.");
            SaveStatus.Text = note;
            await InspectSaveAsync(selected.SavePath, persistPath: true);
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "✗ Local save search failed: " + ex.Message;
        }
    }

    private static List<LocalSaveCandidate> FindLocalSaveCandidates()
    {
        var root = LocalPalworldSaveRoot();
        if (!Directory.Exists(root)) return [];

        var candidates = new List<LocalSaveCandidate>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };
        foreach (var path in Directory.EnumerateFiles(root, "Level.sav", options))
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                var hasPlayers = !string.IsNullOrWhiteSpace(directory)
                    && Directory.Exists(Path.Combine(directory, "Players"));
                candidates.Add(new LocalSaveCandidate(path, hasPlayers, File.GetLastWriteTimeUtc(path)));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return candidates;
    }

    private static string LocalPalworldSaveRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pal",
            "Saved",
            "SaveGames");

    private async Task InspectSaveAsync(string path, bool persistPath)
    {
        try
        {
            LocalSavePath.Text = path;
            SaveStatus.Text = "Reading and parsing save file …";
            SaveMetadata.Text = string.Empty;
            SaveHexPreview.Text = string.Empty;
            SavePlayersList.ItemsSource = null;
            SavePalsList.ItemsSource = null;
            _savePlayerRows = [];
            _savePalRows = [];
            _selectedOwnerName = null;
            _showAllOwners = false;
            SaveOwnerFilterStatus.Text = "Click a player to filter owned Pals.";
            SavePlayerCount.Text = "—";
            SavePalCount.Text = "—";
            SaveSpeciesCount.Text = "—";
            SavePassiveCount.Text = "—";

            if (persistPath)
            {
                _settings.LocalSavePath = path;
                _settingsService.Save(_settings);
            }

            var result = await SaveInspectionService.InspectAsync(path);
            _parsedSave = result.ParsedSave;
            SaveMetadata.Text = result.Metadata;
            SaveHexPreview.Text = result.HexPreview;
            PopulateSaveOverview(_parsedSave);
            AddUnknownPassiveSkillsFromSave(_parsedSave);
            SaveStatus.Text = "✓ Save file loaded and parsed. This path will be restored on the next start.";
            UpdateBreedingAvailabilityStatus();
        }
        catch (Exception ex)
        {
            _parsedSave = null;
            UpdateBreedingAvailabilityStatus();
            SaveStatus.Text = "✗ " + ex.Message;
        }
    }

    private void PickBreedingJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select breeding data JSON",
            Filter = "JSON files|*.json|All files|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true) LoadBreedingJson(dialog.FileName);
    }

    private void UseBundledBreedingData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadBreedingJson(BundledBreedingDataService.EnsureExtracted());
        }
        catch (Exception ex)
        {
            BreedingStatus.Text = "✗ " + ex.Message;
        }
    }

    private void ShowBreedingJsonInfo_Click(object sender, RoutedEventArgs e)
    {
        const string message = """
PalworldHelper accepts either of these JSON structures:

1. Readable object format
{
  "schemaVersion": 1,
  "results": [
    { "parent1": "Lamball", "parent2": "Cattiva", "child": "Chikipi" }
  ]
}

2. Compact indexed format
{
  "schemaVersion": 1,
  "pals": ["Lamball", "Cattiva", "Chikipi"],
  "results": [[0, 1, 2]]
}

The compact format may use "names" instead of "pals". Every result must contain exactly two parents and one child. Empty names and invalid indexes are ignored. A JSON file containing only a manifest cannot be used as breeding data.
""";

        MessageBox.Show(this, message, "Breeding JSON format", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadBreedingJson(string path)
    {
        try
        {
            _breeding.Load(path);
            BreedingJsonPath.Text = path;
            BreedingStatus.Text = $"✓ Loaded {_breeding.ResultCount:N0} results and {_breeding.Names.Count:N0} Pal names.";
            var palOptions = _breeding.Names
                .Select(name => new PalNameOption(name, PalDisplayName(name)))
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            StartPal.ItemsSource = palOptions;
            TargetPal.ItemsSource = palOptions;
            _settings.BreedingJsonPath = path;
            _settingsService.Save(_settings);
            UpdateBreedingAvailabilityStatus();
        }
        catch (Exception ex)
        {
            BreedingStatus.Text = "✗ " + ex.Message;
        }
    }

    private void FindPath_Click(object sender, RoutedEventArgs e)
    {
        ResultList.Items.Clear();
        ParentDetails.Text = string.Empty;
        var start = SelectedPalName(StartPal);
        var target = SelectedPalName(TargetPal);
        if (string.IsNullOrWhiteSpace(target))
        {
            ResultList.Items.Add(new BreedingResultItem("Select a desired child Pal.", string.Empty));
            return;
        }

        IReadOnlyList<BreedingPlanStep>? path;
        if (!string.IsNullOrWhiteSpace(start))
        {
            path = _breeding.FindShortestFromStart(start, target, GetAvailableSpeciesOrNull());
        }
        else
        {
            path = _breeding.FindShortestFromAvailable(target, GetAvailableSpeciesOrNull());
        }

        if (path is null)
        {
            ResultList.Items.Add(new BreedingResultItem(HasParsedSave
                ? "No breeding chain was found using the Pals available in the loaded save."
                : "No breeding chain was found.", string.Empty));
            return;
        }
        if (path.Count == 0)
        {
            ResultList.Items.Add(new BreedingResultItem(HasParsedSave
                ? "You already have this Pal in the loaded save."
                : "This Pal is available directly because no save restriction is active.", target));
            ShowParentChoices(target);
            return;
        }
        for (var i = 0; i < path.Count; i++)
        {
            var parentMarker = path[i].ParentOwned ? "owned" : "bred";
            var mateMarker = path[i].MateOwned ? "owned" : "bred";
            ResultList.Items.Add(new BreedingResultItem(
                $"{i + 1}.  {PalDisplayName(path[i].Parent)} ({parentMarker}) + {PalDisplayName(path[i].Mate)} ({mateMarker})  →  {PalDisplayName(path[i].Child)}",
                path[i].Child));
        }
        ShowParentChoices(path[^1].Child);
    }

    private bool HasParsedSave => _parsedSave is { Pals.Count: > 0 };

    private HashSet<string>? GetAvailableSpeciesOrNull()
    {
        if (!HasParsedSave) return null;
        return _parsedSave!.Pals
            .Where(IsRelevantOwner)
            .Where(pal => !string.IsNullOrWhiteSpace(pal.Species))
            .Select(pal => pal.Species.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private List<string> DesiredPassives()
        => _selectedPassiveSkills.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private void AddPassiveSkill_Click(object sender, RoutedEventArgs e)
    {
        var selected = PassiveSkillPicker.SelectedItem is PassiveSkillOption option
            ? option.Name
            : PassiveSkillPicker.Text?.Trim();

        if (string.IsNullOrWhiteSpace(selected)) return;

        var match = _passiveSkillOptions.FirstOrDefault(skill =>
            skill.Name.Equals(selected, StringComparison.OrdinalIgnoreCase)
            || skill.Id.Equals(selected, StringComparison.OrdinalIgnoreCase)
            || skill.DisplayName.Equals(selected, StringComparison.OrdinalIgnoreCase));

        AddPassiveSkillTag(match?.Name ?? selected);
        PassiveSkillPicker.SelectedItem = null;
        PassiveSkillPicker.Text = string.Empty;
    }

    private void AddPassiveSkillTag(string skill)
    {
        if (_selectedPassiveSkills.Contains(skill, StringComparer.OrdinalIgnoreCase)) return;
        if (_selectedPassiveSkills.Count >= 4)
        {
            PassiveSkillLimitStatus.Text = "A Pal can have at most 4 passive skills. Remove one before adding another.";
            return;
        }

        _selectedPassiveSkills.Add(skill);
        RenderPassiveSkillTags();
    }

    private void RemovePassiveSkill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string skill) return;
        _selectedPassiveSkills.RemoveAll(x => x.Equals(skill, StringComparison.OrdinalIgnoreCase));
        RenderPassiveSkillTags();
    }

    private void RenderPassiveSkillTags()
    {
        PassiveSkillTags.Children.Clear();
        foreach (var skill in _selectedPassiveSkills.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
        {
            var button = new Button
            {
                Content = skill + "  ×",
                Tag = skill,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 6),
                Background = (System.Windows.Media.Brush)FindResource("Panel2")
            };
            button.Click += RemovePassiveSkill_Click;
            PassiveSkillTags.Children.Add(button);
        }

        PassiveSkillLimitStatus.Text = $"{_selectedPassiveSkills.Count}/4 passive skills selected. Cleaner parents with fewer extra passives are ranked higher.";
    }

    private void SaveBreedingGoal_Click(object sender, RoutedEventArgs e)
    {
        var child = SelectedPalName(TargetPal);
        if (string.IsNullOrWhiteSpace(child))
        {
            ParentDetails.Text = "Select a desired child Pal before saving a breeding goal.";
            return;
        }

        var passives = DesiredPassives().Take(4).ToList();
        var existing = _settings.BreedingWishlist.FirstOrDefault(item =>
            item.Child.Equals(child, StringComparison.OrdinalIgnoreCase)
            && item.PassiveSkills.SequenceEqual(passives, StringComparer.OrdinalIgnoreCase));

        if (existing is null)
        {
            _settings.BreedingWishlist.Add(new BreedingWishlistItem
            {
                Child = child,
                PassiveSkills = passives,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _settingsService.Save(_settings);
        RefreshBreedingWishlist();
        SelectBreedingWishlistItem(child, passives);
        ParentDetails.Text = $"Saved breeding goal: {PalDisplayName(child)}{FormatPassiveSuffix(passives)}.";
    }

    private void LoadBreedingGoal_Click(object sender, RoutedEventArgs e)
    {
        if (BreedingWishlistList.SelectedItem is BreedingWishlistRow row)
            LoadBreedingGoal(row);
    }

    private void RemoveBreedingGoal_Click(object sender, RoutedEventArgs e)
    {
        if (BreedingWishlistList.SelectedItem is not BreedingWishlistRow row) return;
        _settings.BreedingWishlist.RemoveAll(item => item.Child.Equals(row.Child, StringComparison.OrdinalIgnoreCase)
            && string.Join(", ", item.PassiveSkills.Distinct(StringComparer.OrdinalIgnoreCase).Take(4)).Equals(row.PassiveSkills, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(_settings);
        RefreshBreedingWishlist();
    }

    private void BreedingWishlistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BreedingWishlistList.SelectedItem is BreedingWishlistRow row)
            ParentDetails.Text = $"Saved goal selected: {row.Summary}. Click Load selected to use it.";
    }

    private void LoadBreedingGoal(BreedingWishlistRow row)
    {
        TargetPal.SelectedItem = TargetPal.Items.Cast<PalNameOption>().FirstOrDefault(option => option.Name.Equals(row.Child, StringComparison.OrdinalIgnoreCase));
        TargetPal.Text = PalDisplayName(row.Child);
        _selectedPassiveSkills.Clear();
        _selectedPassiveSkills.AddRange(row.PassiveSkills.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4));
        RenderPassiveSkillTags();
        ParentDetails.Text = $"Loaded breeding goal: {row.Summary}.";
    }

    private void RefreshBreedingWishlist()
    {
        if (BreedingWishlistList is null) return;

        BreedingWishlistList.ItemsSource = _settings.BreedingWishlist
            .Where(item => !string.IsNullOrWhiteSpace(item.Child))
            .OrderBy(item => PalDisplayName(item.Child), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => string.Join(", ", item.PassiveSkills), StringComparer.CurrentCultureIgnoreCase)
            .Select(item =>
            {
                var passives = item.PassiveSkills.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList();
                var passiveText = string.Join(", ", passives);
                var displayName = PalDisplayName(item.Child);
                return new BreedingWishlistRow(
                    item.Child,
                    displayName,
                    passiveText,
                    $"{displayName}{FormatPassiveSuffix(passives)}");
            })
            .ToList();
    }

    private void SelectBreedingWishlistItem(string child, List<string> passives)
    {
        var passiveText = string.Join(", ", passives);
        BreedingWishlistList.SelectedItem = BreedingWishlistList.Items
            .Cast<BreedingWishlistRow>()
            .FirstOrDefault(row => row.Child.Equals(child, StringComparison.OrdinalIgnoreCase)
                && row.PassiveSkills.Equals(passiveText, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatPassiveSuffix(List<string> passives)
        => passives.Count == 0 ? "" : $" · {string.Join(", ", passives)}";

    private void ResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var text = ResultList.SelectedItem?.ToString();
        if (ResultList.SelectedItem is BreedingResultItem { Child.Length: > 0 } item)
        {
            ShowParentChoices(item.Child);
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || !text.Contains('→')) return;
        ShowParentChoices(PalCanonicalName(text.Split('→').Last()));
    }

    private void ShowParentChoices(string child)
    {
        var desiredPassives = DesiredPassives();
        var choices = _breeding.GetParentChoices(child);
        if (choices.Count == 0)
        {
            ParentDetails.Text = $"No parent combinations found for {PalDisplayName(child)}.";
            return;
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine(CultureInfo.InvariantCulture, $"Target child: {PalDisplayName(child)}");
        output.AppendLine(HasParsedSave ? $"Loaded save is active: only Pals assigned to {CurrentOwnerName() ?? "the selected owner"} are ranked as available." : "No save is active: all species are treated as available.");
        output.AppendLine(desiredPassives.Count == 0 ? "Desired passives: none selected" : $"Desired passives: {string.Join(", ", desiredPassives)}");
        output.AppendLine();

        var ownerChoices = HasParsedSave
            ? choices
                .Select(choice => new
                {
                    Choice = choice,
                    Parent1 = GetBestOwnedPal(choice.Parent1, desiredPassives),
                    Parent2 = GetBestOwnedPal(choice.Parent2, desiredPassives)
                })
                .Where(choice => choice.Parent1 is not null && choice.Parent2 is not null)
                .OrderByDescending(choice => PassiveMatchCount(choice.Parent1!, desiredPassives) + PassiveMatchCount(choice.Parent2!, desiredPassives))
                .ThenBy(choice => UnwantedPassiveCount(choice.Parent1!, desiredPassives) + UnwantedPassiveCount(choice.Parent2!, desiredPassives))
                .ThenByDescending(choice => choice.Parent1!.Level + choice.Parent2!.Level)
                .Select(choice => choice.Choice)
                .ToList()
            : choices.ToList();

        if (ownerChoices.Count == 0)
        {
            ParentDetails.Text = BuildSkillCarrierFallback(child, desiredPassives);
            return;
        }

        foreach (var choice in ownerChoices.Take(20))
        {
            var parent1 = GetBestOwnedPal(choice.Parent1, desiredPassives);
            var parent2 = GetBestOwnedPal(choice.Parent2, desiredPassives);
            output.AppendLine(CultureInfo.InvariantCulture, $"{PalDisplayName(choice.Parent1)} + {PalDisplayName(choice.Parent2)}");
            output.AppendLine(CultureInfo.InvariantCulture, $"  {DescribeParentCandidate(choice.Parent1, parent1, desiredPassives)}");
            output.AppendLine(CultureInfo.InvariantCulture, $"  {DescribeParentCandidate(choice.Parent2, parent2, desiredPassives)}");
            output.AppendLine();
        }

        ParentDetails.Text = output.ToString();
    }

    private ParsedPal? GetBestOwnedPal(string species, List<string> desiredPassives)
    {
        if (!HasParsedSave) return null;
        return _parsedSave!.Pals
            .Where(IsRelevantOwner)
            .Where(pal => pal.Species.Equals(species, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pal => PassiveMatchCount(pal, desiredPassives))
            .ThenBy(pal => UnwantedPassiveCount(pal, desiredPassives))
            .ThenBy(pal => pal.PassiveSkills.Count)
            .ThenByDescending(pal => pal.Level)
            .FirstOrDefault();
    }

    private string BuildSkillCarrierFallback(string child, List<string> desiredPassives)
    {
        var ownerName = CurrentOwnerName() ?? "the selected owner";
        if (!HasParsedSave || desiredPassives.Count == 0)
            return $"No parent combinations for {PalDisplayName(child)} are fully assigned to {ownerName}.";

        var carriers = FindSkillCarrierPlans(child, desiredPassives);
        if (carriers.Count == 0)
            return $"No parent combinations for {PalDisplayName(child)} are fully assigned to {ownerName}, and no owned Pals with the desired passive skills were found.";

        var covered = carriers.SelectMany(plan => plan.CoveredSkills).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = desiredPassives.Except(covered, StringComparer.OrdinalIgnoreCase).ToList();
        var output = new System.Text.StringBuilder();
        output.AppendLine(CultureInfo.InvariantCulture, $"No parent combinations for {PalDisplayName(child)} are fully assigned to {ownerName}.");
        output.AppendLine();
        output.AppendLine("Fallback: use owned passive carriers as the gene pool.");
        output.AppendLine("A child can inherit none, one, or two passives from its parents. Exact odds are not calculated here.");
        output.AppendLine("Mutations can also add unexpected passives — try your luck if the perfect route is not available.");
        output.AppendLine();

        foreach (var plan in carriers)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"{PalDisplayName(plan.Carrier.Species)} ({DisplayNickname(plan.Carrier)}) covers {string.Join(", ", plan.CoveredSkills)}");
            output.AppendLine(CultureInfo.InvariantCulture, $"  Owner: {plan.Carrier.Owner} · Level {plan.Carrier.Level} · {plan.Carrier.Gender}");
            output.AppendLine(CultureInfo.InvariantCulture, $"  Passives: {(plan.Carrier.PassiveSkills.Count == 0 ? "none" : string.Join(", ", plan.Carrier.PassiveSkills))}");
            if (plan.Path is null)
            {
                output.AppendLine(CultureInfo.InvariantCulture, $"  No breeding route from {PalDisplayName(plan.Carrier.Species)} to {PalDisplayName(child)} was found with the current owner pool.");
            }
            else if (plan.Path.Count == 0)
            {
                output.AppendLine(CultureInfo.InvariantCulture, $"  Carrier already is {PalDisplayName(child)}.");
            }
            else
            {
                output.AppendLine(CultureInfo.InvariantCulture, $"  Suggested route to {PalDisplayName(child)}:");
                for (var i = 0; i < plan.Path.Count; i++)
                    output.AppendLine(CultureInfo.InvariantCulture, $"    {i + 1}. {PalDisplayName(plan.Path[i].Parent)} + {PalDisplayName(plan.Path[i].Mate)} → {PalDisplayName(plan.Path[i].Child)}");
            }
            output.AppendLine();
        }

        if (missing.Count > 0)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"Still missing in owned Pals: {string.Join(", ", missing)}");
            output.AppendLine("Try your luck via mutation or add/capture a Pal carrying the missing passive.");
        }

        return output.ToString();
    }

    private List<SkillCarrierPlan> FindSkillCarrierPlans(string child, List<string> desiredPassives)
    {
        var selected = new List<SkillCarrierPlan>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var available = GetAvailableSpeciesOrNull();
        var candidates = _parsedSave!.Pals
            .Where(IsRelevantOwner)
            .Select(pal => new
            {
                Pal = pal,
                CoveredSkills = desiredPassives.Where(skill => pal.PassiveSkills.Contains(skill, StringComparer.OrdinalIgnoreCase)).ToList()
            })
            .Where(candidate => candidate.CoveredSkills.Count > 0)
            .OrderByDescending(candidate => candidate.CoveredSkills.Count)
            .ThenBy(candidate => UnwantedPassiveCount(candidate.Pal, desiredPassives))
            .ThenBy(candidate => candidate.Pal.Species.Equals(child, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(candidate => RouteLength(candidate.Pal.Species, child, available))
            .ThenByDescending(candidate => candidate.Pal.Level)
            .ToList();

        foreach (var candidate in candidates)
        {
            var newSkills = candidate.CoveredSkills.Where(skill => !covered.Contains(skill)).ToList();
            if (newSkills.Count == 0) continue;

            var path = _breeding.FindShortestFromStart(candidate.Pal.Species, child, available);
            selected.Add(new SkillCarrierPlan(candidate.Pal, newSkills, path));
            foreach (var skill in newSkills) covered.Add(skill);
            if (desiredPassives.All(covered.Contains)) break;
        }

        return selected;
    }

    private int RouteLength(string start, string target, IReadOnlySet<string>? available)
    {
        var path = _breeding.FindShortestFromStart(start, target, available);
        return path?.Count ?? int.MaxValue;
    }

    private string DescribeParentCandidate(string species, ParsedPal? pal, List<string> desiredPassives)
    {
        if (pal is null)
            return $"best {PalDisplayName(species)}: no owned candidate in loaded save";

        var matched = desiredPassives.Where(skill => pal.PassiveSkills.Contains(skill, StringComparer.OrdinalIgnoreCase)).ToList();
        var unwanted = UnwantedPassiveCount(pal, desiredPassives);
        var passives = pal.PassiveSkills.Count == 0 ? "no passive skills" : string.Join(", ", pal.PassiveSkills);
        var matchText = desiredPassives.Count == 0 ? $" | clean passives: {pal.PassiveSkills.Count}" : $" | matches {matched.Count}/{desiredPassives.Count}: {(matched.Count == 0 ? "none" : string.Join(", ", matched))} | extra passives: {unwanted}";
        var nickname = string.IsNullOrWhiteSpace(pal.Nickname) ? "" : $" ({pal.Nickname})";
        return $"best {PalDisplayName(species)}: {pal.Owner}{nickname}, Level {pal.Level}, {pal.Gender}, {passives}{matchText}";
    }

    private static int PassiveMatchCount(ParsedPal pal, List<string> desiredPassives)
        => desiredPassives.Count(skill => pal.PassiveSkills.Contains(skill, StringComparer.OrdinalIgnoreCase));

    private static int UnwantedPassiveCount(ParsedPal pal, List<string> desiredPassives)
        => desiredPassives.Count == 0
            ? pal.PassiveSkills.Count
            : pal.PassiveSkills.Count(skill => !desiredPassives.Contains(skill, StringComparer.OrdinalIgnoreCase));

    private static string DisplayNickname(ParsedPal pal)
        => string.IsNullOrWhiteSpace(pal.Nickname) ? pal.Species : pal.Nickname;

    private string PalDisplayName(string name) => _palNames.DisplayName(name);

    private string PalCanonicalName(string name) => _palNames.CanonicalName(name, _breeding.Names);

    private string SelectedPalName(ComboBox comboBox)
        => comboBox.SelectedItem is PalNameOption option
            ? option.Name
            : PalCanonicalName(comboBox.Text ?? string.Empty);

    private void UpdateBreedingAvailabilityStatus()
    {
        if (_breeding.Names.Count == 0) return;
        var suffix = HasParsedSave
            ? $" Save restriction active: {_parsedSave!.Pals.Where(IsRelevantOwner).Select(p => p.Species).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count():N0} owned species available for {CurrentOwnerName() ?? "the selected owner"}."
            : " No save restriction active: all Pals are treated as available.";
        BreedingStatus.Text = $"✓ Loaded {_breeding.ResultCount:N0} results and {_breeding.Names.Count:N0} Pal names.{suffix}";
    }

    private void PopulateSaveOverview(ParsedSave save)
    {
        _savePlayerRows = save.Players
            .OrderBy(player => player.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(player => new SavePlayerRow(player.Name, player.Level, player.PlayerUid))
            .ToList();

        _savePalRows = save.Pals
            .OrderBy(pal => pal.Owner, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(pal => pal.Species, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(pal => pal.Level)
            .Select(pal => new SavePalRow(
                pal.Species,
                pal.Nickname,
                pal.Owner,
                string.IsNullOrWhiteSpace(pal.Storage) ? "World / base" : pal.Storage,
                pal.Level,
                pal.Gender,
                pal.PassiveSkills.Count == 0 ? "—" : string.Join(", ", pal.PassiveSkills),
                pal.OwnerPlayerUids))
            .ToList();

        SavePlayersList.ItemsSource = _savePlayerRows;
        ApplyOwnerFilter();
    }

    private void AddUnknownPassiveSkillsFromSave(ParsedSave save)
    {
        var known = _passiveSkillOptions.Select(skill => skill.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extra = save.Pals
            .SelectMany(pal => pal.PassiveSkills)
            .Where(skill => !string.IsNullOrWhiteSpace(skill) && known.Add(skill))
            .Select(skill => new PassiveSkillOption(skill, skill, 0, "Found in loaded save"))
            .ToList();

        if (extra.Count == 0) return;
        _passiveSkillOptions.AddRange(extra);
        PassiveSkillPicker.ItemsSource = null;
        PassiveSkillPicker.ItemsSource = _passiveSkillOptions
            .OrderByDescending(skill => skill.Rank)
            .ThenBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private string? CurrentOwnerName()
    {
        if (_showAllOwners) return null;
        if (!string.IsNullOrWhiteSpace(_selectedOwnerName)) return _selectedOwnerName;
        var configured = PlayerName.Text?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private bool IsRelevantOwner(ParsedPal pal)
    {
        var owner = CurrentOwnerName();
        if (string.IsNullOrWhiteSpace(owner)) return true;
        if (pal.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase)) return true;

        var ownerUid = CurrentOwnerUid();
        return !string.IsNullOrWhiteSpace(ownerUid)
            && pal.OwnerPlayerUids.Any(uid => NormalizeUid(uid).Equals(ownerUid, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsRelevantOwner(SavePalRow pal)
    {
        var owner = CurrentOwnerName();
        if (string.IsNullOrWhiteSpace(owner)) return true;
        if (pal.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase)) return true;

        var ownerUid = CurrentOwnerUid();
        return !string.IsNullOrWhiteSpace(ownerUid)
            && pal.OwnerPlayerUids.Any(uid => NormalizeUid(uid).Equals(ownerUid, StringComparison.OrdinalIgnoreCase));
    }

    private string? CurrentOwnerUid()
    {
        var owner = CurrentOwnerName();
        if (string.IsNullOrWhiteSpace(owner)) return null;
        var ownerUid = _savePlayerRows.FirstOrDefault(player => player.Name.Equals(owner, StringComparison.OrdinalIgnoreCase))?.PlayerUid;
        return string.IsNullOrWhiteSpace(ownerUid) ? null : NormalizeUid(ownerUid);
    }

    private static string NormalizeUid(string value)
        => value.Replace("-", string.Empty, StringComparison.Ordinal).Trim();

    private void SavePlayersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedOwnerName = SavePlayersList.SelectedItem is SavePlayerRow player ? player.Name : null;
        _showAllOwners = false;
        ApplyOwnerFilter();
        UpdateBreedingAvailabilityStatus();
        RefreshSelectedParentDetails();
    }

    private void ShowAllOwners_Click(object sender, RoutedEventArgs e)
    {
        _selectedOwnerName = null;
        _showAllOwners = true;
        SavePlayersList.SelectedItem = null;
        ApplyOwnerFilter();
        UpdateBreedingAvailabilityStatus();
        RefreshSelectedParentDetails();
    }

    private void ApplyOwnerFilter()
    {
        var owner = CurrentOwnerName();
        var rows = string.IsNullOrWhiteSpace(owner)
            ? _savePalRows
            : _savePalRows.Where(IsRelevantOwner).ToList();

        SavePalsList.ItemsSource = rows;
        UpdateSaveStatCards(rows, owner);
        SaveOwnerFilterStatus.Text = string.IsNullOrWhiteSpace(owner)
            ? "Showing all owners."
            : $"Showing Pals assigned to {owner}.";
    }

    private void UpdateSaveStatCards(IReadOnlyCollection<SavePalRow> rows, string? owner)
    {
        var ownerCount = string.IsNullOrWhiteSpace(owner)
            ? _savePlayerRows.Count
            : _savePlayerRows.Any(player => player.Name.Equals(owner, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;

        SavePlayerCount.Text = ownerCount.ToString("N0", CultureInfo.CurrentCulture);
        SavePalCount.Text = rows.Count.ToString("N0", CultureInfo.CurrentCulture);
        SaveSpeciesCount.Text = rows
            .Select(pal => pal.Species)
            .Where(species => !string.IsNullOrWhiteSpace(species))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .ToString("N0", CultureInfo.CurrentCulture);
        SavePassiveCount.Text = rows
            .SelectMany(pal => pal.PassiveSkills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(skill => skill.Length > 0 && skill != "—")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count()
            .ToString("N0", CultureInfo.CurrentCulture);
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: not null } header) return;
        var property = header.Column.Header?.ToString();
        if (string.IsNullOrWhiteSpace(property)) return;
        if (property.Equals("Passives", StringComparison.OrdinalIgnoreCase)) property = nameof(SavePalRow.PassiveSkills);
        if (property.Equals("UID", StringComparison.OrdinalIgnoreCase)) property = nameof(SavePlayerRow.PlayerUid);

        var listView = sender as ListView ?? FindVisualParent<ListView>(header);
        if (listView?.ItemsSource is null) return;

        var view = CollectionViewSource.GetDefaultView(listView.ItemsSource);
        var direction = ListSortDirection.Ascending;
        if (view.SortDescriptions.Count > 0 && view.SortDescriptions[0].PropertyName.Equals(property, StringComparison.OrdinalIgnoreCase))
        {
            direction = view.SortDescriptions[0].Direction == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(property, direction));
        view.Refresh();
    }

    private void RefreshSelectedParentDetails()
    {
        var text = ResultList.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return;
        var child = ResultList.SelectedItem is BreedingResultItem { Child.Length: > 0 } item
            ? item.Child
            : text.Contains('→') ? PalCanonicalName(text.Split('→').Last()) : SelectedPalName(TargetPal);
        if (!string.IsNullOrWhiteSpace(child)) ShowParentChoices(child);
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
