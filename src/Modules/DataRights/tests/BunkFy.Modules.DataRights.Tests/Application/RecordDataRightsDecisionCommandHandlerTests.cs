namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

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
                propertyId,
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
                propertyId,
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
                propertyId,
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

    private sealed class StubAnonymisationPolicy(
        Result<DataRightsApprovalPolicyEvidence> result)
        : IDataRightsAnonymisationApprovalPolicy
    {
        public int EvaluationCount { get; private set; }

        public Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
            Guid propertyId,
            CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
