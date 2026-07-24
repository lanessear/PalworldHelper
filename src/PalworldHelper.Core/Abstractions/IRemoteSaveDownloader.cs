namespace PalworldHelper.Core.Abstractions;

public interface IRemoteSaveDownloader
{
    Task<string> DownloadAsync(RemoteSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record RemoteSaveRequest(
    string Host,
    int Port,
    string Username,
    string RemotePath,
    string LocalDirectory,
    string CredentialReference);
