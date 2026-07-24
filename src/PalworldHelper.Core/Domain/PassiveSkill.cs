namespace PalworldHelper.Core.Domain;

public sealed class PassiveSkill
{
    private PassiveSkill()
    {
    }

    public PassiveSkill(string key, string displayName, int rank)
    {
        Key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Key is required.", nameof(key)) : key.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? throw new ArgumentException("Display name is required.", nameof(displayName)) : displayName.Trim();
        Rank = rank is >= -3 and <= 4 ? rank : throw new ArgumentOutOfRangeException(nameof(rank));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public int Rank { get; private set; }
    public string? Description { get; private set; }
}
