namespace PalworldHelper.Core.Domain;

public sealed class PalSpecies
{
    private PalSpecies()
    {
    }

    public PalSpecies(string key, string displayName, int paldeckNumber)
    {
        Key = RequireText(key, nameof(key));
        DisplayName = RequireText(displayName, nameof(displayName));
        PaldeckNumber = paldeckNumber > 0
            ? paldeckNumber
            : throw new ArgumentOutOfRangeException(nameof(paldeckNumber));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public int PaldeckNumber { get; private set; }
    public ICollection<PalSpeciesElement> Elements { get; private set; } = new List<PalSpeciesElement>();

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
}

public sealed class PalSpeciesElement
{
    private PalSpeciesElement()
    {
    }

    public PalSpeciesElement(Guid palSpeciesId, Element element)
    {
        PalSpeciesId = palSpeciesId;
        Element = element;
    }

    public Guid PalSpeciesId { get; private set; }
    public Element Element { get; private set; }
}
