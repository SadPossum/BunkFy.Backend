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
        Assert.True(first.MatchesExecutionProof(workItem));
        Assert.Equal(workItem.Id, first.WorkItemId);
        Assert.Equal(workItem.CaseId, first.CaseId);
        Assert.Equal(workItem.ExecutionRevision, first.OperationRevision);
        Assert.Equal(workItem.OwnerReceiptId, first.OwnerReceiptId);
        Assert.Equal(workItem.OwnerReceiptSha256, first.OwnerReceiptSha256);
        Assert.Equal(workItem.ResultingRecordVersion, first.ResultingRecordVersion);
        Assert.Equal(
            DataRightsProcessingLedgerEntry
                .GuestResultVersionContractVersion,
            first.ContractVersion);
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
        Assert.Equal(
            "DataRights.ProcessingLedgerEntryInvalid",
            DataRightsProcessingLedgerEntry.Restore(
                snapshot with
                {
                    ResultingRecordVersion =
                        snapshot.ResultingRecordVersion!.Value + 1
                })
                .Error.Code);
    }

    [Fact]
    public void Version_one_snapshot_preserves_its_original_digest_contract()
    {
        DataRightsProcessingLedgerSnapshot snapshot = new(
            ContractVersion: 1,
            EntryId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ScopeId: "tenant-a",
            TenantSequence: 1,
            WorkItemId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CaseId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ApprovalRevision: 6,
            OperationRevision: 7,
            Operation: DataRightsCaseOperation.Anonymisation,
            RoutingPropertyId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            OwnerKey: "guests",
            RecordType: "guest-profile",
            RecordPseudonymKeyVersion: 1,
            RecordPseudonymSha256: new string('c', 64),
            DispositionCode: "guests.completed",
            ReasonCode: "guests.profile-anonymised",
            CompletedAtUtc: new(
                2026,
                7,
                25,
                12,
                4,
                0,
                TimeSpan.Zero),
            PolicyEvidenceSchemaVersion: 1,
            PolicyId: "approved-policy",
            PolicyVersion: 3,
            PolicyContentSha256: new string('a', 64),
            RetentionPolicyId: "guest-retention",
            RetentionPolicyVersion: 2,
            OwnerReceiptContractVersion: 1,
            OwnerReceiptId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OwnerReceiptSha256: new string('b', 64),
            PreviousEntrySha256:
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            EntrySha256:
                "ff2ce0d9a70bd93ad1ee27ad1100955aea4326be4f180d9e14051fd87424546b",
            ReplayOfLedgerEntryId: null,
            SupersedesLedgerEntryId: null);

        DataRightsProcessingLedgerEntry restored =
            DataRightsProcessingLedgerEntry.Restore(snapshot).Value;

        Assert.Equal(1, restored.ContractVersion);
        Assert.Null(restored.ResultingRecordVersion);
        Assert.True(restored.HasValidCanonicalDigest());
    }

    [Fact]
    public void Version_two_snapshot_preserves_its_original_digest_contract()
    {
        DataRightsProcessingLedgerSnapshot snapshot = new(
            ContractVersion: 2,
            EntryId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ScopeId: "tenant-a",
            TenantSequence: 1,
            WorkItemId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            CaseId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ApprovalRevision: 6,
            OperationRevision: 7,
            Operation: DataRightsCaseOperation.Anonymisation,
            RoutingPropertyId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            OwnerKey: "guests",
            RecordType: "guest-profile",
            RecordPseudonymKeyVersion: 1,
            RecordPseudonymSha256: new string('c', 64),
            DispositionCode: "guests.completed",
            ReasonCode: "guests.profile-anonymised",
            CompletedAtUtc: new(
                2026,
                7,
                25,
                12,
                4,
                0,
                TimeSpan.Zero),
            PolicyEvidenceSchemaVersion: 1,
            PolicyId: "approved-policy",
            PolicyVersion: 3,
            PolicyContentSha256: new string('a', 64),
            RetentionPolicyId: "guest-retention",
            RetentionPolicyVersion: 2,
            OwnerReceiptContractVersion: 1,
            OwnerReceiptId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            OwnerReceiptSha256: new string('b', 64),
            PreviousEntrySha256:
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
            EntrySha256:
                "465bcf7dbdf887dee1bfcaf8b57f066572a82246472fbe560980aadc7ea82d1c",
            ReplayOfLedgerEntryId: null,
            SupersedesLedgerEntryId: null,
            ResultingRecordVersion: 5);

        DataRightsProcessingLedgerEntry restored =
            DataRightsProcessingLedgerEntry.Restore(snapshot).Value;

        Assert.Equal(2, restored.ContractVersion);
        Assert.Equal(5, restored.ResultingRecordVersion);
        Assert.True(restored.HasValidCanonicalDigest());
    }

    [Fact]
    public void Version_three_snapshot_freezes_scoped_policy_evidence()
    {
        DataRightsExecutionWorkItem workItem =
            CreateStaffOwnerProofWorkItem();
        DataRightsProcessingLedgerEntry entry =
            DataRightsProcessingLedgerEntry.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                tenantSequence: 1,
                workItem,
                DataRightsRecordPseudonym.Create(
                    3,
                    new string('c', 64)).Value,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256).Value;
        DataRightsProcessingLedgerSnapshot snapshot = entry.Freeze();

        Assert.Equal(
            DataRightsProcessingLedgerEntry.CurrentContractVersion,
            entry.ContractVersion);
        Assert.Equal(DataRightsCaseKind.StaffRights, entry.CaseKind);
        Assert.Equal(DataRightsCaseScopeKind.Tenant, entry.ScopeKind);
        Assert.Null(entry.RoutingPropertyId);
        Assert.Equal(0, entry.PolicyPropertyVersion);
        Assert.Equal("GB", entry.PolicyOperatingCountryCode);
        Assert.Equal(
            workItem.PolicyStateBindingsJson,
            entry.PolicyStateBindingsJson);
        Assert.Equal(
            workItem.PolicyStateBindingsSha256,
            entry.PolicyStateBindingsSha256);
        Assert.Equal(
            "4a78aac8d4cb58bbfd453cde158ac29142b4391493d19ce7223b4e44fea155d4",
            entry.EntrySha256);
        Assert.True(entry.MatchesExecutionProof(workItem));
        Assert.False(entry.MatchesExecutionProof(
            CreateStaffOwnerProofWorkItem("US")));
        Assert.Equal(
            snapshot,
            DataRightsProcessingLedgerEntry.Restore(snapshot).Value.Freeze());
        Assert.True(DataRightsProcessingLedgerEntry.Restore(
            snapshot with
            {
                RoutingPropertyId =
                    Guid.Parse("99999999-9999-9999-9999-999999999999")
            }).IsFailure);
        Assert.True(DataRightsProcessingLedgerEntry.Restore(
            snapshot with { PolicyOperatingCountryCode = "US" }).IsFailure);
        Assert.True(DataRightsProcessingLedgerEntry.Restore(
            snapshot with
            {
                PolicyStateBindingsJson = "[null]",
                PolicyStateBindingsSha256 = new string('f', 64)
            }).IsFailure);
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
            Guid.NewGuid(),
            DataRightsExecutionScope.ForProperty(propertyId),
            approvalRevision: 6,
            executionRevision: 7,
            DataRightsCaseOperation.Anonymisation,
            subject,
            policy,
            "user:executor",
            Now.AddMinutes(1)).Value;
    }

    private static DataRightsExecutionWorkItem
        CreateStaffOwnerProofWorkItem(string operatingCountryCode = "GB")
    {
        DataRightsSubjectCoordinate subject =
            DataRightsSubjectCoordinate.Create(
                "staff",
                "staff-member",
                Guid.Parse(
                    "66666666-6666-6666-6666-666666666666"),
                8,
                "user:selector",
                Now).Value;
        DataRightsApprovalPolicyEvidence policy =
            DataRightsApprovalPolicyEvidence.CreateScoped(
                DataRightsCaseKind.StaffRights,
                DataRightsCaseScopeKind.Tenant,
                propertyId: null,
                propertyVersion: 0,
                operatingCountryCode,
                "approved-staff-policy",
                3,
                "staff-retention",
                2,
                new string('a', 64),
                "staff-data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                "staff-employment",
                "employment-ended",
                Now.AddDays(-8),
                Now.AddDays(-1),
                Now,
                [
                    DataRightsApprovalEvidenceBinding.Create(
                        "staff.record",
                        8,
                        new string('d', 64)).Value,
                    DataRightsApprovalEvidenceBinding.Create(
                        "staff.governance",
                        2,
                        new string('e', 64)).Value
                ]).Value;
        DataRightsExecutionWorkItem workItem =
            DataRightsExecutionWorkItem.Prepare(
                Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),
                "tenant-a",
                Guid.Parse(
                    "33333333-3333-3333-3333-333333333333"),
                Guid.Parse(
                    "44444444-4444-4444-4444-444444444444"),
                Guid.Parse(
                    "55555555-5555-5555-5555-555555555555"),
                DataRightsExecutionScope.Staff,
                approvalRevision: 6,
                executionRevision: 7,
                DataRightsCaseOperation.Anonymisation,
                subject,
                policy,
                "user:executor",
                Now.AddMinutes(1)).Value;
        Guid taskRunId =
            Guid.Parse("77777777-7777-7777-7777-777777777777");
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(workItem.RecordOwnerProof(
            workItem.Version,
            taskRunId,
            taskAttempt: 1,
            receiptContractVersion: 1,
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            resultingRecordVersion: 9,
            "staff.completed",
            "staff.member-anonymised",
            new string('b', 64),
            Now.AddMinutes(3),
            Now.AddMinutes(4)).IsSuccess);
        return workItem;
    }
}
