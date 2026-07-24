namespace PalworldHelper.Plugins.Abstractions;

public interface IPlugin
{
    PluginMetadata Metadata { get; }
    ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);
}

public interface IPluginContext
{
    string ApplicationVersion { get; }
    string PluginDataDirectory { get; }
}

public sealed record PluginMetadata(
    string Id,
    string Name,
    string Version,
    string ApiVersion,
    string? Description = null);
