using Microsoft.Extensions.DependencyInjection;
using PalworldHelper.Core.Abstractions;

namespace PalworldHelper.SaveImport;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPalworldHelperSaveImport(this IServiceCollection services)
    {
        services.AddSingleton<IRemoteSaveDownloader, SftpRemoteSaveDownloader>();
        services.AddScoped<ISaveImportService, SaveImportService>();
        return services;
    }
}
