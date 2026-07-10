namespace Properties.Tests;

using Properties.Domain.Aggregates;
using Properties.Domain.Entities;
using Properties.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesModelTests
{
    [Fact]
    public void Room_model_has_scope_aware_property_foreign_key()
    {
        using PropertiesDbContext dbContext = CreateDbContext();

        IEntityType roomEntity = dbContext.Model.FindEntityType(typeof(Room))!;
        IEntityType propertyEntity = dbContext.Model.FindEntityType(typeof(Property))!;
        IForeignKey foreignKey = Assert.Single(roomEntity.GetForeignKeys(), candidate => candidate.PrincipalEntityType == propertyEntity);

        Assert.Equal(["ScopeId", "PropertyId"], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(["ScopeId", "Id"], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Topology_versions_are_concurrency_tokens_and_cursor_is_generated()
    {
        using PropertiesDbContext dbContext = CreateDbContext();

        IEntityType propertyEntity = dbContext.Model.FindEntityType(typeof(Property))!;
        IEntityType roomEntity = dbContext.Model.FindEntityType(typeof(Room))!;
        IEntityType bedEntity = dbContext.Model.FindEntityType(typeof(Bed))!;

        Assert.True(propertyEntity.FindProperty(nameof(Property.Version))!.IsConcurrencyToken);
        Assert.True(roomEntity.FindProperty(nameof(Room.Version))!.IsConcurrencyToken);
        Assert.True(bedEntity.FindProperty(nameof(Bed.Version))!.IsConcurrencyToken);
        Assert.Equal(
            ValueGenerated.OnAdd,
            propertyEntity.FindProperty(nameof(Property.ProjectionOrdinal))!.ValueGenerated);
        Assert.Contains(
            propertyEntity.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Property.ProjectionOrdinal)]));
    }

    private static PropertiesDbContext CreateDbContext()
    {
        DbContextOptions<PropertiesDbContext> options = new DbContextOptionsBuilder<PropertiesDbContext>()
            .UseInMemoryDatabase($"properties-model-{Guid.NewGuid():N}")
            .Options;

        return new PropertiesDbContext(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
