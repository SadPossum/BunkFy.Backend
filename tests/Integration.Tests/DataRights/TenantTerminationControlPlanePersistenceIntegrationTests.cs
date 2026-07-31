namespace Integration.Tests;

using System.Text.Json;
using BunkFy.Host.Worker;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Scoping;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using ContractFenceState =
    BunkFy.Modules.Workspaces.Contracts.WorkspaceTerminationFenceState;
using DomainFenceState =
    BunkFy.Modules.Workspaces.Domain.Termination.WorkspaceTerminationFenceState;

public sealed class TenantTerminationControlPlanePersistenceIntegrationTests
{
    private const string BeforeWorkspaceFenceMigration =
        "20260730165414_AddWorkspaceStaffCorrelationAnonymisationProof";
    private const string CoordinatorFoundationMigration =
        "20260731052813_AddTenantTerminationCoordinatorFoundation";
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Fence_and_cancellation_survive_relational_boundaries()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_tenant_termination_control_plane")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();

        await ApplyMigrationsAsync(connectionString);

        Guid firstProcessId = Guid.NewGuid();
        Guid firstEpoch = Guid.NewGuid();
        WorkspaceTerminationFence firstFence = CreateFence(
            TenantA,
            firstProcessId,
            firstEpoch);
        WorkspaceTerminationFenceReceipt firstReceipt = CreateFreezeReceipt(
            firstFence,
            operationRevision: 1);
        await using (WorkspacesDbContext tenantA = CreateWorkspacesDbContext(
            connectionString,
            TenantA))
        {
            tenantA.WorkspaceTerminationFences.Add(firstFence);
            tenantA.WorkspaceTerminationFenceReceipts.Add(firstReceipt);
            await tenantA.SaveChangesAsync();
        }

        WorkspaceTerminationFence tenantBFence = CreateFence(
            TenantB,
            firstProcessId,
            firstEpoch);
        await using (WorkspacesDbContext tenantB = CreateWorkspacesDbContext(
            connectionString,
            TenantB))
        {
            tenantB.WorkspaceTerminationFences.Add(tenantBFence);
            tenantB.WorkspaceTerminationFenceReceipts.Add(
                CreateFreezeReceipt(tenantBFence, operationRevision: 1));
            await tenantB.SaveChangesAsync();
        }

        await AssertWorkerAdmissionAsync(
            connectionString,
            firstProcessId);

        await using (WorkspacesDbContext duplicateActive =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            duplicateActive.WorkspaceTerminationFences.Add(
                CreateFence(TenantA, Guid.NewGuid(), Guid.NewGuid()));
            await AssertUniqueViolationAsync(
                () => duplicateActive.SaveChangesAsync());
        }

        await using (WorkspacesDbContext release =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            WorkspaceTerminationFence persisted =
                await release.WorkspaceTerminationFences.SingleAsync();
            Assert.True(persisted.Release(
                persisted.Version,
                "system:tenant-termination",
                Now.AddMinutes(1)).IsSuccess);
            release.WorkspaceTerminationFenceReceipts.Add(
                CreateReleaseReceipt(
                    persisted,
                    operationRevision: 2,
                    completedAtUtc: Now.AddMinutes(1)));
            await release.SaveChangesAsync();
        }

        await using (WorkspacesDbContext reusedEpoch =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            reusedEpoch.WorkspaceTerminationFences.Add(
                CreateFence(TenantA, Guid.NewGuid(), firstEpoch));
            await AssertUniqueViolationAsync(
                () => reusedEpoch.SaveChangesAsync());
        }

        Guid secondProcessId = Guid.NewGuid();
        WorkspaceTerminationFence secondFence = CreateFence(
            TenantA,
            secondProcessId,
            Guid.NewGuid());
        await using (WorkspacesDbContext nextProcess =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            nextProcess.WorkspaceTerminationFences.Add(secondFence);
            nextProcess.WorkspaceTerminationFenceReceipts.Add(
                CreateFreezeReceipt(secondFence, operationRevision: 1));
            await nextProcess.SaveChangesAsync();
        }

