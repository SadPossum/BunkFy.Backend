namespace BunkFy.Modules.DataRights.Tests.Persistence;

using System.Text;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Microsoft.Extensions.Options;

internal static class ProtectedLedgerTestData
{
    internal static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    internal static HmacDataRightsRecordPseudonymizer CreatePseudonymizer(
        params (int Version, char Key)[] keys)
    {
        DataRightsPseudonymisationOptions options = new()
        {
            ActiveKeyVersion = keys[0].Version
        };
        foreach ((int version, char key) in keys)
        {
            options.Keys[version] = Key(key);
        }

        return new(Options.Create(options));
    }

    internal static AesGcmDataRightsReplayEnvelopeProtector CreateProtector(
        IDataRightsRecordPseudonymizer pseudonymizer,
        int activeKeyVersion = 1,
        params (int Version, char Key)[] keys)
    {
        DataRightsReplayEnvelopeOptions options = new()
        {
            ActiveKeyVersion = activeKeyVersion
        };
        foreach ((int version, char key) in keys)
        {
            options.Keys[version] = Key(key);
        }

        return new(Options.Create(options), pseudonymizer);
    }

    internal static DataRightsProcessingLedgerEntry CreateLedger(
        IDataRightsRecordPseudonymizer pseudonymizer,
        string scopeId,
        long sequence,
        string previousEntrySha256,
        Guid recordId)
    {
        DataRightsExecutionWorkItem workItem =
            CreateCompletedWorkItem(scopeId, recordId);
        DataRightsRecordPseudonym pseudonym = pseudonymizer.CreateActive(
            scopeId,
            workItem.OwnerKey,
            workItem.RecordType,
            recordId).Value;
        return DataRightsProcessingLedgerEntry.Create(
            Guid.NewGuid(),
            sequence,
            workItem,
            pseudonym,
            previousEntrySha256).Value;
    }

    internal static DataRightsLedgerDelta CreateDelta(
        AesGcmDataRightsReplayEnvelopeProtector protector,
        DataRightsProcessingLedgerEntry ledger,
        Guid recordId) =>
        DataRightsLedgerDelta.Create(
            ledger,
            protector.Protect(ledger.Freeze(), recordId).Value);

    internal static string Key(char value) =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(new string(value, 32)));

    private static DataRightsExecutionWorkItem CreateCompletedWorkItem(
        string scopeId,
        Guid recordId)
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsSubjectCoordinate subject = DataRightsSubjectCoordinate.Create(
            "guests",
            "guest-profile",
            recordId,
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
        DataRightsExecutionWorkItem workItem =
            DataRightsExecutionWorkItem.Prepare(
                Guid.NewGuid(),
                scopeId,
                Guid.NewGuid(),
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
        Guid taskRunId = Guid.NewGuid();
        _ = workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2));
        _ = workItem.RecordOwnerProof(
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
            Now.AddMinutes(4));
        return workItem;
    }
}
