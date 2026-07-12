namespace Integration.Tests.Support;

using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Framework.Tasks;
using Microsoft.Extensions.DependencyInjection;

internal static class IntegrationTestTasks
{
    public const string ModuleName = "integration-tests";
    public const string WorkerGroup = "integration-tests";

    public static IServiceCollection AddIntegrationTestTasks(this IServiceCollection services)
    {
        services.AddTaskHandler<TestReportTaskPayload, TestReportTaskHandler>(ModuleName);
        services.AddTaskHandler<TestReportTaskPayloadV2, TestReportTaskV2Handler>(ModuleName);
        services.AddTaskHandler<TestSlowTaskPayload, TestSlowTaskHandler>(ModuleName);
        services.AddTaskHandler<TestQuietTaskPayload, TestQuietTaskHandler>(ModuleName);
        return services;
    }
}

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Integration-test report task.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IntegrationTestTasks.WorkerGroup)]
[ScopeAware]
internal sealed record TestReportTaskPayload(string ReportName, int ExpectedRows) : ITaskPayload
{
    public const string TaskName = "test-report";
    public const int PayloadVersion = 1;
}

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Versioned integration-test report task.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IntegrationTestTasks.WorkerGroup)]
[ScopeAware]
internal sealed record TestReportTaskPayloadV2(
    string ReportName,
    int ExpectedRows,
    string Format) : ITaskPayload
{
    public const string TaskName = TestReportTaskPayload.TaskName;
    public const int PayloadVersion = 2;
}

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Controllable integration-test task.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IntegrationTestTasks.WorkerGroup)]
[SupportsTaskControl]
[ScopeAware]
internal sealed record TestSlowTaskPayload(
    string ReportName,
    int ExpectedRows,
    int Steps,
    int DelayMilliseconds) : ITaskPayload
{
    public const string TaskName = "test-slow-report";
    public const int PayloadVersion = 1;
}

[TaskName(TaskName)]
[TaskPayloadVersion(PayloadVersion)]
[TaskDescription("Long-running integration-test task without cooperative progress reports.")]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup(IntegrationTestTasks.WorkerGroup)]
[ScopeAware]
internal sealed record TestQuietTaskPayload(
    string ReportName,
    int DelayMilliseconds) : ITaskPayload
{
    public const string TaskName = "test-quiet-report";
    public const int PayloadVersion = 1;
}

internal sealed record TestTaskReport(
    string ReportName,
    int ExpectedRows,
    Guid RunId,
    string ScopeId,
    int Attempt);

internal interface ITestTaskReportSink
{
    Task RecordAsync(TestTaskReport report, CancellationToken cancellationToken);
}

internal sealed class TestReportTaskHandler(ITestTaskReportSink sink) : ITaskHandler<TestReportTaskPayload>
{
    public Task HandleAsync(TestReportTaskPayload payload, TaskExecutionContext context,
        CancellationToken cancellationToken) => sink.RecordAsync(new TestTaskReport(
        payload.ReportName,
        payload.ExpectedRows,
        context.RunId,
        context.ScopeId ?? string.Empty,
        context.Attempt), cancellationToken);
}

internal sealed class TestReportTaskV2Handler(ITestTaskReportSink sink) : ITaskHandler<TestReportTaskPayloadV2>
{
    public Task HandleAsync(TestReportTaskPayloadV2 payload, TaskExecutionContext context,
        CancellationToken cancellationToken) => sink.RecordAsync(new TestTaskReport(
        $"{payload.ReportName}.{payload.Format}",
        payload.ExpectedRows,
        context.RunId,
        context.ScopeId ?? string.Empty,
        context.Attempt), cancellationToken);
}

internal sealed class TestSlowTaskHandler(
    ITestTaskReportSink sink,
    ITaskRuntimeReporter reporter,
    ITaskControlLoop controlLoop,
    ISystemClock clock) : ITaskHandler<TestSlowTaskPayload>
{
    public async Task HandleAsync(TestSlowTaskPayload payload, TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        int steps = Math.Clamp(payload.Steps, 1, 20);
        int delayMilliseconds = Math.Clamp(payload.DelayMilliseconds, 0, 5_000);

        for (int step = 1; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await controlLoop.PauseIfRequestedAsync(
                context,
                TimeSpan.FromMilliseconds(100),
                maxMessages: 10,
                cancellationToken).ConfigureAwait(false);

            if (delayMilliseconds > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken)
                    .ConfigureAwait(false);
            }

            await reporter.ReportProgressAsync(
                context,
                new TaskProgress(step * 100 / steps, $"step {step}/{steps}"),
                clock.UtcNow,
                cancellationToken).ConfigureAwait(false);
        }

        await sink.RecordAsync(new TestTaskReport(
            payload.ReportName,
            payload.ExpectedRows,
            context.RunId,
            context.ScopeId ?? string.Empty,
            context.Attempt), cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class TestQuietTaskHandler(ITestTaskReportSink sink) : ITaskHandler<TestQuietTaskPayload>
{
    public async Task HandleAsync(
        TestQuietTaskPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(payload.DelayMilliseconds), cancellationToken)
            .ConfigureAwait(false);
        await sink.RecordAsync(new TestTaskReport(
            payload.ReportName,
            0,
            context.RunId,
            context.ScopeId ?? string.Empty,
            context.Attempt), cancellationToken).ConfigureAwait(false);
    }
}
