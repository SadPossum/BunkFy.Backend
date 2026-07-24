namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using Gma.Framework.Results;
using BunkFy.Modules.DataRights.Domain.Errors;

public sealed class DataRightsApprovalPolicyEvidence
{
    public const int CurrentSchemaVersion = 1;
    public const int CountryCodeLength = 2;
    public const int KeyMaxLength = 128;
    public const int ContentSha256Length = 64;

    private DataRightsApprovalPolicyEvidence() { }

    private DataRightsApprovalPolicyEvidence(
        Guid propertyId,
        long propertyVersion,
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string retentionPolicyId,
        int retentionPolicyVersion,
        string contentSha256,
        string purposeCode,
        string surface,
        string sourceProvenance,
        DateTimeOffset evaluatedAtUtc)
    {
        this.SchemaVersion = CurrentSchemaVersion;
        this.PropertyId = propertyId;
        this.PropertyVersion = propertyVersion;
        this.OperatingCountryCode = operatingCountryCode;
        this.PolicyId = policyId;
        this.PolicyVersion = policyVersion;
        this.RetentionPolicyId = retentionPolicyId;
        this.RetentionPolicyVersion = retentionPolicyVersion;
        this.ContentSha256 = contentSha256;
        this.PurposeCode = purposeCode;
        this.Surface = surface;
        this.SourceProvenance = sourceProvenance;
        this.EvaluatedAtUtc = evaluatedAtUtc;
        this.RequiresDistinctExecutor = true;
    }

    public int SchemaVersion { get; private set; }
    public Guid PropertyId { get; private set; }
    public long PropertyVersion { get; private set; }
    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public string PurposeCode { get; private set; } = string.Empty;
    public string Surface { get; private set; } = string.Empty;
    public string SourceProvenance { get; private set; } = string.Empty;
    public DateTimeOffset EvaluatedAtUtc { get; private set; }
    public bool RequiresDistinctExecutor { get; private set; }

    public static Result<DataRightsApprovalPolicyEvidence> Create(
        Guid propertyId,
        long propertyVersion,
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string retentionPolicyId,
        int retentionPolicyVersion,
        string contentSha256,
        string purposeCode,
        string surface,
        string sourceProvenance,
        DateTimeOffset evaluatedAtUtc)
    {
        string country = operatingCountryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        string policy = policyId?.Trim().ToLowerInvariant() ?? string.Empty;
        string retention = retentionPolicyId?.Trim().ToLowerInvariant() ?? string.Empty;
        string digest = contentSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        string purpose = purposeCode?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedSurface = surface?.Trim().ToLowerInvariant() ?? string.Empty;
        string provenance = sourceProvenance?.Trim().ToLowerInvariant() ?? string.Empty;

        if (propertyId == Guid.Empty ||
            propertyVersion <= 0 ||
            country.Length != CountryCodeLength ||
            country.Any(character => character is < 'A' or > 'Z') ||
            !IsKey(policy) ||
            policyVersion <= 0 ||
            !IsKey(retention) ||
            retentionPolicyVersion <= 0 ||
            !IsSha256(digest) ||
            !IsKey(purpose) ||
            !IsKey(normalizedSurface) ||
            !IsKey(provenance) ||
            evaluatedAtUtc == default)
        {
            return Result.Failure<DataRightsApprovalPolicyEvidence>(
                DataRightsDomainErrors.ApprovalPolicyEvidenceInvalid);
        }

        return Result.Success(new DataRightsApprovalPolicyEvidence(
            propertyId,
            propertyVersion,
            country,
            policy,
            policyVersion,
            retention,
            retentionPolicyVersion,
            digest,
            purpose,
            normalizedSurface,
            provenance,
            evaluatedAtUtc));
    }

    private static bool IsKey(string value) =>
        value.Length is > 0 and <= KeyMaxLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_');

    private static bool IsSha256(string value) =>
        value.Length == ContentSha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
