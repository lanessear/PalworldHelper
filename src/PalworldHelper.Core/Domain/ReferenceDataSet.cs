namespace PalworldHelper.Core.Domain;

public sealed class ReferenceDataSet
{
    private ReferenceDataSet()
    {
    }

    public ReferenceDataSet(string gameVersion, int schemaVersion, DateTimeOffset importedAtUtc)
    {
        GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? throw new ArgumentException("Game version is required.", nameof(gameVersion)) : gameVersion.Trim();
        SchemaVersion = schemaVersion > 0 ? schemaVersion : throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ImportedAtUtc = importedAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string GameVersion { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public DateTimeOffset ImportedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
