namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using System.Text.Json;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class DataRightsApprovalPolicyEvidence
{
    public static Result<DataRightsApprovalPolicyEvidence> CreateScoped(
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
        DateTimeOffset retentionTriggeredAtUtc,
        DateTimeOffset retentionDeadlineUtc,
        DateTimeOffset evaluatedAtUtc,
        IReadOnlyCollection<DataRightsApprovalEvidenceBinding> stateBindings,
        bool requiresDistinctExecutor = true)
    {
        ArgumentNullException.ThrowIfNull(stateBindings);

        NormalizedPolicyEvidence normalized = Normalize(
            operatingCountryCode,
            policyId,
            retentionPolicyId,
            contentSha256,
            purposeCode,
            surface,
            sourceProvenance);
        string dataClass =
            retentionDataClass?.Trim().ToLowerInvariant() ?? string.Empty;
        string trigger =
            retentionTrigger?.Trim().ToLowerInvariant() ?? string.Empty;
        bool scopeValid = scopeKind switch
        {
            DataRightsCaseScopeKind.Property =>
                caseKind == DataRightsCaseKind.GuestRights &&
                propertyId is Guid value &&
                value != Guid.Empty &&
                propertyVersion > 0,
            DataRightsCaseScopeKind.Tenant =>
                caseKind is DataRightsCaseKind.StaffRights or
                    DataRightsCaseKind.TenantTermination &&
                propertyId is null &&
                propertyVersion == 0,
            _ => false
        };
        if (!scopeValid ||
            !IsCommonValid(
                normalized,
                policyVersion,
                retentionPolicyVersion,
                evaluatedAtUtc) ||
            !DataRightsApprovalEvidenceBinding.IsKey(dataClass) ||
            !DataRightsApprovalEvidenceBinding.IsKey(trigger) ||
            retentionTriggeredAtUtc == default ||
            retentionTriggeredAtUtc.Offset != TimeSpan.Zero ||
            retentionDeadlineUtc <= retentionTriggeredAtUtc ||
            retentionDeadlineUtc.Offset != TimeSpan.Zero ||
            evaluatedAtUtc.Offset != TimeSpan.Zero ||
            retentionDeadlineUtc > evaluatedAtUtc ||
            !requiresDistinctExecutor ||
            !TryFreezeStateBindings(
                stateBindings,
                out string? bindingsJson,
                out string? bindingsSha256))
        {
            return Invalid();
        }

        return Result.Success(new DataRightsApprovalPolicyEvidence(
            CurrentSchemaVersion,
            caseKind,
            scopeKind,
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
            dataClass,
            trigger,
            retentionTriggeredAtUtc,
            retentionDeadlineUtc,
            evaluatedAtUtc,
            bindingsJson!,
            bindingsSha256!,
            requiresDistinctExecutor));
    }

    private static bool TryFreezeStateBindings(
        IReadOnlyCollection<DataRightsApprovalEvidenceBinding> bindings,
        out string? canonicalJson,
        out string? sha256)
    {
        canonicalJson = null;
        sha256 = null;
        if (bindings.Count is <= 0 or > MaximumStateBindings ||
            bindings.Any(binding =>
                binding is null ||
                !DataRightsApprovalEvidenceBinding.IsKey(binding.Key) ||
                binding.Version < 0 ||
                !DataRightsApprovalEvidenceBinding.IsSha256(
                    binding.Sha256)) ||
            bindings.Select(binding => binding.Key)
                .Distinct(StringComparer.Ordinal)
                .Count() != bindings.Count)
        {
            return false;
        }

        BindingPayload[] payload = bindings
            .OrderBy(binding => binding.Key, StringComparer.Ordinal)
            .Select(binding => new BindingPayload(
                binding.Key,
                binding.Version,
                binding.Sha256))
            .ToArray();
        canonicalJson = JsonSerializer.Serialize(
            payload,
            BindingSerializerOptions);
        if (canonicalJson.Length > StateBindingsJsonMaxLength)
        {
            canonicalJson = null;
            return false;
        }

        sha256 = ComputeSha256(canonicalJson);
        return true;
    }

    private static bool TryReadStateBindings(
        string json,
        out DataRightsApprovalEvidenceBinding[] bindings)
    {
        bindings = [];
        if (string.IsNullOrWhiteSpace(json) ||
            json.Length > StateBindingsJsonMaxLength)
        {
            return false;
        }

        try
        {
            BindingPayload[]? payload =
                JsonSerializer.Deserialize<BindingPayload[]>(
                    json,
                    BindingSerializerOptions);
            if (payload is null)
            {
                return false;
            }

            List<DataRightsApprovalEvidenceBinding> parsed =
                new(payload.Length);
            foreach (BindingPayload item in payload)
            {
                if (item is null)
                {
                    return false;
                }

                Result<DataRightsApprovalEvidenceBinding> binding =
                    DataRightsApprovalEvidenceBinding.Create(
                        item.Key,
                        item.Version,
                        item.Sha256);
                if (binding.IsFailure)
                {
                    return false;
                }

                parsed.Add(binding.Value);
            }

            bindings = parsed.ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool HasValidStateBindings(
        string json,
        string sha256) =>
        TryReadStateBindings(
            json,
            out DataRightsApprovalEvidenceBinding[] bindings) &&
        TryFreezeStateBindings(
            bindings,
            out string? canonicalJson,
            out string? canonicalSha256) &&
        string.Equals(canonicalJson, json, StringComparison.Ordinal) &&
        string.Equals(canonicalSha256, sha256, StringComparison.Ordinal);
}
