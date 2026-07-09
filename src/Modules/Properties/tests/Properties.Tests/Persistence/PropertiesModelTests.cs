namespace Properties.Tests;

using Properties.Domain.Aggregates;
using Properties.Persistence;
using Gma.Framework.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesModelTests
{
    [Fact]
    public void Room_model_has_tenant_aware_property_foreign_key()
    {
        using PropertiesDbContext dbContext = CreateDbContext();

        IEntityType roomEntity = dbContext.Model.FindEntityType(typeof(Room))!;
        IEntityType propertyEntity = dbContext.Model.FindEntityType(typeof(Property))!;
        IForeignKey foreignKey = Assert.Single(roomEntity.GetForeignKeys(), candidate => candidate.PrincipalEntityType == propertyEntity);

        Assert.Equal(["TenantId", "PropertyId"], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(["TenantId", "Id"], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static PropertiesDbContext CreateDbContext()
    {
        DbContextOptions<PropertiesDbContext> options = new DbContextOptionsBuilder<PropertiesDbContext>()
            .UseInMemoryDatabase($"properties-model-{Guid.NewGuid():N}")
            .Options;

        return new PropertiesDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public bool IsEnabled => true;
        public string TenantId => "tenant-a";
    }
}
