namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public sealed class PropertyGovernanceBinding
{
    private PropertyGovernanceBinding() { }

    private PropertyGovernanceBinding(
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string dataRegionId,
        string transferProfileId,
        string retentionPolicyId,
        int retentionPolicyVersion,
        string contentSha256,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset activatedAtUtc)
    {
        this.OperatingCountryCode = operatingCountryCode;
        this.PolicyId = policyId;
        this.PolicyVersion = policyVersion;
        this.DataRegionId = dataRegionId;
        this.TransferProfileId = transferProfileId;
        this.RetentionPolicyId = retentionPolicyId;
        this.RetentionPolicyVersion = retentionPolicyVersion;
        this.ContentSha256 = contentSha256;
        this.PolicyEffectiveAtUtc = policyEffectiveAtUtc;
        this.PolicyExpiresAtUtc = policyExpiresAtUtc;
        this.ActivatedAtUtc = activatedAtUtc;
    }

    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string DataRegionId { get; private set; } = string.Empty;
    public string TransferProfileId { get; private set; } = string.Empty;
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public DateTimeOffset PolicyEffectiveAtUtc { get; private set; }
    public DateTimeOffset PolicyExpiresAtUtc { get; private set; }
    public DateTimeOffset ActivatedAtUtc { get; private set; }

    public static Result<PropertyGovernanceBinding> Create(
        string? operatingCountryCode,
        string? policyId,
        int policyVersion,
        string? dataRegionId,
        string? transferProfileId,
        string? retentionPolicyId,
        int retentionPolicyVersion,
        string? contentSha256,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset activatedAtUtc)
    {
        string country = operatingCountryCode?.Trim() ?? string.Empty;
        string normalizedPolicyId = policyId?.Trim() ?? string.Empty;
        string region = dataRegionId?.Trim() ?? string.Empty;
        string transfer = transferProfileId?.Trim() ?? string.Empty;
        string retention = retentionPolicyId?.Trim() ?? string.Empty;
        string digest = contentSha256?.Trim() ?? string.Empty;

        if (!IsCountryCode(country) ||
            !IsKey(normalizedPolicyId) || policyVersion <= 0 ||
            !IsKey(region) || !IsKey(transfer) ||
            !IsKey(retention) || retentionPolicyVersion <= 0 ||
            !IsSha256(digest) ||
            policyEffectiveAtUtc == default || policyExpiresAtUtc <= policyEffectiveAtUtc ||
            activatedAtUtc < policyEffectiveAtUtc || activatedAtUtc >= policyExpiresAtUtc)
        {
            return Result.Failure<PropertyGovernanceBinding>(PropertiesDomainErrors.PolicyBindingInvalid);
        }

        return Result.Success(new PropertyGovernanceBinding(
            country,
            normalizedPolicyId,
            policyVersion,
            region,
            transfer,
            retention,
            retentionPolicyVersion,
            digest,
            policyEffectiveAtUtc,
            policyExpiresAtUtc,
            activatedAtUtc));
    }

    private static bool IsCountryCode(string value) =>
        value.Length == Property.CountryCodeLength && value.All(character => character is >= 'A' and <= 'Z');

    private static bool IsKey(string value) =>
        value.Length is > 0 and <= Property.PolicyKeyMaxLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_');

    private static bool IsSha256(string value) =>
        value.Length == Property.ContentSha256Length && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
