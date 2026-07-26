namespace BunkFy.Modules.Ingestion.Persistence;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Microsoft.Extensions.Options;

internal sealed class HmacIngestionAnonymisationFingerprintService(
    IOptions<IngestionAnonymisationFingerprintOptions> options)
    : IIngestionAnonymisationFingerprintService
{
    private const string Domain =
        "bunkfy.ingestion.anonymisation-fingerprint.v1";

    private readonly IngestionAnonymisationFingerprintOptions options =
        options.Value;

    public Result<IngestionAnonymisationFingerprintValue> CreateActive(
        string tenantId,
        IngestionAnonymisationFingerprintPurpose purpose,
        Guid connectionId,
        string recordType,
        string externalId) =>
        this.Create(
            this.options.ActiveKeyVersion,
            tenantId,
            purpose,
            connectionId,
            recordType,
            externalId);

    public Result<IReadOnlyList<IngestionAnonymisationFingerprintValue>>
        CreateCandidates(
            string tenantId,
            IngestionAnonymisationFingerprintPurpose purpose,
            Guid connectionId,
            string recordType,
            string externalId)
    {
        List<IngestionAnonymisationFingerprintValue> candidates = [];
        foreach (int keyVersion in this.options.Keys.Keys.Order())
        {
            Result<IngestionAnonymisationFingerprintValue> candidate =
                this.Create(
                    keyVersion,
                    tenantId,
                    purpose,
                    connectionId,
                    recordType,
                    externalId);
            if (candidate.IsFailure)
            {
                return Result.Failure<
                    IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                    candidate.Error);
            }

            candidates.Add(candidate.Value);
        }

        return candidates.Count > 0
            ? Result.Success<
                IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                candidates)
            : Result.Failure<
                IReadOnlyList<IngestionAnonymisationFingerprintValue>>(
                IngestionDomainErrors.AnonymisationFingerprintKeyUnavailable);
    }

    private Result<IngestionAnonymisationFingerprintValue> Create(
        int keyVersion,
        string tenantId,
        IngestionAnonymisationFingerprintPurpose purpose,
        Guid connectionId,
        string recordType,
        string externalId)
    {
        string type = recordType?.Trim().ToLowerInvariant() ?? string.Empty;
        string external = externalId?.Trim() ?? string.Empty;
        if (keyVersion <= 0 ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            !Enum.IsDefined(purpose) ||
            purpose == IngestionAnonymisationFingerprintPurpose.Unknown ||
            connectionId == Guid.Empty ||
            type.Length is 0 or > 100 ||
            external.Length is 0 or > 512)
        {
            return Result.Failure<IngestionAnonymisationFingerprintValue>(
                IngestionDomainErrors.AnonymisationFingerprintInvalid);
        }

        if (!this.options.Keys.TryGetValue(
                keyVersion,
                out string? encodedKey) ||
            !IngestionAnonymisationFingerprintOptionsValidator.TryDecode(
                encodedKey,
                out byte[] key))
        {
            return Result.Failure<IngestionAnonymisationFingerprintValue>(
                IngestionDomainErrors
                    .AnonymisationFingerprintKeyUnavailable);
        }

        byte[] input = CreateCanonicalInput(
            keyVersion,
            scopeId,
            purpose,
            connectionId,
            type,
            external);
        try
        {
            byte[] digest = HMACSHA256.HashData(key, input);
            try
            {
                return Result.Success(
                    new IngestionAnonymisationFingerprintValue(
                        purpose,
                        keyVersion,
                        Convert.ToHexStringLower(digest)));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] CreateCanonicalInput(
        int keyVersion,
        string scopeId,
        IngestionAnonymisationFingerprintPurpose purpose,
        Guid connectionId,
        string recordType,
        string externalId)
    {
        StringBuilder canonical = new();
        Append(canonical, Domain);
        Append(
            canonical,
            keyVersion.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            ((int)purpose).ToString(CultureInfo.InvariantCulture));
        Append(canonical, scopeId);
        Append(canonical, connectionId.ToString("N"));
        Append(canonical, recordType);
        Append(canonical, externalId);
        return Encoding.UTF8.GetBytes(canonical.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(
            value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }
}
