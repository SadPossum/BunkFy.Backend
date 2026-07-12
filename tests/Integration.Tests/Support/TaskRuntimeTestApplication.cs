namespace Integration.Tests.Support;

using System.Text.Json;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Tasks;
using Gma.Modules.TaskRuntime.Application;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using Gma.Modules.TaskRuntime.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class TaskRuntimeTestApplication : IAsyncDisposable
{
    private readonly string provider;
    private readonly string connectionString;
    private readonly IHost host;

    public TaskRuntimeTestApplication(
        string provider,
        string connectionString,
        bool workerEnabled,
        DateTimeOffset? clockUtcNow = null,
        string workerId = "worker-test",
        string nodeId = "node-test",
        TimeSpan? leaseDuration = null,
        TimeSpan? heartbeatInterval = null,
        int maxConcurrency = 5,
        int batchSize = 5,
        bool retentionEnabled = false)
    {
        this.provider = provider;
        this.connectionString = connectionString;
        this.Sink = new RecordingTestTaskReportSink();

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Integration",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = provider,
            ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? connectionString : string.Empty,
            ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? connectionString : string.Empty,
            ["Tenancy:Enabled"] = "true",
            ["Tasks:Worker:Enabled"] = workerEnabled.ToString(),
            ["Tasks:Worker:WorkerGroups:0"] = IntegrationTestTasks.WorkerGroup,
            ["Tasks:Worker:BatchSize"] = batchSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Tasks:Worker:MaxConcurrency"] = maxConcurrency.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Tasks:Worker:PollInterval"] = "00:00:00.100",
            ["Tasks:Worker:LeaseDuration"] = (leaseDuration ?? TimeSpan.FromSeconds(5)).ToString("c"),
            ["Tasks:Worker:HeartbeatInterval"] = heartbeatInterval?.ToString("c"),
            ["Tasks:Worker:HandlerTimeout"] = "00:00:05",
            ["Tasks:Worker:RetryBaseDelay"] = "00:00:00.100",
            ["Tasks:Worker:RetryMaxDelay"] = "00:00:01",
            ["Tasks:Worker:WorkerId"] = workerId,
            ["Tasks:Worker:NodeId"] = nodeId,
            ["TaskRuntimeRetention:Enabled"] = retentionEnabled.ToString(),
            ["TaskRuntimeRetention:CleanupInterval"] = "00:01:00",
            ["TaskRuntimeRetention:BatchSize"] = "100",
            ["TaskRuntimeRetention:MaxBatchesPerStatusPerCycle"] = "1",
        });
        builder.Logging.ClearProviders();
        if (clockUtcNow is not null)
        {
            builder.Services.AddSingleton<ISystemClock>(new FixedClock(clockUtcNow.Value));
        }

        builder.Services.AddTaskRuntimeApplication();
        builder.AddTenancyInfrastructure();
        builder.AddTenantTaskExecutionContext();
        builder.AddTaskRuntimePersistence();
        builder.AddTaskCqrs();
        builder.AddTaskWorkerRuntime();
        builder.Services.AddSingleton(this.Sink);
        builder.Services.AddSingleton<ITestTaskReportSink>(this.Sink);
        builder.Services.AddIntegrationTestTasks();

        this.host = builder.Build();
    }

    public RecordingTestTaskReportSink Sink { get; }

    public IServiceProvider Services => this.host.Services;

    public Task StartAsync() => this.host.StartAsync();

    public Task StopAsync() => this.host.StopAsync();

    public async Task MigrateDatabaseAsync()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = this.provider,
                ["ConnectionStrings:SqlServer"] = this.provider == "SqlServer" ? this.connectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = this.provider == "PostgreSql" ? this.connectionString : string.Empty,
            })
            .Build();
        DbContextOptionsBuilder<TaskRuntimeDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            TaskRuntimeMigrations.SqlServerAssembly,
            TaskRuntimeMigrations.PostgreSqlAssembly,
            TaskRuntimeMigrations.Schema,
            TaskRuntimeMigrations.HistoryTable);

        await using TaskRuntimeDbContext dbContext = new(options.Options);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task EnqueueSampleTaskAsync(
        Guid runId,
        DateTimeOffset createdAtUtc,
        int maxAttempts = 3,
        int payloadVersion = TestReportTaskPayload.PayloadVersion,
        string? deduplicationKey = null,
        string? payloadJson = null,
        string taskName = TestReportTaskPayload.TaskName)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        string payload = payloadJson ?? JsonSerializer.Serialize(new TestReportTaskPayload("daily", 10));

        await store.EnqueueAsync(
                new TaskRunRequest(
                    runId,
                    IntegrationTestTasks.ModuleName,
                    taskName,
                    payload,
                    createdAtUtc,
                    createdAtUtc,
                    IntegrationTestTasks.WorkerGroup,
                    scopeId: "tenant-a",
                    requestedBy: "operator",
                    maxAttempts: maxAttempts,
                    payloadVersion: payloadVersion,
                    deduplicationKey: deduplicationKey),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunLease>> ClaimAsync(
        string workerId,
        string nodeId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();

        return await store.ClaimReadyAsync(
                new TaskWorkerClaim(
                    IntegrationTestTasks.WorkerGroup,
                    workerId,
                    nodeId,
                    nowUtc,
                    maxRuns: 10,
                    leaseDuration),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task MarkStartedAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkStartedAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkSucceededAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkSucceededAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkCanceledAsync(TaskExecutionContext context, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkCanceledAsync(context, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        TaskExecutionContext context,
        string error,
        DateTimeOffset failedAtUtc,
        DateTimeOffset? retryAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.MarkFailedAsync(context, error, failedAtUtc, retryAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task ReportProgressAsync(TaskExecutionContext context, TaskProgress progress, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.ReportProgressAsync(context, progress, nowUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task RequestCancellationAsync(Guid runId, string? requestedBy, DateTimeOffset requestedAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.RequestCancellationAsync(runId, requestedBy, requestedAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task RetryAsync(Guid runId, string? requestedBy, DateTimeOffset scheduledAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.RetryAsync(runId, requestedBy, scheduledAtUtc, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
        DateTimeOffset nowUtc,
        TimeSpan staleAfter,
        int maxRuns = 100)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        return await store.MarkStaleTimedOutAsync(nowUtc, staleAfter, maxRuns, CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<TaskRunDetails>> EnqueueThroughApplicationAsync(
        Guid runId,
        string payloadJson,
        DateTimeOffset scheduledAtUtc,
        string? deduplicationKey = null,
        int payloadVersion = TestReportTaskPayload.PayloadVersion)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(
                new EnqueueTaskRunCommand(
                    runId,
                    IntegrationTestTasks.ModuleName,
                    TestReportTaskPayload.TaskName,
                    payloadJson,
                    scheduledAtUtc,
                    IntegrationTestTasks.WorkerGroup,
                    "tenant-a",
                    null,
                    "operator",
                    3,
                    payloadVersion,
                    deduplicationKey),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<Unit>> CancelThroughApplicationAsync(Guid runId, string? requestedBy)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(new CancelTaskRunCommand(runId, requestedBy), CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<Unit>> RetryThroughApplicationAsync(
        Guid runId,
        string? requestedBy,
        DateTimeOffset scheduledAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
                new RetryTaskRunCommand(runId, requestedBy, scheduledAtUtc),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<TaskRunSummary>>> ListThroughApplicationAsync(
        string? deduplicationKey = null)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        Result<IReadOnlyList<TaskRunSummary>> result = await dispatcher.QueryAsync(
                new ListTaskRunsQuery(
                    IntegrationTestTasks.ModuleName,
                    TestReportTaskPayload.TaskName,
                    IntegrationTestTasks.WorkerGroup,
                    null,
                    "tenant-a",
                    1,
                    50),
                CancellationToken.None)
            .ConfigureAwait(false);

        return result.IsFailure || string.IsNullOrWhiteSpace(deduplicationKey)
            ? result
            : Result.Success<IReadOnlyList<TaskRunSummary>>(
                result.Value.Where(run => run.DeduplicationKey == deduplicationKey).ToArray());
    }

    public async Task<Result<TaskRunStats>> GetStatsThroughApplicationAsync()
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.QueryAsync(
                new GetTaskRunStatsQuery(
                    IntegrationTestTasks.ModuleName,
                    null,
                    IntegrationTestTasks.WorkerGroup,
                    "tenant-a"),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<Result<TaskControlMessage>> SendControlThroughApplicationAsync(
        Guid runId,
        string commandName,
        string payloadJson,
        DateTimeOffset? expiresAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(
                new SendTaskControlMessageCommand(
                    runId,
                    commandName,
                    payloadJson,
                    expiresAtUtc,
                    "operator-control"),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskControlMessage>> ReadPendingControlAsync(TaskExecutionContext context)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        return await store.ReadPendingAsync(context, 10, CancellationToken.None).ConfigureAwait(false);
    }

    public async Task EnqueueControlAsync(Guid messageId, Guid runId, DateTimeOffset nowUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITaskRunStore store = scope.ServiceProvider.GetRequiredService<ITaskRunStore>();
        await store.EnqueueControlMessageAsync(
                new TaskControlMessage(
                    messageId,
                    runId,
                    "tasks.pause",
                    "{}",
                    nowUtc,
                    "operator",
                    nowUtc.AddMinutes(5)),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<TaskRunSnapshot> GetSnapshotAsync(Guid runId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        TaskRuntimeDbContext dbContext = scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>();
        TaskRunSnapshot? snapshot = await dbContext.TaskRuns
            .Where(taskRun => taskRun.Id == runId)
            .Select(taskRun => new TaskRunSnapshot(
                taskRun.Id,
                taskRun.Status,
                taskRun.LockedBy,
                taskRun.NodeId,
                taskRun.Attempts,
                taskRun.NextAttemptAtUtc,
                taskRun.CompletedAtUtc,
                taskRun.LastError,
                taskRun.ProgressPercent,
                taskRun.ProgressMessage,
                taskRun.RequestedBy,
                taskRun.CancellationRequestedBy,
                taskRun.CancellationRequestedAtUtc,
                taskRun.PayloadVersion,
                taskRun.DeduplicationKey))
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);

        Xunit.Assert.NotNull(snapshot);
        return snapshot;
    }

    public async Task AddPendingControlAsync(Guid messageId, Guid runId, DateTimeOffset enqueuedAtUtc)
    {
        using IServiceScope scope = this.Services.CreateScope();
        TaskRuntimeDbContext dbContext = scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>();
        dbContext.TaskControlMessages.Add(TaskControlMessageState.Enqueue(new TaskControlMessage(
            messageId,
            runId,
            "tasks.pause",
            "{}",
            enqueuedAtUtc,
            "operator",
            enqueuedAtUtc.AddYears(1))));
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<bool> TaskRunExistsAsync(Guid runId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        TaskRuntimeDbContext dbContext = scope.ServiceProvider.GetRequiredService<TaskRuntimeDbContext>();
        return await dbContext.TaskRuns.AnyAsync(run => run.Id == runId).ConfigureAwait(false);
    }

    public async Task<bool> WaitForTaskRunDeletionAsync(Guid runId, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!await this.TaskRunExistsAsync(runId).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return !await this.TaskRunExistsAsync(runId).ConfigureAwait(false);
    }

    public async Task<TaskRunSnapshot> WaitForStatusAsync(
        Guid runId,
        TaskRunStatus expectedStatus,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            TaskRunSnapshot snapshot = await this.GetSnapshotAsync(runId).ConfigureAwait(false);
            if (snapshot.Status == expectedStatus)
            {
                return snapshot;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return await this.GetSnapshotAsync(runId).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await this.host.StopAsync().ConfigureAwait(false);
        this.host.Dispose();
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

internal sealed class RecordingTestTaskReportSink : ITestTaskReportSink
{
    private readonly List<TestTaskReport> reports = [];

    public IReadOnlyList<TestTaskReport> Reports
    {
        get
        {
            lock (this.reports)
            {
                return this.reports.ToArray();
            }
        }
    }

    public Task RecordAsync(TestTaskReport report, CancellationToken cancellationToken)
    {
        lock (this.reports)
        {
            this.reports.Add(report);
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TestTaskReport>> WaitForReportsAsync(int expectedCount, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            IReadOnlyList<TestTaskReport> current = this.Reports;
            if (current.Count >= expectedCount)
            {
                return current;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        return this.Reports;
    }
}
