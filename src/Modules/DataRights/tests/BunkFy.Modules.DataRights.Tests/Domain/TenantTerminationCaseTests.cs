namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_accepts_only_exact_anonymisation_operation()
    {
        Result<DataRightsCaseRequest> invalid = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.AccessExport |
                DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner);

        Assert.True(invalid.IsFailure);
        Assert.Equal("DataRights.OperationsInvalid", invalid.Error.Code);

        Result<DataRightsCaseRequest> valid = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner);

        Assert.True(valid.IsSuccess);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Request_moves_directly_to_review_without_subject_discovery(
        bool exportRequested)
    {
        DataRightsCase dataRightsCase = Create();

        Result prepared = dataRightsCase.PrepareTenantTerminationReview(
            exportRequested,
            1,
            "admin-api:requester",
            Now.AddMinutes(1));

        Assert.True(prepared.IsSuccess);
        Assert.Equal(DataRightsCaseState.ReviewRequired, dataRightsCase.Status);
        Assert.Equal(exportRequested, dataRightsCase.TenantTerminationExportRequested);
        Assert.Empty(dataRightsCase.SelectedSubjects);
        Assert.Equal(2, dataRightsCase.Version);
    }

    [Fact]
    public void Approval_requires_a_canonical_policy_digest()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();

        Result invalid = dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            "ABC",
            2,
            "admin-api:approver",
            Now.AddMinutes(2));

        Assert.True(invalid.IsFailure);
        Assert.Equal(
            "DataRights.TenantTerminationTransitionInvalid",
            invalid.Error.Code);

        Result approved = dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            new string('a', TenantTerminationProcess.Sha256Length),
            2,
            "admin-api:approver",
            Now.AddMinutes(2));

        Assert.True(approved.IsSuccess);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Equal(3, dataRightsCase.DecisionRevision);
        Assert.Equal(
            new string('a', TenantTerminationProcess.Sha256Length),
            dataRightsCase.TenantTerminationPolicyEvidenceSha256);
        Assert.Null(dataRightsCase.ApprovalPolicyEvidence);
    }

    [Fact]
    public void Denial_rejects_policy_digest_and_is_terminal()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();

        Result invalid = dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Denied,
            DataRightsCaseDecisionReason.LegalObligation,
            new string('a', TenantTerminationProcess.Sha256Length),
            2,
            "admin-api:approver",
            Now.AddMinutes(2));

        Assert.True(invalid.IsFailure);

        Result denied = dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Denied,
            DataRightsCaseDecisionReason.LegalObligation,
            policyEvidenceSha256: null,
            2,
            "admin-api:approver",
            Now.AddMinutes(2));

        Assert.True(denied.IsSuccess);
        Assert.Equal(DataRightsCaseState.Denied, dataRightsCase.Status);
        Assert.Null(dataRightsCase.TenantTerminationPolicyEvidenceSha256);
    }

    [Fact]
    public void Approver_cannot_start_execution()
    {
        DataRightsCase dataRightsCase = CreateApproved();

        Result result = dataRightsCase.BeginTenantTerminationExecution(
            3,
            "admin-api:approver",
            Now.AddMinutes(3));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "DataRights.TenantTerminationExecutorInvalid",
            result.Error.Code);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
    }

    [Fact]
    public void Execution_completes_idempotently_only_for_exact_approval_proof()
    {
        DataRightsCase dataRightsCase = CreateApproved();
        Assert.True(dataRightsCase.BeginTenantTerminationExecution(
            3,
            "admin-api:executor",
            Now.AddMinutes(3)).IsSuccess);

        Result wrongProof = dataRightsCase.CompleteTenantTerminationExecution(
            approvalRevision: 3,
            new string('b', TenantTerminationProcess.Sha256Length),
            expectedVersion: 4,
            "worker:tenant-termination",
            Now.AddMinutes(4));
        Assert.True(wrongProof.IsFailure);

        string proof = new('a', TenantTerminationProcess.Sha256Length);
        Result completed = dataRightsCase.CompleteTenantTerminationExecution(
            approvalRevision: 3,
            proof,
            expectedVersion: 4,
            "worker:tenant-termination",
            Now.AddMinutes(4));
        Assert.True(completed.IsSuccess);
        Assert.Equal(DataRightsCaseState.Completed, dataRightsCase.Status);

        Result replayed = dataRightsCase.CompleteTenantTerminationExecution(
            approvalRevision: 3,
            proof,
            expectedVersion: 4,
            "worker:tenant-termination",
            Now.AddMinutes(4));
        Assert.True(replayed.IsSuccess);
        Assert.Equal(5, dataRightsCase.Version);
    }

    private static DataRightsCase Create()
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.TenantOwner).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "admin-api:requester",
            Now).Value;
    }

    private static DataRightsCase CreateReviewRequired()
    {
        DataRightsCase dataRightsCase = Create();
        Assert.True(dataRightsCase.PrepareTenantTerminationReview(
            exportRequested: true,
            1,
            "admin-api:requester",
            Now.AddMinutes(1)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsCase CreateApproved()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();
        Assert.True(dataRightsCase.RecordTenantTerminationDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            new string('a', TenantTerminationProcess.Sha256Length),
            2,
            "admin-api:approver",
            Now.AddMinutes(2)).IsSuccess);
        return dataRightsCase;
    }
}
