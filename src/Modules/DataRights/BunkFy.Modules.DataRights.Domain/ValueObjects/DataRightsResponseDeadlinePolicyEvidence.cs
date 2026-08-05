namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed class DataRightsResponseDeadlinePolicyEvidence
{
    public const int CurrentSchemaVersion = 1;
    public const int CountryCodeLength = 2;
    public const int KeyMaxLength = 128;
    public const int ContentSha256Length = 64;
    public const int TimeZoneIdMaxLength = 128;

    private DataRightsResponseDeadlinePolicyEvidence() { }

    private DataRightsResponseDeadlinePolicyEvidence(
        Guid propertyId,
        long propertyTopologySourceVersion,
        long propertyPolicySourceVersion,
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string contentSha256,
        DataRightsResponseRight controllingRight,
        string ruleReference,
        int periodYears,
        int periodMonths,
        int periodDays,
        string timeZoneId,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        DateTimeOffset dueAtUtc)
    {
        this.SchemaVersion = CurrentSchemaVersion;
        this.PropertyId = propertyId;
        this.PropertyTopologySourceVersion = propertyTopologySourceVersion;
        this.PropertyPolicySourceVersion = propertyPolicySourceVersion;
        this.OperatingCountryCode = operatingCountryCode;
        this.PolicyId = policyId;
        this.PolicyVersion = policyVersion;
        this.ContentSha256 = contentSha256;
        this.ControllingRight = controllingRight;
        this.RuleReference = ruleReference;
        this.PeriodYears = periodYears;
        this.PeriodMonths = periodMonths;
        this.PeriodDays = periodDays;
        this.TimeZoneId = timeZoneId;
        this.PolicyEffectiveAtUtc = policyEffectiveAtUtc;
        this.PolicyExpiresAtUtc = policyExpiresAtUtc;
        this.ReceivedAtUtc = receivedAtUtc;
        this.EvaluatedAtUtc = evaluatedAtUtc;
        this.DueAtUtc = dueAtUtc;
    }

    public int SchemaVersion { get; private set; }
    public Guid PropertyId { get; private set; }
    public long PropertyTopologySourceVersion { get; private set; }
    public long PropertyPolicySourceVersion { get; private set; }
    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public DataRightsResponseRight ControllingRight { get; private set; }
    public string RuleReference { get; private set; } = string.Empty;
    public int PeriodYears { get; private set; }
    public int PeriodMonths { get; private set; }
    public int PeriodDays { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTimeOffset PolicyEffectiveAtUtc { get; private set; }
    public DateTimeOffset PolicyExpiresAtUtc { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset EvaluatedAtUtc { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }

    public static Result<DataRightsResponseDeadlinePolicyEvidence> Create(
        Guid propertyId,
        long propertyTopologySourceVersion,
        long propertyPolicySourceVersion,
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string contentSha256,
        DataRightsResponseRight controllingRight,
        string ruleReference,
        int periodYears,
        int periodMonths,
        int periodDays,
        string timeZoneId,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        DateTimeOffset dueAtUtc)
    {
        string country = operatingCountryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        string normalizedPolicyId = policyId?.Trim().ToLowerInvariant() ?? string.Empty;
        string digest = contentSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedRule = ruleReference?.Trim().ToLowerInvariant() ?? string.Empty;
        string zone = timeZoneId?.Trim() ?? string.Empty;
        if (propertyId == Guid.Empty ||
            propertyTopologySourceVersion <= 0 ||
            propertyPolicySourceVersion <= 0 ||
            country.Length != CountryCodeLength ||
            country.Any(character => character is < 'A' or > 'Z') ||
            !DataRightsApprovalEvidenceBinding.IsKey(normalizedPolicyId) ||
            policyVersion <= 0 ||
            !DataRightsApprovalEvidenceBinding.IsSha256(digest) ||
            !Enum.IsDefined(controllingRight) ||
            controllingRight == DataRightsResponseRight.Unknown ||
            !DataRightsApprovalEvidenceBinding.IsKey(normalizedRule) ||
            !IsPeriodValid(periodYears, periodMonths, periodDays) ||
            string.IsNullOrWhiteSpace(zone) ||
            zone.Length > TimeZoneIdMaxLength ||
            zone.Any(char.IsControl) ||
            !IsUtc(policyEffectiveAtUtc) ||
            !IsUtc(policyExpiresAtUtc) ||
            !IsUtc(receivedAtUtc) ||
            !IsUtc(evaluatedAtUtc) ||
            !IsUtc(dueAtUtc) ||
            policyEffectiveAtUtc >= policyExpiresAtUtc ||
            receivedAtUtc < policyEffectiveAtUtc ||
            receivedAtUtc >= policyExpiresAtUtc ||
            evaluatedAtUtc < receivedAtUtc ||
            dueAtUtc <= receivedAtUtc)
        {
            return Invalid();
        }

        return Result.Success(new DataRightsResponseDeadlinePolicyEvidence(
            propertyId,
            propertyTopologySourceVersion,
            propertyPolicySourceVersion,
            country,
            normalizedPolicyId,
            policyVersion,
            digest,
            controllingRight,
            normalizedRule,
            periodYears,
            periodMonths,
            periodDays,
            zone,
            policyEffectiveAtUtc,
            policyExpiresAtUtc,
            receivedAtUtc,
            evaluatedAtUtc,
            dueAtUtc));
    }

    public bool HasSameCoordinates(DataRightsResponseDeadlinePolicyEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return this.SchemaVersion == other.SchemaVersion &&
            this.PropertyId == other.PropertyId &&
            this.PropertyTopologySourceVersion == other.PropertyTopologySourceVersion &&
            this.PropertyPolicySourceVersion == other.PropertyPolicySourceVersion &&
            string.Equals(this.OperatingCountryCode, other.OperatingCountryCode, StringComparison.Ordinal) &&
            string.Equals(this.PolicyId, other.PolicyId, StringComparison.Ordinal) &&
            this.PolicyVersion == other.PolicyVersion &&
            string.Equals(this.ContentSha256, other.ContentSha256, StringComparison.Ordinal) &&
            this.ControllingRight == other.ControllingRight &&
            string.Equals(this.RuleReference, other.RuleReference, StringComparison.Ordinal) &&
            this.PeriodYears == other.PeriodYears &&
            this.PeriodMonths == other.PeriodMonths &&
            this.PeriodDays == other.PeriodDays &&
            string.Equals(this.TimeZoneId, other.TimeZoneId, StringComparison.Ordinal) &&
            this.PolicyEffectiveAtUtc == other.PolicyEffectiveAtUtc &&
            this.PolicyExpiresAtUtc == other.PolicyExpiresAtUtc &&
            this.ReceivedAtUtc == other.ReceivedAtUtc &&
            this.EvaluatedAtUtc == other.EvaluatedAtUtc &&
            this.DueAtUtc == other.DueAtUtc;
    }

    public bool HasValidShape()
    {
        Result<DataRightsResponseDeadlinePolicyEvidence> normalized = Create(
            this.PropertyId,
            this.PropertyTopologySourceVersion,
            this.PropertyPolicySourceVersion,
            this.OperatingCountryCode,
            this.PolicyId,
            this.PolicyVersion,
            this.ContentSha256,
            this.ControllingRight,
            this.RuleReference,
            this.PeriodYears,
            this.PeriodMonths,
            this.PeriodDays,
            this.TimeZoneId,
            this.PolicyEffectiveAtUtc,
            this.PolicyExpiresAtUtc,
            this.ReceivedAtUtc,
            this.EvaluatedAtUtc,
            this.DueAtUtc);
        return normalized.IsSuccess && this.HasSameCoordinates(normalized.Value);
    }

    private static bool IsPeriodValid(int years, int months, int days) =>
        years is >= 0 and <= 10 &&
        months is >= 0 and <= 11 &&
        days is >= 0 and <= 366 &&
        years + months + days > 0;

    private static bool IsUtc(DateTimeOffset value) =>
        value != default && value.Offset == TimeSpan.Zero;

    private static Result<DataRightsResponseDeadlinePolicyEvidence> Invalid() =>
        Result.Failure<DataRightsResponseDeadlinePolicyEvidence>(
            DataRightsDomainErrors.ResponseDeadlinePolicyEvidenceInvalid);
}
