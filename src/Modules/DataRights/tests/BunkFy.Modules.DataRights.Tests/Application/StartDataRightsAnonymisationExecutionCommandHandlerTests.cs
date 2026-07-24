namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StartDataRightsAnonymisationExecutionCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_prepares_one_immutable_work_item_from_server_owned_coordinates()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        StubWorkItemRepository workItems = new();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.ApprovedWithEvidence(
                dataRightsCase.ToApprovalEvidence()!));
        Guid workItemId = Guid.NewGuid();
        Guid idempotencyKey = Guid.NewGuid();
        StartDataRightsAnonymisationExecutionCommandHandler handler = CreateHandler(
            dataRightsCase,
            workItems,
            gate,
            workItemId);

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                dataRightsCase.PropertyId!.Value,
                dataRightsCase.Id,
                idempotencyKey,
                6,
                "user:executor"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsCaseStatus.Executing, result.Value.Case.Status);
        Assert.Equal(7, result.Value.Case.ExecutionRevision);
        Assert.Equal(DataRightsExecutionWorkItemStatus.Prepared, result.Value.WorkItem.Status);
        Assert.Equal(workItemId, result.Value.WorkItem.Id);
        Assert.Same(workItems.Item, Assert.Single(workItems.Added));
        Assert.True(workItems.Item!.HasIdempotencyKey(idempotencyKey));
        DataRightsOperationApprovalRequest request = Assert.IsType<DataRightsOperationApprovalRequest>(
            gate.Request);
        Assert.Equal("tenant-a", request.TenantId);
        Assert.Equal(dataRightsCase.PropertyId.Value, request.PropertyId);
        Assert.Equal(6, request.ApprovalRevision);
        Assert.Equal(DataRightsOperation.Anonymisation, request.Operation);
        Assert.Equal("guests", request.OwnerKey);
        Assert.Equal("guest-profile", request.RecordType);
        Assert.Equal("user:executor", request.ExecutingActorId);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_existing_execution()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        StubWorkItemRepository workItems = new();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.ApprovedWithEvidence(
                dataRightsCase.ToApprovalEvidence()!));
        Guid idempotencyKey = Guid.NewGuid();
        StartDataRightsAnonymisationExecutionCommandHandler handler = CreateHandler(
            dataRightsCase,
            workItems,
            gate,
            Guid.NewGuid());
        StartDataRightsAnonymisationExecutionCommand command = new(
            dataRightsCase.PropertyId!.Value,
            dataRightsCase.Id,
            idempotencyKey,
            6,
            "user:executor");

        Result<DataRightsExecutionDto> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<DataRightsExecutionDto> retry =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value.WorkItem.Id, retry.Value.WorkItem.Id);
        Assert.Single(workItems.Added);
        Assert.Equal(1, gate.EvaluationCount);
    }

    [Fact]
    public async Task Different_idempotency_key_cannot_retarget_an_existing_execution()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        StubWorkItemRepository workItems = new();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.ApprovedWithEvidence(
                dataRightsCase.ToApprovalEvidence()!));
        StartDataRightsAnonymisationExecutionCommandHandler handler = CreateHandler(
            dataRightsCase,
            workItems,
            gate,
            Guid.NewGuid());

        Assert.True((await handler.HandleAsync(
            new(
                dataRightsCase.PropertyId!.Value,
                dataRightsCase.Id,
                Guid.NewGuid(),
                6,
                "user:executor"),
            CancellationToken.None)).IsSuccess);
        Result<DataRightsExecutionDto> conflict = await handler.HandleAsync(
            new(
                dataRightsCase.PropertyId!.Value,
                dataRightsCase.Id,
                Guid.NewGuid(),
                6,
                "user:executor"),
            CancellationToken.None);

        Assert.True(conflict.IsFailure);
        Assert.Equal(DataRightsApplicationErrors.ExecutionAlreadyStarted.Code, conflict.Error.Code);
        Assert.Single(workItems.Added);
    }

    [Fact]
    public async Task Approval_gate_denial_leaves_case_and_work_items_unchanged()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        StubWorkItemRepository workItems = new();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.SubjectNotApproved));
        StartDataRightsAnonymisationExecutionCommandHandler handler = CreateHandler(
            dataRightsCase,
            workItems,
            gate,
            Guid.NewGuid());

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                dataRightsCase.PropertyId!.Value,
                dataRightsCase.Id,
                Guid.NewGuid(),
                6,
                "user:executor"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DataRightsApplicationErrors.AnonymisationExecutionDenied.Code, result.Error.Code);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Null(dataRightsCase.ExecutionRevision);
        Assert.Empty(workItems.Added);
    }

    private static StartDataRightsAnonymisationExecutionCommandHandler CreateHandler(
        DataRightsCase dataRightsCase,
        StubWorkItemRepository workItems,
        RecordingApprovalGate gate,
        Guid workItemId) =>
        new(
            new StubCaseRepository(dataRightsCase),
            workItems,
            gate,
            new TestClock(),
            new TestIdGenerator(workItemId));

    private static DataRightsCase CreateApprovedAnonymisation()
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
            Now.AddMinutes(-6)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            Now.AddMinutes(-5)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            3,
            2,
            "user:operator",
            Now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(-2)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                11,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('b', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-1)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            Now.AddMinutes(-1),
            evidence).IsSuccess);
        return dataRightsCase;
    }

    private sealed class StubCaseRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase ignored,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult(
            dataRightsCase.PropertyId == propertyId && dataRightsCase.Id == caseId
                ? dataRightsCase
                : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            Guid propertyId,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubWorkItemRepository : IDataRightsExecutionWorkItemRepository
    {
        public DataRightsExecutionWorkItem? Item { get; private set; }
        public List<DataRightsExecutionWorkItem> Added { get; } = [];

        public Task AddAsync(
            DataRightsExecutionWorkItem workItem,
            CancellationToken cancellationToken)
        {
            this.Item = workItem;
            this.Added.Add(workItem);
            return Task.CompletedTask;
        }

        public Task<DataRightsExecutionWorkItem?> GetByCaseAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Item?.PropertyId == propertyId && this.Item.CaseId == caseId
                    ? this.Item
                    : null);
    }

    private sealed class RecordingApprovalGate(DataRightsOperationApprovalResult result)
        : IDataRightsOperationApprovalGate
    {
        public DataRightsOperationApprovalRequest? Request { get; private set; }
        public int EvaluationCount { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            this.EvaluationCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }
}
