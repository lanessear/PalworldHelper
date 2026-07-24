namespace PalworldHelper.Core.Abstractions;

public interface IAppDataPathProvider
{
    string RootDirectory { get; }
    string DatabasePath { get; }
    string ImportDirectory { get; }
    string PluginDirectory { get; }
}
