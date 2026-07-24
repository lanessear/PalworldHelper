using PalworldHelper.Core.Abstractions;

namespace PalworldHelper.SaveImport;

public sealed class SaveImportService : ISaveImportService
{
    public Task<SaveImportResult> ImportAsync(
        SaveImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new SaveImportResult(
            Succeeded: false,
            Message: "Save import is prepared architecturally but not implemented in this foundation release.",
            ImportedPalCount: 0,
            CompletedAtUtc: DateTimeOffset.UtcNow));
    }
}
