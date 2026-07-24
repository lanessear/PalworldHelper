using Renci.SshNet;
using Renci.SshNet.Common;
using PalworldHelper.Models;

namespace PalworldHelper.Services;

public sealed class SshSaveDownloader
{
    public async Task<string> DownloadAsync(ServerProfile profile, string? password, string? keyPassphrase, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrWhiteSpace(profile.PrivateKeyPath))
            {
                if (!File.Exists(profile.PrivateKeyPath)) throw new FileNotFoundException("SSH private key not found.", profile.PrivateKeyPath);
                var key = string.IsNullOrEmpty(keyPassphrase)
                    ? new PrivateKeyFile(profile.PrivateKeyPath)
                    : new PrivateKeyFile(profile.PrivateKeyPath, keyPassphrase);
                methods.Add(new PrivateKeyAuthenticationMethod(profile.Username, key));
            }
            if (!string.IsNullOrEmpty(password)) methods.Add(new PasswordAuthenticationMethod(profile.Username, password));
            if (methods.Count == 0) throw new InvalidOperationException("Enter a password or configure a private key.");

            var info = new ConnectionInfo(profile.Host, profile.Port, profile.Username, methods.ToArray());
            using var client = new SftpClient(info);
            client.Connect();
            var tempDir = Path.Combine(Path.GetTempPath(), "PalworldHelper", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var localPath = Path.Combine(tempDir, "Level.sav");
            using (var output = File.Create(localPath)) client.DownloadFile(profile.RemoteSavePath, output);
            client.Disconnect();
            return localPath;
        }, ct);
    }
}
