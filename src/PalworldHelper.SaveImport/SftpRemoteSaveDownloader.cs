using PalworldHelper.Core.Abstractions;

namespace PalworldHelper.SaveImport;

public sealed class SftpRemoteSaveDownloader : IRemoteSaveDownloader
{
    public Task<string> DownloadAsync(RemoteSaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Credential resolution and SSH.NET transfer are deliberately left for the
        // dedicated save-import milestone. Credentials must not be passed as plain text.
        throw new NotImplementedException(
            "SFTP download is not implemented yet. The interface is ready for a secure credential provider.");
    }
}
