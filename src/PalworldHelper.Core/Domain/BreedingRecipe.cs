namespace PalworldHelper.Core.Domain;

public sealed class BreedingRecipe
{
    private BreedingRecipe()
    {
    }

    public BreedingRecipe(Guid parentAId, Guid parentBId, Guid childId, Guid dataSetId)
    {
        if (parentAId == Guid.Empty || parentBId == Guid.Empty || childId == Guid.Empty || dataSetId == Guid.Empty)
        {
            throw new ArgumentException("Recipe identifiers must not be empty.");
        }

        ParentAId = parentAId;
        ParentBId = parentBId;
        ChildId = childId;
        DataSetId = dataSetId;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ParentAId { get; private set; }
    public Guid ParentBId { get; private set; }
    public Guid ChildId { get; private set; }
    public Guid DataSetId { get; private set; }
}
