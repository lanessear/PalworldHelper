using Microsoft.EntityFrameworkCore;
using PalworldHelper.Core.Domain;
using PalworldHelper.Data.Persistence;

namespace PalworldHelper.Data.Tests;

public sealed class DatabaseModelTests
{
    [Fact]
    public async Task CanPersistPalSpecies()
    {
        var options = new DbContextOptionsBuilder<PalworldHelperDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new PalworldHelperDbContext(options);
        db.PalSpecies.Add(new PalSpecies("polapup-terra", "Polapup Terra", 148));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PalSpecies.CountAsync());
    }
}
