namespace Integration.Tests;

using System.Globalization;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Host.Worker;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Retention;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Aggregates;
using BunkFy.Modules.Retention.Domain.Models;
using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.TaskRuntime.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class RetentionControlPlaneIntegrationTests
{
    private const string HeldTenantId =
        "9b000000-0000-0000-0000-000000000001";
    private const string UnheldTenantId =
        "9b000000-0000-0000-0000-000000000002";
    private const string SensitiveHistoryDataClass =
        "sensitive-reservation-history";
    private const string RawPayloadDataClass = "raw-source-evidence";
    private static readonly Guid HeldPropertyId =
        Guid.Parse("9b000000-0000-0000-0000-000000000011");
    private static readonly Guid UnheldPropertyId =
        Guid.Parse("9b000000-0000-0000-0000-000000000012");

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Scheduler_isolates_tenants_and_converges_after_legal_hold_release()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_retention_control_plane_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        using IHost worker = CreateWorker(postgreSql.GetConnectionString());
        await MigrateAsync(worker).ConfigureAwait(false);
        SeededCandidate held = await SeedTenantAsync(
            worker,
            HeldTenantId,
            HeldPropertyId,
            placeLegalHold: true).ConfigureAwait(false);
        SeededCandidate unheld = await SeedTenantAsync(
            worker,
            UnheldTenantId,
            UnheldPropertyId,
            placeLegalHold: false).ConfigureAwait(false);

        bool workerStarted = false;
        await worker.StartAsync().ConfigureAwait(false);
        workerStarted = true;
        try
        {
            IReadOnlyList<TaskRun> scheduledRuns =
                await WaitForScheduledRunsAsync(
                    worker,
                    expectedCount: 4,
                    TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            Assert.All(
                scheduledRuns,
                run => Assert.Equal(TaskRunStatus.Succeeded, run.Status));
            Assert.Equal(
                [HeldTenantId, UnheldTenantId],
                scheduledRuns
                    .Select(run => run.ScopeId!)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());

            await AssertInitialOutcomesAsync(
                worker,
                held,
                unheld).ConfigureAwait(false);

            await ReleaseLegalHoldAsync(worker, held).ConfigureAwait(false);
            Guid retryRunId = await EnqueueSensitiveHistoryRunAsync(
                worker,
                HeldTenantId).ConfigureAwait(false);
            TaskRun retryRun = await WaitForRunAsync(
                worker,
                retryRunId,
                TaskRunStatus.Succeeded,
                TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            Assert.Equal(1, retryRun.Attempts);
            await AssertReleasedHoldOutcomeAsync(worker, held)
                .ConfigureAwait(false);
        }
        finally
        {
            if (workerStarted)
            {
                await worker.StopAsync().ConfigureAwait(false);
            }
        }
    }

    private static IHost CreateWorker(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = "Integration"
            });
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.Configuration["NatsJetStream:Enabled"] = "false";
        builder.Configuration["NatsConsumers:Enabled"] = "false";
        builder.Configuration["Tasks:Worker:Enabled"] = "true";
        builder.Configuration["Tasks:Worker:WorkerGroups:0"] =
            RetentionModuleMetadata.WorkerGroup;
        builder.Configuration["Tasks:Worker:BatchSize"] = "1";
        builder.Configuration["Tasks:Worker:MaxConcurrency"] = "1";
        builder.Configuration["Tasks:Worker:PollInterval"] =
            "00:00:00.100";
        builder.Configuration["Tasks:Worker:LeaseDuration"] =
            "00:00:30";
        builder.Configuration["Tasks:Worker:HandlerTimeout"] =
            "00:00:30";
        builder.Configuration["Tasks:Worker:RetryBaseDelay"] =
            "00:00:00.100";
        builder.Configuration["Tasks:Worker:RetryMaxDelay"] =
            "00:00:01";
        builder.Configuration["Tasks:Worker:WorkerId"] =
            "retention-worker-test";
        builder.Configuration["Tasks:Worker:NodeId"] =
            "retention-worker-node";
        builder.Configuration["Tasks:Worker:TimeoutScannerEnabled"] =
            "false";
        builder.Configuration["Tasks:Worker:MetricsSamplerEnabled"] =
            "false";
        builder.Configuration["Tasks:Scheduler:Enabled"] = "true";
        builder.Configuration["Tasks:Scheduler:PollInterval"] =
            "00:00:00.100";
        builder.Configuration["Tasks:Scheduler:RequestedBy"] =
            "retention-integration-scheduler";
        builder.Configuration["Worker:Modules:Ingestion"] = "true";
        builder.Configuration["Worker:Modules:Organizations"] = "true";
        builder.Configuration["Worker:Modules:Properties"] = "true";
        builder.Configuration["Worker:Modules:Retention"] = "true";
        builder.Configuration["Worker:Modules:TaskRuntime"] = "true";
        builder.Configuration["FileManagement:Enabled"] = "true";
        builder.Configuration["FileManagement:Provider"] = "Minio";
        builder.Configuration["FileManagement:AllowedContentTypes:0"] =
            "application/json";
        builder.Configuration["FileManagement:Minio:Endpoint"] =
            "localhost:9000";
        builder.Configuration["FileManagement:Minio:AccessKey"] = "test";
        builder.Configuration["FileManagement:Minio:SecretKey"] =
            "test-secret";
        builder.Configuration["FileManagement:Minio:BucketName"] =
            "retention-test";
        builder.Configuration["FileManagement:Minio:UseSsl"] = "false";
        builder.Configuration[
            "FileManagement:Minio:CreateBucketIfMissing"] = "false";
        builder.Logging.ClearProviders();

        builder.AddWorkerHost();
        ModuleCompositionValidationResult composition =
            builder.ValidateModuleComposition();
        Assert.True(composition.IsValid, composition.Report);
        return builder.Build();
    }

    private static async Task MigrateAsync(IHost worker)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IngestionDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<RetentionDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task<SeededCandidate> SeedTenantAsync(
        IHost worker,
        string tenantId,
        Guid propertyId,
        bool placeLegalHold)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(tenantId);
        IIntegrationEventSubscriptionRegistry subscriptions =
            scope.ServiceProvider
                .GetRequiredService<IIntegrationEventSubscriptionRegistry>();

        IntegrationEventSubscription organizationSubscription =
            subscriptions.Subscriptions.Single(item =>
                item.ConsumerModule == RetentionModuleMetadata.Name &&
                item.EventType ==
                typeof(OrganizationChangedIntegrationEvent));
        var organizationHandler =
            (IIntegrationEventHandler<OrganizationChangedIntegrationEvent>)
            scope.ServiceProvider.GetRequiredService(
                organizationSubscription.HandlerType);
        await organizationHandler.HandleAsync(
            new OrganizationChangedIntegrationEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                tenantId,
                Guid.NewGuid(),
                OrganizationChange.Created,
                OrganizationStatus.Active,
                organizationVersion: 1),
            CancellationToken.None).ConfigureAwait(false);

        IntegrationEventSubscription propertySubscription =
            subscriptions.Subscriptions.Single(item =>
                item.ConsumerModule == IngestionModuleMetadata.Name &&
                item.EventType == typeof(PropertyCreatedIntegrationEvent));
        var propertyHandler =
            (IIntegrationEventHandler<PropertyCreatedIntegrationEvent>)
            scope.ServiceProvider.GetRequiredService(
                propertySubscription.HandlerType);
        await propertyHandler.HandleAsync(
            new PropertyCreatedIntegrationEvent(
                Guid.NewGuid(),
                tenantId,
                DateTimeOffset.UtcNow,
                propertyId,
                "Retention Test Property",
                $"retention-{propertyId:N}",
                "UTC",
                PropertyStatus.Active,
                propertyVersion: 1),
            CancellationToken.None).ConfigureAwait(false);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        DateTimeOffset proposalCreatedAtUtc = nowUtc.AddDays(-10);
        DateTimeOffset proposalCompletedAtUtc =
            proposalCreatedAtUtc.AddDays(1);
        Guid connectionId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid payloadFileId = Guid.NewGuid();
        IngestionDbContext ingestion =
            scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        ingestion.AdapterConnections.Add(AdapterConnection.Create(
            connectionId,
            tenantId,
            propertyId,
            "retention.integration",
            AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://retention-integration",
            secretReference: null,
            nowUtc).Value);

        ObservationCountryPolicyEvidence evidence =
            ObservationCountryPolicyEvidence.Create(
                "GB",
                "integration-policy",
                1,
                "eu",
                "eu-only",
                "integration-retention",
                1,
                new string('a', 64),
                "reservation-import",
                "adapter-ingress",
                "property-policy",
                proposalCreatedAtUtc,
                nowUtc.AddDays(30),
                proposalCreatedAtUtc).Value;
        ObservationReceipt receipt = ObservationReceipt.Create(
            receiptId,
            tenantId,
            propertyId,
            connectionId,
            runId: null,
            Guid.NewGuid(),
            "reservation",
            $"external-{receiptId:N}",
            "revision-1",
            $"reservation:{receiptId:N}:revision-1",
            new string('b', 64),
            evidence,
            payloadFileId,
            nowUtc.AddDays(30),
            proposalCreatedAtUtc,
            proposalCreatedAtUtc,
            proposalCreatedAtUtc).Value;
        Assert.True(
            receipt.MarkProcessed(
                proposalCreatedAtUtc.AddMinutes(1)).IsSuccess);
        ingestion.ObservationReceipts.Add(receipt);

        const string sensitiveDiff = /*lang=json,strict*/ "{\"guest\":\"Sensitive\"}";
        ChangeProposal proposal = ChangeProposal.Create(
            Guid.NewGuid(),
            tenantId,
            propertyId,
            connectionId,
            receiptId,
            Guid.NewGuid(),
            payloadFileId,
            1,
            "retention-integration",
            sensitiveDiff,
            proposalCreatedAtUtc).Value;
        Assert.True(proposal.BeginApply(
            "integration:retention",
            Guid.NewGuid(),
            proposal.Version,
            proposalCompletedAtUtc.AddMinutes(-1)).IsSuccess);
        Assert.True(proposal.MarkFailed(
            "Integration retention terminal state",
            proposal.Version,
            proposalCompletedAtUtc.AddDays(1),
            proposalCompletedAtUtc).IsSuccess);
        ingestion.ChangeProposals.Add(proposal);

        LegalHold? legalHold = null;
        if (placeLegalHold)
        {
            legalHold = LegalHold.Place(
                Guid.NewGuid(),
                tenantId,
                propertyId,
                "Integration legal hold",
                "integration:retention",
                nowUtc.AddHours(-1)).Value;
            ingestion.LegalHolds.Add(legalHold);
        }

        await ingestion.SaveChangesAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<RetentionDbContext>()
            .SaveChangesAsync().ConfigureAwait(false);
        return new(
            tenantId,
            propertyId,
            proposal.Id,
            legalHold?.Id);
    }

    private static async Task<IReadOnlyList<TaskRun>>
        WaitForScheduledRunsAsync(
            IHost worker,
            int expectedCount,
            TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = worker.Services.CreateScope();
            TaskRun[] runs = await scope.ServiceProvider
                .GetRequiredService<TaskRuntimeDbContext>()
                .TaskRuns.AsNoTracking()
                .Where(run =>
                    run.ModuleName == RetentionModuleMetadata.Name &&
                    run.TaskName ==
                    ExecuteRetentionSchedulePayload.TaskName)
                .OrderBy(run => run.CreatedAtUtc)
                .ToArrayAsync().ConfigureAwait(false);
            TaskRun? failed = runs.FirstOrDefault(run =>
                run.Status is TaskRunStatus.Failed or
                    TaskRunStatus.Canceled or
                    TaskRunStatus.TimedOut);
            if (failed is not null)
            {
                string details = await DescribeFailedRunAsync(
                    worker,
                    failed).ConfigureAwait(false);
                Assert.True(failed is null, details);
            }

            if (runs.Length == expectedCount &&
                runs.All(run => run.Status == TaskRunStatus.Succeeded))
            {
                return runs;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Expected {expectedCount} scheduled retention runs.");
    }

    private static async Task<string> DescribeFailedRunAsync(
        IHost worker,
        TaskRun run)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(run.ScopeId!);
        RetentionExecution? central = await scope.ServiceProvider
            .GetRequiredService<RetentionDbContext>()
            .Executions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == run.Id)
            .ConfigureAwait(false);
        IngestionRetentionExecution? owner = await scope.ServiceProvider
            .GetRequiredService<IngestionDbContext>()
            .RetentionExecutions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == run.Id)
            .ConfigureAwait(false);

        return $"Retention run {run.Id} failed: {run.LastError}; " +
            $"scope={run.ScopeId}; attempts={run.Attempts}; " +
            $"payload={run.Payload}; " +
            $"central={Describe(central)}; owner={Describe(owner)}";
    }

    private static string Describe(RetentionExecution? execution) =>
        execution is null
            ? "missing"
            : $"state:{execution.State},attempt:{execution.Attempt}," +
              $"outcome:{execution.OutcomeCode ?? "none"}";

    private static string Describe(IngestionRetentionExecution? execution) =>
        execution is null
            ? "missing"
            : $"state:{execution.State},attempt:{execution.Attempt}," +
              $"affected:{execution.AffectedCount}," +
              $"remaining:{execution.RemainingCount?.ToString(CultureInfo.InvariantCulture) ?? "none"}," +
              $"outcome:{execution.OutcomeCode ?? "none"}";

    private static async Task<TaskRun> WaitForRunAsync(
        IHost worker,
        Guid runId,
        TaskRunStatus expectedStatus,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = worker.Services.CreateScope();
            TaskRun? run = await scope.ServiceProvider
                .GetRequiredService<TaskRuntimeDbContext>()
                .TaskRuns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == runId)
                .ConfigureAwait(false);
            if (run?.Status == expectedStatus)
            {
                return run;
            }

            if (run?.Status is TaskRunStatus.Failed or
                TaskRunStatus.Canceled or
                TaskRunStatus.TimedOut)
            {
                throw new InvalidOperationException(
                    await DescribeFailedRunAsync(worker, run)
                        .ConfigureAwait(false));
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Retention run {runId} did not reach {expectedStatus}.");
    }

    private static async Task AssertInitialOutcomesAsync(
        IHost worker,
        SeededCandidate held,
        SeededCandidate unheld)
    {
        await AssertTenantOutcomeAsync(
            worker,
            held,
            RetentionExecutionState.Blocked,
            "ingestion.sensitive-history.legal-hold",
            expectedAffectedCount: 0,
            expectedRemainingCount: 1,
            expectedProposalRedacted: false,
            expectedOwnerReceiptCount: 2).ConfigureAwait(false);
        await AssertTenantOutcomeAsync(
            worker,
            unheld,
            RetentionExecutionState.Completed,
            "ingestion.sensitive-history.completed",
            expectedAffectedCount: 1,
            expectedRemainingCount: 0,
            expectedProposalRedacted: true,
            expectedOwnerReceiptCount: 2).ConfigureAwait(false);
    }

    private static async Task AssertReleasedHoldOutcomeAsync(
        IHost worker,
        SeededCandidate held) =>
        await AssertTenantOutcomeAsync(
            worker,
            held,
            RetentionExecutionState.Completed,
            "ingestion.sensitive-history.completed",
            expectedAffectedCount: 1,
            expectedRemainingCount: 0,
            expectedProposalRedacted: true,
            expectedOwnerReceiptCount: 3).ConfigureAwait(false);

    private static async Task AssertTenantOutcomeAsync(
        IHost worker,
        SeededCandidate candidate,
        RetentionExecutionState expectedState,
        string expectedOutcomeCode,
        int expectedAffectedCount,
        int expectedRemainingCount,
        bool expectedProposalRedacted,
        int expectedOwnerReceiptCount)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(candidate.TenantId);
        RetentionDbContext retention =
            scope.ServiceProvider.GetRequiredService<RetentionDbContext>();
        RetentionScheduleState[] scheduleStates =
            await retention.ScheduleStates
                .AsNoTracking()
                .OrderBy(state => state.DataClassKey)
                .ToArrayAsync().ConfigureAwait(false);
        Assert.Equal(2, scheduleStates.Length);
        Assert.Contains(
            scheduleStates,
            state => state.DataClassKey == RawPayloadDataClass);
        RetentionScheduleState sensitive = Assert.Single(
            scheduleStates,
            state => state.DataClassKey == SensitiveHistoryDataClass);
        Assert.Equal(expectedState, sensitive.State);
        Assert.Equal(expectedOutcomeCode, sensitive.OutcomeCode);
        Assert.Equal(expectedAffectedCount, sensitive.LastAffectedCount);
        Assert.Equal(expectedRemainingCount, sensitive.LastRemainingCount);
        Assert.Equal(
            expectedState == RetentionExecutionState.Blocked,
            sensitive.HoldReviewDueAtUtc.HasValue);

        IngestionDbContext ingestion =
            scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        ChangeProposal proposal = await ingestion.ChangeProposals
            .AsNoTracking()
            .SingleAsync(item => item.Id == candidate.ProposalId)
            .ConfigureAwait(false);
        Assert.Equal(expectedProposalRedacted, proposal.Diff is null);
        Assert.Equal(
            expectedOwnerReceiptCount,
            await ingestion.RetentionExecutions.CountAsync()
                .ConfigureAwait(false));
        IngestionRetentionExecution ownerReceipt =
            await ingestion.RetentionExecutions
                .AsNoTracking()
                .Where(item =>
                    item.DataClassKey == SensitiveHistoryDataClass)
                .OrderByDescending(item => item.StartedAtUtc)
                .FirstAsync().ConfigureAwait(false);
        Assert.Equal(expectedOutcomeCode, ownerReceipt.OutcomeCode);
        Assert.Equal(expectedAffectedCount, ownerReceipt.AffectedCount);
        Assert.Equal(expectedRemainingCount, ownerReceipt.RemainingCount);
    }

    private static async Task ReleaseLegalHoldAsync(
        IHost worker,
        SeededCandidate held)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(held.TenantId);
        IngestionDbContext ingestion =
            scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
        LegalHold legalHold = await ingestion.LegalHolds.SingleAsync(
            item => item.Id == held.LegalHoldId).ConfigureAwait(false);
        Assert.True(legalHold.Release(
            legalHold.Version,
            "integration:retention",
            "Integration legal hold released",
            DateTimeOffset.UtcNow).IsSuccess);
        await ingestion.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Guid> EnqueueSensitiveHistoryRunAsync(
        IHost worker,
        string tenantId)
    {
        using IServiceScope scope = worker.Services.CreateScope();
        Guid runId = Guid.NewGuid();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        await scope.ServiceProvider.GetRequiredService<ITaskRunStore>()
            .EnqueueAsync(
                new TaskRunRequest(
                    runId,
                    RetentionModuleMetadata.Name,
                    ExecuteRetentionSchedulePayload.TaskName,
                    JsonSerializer.Serialize(
                        new ExecuteRetentionSchedulePayload(
                            IngestionModuleMetadata.Name,
                            SensitiveHistoryDataClass,
                            1,
                            RetentionTargetScopeKind.Tenant)),
                    nowUtc,
                    nowUtc,
                    RetentionModuleMetadata.WorkerGroup,
                    tenantId,
                    requestedBy: "retention-integration-test",
                    maxAttempts: 1,
                    payloadVersion:
                        ExecuteRetentionSchedulePayload.PayloadVersion,
                    deduplicationKey:
                        $"retention-release:{tenantId}:{runId:N}"),
                CancellationToken.None).ConfigureAwait(false);
        return runId;
    }

    private sealed record SeededCandidate(
        string TenantId,
        Guid PropertyId,
        Guid ProposalId,
        Guid? LegalHoldId);
}
