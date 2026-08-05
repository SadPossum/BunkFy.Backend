namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
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
        IEntityType frozenOwner =
            designModel.FindEntityType(typeof(TenantTerminationFrozenOwner))!;
        IEntityType terminalReceipt = designModel.FindEntityType(
            typeof(TenantTerminationTerminalReceipt))!;

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
            process.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_process_destroy_checkpoint");
        Assert.Contains(
            process.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_process_verification_confirmation");
        Assert.Contains(
            process.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationProcess.ScopeId),
                        nameof(TenantTerminationProcess.CaseId)
                    ]));
        Assert.Contains(
            process.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationProcess.ScopeId),
                        nameof(TenantTerminationProcess.Id),
                        nameof(TenantTerminationProcess.TerminalReceiptId),
                        nameof(
                            TenantTerminationProcess.TerminalReceiptVersion)
                    ]) &&
                foreignKey.PrincipalKey.Properties
                    .Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationTerminalReceipt.ScopeId),
                        nameof(TenantTerminationTerminalReceipt.ProcessId),
                        nameof(TenantTerminationTerminalReceipt.Id),
                        nameof(TenantTerminationTerminalReceipt.Version)
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

        Assert.True(frozenOwner.IsOwned());
        Assert.Equal(
            "tenant_termination_frozen_export_owners",
            frozenOwner.GetTableName());
        Assert.Contains(
            frozenOwner.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        "ProcessId",
                        nameof(TenantTerminationFrozenOwner.OwnerKey)
                    ]));
        Assert.Contains(
            frozenOwner.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_frozen_owner_catalog");

        Assert.True(terminalReceipt.FindProperty(
            nameof(TenantTerminationTerminalReceipt.Version))!
            .IsConcurrencyToken);
        Assert.Contains(
            terminalReceipt.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationTerminalReceipt.ScopeId),
                        nameof(
                            TenantTerminationTerminalReceipt.ProcessId),
                        nameof(
                            TenantTerminationTerminalReceipt
                                .VerificationOperationRevision)
                    ]));
        Assert.Contains(
            terminalReceipt.GetKeys(),
            key => key.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(TenantTerminationTerminalReceipt.ScopeId),
                    nameof(TenantTerminationTerminalReceipt.ProcessId),
                    nameof(TenantTerminationTerminalReceipt.Id),
                    nameof(TenantTerminationTerminalReceipt.Version)
                ]));
        Assert.Contains(
            terminalReceipt.GetCheckConstraints(),
            constraint =>
                constraint.Name ==
                "CK_data_rights_tenant_termination_terminal_receipt_replay");
        Assert.Contains(
            terminalReceipt.GetForeignKeys(),
            foreignKey =>
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationTerminalReceipt.ScopeId),
                        nameof(TenantTerminationTerminalReceipt.ProcessId),
                        nameof(TenantTerminationTerminalReceipt.CaseId),
                        nameof(
                            TenantTerminationTerminalReceipt
                                .ApprovalRevision),
                        nameof(
                            TenantTerminationTerminalReceipt
                                .TerminationEpoch),
                        nameof(
                            TenantTerminationTerminalReceipt
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
                dataRightsCase);
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

    [Fact]
    public async Task Repository_round_trips_the_immutable_freeze_catalog()
    {
        string databaseName =
            $"tenant-termination-freeze-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid processId;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            TenantA))
        {
            DataRightsCase dataRightsCase = CreateTenantTerminationCase(TenantA);
            TenantTerminationProcess process = PrepareProcess(
                dataRightsCase);
            processId = process.Id;
            Assert.True(process.BeginPhase(
                TenantTerminationProcessPhase.Freeze,
                process.Version,
                "system:tenant-termination",
                Now.AddMinutes(4)).IsSuccess);
            Assert.True(process.CompleteFreeze(
                process.OperationRevision,
                workspaceFenceRevision: 3,
                Digest,
                [
                    new("workspaces", 1, 2, new string('c', 64)),
                    new("reservations", 1, 4, new string('b', 64))
                ],
                process.Version,
                "system:tenant-termination",
                Now.AddMinutes(5)).IsSuccess);

            writer.Cases.Add(dataRightsCase);
            writer.TenantTerminationProcesses.Add(process);
            await writer.SaveChangesAsync();
        }

        await using DataRightsDbContext reader = CreateDbContext(
            databaseName,
            root,
            TenantA);
        TenantTerminationProcess restored =
            (await new TenantTerminationRepository(reader).GetProcessAsync(
                processId,
                CancellationToken.None))!;

        Assert.Equal(3, restored.WorkspaceFenceRevision);
        Assert.Equal(Digest, restored.FrozenRevisionSha256);
        Assert.Equal(
            ["reservations", "workspaces"],
            restored.FrozenExportOwners
                .OrderBy(owner => owner.Ordinal)
                .Select(owner => owner.OwnerKey)
                .ToArray());
        Assert.Equal([1, 2], restored.FrozenExportOwners
            .OrderBy(owner => owner.Ordinal)
            .Select(owner => owner.Ordinal)
            .ToArray());
    }

    private static DataRightsCase CreateTenantTerminationCase(string tenantId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            tenantId,
            request,
            "owner:approver",
            Now).Value;
        Assert.True(dataRightsCase.PrepareTenantTerminationReview(
            exportRequested: false,
            dataRightsCase.Version,
            "owner:approver",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            Digest,
            dataRightsCase.Version,
            "owner:approver",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.BeginTenantTerminationExecution(
            dataRightsCase.Version,
            "system:tenant-termination",
            Now.AddMinutes(3)).IsSuccess);
        return dataRightsCase;
    }

    private static TenantTerminationProcess PrepareProcess(
        DataRightsCase dataRightsCase) =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            dataRightsCase.ScopeId,
            Guid.NewGuid(),
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "owner:approver",
            Now.AddMinutes(2),
            "system:tenant-termination",
            Now.AddMinutes(3)).Value;

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
