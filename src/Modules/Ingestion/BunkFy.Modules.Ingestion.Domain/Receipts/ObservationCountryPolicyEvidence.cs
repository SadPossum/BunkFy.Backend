namespace BunkFy.Modules.Ingestion.Domain.Receipts;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Results;

public sealed class ObservationCountryPolicyEvidence
{
    public const int CountryCodeLength = 2;
    public const int PolicyKeyMaxLength = 100;
    public const int ContentSha256Length = 64;
    public const int ProcessingSurfaceMaxLength = 50;

    private ObservationCountryPolicyEvidence() { }

    private ObservationCountryPolicyEvidence(
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string dataRegionId,
        string transferProfileId,
        string retentionPolicyId,
        int retentionPolicyVersion,
        string contentSha256,
        string purposeCode,
        string processingSurface,
        string sourceProvenance,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset evaluatedAtUtc)
    {
        this.OperatingCountryCode = operatingCountryCode;
        this.PolicyId = policyId;
        this.PolicyVersion = policyVersion;
        this.DataRegionId = dataRegionId;
        this.TransferProfileId = transferProfileId;
        this.RetentionPolicyId = retentionPolicyId;
        this.RetentionPolicyVersion = retentionPolicyVersion;
        this.ContentSha256 = contentSha256;
        this.PurposeCode = purposeCode;
        this.ProcessingSurface = processingSurface;
        this.SourceProvenance = sourceProvenance;
        this.PolicyEffectiveAtUtc = policyEffectiveAtUtc;
        this.PolicyExpiresAtUtc = policyExpiresAtUtc;
        this.EvaluatedAtUtc = evaluatedAtUtc;
    }

    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string DataRegionId { get; private set; } = string.Empty;
    public string TransferProfileId { get; private set; } = string.Empty;
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public string PurposeCode { get; private set; } = string.Empty;
    public string ProcessingSurface { get; private set; } = string.Empty;
    public string SourceProvenance { get; private set; } = string.Empty;
    public DateTimeOffset PolicyEffectiveAtUtc { get; private set; }
    public DateTimeOffset PolicyExpiresAtUtc { get; private set; }
    public DateTimeOffset EvaluatedAtUtc { get; private set; }

    public static Result<ObservationCountryPolicyEvidence> Create(
        string? operatingCountryCode,
        string? policyId,
        int policyVersion,
        string? dataRegionId,
        string? transferProfileId,
        string? retentionPolicyId,
        int retentionPolicyVersion,
        string? contentSha256,
        string? purposeCode,
        string? processingSurface,
        string? sourceProvenance,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset evaluatedAtUtc)
    {
        string country = operatingCountryCode?.Trim() ?? string.Empty;
        string policy = policyId?.Trim() ?? string.Empty;
        string region = dataRegionId?.Trim() ?? string.Empty;
        string transfer = transferProfileId?.Trim() ?? string.Empty;
        string retention = retentionPolicyId?.Trim() ?? string.Empty;
        string digest = contentSha256?.Trim() ?? string.Empty;
        string purpose = purposeCode?.Trim() ?? string.Empty;
        string surface = processingSurface?.Trim() ?? string.Empty;
        string provenance = sourceProvenance?.Trim() ?? string.Empty;

        if (!IsCountryCode(country) || !IsKey(policy) || policyVersion <= 0 ||
            !IsKey(region) || !IsKey(transfer) || !IsKey(retention) || retentionPolicyVersion <= 0 ||
            !IsSha256(digest) || !IsKey(purpose) || !IsKey(provenance) ||
            surface.Length is 0 or > ProcessingSurfaceMaxLength || !IsKeyText(surface) ||
            policyEffectiveAtUtc == default || policyExpiresAtUtc <= policyEffectiveAtUtc ||
            evaluatedAtUtc < policyEffectiveAtUtc || evaluatedAtUtc >= policyExpiresAtUtc)
        {
            return Result.Failure<ObservationCountryPolicyEvidence>(
                IngestionDomainErrors.CountryPolicyEvidenceInvalid);
        }

        return Result.Success(new ObservationCountryPolicyEvidence(
            country,
            policy,
            policyVersion,
            region,
            transfer,
            retention,
            retentionPolicyVersion,
            digest,
            purpose,
            surface,
            provenance,
            policyEffectiveAtUtc,
            policyExpiresAtUtc,
            evaluatedAtUtc));
    }

    private static bool IsCountryCode(string value) =>
        value.Length == CountryCodeLength && value.All(character => character is >= 'A' and <= 'Z');

    private static bool IsKey(string value) =>
        value.Length is > 0 and <= PolicyKeyMaxLength && IsKeyText(value);

    private static bool IsKeyText(string value) =>
        value[0] is >= 'a' and <= 'z' && value.All(character =>
            character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_');

    private static bool IsSha256(string value) =>
        value.Length == ContentSha256Length && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
