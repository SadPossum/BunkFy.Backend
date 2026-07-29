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
using Gma.Framework.Messaging;
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
        RecordingOutbox outbox = new();
        StartDataRightsAnonymisationExecutionCommandHandler handler = CreateHandler(
            dataRightsCase,
            workItems,
            gate,
            workItemId,
            outbox);

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
                dataRightsCase.Id,
                idempotencyKey,
                6,
                "user:executor"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsCaseStatus.Executing, result.Value.Case.Status);
        Assert.Equal(7, result.Value.Case.ExecutionRevision);
        DataRightsExecutionWorkItemDto workItem = Assert.Single(result.Value.WorkItems);
        Assert.Equal(DataRightsExecutionWorkItemStatus.Prepared, workItem.Status);
        Assert.Equal(workItemId, workItem.Id);
        Assert.Equal(result.Value.Batch.Id, workItem.BatchId);
        Assert.Same(workItems.Item, Assert.Single(workItems.Added));
        DataRightsAnonymisationExecutionPreparedIntegrationEvent prepared =
            Assert.IsType<DataRightsAnonymisationExecutionPreparedIntegrationEvent>(
                Assert.Single(outbox.Events));
        Assert.Equal(workItemId, prepared.WorkItemId);
        Assert.Equal(dataRightsCase.Id, prepared.CaseId);
        Assert.Equal(7, prepared.ExecutionRevision);
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
    public async Task Start_prepares_one_ordered_work_item_per_selected_owner()
    {
        DataRightsCase dataRightsCase =
            CreateApprovedAnonymisation(multiOwner: true);
        StubBatchRepository batches = new();
        StubWorkItemRepository workItems = new();
        RecordingApprovalGate gate = new(
            DataRightsOperationApprovalResult.ApprovedWithEvidence(
                dataRightsCase.ToApprovalEvidence()!));
        RecordingOutbox outbox = new();
        Guid batchId = Guid.NewGuid();
        Guid guestWorkItemId = Guid.NewGuid();
        Guid reservationWorkItemId = Guid.NewGuid();
        StartDataRightsAnonymisationExecutionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            batches,
            workItems,
            gate,
            new RecordingOutboxRegistry(outbox),
            new TestClock(),
            new TestIdGenerator(
                batchId,
                guestWorkItemId,
                reservationWorkItemId,
                Guid.NewGuid(),
                Guid.NewGuid()));

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
                dataRightsCase.Id,
                Guid.NewGuid(),
                dataRightsCase.Version,
                "user:executor"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(batchId, result.Value.Batch.Id);
        Assert.Equal(2, result.Value.Batch.SelectedSubjectCount);
        Assert.Equal(2, gate.EvaluationCount);
        Assert.Equal(2, outbox.Events.Count);
        Assert.Equal(
            ["guests", "reservations"],
            result.Value.WorkItems.Select(item => item.OwnerKey));
        Assert.Equal(
            [guestWorkItemId, reservationWorkItemId],
            result.Value.WorkItems.Select(item => item.Id));
        Assert.Equal(
            2,
            workItems.Added.Select(item => item.IdempotencyKey).Distinct().Count());
        Assert.All(workItems.Added, item => Assert.Equal(batchId, item.BatchId));
        Assert.All(
            outbox.Events,
            item => Assert.IsType<
                DataRightsAnonymisationExecutionPreparedIntegrationEvent>(item));
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
            DataRightsCaseScope.ForProperty(
                dataRightsCase.PropertyId!.Value),
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
        Assert.Equal(
            Assert.Single(first.Value.WorkItems).Id,
            Assert.Single(retry.Value.WorkItems).Id);
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
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
                dataRightsCase.Id,
                Guid.NewGuid(),
                6,
                "user:executor"),
            CancellationToken.None)).IsSuccess);
        Result<DataRightsExecutionDto> conflict = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
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
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
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

    [Fact]
    public async Task Scope_confused_approval_evidence_fails_closed()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        DataRightsApprovalEvidence confused =
            dataRightsCase.ToApprovalEvidence()! with
            {
                CaseType = DataRightsCaseType.StaffRights,
                ScopeKind = DataRightsExecutionScopeKind.Tenant
            };
        StubWorkItemRepository workItems = new();
        StartDataRightsAnonymisationExecutionCommandHandler handler =
            CreateHandler(
                dataRightsCase,
                workItems,
                new RecordingApprovalGate(
                    DataRightsOperationApprovalResult
                        .ApprovedWithEvidence(confused)),
                Guid.NewGuid());

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
                dataRightsCase.Id,
                Guid.NewGuid(),
                dataRightsCase.Version,
                "user:executor"),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationExecutionDenied,
            result.Error);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Empty(workItems.Added);
    }

    [Fact]
    public async Task Contradictory_legacy_binding_digest_fails_closed()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();
        DataRightsApprovalEvidence contradictory =
            dataRightsCase.ToApprovalEvidence()! with
            {
                StateBindingsSha256 = new string('f', 64)
            };
        StubWorkItemRepository workItems = new();
        StartDataRightsAnonymisationExecutionCommandHandler handler =
            CreateHandler(
                dataRightsCase,
                workItems,
                new RecordingApprovalGate(
                    DataRightsOperationApprovalResult
                        .ApprovedWithEvidence(contradictory)),
                Guid.NewGuid());

        Result<DataRightsExecutionDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(
                    dataRightsCase.PropertyId!.Value),
                dataRightsCase.Id,
                Guid.NewGuid(),
                dataRightsCase.Version,
                "user:executor"),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationExecutionDenied,
            result.Error);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Empty(workItems.Added);
    }

    private static StartDataRightsAnonymisationExecutionCommandHandler CreateHandler(
        DataRightsCase dataRightsCase,
        StubWorkItemRepository workItems,
        RecordingApprovalGate gate,
        Guid workItemId,
        RecordingOutbox? outbox = null) =>
        new(
            new StubCaseRepository(dataRightsCase),
            new StubBatchRepository(),
            workItems,
            gate,
            new RecordingOutboxRegistry(outbox ?? new RecordingOutbox()),
            new TestClock(),
            new TestIdGenerator(
                Guid.NewGuid(),
                workItemId,
                Guid.NewGuid()));

    private static DataRightsCase CreateApprovedAnonymisation(
        bool multiOwner = false)
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
        if (multiOwner)
        {
            Assert.True(dataRightsCase.SelectSubject(
                "reservations",
                "reservation",
                Guid.NewGuid(),
                4,
                dataRightsCase.Version,
                "user:operator",
                Now.AddMinutes(-3)).IsSuccess);
        }

        Assert.True(dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:operator",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            dataRightsCase.Version,
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
            dataRightsCase.Version,
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
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult(
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

        public Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            Guid batchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                (IReadOnlyCollection<DataRightsExecutionWorkItem>)this.Added
                    .Where(item =>
                        item.CaseKind ==
                            (DataRightsCaseKind)scope.CaseType &&
                        item.PropertyId == scope.PropertyId &&
                        item.CaseId == caseId &&
                        item.BatchId == batchId)
                    .ToArray());

        public Task<DataRightsExecutionWorkItem?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Item?.CaseKind ==
                    (DataRightsCaseKind)scope.CaseType &&
                this.Item.PropertyId == scope.PropertyId &&
                this.Item.CaseId == caseId &&
                this.Item.Id == workItemId
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

    private sealed class TestIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private readonly Queue<Guid> remaining = new(ids);

        public Guid NewId() => this.remaining.Dequeue();
    }

    private sealed class StubBatchRepository : IDataRightsExecutionBatchRepository
    {
        private DataRightsExecutionBatch? batch;

        public Task AddAsync(
            DataRightsExecutionBatch executionBatch,
            CancellationToken cancellationToken)
        {
            this.batch = executionBatch;
            return Task.CompletedTask;
        }

        public Task<DataRightsExecutionBatch?> GetByCaseAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.batch?.CaseKind ==
                    (DataRightsCaseKind)scope.CaseType &&
                this.batch.PropertyId == scope.PropertyId &&
                this.batch.CaseId == caseId
                    ? this.batch
                    : null);
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
