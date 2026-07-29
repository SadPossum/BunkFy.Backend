namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using ContractEvidenceBinding =
    BunkFy.Modules.DataRights.Contracts.DataRightsApprovalEvidenceBinding;

internal static class DataRightsApprovalEvidenceComparer
{
    public static bool Matches(
        DataRightsApprovalPolicyEvidence frozen,
        DataRightsApprovalEvidence current) =>
        frozen.HasValidShape() &&
        HasValidContractShape(current) &&
        frozen.SchemaVersion == current.SchemaVersion &&
        frozen.PropertyId == current.PropertyId &&
        frozen.PropertyVersion == current.PropertyVersion &&
        string.Equals(
            frozen.OperatingCountryCode,
            current.OperatingCountryCode,
            StringComparison.Ordinal) &&
        string.Equals(frozen.PolicyId, current.PolicyId, StringComparison.Ordinal) &&
        frozen.PolicyVersion == current.PolicyVersion &&
        string.Equals(
            frozen.RetentionPolicyId,
            current.RetentionPolicyId,
            StringComparison.Ordinal) &&
        frozen.RetentionPolicyVersion == current.RetentionPolicyVersion &&
        string.Equals(
            frozen.ContentSha256,
            current.ContentSha256,
            StringComparison.Ordinal) &&
        string.Equals(frozen.PurposeCode, current.PurposeCode, StringComparison.Ordinal) &&
        string.Equals(frozen.Surface, current.Surface, StringComparison.Ordinal) &&
        string.Equals(
            frozen.SourceProvenance,
            current.SourceProvenance,
            StringComparison.Ordinal) &&
        frozen.EvaluatedAtUtc == current.EvaluatedAtUtc &&
        frozen.RequiresDistinctExecutor == current.RequiresDistinctExecutor &&
        (frozen.SchemaVersion ==
            DataRightsApprovalPolicyEvidence.MinimumSupportedSchemaVersion ||
         MatchesScopedEvidence(frozen, current));

    private static bool HasValidContractShape(
        DataRightsApprovalEvidence evidence)
    {
        if (evidence.SchemaVersion ==
            DataRightsApprovalPolicyEvidence.MinimumSupportedSchemaVersion)
        {
            return evidence.CaseType == DataRightsCaseType.GuestRights &&
                evidence.ScopeKind ==
                    DataRightsExecutionScopeKind.Property &&
                evidence.PropertyId is Guid propertyId &&
                propertyId != Guid.Empty &&
                evidence.RetentionDataClass is null &&
                evidence.RetentionTrigger is null &&
                evidence.RetentionTriggeredAtUtc is null &&
                evidence.RetentionDeadlineUtc is null &&
                (evidence.StateBindings is null ||
                 evidence.StateBindings.Count == 0) &&
                (evidence.StateBindingsSha256 is null ||
                 string.Equals(
                     evidence.StateBindingsSha256,
                     DataRightsApprovalEvidence.EmptyStateBindingsSha256,
                     StringComparison.Ordinal));
        }

        return evidence.SchemaVersion ==
                DataRightsApprovalPolicyEvidence.CurrentSchemaVersion &&
            evidence.StateBindings is not null;
    }

    private static bool MatchesScopedEvidence(
        DataRightsApprovalPolicyEvidence frozen,
        DataRightsApprovalEvidence current)
    {
        if ((DataRightsCaseType)frozen.CaseKind != current.CaseType ||
            (DataRightsExecutionScopeKind)frozen.ScopeKind !=
                current.ScopeKind ||
            !string.Equals(
                frozen.RetentionDataClass,
                current.RetentionDataClass,
                StringComparison.Ordinal) ||
            !string.Equals(
                frozen.RetentionTrigger,
                current.RetentionTrigger,
                StringComparison.Ordinal) ||
            frozen.RetentionTriggeredAtUtc !=
                current.RetentionTriggeredAtUtc ||
            frozen.RetentionDeadlineUtc != current.RetentionDeadlineUtc ||
            !string.Equals(
                frozen.StateBindingsSha256,
                current.StateBindingsSha256,
                StringComparison.Ordinal))
        {
            return false;
        }

        IReadOnlyCollection<ContractEvidenceBinding> currentBindings =
            current.StateBindings ?? [];
        return frozen.StateBindings.Count == currentBindings.Count &&
            frozen.StateBindings
                .OrderBy(binding => binding.Key, StringComparer.Ordinal)
                .Zip(
                    currentBindings.OrderBy(
                        binding => binding.Key,
                        StringComparer.Ordinal))
                .All(pair =>
                    string.Equals(
                        pair.First.Key,
                        pair.Second.Key,
                        StringComparison.Ordinal) &&
                    pair.First.Version == pair.Second.Version &&
                    string.Equals(
                        pair.First.Sha256,
                        pair.Second.Sha256,
                        StringComparison.Ordinal));
    }
}
