namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsModelTests
{
    [Fact]
    public void Model_enforces_concurrency_scope_and_lifecycle_constraints()
    {
        using DataRightsDbContext dbContext = CreateDbContext(
            $"data-rights-model-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot(),
            "tenant-a");
        IEntityType entity = dbContext.Model.FindEntityType(typeof(DataRightsCase))!;
        IEntityType designEntity = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(DataRightsCase))!;

        Assert.True(entity.FindProperty(nameof(DataRightsCase.Version))!.IsConcurrencyToken);
        Assert.Equal(
            DataRightsCase.ActorIdMaxLength,
            entity.FindProperty(nameof(DataRightsCase.CreatedBy))!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(item => item.Name)
            .SequenceEqual([
                nameof(DataRightsCase.ScopeId),
                nameof(DataRightsCase.PropertyId),
                nameof(DataRightsCase.Status),
                nameof(DataRightsCase.CreatedAtUtc),
                nameof(DataRightsCase.Id)
            ]));
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_property_scope");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_operations");
        Assert.Contains(
            designEntity.GetCheckConstraints(),
            constraint => constraint.Name == "CK_data_rights_cases_requester_scope");
    }

    [Fact]
    public async Task Scope_filter_hides_cases_from_another_tenant()
    {
        string databaseName = $"data-rights-scope-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateCase("tenant-a", propertyId);

        await using (DataRightsDbContext tenantA = CreateDbContext(
            databaseName,
            root,
            "tenant-a"))
        {
            tenantA.Cases.Add(dataRightsCase);
            await tenantA.SaveChangesAsync();
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            "tenant-b");
        Assert.Empty(await tenantB.Cases.ToArrayAsync());
    }

    private static DataRightsCase CreateCase(string tenantId, Guid propertyId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "user:operator",
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)).Value;
    }

    private static DataRightsDbContext CreateDbContext(
        string databaseName,
        InMemoryDatabaseRoot root,
        string tenantId)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseInMemoryDatabase(databaseName, root)
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
