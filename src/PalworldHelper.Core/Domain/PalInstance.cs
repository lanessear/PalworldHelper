namespace PalworldHelper.Core.Domain;

public sealed class PalInstance
{
    private PalInstance()
    {
    }

    public PalInstance(Guid speciesId, Guid collectionId, string sourceInstanceId, Gender gender, int level)
    {
        SpeciesId = speciesId != Guid.Empty ? speciesId : throw new ArgumentException("Species ID is required.", nameof(speciesId));
        CollectionId = collectionId != Guid.Empty ? collectionId : throw new ArgumentException("Collection ID is required.", nameof(collectionId));
        SourceInstanceId = string.IsNullOrWhiteSpace(sourceInstanceId) ? throw new ArgumentException("Source instance ID is required.", nameof(sourceInstanceId)) : sourceInstanceId.Trim();
        Gender = gender;
        Level = level is >= 1 and <= 100 ? level : throw new ArgumentOutOfRangeException(nameof(level));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SpeciesId { get; private set; }
    public Guid CollectionId { get; private set; }
    public string SourceInstanceId { get; private set; } = string.Empty;
    public Gender Gender { get; private set; }
    public int Level { get; private set; }
    public TalentValue Talents { get; private set; } = TalentValue.Empty;
    public ICollection<PalInstancePassive> Passives { get; private set; } = new List<PalInstancePassive>();
}

public sealed class PalInstancePassive
{
    private PalInstancePassive()
    {
    }

    public PalInstancePassive(Guid palInstanceId, Guid passiveSkillId)
    {
        PalInstanceId = palInstanceId;
        PassiveSkillId = passiveSkillId;
    }

    public Guid PalInstanceId { get; private set; }
    public Guid PassiveSkillId { get; private set; }
}
