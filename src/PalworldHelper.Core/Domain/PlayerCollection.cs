namespace PalworldHelper.Core.Domain;

public sealed class PlayerCollection
{
    private PlayerCollection()
    {
    }

    public PlayerCollection(string playerId, string playerName, Guid serverProfileId)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("Player ID is required.", nameof(playerId)) : playerId.Trim();
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? throw new ArgumentException("Player name is required.", nameof(playerName)) : playerName.Trim();
        ServerProfileId = serverProfileId != Guid.Empty ? serverProfileId : throw new ArgumentException("Server profile ID is required.", nameof(serverProfileId));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string PlayerId { get; private set; } = string.Empty;
    public string PlayerName { get; private set; } = string.Empty;
    public Guid ServerProfileId { get; private set; }
    public DateTimeOffset? LastImportedAtUtc { get; private set; }
    public ICollection<PalInstance> Pals { get; private set; } = new List<PalInstance>();

    public void MarkImported(DateTimeOffset importedAtUtc) => LastImportedAtUtc = importedAtUtc;
}
