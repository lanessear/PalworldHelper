namespace PalworldHelper.Core.Abstractions;

public interface ISaveImportService
{
    Task<SaveImportResult> ImportAsync(SaveImportRequest request, CancellationToken cancellationToken = default);
}

public sealed record SaveImportRequest(Guid ServerProfileId, string? PlayerName = null);

public sealed record SaveImportResult(
    bool Succeeded,
    string Message,
    int ImportedPalCount,
    DateTimeOffset CompletedAtUtc);
