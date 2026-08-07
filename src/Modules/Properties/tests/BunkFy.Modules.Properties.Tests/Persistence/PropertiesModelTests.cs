namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Persistence;
using BunkFy.Modules.Properties.Persistence.TenantTermination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

    [Fact]
    public void Operation_locks_are_scope_unique_and_concurrency_guarded()
    {
        using PropertiesDbContext dbContext = CreateDbContext();
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;

        AssertOperationLock(
            designModel.FindEntityType(typeof(PropertyOperationLock))!,
            nameof(PropertyOperationLock.PropertyId),
            "CK_property_operation_locks_coordinate",
            "CK_property_operation_locks_revision");
        AssertOperationLock(
            designModel.FindEntityType(typeof(RoomOperationLock))!,
            nameof(RoomOperationLock.RoomId),
            "CK_room_operation_locks_coordinate",
            "CK_room_operation_locks_revision");
    }

    [Fact]
    public void Property_mutation_operations_are_scoped_constrained_and_cascade()
    {
        using PropertiesDbContext dbContext = CreateDbContext();
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType operation = designModel.FindEntityType(
            typeof(PropertyMutationOperation))!;
        IEntityType property = designModel.FindEntityType(typeof(Property))!;

        Assert.Equal(
            ["ScopeId", "ResourceKind", "ResourceId", "Id"],
            operation.FindPrimaryKey()!.Properties.Select(
                item => item.Name));
        Assert.NotEmpty(operation.GetDeclaredQueryFilters());
        IForeignKey foreignKey = Assert.Single(
            operation.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType == property);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(
            ["ScopeId", "PropertyId"],
            foreignKey.Properties.Select(item => item.Name));
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_property_mutation_operations_fingerprint");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_property_mutation_operations_versions");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_property_mutation_operations_kind" &&
                constraint.Sql == "\"Kind\" IN (1, 2, 3, 4, 5, 6)");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_property_mutation_operations_resource");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_property_mutation_operations_status");
        Assert.True(operation.FindProperty("ResultStatus")!.IsNullable);
        Assert.True(operation.FindProperty("ResultRoomId")!.IsNullable);
        Assert.False(
            operation.FindProperty("ResultResourceVersion")!.IsNullable);
    }

    [Fact]
    public void Tenant_revision_is_scope_keyed_and_concurrency_guarded()
    {
        using PropertiesDbContext dbContext = CreateDbContext();

        IEntityType revisionEntity = dbContext.Model.FindEntityType(
            typeof(PropertiesTenantRevision))!;
        IEntityType designRevisionEntity = dbContext
            .GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(PropertiesTenantRevision))!;

        Assert.Equal(
            [nameof(PropertiesTenantRevision.ScopeId)],
            revisionEntity.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(
            revisionEntity.FindProperty(
                nameof(PropertiesTenantRevision.Revision))!
                .IsConcurrencyToken);
        Assert.NotEmpty(revisionEntity.GetDeclaredQueryFilters());
        Assert.Contains(
            designRevisionEntity.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_properties_tenant_revision_positive",
                StringComparison.Ordinal));
        Assert.Contains(
            designRevisionEntity.GetCheckConstraints(),
            constraint => string.Equals(
                constraint.Name,
                "CK_properties_tenant_revision_lifecycle",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Tenant_destruction_progress_and_receipt_are_scope_unique_and_constrained()
    {
        using PropertiesDbContext dbContext = CreateDbContext();
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType operation = designModel.FindEntityType(
            typeof(PropertiesTenantDestroyOperation))!;
        IEntityType receipt = designModel.FindEntityType(
            typeof(PropertiesTenantDestroyReceipt))!;

        Assert.Equal(
            [nameof(PropertiesTenantDestroyOperation.OperationId)],
            operation.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(operation.FindProperty(
            nameof(PropertiesTenantDestroyOperation.ConcurrencyVersion))!
            .IsConcurrencyToken);
        Assert.Contains(
            operation.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(PropertiesTenantDestroyOperation.ScopeId)
                    ]));
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_tenant_destroy_operation_batch");
        Assert.Contains(
            operation.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_tenant_destroy_operation_progress");

        Assert.Equal(
            [nameof(PropertiesTenantDestroyReceipt.OperationId)],
            receipt.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(PropertiesTenantDestroyReceipt.ScopeId)
                    ]));
        Assert.Contains(
            receipt.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_properties_tenant_destroy_receipt_progress");
    }

    private static PropertiesDbContext CreateDbContext()
    {
        DbContextOptions<PropertiesDbContext> options = new DbContextOptionsBuilder<PropertiesDbContext>()
            .UseInMemoryDatabase($"properties-model-{Guid.NewGuid():N}")
            .Options;

        return new PropertiesDbContext(options, new TestScopeContext());
    }

    private static void AssertOperationLock(
        IEntityType entity,
        string resourceProperty,
        string coordinateConstraint,
        string revisionConstraint)
    {
        Assert.Equal(
            ["Id"],
            entity.FindPrimaryKey()!.Properties.Select(
                property => property.Name));
        Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.Contains(
            entity.GetKeys(),
            key => key.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "Id"]));
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(["ScopeId", resourceProperty]));
        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => constraint.Name == coordinateConstraint);
        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => constraint.Name == revisionConstraint);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
