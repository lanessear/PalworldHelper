namespace PalworldHelper.Core.Abstractions;

public interface IBreedingPlanner
{
    Task<IReadOnlyList<BreedingPlan>> FindPlansAsync(BreedingPlanRequest request, CancellationToken cancellationToken = default);
}

public sealed record BreedingPlanRequest(
    Guid TargetSpeciesId,
    IReadOnlyCollection<Guid> DesiredPassiveSkillIds,
    Guid? CollectionId,
    int MaximumGenerations = 8);

public sealed record BreedingPlan(
    string Name,
    int Generations,
    decimal EstimatedScore,
    IReadOnlyList<BreedingPlanStep> Steps);

public sealed record BreedingPlanStep(Guid ParentAId, Guid ParentBId, Guid ChildId, int Generation);
