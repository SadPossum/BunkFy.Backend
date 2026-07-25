namespace BunkFy.Modules.DataRights.Persistence;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;
using Microsoft.Extensions.Options;

internal sealed class AesGcmDataRightsReplayEnvelopeProtector(
    IOptions<DataRightsReplayEnvelopeOptions> options,
    IDataRightsRecordPseudonymizer pseudonymizer)
    : IDataRightsReplayEnvelopeProtector
{
    private const string EncryptionKeyDomain =
        "bunkfy.data-rights.replay-envelope.encryption-key.v1";
    private const string NonceKeyDomain =
        "bunkfy.data-rights.replay-envelope.nonce-key.v1";
    private const string BindingDomain =
        "bunkfy.data-rights.replay-envelope.binding.v1";
    private readonly DataRightsReplayEnvelopeOptions options = options.Value;

    public Result<DataRightsProtectedReplayEnvelope> Protect(
        DataRightsProcessingLedgerSnapshot ledger,
        Guid recordId)
    {
        if (!this.TryValidateLedgerSubject(ledger, recordId))
        {
            return Invalid();
        }

        int keyVersion = this.options.ActiveKeyVersion;
        if (!this.TryGetKey(keyVersion, out byte[] masterKey))
        {
            return KeyUnavailable();
        }

        byte[] binding = CreateBinding(ledger);
        byte[] plaintext = Encoding.ASCII.GetBytes(recordId.ToString("N"));
        byte[] encryptionKey = Derive(masterKey, EncryptionKeyDomain);
        byte[] nonceKey = Derive(masterKey, NonceKeyDomain);
        byte[] nonceDigest = HMACSHA256.HashData(nonceKey, binding);
        byte[] nonce = nonceDigest[
            ..DataRightsProtectedReplayEnvelope.NonceSizeBytes];
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag =
            new byte[DataRightsProtectedReplayEnvelope.AuthenticationTagSizeBytes];

        try
        {
            using AesGcm aes = new(
                encryptionKey,
                DataRightsProtectedReplayEnvelope.AuthenticationTagSizeBytes);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, binding);
            return Result.Success(new DataRightsProtectedReplayEnvelope(
                DataRightsProtectedReplayEnvelope.CurrentContractVersion,
                keyVersion,
                DataRightsProtectedReplayEnvelope.Aes256GcmAlgorithm,
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(ciphertext),
                Convert.ToBase64String(tag)));
        }
        catch (CryptographicException)
        {
            return Invalid();
        }
        finally
        {
            Zero(
                masterKey,
                binding,
                plaintext,
                encryptionKey,
                nonceKey,
                nonceDigest,
                nonce,
                ciphertext,
                tag);
        }
    }

    public Result<Guid> Unprotect(
        DataRightsProcessingLedgerSnapshot ledger,
        DataRightsProtectedReplayEnvelope envelope)
    {
        if (ledger is null ||
            envelope is null ||
            DataRightsProcessingLedgerEntry.Restore(ledger).IsFailure ||
            !envelope.HasValidShape())
        {
            return InvalidGuid();
        }

        if (!this.TryGetKey(envelope.KeyVersion, out byte[] masterKey))
        {
            return KeyUnavailableGuid();
        }

        byte[] binding = CreateBinding(ledger);
        byte[] encryptionKey = Derive(masterKey, EncryptionKeyDomain);
        byte[] nonceKey = Derive(masterKey, NonceKeyDomain);
        byte[] nonceDigest = HMACSHA256.HashData(nonceKey, binding);
        byte[] expectedNonce = nonceDigest[
            ..DataRightsProtectedReplayEnvelope.NonceSizeBytes];
        byte[] nonce = Convert.FromBase64String(envelope.NonceBase64);
        byte[] ciphertext = Convert.FromBase64String(envelope.CiphertextBase64);
        byte[] tag = Convert.FromBase64String(envelope.AuthenticationTagBase64);
        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    nonce,
                    expectedNonce))
            {
                return InvalidGuid();
            }

            using AesGcm aes = new(
                encryptionKey,
                DataRightsProtectedReplayEnvelope.AuthenticationTagSizeBytes);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, binding);
            string recordCoordinate = Encoding.ASCII.GetString(plaintext);
            if (!Guid.TryParseExact(recordCoordinate, "N", out Guid recordId) ||
                !this.TryValidateLedgerSubject(ledger, recordId))
            {
                return InvalidGuid();
            }

            return Result.Success(recordId);
        }
        catch (CryptographicException)
        {
            return InvalidGuid();
        }
        finally
        {
            Zero(
                masterKey,
                binding,
                encryptionKey,
                nonceKey,
                nonceDigest,
                expectedNonce,
                nonce,
                ciphertext,
                tag,
                plaintext);
        }
    }

    private bool TryValidateLedgerSubject(
        DataRightsProcessingLedgerSnapshot? ledger,
        Guid recordId)
    {
        if (ledger is null ||
            recordId == Guid.Empty ||
            DataRightsProcessingLedgerEntry.Restore(ledger).IsFailure)
        {
            return false;
        }

        Result<DataRightsRecordPseudonym> pseudonym = pseudonymizer.Create(
            ledger.RecordPseudonymKeyVersion,
            ledger.ScopeId,
            ledger.OwnerKey,
            ledger.RecordType,
            recordId);
        if (pseudonym.IsFailure)
        {
            return false;
        }

        byte[] expected = Convert.FromHexString(pseudonym.Value.Sha256);
        byte[] actual = Convert.FromHexString(ledger.RecordPseudonymSha256);
        try
        {
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            Zero(expected, actual);
        }
    }

    private bool TryGetKey(int keyVersion, out byte[] key)
    {
        key = [];
        return keyVersion > 0 &&
            this.options.Keys.TryGetValue(keyVersion, out string? encodedKey) &&
            DataRightsPseudonymisationOptionsValidator.TryDecode(
                encodedKey,
                out key);
    }

    private static byte[] CreateBinding(
        DataRightsProcessingLedgerSnapshot ledger)
    {
        StringBuilder canonical = new();
        Append(canonical, BindingDomain);
        Append(canonical, ledger.ContractVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ledger.EntryId.ToString("N"));
        Append(canonical, ledger.ScopeId);
        Append(canonical, ledger.TenantSequence.ToString(CultureInfo.InvariantCulture));
        Append(canonical, ledger.EntrySha256);
        Append(canonical, ledger.OwnerKey);
        Append(canonical, ledger.RecordType);
        Append(
            canonical,
            ledger.RecordPseudonymKeyVersion.ToString(
                CultureInfo.InvariantCulture));
        Append(canonical, ledger.RecordPseudonymSha256);
        return Encoding.UTF8.GetBytes(canonical.ToString());
    }

    private static byte[] Derive(byte[] masterKey, string domain) =>
        HMACSHA256.HashData(masterKey, Encoding.ASCII.GetBytes(domain));

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }

    private static void Zero(params byte[][] buffers)
    {
        foreach (byte[] buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static Result<DataRightsProtectedReplayEnvelope> Invalid() =>
        Result.Failure<DataRightsProtectedReplayEnvelope>(
            DataRightsDomainErrors.ReplayEnvelopeInvalid);

    private static Result<DataRightsProtectedReplayEnvelope> KeyUnavailable() =>
        Result.Failure<DataRightsProtectedReplayEnvelope>(
            DataRightsDomainErrors.ReplayEnvelopeKeyUnavailable);

    private static Result<Guid> InvalidGuid() =>
        Result.Failure<Guid>(DataRightsDomainErrors.ReplayEnvelopeInvalid);

    private static Result<Guid> KeyUnavailableGuid() =>
        Result.Failure<Guid>(
            DataRightsDomainErrors.ReplayEnvelopeKeyUnavailable);
}
