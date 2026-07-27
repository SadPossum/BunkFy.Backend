namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GenerateDataRightsExportTaskHandlerTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Task_generates_and_records_protected_artifact()
    {
        DataRightsExportGenerationStart start = Start(dispatchRequired: true);
        DataRightsProtectedExportArtifact generated = Protected();
        FakeTaskDispatcher dispatcher = new(start);
        RecordingGenerator generator = new(generated);
        GenerateDataRightsExportTaskHandler handler = new(
            dispatcher,
            generator,
            new RecordingObjectStore());
        TaskExecutionContext context = Context();

        await handler.HandleAsync(
            Payload(start),
            context,
            CancellationToken.None);

        Assert.NotNull(generator.Request);
        CompleteDataRightsExportGenerationCommand completed =
            Assert.IsType<CompleteDataRightsExportGenerationCommand>(
                dispatcher.Completed);
        Assert.Equal(context.RunId, completed.RunId);
        Assert.Equal(context.Attempt, completed.Attempt);
        Assert.Same(generated, completed.ProtectedArtifact);
        Assert.Null(dispatcher.Failed);
    }

    [Fact]
    public async Task Available_retry_does_not_generate_again()
    {
        DataRightsExportGenerationStart start = Start(dispatchRequired: false);
        FakeTaskDispatcher dispatcher = new(start);
        RecordingGenerator generator = new(Protected());
        GenerateDataRightsExportTaskHandler handler = new(
            dispatcher,
            generator,
            new RecordingObjectStore());

        await handler.HandleAsync(
            Payload(start),
            Context(),
            CancellationToken.None);

        Assert.Null(generator.Request);
        Assert.Null(dispatcher.Completed);
        Assert.Null(dispatcher.Failed);
    }

    [Fact]
    public async Task Generation_failure_records_bounded_code_for_retry()
    {
        DataRightsExportGenerationStart start = Start(dispatchRequired: true);
        FakeTaskDispatcher dispatcher = new(start);
        RecordingGenerator generator = new("subject-stale");
        RecordingObjectStore objectStore = new();
        GenerateDataRightsExportTaskHandler handler = new(
            dispatcher,
            generator,
            objectStore);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.HandleAsync(
                    Payload(start),
                    Context(),
                    CancellationToken.None));

        Assert.Contains("subject-stale", exception.Message);
        FailDataRightsExportGenerationCommand failed =
            Assert.IsType<FailDataRightsExportGenerationCommand>(
                dispatcher.Failed);
        Assert.Equal("subject-stale", failed.FailureCode);
        Assert.Null(dispatcher.Completed);
        Assert.Equal(start.ArtifactId, objectStore.DeletedArtifactId);
    }

    [Fact]
    public async Task Generation_cancellation_removes_partial_object()
    {
        DataRightsExportGenerationStart start = Start(dispatchRequired: true);
        RecordingObjectStore objectStore = new();
        GenerateDataRightsExportTaskHandler handler = new(
            new FakeTaskDispatcher(start),
            new CancellingGenerator(),
            objectStore);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                Payload(start),
                Context(),
                CancellationToken.None));

        Assert.Equal(start.ArtifactId, objectStore.DeletedArtifactId);
    }

    [Fact]
    public async Task Database_completion_failure_removes_written_object()
    {
        DataRightsExportGenerationStart start = Start(dispatchRequired: true);
        FakeTaskDispatcher dispatcher = new(start, completeFails: true);
        RecordingObjectStore objectStore = new();
        GenerateDataRightsExportTaskHandler handler = new(
            dispatcher,
            new RecordingGenerator(Protected()),
            objectStore);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                Payload(start),
                Context(),
                CancellationToken.None));

        Assert.Equal(start.ArtifactId, objectStore.DeletedArtifactId);
    }

    private static DataRightsExportGenerationStart Start(
        bool dispatchRequired)
    {
        Guid artifactId = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        return new(
            dispatchRequired,
            artifactId,
            "tenant-a",
            caseId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            DecisionRevision: 7,
            [new("staff", "profile", Guid.NewGuid(), 3)],
            GeneratedAt,
            GeneratedAt.AddHours(24));
    }

    private static DataRightsProtectedExportArtifact Protected() => new(
        "data-rights/exports/scope-1234/artifact-1234.bfdrx",
        EncryptedByteLength: 100,
        PlaintextSha256: new string('a', 64),
        EncryptionKeyVersion: 1,
        FormatVersion: 1,
        GeneratedAt.AddMinutes(1),
        GeneratedAt.AddHours(24));

    private static GenerateDataRightsExportPayload Payload(
        DataRightsExportGenerationStart start) => new(
            start.ArtifactId,
            start.CaseId,
            start.CaseType,
            start.PropertyId,
            start.DecisionRevision);

    private static TaskExecutionContext Context() => new(
        Guid.NewGuid(),
        DataRightsModuleMetadata.Name,
        GenerateDataRightsExportPayload.TaskName,
        DataRightsModuleMetadata.ExportWorkerGroup,
        "worker-1",
        "node-1",
        attempt: 1,
        scopeId: "tenant-a",
        correlationId: Guid.NewGuid());

    private sealed class FakeTaskDispatcher(
        DataRightsExportGenerationStart start,
        bool completeFails = false)
        : ITaskCommandDispatcher
    {
        public object? Completed { get; private set; }
        public object? Failed { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                BeginDataRightsExportGenerationCommand =>
                    Result.Success(start),
                CompleteDataRightsExportGenerationCommand completed =>
                    this.Complete(completed),
                FailDataRightsExportGenerationCommand failed =>
                    this.Fail(failed),
                _ => throw new InvalidOperationException(
                    $"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<Unit> Complete(
            CompleteDataRightsExportGenerationCommand command)
        {
            this.Completed = command;
            return completeFails
                ? Result.Failure<Unit>(
                    new Error(
                        "DataRights.ExportCompletionConflict",
                        "The export completion conflicted."))
                : Result.Success(Unit.Value);
        }

        private Result<Unit> Fail(
            FailDataRightsExportGenerationCommand command)
        {
            this.Failed = command;
            return Result.Success(Unit.Value);
        }
    }

    private sealed class RecordingGenerator : IDataRightsExportArtifactGenerator
    {
        private readonly DataRightsProtectedExportArtifact? result;
        private readonly string? failureCode;

        public RecordingGenerator(DataRightsProtectedExportArtifact result) =>
            this.result = result;

        public RecordingGenerator(string failureCode) =>
            this.failureCode = failureCode;

        public DataRightsExportGenerationRequest? Request { get; private set; }

        public Task<DataRightsProtectedExportArtifact> GenerateAsync(
            DataRightsExportGenerationRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            if (this.failureCode is not null)
            {
                throw new DataRightsExportGenerationException(this.failureCode);
            }

            return Task.FromResult(this.result!);
        }
    }

    private sealed class RecordingObjectStore
        : IDataRightsExportArtifactObjectStore
    {
        public Guid? DeletedArtifactId { get; private set; }

        public Task<bool> DeleteAsync(
            Guid artifactId,
            CancellationToken cancellationToken)
        {
            this.DeletedArtifactId = artifactId;
            return Task.FromResult(true);
        }
    }

    private sealed class CancellingGenerator
        : IDataRightsExportArtifactGenerator
    {
        public Task<DataRightsProtectedExportArtifact> GenerateAsync(
            DataRightsExportGenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<DataRightsProtectedExportArtifact>(
                new CancellationToken(canceled: true));
    }
}
