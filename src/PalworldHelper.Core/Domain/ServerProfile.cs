namespace PalworldHelper.Core.Domain;

public sealed class ServerProfile
{
    private ServerProfile()
    {
    }

    public ServerProfile(string name, string host, int port, string username, string remoteSavePath)
    {
        Name = Require(name, nameof(name));
        Host = Require(host, nameof(host));
        Port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));
        Username = Require(username, nameof(username));
        RemoteSavePath = Require(remoteSavePath, nameof(remoteSavePath));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; } = 22;
    public string Username { get; private set; } = string.Empty;
    public string RemoteSavePath { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value.Trim();
}
