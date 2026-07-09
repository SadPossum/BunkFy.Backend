namespace TaskSamples.Application.Tasks;

internal sealed class NullTaskSampleReportSink : ITaskSampleReportSink
{
    public Task RecordAsync(TaskSampleReport report, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
