namespace BunkFy.Modules.DataRights.Tests.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Results;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsReplayEnvelopeProtectorTests
{
    [Fact]
    public void Envelope_is_deterministic_authenticated_and_tenant_bound()
    {
        HmacDataRightsRecordPseudonymizer pseudonymizer =
            ProtectedLedgerTestData.CreatePseudonymizer((1, 'a'));
        AesGcmDataRightsReplayEnvelopeProtector protector =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 1,
                (1, 'r'));
        Guid recordId = Guid.NewGuid();
        DataRightsProcessingLedgerEntry ledger =
            ProtectedLedgerTestData.CreateLedger(
                pseudonymizer,
                "tenant-a",
                sequence: 1,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                recordId);

        DataRightsProtectedReplayEnvelope first =
            protector.Protect(ledger.Freeze(), recordId).Value;
        DataRightsProtectedReplayEnvelope replay =
            protector.Protect(ledger.Freeze(), recordId).Value;
        Result<Guid> restored =
            protector.Unprotect(ledger.Freeze(), first);

        Assert.Equal(first, replay);
        Assert.Equal(recordId, restored.Value);

        DataRightsProcessingLedgerEntry anotherTenant =
            ProtectedLedgerTestData.CreateLedger(
                pseudonymizer,
                "tenant-b",
                sequence: 1,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                recordId);
        Assert.Equal(
            "DataRights.ReplayEnvelopeInvalid",
            protector.Unprotect(anotherTenant.Freeze(), first).Error.Code);
    }

    [Fact]
    public void Tampering_and_a_mismatched_record_are_rejected()
    {
        HmacDataRightsRecordPseudonymizer pseudonymizer =
            ProtectedLedgerTestData.CreatePseudonymizer((1, 'a'));
        AesGcmDataRightsReplayEnvelopeProtector protector =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 1,
                (1, 'r'));
        Guid recordId = Guid.NewGuid();
        DataRightsProcessingLedgerEntry ledger =
            ProtectedLedgerTestData.CreateLedger(
                pseudonymizer,
                "tenant-a",
                sequence: 1,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                recordId);
        DataRightsProtectedReplayEnvelope envelope =
            protector.Protect(ledger.Freeze(), recordId).Value;
        byte[] tag = Convert.FromBase64String(
            envelope.AuthenticationTagBase64);
        tag[0] ^= 0xff;

        Result<Guid> tampered = protector.Unprotect(
            ledger.Freeze(),
            envelope with
            {
                AuthenticationTagBase64 = Convert.ToBase64String(tag)
            });
        Result<DataRightsProtectedReplayEnvelope> mismatch =
            protector.Protect(ledger.Freeze(), Guid.NewGuid());

        Assert.Equal(
            "DataRights.ReplayEnvelopeInvalid",
            tampered.Error.Code);
        Assert.Equal(
            "DataRights.ReplayEnvelopeInvalid",
            mismatch.Error.Code);
    }

    [Fact]
    public void Rotation_reads_old_envelopes_and_missing_old_keys_fail_closed()
    {
        HmacDataRightsRecordPseudonymizer pseudonymizer =
            ProtectedLedgerTestData.CreatePseudonymizer((1, 'a'));
        Guid recordId = Guid.NewGuid();
        DataRightsProcessingLedgerEntry ledger =
            ProtectedLedgerTestData.CreateLedger(
                pseudonymizer,
                "tenant-a",
                sequence: 1,
                DataRightsProcessingLedgerEntry.GenesisEntrySha256,
                recordId);
        AesGcmDataRightsReplayEnvelopeProtector original =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 1,
                (1, 'r'));
        DataRightsProtectedReplayEnvelope oldEnvelope =
            original.Protect(ledger.Freeze(), recordId).Value;
        AesGcmDataRightsReplayEnvelopeProtector rotated =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 2,
                (1, 'r'),
                (2, 's'));
        AesGcmDataRightsReplayEnvelopeProtector missingOld =
            ProtectedLedgerTestData.CreateProtector(
                pseudonymizer,
                activeKeyVersion: 2,
                (2, 's'));

        Assert.Equal(
            recordId,
            rotated.Unprotect(ledger.Freeze(), oldEnvelope).Value);
        Assert.Equal(
            2,
            rotated.Protect(ledger.Freeze(), recordId).Value.KeyVersion);
        Assert.Equal(
            "DataRights.ReplayEnvelopeKeyUnavailable",
            missingOld.Unprotect(ledger.Freeze(), oldEnvelope).Error.Code);
    }

    [Fact]
    public void Production_validation_rejects_the_development_key()
    {
        DataRightsReplayEnvelopeOptions development = new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = DataRightsReplayEnvelopeOptions.DevelopmentKeyBase64
            }
        };
        DataRightsReplayEnvelopeOptions deployment = new()
        {
            ActiveKeyVersion = 2,
            Keys =
            {
                [1] = ProtectedLedgerTestData.Key('r'),
                [2] = ProtectedLedgerTestData.Key('s')
            }
        };

        Assert.True(
            new DataRightsReplayEnvelopeOptionsValidator(isProduction: true)
                .Validate(Options.DefaultName, development)
                .Failed);
        Assert.True(
            new DataRightsReplayEnvelopeOptionsValidator(isProduction: true)
                .Validate(Options.DefaultName, deployment)
                .Succeeded);
    }
}
