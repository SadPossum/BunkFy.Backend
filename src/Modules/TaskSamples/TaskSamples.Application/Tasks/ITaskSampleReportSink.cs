namespace TaskSamples.Application.Tasks;

public interface ITaskSampleReportSink
{
    Task RecordAsync(TaskSampleReport report, CancellationToken cancellationToken);
}
