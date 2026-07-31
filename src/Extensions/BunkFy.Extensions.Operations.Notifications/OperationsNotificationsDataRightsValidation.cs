namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Scoping;

internal static class OperationsNotificationsDataRightsValidation
{
    public static bool IsGuestPropertyScope(
        IScopeContext scopeContext,
        string? tenantId,
        DataRightsCaseType caseType,
        Guid? propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        !string.IsNullOrWhiteSpace(tenantId) &&
        string.Equals(
            scopeContext.ScopeId,
            tenantId.Trim(),
            StringComparison.Ordinal) &&
        caseType == DataRightsCaseType.GuestRights &&
        propertyId is Guid value &&
        value != Guid.Empty;

    public static bool IsReservationHistoryCoordinate(
        DataRightsSubjectCoordinate? coordinate) =>
        coordinate is not null &&
        string.Equals(
            coordinate.OwnerKey,
            OperationsNotificationsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            coordinate.RecordType,
            OperationsNotificationsDataRightsCoordinates
                .ReservationHistoryRecordType,
            StringComparison.Ordinal) &&
        coordinate.RecordId != Guid.Empty &&
        coordinate.RecordVersion > 0;

    public static bool IsStaffTenantScope(
        IScopeContext scopeContext,
        string? tenantId,
        DataRightsCaseType caseType,
        Guid? propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        !string.IsNullOrWhiteSpace(tenantId) &&
        string.Equals(
            scopeContext.ScopeId,
            tenantId.Trim(),
            StringComparison.Ordinal) &&
        caseType == DataRightsCaseType.StaffRights &&
        propertyId is null;

    public static bool IsStaffHistoryCoordinate(
        DataRightsSubjectCoordinate? coordinate) =>
        coordinate is not null &&
        string.Equals(
            coordinate.OwnerKey,
            OperationsNotificationsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            coordinate.RecordType,
            OperationsNotificationsDataRightsCoordinates
                .StaffInboxHistoryRecordType,
            StringComparison.Ordinal) &&
        coordinate.RecordId != Guid.Empty &&
        coordinate.RecordVersion > 0;

    public static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    public static bool IsApprovalEvidence(
        DataRightsApprovalEvidence? evidence,
        Guid propertyId) =>
        evidence is not null &&
        evidence.SchemaVersion == 1 &&
        evidence.PropertyId == propertyId &&
        evidence.PropertyVersion > 0 &&
        evidence.OperatingCountryCode?.Trim().Length == 2 &&
        !string.IsNullOrWhiteSpace(evidence.PolicyId) &&
        evidence.PolicyVersion > 0 &&
        !string.IsNullOrWhiteSpace(evidence.RetentionPolicyId) &&
        evidence.RetentionPolicyVersion > 0 &&
        IsSha256(evidence.ContentSha256) &&
        !string.IsNullOrWhiteSpace(evidence.PurposeCode) &&
        !string.IsNullOrWhiteSpace(evidence.Surface) &&
        !string.IsNullOrWhiteSpace(evidence.SourceProvenance) &&
        evidence.EvaluatedAtUtc != default &&
        evidence.RequiresDistinctExecutor &&
            evidence.CaseType == DataRightsCaseType.GuestRights &&
            evidence.ScopeKind == DataRightsExecutionScopeKind.Property;

    public static bool IsStaffApprovalEvidence(
        DataRightsApprovalEvidence? evidence) =>
        evidence is not null &&
        evidence.SchemaVersion == 2 &&
        evidence.PropertyId is null &&
        evidence.PropertyVersion == 0 &&
        evidence.OperatingCountryCode?.Trim().Length == 2 &&
        !string.IsNullOrWhiteSpace(evidence.PolicyId) &&
        evidence.PolicyVersion > 0 &&
        !string.IsNullOrWhiteSpace(evidence.RetentionPolicyId) &&
        evidence.RetentionPolicyVersion > 0 &&
        IsSha256(evidence.ContentSha256) &&
        !string.IsNullOrWhiteSpace(evidence.PurposeCode) &&
        !string.IsNullOrWhiteSpace(evidence.Surface) &&
        !string.IsNullOrWhiteSpace(evidence.SourceProvenance) &&
        !string.IsNullOrWhiteSpace(evidence.RetentionDataClass) &&
        !string.IsNullOrWhiteSpace(evidence.RetentionTrigger) &&
        evidence.RetentionTriggeredAtUtc.HasValue &&
        evidence.RetentionDeadlineUtc.HasValue &&
        evidence.RetentionDeadlineUtc <= evidence.EvaluatedAtUtc &&
        evidence.EvaluatedAtUtc != default &&
        evidence.RequiresDistinctExecutor &&
        evidence.CaseType == DataRightsCaseType.StaffRights &&
        evidence.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        HasValidStateBindings(evidence.StateBindings, minimumCount: 5) &&
        IsSha256(evidence.StateBindingsSha256) &&
        OperationsNotificationsStaffHistoryPolicyEvidence.TryGetBinding(
            evidence,
            out _);

    private static bool HasValidStateBindings(
        IReadOnlyCollection<DataRightsApprovalEvidenceBinding>? bindings,
        int minimumCount)
    {
        if (bindings is null ||
            bindings.Count < minimumCount ||
            bindings.Count >
                DataRightsAnonymisationPolicyContract
                    .MaximumStateBindings)
        {
            return false;
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        return bindings.All(binding =>
            binding is not null &&
            binding.Key.Length is > 0 and <=
                DataRightsAnonymisationPolicyContract.KeyMaxLength &&
            binding.Key[0] is >= 'a' and <= 'z' &&
            binding.Key.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_') &&
            binding.Version >= 0 &&
            IsSha256(binding.Sha256) &&
            keys.Add(binding.Key));
    }
}
