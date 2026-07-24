namespace PalworldHelper.Contracts;

public sealed record SaveServerRequest(
    long? Id,
    string Name,
    string Host,
    int Port,
    string Username,
    string RemoteSavePath,
    string PlayerName,
    string? Password,
    string? PrivateKeyPath,
    string? PrivateKeyPassphrase);

public sealed record SyncRequest(
    long ServerProfileId,
    string? Password,
    string? PrivateKeyPassphrase);

public sealed record BreedingSearchRequest(
    long ServerProfileId,
    string TargetPal,
    IReadOnlyList<string> PassiveSkills,
    bool UseOnlyOwnedPals = false,
    bool PreferShortestRoute = true,
    int MaxDepth = 5,
    int MaxResults = 10);
