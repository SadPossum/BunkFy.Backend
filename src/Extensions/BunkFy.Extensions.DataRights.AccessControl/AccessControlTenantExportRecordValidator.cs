namespace BunkFy.Extensions.DataRights.AccessControl;

using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

internal static class AccessControlTenantExportRecordValidator
{
    private const int MaximumTextLength = 4_096;
    private const int MaximumIdentifierLength = 512;
    private const int MaximumPermissionCount = 1_000;

    public static bool IsValid(
        AccessScope rootScope,
        AccessControlScopeExportStore store,
        AccessControlScopeExportRecord? record) =>
        rootScope is not null &&
        !rootScope.IsGlobal &&
        (store, record) switch
        {
            (AccessControlScopeExportStore.RoleAssignments,
                AccessControlRoleAssignmentExportRecord value) =>
                value.AssignmentId != Guid.Empty &&
                IsDefined(value.SubjectKind) &&
                IsText(value.SubjectId, MaximumIdentifierLength) &&
                IsText(value.RoleName, MaximumIdentifierLength) &&
                IsPermissions(value.RolePermissions) &&
                IsOwnedScope(rootScope, value.AccessScopeValue) &&
                value.CreatedAtUtc != default &&
                (!value.ExpiresAtUtc.HasValue ||
                 value.ExpiresAtUtc.Value > value.CreatedAtUtc) &&
                (!value.RevokedAtUtc.HasValue ||
                 value.RevokedAtUtc.Value >= value.CreatedAtUtc),
            (AccessControlScopeExportStore.Profiles,
                AccessControlProfileExportRecord value) =>
                value.ProfileId != Guid.Empty &&
                IsOwnedScope(rootScope, value.OwnerScopeValue) &&
                IsText(value.Key, MaximumIdentifierLength) &&
                IsText(value.DisplayName) &&
                IsBoundedText(value.Description) &&
                IsDefined(value.Status) &&
                value.Version > 0 &&
                IsDefined(value.CreatedByKind) &&
                IsText(value.CreatedById, MaximumIdentifierLength) &&
                value.CreatedAtUtc != default &&
                IsDefined(value.LastChangedByKind) &&
                IsText(value.LastChangedById, MaximumIdentifierLength) &&
                value.LastChangedAtUtc >= value.CreatedAtUtc &&
                IsPermissions(value.Permissions),
            (AccessControlScopeExportStore.ProfileAssignments,
                AccessControlProfileAssignmentExportRecord value) =>
                value.AssignmentId != Guid.Empty &&
                value.ProfileId != Guid.Empty &&
                IsOwnedScope(rootScope, value.ProfileOwnerScopeValue) &&
                IsText(value.ProfileKey, MaximumIdentifierLength) &&
                IsOwnedScope(rootScope, value.AssignmentScopeValue) &&
                IsDefined(value.SubjectKind) &&
                IsText(value.SubjectId, MaximumIdentifierLength) &&
                IsDefined(value.CreatedByKind) &&
                IsText(value.CreatedById, MaximumIdentifierLength) &&
                value.CreatedAtUtc != default,
            (AccessControlScopeExportStore.ProfileChanges,
                AccessControlProfileChangeExportRecord value) =>
                value.ChangeId != Guid.Empty &&
                value.ProfileId != Guid.Empty &&
                IsOwnedScope(rootScope, value.ProfileOwnerScopeValue) &&
                IsText(value.ProfileKey, MaximumIdentifierLength) &&
                IsDefined(value.Kind) &&
                IsDefined(value.ActorKind) &&
                IsText(value.ActorId, MaximumIdentifierLength) &&
                HasConsistentSubject(value.SubjectKind, value.SubjectId) &&
                (value.AssignmentScopeValue is null ||
                 IsOwnedScope(rootScope, value.AssignmentScopeValue)) &&
                value.ProfileVersion > 0 &&
                value.OccurredAtUtc != default,
            _ => false
        };

    private static bool HasConsistentSubject(
        AccessSubjectKind? subjectKind,
        string? subjectId) =>
        subjectKind.HasValue == (subjectId is not null) &&
        (!subjectKind.HasValue ||
         (IsDefined(subjectKind.Value) &&
          IsText(subjectId, MaximumIdentifierLength)));

    private static bool IsOwnedScope(
        AccessScope rootScope,
        string? candidate) =>
        AccessScope.TryParse(candidate, out AccessScope? parsed) &&
        (string.Equals(
             parsed.Value,
             rootScope.Value,
             StringComparison.Ordinal) ||
         parsed.Value.StartsWith(
             rootScope.Value + "/",
             StringComparison.Ordinal));

    private static bool IsPermissions(IReadOnlyList<string>? values) =>
        values is not null &&
        values.Count <= MaximumPermissionCount &&
        values.All(value => IsText(value, MaximumIdentifierLength)) &&
        values.SequenceEqual(
            values.Order(StringComparer.Ordinal),
            StringComparer.Ordinal) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count;

    private static bool IsBoundedText(string? value)
    {
        if (value is null || value.Length > MaximumTextLength)
        {
            return false;
        }

        string normalized = value.Trim();
        return string.Equals(value, normalized, StringComparison.Ordinal) &&
            !normalized.Any(char.IsControl);
    }

    private static bool IsText(
        string? value,
        int maximumLength = MaximumTextLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 &&
            normalized.Length <= maximumLength &&
            string.Equals(value, normalized, StringComparison.Ordinal) &&
            !normalized.Any(char.IsControl);
    }

    private static bool IsDefined<T>(T value)
        where T : struct, Enum =>
        Enum.IsDefined(value) &&
        !EqualityComparer<T>.Default.Equals(value, default);
}
