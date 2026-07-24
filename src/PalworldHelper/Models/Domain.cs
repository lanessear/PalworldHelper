namespace PalworldHelper.Models;

public sealed record ServerProfile(
    long Id,
    string Name,
    string Host,
    int Port,
    string Username,
    string RemoteSavePath,
    string PlayerName,
    string? PrivateKeyPath,
    DateTimeOffset? LastSyncUtc);

public sealed record OwnedPal(
    long Id,
    long ServerProfileId,
    string InstanceId,
    string SpeciesId,
    string DisplayName,
    string Nickname,
    int? Level,
    string Gender,
    int Rank,
    int? TalentHp,
    int? TalentAttack,
    int? TalentDefense,
    IReadOnlyList<string> PassiveSkills);

public sealed record BreedingCombination(string Parent1, string Parent2, string Child);

public sealed record BreedingStep(
    string Parent1,
    string Parent2,
    string Child,
    bool Parent1Owned,
    bool Parent2Owned,
    int Depth);

public sealed record BreedingRoute(
    string Target,
    IReadOnlyList<string> RequestedPassives,
    IReadOnlyList<BreedingStep> Steps,
    int MissingParents,
    int EstimatedEggs,
    string SkillCarrier);
