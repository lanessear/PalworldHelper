using Microsoft.EntityFrameworkCore;
using PalworldHelper.Core.Domain;

namespace PalworldHelper.Data.Persistence;

public sealed class PalworldHelperDbContext(DbContextOptions<PalworldHelperDbContext> options) : DbContext(options)
{
    public DbSet<PalSpecies> PalSpecies => Set<PalSpecies>();
    public DbSet<PassiveSkill> PassiveSkills => Set<PassiveSkill>();
    public DbSet<BreedingRecipe> BreedingRecipes => Set<BreedingRecipe>();
    public DbSet<PlayerCollection> PlayerCollections => Set<PlayerCollection>();
    public DbSet<PalInstance> PalInstances => Set<PalInstance>();
    public DbSet<ServerProfile> ServerProfiles => Set<ServerProfile>();
    public DbSet<ReferenceDataSet> ReferenceDataSets => Set<ReferenceDataSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PalSpecies>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(128);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.OwnsMany(x => x.Elements, owned =>
            {
                owned.WithOwner().HasForeignKey(x => x.PalSpeciesId);
                owned.HasKey(x => new { x.PalSpeciesId, x.Element });
            });
        });

        modelBuilder.Entity<PassiveSkill>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(128);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<BreedingRecipe>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DataSetId, x.ParentAId, x.ParentBId, x.ChildId }).IsUnique();
        });

        modelBuilder.Entity<PlayerCollection>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServerProfileId, x.PlayerId }).IsUnique();
            entity.Property(x => x.PlayerId).HasMaxLength(128);
            entity.Property(x => x.PlayerName).HasMaxLength(200);
            entity.HasMany(x => x.Pals).WithOne().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PalInstance>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CollectionId, x.SourceInstanceId }).IsUnique();
            entity.Property(x => x.SourceInstanceId).HasMaxLength(200);
            entity.OwnsOne(x => x.Talents);
            entity.OwnsMany(x => x.Passives, owned =>
            {
                owned.WithOwner().HasForeignKey(x => x.PalInstanceId);
                owned.HasKey(x => new { x.PalInstanceId, x.PassiveSkillId });
            });
        });

        modelBuilder.Entity<ServerProfile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Host).HasMaxLength(255);
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.RemoteSavePath).HasMaxLength(1000);
        });

        modelBuilder.Entity<ReferenceDataSet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.GameVersion, x.SchemaVersion }).IsUnique();
            entity.Property(x => x.GameVersion).HasMaxLength(100);
        });
    }
}
