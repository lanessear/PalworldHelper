using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PalworldHelper.Data.Persistence;

namespace PalworldHelper.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPalworldHelperData(this IServiceCollection services, string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddDbContext<PalworldHelperDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        return services;
    }

    public static async Task InitializePalworldHelperDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PalworldHelperDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
