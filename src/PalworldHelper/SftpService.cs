using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.IO;

namespace PalworldHelper;

public sealed record RemoteSaveCandidate(string Path, DateTime LastWriteTimeUtc);

public sealed class SftpService
{
    private static readonly string[] CommonSearchRoots =
    [
        "/home",
        "/opt",
        "/srv",
        "/mnt",
        "/palworld",
        "/PalServer"
    ];

    private static ConnectionInfo BuildConnection(ServerProfile p, string password)
    {
        if (string.Equals(p.Authentication, "SSH-Key", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Authentication, "SSH key", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(p.PrivateKeyPath))
            {
                throw new FileNotFoundException("The SSH key could not be found.", p.PrivateKeyPath);
            }

            return new ConnectionInfo(
                p.Host,
                p.Port,
                p.Username,
                new PrivateKeyAuthenticationMethod(
                    p.Username,
                    new PrivateKeyFile(p.PrivateKeyPath)));
        }

        return new ConnectionInfo(
            p.Host,
            p.Port,
            p.Username,
            new PasswordAuthenticationMethod(p.Username, password));
    }

    public static Task TestAsync(ServerProfile p, string password) => Task.Run(() =>
    {
        using var client = CreateClient(p, password, TimeSpan.FromSeconds(10));
        client.Connect();
        client.Disconnect();
    });

    public static Task<IReadOnlyList<RemoteSaveCandidate>> FindSaveFilesAsync(
        ServerProfile p,
        string password) => Task.Run<IReadOnlyList<RemoteSaveCandidate>>(() =>
    {
        using var client = CreateClient(p, password, TimeSpan.FromSeconds(20));
        client.Connect();

        var roots = BuildSearchRoots(client);
        var matches = new Dictionary<string, RemoteSaveCandidate>(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            SearchDirectory(client, root, 0, matches);
        }

        client.Disconnect();

        return matches.Values
            .OrderByDescending(candidate => candidate.LastWriteTimeUtc)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .ToList();
    });

    public static async Task<string> DownloadSaveAsync(ServerProfile p, string password)
    {
        var remotePath = p.RemoteSavePath;
        if (string.IsNullOrWhiteSpace(remotePath))
        {
            var candidates = await FindSaveFilesAsync(p, password).ConfigureAwait(false);
            remotePath = candidates.Count > 0 ? candidates[0].Path : null;

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                throw new FileNotFoundException(
                    "No Level.sav file was found on the server. Enter the path manually.");
            }
        }

        return await Task.Run(() => DownloadFile(p, password, remotePath)).ConfigureAwait(false);
    }

    private static string DownloadFile(ServerProfile p, string password, string remotePath)
    {
        var targetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PalworldHelper",
            "saves",
            p.Id);

        Directory.CreateDirectory(targetDir);
        var target = Path.Combine(targetDir, $"Level_{DateTime.Now:yyyyMMdd_HHmmss}.sav");

        using var client = CreateClient(p, password, TimeSpan.FromSeconds(30));
        client.Connect();

        if (!client.Exists(remotePath))
        {
            throw new FileNotFoundException("The specified Level.sav file does not exist on the server.", remotePath);
        }

        using var output = File.Create(target);
        client.DownloadFile(remotePath, output);
        client.Disconnect();
        return target;
    }

    private static SftpClient CreateClient(ServerProfile p, string password, TimeSpan timeout)
    {
        var client = new SftpClient(BuildConnection(p, password));
        client.ConnectionInfo.Timeout = timeout;
        client.OperationTimeout = timeout;
        return client;
    }

    private static List<string> BuildSearchRoots(SftpClient client)
    {
        var roots = new List<string>();

        AddRootIfAvailable(client, roots, client.WorkingDirectory);
        foreach (var root in CommonSearchRoots)
        {
            AddRootIfAvailable(client, roots, root);
        }

        return roots.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddRootIfAvailable(SftpClient client, List<string> roots, string root)
    {
        try
        {
            if (client.Exists(root) && client.GetAttributes(root).IsDirectory)
            {
                roots.Add(root);
            }
        }
        catch (SshException)
        {
            // The configured user may not have permission to access this common search path.
        }
    }

    private static void SearchDirectory(
        SftpClient client,
        string directory,
        int depth,
        IDictionary<string, RemoteSaveCandidate> matches)
    {
        const int maximumDepth = 10;
        const int maximumMatches = 50;

        if (depth > maximumDepth || matches.Count >= maximumMatches)
        {
            return;
        }

        IEnumerable<ISftpFile> entries;
        try
        {
            entries = client.ListDirectory(directory);
        }
        catch (SshException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.Name is "." or ".." || entry.IsSymbolicLink)
            {
                continue;
            }

            if (entry.IsRegularFile &&
                string.Equals(entry.Name, "Level.sav", StringComparison.OrdinalIgnoreCase))
            {
                matches[entry.FullName] = new RemoteSaveCandidate(
                    entry.FullName,
                    entry.LastWriteTimeUtc);
                continue;
            }

            if (!entry.IsDirectory || ShouldSkipDirectory(entry.Name))
            {
                continue;
            }

            SearchDirectory(client, entry.FullName, depth + 1, matches);
            if (matches.Count >= maximumMatches)
            {
                return;
            }
        }
    }

    private static bool ShouldSkipDirectory(string name) =>
        name.Equals("proc", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sys", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("dev", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("run", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("lost+found", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
}
