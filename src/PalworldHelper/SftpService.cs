using Renci.SshNet;

namespace PalworldHelper;

public sealed class SftpService
{
    private static ConnectionInfo BuildConnection(ServerProfile p, string password)
    {
        if (string.Equals(p.Authentication, "SSH-Key", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(p.PrivateKeyPath)) throw new FileNotFoundException("SSH-Key wurde nicht gefunden.", p.PrivateKeyPath);
            return new ConnectionInfo(p.Host, p.Port, p.Username, new PrivateKeyAuthenticationMethod(p.Username, new PrivateKeyFile(p.PrivateKeyPath)));
        }
        return new ConnectionInfo(p.Host, p.Port, p.Username, new PasswordAuthenticationMethod(p.Username, password));
    }

    public Task TestAsync(ServerProfile p, string password) => Task.Run(() =>
    {
        using var client = new SftpClient(BuildConnection(p, password));
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
        client.Connect();
        client.Disconnect();
    });

    public Task<string> DownloadSaveAsync(ServerProfile p, string password) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(p.RemoteSavePath)) throw new InvalidOperationException("Bitte den vollständigen Remote-Pfad zur Level.sav eintragen.");
        var targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PalworldHelper", "saves", p.Id);
        Directory.CreateDirectory(targetDir);
        var target = Path.Combine(targetDir, $"Level_{DateTime.Now:yyyyMMdd_HHmmss}.sav");
        using var client = new SftpClient(BuildConnection(p, password));
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
        client.Connect();
        using var output = File.Create(target);
        client.DownloadFile(p.RemoteSavePath, output);
        client.Disconnect();
        return target;
    });
}
