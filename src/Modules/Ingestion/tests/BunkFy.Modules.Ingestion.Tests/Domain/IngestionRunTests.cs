namespace BunkFy.Modules.Ingestion.Tests.Domain;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Domain.Errors;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_records_task_execution_identity_without_owning_its_lease()
    {
        Guid taskRunId = Guid.NewGuid();
        IngestionRun run = CreateRun(taskRunId, taskAttempt: 2);

        Assert.Equal(taskRunId, run.TaskRunId);
        Assert.Equal(2, run.TaskAttempt);
        Assert.Equal("cursor-1", run.StartingCheckpoint);
        Assert.Equal(IngestionRunState.Running, run.State);
    }

    [Fact]
    public void Completion_records_source_counts_and_checkpoint_once()
    {
        IngestionRun run = CreateRun(Guid.NewGuid(), taskAttempt: 1);

        Assert.True(run.Complete(
            AdapterRunOutcome.PartiallySucceeded,
            3,
            2,
            1,
            "cursor-2",
            null,
            1,
            Now.AddMinutes(1)).IsSuccess);

        Assert.Equal(IngestionRunState.PartiallySucceeded, run.State);
        Assert.Equal("cursor-2", run.AcceptedCheckpoint);
        Assert.Equal(2, run.Version);
        Assert.Equal(
            IngestionDomainErrors.RunNotActive,
            run.Complete(AdapterRunOutcome.Succeeded, 0, 0, 0, null, null, 2, Now.AddMinutes(2)).Error);
    }

    [Fact]
    public void Failed_completion_requires_an_error_message()
    {
        IngestionRun run = CreateRun(Guid.NewGuid(), taskAttempt: 1);

        Assert.Equal(
            IngestionDomainErrors.ErrorMessageInvalid,
            run.Complete(AdapterRunOutcome.Failed, 0, 0, 0, null, null, 1, Now).Error);
    }

    [Fact]
    public void Completion_rejects_counts_that_overflow_thirty_two_bit_arithmetic()
    {
        IngestionRun run = CreateRun(Guid.NewGuid(), taskAttempt: 1);

        Assert.Equal(
            IngestionDomainErrors.RunCountsInvalid,
            run.Complete(
                AdapterRunOutcome.Succeeded,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                null,
                null,
                1,
                Now).Error);
    }

    [Fact]
    public void Start_requires_a_real_task_run_and_positive_attempt()
    {
        Assert.Equal(
            IngestionDomainErrors.TaskExecutionInvalid,
            Start(Guid.Empty, 1).Error);
        Assert.Equal(
            IngestionDomainErrors.TaskExecutionInvalid,
            Start(Guid.NewGuid(), 0).Error);
    }

    private static IngestionRun CreateRun(Guid taskRunId, int taskAttempt) => Start(taskRunId, taskAttempt).Value;

    private static Gma.Framework.Results.Result<IngestionRun> Start(Guid taskRunId, int taskAttempt) => IngestionRun.Start(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        taskRunId,
        taskAttempt,
        "cursor-1",
        Now);
}
