namespace BunkFy.Extensions.DataRights.AccessControl;

using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class AccessControlTenantExportFieldAttribute(string fieldId)
    : Attribute
{
    public string FieldId { get; } = fieldId;
}

internal sealed record AccessControlRoleAssignmentTenantExport(
    [property: AccessControlTenantExportField("access-control.assignment-id")]
    Guid AssignmentId,
    [property: AccessControlTenantExportField("access-control.subject-kind")]
    AccessSubjectKind SubjectKind,
    [property: AccessControlTenantExportField("access-control.subject-id")]
    string SubjectId,
    [property: AccessControlTenantExportField("access-control.role-name")]
    string RoleName,
    [property: AccessControlTenantExportField("access-control.role-permissions")]
    IReadOnlyList<string> RolePermissions,
    [property: AccessControlTenantExportField("access-control.access-scope")]
    string AccessScopeValue,
    [property: AccessControlTenantExportField("access-control.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: AccessControlTenantExportField("access-control.expires-at")]
    DateTimeOffset? ExpiresAtUtc,
    [property: AccessControlTenantExportField("access-control.revoked-at")]
    DateTimeOffset? RevokedAtUtc);

internal sealed record AccessControlProfileTenantExport(
    [property: AccessControlTenantExportField("access-control.profile-id")]
    Guid ProfileId,
    [property: AccessControlTenantExportField("access-control.profile-owner-scope")]
    string OwnerScopeValue,
    [property: AccessControlTenantExportField("access-control.profile-key")]
    string Key,
    [property: AccessControlTenantExportField("access-control.display-name")]
    string DisplayName,
    [property: AccessControlTenantExportField("access-control.description")]
    string Description,
    [property: AccessControlTenantExportField("access-control.status")]
    AccessProfileStatus Status,
    [property: AccessControlTenantExportField("access-control.record-version")]
    long Version,
    [property: AccessControlTenantExportField("access-control.created-by-kind")]
    AccessSubjectKind CreatedByKind,
    [property: AccessControlTenantExportField("access-control.created-by-id")]
    string CreatedById,
    [property: AccessControlTenantExportField("access-control.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: AccessControlTenantExportField("access-control.last-changed-by-kind")]
    AccessSubjectKind LastChangedByKind,
    [property: AccessControlTenantExportField("access-control.last-changed-by-id")]
    string LastChangedById,
    [property: AccessControlTenantExportField("access-control.last-changed-at")]
    DateTimeOffset LastChangedAtUtc,
    [property: AccessControlTenantExportField("access-control.permissions")]
    IReadOnlyList<string> Permissions);

internal sealed record AccessControlProfileAssignmentTenantExport(
    [property: AccessControlTenantExportField("access-control.assignment-id")]
    Guid AssignmentId,
    [property: AccessControlTenantExportField("access-control.profile-id")]
    Guid ProfileId,
    [property: AccessControlTenantExportField("access-control.profile-owner-scope")]
    string ProfileOwnerScopeValue,
    [property: AccessControlTenantExportField("access-control.profile-key")]
    string ProfileKey,
    [property: AccessControlTenantExportField("access-control.assignment-scope")]
    string AssignmentScopeValue,
    [property: AccessControlTenantExportField("access-control.subject-kind")]
    AccessSubjectKind SubjectKind,
    [property: AccessControlTenantExportField("access-control.subject-id")]
    string SubjectId,
    [property: AccessControlTenantExportField("access-control.created-by-kind")]
    AccessSubjectKind CreatedByKind,
    [property: AccessControlTenantExportField("access-control.created-by-id")]
    string CreatedById,
    [property: AccessControlTenantExportField("access-control.created-at")]
    DateTimeOffset CreatedAtUtc);

internal sealed record AccessControlProfileChangeTenantExport(
    [property: AccessControlTenantExportField("access-control.change-id")]
    Guid ChangeId,
    [property: AccessControlTenantExportField("access-control.profile-id")]
    Guid ProfileId,
    [property: AccessControlTenantExportField("access-control.profile-owner-scope")]
    string ProfileOwnerScopeValue,
    [property: AccessControlTenantExportField("access-control.profile-key")]
    string ProfileKey,
    [property: AccessControlTenantExportField("access-control.change-kind")]
    AccessProfileChangeKind Kind,
    [property: AccessControlTenantExportField("access-control.actor-kind")]
    AccessSubjectKind ActorKind,
    [property: AccessControlTenantExportField("access-control.actor-id")]
    string ActorId,
    [property: AccessControlTenantExportField("access-control.subject-kind")]
    AccessSubjectKind? SubjectKind,
    [property: AccessControlTenantExportField("access-control.subject-id")]
    string? SubjectId,
    [property: AccessControlTenantExportField("access-control.assignment-scope")]
    string? AssignmentScopeValue,
    [property: AccessControlTenantExportField("access-control.record-version")]
    long ProfileVersion,
    [property: AccessControlTenantExportField("access-control.occurred-at")]
    DateTimeOffset OccurredAtUtc);
