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
}
