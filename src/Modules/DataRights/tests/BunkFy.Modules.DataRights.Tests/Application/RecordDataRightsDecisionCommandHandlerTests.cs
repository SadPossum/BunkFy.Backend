namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
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
using DomainEvidenceBinding =
    DataRights.Domain.ValueObjects.DataRightsApprovalEvidenceBinding;
using SelectedSubject =
    DataRights.Domain.Entities.DataRightsSubjectCoordinate;

[Trait("Category", "Unit")]
public sealed class RecordDataRightsDecisionCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_anonymisation_uses_server_resolved_policy_evidence()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDecisionPendingCase(
            propertyId,
            DataRightsCaseOperation.Anonymisation);
        DataRightsApprovalPolicyEvidence evidence = CreateEvidence(propertyId);
        RecordDataRightsDecisionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            new StubAnonymisationPolicy(Result.Success(evidence)),
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(propertyId),
                dataRightsCase.Id,
                DataRightsDecisionOutcome.Approved,
                DataRightsDecisionReason.RequestValidated,
                5,
                "user:decision-maker"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(evidence.PolicyId, result.Value.ApprovalEvidence?.PolicyId);
        Assert.Equal(evidence.PropertyVersion, result.Value.ApprovalEvidence?.PropertyVersion);
        Assert.True(result.Value.ApprovalEvidence?.RequiresDistinctExecutor);
    }

    [Fact]
    public async Task Policy_denial_leaves_the_case_decision_pending()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDecisionPendingCase(
            propertyId,
            DataRightsCaseOperation.Anonymisation);
        RecordDataRightsDecisionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            new StubAnonymisationPolicy(
                Result.Failure<DataRightsApprovalPolicyEvidence>(
                    DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied)),
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(propertyId),
                dataRightsCase.Id,
                DataRightsDecisionOutcome.Approved,
                DataRightsDecisionReason.RequestValidated,
                5,
                "user:decision-maker"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied.Code,
            result.Error.Code);
        Assert.Equal(DataRightsCaseState.DecisionPending, dataRightsCase.Status);
        Assert.Null(dataRightsCase.DecisionRevision);
    }

    [Fact]
    public async Task Combined_anonymisation_approval_is_rejected_before_policy_evaluation()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDecisionPendingCase(
            propertyId,
            DataRightsCaseOperation.Anonymisation | DataRightsCaseOperation.Correction);
        StubAnonymisationPolicy policy = new(Result.Success(CreateEvidence(propertyId)));
        RecordDataRightsDecisionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            policy,
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.ForProperty(propertyId),
                dataRightsCase.Id,
                DataRightsDecisionOutcome.Approved,
                DataRightsDecisionReason.RequestValidated,
                5,
                "user:decision-maker"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationMustBeApprovedSeparately.Code,
            result.Error.Code);
        Assert.Equal(0, policy.EvaluationCount);
    }

    [Fact]
    public async Task Staff_authority_and_required_companion_can_be_approved_together()
    {
        DataRightsCase dataRightsCase =
            CreateStaffDecisionPendingCase();
        DataRightsApprovalPolicyEvidence evidence =
            CreateStaffEvidence();
        StubAnonymisationPolicy policy =
            new(Result.Success(evidence));
        RecordDataRightsDecisionCommandHandler handler = new(
            new StubCaseRepository(dataRightsCase),
            policy,
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new(
                DataRightsCaseScope.Staff,
                dataRightsCase.Id,
                DataRightsDecisionOutcome.Approved,
                DataRightsDecisionReason.RequestValidated,
                6,
                "user:decision-maker"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal(DataRightsCaseStatus.Approved, result.Value.Status);
        Assert.Equal(2, result.Value.SelectedSubjectCount);
        Assert.Equal(1, policy.EvaluationCount);
        Assert.Equal(2, policy.LastSubjectCount);
        Assert.Equal(
            evidence.StateBindingsSha256,
            result.Value.ApprovalEvidence?.StateBindingsSha256);
    }

    private static DataRightsCase CreateDecisionPendingCase(
        Guid propertyId,
        DataRightsCaseOperation operations)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            operations,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            Now.AddMinutes(-5)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            Now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            3,
            2,
            "user:operator",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator",
            Now.AddMinutes(-2)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(-1)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsApprovalPolicyEvidence CreateEvidence(Guid propertyId) =>
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
            Now).Value;

    private static DataRightsCase CreateStaffDecisionPendingCase()
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.StaffRights,
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
            "staff",
            "staff-member",
            Guid.NewGuid(),
            7,
            2,
            "user:operator",
            Now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "workspaces",
            "staff-access-process",
            Guid.NewGuid(),
            4,
            3,
            "user:operator",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            4,
            "user:operator",
            Now.AddMinutes(-2)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            5,
            "user:decision-maker",
            Now.AddMinutes(-1)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsApprovalPolicyEvidence
        CreateStaffEvidence() =>
        DataRightsApprovalPolicyEvidence.CreateScoped(
            DataRightsCaseKind.StaffRights,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null,
            propertyVersion: 0,
            "GB",
            "staff-approved-policy",
            policyVersion: 3,
            "staff-employment",
            retentionPolicyVersion: 2,
            new string('b', 64),
            "staff-data-rights-anonymisation",
            "erasure",
            "authorized-workspace-operator",
            "staff-employment",
            "employment-ended",
            Now.AddDays(-2_557),
            Now.AddDays(-1),
            Now,
            [
                DomainEvidenceBinding.Create(
                    "staff.record",
                    7,
                    new string('c', 64)).Value,
                DomainEvidenceBinding.Create(
                    "workspaces.staff-correlation",
                    4,
                    new string('d', 64)).Value
            ]).Value;

    private sealed class StubAnonymisationPolicy(
        Result<DataRightsApprovalPolicyEvidence> result)
        : IDataRightsAnonymisationApprovalPolicy
    {
        public int EvaluationCount { get; private set; }
        public int LastSubjectCount { get; private set; }

        public Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
            string tenantId,
            DataRightsCaseScope scope,
            Guid caseId,
            IReadOnlyCollection<SelectedSubject> subjects,
            CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
            this.LastSubjectCount = subjects.Count;
            return Task.FromResult(result);
        }
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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
