namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsApprovalPolicyEvidence
{
    public const int MinimumSupportedSchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;
    public const int CountryCodeLength = 2;
    public const int KeyMaxLength = 128;
    public const int ContentSha256Length = 64;
    public const int MaximumStateBindings = 8;
    public const int StateBindingsJsonMaxLength = 4_096;

    private static readonly JsonSerializerOptions BindingSerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false
        };

    private DataRightsApprovalPolicyEvidence() { }

    private DataRightsApprovalPolicyEvidence(
        int schemaVersion,
        DataRightsCaseKind caseKind,
        DataRightsCaseScopeKind scopeKind,
        Guid? propertyId,
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
        string retentionDataClass,
        string retentionTrigger,
        DateTimeOffset? retentionTriggeredAtUtc,
        DateTimeOffset? retentionDeadlineUtc,
        DateTimeOffset evaluatedAtUtc,
        string stateBindingsJson,
        string stateBindingsSha256,
        bool requiresDistinctExecutor)
    {
        this.SchemaVersion = schemaVersion;
        this.CaseKind = caseKind;
        this.ScopeKind = scopeKind;
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
        this.RetentionDataClass = retentionDataClass;
        this.RetentionTrigger = retentionTrigger;
        this.RetentionTriggeredAtUtc = retentionTriggeredAtUtc;
        this.RetentionDeadlineUtc = retentionDeadlineUtc;
        this.EvaluatedAtUtc = evaluatedAtUtc;
        this.StateBindingsJson = stateBindingsJson;
        this.StateBindingsSha256 = stateBindingsSha256;
        this.RequiresDistinctExecutor = requiresDistinctExecutor;
    }

    public int SchemaVersion { get; private set; }
    public DataRightsCaseKind CaseKind { get; private set; } =
        DataRightsCaseKind.GuestRights;
    public DataRightsCaseScopeKind ScopeKind { get; private set; } =
        DataRightsCaseScopeKind.Property;
    public Guid? PropertyId { get; private set; }
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
    public string RetentionDataClass { get; private set; } = string.Empty;
    public string RetentionTrigger { get; private set; } = string.Empty;
    public DateTimeOffset? RetentionTriggeredAtUtc { get; private set; }
    public DateTimeOffset? RetentionDeadlineUtc { get; private set; }
    public DateTimeOffset EvaluatedAtUtc { get; private set; }
    public string StateBindingsJson { get; private set; } = "[]";
    public string StateBindingsSha256 { get; private set; } =
        ComputeSha256("[]");
    public bool RequiresDistinctExecutor { get; private set; }

    public IReadOnlyCollection<DataRightsApprovalEvidenceBinding>
        StateBindings =>
        TryReadStateBindings(
            this.StateBindingsJson,
            out DataRightsApprovalEvidenceBinding[] bindings)
            ? bindings
            : [];

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
        NormalizedPolicyEvidence normalized = Normalize(
            operatingCountryCode,
            policyId,
            retentionPolicyId,
            contentSha256,
            purposeCode,
            surface,
            sourceProvenance);
        if (propertyId == Guid.Empty ||
            propertyVersion <= 0 ||
            !IsCommonValid(
                normalized,
                policyVersion,
                retentionPolicyVersion,
                evaluatedAtUtc))
        {
            return Invalid();
        }

        const string emptyBindings = "[]";
        return Result.Success(new DataRightsApprovalPolicyEvidence(
            MinimumSupportedSchemaVersion,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseScopeKind.Property,
            propertyId,
            propertyVersion,
            normalized.Country,
            normalized.Policy,
            policyVersion,
            normalized.Retention,
            retentionPolicyVersion,
            normalized.Digest,
            normalized.Purpose,
            normalized.Surface,
            normalized.Provenance,
            retentionDataClass: string.Empty,
            retentionTrigger: string.Empty,
            retentionTriggeredAtUtc: null,
            retentionDeadlineUtc: null,
            evaluatedAtUtc,
            emptyBindings,
            ComputeSha256(emptyBindings),
            requiresDistinctExecutor: true));
    }

    public bool HasValidShape()
    {
        NormalizedPolicyEvidence normalized = Normalize(
            this.OperatingCountryCode,
            this.PolicyId,
            this.RetentionPolicyId,
            this.ContentSha256,
            this.PurposeCode,
            this.Surface,
            this.SourceProvenance);
        if (!IsCommonValid(
                normalized,
                this.PolicyVersion,
                this.RetentionPolicyVersion,
                this.EvaluatedAtUtc) ||
            !string.Equals(
                normalized.Country,
                this.OperatingCountryCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Policy,
                this.PolicyId,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Retention,
                this.RetentionPolicyId,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Digest,
                this.ContentSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Purpose,
                this.PurposeCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Surface,
                this.Surface,
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized.Provenance,
                this.SourceProvenance,
                StringComparison.Ordinal) ||
            !this.RequiresDistinctExecutor)
        {
            return false;
        }

        if (this.SchemaVersion == MinimumSupportedSchemaVersion)
        {
            return this.CaseKind == DataRightsCaseKind.GuestRights &&
                this.ScopeKind == DataRightsCaseScopeKind.Property &&
                this.PropertyId is Guid propertyId &&
                propertyId != Guid.Empty &&
                this.PropertyVersion > 0 &&
                this.RetentionDataClass.Length == 0 &&
                this.RetentionTrigger.Length == 0 &&
                this.RetentionTriggeredAtUtc is null &&
                this.RetentionDeadlineUtc is null &&
                string.Equals(
                    this.StateBindingsJson,
                    "[]",
                    StringComparison.Ordinal) &&
                string.Equals(
                    this.StateBindingsSha256,
                    ComputeSha256("[]"),
                    StringComparison.Ordinal);
        }

        return this.SchemaVersion == CurrentSchemaVersion &&
            DataRightsApprovalEvidenceBinding.IsKey(
                this.RetentionDataClass) &&
            DataRightsApprovalEvidenceBinding.IsKey(
                this.RetentionTrigger) &&
            this.RetentionTriggeredAtUtc is DateTimeOffset triggered &&
            triggered.Offset == TimeSpan.Zero &&
            this.RetentionDeadlineUtc is DateTimeOffset deadline &&
            deadline.Offset == TimeSpan.Zero &&
            deadline > triggered &&
            this.EvaluatedAtUtc.Offset == TimeSpan.Zero &&
            deadline <= this.EvaluatedAtUtc &&
            TryReadStateBindings(
                this.StateBindingsJson,
                out DataRightsApprovalEvidenceBinding[] bindings) &&
            TryFreezeStateBindings(
                bindings,
                out string? canonicalJson,
                out string? canonicalSha256) &&
            string.Equals(
                canonicalJson,
                this.StateBindingsJson,
                StringComparison.Ordinal) &&
            string.Equals(
                canonicalSha256,
                this.StateBindingsSha256,
                StringComparison.Ordinal) &&
            IsScopeValid(this);
    }

    private static bool IsScopeValid(
        DataRightsApprovalPolicyEvidence evidence) =>
        evidence.ScopeKind switch
        {
            DataRightsCaseScopeKind.Property =>
                evidence.CaseKind == DataRightsCaseKind.GuestRights &&
                evidence.PropertyId is Guid propertyId &&
                propertyId != Guid.Empty &&
                evidence.PropertyVersion > 0,
            DataRightsCaseScopeKind.Tenant =>
                evidence.CaseKind is DataRightsCaseKind.StaffRights or
                    DataRightsCaseKind.TenantTermination &&
                evidence.PropertyId is null &&
                evidence.PropertyVersion == 0,
            _ => false
        };

    private static bool IsCommonValid(
        NormalizedPolicyEvidence evidence,
        int policyVersion,
        int retentionPolicyVersion,
        DateTimeOffset evaluatedAtUtc) =>
        evidence.Country.Length == CountryCodeLength &&
        evidence.Country.All(character => character is >= 'A' and <= 'Z') &&
        DataRightsApprovalEvidenceBinding.IsKey(evidence.Policy) &&
        policyVersion > 0 &&
        DataRightsApprovalEvidenceBinding.IsKey(evidence.Retention) &&
        retentionPolicyVersion > 0 &&
        DataRightsApprovalEvidenceBinding.IsSha256(evidence.Digest) &&
        DataRightsApprovalEvidenceBinding.IsKey(evidence.Purpose) &&
        DataRightsApprovalEvidenceBinding.IsKey(evidence.Surface) &&
        DataRightsApprovalEvidenceBinding.IsKey(evidence.Provenance) &&
        evaluatedAtUtc != default;

    private static NormalizedPolicyEvidence Normalize(
        string operatingCountryCode,
        string policyId,
        string retentionPolicyId,
        string contentSha256,
        string purposeCode,
        string surface,
        string sourceProvenance) =>
        new(
            operatingCountryCode?.Trim().ToUpperInvariant() ??
                string.Empty,
            policyId?.Trim().ToLowerInvariant() ?? string.Empty,
            retentionPolicyId?.Trim().ToLowerInvariant() ??
                string.Empty,
            contentSha256?.Trim().ToLowerInvariant() ?? string.Empty,
            purposeCode?.Trim().ToLowerInvariant() ?? string.Empty,
            surface?.Trim().ToLowerInvariant() ?? string.Empty,
            sourceProvenance?.Trim().ToLowerInvariant() ??
                string.Empty);

    private static string ComputeSha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Result<DataRightsApprovalPolicyEvidence> Invalid() =>
        Result.Failure<DataRightsApprovalPolicyEvidence>(
            DataRightsDomainErrors.ApprovalPolicyEvidenceInvalid);

    private sealed record BindingPayload(
        string Key,
        long Version,
        string Sha256);

    private sealed record NormalizedPolicyEvidence(
        string Country,
        string Policy,
        string Retention,
        string Digest,
        string Purpose,
        string Surface,
        string Provenance);
}
