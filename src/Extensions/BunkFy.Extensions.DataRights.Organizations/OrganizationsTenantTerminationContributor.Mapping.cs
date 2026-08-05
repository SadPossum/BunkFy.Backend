namespace BunkFy.Extensions.DataRights.Organizations;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Modules.Organizations.Application.Ports;

internal sealed partial class OrganizationsTenantTerminationContributor
{
    private static DataRightsExportRecord Map(
        Guid organizationId,
        OrganizationScopeExportStore store,
        OrganizationScopeExportRecord record)
    {
        if (!OrganizationsTenantExportRecordValidator.IsValid(
                organizationId,
                store,
                record))
        {
            throw new InvalidDataException(
                "The Organizations scope-export record is invalid.");
        }

        return (store, record) switch
        {
            (OrganizationScopeExportStore.Organization,
                OrganizationScopeOrganizationExportRecord value) =>
                Create(
                    OrganizationsTenantTerminationMetadata
                        .OrganizationRecordType,
                    value.OrganizationId,
                    value.Version,
                    new OrganizationTenantExport(
                        value.OrganizationId,
                        value.Name,
                        value.Slug,
                        value.Status,
                        value.ActiveOwnerCount,
                        value.Version,
                        value.CreatedBy,
                        value.CreatedAtUtc,
                        value.LastChangedBy,
                        value.LastChangedAtUtc)),
            (OrganizationScopeExportStore.Memberships,
                OrganizationScopeMembershipExportRecord value) =>
                Create(
                    OrganizationsTenantTerminationMetadata
                        .MembershipRecordType,
                    value.MembershipId,
                    value.Version,
                    new OrganizationMembershipTenantExport(
                        value.MembershipId,
                        value.OrganizationId,
                        value.SubjectId,
                        value.Role,
                        value.Status,
                        value.Version,
                        value.CreatedBy,
                        value.JoinedAtUtc,
                        value.LastChangedBy,
                        value.LastChangedAtUtc)),
            (OrganizationScopeExportStore.Invitations,
                OrganizationScopeInvitationExportRecord value) =>
                Create(
                    OrganizationsTenantTerminationMetadata
                        .InvitationRecordType,
                    value.InvitationId,
                    value.Version,
                    new OrganizationInvitationTenantExport(
                        value.InvitationId,
                        value.OrganizationId,
                        value.InviterSubjectId,
                        value.RecipientEmail,
                        value.TokenVersion,
                        value.ExpiresAtUtc,
                        value.Status,
                        value.AcceptedSubjectId,
                        value.AcceptedMembershipId,
                        value.AcceptedAtUtc,
                        value.Version,
                        value.CreatedBy,
                        value.CreatedAtUtc,
                        value.LastChangedBy,
                        value.LastChangedAtUtc)),
            (OrganizationScopeExportStore.EnrollmentLinks,
                OrganizationScopeEnrollmentLinkExportRecord value) =>
                Create(
                    OrganizationsTenantTerminationMetadata
                        .EnrollmentLinkRecordType,
                    value.EnrollmentLinkId,
                    value.Version,
                    new OrganizationEnrollmentLinkTenantExport(
                        value.EnrollmentLinkId,
                        value.OrganizationId,
                        value.CreatorSubjectId,
                        value.TokenVersion,
                        value.ExpiresAtUtc,
                        value.MaximumClaims,
                        value.ReservedClaims,
                        value.ApprovalMode,
                        value.Status,
                        value.Version,
                        value.CreatedBy,
                        value.CreatedAtUtc,
                        value.LastChangedBy,
                        value.LastChangedAtUtc)),
            (OrganizationScopeExportStore.EnrollmentClaims,
                OrganizationScopeEnrollmentClaimExportRecord value) =>
                Create(
                    OrganizationsTenantTerminationMetadata
                        .EnrollmentClaimRecordType,
                    value.EnrollmentClaimId,
                    value.Version,
                    new OrganizationEnrollmentClaimTenantExport(
                        value.EnrollmentClaimId,
                        value.OrganizationId,
                        value.EnrollmentLinkId,
                        value.SubjectId,
                        value.Status,
                        value.MembershipId,
                        value.DecisionExpiresAtUtc,
                        value.Version,
                        value.CreatedAtUtc,
                        value.LastChangedBy,
                        value.LastChangedAtUtc)),
            _ => throw new InvalidDataException(
                "The Organizations scope-export record type is invalid.")
        };
    }

    private static DataRightsExportRecord Create(
        string recordType,
        Guid recordId,
        long recordVersion,
        object source) =>
        OrganizationsTenantTerminationExportSchema.CreateRecord(
            recordType,
            recordId,
            recordVersion,
            source);
}
