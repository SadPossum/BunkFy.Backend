namespace Integration.Tests;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Production;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.FileManagement.LocalStorage;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class
    TenantTerminationOperatorRecoveryPersistenceIntegrationTests
{
    private const string TenantId = "tenant-a";
    private const string Requester = "operator:requester";
    private const string Approver = "operator:approver";
    private const string Executor = "system:tenant-termination";
    private static readonly string CatalogDigest = new('b', 64);
    private static readonly DateTimeOffset RequestedAtUtc =
        new(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ApprovedAtUtc =
        RequestedAtUtc.AddMinutes(1);
    private static readonly DateTimeOffset ExecutionStartedAtUtc =
        RequestedAtUtc.AddMinutes(2);

    [Fact]
    [Trait("Category", "Integration")]
    public void Recovery_fixture_composes_without_infrastructure_io()
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-recovery-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            using ServiceProvider provider = CreateProvider(
                "Host=127.0.0.1;Port=1;Database=bunkfy;" +
                "Username=bunkfy;Password=bunkfy",
                contentRoot,
                Path.Combine(contentRoot, "protected-replay"));
            using IServiceScope scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider
                .GetRequiredService<IRequestDispatcher>());
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Protected_intent_recovers_exact_relational_start()
    {
        string contentRoot = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-tenant-termination-recovery-{Guid.NewGuid():N}");
        string replayRoot = Path.Combine(contentRoot, "protected-replay");
        Directory.CreateDirectory(contentRoot);

        try
        {
            await using PostgreSqlContainer postgreSql =
                new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("bunkfy_tenant_termination_recovery")
                    .Build();
            await postgreSql.StartAsync();
            string connectionString = postgreSql.GetConnectionString();
            await ApplyMigrationsAsync(connectionString);

            TenantTerminationApprovalEvidence evidence = CreateEvidence();
            DataRightsCase dataRightsCase = CreateApprovedCase(evidence);
            Guid processId = Guid.NewGuid();
            TenantTerminationReplayIntent intent = CreateIntent(
                dataRightsCase,
                processId);

            await using (DataRightsDbContext seed = CreateDbContext(
                connectionString))
            {
                seed.Cases.Add(dataRightsCase);
                await seed.SaveChangesAsync();
            }

            await using ServiceProvider provider = CreateProvider(
                connectionString,
                contentRoot,
                replayRoot);
            ITenantTerminationReplayStore replayStore = provider
                .GetRequiredService<ITenantTerminationReplayStore>();
            TenantTerminationReplayStoreReadiness readiness =
                await replayStore.CheckReadinessAsync(
                    CancellationToken.None);
            Assert.True(readiness.IsReady, readiness.FailureCode);

            TenantTerminationReplayAppendReceipt protectedReceipt =
                await replayStore.AppendAsync(
                    TenantTerminationReplayJournalEntry.ForIntent(intent),
                    CancellationToken.None);
            Assert.Equal(
                TenantTerminationReplayEntryKind.Intent,
                protectedReceipt.Kind);
            TenantTerminationReplayCheckpoint protectedCheckpoint =
                await replayStore.ReadTrustedCheckpointAsync(
                    TenantId,
                    processId,
                    CancellationToken.None);
            Assert.Equal(1, protectedCheckpoint.Cursor.Sequence);

            await AssertOrphanedStartAsync(
                connectionString,
                dataRightsCase.Id);

            Result<TenantTerminationStartDto> recovered =
                await RecoverAsync(
                    provider,
                    dataRightsCase.Id,
                    processId,
                    evidence,
                    dataRightsCase.Version,
                    expectedProcessVersion: null);

            Assert.True(recovered.IsSuccess, recovered.Error.Code);
            Assert.Equal(processId, recovered.Value.Process.Id);
            Assert.Equal(
                ExecutionStartedAtUtc,
                recovered.Value.Case.ExecutionStartedAtUtc);
            Assert.Equal(
                TenantTerminationStatus.Pending,
                recovered.Value.Process.Status);

            await AssertRecoveredStartAsync(
                connectionString,
                dataRightsCase,
                intent,
                expectedWakeUpCount: 1);

            Result<TenantTerminationStartDto> replayed =
                await RecoverAsync(
                    provider,
                    dataRightsCase.Id,
                    processId,
                    evidence,
                    recovered.Value.Case.Version,
                    recovered.Value.Process.Version);

            Assert.True(replayed.IsSuccess, replayed.Error.Code);
            Assert.Equal(
                recovered.Value.Process,
                replayed.Value.Process);
            await AssertRecoveredStartAsync(
                connectionString,
                dataRightsCase,
                intent,
                expectedWakeUpCount: 2);

            TenantTerminationReplayCheckpoint replayedCheckpoint =
                await replayStore.ReadTrustedCheckpointAsync(
                    TenantId,
                    processId,
                    CancellationToken.None);
            Assert.Equal(protectedCheckpoint, replayedCheckpoint);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    private static async Task<Result<TenantTerminationStartDto>> RecoverAsync(
        ServiceProvider provider,
        Guid caseId,
        Guid processId,
        TenantTerminationApprovalEvidence evidence,
        long expectedCaseVersion,
        long? expectedProcessVersion)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
            new RecoverTenantTerminationCommand(
                caseId,
                processId,
                evidence,
                expectedCaseVersion,
                expectedProcessVersion,
                Executor),
            CancellationToken.None);
    }

    private static async Task AssertOrphanedStartAsync(
        string connectionString,
        Guid caseId)
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            connectionString);
        DataRightsCase persistedCase = await dbContext.Cases
            .AsNoTracking()
            .SingleAsync(item => item.Id == caseId);
        Assert.Equal(DataRightsCaseState.Approved, persistedCase.Status);
        Assert.Null(persistedCase.ExecutionStartedAtUtc);
        Assert.Empty(await dbContext.TenantTerminationProcesses.ToArrayAsync());
        Assert.Empty(await dbContext.OutboxMessages.ToArrayAsync());
    }

    private static async Task AssertRecoveredStartAsync(
        string connectionString,
        DataRightsCase approvedCase,
        TenantTerminationReplayIntent intent,
        int expectedWakeUpCount)
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            connectionString);
        DataRightsCase recoveredCase = await dbContext.Cases
            .AsNoTracking()
            .SingleAsync(item => item.Id == approvedCase.Id);
        TenantTerminationProcess process = Assert.Single(
            await dbContext.TenantTerminationProcesses
                .AsNoTracking()
                .ToArrayAsync());
        OutboxMessage[] wakeUps = await dbContext.OutboxMessages
            .AsNoTracking()
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .ToArrayAsync();

        Assert.Equal(DataRightsCaseState.Executing, recoveredCase.Status);
        Assert.Equal(
            intent.ExecutionStartedAtUtc,
            recoveredCase.ExecutionStartedAtUtc);
        Assert.Equal(intent.ProcessId, process.Id);
        Assert.Equal(intent.CaseId, process.CaseId);
        Assert.Equal(intent.ApprovalRevision, process.ApprovalRevision);
        Assert.Equal(intent.IdempotencyKey, process.IdempotencyKey);
        Assert.Equal(intent.TerminationEpoch, process.TerminationEpoch);
        Assert.Equal(intent.ExportRequested, process.ExportRequested);
        Assert.Equal(intent.PolicyEvidenceSha256, process.PolicyEvidenceSha256);
        Assert.Equal(intent.ApprovedBy, process.ApprovedBy);
        Assert.Equal(intent.ApprovedAtUtc, process.ApprovedAtUtc);
        Assert.Equal(intent.ExecutingActorId, process.CreatedBy);
        Assert.Equal(intent.ExecutionStartedAtUtc, process.CreatedAtUtc);
        Assert.Equal(TenantTerminationProcessPhase.Freeze, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);

        Assert.Equal(expectedWakeUpCount, wakeUps.Length);
        Assert.All(wakeUps, wakeUp =>
        {
            Assert.Equal(
                typeof(TenantTerminationCoordinationRequestedIntegrationEvent)
                    .FullName,
                wakeUp.EventType);
            Assert.Equal(
                IntegrationEventNaming.CreateSubject(
                    "bunkfy-recovery-test",
                    DataRightsModuleMetadata.Name,
                    TenantTerminationCoordinationRequestedIntegrationEvent
                        .EventType,
                    TenantTerminationCoordinationRequestedIntegrationEvent
                        .EventVersion),
                wakeUp.Subject);
            Assert.Equal(
                TenantTerminationCoordinationRequestedIntegrationEvent
                    .EventVersion,
                wakeUp.Version);
            Assert.Equal(TenantId, wakeUp.ScopeId);
            Assert.Equal(intent.ExecutionStartedAtUtc, wakeUp.OccurredAtUtc);
            Assert.Contains(
                intent.ProcessId.ToString("D"),
                wakeUp.Payload,
                StringComparison.OrdinalIgnoreCase);
            Assert.Null(wakeUp.ProcessedAtUtc);
        });
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        string contentRoot,
        string replayRoot)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = contentRoot
            });
        builder.Configuration["ApplicationIdentity:Namespace"] =
            "bunkfy-recovery-test";
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "LocalStorage";
        builder.Configuration["FileManagement:MaximumObjectBytes"] =
            "2097152";
        builder.Configuration["FileManagement:AllowedContentTypes:0"] =
            "application/octet-stream";
        builder.Configuration["FileManagement:LocalStorage:RootPath"] =
            Path.Combine(contentRoot, "files");
        builder.Configuration[
            "DataRights:TenantTerminationReplay:Provider"] = "LocalFile";
        builder.Configuration[
            "DataRights:TenantTerminationReplay:LocalFilePath"] = replayRoot;

        builder.Services.AddSingleton<IScopeContext>(
            new TestScopeContext());
        builder.Services.AddSingleton<ISystemClock>(
            new TestClock());
        builder.Services.AddSingleton<ITaskRunStore>(
            new UnreachableTaskRunStore());
        builder.Services.AddSingleton<
            ITenantTerminationRequiredOwnerCatalog>(
                new TestRequiredOwnerCatalog());
        builder.Services.AddSingleton<ITenantTerminationProductionCatalog>(
            new TestProductionCatalog());

        builder.AddApplicationEventsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.AddTenancyInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.AddCqrsInfrastructure();
        builder.AddLocalFileStorage();
        builder.Services.AddDataRightsApplication();
        builder.Services.AddDataRightsTenantTerminationTaskScheduling();
        builder.AddDataRightsPersistence();

        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        await using DataRightsDbContext dbContext = CreateDbContext(
            connectionString);
        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync();
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
    }

    private static DataRightsDbContext CreateDbContext(
        string connectionString)
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
        return new(options, new TestScopeContext());
    }

    private static DataRightsCase CreateApprovedCase(
        TenantTerminationApprovalEvidence evidence)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner,
            DataRightsRestrictionAction.None).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            TenantId,
            request,
            Requester,
            RequestedAtUtc).Value;
        Assert.True(dataRightsCase.PrepareTenantTerminationReview(
            exportRequested: false,
            dataRightsCase.Version,
            Requester,
            RequestedAtUtc).IsSuccess);
        Assert.True(dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            evidence.ComputeSha256(),
            dataRightsCase.Version,
            Approver,
            ApprovedAtUtc).IsSuccess);
        return dataRightsCase;
    }

    private static TenantTerminationReplayIntent CreateIntent(
        DataRightsCase dataRightsCase,
        Guid processId) =>
        TenantTerminationReplayIntent.Create(
            TenantId,
            processId,
            dataRightsCase.Id,
            DataRightsRequesterRelationship.TenantOwner,
            dataRightsCase.CreatedBy,
            dataRightsCase.CreatedAtUtc,
            exportRequested: false,
            dataRightsCase.DecisionRevision!.Value,
            dataRightsCase.DecidedBy!,
            dataRightsCase.DecidedAtUtc!.Value,
            dataRightsCase.TenantTerminationPolicyEvidenceSha256!,
            CatalogDigest,
            CreateProcessCoordinate(
                "bunkfy.data-rights.tenant-termination." +
                "process-idempotency.v1",
                processId),
            CreateProcessCoordinate(
                "bunkfy.data-rights.tenant-termination.epoch.v1",
                processId),
            Executor,
            ExecutionStartedAtUtc,
            ExecutionStartedAtUtc);

    private static Guid CreateProcessCoordinate(
        string domain,
        Guid processId)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendHashCoordinate(hash, domain);
        AppendHashCoordinate(hash, processId.ToString("N"));
        byte[] digest = hash.GetHashAndReset();
        try
        {
            return new Guid(digest.AsSpan(0, 16));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void AppendHashCoordinate(
        IncrementalHash hash,
        string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        try
        {
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static TenantTerminationApprovalEvidence CreateEvidence() =>
        new(
            "approval:recovery-1",
            CatalogDigest,
            "backup:recovery-1",
            "restore:recovery-1",
            "assurance:recovery-1");

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => ExecutionStartedAtUtc;
    }

    private sealed class TestRequiredOwnerCatalog
        : ITenantTerminationRequiredOwnerCatalog
    {
        public IReadOnlyCollection<string> RequiredOwnerKeys { get; } =
            ["workspaces"];
    }

    private sealed class TestProductionCatalog
        : ITenantTerminationProductionCatalog
    {
        public Result<TenantTerminationProductionCatalogEvidence> Validate(
            IReadOnlyCollection<string> requiredOwnerKeys) =>
            Result.Success(new TenantTerminationProductionCatalogEvidence(
                OwnerCount: 1,
                ExportOwnerCount: 1,
                TerminalOwnerKey: "workspaces",
                CatalogDigest));
    }

    private sealed class UnreachableTaskRunStore : ITaskRunStore
    {
        public Task<TaskRunEnqueueResult> EnqueueAsync(
            TaskRunRequest request,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunPage> ListAsync(
            TaskRunFilter filter,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunDetails?> GetAsync(
            Guid runId,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunStats> GetStatsAsync(
            TaskRunStatsFilter filter,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
            TaskWorkerClaim claim,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkStartedAsync(
            TaskExecutionContext context,
            DateTimeOffset startedAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkSucceededAsync(
            TaskExecutionContext context,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkCanceledAsync(
            TaskExecutionContext context,
            DateTimeOffset canceledAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            string error,
            DateTimeOffset failedAtUtc,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> RequestCancellationAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset requestedAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> RetryAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset scheduledAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
            DateTimeOffset nowUtc,
            TimeSpan staleAfter,
            int maxRuns,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskControlMessageEnqueueOutcome>
            EnqueueControlMessageAsync(
                TaskControlMessage message,
                CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
            TaskExecutionContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken) =>
            throw Unexpected();

        private static NotSupportedException Unexpected() =>
            new("Recovery must not schedule task work before coordination.");
    }
}
