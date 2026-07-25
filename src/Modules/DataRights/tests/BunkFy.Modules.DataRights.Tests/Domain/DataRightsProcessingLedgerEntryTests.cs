namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsProcessingLedgerEntryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Entry_freezes_owner_policy_subject_and_chain_proof_deterministically()
    {
        DataRightsExecutionWorkItem workItem = CreateOwnerProofWorkItem();
        DataRightsRecordPseudonym pseudonym =
            DataRightsRecordPseudonym.Create(3, new string('c', 64)).Value;
        Guid entryId = Guid.NewGuid();

        DataRightsProcessingLedgerEntry first =
            DataRightsProcessingLedgerEntry.Create(
                entryId,
                tenantSequence: 1,
                workItem,
                pseudonym,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;
        DataRightsProcessingLedgerEntry replay =
            DataRightsProcessingLedgerEntry.Create(
                entryId,
                tenantSequence: 1,
                workItem,
                pseudonym,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;

        Assert.Equal(first.EntrySha256, replay.EntrySha256);
        Assert.True(first.HasValidCanonicalDigest());
        Assert.Equal(workItem.Id, first.WorkItemId);
        Assert.Equal(workItem.CaseId, first.CaseId);
        Assert.Equal(workItem.ExecutionRevision, first.OperationRevision);
        Assert.Equal(workItem.OwnerReceiptId, first.OwnerReceiptId);
        Assert.Equal(workItem.OwnerReceiptSha256, first.OwnerReceiptSha256);
        Assert.Equal(pseudonym.KeyVersion, first.RecordPseudonymKeyVersion);
        Assert.Equal(pseudonym.Sha256, first.RecordPseudonymSha256);
        Assert.Equal(
            DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            first.PreviousEntrySha256);
    }

    [Fact]
    public void Frozen_entry_restores_without_a_case_or_work_item_and_rejects_tampering()
    {
        DataRightsExecutionWorkItem workItem = CreateOwnerProofWorkItem();
        DataRightsProcessingLedgerEntry original =
            DataRightsProcessingLedgerEntry.Create(
                Guid.NewGuid(),
                tenantSequence: 1,
                workItem,
                DataRightsRecordPseudonym.Create(1, new string('c', 64)).Value,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;
        DataRightsProcessingLedgerSnapshot snapshot = original.Freeze();

        DataRightsProcessingLedgerEntry restored =
            DataRightsProcessingLedgerEntry.Restore(snapshot).Value;

        Assert.Equal(snapshot, restored.Freeze());
        Assert.True(restored.HasValidCanonicalDigest());
        Assert.Equal(
            "DataRights.ProcessingLedgerEntryInvalid",
            DataRightsProcessingLedgerEntry.Restore(
                snapshot with { OwnerReceiptSha256 = new string('d', 64) })
                .Error.Code);
    }

    [Fact]
    public void Entry_rejects_non_terminal_owner_proof_and_invalid_chain_coordinate()
    {
        DataRightsExecutionWorkItem prepared = CreatePreparedWorkItem();
        DataRightsRecordPseudonym pseudonym =
            DataRightsRecordPseudonym.Create(1, new string('d', 64)).Value;

        Assert.Equal(
            "DataRights.ProcessingLedgerEntryInvalid",
            DataRightsProcessingLedgerEntry.Create(
                Guid.NewGuid(),
                tenantSequence: 1,
                prepared,
                pseudonym,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Error.Code);

        DataRightsExecutionWorkItem completed = CreateOwnerProofWorkItem();
        Assert.Equal(
            "DataRights.ProcessingLedgerEntryInvalid",
            DataRightsProcessingLedgerEntry.Create(
                Guid.NewGuid(),
                tenantSequence: 2,
                completed,
                pseudonym,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Error.Code);
        Assert.Equal(
            "DataRights.ProcessingLedgerEntryInvalid",
            DataRightsProcessingLedgerEntry.Create(
                Guid.NewGuid(),
                tenantSequence: 1,
                completed,
                pseudonym,
                new string('e', 64)).Error.Code);
    }

    [Fact]
    public void Entry_chain_and_replay_coordinates_change_the_canonical_digest()
    {
        DataRightsExecutionWorkItem workItem = CreateOwnerProofWorkItem();
        DataRightsRecordPseudonym pseudonym =
            DataRightsRecordPseudonym.Create(1, new string('f', 64)).Value;
        Guid entryId = Guid.NewGuid();
        string previousDigest = new('1', 64);

        DataRightsProcessingLedgerEntry original =
            DataRightsProcessingLedgerEntry.Create(
                entryId,
                tenantSequence: 2,
                workItem,
                pseudonym,
                previousDigest).Value;
        DataRightsProcessingLedgerEntry replay =
            DataRightsProcessingLedgerEntry.Create(
                entryId,
                tenantSequence: 2,
                workItem,
                pseudonym,
                previousDigest,
                replayOfLedgerEntryId: Guid.NewGuid()).Value;

        Assert.NotEqual(original.EntrySha256, replay.EntrySha256);
        Assert.True(original.HasValidCanonicalDigest());
        Assert.True(replay.HasValidCanonicalDigest());
    }

    private static DataRightsExecutionWorkItem CreateOwnerProofWorkItem()
    {
        DataRightsExecutionWorkItem workItem = CreatePreparedWorkItem();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(workItem.RecordOwnerProof(
            workItem.Version,
            taskRunId,
            taskAttempt: 1,
            receiptContractVersion: 1,
            Guid.NewGuid(),
            resultingRecordVersion: 5,
            "guests.completed",
            "guests.profile-anonymised",
            new string('b', 64),
            Now.AddMinutes(3),
            Now.AddMinutes(4)).IsSuccess);
        return workItem;
    }

    private static DataRightsExecutionWorkItem CreatePreparedWorkItem()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsSubjectCoordinate subject = DataRightsSubjectCoordinate.Create(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4,
            "user:selector",
            Now).Value;
        DataRightsApprovalPolicyEvidence policy =
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
                Now).Value;
        return DataRightsExecutionWorkItem.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId,
            approvalRevision: 6,
            executionRevision: 7,
            DataRightsCaseOperation.Anonymisation,
            subject,
            policy,
            "user:executor",
            Now.AddMinutes(1)).Value;
    }
}
