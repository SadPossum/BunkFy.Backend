namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsRestrictionExecutionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Restriction_cannot_enter_review_with_multiple_subjects()
    {
        DataRightsCase dataRightsCase = CreateDiscoveryCase(
            DataRightsRestrictionAction.Apply);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            recordVersion: 2,
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-3)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "reservations",
            "reservation",
            Guid.NewGuid(),
            recordVersion: 4,
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-2)).IsSuccess);

        var result = dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:privacy",
            Now.AddMinutes(-1));

        Assert.Equal("DataRights.RestrictionExecutionInvalid", result.Error.Code);
        Assert.Equal(DataRightsCaseState.Discovery, dataRightsCase.Status);
    }

    [Fact]
    public void Proof_must_match_directive_effective_state()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase(
            DataRightsRestrictionAction.Release);
        var proof = DataRightsRestrictionExecutionProof.Create(
            Guid.NewGuid(),
            dataRightsCase.DecisionRevision!.Value,
            DataRightsRestrictionAction.Release,
            dataRightsCase.SelectedSubjects.Single(),
            receiptContractVersion: 1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            resultingOwnerRevision: 2,
            resultingProjectionRevision: 3,
            effectiveRestricted: true,
            new string('a', 64),
            "user:executor",
            Now.AddSeconds(-5));

        Assert.Equal(
            "DataRights.RestrictionExecutionProofInvalid",
            proof.Error.Code);
    }

    [Fact]
    public void Completion_is_idempotent_but_changed_replay_conflicts()
    {
        DataRightsCase dataRightsCase = CreateApprovedCase(
            DataRightsRestrictionAction.Apply);
        DataRightsRestrictionExecutionProof proof =
            DataRightsRestrictionExecutionProof.Create(
                Guid.NewGuid(),
                dataRightsCase.DecisionRevision!.Value,
                DataRightsRestrictionAction.Apply,
                dataRightsCase.SelectedSubjects.Single(),
                receiptContractVersion: 1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                resultingOwnerRevision: 2,
                resultingProjectionRevision: 3,
                effectiveRestricted: true,
                new string('a', 64),
                "user:executor",
                Now.AddSeconds(-5)).Value;

        var completed = dataRightsCase.CompleteRestrictionExecution(
            dataRightsCase.Version,
            proof,
            Now);
        var replay = dataRightsCase.CompleteRestrictionExecution(
            expectedVersion: 1,
            proof,
            Now.AddSeconds(1));
        DataRightsRestrictionExecutionProof changed =
            DataRightsRestrictionExecutionProof.Create(
                Guid.NewGuid(),
                proof.ApprovalRevision,
                proof.Directive,
                dataRightsCase.SelectedSubjects.Single(),
                proof.ReceiptContractVersion,
                proof.ReceiptId,
                proof.OwnerOperationId,
                proof.ResultingOwnerRevision,
                proof.ResultingProjectionRevision,
                proof.EffectiveRestricted,
                proof.ReceiptSha256,
                proof.ExecutedBy,
                proof.CompletedAtUtc).Value;
        var conflict = dataRightsCase.CompleteRestrictionExecution(
            dataRightsCase.Version,
            changed,
            Now.AddSeconds(2));

        Assert.True(completed.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(
            "DataRights.RestrictionExecutionConflict",
            conflict.Error.Code);
        Assert.Equal(DataRightsCaseState.Completed, dataRightsCase.Status);
    }

    private static DataRightsCase CreateDiscoveryCase(
        DataRightsRestrictionAction action)
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Restriction,
            DataRightsRequesterRelation.ControllerInitiated,
            action).Value;
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
        return dataRightsCase;
    }

    private static DataRightsCase CreateApprovedCase(
        DataRightsRestrictionAction action)
    {
        DataRightsCase dataRightsCase = CreateDiscoveryCase(action);
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
}
