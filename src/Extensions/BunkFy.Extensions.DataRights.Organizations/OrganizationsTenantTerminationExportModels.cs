namespace BunkFy.Extensions.DataRights.Organizations;

using Gma.Modules.Organizations.Contracts;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class OrganizationsTenantExportFieldAttribute(string fieldId)
    : Attribute
{
    public string FieldId { get; } = fieldId;
}

internal sealed record OrganizationTenantExport(
    [property: OrganizationsTenantExportField("organizations.organization-id")]
    Guid OrganizationId,
    [property: OrganizationsTenantExportField("organizations.organization-name")]
    string Name,
    [property: OrganizationsTenantExportField("organizations.organization-slug")]
    string Slug,
    [property: OrganizationsTenantExportField("organizations.organization-status")]
    OrganizationStatus Status,
    [property: OrganizationsTenantExportField("organizations.active-owner-count")]
    int ActiveOwnerCount,
    [property: OrganizationsTenantExportField("organizations.record-version")]
    long Version,
    [property: OrganizationsTenantExportField("organizations.created-by")]
    string CreatedBy,
    [property: OrganizationsTenantExportField("organizations.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: OrganizationsTenantExportField("organizations.last-changed-by")]
    string LastChangedBy,
    [property: OrganizationsTenantExportField("organizations.last-changed-at")]
    DateTimeOffset LastChangedAtUtc);

internal sealed record OrganizationMembershipTenantExport(
    [property: OrganizationsTenantExportField("organizations.membership-id")]
    Guid MembershipId,
    [property: OrganizationsTenantExportField("organizations.organization-id")]
    Guid OrganizationId,
    [property: OrganizationsTenantExportField("organizations.subject-id")]
    string SubjectId,
    [property: OrganizationsTenantExportField("organizations.membership-role")]
    OrganizationMembershipRole Role,
    [property: OrganizationsTenantExportField("organizations.membership-status")]
    OrganizationMembershipStatus Status,
    [property: OrganizationsTenantExportField("organizations.record-version")]
    long Version,
    [property: OrganizationsTenantExportField("organizations.created-by")]
    string CreatedBy,
    [property: OrganizationsTenantExportField("organizations.joined-at")]
    DateTimeOffset JoinedAtUtc,
    [property: OrganizationsTenantExportField("organizations.last-changed-by")]
    string LastChangedBy,
    [property: OrganizationsTenantExportField("organizations.last-changed-at")]
    DateTimeOffset LastChangedAtUtc);

internal sealed record OrganizationInvitationTenantExport(
    [property: OrganizationsTenantExportField("organizations.invitation-id")]
    Guid InvitationId,
    [property: OrganizationsTenantExportField("organizations.organization-id")]
    Guid OrganizationId,
    [property: OrganizationsTenantExportField("organizations.inviter-subject-id")]
    string InviterSubjectId,
    [property: OrganizationsTenantExportField("organizations.recipient-email")]
    string? RecipientEmail,
    [property: OrganizationsTenantExportField("organizations.invitation-token-version")]
    int TokenVersion,
    [property: OrganizationsTenantExportField("organizations.invitation-expires-at")]
    DateTimeOffset ExpiresAtUtc,
    [property: OrganizationsTenantExportField("organizations.invitation-status")]
    OrganizationInvitationStatus Status,
    [property: OrganizationsTenantExportField("organizations.accepted-subject-id")]
    string? AcceptedSubjectId,
    [property: OrganizationsTenantExportField("organizations.accepted-membership-id")]
    Guid? AcceptedMembershipId,
    [property: OrganizationsTenantExportField("organizations.accepted-at")]
    DateTimeOffset? AcceptedAtUtc,
    [property: OrganizationsTenantExportField("organizations.record-version")]
    long Version,
    [property: OrganizationsTenantExportField("organizations.created-by")]
    string CreatedBy,
    [property: OrganizationsTenantExportField("organizations.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: OrganizationsTenantExportField("organizations.last-changed-by")]
    string LastChangedBy,
    [property: OrganizationsTenantExportField("organizations.last-changed-at")]
    DateTimeOffset LastChangedAtUtc);

internal sealed record OrganizationEnrollmentLinkTenantExport(
    [property: OrganizationsTenantExportField("organizations.enrollment-link-id")]
    Guid EnrollmentLinkId,
    [property: OrganizationsTenantExportField("organizations.organization-id")]
    Guid OrganizationId,
    [property: OrganizationsTenantExportField("organizations.creator-subject-id")]
    string CreatorSubjectId,
    [property: OrganizationsTenantExportField("organizations.enrollment-link-token-version")]
    int TokenVersion,
    [property: OrganizationsTenantExportField("organizations.enrollment-link-expires-at")]
    DateTimeOffset ExpiresAtUtc,
    [property: OrganizationsTenantExportField("organizations.maximum-claims")]
    int MaximumClaims,
    [property: OrganizationsTenantExportField("organizations.reserved-claims")]
    int ReservedClaims,
    [property: OrganizationsTenantExportField("organizations.approval-mode")]
    OrganizationEnrollmentApprovalMode ApprovalMode,
    [property: OrganizationsTenantExportField("organizations.enrollment-link-status")]
    OrganizationEnrollmentLinkStatus Status,
    [property: OrganizationsTenantExportField("organizations.record-version")]
    long Version,
    [property: OrganizationsTenantExportField("organizations.created-by")]
    string CreatedBy,
    [property: OrganizationsTenantExportField("organizations.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: OrganizationsTenantExportField("organizations.last-changed-by")]
    string LastChangedBy,
    [property: OrganizationsTenantExportField("organizations.last-changed-at")]
    DateTimeOffset LastChangedAtUtc);

internal sealed record OrganizationEnrollmentClaimTenantExport(
    [property: OrganizationsTenantExportField("organizations.enrollment-claim-id")]
    Guid EnrollmentClaimId,
    [property: OrganizationsTenantExportField("organizations.organization-id")]
    Guid OrganizationId,
    [property: OrganizationsTenantExportField("organizations.enrollment-link-id")]
    Guid EnrollmentLinkId,
    [property: OrganizationsTenantExportField("organizations.subject-id")]
    string SubjectId,
    [property: OrganizationsTenantExportField("organizations.enrollment-claim-status")]
    OrganizationEnrollmentClaimStatus Status,
    [property: OrganizationsTenantExportField("organizations.membership-id")]
    Guid? MembershipId,
    [property: OrganizationsTenantExportField("organizations.decision-expires-at")]
    DateTimeOffset? DecisionExpiresAtUtc,
    [property: OrganizationsTenantExportField("organizations.record-version")]
    long Version,
    [property: OrganizationsTenantExportField("organizations.created-at")]
    DateTimeOffset CreatedAtUtc,
    [property: OrganizationsTenantExportField("organizations.last-changed-by")]
    string LastChangedBy,
    [property: OrganizationsTenantExportField("organizations.last-changed-at")]
    DateTimeOffset LastChangedAtUtc);
