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
public sealed class TenantTerminationExportFragmentPersistenceTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private static readonly string Digest = new('a', 64);
    private static readonly string FrozenDigest = new('b', 64);
    private static readonly string FragmentSetDigest = new('c', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Model_enforces_fragment_identity_lifecycle_and_owner_work_binding()
    {
        using DataRightsDbContext dbContext = CreateDbContext(
            $"tenant-export-fragment-model-{Guid.NewGuid():N}",
            new InMemoryDatabaseRoot(),
            TenantA);
        IModel designModel = dbContext.GetService<IDesignTimeModel>().Model;
        IEntityType fragment = designModel.FindEntityType(
            typeof(TenantTerminationExportFragment))!;
        IEntityType artifact = designModel.FindEntityType(
            typeof(TenantTerminationExportArtifact))!;
        IEntityType process = designModel.FindEntityType(
            typeof(TenantTerminationProcess))!;

        Assert.Equal(
            "tenant_termination_export_fragments",
            fragment.GetTableName());
        Assert.True(fragment.FindProperty(
            nameof(TenantTerminationExportFragment.Version))!
            .IsConcurrencyToken);
        Assert.Contains(
            fragment.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationExportFragment.ScopeId),
                        nameof(TenantTerminationExportFragment.IdempotencyKey)
                    ]));
        Assert.Contains(
            fragment.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationExportFragment.ScopeId),
                        nameof(TenantTerminationExportFragment.ProcessId),
                        nameof(
                            TenantTerminationExportFragment
                                .ExportOperationRevision),
                        nameof(TenantTerminationExportFragment.OwnerKey)
                    ]));
        Assert.Contains(
            fragment.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_data_rights_tenant_termination_export_fragment_lifecycle");
        Assert.Contains(
            fragment.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_data_rights_tenant_termination_export_fragment_proof");
        Assert.Contains(
            fragment.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_data_rights_tenant_termination_export_fragment_storage");
        Assert.Contains(
            fragment.GetForeignKeys(),
            foreignKey =>
                foreignKey.IsUnique &&
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.PrincipalEntityType.ClrType ==
                    typeof(TenantTerminationOwnerWorkItem) &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationExportFragment.ScopeId),
                        nameof(TenantTerminationExportFragment.Id)
                    ]));

        Assert.Equal(
            "tenant_termination_export_artifacts",
            artifact.GetTableName());
        Assert.True(artifact.FindProperty(
            nameof(TenantTerminationExportArtifact.Version))!
            .IsConcurrencyToken);
        Assert.Contains(
            artifact.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(TenantTerminationExportArtifact.ScopeId),
                        nameof(TenantTerminationExportArtifact.ProcessId),
                        nameof(
                            TenantTerminationExportArtifact
                                .ExportOperationRevision)
                    ]));
        Assert.Contains(
            artifact.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_data_rights_tenant_termination_export_artifact_lifecycle");
        Assert.Contains(
            artifact.GetForeignKeys(),
            foreignKey =>
                !foreignKey.IsUnique &&
                foreignKey.DeleteBehavior == DeleteBehavior.Restrict &&
                foreignKey.PrincipalEntityType.ClrType ==
                    typeof(TenantTerminationProcess));
        Assert.Contains(
            process.GetCheckConstraints(),
            constraint => constraint.Name ==
                "CK_data_rights_tenant_termination_process_export_confirmation");
    }

    [Fact]
    public async Task Repository_is_tenant_scoped_and_orders_fragments_by_owner()
    {
        string databaseName =
            $"tenant-export-fragment-repository-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid processId;
        Guid workspacesId;
        Guid workspacesIdempotencyKey;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            TenantA))
        {
            DataRightsCase dataRightsCase = CreateTenantTerminationCase(TenantA);
            TenantTerminationProcess process = PrepareProcess(
                dataRightsCase);
            processId = process.Id;
            TenantTerminationRepository processRepository = new(writer);
            TenantTerminationExportFragmentRepository fragmentRepository =
                new(writer);
            TenantTerminationOwnerWorkItem inventory = PrepareOwnerWork(
                process,
                "inventory");
            TenantTerminationOwnerWorkItem workspaces = PrepareOwnerWork(
                process,
                "workspaces");
            workspacesId = workspaces.Id;
            workspacesIdempotencyKey = workspaces.IdempotencyKey;

            writer.Cases.Add(dataRightsCase);
            await processRepository.AddProcessAsync(process, CancellationToken.None);
            await processRepository.AddOwnerWorkItemAsync(
                workspaces,
                CancellationToken.None);
            await processRepository.AddOwnerWorkItemAsync(
                inventory,
                CancellationToken.None);
            await fragmentRepository.AddAsync(
                PrepareFragment(process, workspaces),
                CancellationToken.None);
            await fragmentRepository.AddAsync(
                PrepareFragment(process, inventory),
                CancellationToken.None);
            await writer.SaveChangesAsync();

            Assert.Equal(
                workspacesId,
                (await fragmentRepository.GetAsync(
                    workspacesId,
                    CancellationToken.None))!.Id);
            Assert.Equal(
                workspacesId,
                (await fragmentRepository.GetByIdempotencyKeyAsync(
                    workspacesIdempotencyKey,
                    CancellationToken.None))!.Id);
            Assert.Equal(
                ["inventory", "workspaces"],
                (await fragmentRepository.ListAsync(
                    processId,
                    exportOperationRevision: 2,
                    CancellationToken.None))
                    .Select(fragment => fragment.OwnerKey)
                    .ToArray());
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            TenantB);
        TenantTerminationExportFragmentRepository tenantBRepository =
            new(tenantB);
        Assert.Null(await tenantBRepository.GetAsync(
            workspacesId,
            CancellationToken.None));
        Assert.Null(await tenantBRepository.GetByIdempotencyKeyAsync(
            workspacesIdempotencyKey,
            CancellationToken.None));
        Assert.Empty(await tenantBRepository.ListAsync(
            processId,
            exportOperationRevision: 2,
            CancellationToken.None));
    }

    [Fact]
    public async Task Final_artifact_repository_is_tenant_scoped_and_process_bound()
    {
        string databaseName =
            $"tenant-export-artifact-repository-{Guid.NewGuid():N}";
        InMemoryDatabaseRoot root = new();
        Guid processId;
        Guid artifactId;
        Guid idempotencyKey;

        await using (DataRightsDbContext writer = CreateDbContext(
            databaseName,
            root,
            TenantA))
        {
            DataRightsCase dataRightsCase = CreateTenantTerminationCase(TenantA);
            TenantTerminationProcess process = PrepareProcess(
                dataRightsCase);
            TenantTerminationExportArtifact artifact = PrepareArtifact(process);
            processId = process.Id;
            artifactId = artifact.Id;
            idempotencyKey = artifact.IdempotencyKey;
            TenantTerminationRepository processRepository = new(writer);
            TenantTerminationExportArtifactRepository artifactRepository =
                new(writer);

            writer.Cases.Add(dataRightsCase);
            await processRepository.AddProcessAsync(process, CancellationToken.None);
            await artifactRepository.AddAsync(artifact, CancellationToken.None);
            await writer.SaveChangesAsync();

            Assert.Equal(
                artifactId,
                (await artifactRepository.GetAsync(
                    artifactId,
                    CancellationToken.None))!.Id);
            Assert.Equal(
                artifactId,
                (await artifactRepository.GetByIdempotencyKeyAsync(
                    idempotencyKey,
                    CancellationToken.None))!.Id);
            Assert.Equal(
                artifactId,
                (await artifactRepository.GetByProcessAsync(
                    processId,
                    exportOperationRevision: 2,
                    CancellationToken.None))!.Id);
        }

        await using DataRightsDbContext tenantB = CreateDbContext(
            databaseName,
            root,
            TenantB);
        TenantTerminationExportArtifactRepository tenantBRepository =
            new(tenantB);
        Assert.Null(await tenantBRepository.GetAsync(
            artifactId,
            CancellationToken.None));
        Assert.Null(await tenantBRepository.GetByIdempotencyKeyAsync(
            idempotencyKey,
            CancellationToken.None));
        Assert.Null(await tenantBRepository.GetByProcessAsync(
            processId,
            exportOperationRevision: 2,
            CancellationToken.None));
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
            exportRequested: true,
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
            exportRequested: true,
            Digest,
            "owner:approver",
            Now.AddMinutes(2),
            "system:tenant-termination",
            Now.AddMinutes(3)).Value;

    private static TenantTerminationOwnerWorkItem PrepareOwnerWork(
        TenantTerminationProcess process,
        string ownerKey) =>
        TenantTerminationOwnerWorkItem.Prepare(
            Guid.NewGuid(),
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            operationRevision: 2,
            process.TerminationEpoch,
            Guid.NewGuid(),
            TenantTerminationOwnerPhase.Export,
            ownerKey,
            ownerContractVersion: 1,
            catalogVersion: 1,
            Digest,
            process.PolicyEvidenceSha256,
            Now).Value;

    private static TenantTerminationExportFragment PrepareFragment(
        TenantTerminationProcess process,
        TenantTerminationOwnerWorkItem workItem) =>
        TenantTerminationExportFragment.Request(
            workItem.Id,
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            freezeOperationRevision: 1,
            exportOperationRevision: workItem.OperationRevision,
            process.TerminationEpoch,
            workItem.IdempotencyKey,
            workItem.OwnerKey,
            workItem.OwnerContractVersion,
            workItem.CatalogVersion,
            workItem.CatalogSha256,
            FrozenDigest,
            process.PolicyEvidenceSha256,
            Now,
            Now.AddHours(24)).Value;

    private static TenantTerminationExportArtifact PrepareArtifact(
        TenantTerminationProcess process) =>
        TenantTerminationExportArtifact.Request(
            Guid.NewGuid(),
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            freezeOperationRevision: 1,
            exportOperationRevision: 2,
            process.TerminationEpoch,
            Guid.NewGuid(),
            FrozenDigest,
            process.PolicyEvidenceSha256,
            expectedFragmentCount: 2,
            FragmentSetDigest,
            Now,
            Now.AddHours(24)).Value;

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
