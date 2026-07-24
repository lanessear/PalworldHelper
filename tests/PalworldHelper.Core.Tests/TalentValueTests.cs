using PalworldHelper.Core.Domain;

namespace PalworldHelper.Core.Tests;

public sealed class TalentValueTests
{
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 101, 0)]
    [InlineData(0, 0, 999)]
    public void Validate_RejectsValuesOutsideRange(int health, int attack, int defense)
    {
        var talent = new TalentValue(health, attack, defense);
        Assert.Throws<ArgumentOutOfRangeException>(() => talent.Validate());
    }
}
