namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;
using SelectedSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

[Trait("Category", "Unit")]
public sealed class ProcessDataRightsAnonymisationWorkItemCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_attempt_records_owner_proof_without_completing_case()
    {
        ExecutionFixture fixture = CreateFixture();
        MutableClock clock = new(Now);
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.ApprovedWithEvidence(
                fixture.Case.ToApprovalEvidence()!));
        BeginDataRightsAnonymisationWorkItemCommandHandler begin = new(
            fixture.Cases,
            fixture.WorkItems,
            gate,
            new RecordingOutboxRegistry(new RecordingOutbox()),
            clock,
            new TestIdGenerator());
        Guid taskRunId = Guid.NewGuid();

        Result<DataRightsAnonymisationWorkItemStart> started =
            await begin.HandleAsync(
                Command(fixture, taskRunId, taskAttempt: 1),
                CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.True(started.Value.DispatchRequired);
        Assert.Equal(fixture.WorkItem.Id, started.Value.Request!.WorkItemId);
        Assert.Equal("user:executor", started.Value.Request.ExecutingActorId);
        Assert.Equal(
            DataRightsExecutionWorkItemState.Processing,
            fixture.WorkItem.State);

        clock.UtcNow = Now.AddMinutes(1);
        DataRightsAnonymisationOwnerProof proof = new(
            ReceiptContractVersion: 1,
            Guid.NewGuid(),
            ResultingRecordVersion: fixture.WorkItem.SelectedRecordVersion + 1,
            "guests.completed",
            "guests.profile-anonymised",
            new string('c', 64),
            Now.AddSeconds(30));
        RecordDataRightsAnonymisationOwnerResultCommandHandler record = new(
            fixture.Cases,
            fixture.WorkItems,
            new RecordingOutboxRegistry(new RecordingOutbox()),
            clock,
            new TestIdGenerator());
        Result<Gma.Framework.Cqrs.Unit> recorded = await record.HandleAsync(
            new RecordDataRightsAnonymisationOwnerResultCommand(
                fixture.WorkItem.Id,
                fixture.Case.Id,
                DataRightsCaseScope.ForProperty(
                    fixture.WorkItem.PropertyId!.Value),
                fixture.WorkItem.ApprovalRevision,
                fixture.WorkItem.ExecutionRevision,
                taskRunId,
                TaskAttempt: 1,
                started.Value.WorkItemVersion,
                DataRightsAnonymisationContributionResult.Completed(proof)),
            CancellationToken.None);

        Assert.True(recorded.IsSuccess);
        Assert.Equal(
            DataRightsExecutionWorkItemState.OwnerProofRecorded,
            fixture.WorkItem.State);
        Assert.Equal(DataRightsCaseState.Executing, fixture.Case.Status);
        Assert.Equal(proof.ReceiptId, fixture.WorkItem.OwnerReceiptId);

        Result<DataRightsAnonymisationWorkItemStart> retry =
            await begin.HandleAsync(
                Command(fixture, taskRunId, taskAttempt: 2),
                CancellationToken.None);
        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value.DispatchRequired);
        Assert.Equal(1, gate.EvaluationCount);
    }

    [Fact]
    public async Task Approval_revalidation_denial_blocks_work_item_before_owner_dispatch()
    {
        ExecutionFixture fixture = CreateFixture();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.ApprovalRevisionMismatch));
        RecordingOutbox outbox = new();
        BeginDataRightsAnonymisationWorkItemCommandHandler begin = new(
            fixture.Cases,
            fixture.WorkItems,
            gate,
            new RecordingOutboxRegistry(outbox),
            new MutableClock(Now),
            new TestIdGenerator());

        Result<DataRightsAnonymisationWorkItemStart> result =
            await begin.HandleAsync(
                Command(fixture, Guid.NewGuid(), taskAttempt: 1),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.DispatchRequired);
        Assert.Equal(DataRightsCaseState.Executing, fixture.Case.Status);
        Assert.Equal(
            DataRightsExecutionWorkItemState.Blocked,
            fixture.WorkItem.State);
        Assert.Equal(
            "DataRights.ApprovalRevalidationDenied",
            fixture.WorkItem.OutcomeCode);
        Assert.IsType<DataRightsAnonymisationWorkItemTerminalIntegrationEvent>(
            Assert.Single(outbox.Events));
    }

    private static BeginDataRightsAnonymisationWorkItemCommand Command(
        ExecutionFixture fixture,
        Guid taskRunId,
        int taskAttempt) =>
        new(
            fixture.WorkItem.Id,
            fixture.Case.Id,
            DataRightsCaseScope.ForProperty(
                fixture.WorkItem.PropertyId!.Value),
            fixture.WorkItem.ApprovalRevision,
            fixture.WorkItem.ExecutionRevision,
            taskRunId,
            taskAttempt);

    private static ExecutionFixture CreateFixture()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            Now.AddMinutes(-8)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            Now.AddMinutes(-7)).IsSuccess);
        Guid guestId = Guid.NewGuid();
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            guestId,
            3,
            2,
            "user:operator",
            Now.AddMinutes(-6)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            Now.AddMinutes(-5)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:approver",
            Now.AddMinutes(-4)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                11,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-3)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:approver",
            Now.AddMinutes(-3),
            evidence).IsSuccess);
        Assert.True(dataRightsCase.BeginAnonymisationExecution(
            6,
            "user:executor",
            Now.AddMinutes(-2)).IsSuccess);

        SelectedSubject subject = Assert.Single(
            dataRightsCase.SelectedSubjects);
        DataRightsExecutionWorkItem workItem =
            DataRightsExecutionWorkItem.Prepare(
                Guid.NewGuid(),
                dataRightsCase.ScopeId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                dataRightsCase.Id,
                DataRightsExecutionScope.ForProperty(propertyId),
                approvalRevision: 6,
                dataRightsCase.ExecutionRevision!.Value,
                DataRightsCaseOperation.Anonymisation,
                subject,
                evidence,
                "user:executor",
                Now.AddMinutes(-2)).Value;
        StubCaseRepository cases = new(dataRightsCase);
        StubWorkItemRepository workItems = new(workItem);
        return new(dataRightsCase, workItem, cases, workItems);
    }

    private sealed record ExecutionFixture(
        DataRightsCase Case,
        DataRightsExecutionWorkItem WorkItem,
        StubCaseRepository Cases,
        StubWorkItemRepository WorkItems);

    private sealed class StubCaseRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase ignored,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                dataRightsCase.PropertyId == scope.PropertyId &&
                dataRightsCase.Kind == (DataRightsCaseKind)scope.CaseType &&
                dataRightsCase.Id == caseId
                    ? dataRightsCase
                    : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            DataRightsCaseScope scope,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubWorkItemRepository(DataRightsExecutionWorkItem workItem)
        : IDataRightsExecutionWorkItemRepository
    {
        public Task AddAsync(
            DataRightsExecutionWorkItem ignored,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            Guid batchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                (IReadOnlyCollection<DataRightsExecutionWorkItem>)(
                    workItem.CaseKind ==
                        (DataRightsCaseKind)scope.CaseType &&
                    workItem.PropertyId == scope.PropertyId &&
                    workItem.CaseId == caseId &&
                    workItem.BatchId == batchId
                        ? [workItem]
                        : []));

        public Task<DataRightsExecutionWorkItem?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                workItem.CaseKind ==
                    (DataRightsCaseKind)scope.CaseType &&
                workItem.PropertyId == scope.PropertyId &&
                workItem.CaseId == caseId &&
                workItem.Id == workItemId
                    ? workItem
                    : null);
    }

    private sealed class RecordingApprovalGate(DataRightsOperationApprovalResult result)
        : IDataRightsOperationApprovalGate
    {
        public int EvaluationCount { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public List<object> Events { get; } = [];
        public string ModuleName => DataRightsModuleMetadata.Name;

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName)
        {
            Assert.Equal(DataRightsModuleMetadata.Name, moduleName);
            return outbox;
        }
    }
}
