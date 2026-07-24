using PalworldHelper.Core.Domain;

namespace PalworldHelper.Core.Tests;

public sealed class PalSpeciesTests
{
    [Fact]
    public void Constructor_NormalizesRequiredText()
    {
        var species = new PalSpecies("  jolthog-cryst ", " Jolthog Cryst ", 12);

        Assert.Equal("jolthog-cryst", species.Key);
        Assert.Equal("Jolthog Cryst", species.DisplayName);
        Assert.Equal(12, species.PaldeckNumber);
    }

    [Fact]
    public void Constructor_RejectsInvalidPaldeckNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PalSpecies("test", "Test", 0));
    }
}
