namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.DataRights.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationPersistenceTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Model_enforces_tenant_lifecycle_and_owner_work_invariants()
    {
        using DataRightsDbContext dbContext = CreateDbContext(
            $"tenant-termination-model-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot(),
            TenantA);
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType process =
            designModel.FindEntityType(typeof(TenantTerminationProcess))!;
        IEntityType ownerWork =
            designModel.FindEntityType(typeof(TenantTerminationOwnerWorkItem))!;

        Assert.True(process.FindProperty(
            nameof(TenantTerminationProcess.Version))!.IsConcurrencyToken);
        Assert.Contains(
            process.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationProcess.ScopeId)
                    ]) &&
                index.GetFilter() == "\"Status\" IN (1, 2, 3, 4)");
        Assert.Contains(
            process.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationProcess.ScopeId),
                        nameof(TenantTerminationProcess.IdempotencyKey)
                    ]));
        Assert.Contains(
            process.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_process_completion");
        Assert.Contains(
            process.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationProcess.ScopeId),
                        nameof(TenantTerminationProcess.CaseId)
                    ]));

        Assert.True(ownerWork.FindProperty(
            nameof(TenantTerminationOwnerWorkItem.Version))!.IsConcurrencyToken);
        Assert.Contains(
            ownerWork.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationOwnerWorkItem.ScopeId),
                        nameof(TenantTerminationOwnerWorkItem.ProcessId),
                        nameof(TenantTerminationOwnerWorkItem.Phase),
                        nameof(TenantTerminationOwnerWorkItem.OwnerKey),
                        nameof(TenantTerminationOwnerWorkItem.OperationRevision)
                    ]));
        Assert.Contains(
            ownerWork.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_owner_result");
        Assert.Contains(
            ownerWork.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationOwnerWorkItem.ScopeId),
                        nameof(TenantTerminationOwnerWorkItem.ProcessId),
                        nameof(TenantTerminationOwnerWorkItem.CaseId),
                        nameof(TenantTerminationOwnerWorkItem.ApprovalRevision),
                        nameof(TenantTerminationOwnerWorkItem.TerminationEpoch),
                        nameof(
                            TenantTerminationOwnerWorkItem
                                .PolicyEvidenceSha256)
                    ]));
    }

    [Fact]
    public async Task Repository_is_tenant_scoped_and_orders_owner_work()
    {
        string databaseName = $"tenant-termination-repository-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid processId;
        Guid idempotencyKey;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            TenantA))
        {
            DataRightsCase dataRightsCase = CreateTenantTerminationCase(TenantA);
            TenantTerminationProcess process = PrepareProcess(
                TenantA,
                dataRightsCase.Id);
            processId = process.Id;
            idempotencyKey = process.IdempotencyKey;
            TenantTerminationRepository repository = new(writer);

            writer.Cases.Add(dataRightsCase);
            await repository.AddProcessAsync(process, CancellationToken.None);
            await repository.AddOwnerWorkItemAsync(
                PrepareOwnerWork(process, "workspaces", Guid.NewGuid()),
                CancellationToken.None);
            await repository.AddOwnerWorkItemAsync(
                PrepareOwnerWork(process, "access-control", Guid.NewGuid()),
                CancellationToken.None);
            await writer.SaveChangesAsync();

            Assert.Equal(
                processId,
                (await repository.GetActiveProcessAsync(
                    CancellationToken.None))!.Id);
            Assert.Equal(
                processId,
                (await repository.GetProcessByIdempotencyKeyAsync(
                    idempotencyKey,
                    CancellationToken.None))!.Id);
            Assert.Equal(
                ["access-control", "workspaces"],
                (await repository.ListOwnerWorkItemsAsync(
                    processId,
                    TenantTerminationOwnerPhase.Freeze,
                    operationRevision: 1,
                    CancellationToken.None))
                    .Select(item => item.OwnerKey)
                    .ToArray());
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            TenantB);
        TenantTerminationRepository tenantBRepository = new(tenantB);
        Assert.Null(await tenantBRepository.GetProcessAsync(
            processId,
            CancellationToken.None));
        Assert.Null(await tenantBRepository.GetActiveProcessAsync(
            CancellationToken.None));
        Assert.Empty(await tenantBRepository.ListOwnerWorkItemsAsync(
            processId,
            TenantTerminationOwnerPhase.Freeze,
            operationRevision: 1,
            CancellationToken.None));
    }

    private static DataRightsCase CreateTenantTerminationCase(string tenantId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.TenantOwner).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "owner:approver",
            Now).Value;
    }

    private static TenantTerminationProcess PrepareProcess(
        string tenantId,
        Guid caseId) =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            caseId,
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "owner:approver",
            Now,
            "system:tenant-termination",
            Now).Value;

    private static TenantTerminationOwnerWorkItem PrepareOwnerWork(
        TenantTerminationProcess process,
        string ownerKey,
        Guid idempotencyKey) =>
        TenantTerminationOwnerWorkItem.Prepare(
            Guid.NewGuid(),
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            operationRevision: 1,
            process.TerminationEpoch,
            idempotencyKey,
            TenantTerminationOwnerPhase.Freeze,
            ownerKey,
            ownerContractVersion: 1,
            catalogVersion: 1,
            Digest,
            process.PolicyEvidenceSha256,
            Now).Value;

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
