namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ExecuteDataRightsAnonymisationTaskHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Task_dispatches_exact_contributor_and_records_its_result()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        DataRightsAnonymisationContributionResult contribution =
            DataRightsAnonymisationContributionResult.Completed(
                new DataRightsAnonymisationOwnerProof(
                    ReceiptContractVersion: 1,
                    Guid.NewGuid(),
                    ResultingRecordVersion: request.Coordinate.RecordVersion + 1,
                    "guests.completed",
                    "guests.profile-anonymised",
                    new string('d', 64),
                    Now.AddSeconds(30)));
        FakeTaskDispatcher dispatcher = new(
            DataRightsAnonymisationWorkItemStart.Ready(
                workItemVersion: 2,
                request));
        RecordingContributor contributor = new(contribution);
        ExecuteDataRightsAnonymisationTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock());
        TaskExecutionContext context = CreateContext();

        await handler.HandleAsync(
            Payload(request),
            context,
            CancellationToken.None);

        Assert.Same(request, contributor.Request);
        RecordDataRightsAnonymisationOwnerResultCommand recorded =
            Assert.IsType<RecordDataRightsAnonymisationOwnerResultCommand>(
                dispatcher.Recorded);
        Assert.Equal(context.RunId, recorded.TaskRunId);
        Assert.Equal(context.Attempt, recorded.TaskAttempt);
        Assert.Equal(2, recorded.ExpectedWorkItemVersion);
        Assert.Same(contribution, recorded.Result);
        FinalizeDataRightsAnonymisationLedgerCommand finalized =
            Assert.IsType<FinalizeDataRightsAnonymisationLedgerCommand>(
                dispatcher.Finalized);
        Assert.Equal(context.RunId, finalized.TaskRunId);
    }

    [Fact]
    public async Task Terminal_retry_does_not_call_owner_again()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        FakeTaskDispatcher dispatcher = new(
            DataRightsAnonymisationWorkItemStart.Terminal(workItemVersion: 3));
        RecordingContributor contributor = new(
            DataRightsAnonymisationContributionResult.Failed("not-used"));
        ExecuteDataRightsAnonymisationTaskHandler handler = new(
            dispatcher,
            [contributor],
            new TestClock());

        await handler.HandleAsync(
            Payload(request),
            CreateContext(),
            CancellationToken.None);

        Assert.Null(contributor.Request);
        Assert.Null(dispatcher.Recorded);
        Assert.IsType<FinalizeDataRightsAnonymisationLedgerCommand>(
            dispatcher.Finalized);
    }

    [Fact]
    public async Task Missing_owner_contributor_fails_for_task_runtime_retry()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        FakeTaskDispatcher dispatcher = new(
            DataRightsAnonymisationWorkItemStart.Ready(
                workItemVersion: 2,
                request));
        ExecuteDataRightsAnonymisationTaskHandler handler = new(
            dispatcher,
            [],
            new TestClock());

        InvalidOperationException exception = await Assert.ThrowsAsync<
            InvalidOperationException>(() => handler.HandleAsync(
                Payload(request),
                CreateContext(),
                CancellationToken.None));

        Assert.Equal(
            "DataRights.AnonymisationOwnerContributorUnavailable",
            exception.Message);
        Assert.Null(dispatcher.Recorded);
    }

    private static ExecuteDataRightsAnonymisationPayload Payload(
        DataRightsAnonymisationContributionRequest request) =>
        new(
            request.WorkItemId,
            request.CaseId,
            request.RoutingPropertyId,
            request.ApprovalRevision,
            request.OperationRevision);

    private static DataRightsAnonymisationContributionRequest CreateRequest()
    {
        Guid propertyId = Guid.NewGuid();
        return new(
            DataRightsAnonymisationContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId,
            Guid.NewGuid(),
            ApprovalRevision: 6,
            OperationRevision: 7,
            new DataRightsSubjectCoordinate(
                "guests",
                "guest-profile",
                Guid.NewGuid(),
                RecordVersion: 3),
            new DataRightsApprovalEvidence(
                SchemaVersion: 1,
                propertyId,
                PropertyVersion: 11,
                "GB",
                "approved-policy",
                PolicyVersion: 3,
                "guest-retention",
                RetentionPolicyVersion: 2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-1),
                RequiresDistinctExecutor: true),
            "user:executor",
            Now.AddMinutes(2));
    }

    private static TaskExecutionContext CreateContext() =>
        new(
            Guid.NewGuid(),
            DataRightsModuleMetadata.Name,
            ExecuteDataRightsAnonymisationPayload.TaskName,
            DataRightsModuleMetadata.AnonymisationWorkerGroup,
            "worker-1",
            "node-1",
            attempt: 1,
            scopeId: "tenant-a",
            correlationId: Guid.NewGuid());

    private sealed class FakeTaskDispatcher(DataRightsAnonymisationWorkItemStart start)
        : ITaskCommandDispatcher
    {
        public object? Recorded { get; private set; }
        public object? Finalized { get; private set; }

        public Task<Result<TResponse>> DispatchAsync<TCommand, TResponse>(
            TaskExecutionContext context,
            TCommand command,
            CancellationToken cancellationToken)
            where TCommand : ICommand<TResponse>
        {
            object result = command switch
            {
                BeginDataRightsAnonymisationWorkItemCommand =>
                    Result.Success(start),
                RecordDataRightsAnonymisationOwnerResultCommand recorded =>
                    this.Record(recorded),
                FinalizeDataRightsAnonymisationLedgerCommand finalized =>
                    this.Finalize(finalized),
                _ => throw new InvalidOperationException(
                    $"Unexpected command {command.GetType().Name}.")
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        private Result<Unit> Record(
            RecordDataRightsAnonymisationOwnerResultCommand command)
        {
            this.Recorded = command;
            return Result.Success(Unit.Value);
        }

        private Result<Unit> Finalize(
            FinalizeDataRightsAnonymisationLedgerCommand command)
        {
            this.Finalized = command;
            return Result.Success(Unit.Value);
        }
    }

    private sealed class RecordingContributor(
        DataRightsAnonymisationContributionResult result)
        : IDataRightsAnonymisationContributor
    {
        public string OwnerKey => "guests";
        public int ContractVersion => DataRightsAnonymisationContract.CurrentVersion;
        public DataRightsAnonymisationContributionRequest? Request { get; private set; }

        public Task<DataRightsAnonymisationContributionResult> ExecuteAsync(
            DataRightsAnonymisationContributionRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(result);
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
