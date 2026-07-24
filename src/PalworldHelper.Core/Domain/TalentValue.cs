namespace PalworldHelper.Core.Domain;

public sealed record TalentValue(int Health, int Attack, int Defense)
{
    public static TalentValue Empty { get; } = new(0, 0, 0);

    public TalentValue Validate()
    {
        ValidatePart(Health, nameof(Health));
        ValidatePart(Attack, nameof(Attack));
        ValidatePart(Defense, nameof(Defense));
        return this;
    }

    private static void ValidatePart(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, "Talent values must be between 0 and 100.");
        }
    }
}
