namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Guest_case_starts_as_a_versioned_PII_minimal_draft()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = Create(
            propertyId,
            DataRightsRequesterRelation.DataSubject);

        Assert.Equal(propertyId, dataRightsCase.PropertyId);
        Assert.Equal(DataRightsCaseKind.GuestRights, dataRightsCase.Kind);
        Assert.Equal(DataRightsCaseOperation.AccessExport, dataRightsCase.RequestedOperations);
        Assert.Equal(DataRightsVerificationState.Pending, dataRightsCase.VerificationStatus);
        Assert.Equal(DataRightsRoutingState.Pending, dataRightsCase.RoutingStatus);
        Assert.Equal(DataRightsCaseState.Draft, dataRightsCase.Status);
        Assert.Equal(1, dataRightsCase.Version);
        Assert.Equal("user:operator-a", dataRightsCase.CreatedBy);
    }

    [Fact]
    public void Sensitive_discovery_requires_verification_and_controller_routing()
    {
        DataRightsCase dataRightsCase = Create(
            Guid.NewGuid(),
            DataRightsRequesterRelation.AuthorizedRepresentative);

        Assert.Equal(
            "DataRights.VerificationRequired",
            dataRightsCase.BeginDiscovery(1, "user:operator-a", Now.AddMinutes(1)).Error.Code);
        Assert.True(dataRightsCase.RecordRequesterVerification(
            verified: true,
            expectedVersion: 1,
            "user:operator-b",
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(
            "DataRights.ControllerRoutingRequired",
            dataRightsCase.BeginDiscovery(2, "user:operator-b", Now.AddMinutes(2)).Error.Code);
        Assert.True(dataRightsCase.RecordControllerRouting(
            2,
            "user:operator-b",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.BeginDiscovery(
            3,
            "user:operator-b",
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(DataRightsCaseState.Discovery, dataRightsCase.Status);
        Assert.Equal(4, dataRightsCase.Version);
    }

    [Fact]
    public void Controller_initiated_case_can_start_discovery_without_fake_verification()
    {
        DataRightsCase dataRightsCase = Create(
            Guid.NewGuid(),
            DataRightsRequesterRelation.ControllerInitiated);

        Assert.Equal(DataRightsVerificationState.NotRequired, dataRightsCase.VerificationStatus);
        Assert.Equal(DataRightsRoutingState.NotRequired, dataRightsCase.RoutingStatus);
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator-a",
            Now.AddMinutes(1)).IsSuccess);
    }

    [Fact]
    public void Review_and_cancel_are_explicit_versioned_transitions()
    {
        DataRightsCase dataRightsCase = Create(
            Guid.NewGuid(),
            DataRightsRequesterRelation.ControllerInitiated);
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator-a",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            1,
            2,
            "user:operator-b",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator-b",
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(
            "DataRights.VersionConflict",
            dataRightsCase.Cancel(3, "user:operator-b", Now.AddMinutes(4)).Error.Code);
        Assert.True(dataRightsCase.Cancel(
            4,
            "user:operator-b",
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(DataRightsCaseState.Canceled, dataRightsCase.Status);
        Assert.Equal(5, dataRightsCase.Version);
    }

    [Fact]
    public void Review_requires_a_selected_subject_coordinate()
    {
        DataRightsCase dataRightsCase = Create(
            Guid.NewGuid(),
            DataRightsRequesterRelation.ControllerInitiated);
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator-a",
            Now.AddMinutes(1)).IsSuccess);

        Assert.Equal(
            "DataRights.SubjectSelectionRequired",
            dataRightsCase.RequireReview(
                2,
                "user:operator-a",
                Now.AddMinutes(2)).Error.Code);
    }

    [Fact]
    public void Approval_is_an_explicit_attributable_and_immutable_revision()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();

        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(DataRightsCaseState.DecisionPending, dataRightsCase.Status);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            " user:decision-maker ",
            Now.AddMinutes(5)).IsSuccess);

        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Equal(DataRightsCaseDecision.Approved, dataRightsCase.Decision);
        Assert.Equal(DataRightsCaseDecisionReason.RequestValidated, dataRightsCase.DecisionReason);
        Assert.Equal(6, dataRightsCase.DecisionRevision);
        Assert.Equal(dataRightsCase.Version, dataRightsCase.DecisionRevision);
        Assert.Equal("user:decision-maker", dataRightsCase.DecidedBy);
        Assert.Equal(Now.AddMinutes(5), dataRightsCase.DecidedAtUtc);
        Assert.Equal(
            "DataRights.TransitionInvalid",
            dataRightsCase.RecordDecision(
                DataRightsCaseDecision.Denied,
                DataRightsCaseDecisionReason.RequestInvalid,
                6,
                "user:other",
                Now.AddMinutes(6)).Error.Code);
    }

    [Fact]
    public void Decision_reason_must_match_the_outcome()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(4)).IsSuccess);

        Assert.Equal(
            "DataRights.DecisionInvalid",
            dataRightsCase.RecordDecision(
                DataRightsCaseDecision.Approved,
                DataRightsCaseDecisionReason.LegalObligation,
                5,
                "user:decision-maker",
                Now.AddMinutes(5)).Error.Code);
        Assert.Equal(DataRightsCaseState.DecisionPending, dataRightsCase.Status);
        Assert.Equal(5, dataRightsCase.Version);
        Assert.Null(dataRightsCase.DecisionRevision);

        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Denied,
            DataRightsCaseDecisionReason.LegalObligation,
            5,
            "user:decision-maker",
            Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(DataRightsCaseState.Denied, dataRightsCase.Status);
        Assert.Equal(6, dataRightsCase.DecisionRevision);
    }

    [Fact]
    public void Anonymisation_approval_requires_exact_immutable_policy_evidence()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateReviewRequired(
            propertyId,
            DataRightsCaseOperation.Anonymisation);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(4)).IsSuccess);

        Assert.Equal(
            "DataRights.ApprovalPolicyEvidenceInvalid",
            dataRightsCase.RecordDecision(
                DataRightsCaseDecision.Approved,
                DataRightsCaseDecisionReason.RequestValidated,
                5,
                "user:decision-maker",
                Now.AddMinutes(5)).Error.Code);

        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                9,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(5)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            Now.AddMinutes(5),
            evidence).IsSuccess);

        DataRightsApprovalPolicyEvidence persisted =
            Assert.IsType<DataRightsApprovalPolicyEvidence>(
                dataRightsCase.ApprovalPolicyEvidence);
        Assert.Same(evidence, persisted);
        Assert.True(persisted.RequiresDistinctExecutor);
    }

    [Fact]
    public void Anonymisation_cannot_share_an_approval_with_other_operations()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateReviewRequired(
            propertyId,
            DataRightsCaseOperation.Anonymisation | DataRightsCaseOperation.Correction);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(4)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                9,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(5)).Value;

        Assert.Equal(
            "DataRights.ApprovalPolicyEvidenceInvalid",
            dataRightsCase.RecordDecision(
                DataRightsCaseDecision.Approved,
                DataRightsCaseDecisionReason.RequestValidated,
                5,
                "user:decision-maker",
                Now.AddMinutes(5),
                evidence).Error.Code);
    }

    [Fact]
    public void Approved_anonymisation_can_begin_once_with_a_distinct_executor()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();

        Assert.True(dataRightsCase.BeginAnonymisationExecution(
            6,
            " user:executor ",
            Now.AddMinutes(6)).IsSuccess);

        Assert.Equal(DataRightsCaseState.Executing, dataRightsCase.Status);
        Assert.Equal(7, dataRightsCase.ExecutionRevision);
        Assert.Equal(7, dataRightsCase.Version);
        Assert.Equal("user:executor", dataRightsCase.ExecutionStartedBy);
        Assert.Equal(Now.AddMinutes(6), dataRightsCase.ExecutionStartedAtUtc);
        Assert.Equal(
            "DataRights.TransitionInvalid",
            dataRightsCase.BeginAnonymisationExecution(
                7,
                "user:other-executor",
                Now.AddMinutes(7)).Error.Code);
    }

    [Fact]
    public void Anonymisation_decision_actor_cannot_execute_their_own_approval()
    {
        DataRightsCase dataRightsCase = CreateApprovedAnonymisation();

        Assert.Equal(
            "DataRights.DecisionActorCannotExecute",
            dataRightsCase.BeginAnonymisationExecution(
                6,
                "user:decision-maker",
                Now.AddMinutes(6)).Error.Code);
        Assert.Equal(DataRightsCaseState.Approved, dataRightsCase.Status);
        Assert.Null(dataRightsCase.ExecutionRevision);
        Assert.Null(dataRightsCase.ExecutionStartedBy);
    }

    [Fact]
    public void Case_changes_reject_timestamp_regression()
    {
        DataRightsCase dataRightsCase = CreateReviewRequired();

        Assert.Equal(
            "DataRights.TimestampInvalid",
            dataRightsCase.BeginDecision(
                4,
                "user:decision-maker",
                Now.AddMinutes(2)).Error.Code);
        Assert.Equal(DataRightsCaseState.ReviewRequired, dataRightsCase.Status);
        Assert.Equal(4, dataRightsCase.Version);
    }

    [Fact]
    public void Subject_selection_is_bounded_deduplicated_and_removable()
    {
        DataRightsCase dataRightsCase = Create(
            Guid.NewGuid(),
            DataRightsRequesterRelation.ControllerInitiated);
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator-a",
            Now.AddMinutes(1)).IsSuccess);
        Guid firstRecordId = Guid.NewGuid();
        Assert.True(dataRightsCase.SelectSubject(
            " Guests ",
            " Guest-Profile ",
            firstRecordId,
            4,
            2,
            "user:operator-a",
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(
            "DataRights.SubjectAlreadySelected",
            dataRightsCase.SelectSubject(
                "guests",
                "guest-profile",
                firstRecordId,
                5,
                3,
                "user:operator-a",
                Now.AddMinutes(3)).Error.Code);
        Assert.True(dataRightsCase.UnselectSubject(
            "guests",
            "guest-profile",
            firstRecordId,
            3,
            "user:operator-a",
            Now.AddMinutes(3)).IsSuccess);
        Assert.Empty(dataRightsCase.SelectedSubjects);

        long version = dataRightsCase.Version;
        for (int index = 0; index < DataRightsCase.MaxSelectedSubjects; index++)
        {
            Assert.True(dataRightsCase.SelectSubject(
                "guests",
                "guest-profile",
                Guid.NewGuid(),
                1,
                version++,
                "user:operator-a",
                Now.AddMinutes(4)).IsSuccess);
        }

        Assert.Equal(
            "DataRights.SubjectSelectionLimitReached",
            dataRightsCase.SelectSubject(
                "guests",
                "guest-profile",
                Guid.NewGuid(),
                1,
                version,
                "user:operator-a",
                Now.AddMinutes(5)).Error.Code);
    }

    [Fact]
    public void Case_request_rejects_invalid_scope_and_operation_combinations()
    {
        Assert.Equal(
            "DataRights.PropertyRequired",
            DataRightsCaseRequest.Create(
                propertyId: null,
                DataRightsCaseKind.GuestRights,
                DataRightsCaseOperation.AccessExport,
                DataRightsRequesterRelation.DataSubject).Error.Code);
        Assert.Equal(
            "DataRights.PropertyNotAllowed",
            DataRightsCaseRequest.Create(
                Guid.NewGuid(),
                DataRightsCaseKind.TenantTermination,
                DataRightsCaseOperation.AccessExport,
                DataRightsRequesterRelation.TenantOwner).Error.Code);
        Assert.Equal(
            "DataRights.OperationsInvalid",
            DataRightsCaseRequest.Create(
                Guid.NewGuid(),
                DataRightsCaseKind.GuestRights,
                DataRightsCaseOperation.None,
                DataRightsRequesterRelation.DataSubject).Error.Code);
        Assert.Equal(
            "DataRights.GuestRightsRequesterInvalid",
            DataRightsCaseRequest.Create(
                Guid.NewGuid(),
                DataRightsCaseKind.GuestRights,
                DataRightsCaseOperation.AccessExport,
                DataRightsRequesterRelation.TenantOwner).Error.Code);
    }

    [Theory]
    [InlineData(DataRightsCaseOperation.Restriction, DataRightsRestrictionAction.None, false)]
    [InlineData(DataRightsCaseOperation.Restriction, DataRightsRestrictionAction.Apply, true)]
    [InlineData(DataRightsCaseOperation.Restriction, DataRightsRestrictionAction.Release, true)]
    [InlineData(DataRightsCaseOperation.AccessExport, DataRightsRestrictionAction.Apply, false)]
    [InlineData(DataRightsCaseOperation.AccessExport, DataRightsRestrictionAction.Release, false)]
    public void Case_request_requires_a_directive_only_for_restriction(
        DataRightsCaseOperation operations,
        DataRightsRestrictionAction restrictionAction,
        bool expectedSuccess)
    {
        Result<DataRightsCaseRequest> result = DataRightsCaseRequest.Create(
            Guid.NewGuid(),
            DataRightsCaseKind.GuestRights,
            operations,
            DataRightsRequesterRelation.ControllerInitiated,
            restrictionAction);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (expectedSuccess)
        {
            Assert.Equal(restrictionAction, result.Value.RestrictionAction);
        }
        else
        {
            Assert.Equal("DataRights.RestrictionDirectiveInvalid", result.Error.Code);
        }
    }

    private static DataRightsCase Create(
        Guid propertyId,
        DataRightsRequesterRelation requesterRelationship,
        DataRightsCaseOperation operations = DataRightsCaseOperation.AccessExport)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            operations,
            requesterRelationship).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "  user:operator-a  ",
            Now).Value;
    }

    private static DataRightsCase CreateReviewRequired()
        => CreateReviewRequired(
            Guid.NewGuid(),
            DataRightsCaseOperation.AccessExport | DataRightsCaseOperation.Correction);

    private static DataRightsCase CreateReviewRequired(
        Guid propertyId,
        DataRightsCaseOperation operations)
    {
        DataRightsCase dataRightsCase = Create(
            propertyId,
            DataRightsRequesterRelation.ControllerInitiated,
            operations);
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator-a",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            1,
            2,
            "user:operator-a",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            3,
            "user:operator-a",
            Now.AddMinutes(3)).IsSuccess);
        return dataRightsCase;
    }

    private static DataRightsCase CreateApprovedAnonymisation()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateReviewRequired(
            propertyId,
            DataRightsCaseOperation.Anonymisation);
        Assert.True(dataRightsCase.BeginDecision(
            4,
            "user:decision-maker",
            Now.AddMinutes(4)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                9,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(5)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            5,
            "user:decision-maker",
            Now.AddMinutes(5),
            evidence).IsSuccess);
        return dataRightsCase;
    }
}
