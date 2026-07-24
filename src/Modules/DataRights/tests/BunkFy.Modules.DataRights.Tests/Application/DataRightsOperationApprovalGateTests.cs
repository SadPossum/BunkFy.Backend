namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Authorization;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsOperationApprovalGateTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_approved_case_operation_subject_and_revision_are_authorized()
    {
        Guid propertyId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateApprovedCase(propertyId, recordId);
        DataRightsOperationApprovalGate gate = new(new StubCaseRepository(dataRightsCase));

        DataRightsOperationApprovalResult result = await gate.EvaluateAsync(
            CreateRequest(dataRightsCase, propertyId, recordId),
            CancellationToken.None);

        Assert.True(result.IsApproved);
        Assert.Equal(DataRightsOperationApprovalDenial.None, result.Denial);
    }

    [Fact]
    public async Task Restriction_approval_authorizes_only_its_exact_directive()
    {
        Guid propertyId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateApprovedCase(
            propertyId,
            recordId,
            DataRightsCaseOperation.Restriction,
            DataRightsRestrictionAction.Apply);
        DataRightsOperationApprovalGate gate = new(new StubCaseRepository(dataRightsCase));
        DataRightsOperationApprovalRequest applyRequest = CreateRequest(
            dataRightsCase,
            propertyId,
            recordId) with
        {
            Operation = DataRightsOperation.Restriction,
            RestrictionDirective = DataRightsRestrictionDirective.Apply
        };

        DataRightsOperationApprovalResult approved = await gate.EvaluateAsync(
            applyRequest,
            CancellationToken.None);
        DataRightsOperationApprovalResult mismatched = await gate.EvaluateAsync(
            applyRequest with { RestrictionDirective = DataRightsRestrictionDirective.Release },
            CancellationToken.None);
        DataRightsOperationApprovalResult unknown = await gate.EvaluateAsync(
            applyRequest with { RestrictionDirective = DataRightsRestrictionDirective.Unknown },
            CancellationToken.None);

        Assert.True(approved.IsApproved);
        Assert.Equal(
            DataRightsOperationApprovalDenial.RestrictionDirectiveMismatch,
            mismatched.Denial);
        Assert.Equal(DataRightsOperationApprovalDenial.InvalidRequest, unknown.Denial);
    }

    [Theory]
    [InlineData("tenant-b", 6, DataRightsOperation.Correction, 3, DataRightsOperationApprovalDenial.CaseNotFound)]
    [InlineData("tenant-a", 5, DataRightsOperation.Correction, 3, DataRightsOperationApprovalDenial.ApprovalRevisionMismatch)]
    [InlineData("tenant-a", 6, DataRightsOperation.Erasure, 3, DataRightsOperationApprovalDenial.OperationNotApproved)]
    [InlineData("tenant-a", 6, DataRightsOperation.Correction, 4, DataRightsOperationApprovalDenial.SubjectNotApproved)]
    public async Task Approval_gate_fails_closed_on_every_execution_coordinate_mismatch(
        string tenantId,
        long approvalRevision,
        DataRightsOperation operation,
        long recordVersion,
        DataRightsOperationApprovalDenial expectedDenial)
    {
        Guid propertyId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateApprovedCase(propertyId, recordId);
        DataRightsOperationApprovalGate gate = new(new StubCaseRepository(dataRightsCase));
        DataRightsOperationApprovalRequest request = CreateRequest(
            dataRightsCase,
            propertyId,
            recordId) with
        {
            TenantId = tenantId,
            ApprovalRevision = approvalRevision,
            Operation = operation,
            RecordVersion = recordVersion
        };

        DataRightsOperationApprovalResult result = await gate.EvaluateAsync(
            request,
            CancellationToken.None);

        Assert.False(result.IsApproved);
        Assert.Equal(expectedDenial, result.Denial);
    }

    [Fact]
    public async Task Approval_gate_rejects_combined_operations_as_an_invalid_request()
    {
        Guid propertyId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateApprovedCase(propertyId, recordId);
        DataRightsOperationApprovalGate gate = new(new StubCaseRepository(dataRightsCase));
        DataRightsOperationApprovalRequest request = CreateRequest(
            dataRightsCase,
            propertyId,
            recordId) with
        {
            Operation = DataRightsOperation.AccessExport | DataRightsOperation.Correction
        };

        DataRightsOperationApprovalResult result = await gate.EvaluateAsync(
            request,
            CancellationToken.None);

        Assert.False(result.IsApproved);
        Assert.Equal(DataRightsOperationApprovalDenial.InvalidRequest, result.Denial);
    }

    [Fact]
    public async Task Denied_case_cannot_authorize_owner_execution()
    {
        Guid propertyId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDecisionPendingCase(propertyId, recordId);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Denied,
            DataRightsCaseDecisionReason.RequestInvalid,
            5,
            "user:operator-b",
            Now.AddMinutes(5)).IsSuccess);
        DataRightsOperationApprovalGate gate = new(new StubCaseRepository(dataRightsCase));

        DataRightsOperationApprovalResult result = await gate.EvaluateAsync(
            CreateRequest(dataRightsCase, propertyId, recordId),
            CancellationToken.None);

        Assert.False(result.IsApproved);
        Assert.Equal(DataRightsOperationApprovalDenial.CaseNotApproved, result.Denial);
    }

    private static DataRightsCase CreateApprovedCase(
        Guid propertyId,
        Guid recordId,
        DataRightsCaseOperation operations =
            DataRightsCaseOperation.AccessExport | DataRightsCaseOperation.Correction,
        DataRightsRestrictionAction restrictionAction = DataRightsRestrictionAction.None)
    {
        DataRightsCase dataRightsCase = CreateDecisionPendingCase(
            propertyId,
            recordId,
            operations,
            restrictionAction);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:operator-b",
            Now.AddMinutes(5)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsCase CreateDecisionPendingCase(
        Guid propertyId,
        Guid recordId,
        DataRightsCaseOperation operations =
            DataRightsCaseOperation.AccessExport | DataRightsCaseOperation.Correction,
        DataRightsRestrictionAction restrictionAction = DataRightsRestrictionAction.None)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            operations,
            DataRightsRequesterRelation.ControllerInitiated,
            restrictionAction).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator-a",
            Now).Value;
        Assert.True(dataRightsCase.BeginDiscovery(1, "user:operator-a", Now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            recordId,
            3,
            2,
            "user:operator-a",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(3, "user:operator-a", Now.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(4, "user:operator-b", Now.AddMinutes(4)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsOperationApprovalRequest CreateRequest(
        DataRightsCase dataRightsCase,
        Guid propertyId,
        Guid recordId) => new(
            "tenant-a",
            propertyId,
            dataRightsCase.Id,
            dataRightsCase.DecisionRevision!.Value,
            DataRightsOperation.Correction,
            "guests",
            "guest-profile",
            recordId,
            3);

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
}
