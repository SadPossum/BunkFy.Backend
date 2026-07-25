namespace BunkFy.Modules.DataRights.Persistence;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Microsoft.Extensions.Options;

internal sealed class HmacDataRightsRecordPseudonymizer(
    IOptions<DataRightsPseudonymisationOptions> options)
    : IDataRightsRecordPseudonymizer
{
    private const string Domain = "bunkfy.data-rights.record-pseudonym.v1";
    private readonly DataRightsPseudonymisationOptions options = options.Value;

    public Result<DataRightsRecordPseudonym> CreateActive(
        string tenantId,
        string ownerKey,
        string recordType,
        Guid recordId) =>
        this.Create(
            this.options.ActiveKeyVersion,
            tenantId,
            ownerKey,
            recordType,
            recordId);

    public Result<DataRightsRecordPseudonym> Create(
        int keyVersion,
        string tenantId,
        string ownerKey,
        string recordType,
        Guid recordId)
    {
        if (keyVersion <= 0 ||
            recordId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<DataRightsRecordPseudonym>(
                DataRightsDomainErrors.RecordPseudonymInvalid);
        }

        string owner = ownerKey?.Trim() ?? string.Empty;
        string type = recordType?.Trim() ?? string.Empty;
        if (owner.Length is 0 or > DataRightsSubjectCoordinate.OwnerKeyMaxLength ||
            type.Length is 0 or > DataRightsSubjectCoordinate.RecordTypeMaxLength)
        {
            return Result.Failure<DataRightsRecordPseudonym>(
                DataRightsDomainErrors.RecordPseudonymInvalid);
        }

        if (!this.options.Keys.TryGetValue(keyVersion, out string? encodedKey) ||
            !DataRightsPseudonymisationOptionsValidator.TryDecode(
                encodedKey,
                out byte[] key))
        {
            return Result.Failure<DataRightsRecordPseudonym>(
                DataRightsDomainErrors.RecordPseudonymKeyUnavailable);
        }

        byte[] input = CreateCanonicalInput(
            keyVersion,
            scopeId,
            owner,
            type,
            recordId);
        try
        {
            byte[] digest = HMACSHA256.HashData(key, input);
            try
            {
                return DataRightsRecordPseudonym.Create(
                    keyVersion,
                    Convert.ToHexStringLower(digest));
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
        string ownerKey,
        string recordType,
        Guid recordId)
    {
        StringBuilder canonical = new();
        Append(canonical, Domain);
        Append(canonical, keyVersion.ToString(CultureInfo.InvariantCulture));
        Append(canonical, scopeId);
        Append(canonical, ownerKey);
        Append(canonical, recordType);
        Append(canonical, recordId.ToString("N"));
        return Encoding.UTF8.GetBytes(canonical.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(value);
    }
}