        await AssertCurrentFenceAsync(
            connectionString,
            secondProcessId);
        await AssertTerminationEvidenceIsImmutableAsync(
            connectionString,
            firstFence.Id,
            firstReceipt.Id);
        await AssertCancellationPersistenceAsync(connectionString);
    }

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        await using (WorkspacesDbContext workspaces =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            IMigrator migrator =
                workspaces.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(BeforeWorkspaceFenceMigration);
            await workspaces.Database.MigrateAsync();
            Assert.Empty(
                await workspaces.Database.GetPendingMigrationsAsync());
        }

        await using DataRightsDbContext dataRights =
            CreateDataRightsDbContext(connectionString, TenantA);
        IMigrator dataRightsMigrator =
            dataRights.Database.GetService<IMigrator>();
        await dataRightsMigrator.MigrateAsync(
            CoordinatorFoundationMigration);
        await dataRights.Database.MigrateAsync();
        Assert.Empty(
            await dataRights.Database.GetPendingMigrationsAsync());
    }

    private static async Task AssertWorkerAdmissionAsync(
        string connectionString,
        Guid processId)
    {
        await using ServiceProvider provider = CreateWorkspacesProvider(
            connectionString,
            TenantA);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IWorkspaceTerminationFenceReader reader =
            scope.ServiceProvider
                .GetRequiredService<IWorkspaceTerminationFenceReader>();
        WorkspaceTerminationTaskExecutionContextContributor contributor =
            new(
                reader,
                NullLogger<
                    WorkspaceTerminationTaskExecutionContextContributor>
                    .Instance);

        Assert.True((await contributor.PrepareAsync(
            CreateTaskContext(processId),
            default)).IsSuccess);
        Assert.True((await contributor.PrepareAsync(
            CreateTaskContext(Guid.NewGuid()),
            default)).IsFailure);
    }

    private static async Task AssertCurrentFenceAsync(
        string connectionString,
        Guid expectedProcessId)
    {
        await using ServiceProvider provider = CreateWorkspacesProvider(
            connectionString,
            TenantA);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IWorkspaceTerminationFenceReader reader =
            scope.ServiceProvider
                .GetRequiredService<IWorkspaceTerminationFenceReader>();

        WorkspaceTerminationFenceSnapshot snapshot =
            Assert.IsType<WorkspaceTerminationFenceSnapshot>(
                await reader.GetCurrentAsync());
        Assert.Equal(expectedProcessId, snapshot.ProcessId);
        Assert.Equal(ContractFenceState.Frozen, snapshot.State);
        Assert.Equal(1, snapshot.Version);
    }

    private static async Task AssertTerminationEvidenceIsImmutableAsync(
        string connectionString,
        Guid fenceId,
        Guid receiptId)
    {
        await using (WorkspacesDbContext receiptTamper =
            CreateWorkspacesDbContext(connectionString, TenantA))
        {
            PostgresException failure =
                await Assert.ThrowsAsync<PostgresException>(() =>
                    receiptTamper.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE
                            workspaces.workspace_termination_fence_receipts
                        SET "ActorId" = 'tampered'
                        WHERE "Id" = {receiptId};
                        """));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("append-only", failure.MessageText);
        }

        await using WorkspacesDbContext fenceDeletion =
            CreateWorkspacesDbContext(connectionString, TenantA);
        PostgresException deletionFailure =
            await Assert.ThrowsAsync<PostgresException>(() =>
                fenceDeletion.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM workspaces.workspace_termination_fences
                    WHERE "Id" = {fenceId};
                    """));
        Assert.Equal("P0001", deletionFailure.SqlState);
        Assert.Contains(
            "historical evidence",
            deletionFailure.MessageText);
    }

    private static async Task AssertCancellationPersistenceAsync(
        string connectionString)
    {
        DataRightsCase firstCase = CreateCase(TenantA);
        TenantTerminationProcess cancelled = PrepareProcess(
            TenantA,
            firstCase.Id);
        Assert.True(cancelled.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            cancelled.Version,
            "owner:approver",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(cancelled.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            cancelled.OperationRevision,
            cancelled.Version,
            "owner:approver",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(cancelled.RequestCancellation(
            cancelled.Version,
            "owner:approver",
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(cancelled.BeginPhase(
            TenantTerminationProcessPhase.Restore,
            cancelled.Version,
            "owner:approver",
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(cancelled.CompleteCancellation(
            cancelled.OperationRevision,
            cancelled.Version,
            "owner:approver",
            Now.AddMinutes(5)).IsSuccess);

        await using (DataRightsDbContext first =
            CreateDataRightsDbContext(connectionString, TenantA))
        {
            first.Cases.Add(firstCase);
            first.TenantTerminationProcesses.Add(cancelled);
            await first.SaveChangesAsync();
        }

        DataRightsCase secondCase = CreateCase(TenantA);
        TenantTerminationProcess active = PrepareProcess(
            TenantA,
            secondCase.Id);
        await using (DataRightsDbContext second =
            CreateDataRightsDbContext(connectionString, TenantA))
        {
            second.Cases.Add(secondCase);
            second.TenantTerminationProcesses.Add(active);
            await second.SaveChangesAsync();
            Assert.Equal(
                active.Id,
                (await second.TenantTerminationProcesses.SingleAsync(
                    process =>
                        process.Status !=
                            TenantTerminationProcessStatus.Cancelled &&
                        process.Status !=
                            TenantTerminationProcessStatus.Completed)).Id);
        }

        await using DataRightsDbContext invalid =
            CreateDataRightsDbContext(connectionString, TenantA);
        PostgresException constraintFailure =
            await Assert.ThrowsAsync<PostgresException>(() =>
                invalid.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "data-rights"."tenant_termination_processes"
                    SET "Phase" =
                        {(int)TenantTerminationProcessPhase.Destroy}
                    WHERE "Id" = {cancelled.Id};
                    """));
        Assert.Equal(
            PostgresErrorCodes.CheckViolation,
            constraintFailure.SqlState);
    }

    private static WorkspaceTerminationFence CreateFence(
        string tenantId,
        Guid processId,
        Guid terminationEpoch) =>
        WorkspaceTerminationFence.Freeze(
            Guid.NewGuid(),
            tenantId,
            processId,
            Guid.NewGuid(),
            approvalRevision: 1,
            terminationEpoch,
            Digest,
            "system:tenant-termination",
            Now).Value;

    private static WorkspaceTerminationFenceReceipt CreateFreezeReceipt(
        WorkspaceTerminationFence fence,
        long operationRevision) =>
        WorkspaceTerminationFenceReceipt.Create(
            Guid.NewGuid(),
            fence.ScopeId,
            fence.Id,
            fence.ProcessId,
            fence.CaseId,
            fence.ApprovalRevision,
            operationRevision,
            Guid.NewGuid(),
            Guid.NewGuid(),
            fence.TerminationEpoch,
            WorkspaceTerminationFenceAction.Freeze,
            selectedFenceVersion: 0,
            resultingFenceVersion: 1,
            DomainFenceState.Frozen,
            fence.PolicyEvidenceSha256,
            fence.CreatedBy,
            fence.CreatedAtUtc).Value;

    private static WorkspaceTerminationFenceReceipt CreateReleaseReceipt(
        WorkspaceTerminationFence fence,
        long operationRevision,
        DateTimeOffset completedAtUtc) =>
        WorkspaceTerminationFenceReceipt.Create(
            Guid.NewGuid(),
            fence.ScopeId,
            fence.Id,
            fence.ProcessId,
            fence.CaseId,
            fence.ApprovalRevision,
            operationRevision,
            Guid.NewGuid(),
            Guid.NewGuid(),
            fence.TerminationEpoch,
            WorkspaceTerminationFenceAction.Release,
            selectedFenceVersion: fence.Version - 1,
            resultingFenceVersion: fence.Version,
            DomainFenceState.Released,
            fence.PolicyEvidenceSha256,
            fence.LastChangedBy,
            completedAtUtc).Value;

    private static async Task AssertUniqueViolationAsync(
        Func<Task> action)
    {
        DbUpdateException failure =
            await Assert.ThrowsAsync<DbUpdateException>(action);
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            Assert.IsType<PostgresException>(
                failure.GetBaseException()).SqlState);
    }

    private static DataRightsCase CreateCase(string tenantId)
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

    private static ServiceProvider CreateWorkspacesProvider(
        string connectionString,
        string tenantId)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext(tenantId));
        builder.AddWorkspacesTerminationAdmissionPersistence();
        return builder.Services.BuildServiceProvider();
    }

    private static WorkspacesDbContext CreateWorkspacesDbContext(
        string connectionString,
        string tenantId)
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(
                        WorkspacesMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        WorkspacesMigrations.HistoryTable,
                        WorkspacesMigrations.Schema))
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private static DataRightsDbContext CreateDataRightsDbContext(
        string connectionString,
        string tenantId)
    {
        DbContextOptions<DataRightsDbContext> options =
            new DbContextOptionsBuilder<DataRightsDbContext>()
                .UseNpgsql(connectionString, provider => provider
                    .MigrationsAssembly(
                        DataRightsMigrations.PostgreSqlAssembly)
                    .MigrationsHistoryTable(
                        DataRightsMigrations.HistoryTable,
                        DataRightsMigrations.Schema))
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private static TaskExecutionContextPreparationContext CreateTaskContext(
        Guid processId)
    {
        ExecuteTenantTerminationOwnerWorkPayload payload = new(
            processId,
            Guid.NewGuid(),
            1,
            TenantTerminationContributionPhase.Freeze,
            "workspaces");
        TaskHandlerRegistration registration =
            TaskHandlerRegistration.Create<
                ExecuteTenantTerminationOwnerWorkPayload,
                StubHandler>(DataRightsModuleMetadata.Name);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        TaskRunLease lease = new(
            Guid.NewGuid(),
            DataRightsModuleMetadata.Name,
            ExecuteTenantTerminationOwnerWorkPayload.TaskName,
            DataRightsModuleMetadata.TenantTerminationWorkerGroup,
            "worker-1",
            "node-1",
            JsonSerializer.Serialize(payload, JsonOptions),
            attempt: 1,
            nowUtc,
            nowUtc.AddMinutes(1),
            TenantA,
            payloadVersion:
                ExecuteTenantTerminationOwnerWorkPayload.PayloadVersion);
        return new(
            lease,
            registration,
            lease.CreateExecutionContext());
    }

    private sealed class StubHandler
        : ITaskHandler<ExecuteTenantTerminationOwnerWorkPayload>
    {
        public Task HandleAsync(
            ExecuteTenantTerminationOwnerWorkPayload payload,
            TaskExecutionContext context,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
