using PalworldHelper.Core.Abstractions;

namespace PalworldHelper.App;

public sealed class AppDataPathProvider : IAppDataPathProvider
{
    public AppDataPathProvider()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        RootDirectory = Path.Combine(localAppData, "PalworldHelper");
        DatabasePath = Path.Combine(RootDirectory, "palworldhelper.db");
        ImportDirectory = Path.Combine(RootDirectory, "imports");
        PluginDirectory = Path.Combine(RootDirectory, "plugins");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ImportDirectory);
        Directory.CreateDirectory(PluginDirectory);
    }

    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string ImportDirectory { get; }
    public string PluginDirectory { get; }
}
