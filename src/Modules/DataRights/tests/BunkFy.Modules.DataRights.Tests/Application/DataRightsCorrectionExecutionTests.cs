namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Authorization;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;
using DomainSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

[Trait("Category", "Unit")]
public sealed class DataRightsCorrectionExecutionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_claims_one_exact_owner_and_same_request_replays()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase();
        StubCorrectionExecutionRepository executions = new();
        MutableClock clock = new(Now);
        StartDataRightsCorrectionExecutionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            [new GuestCorrectionPolicy()],
            clock);
        Guid executionId = Guid.NewGuid();
        StartDataRightsCorrectionExecutionCommand command = new(
            dataRightsCase.PropertyId!.Value,
            dataRightsCase.Id,
            executionId,
            dataRightsCase.Version,
            "user:executor");

        Result<DataRightsCorrectionExecutionDto> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<DataRightsCorrectionExecutionDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(DataRightsCaseStatus.Executing, first.Value.Case.Status);
        Assert.Equal(executionId, first.Value.Execution.ExecutionId);
        Assert.Equal(
            GuestCorrectionPolicy.PolicyKey,
            first.Value.Execution.FieldPolicyKey);
        Assert.Equal("user:executor", first.Value.Execution.ExecutedBy);
        Assert.Equal(first.Value.Execution, replay.Value.Execution);
        Assert.Equal(1, executions.AddCount);
    }

    [Fact]
    public async Task Replay_fails_closed_when_case_no_longer_matches_claim_revision()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase();
        StubCorrectionExecutionRepository executions = new();
        StartDataRightsCorrectionExecutionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            [new GuestCorrectionPolicy()],
            new MutableClock(Now));
        StartDataRightsCorrectionExecutionCommand command = new(
            dataRightsCase.PropertyId!.Value,
            dataRightsCase.Id,
            Guid.NewGuid(),
            dataRightsCase.Version,
            "user:executor");
        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).IsSuccess);
        Assert.True(dataRightsCase.CompleteCorrectionExecution(
            dataRightsCase.Version,
            dataRightsCase.DecisionRevision!.Value,
            "system:test",
            Now.AddSeconds(1),
            Now.AddSeconds(1)).IsSuccess);

        Result<DataRightsCorrectionExecutionDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.CorrectionExecutionConflict,
            replay.Error);
    }

    [Fact]
    public async Task Gate_requires_exact_actor_policy_subject_and_unexpired_claim()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase();
        StubCorrectionExecutionRepository executions = new();
        MutableClock clock = new(Now);
        StartDataRightsCorrectionExecutionCommandHandler starter = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            [new GuestCorrectionPolicy()],
            clock);
        Guid executionId = Guid.NewGuid();
        Assert.True((await starter.HandleAsync(
            new(
                dataRightsCase.PropertyId!.Value,
                dataRightsCase.Id,
                executionId,
                dataRightsCase.Version,
                "user:executor"),
            CancellationToken.None)).IsSuccess);
        DomainSubject subject = dataRightsCase.SelectedSubjects.Single();
        DataRightsCorrectionExecutionGate gate = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            clock);
        DataRightsCorrectionExecutionGateRequest request = new(
            dataRightsCase.ScopeId,
            dataRightsCase.PropertyId.Value,
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            executionId,
            new(
                subject.OwnerKey,
                subject.RecordType,
                subject.RecordId,
                subject.RecordVersion),
            GuestCorrectionPolicy.PolicyKey,
            "user:executor");

        DataRightsCorrectionExecutionGateResult allowed =
            await gate.EvaluateAsync(request, CancellationToken.None);
        DataRightsCorrectionExecutionGateResult changedActor =
            await gate.EvaluateAsync(
                request with { ExecutingActorId = "user:other" },
                CancellationToken.None);
        DataRightsCorrectionExecutionGateResult changedPolicy =
            await gate.EvaluateAsync(
                request with { FieldPolicyKey = "guests.other.correction.v1" },
                CancellationToken.None);
        clock.UtcNow = Now.AddMinutes(11);
        DataRightsCorrectionExecutionGateResult expired =
            await gate.EvaluateAsync(request, CancellationToken.None);

        Assert.True(allowed.IsAllowed);
        Assert.Equal(
            DataRightsCorrectionExecutionDenial.ExecutionMismatch,
            changedActor.Denial);
        Assert.Equal(
            DataRightsCorrectionExecutionDenial.ExecutionMismatch,
            changedPolicy.Denial);
        Assert.Equal(
            DataRightsCorrectionExecutionDenial.ExecutionExpired,
            expired.Denial);
    }

    [Fact]
    public async Task Owner_completion_converges_case_and_claim_once_with_exact_proof()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase();
        StubCorrectionExecutionRepository executions = new();
        MutableClock clock = new(Now.AddMinutes(2));
        Guid executionId = Guid.NewGuid();
        StartDataRightsCorrectionExecutionCommandHandler starter = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            [new GuestCorrectionPolicy()],
            new MutableClock(Now));
        StartDataRightsCorrectionExecutionCommand command = new(
            dataRightsCase.PropertyId!.Value,
            dataRightsCase.Id,
            executionId,
            dataRightsCase.Version,
            "user:executor");
        Assert.True((await starter.HandleAsync(
            command,
            CancellationToken.None)).IsSuccess);
        DomainSubject subject = dataRightsCase.SelectedSubjects.Single();
        DataRightsCorrectionAppliedIntegrationEvent applied = new(
            Guid.NewGuid(),
            dataRightsCase.ScopeId,
            Now.AddMinutes(1),
            executionId,
            dataRightsCase.PropertyId.Value,
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            subject.OwnerKey,
            subject.RecordType,
            subject.RecordId,
            subject.RecordVersion,
            subject.RecordVersion + 1,
            GuestCorrectionPolicy.PolicyKey,
            receiptContractVersion: 1,
            Guid.NewGuid(),
            ["guest.profile.display-name"]);
        DataRightsCorrectionCompletionCoordinator coordinator = new(
            new StubCaseRepository(dataRightsCase),
            executions,
            clock);

        DataRightsCorrectionAppliedIntegrationEvent mismatched = new(
            Guid.NewGuid(),
            dataRightsCase.ScopeId,
            applied.OccurredAtUtc,
            executionId,
            applied.PropertyId,
            applied.CaseId,
            applied.ApprovalRevision,
            "reservations",
            "reservation",
            applied.RecordId,
            applied.SelectedRecordVersion,
            applied.CurrentRecordVersion,
            "reservations.reservation.correction.v1",
            applied.ReceiptContractVersion,
            Guid.NewGuid(),
            ["reservation.guest.primary-name"]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CompleteAsync(mismatched, CancellationToken.None));

        await coordinator.CompleteAsync(applied, CancellationToken.None);
        await coordinator.CompleteAsync(applied, CancellationToken.None);
        Result<DataRightsCorrectionExecutionDto> replay =
            await starter.HandleAsync(command, CancellationToken.None);

        Assert.Equal(DataRightsCaseState.Completed, dataRightsCase.Status);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(
            DataRightsCorrectionExecutionStatus.Completed,
            replay.Value.Execution.Status);
        Assert.Equal(
            DataRightsCorrectionExecutionState.Completed,
            executions.Execution!.State);
        Assert.Equal(applied.ReceiptId, executions.Execution.ReceiptId);
        Assert.Equal(applied.ChangedFieldsSha256, executions.Execution.ChangedFieldsSha256);
        Assert.Equal(applied.ReceiptSha256, executions.Execution.ReceiptSha256);
    }

    private static DataRightsCase CreateApprovedCase()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Correction,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:privacy",
            Now.AddMinutes(-6)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-5)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            recordVersion: 3,
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            dataRightsCase.Version,
            "user:decision-maker",
            Now.AddMinutes(-2)).IsSuccess);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            dataRightsCase.Version,
            "user:decision-maker",
            Now.AddMinutes(-1)).IsSuccess);
        return dataRightsCase;
    }

    private sealed class GuestCorrectionPolicy : IDataRightsCorrectionPolicyContributor
    {
        public const string PolicyKey = "guests.guest-profile.correction.v1";

        public int ContractVersion => DataRightsCorrectionContract.CurrentVersion;
        public string OwnerKey => "guests";
        public string RecordType => "guest-profile";
        public string FieldPolicyKey => PolicyKey;
    }

    private sealed class StubCorrectionExecutionRepository
        : IDataRightsCorrectionExecutionRepository
    {
        public DataRightsCorrectionExecution? Execution { get; private set; }
        public int AddCount { get; private set; }

        public Task AddAsync(
            DataRightsCorrectionExecution execution,
            CancellationToken cancellationToken)
        {
            this.Execution = execution;
            this.AddCount++;
            return Task.CompletedTask;
        }

        public Task<DataRightsCorrectionExecution?> GetAsync(
            Guid propertyId,
            Guid caseId,
            Guid executionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Execution?.PropertyId == propertyId &&
                this.Execution.CaseId == caseId &&
                this.Execution.Id == executionId
                    ? this.Execution
                    : null);

        public Task<DataRightsCorrectionExecution?> GetByCaseAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Execution?.PropertyId == propertyId &&
                this.Execution.CaseId == caseId
                    ? this.Execution
                    : null);
    }

    private sealed class StubCaseRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase ignored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                scope.PropertyId == dataRightsCase.PropertyId &&
                caseId == dataRightsCase.Id
                    ? dataRightsCase
                    : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            DataRightsCaseScope scope,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class MutableClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }
}
