namespace BunkFy.Extensions.DataRights.AccessControl;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;

internal sealed partial class AccessControlTenantTerminationContributor
{
    private static DataRightsExportRecord Map(
        AccessScope rootScope,
        AccessControlScopeExportStore store,
        AccessControlScopeExportRecord record)
    {
        if (!AccessControlTenantExportRecordValidator.IsValid(
                rootScope,
                store,
                record))
        {
            throw new InvalidDataException(
                "The Access Control scope-export record is invalid.");
        }

        return (store, record) switch
        {
            (AccessControlScopeExportStore.RoleAssignments,
                AccessControlRoleAssignmentExportRecord value) =>
                Create(
                    AccessControlTenantTerminationMetadata
                        .RoleAssignmentRecordType,
                    value.AssignmentId,
                    recordVersion: 1,
                    new AccessControlRoleAssignmentTenantExport(
                        value.AssignmentId,
                        value.SubjectKind,
                        value.SubjectId,
                        value.RoleName,
                        value.RolePermissions.ToArray(),
                        value.AccessScopeValue,
                        value.CreatedAtUtc,
                        value.ExpiresAtUtc,
                        value.RevokedAtUtc)),
            (AccessControlScopeExportStore.Profiles,
                AccessControlProfileExportRecord value) =>
                Create(
                    AccessControlTenantTerminationMetadata.ProfileRecordType,
                    value.ProfileId,
                    value.Version,
                    new AccessControlProfileTenantExport(
                        value.ProfileId,
                        value.OwnerScopeValue,
                        value.Key,
                        value.DisplayName,
                        value.Description,
                        value.Status,
                        value.Version,
                        value.CreatedByKind,
                        value.CreatedById,
                        value.CreatedAtUtc,
                        value.LastChangedByKind,
                        value.LastChangedById,
                        value.LastChangedAtUtc,
                        value.Permissions.ToArray())),
            (AccessControlScopeExportStore.ProfileAssignments,
                AccessControlProfileAssignmentExportRecord value) =>
                Create(
                    AccessControlTenantTerminationMetadata
                        .ProfileAssignmentRecordType,
                    value.AssignmentId,
                    recordVersion: 1,
                    new AccessControlProfileAssignmentTenantExport(
                        value.AssignmentId,
                        value.ProfileId,
                        value.ProfileOwnerScopeValue,
                        value.ProfileKey,
                        value.AssignmentScopeValue,
                        value.SubjectKind,
                        value.SubjectId,
                        value.CreatedByKind,
                        value.CreatedById,
                        value.CreatedAtUtc)),
            (AccessControlScopeExportStore.ProfileChanges,
                AccessControlProfileChangeExportRecord value) =>
                Create(
                    AccessControlTenantTerminationMetadata
                        .ProfileChangeRecordType,
                    value.ChangeId,
                    value.ProfileVersion,
                    new AccessControlProfileChangeTenantExport(
                        value.ChangeId,
                        value.ProfileId,
                        value.ProfileOwnerScopeValue,
                        value.ProfileKey,
                        value.Kind,
                        value.ActorKind,
                        value.ActorId,
                        value.SubjectKind,
                        value.SubjectId,
                        value.AssignmentScopeValue,
                        value.ProfileVersion,
                        value.OccurredAtUtc)),
            _ => throw new InvalidDataException(
                "The Access Control scope-export record type is invalid.")
        };
    }

    private static DataRightsExportRecord Create(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source) =>
        AccessControlTenantTerminationExportSchema.CreateRecord(
            recordType,
            recordId,
            recordVersion,
            source);
}
