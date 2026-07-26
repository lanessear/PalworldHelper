namespace PalworldHelper;

public sealed class AppSettings
{
    public List<ServerProfile> Profiles { get; set; } = [];
    public string? SelectedProfileId { get; set; }
    public string? BreedingJsonPath { get; set; }
    public string? LocalSavePath { get; set; }
    public string Theme { get; set; } = "Dark";
    public List<BreedingWishlistItem> BreedingWishlist { get; set; } = [];
}

public sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "My Server";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string RemoteSavePath { get; set; } = "";
    public string PlayerName { get; set; } = "Lanessear";
    public string Authentication { get; set; } = "Password";
    public string PrivateKeyPath { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public override string ToString() => Name;
}

public sealed class BreedingPayload
{
    public int SchemaVersion { get; set; }
    public List<BreedingResult> Results { get; set; } = [];
}
public sealed class BreedingResult
{
    public string Parent1 { get; set; } = "";
    public string Parent2 { get; set; } = "";
    public string Child { get; set; } = "";
    public DateTimeOffset? FirstSeen { get; set; }
}
public sealed record BreedingStep(string Parent, string Mate, string Child);

public sealed record BreedingPlanStep(string Parent, string Mate, string Child, bool ParentOwned, bool MateOwned);

public sealed record ParentChoice(string Parent1, string Parent2);

public sealed record PalNameOption(string Name, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record BreedingResultItem(string Text, string Child)
{
    public override string ToString() => Text;
}

public sealed class BreedingWishlistItem
{
    public string Child { get; set; } = "";
    public List<string> PassiveSkills { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record BreedingWishlistRow(string Child, string DisplayName, string PassiveSkills, string Summary)
{
    public override string ToString() => Summary;
}

public sealed record PassiveSkillOption(string Id, string Name, int Rank, string Description)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Description)
        ? $"{Name} ({Id})"
        : $"{Name} ({Id}) · {Description}";
}

public sealed record SavePalRow(string Species, string Nickname, string Owner, string Storage, int Level, string Gender, string PassiveSkills, IReadOnlyList<string> OwnerPlayerUids);

public sealed record SavePlayerRow(string Name, int Level, string PlayerUid);

public sealed record SkillCarrierPlan(ParsedPal Carrier, IReadOnlyList<string> CoveredSkills, IReadOnlyList<BreedingPlanStep>? Path);
