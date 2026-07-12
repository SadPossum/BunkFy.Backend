namespace Integration.Tests;

using System.Text.Json;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Modules.TaskRuntime.Application;
using Integration.Tests.Support;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class TaskRuntimeIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_runtime_store_claims_retries_reclaims_and_completes_against_sql_server_and_postgre_sql()
    {
        await RunStoreScenarioAsync(
            "SqlServer",
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_task_runtime_tests"));
            });

        await RunStoreScenarioAsync(
            "PostgreSql",
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_task_runtime_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_worker_processes_sample_task_through_persisted_runtime()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_worker_tests")
            .Build();
        await postgreSql.StartAsync();

        await using TaskRuntimeTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid runId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        await application.EnqueueSampleTaskAsync(runId, DateTimeOffset.UtcNow).ConfigureAwait(false);
        await application.StartAsync().ConfigureAwait(false);

        IReadOnlyList<TestTaskReport> reports =
            await application.Sink.WaitForReportsAsync(1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskRunSnapshot snapshot = await application.WaitForStatusAsync(
                runId,
                TaskRunStatus.Succeeded,
                TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.Single(reports);
        Assert.Equal(runId, reports[0].RunId);
        Assert.Equal("tenant-a", reports[0].ScopeId);
        Assert.Equal(TaskRunStatus.Succeeded, snapshot.Status);
        Assert.Equal(1, snapshot.Attempts);
        Assert.Null(snapshot.LockedBy);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Competing_workers_do_not_reclaim_a_quiet_task_while_automatic_heartbeat_is_active()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_worker_heartbeat_tests")
            .Build();
        await postgreSql.StartAsync();

        TimeSpan leaseDuration = TimeSpan.FromMilliseconds(600);
        TimeSpan heartbeatInterval = TimeSpan.FromMilliseconds(150);
        await using TaskRuntimeTestApplication workerA = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true,
            workerId: "worker-a",
            nodeId: "node-a",
            leaseDuration: leaseDuration,
            heartbeatInterval: heartbeatInterval,
            maxConcurrency: 1,
            batchSize: 1);
        await using TaskRuntimeTestApplication workerB = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true,
            workerId: "worker-b",
            nodeId: "node-b",
            leaseDuration: leaseDuration,
            heartbeatInterval: heartbeatInterval,
            maxConcurrency: 1,
            batchSize: 1);
        await workerA.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid runId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        string payload = JsonSerializer.Serialize(new TestQuietTaskPayload("quiet", 1_800));

        await workerA.EnqueueSampleTaskAsync(
                runId,
                DateTimeOffset.UtcNow,
                maxAttempts: 1,
                payloadJson: payload,
                taskName: TestQuietTaskPayload.TaskName)
            .ConfigureAwait(false);
        await Task.WhenAll(workerA.StartAsync(), workerB.StartAsync()).ConfigureAwait(false);

        TaskRunSnapshot snapshot = await workerA.WaitForStatusAsync(
                runId,
                TaskRunStatus.Succeeded,
                TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
        IReadOnlyList<TestTaskReport> reports = workerA.Sink.Reports.Concat(workerB.Sink.Reports).ToArray();

        TestTaskReport report = Assert.Single(reports);
        Assert.Equal(runId, report.RunId);
        Assert.Equal(1, report.Attempt);
        Assert.Equal(TaskRunStatus.Succeeded, snapshot.Status);
        Assert.Equal(1, snapshot.Attempts);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_worker_cooperatively_cancels_slow_task_from_control_message()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_control_tests")
            .Build();
        await postgreSql.StartAsync();

        await using TaskRuntimeTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: true);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid runId = Guid.Parse("12121212-3434-5656-7878-909090909090");
        string payload = JsonSerializer.Serialize(new TestSlowTaskPayload(
            "slow",
            10,
            Steps: 20,
            DelayMilliseconds: 50));

        await application.EnqueueSampleTaskAsync(
                runId,
                DateTimeOffset.UtcNow,
                maxAttempts: 1,
                payloadJson: payload,
                taskName: TestSlowTaskPayload.TaskName)
            .ConfigureAwait(false);
        await application.StartAsync().ConfigureAwait(false);
        Result<TaskControlMessage> control = await application.SendControlThroughApplicationAsync(
                runId,
                TaskControlCommandNames.Cancel,
                "{}",
                DateTimeOffset.UtcNow.AddMinutes(5))
            .ConfigureAwait(false);
        TaskRunSnapshot snapshot = await application.WaitForStatusAsync(
                runId,
                TaskRunStatus.Canceled,
                TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);

        Assert.True(control.IsSuccess);
        Assert.Equal(TaskRunStatus.Canceled, snapshot.Status);
        Assert.Empty(application.Sink.Reports);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Task_runtime_retention_deletes_only_due_terminal_runs_without_control_history()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_task_retention_tests")
            .Build();
        await postgreSql.StartAsync();
        await using TaskRuntimeTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            workerEnabled: false,
            clockUtcNow: Now,
            retentionEnabled: true);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);
        Guid oldRunId = Guid.Parse("10101010-1010-1010-1010-101010101010");
        Guid recentRunId = Guid.Parse("20202020-2020-2020-2020-202020202020");
        Guid protectedRunId = Guid.Parse("30303030-3030-3030-3030-303030303030");

        await CompleteRunAsync(application, oldRunId, Now.AddDays(-40)).ConfigureAwait(false);
        await CompleteRunAsync(application, recentRunId, Now.AddDays(-1)).ConfigureAwait(false);
        await CompleteRunAsync(application, protectedRunId, Now.AddDays(-40)).ConfigureAwait(false);
        await application.AddPendingControlAsync(
            Guid.Parse("40404040-4040-4040-4040-404040404040"),
            protectedRunId,
            Now.AddDays(-39)).ConfigureAwait(false);

        await application.StartAsync().ConfigureAwait(false);

        Assert.True(await application.WaitForTaskRunDeletionAsync(oldRunId, TimeSpan.FromSeconds(10)));
        Assert.True(await application.TaskRunExistsAsync(recentRunId).ConfigureAwait(false));
        Assert.True(await application.TaskRunExistsAsync(protectedRunId).ConfigureAwait(false));
    }

    private static async Task CompleteRunAsync(
        TaskRuntimeTestApplication application,
        Guid runId,
        DateTimeOffset createdAtUtc)
    {
        await application.EnqueueSampleTaskAsync(runId, createdAtUtc).ConfigureAwait(false);
        TaskRunLease lease = (await application.ClaimAsync(
                "retention-worker",
                "retention-node",
                createdAtUtc,
                TimeSpan.FromMinutes(5))
            .ConfigureAwait(false)).Single();
        TaskExecutionContext context = lease.CreateExecutionContext();
        await application.MarkStartedAsync(context, createdAtUtc.AddMinutes(1)).ConfigureAwait(false);
        await application.MarkSucceededAsync(context, createdAtUtc.AddMinutes(2)).ConfigureAwait(false);
    }

    private static async Task RunStoreScenarioAsync(
        string provider,
        Func<Task<ProviderLease>> createProvider)
    {
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        await using TaskRuntimeTestApplication application = new(
            provider,
            providerLease.ConnectionString,
            workerEnabled: false,
            clockUtcNow: Now);
        await application.MigrateDatabaseAsync().ConfigureAwait(false);

        Guid runId = Guid.Parse($"11111111-1111-1111-1111-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(runId, Now, maxAttempts: 2).ConfigureAwait(false);

        IReadOnlyList<TaskRunLease> firstClaim = await application.ClaimAsync(
            "worker-a",
            "node-a",
            Now,
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> blockedClaim = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now,
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskExecutionContext context = firstClaim.Single().CreateExecutionContext();
        TaskExecutionContext wrongWorker = new(
            runId,
            context.ModuleName,
            context.TaskName,
            context.WorkerGroup,
            "worker-b",
            context.NodeId,
            context.Attempt,
            context.ScopeId);

        await application.MarkStartedAsync(wrongWorker, Now.AddSeconds(1)).ConfigureAwait(false);
        TaskRunSnapshot afterWrongWorker = await application.GetSnapshotAsync(runId).ConfigureAwait(false);

        await application.MarkStartedAsync(context, Now.AddSeconds(1)).ConfigureAwait(false);
        await application.ReportProgressAsync(context, new TaskProgress(25, "warming up"), Now.AddSeconds(2)).ConfigureAwait(false);
        await application.EnqueueControlAsync(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            runId,
            Now.AddSeconds(3)).ConfigureAwait(false);
        IReadOnlyList<TaskControlMessage> controlMessages = await application.ReadPendingControlAsync(context).ConfigureAwait(false);
        await application.MarkFailedAsync(context, "temporary", Now.AddSeconds(4), Now.AddSeconds(10)).ConfigureAwait(false);
        TaskRunSnapshot afterFailure = await application.GetSnapshotAsync(runId).ConfigureAwait(false);

        IReadOnlyList<TaskRunLease> beforeRetryDue = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now.AddSeconds(5),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> retryClaim = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now.AddSeconds(10),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskExecutionContext retryContext = retryClaim.Single().CreateExecutionContext();
        await application.MarkStartedAsync(retryContext, Now.AddSeconds(11)).ConfigureAwait(false);
        await application.MarkSucceededAsync(retryContext, Now.AddSeconds(12)).ConfigureAwait(false);
        TaskRunSnapshot afterSuccess = await application.GetSnapshotAsync(runId).ConfigureAwait(false);

        Guid reclaimRunId = Guid.Parse($"99999999-9999-9999-9999-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(reclaimRunId, Now.AddSeconds(20)).ConfigureAwait(false);
        _ = await application.ClaimAsync("worker-a", "node-a", Now.AddSeconds(20), TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> reclaimed = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now.AddSeconds(22),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskRunLease reclaimedLease = reclaimed.Single();
        TaskExecutionContext reclaimedContext = reclaimedLease.CreateExecutionContext();
        await application.MarkStartedAsync(reclaimedContext, Now.AddSeconds(23)).ConfigureAwait(false);
        await application.MarkSucceededAsync(reclaimedContext, Now.AddSeconds(24)).ConfigureAwait(false);

        Guid cancelQueuedRunId = Guid.Parse($"77777777-7777-7777-7777-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(cancelQueuedRunId, Now.AddSeconds(30)).ConfigureAwait(false);
        await application.RequestCancellationAsync(cancelQueuedRunId, "operator", Now.AddSeconds(31)).ConfigureAwait(false);
        TaskRunSnapshot canceledQueued = await application.GetSnapshotAsync(cancelQueuedRunId).ConfigureAwait(false);

        Guid cancelReclaimRunId = Guid.Parse($"88888888-8888-8888-8888-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(cancelReclaimRunId, Now.AddSeconds(40)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> cancellationFirstClaim = await application.ClaimAsync(
            "worker-a",
            "node-a",
            Now.AddSeconds(40),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        TaskExecutionContext cancellationFirstContext = cancellationFirstClaim.Single().CreateExecutionContext();
        await application.MarkStartedAsync(cancellationFirstContext, Now.AddSeconds(41)).ConfigureAwait(false);
        await application.RequestCancellationAsync(cancelReclaimRunId, "operator", Now.AddSeconds(42)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> cancellationBlockedClaim = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now.AddSeconds(43),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> cancellationReclaim = await application.ClaimAsync(
            "worker-b",
            "node-b",
            Now.AddSeconds(46),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        TaskExecutionContext cancellationReclaimContext = cancellationReclaim.Single().CreateExecutionContext();
        await application.MarkCanceledAsync(cancellationReclaimContext, Now.AddSeconds(47)).ConfigureAwait(false);
        TaskRunSnapshot canceledReclaimed = await application.GetSnapshotAsync(cancelReclaimRunId).ConfigureAwait(false);

        string dedupeKey = $"sample-dedupe-{provider.ToLowerInvariant()}";
        string samplePayload = JsonSerializer.Serialize(new TestReportTaskPayload("dedupe", 1));
        Guid dedupeRunId = Guid.Parse($"66666666-6666-6666-6666-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        Guid duplicateDedupeRunId = Guid.Parse($"66666666-6666-6666-6666-{(provider == "SqlServer" ? "333333333333" : "444444444444")}");
        Error invalidPayloadError = (await application.EnqueueThroughApplicationAsync(
                Guid.Parse($"55555555-5555-5555-5555-{(provider == "SqlServer" ? "111111111111" : "222222222222")}"),
                "{invalid-json",
                Now.AddSeconds(50))
            .ConfigureAwait(false)).Error;
        await application.EnqueueThroughApplicationAsync(
                dedupeRunId,
                samplePayload,
                Now.AddHours(1),
                dedupeKey)
            .ConfigureAwait(false);
        await application.EnqueueThroughApplicationAsync(
                duplicateDedupeRunId,
                samplePayload,
                Now.AddHours(1).AddSeconds(1),
                dedupeKey)
            .ConfigureAwait(false);
        IReadOnlyList<TaskRunSummary> dedupedRuns = (await application.ListThroughApplicationAsync(dedupeKey)
                .ConfigureAwait(false))
            .Value;
        Result<TaskRunStats> stats = await application.GetStatsThroughApplicationAsync().ConfigureAwait(false);

        Guid renewalRunId = Guid.Parse($"23232323-2323-2323-2323-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(renewalRunId, Now.AddSeconds(52)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> renewalClaim = await application.ClaimAsync(
            "worker-renew-a",
            "node-renew-a",
            Now.AddSeconds(52),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        TaskExecutionContext renewalContext = renewalClaim.Single().CreateExecutionContext();
        await application.MarkStartedAsync(renewalContext, Now.AddSeconds(53)).ConfigureAwait(false);
        await application.ReportProgressAsync(
                renewalContext,
                new TaskProgress(10, "renewed"),
                Now.AddSeconds(56))
            .ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> blockedByRenewal = await application.ClaimAsync(
            "worker-renew-b",
            "node-renew-b",
            Now.AddSeconds(58),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> reclaimedAfterRenewedExpiry = await application.ClaimAsync(
            "worker-renew-b",
            "node-renew-b",
            Now.AddSeconds(62),
            TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        TaskExecutionContext renewedReclaimContext = reclaimedAfterRenewedExpiry.Single().CreateExecutionContext();
        await application.MarkStartedAsync(renewedReclaimContext, Now.AddSeconds(63)).ConfigureAwait(false);
        await application.MarkSucceededAsync(renewedReclaimContext, Now.AddSeconds(64)).ConfigureAwait(false);

        Guid versionedRunId = Guid.Parse($"44444444-4444-4444-4444-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        string versionedPayload = JsonSerializer.Serialize(new TestReportTaskPayloadV2("daily", 10, "csv"));
        await application.EnqueueSampleTaskAsync(
                versionedRunId,
                Now.AddSeconds(60),
            payloadVersion: TestReportTaskPayloadV2.PayloadVersion,
                payloadJson: versionedPayload)
            .ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> versionedClaim = await application.ClaimAsync(
            "worker-c",
            "node-c",
            Now.AddSeconds(60),
            TimeSpan.FromMinutes(15)).ConfigureAwait(false);

        Guid timeoutRunId = Guid.Parse($"33333333-3333-3333-3333-{(provider == "SqlServer" ? "111111111111" : "222222222222")}");
        await application.EnqueueSampleTaskAsync(timeoutRunId, Now.AddSeconds(70)).ConfigureAwait(false);
        IReadOnlyList<TaskRunLease> timeoutClaim = await application.ClaimAsync(
            "worker-d",
            "node-d",
            Now.AddSeconds(70),
            TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        TaskExecutionContext timeoutContext = timeoutClaim.Single().CreateExecutionContext();
        await application.MarkStartedAsync(timeoutContext, Now.AddSeconds(71)).ConfigureAwait(false);
        IReadOnlyList<TaskRunSummary> timedOutRuns = await application.MarkStaleTimedOutAsync(
            Now.AddMinutes(10),
            TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        TaskRunSnapshot timedOut = await application.GetSnapshotAsync(timeoutRunId).ConfigureAwait(false);
        Result<Gma.Framework.Cqrs.Unit> retryTimedOut = await application.RetryThroughApplicationAsync(
                timeoutRunId,
                "operator-retry",
                Now.AddMinutes(11))
            .ConfigureAwait(false);
        TaskRunSnapshot retried = await application.GetSnapshotAsync(timeoutRunId).ConfigureAwait(false);
        Result<Gma.Framework.Cqrs.Unit> cancelRetried = await application.CancelThroughApplicationAsync(
                timeoutRunId,
                "operator-cancel")
            .ConfigureAwait(false);
        TaskRunSnapshot canceledRetried = await application.GetSnapshotAsync(timeoutRunId).ConfigureAwait(false);
        Result<TaskControlMessage> controlMessage = await application.SendControlThroughApplicationAsync(
                versionedRunId,
                TaskControlCommandNames.Pause,
                "{}",
                Now.AddMinutes(20))
            .ConfigureAwait(false);
        Result<TaskControlMessage> terminalControlMessage = await application.SendControlThroughApplicationAsync(
                timeoutRunId,
                TaskControlCommandNames.Pause,
                "{}",
                Now.AddMinutes(20))
            .ConfigureAwait(false);

        Assert.Single(firstClaim);
        Assert.Empty(blockedClaim);
        Assert.Equal(TaskRunStatus.Leased, afterWrongWorker.Status);
        Assert.Single(controlMessages);
        Assert.Equal(25, afterFailure.ProgressPercent);
        Assert.Equal("warming up", afterFailure.ProgressMessage);
        Assert.Equal(TaskRunStatus.RetryScheduled, afterFailure.Status);
        Assert.Equal(1, afterFailure.Attempts);
        Assert.Equal(Now.AddSeconds(10), afterFailure.NextAttemptAtUtc);
        Assert.Empty(beforeRetryDue);
        Assert.Single(retryClaim);
        Assert.Equal(TaskRunStatus.Succeeded, afterSuccess.Status);
        Assert.Equal(2, afterSuccess.Attempts);
        Assert.Null(afterSuccess.LockedBy);
        Assert.Equal("operator", afterSuccess.RequestedBy);
        Assert.Single(reclaimed);
        Assert.Equal(reclaimRunId, reclaimedLease.RunId);
        Assert.Equal(TaskRunStatus.Canceled, canceledQueued.Status);
        Assert.Equal("operator", canceledQueued.CancellationRequestedBy);
        Assert.Empty(cancellationBlockedClaim);
        Assert.Single(cancellationReclaim);
        Assert.True(cancellationReclaim[0].CancellationRequested);
        Assert.True(cancellationReclaimContext.CancellationRequested);
        Assert.Equal(1, cancellationReclaim[0].Attempt);
        Assert.Equal(TaskRunStatus.Canceled, canceledReclaimed.Status);
        Assert.Null(canceledReclaimed.LockedBy);
        Assert.Equal(TaskRuntimeApplicationErrors.InvalidPayloadJson.Code, invalidPayloadError.Code);
        Assert.Single(dedupedRuns);
        Assert.Equal(dedupeRunId, dedupedRuns[0].RunId);
        Assert.True(stats.IsSuccess);
        Assert.True(stats.Value.Total >= 5);
        Assert.Single(renewalClaim);
        Assert.Empty(blockedByRenewal);
        Assert.Single(reclaimedAfterRenewedExpiry);
        Assert.Equal(renewalRunId, reclaimedAfterRenewedExpiry[0].RunId);
        Assert.Single(versionedClaim);
        Assert.Equal(versionedRunId, versionedClaim[0].RunId);
        Assert.Equal(TestReportTaskPayloadV2.PayloadVersion, versionedClaim[0].PayloadVersion);
        Assert.Contains(timedOutRuns, run => run.RunId == timeoutRunId);
        Assert.Equal(TaskRunStatus.TimedOut, timedOut.Status);
        Assert.True(retryTimedOut.IsSuccess);
        Assert.Equal(TaskRunStatus.Queued, retried.Status);
        Assert.Equal("operator-retry", retried.RequestedBy);
        Assert.True(cancelRetried.IsSuccess);
        Assert.Equal(TaskRunStatus.Canceled, canceledRetried.Status);
        Assert.Equal("operator-cancel", canceledRetried.CancellationRequestedBy);
        Assert.True(controlMessage.IsSuccess);
        Assert.Equal(TaskControlCommandNames.Pause, controlMessage.Value.CommandName);
        Assert.False(terminalControlMessage.IsSuccess);
        Assert.Equal(TaskRuntimeApplicationErrors.RunCannotBeControlled.Code, terminalControlMessage.Error.Code);
    }
}
