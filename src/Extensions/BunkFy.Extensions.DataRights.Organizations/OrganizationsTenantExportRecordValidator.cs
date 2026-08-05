namespace BunkFy.Extensions.DataRights.Organizations;

using Gma.Modules.Organizations.Application.Ports;

internal static class OrganizationsTenantExportRecordValidator
{
    private const int MaximumTextLength = 4_096;
    private const int MaximumEmailLength = 320;

    public static bool IsValid(
        Guid organizationId,
        OrganizationScopeExportStore store,
        OrganizationScopeExportRecord? record) =>
        organizationId != Guid.Empty &&
        (store, record) switch
        {
            (OrganizationScopeExportStore.Organization,
                OrganizationScopeOrganizationExportRecord value) =>
                value.OrganizationId == organizationId &&
                IsText(value.Name) &&
                IsText(value.Slug) &&
                IsDefined(value.Status) &&
                value.ActiveOwnerCount >= 0 &&
                IsCommon(
                    value.Version,
                    value.CreatedBy,
                    value.CreatedAtUtc,
                    value.LastChangedBy,
                    value.LastChangedAtUtc),
            (OrganizationScopeExportStore.Memberships,
                OrganizationScopeMembershipExportRecord value) =>
                value.MembershipId != Guid.Empty &&
                value.OrganizationId == organizationId &&
                IsText(value.SubjectId) &&
                IsDefined(value.Role) &&
                IsDefined(value.Status) &&
                IsCommon(
                    value.Version,
                    value.CreatedBy,
                    value.JoinedAtUtc,
                    value.LastChangedBy,
                    value.LastChangedAtUtc),
            (OrganizationScopeExportStore.Invitations,
                OrganizationScopeInvitationExportRecord value) =>
                value.InvitationId != Guid.Empty &&
                value.OrganizationId == organizationId &&
                IsText(value.InviterSubjectId) &&
                IsOptionalText(value.RecipientEmail, MaximumEmailLength) &&
                value.TokenVersion > 0 &&
                value.ExpiresAtUtc != default &&
                IsDefined(value.Status) &&
                HasConsistentAcceptance(value) &&
                IsCommon(
                    value.Version,
                    value.CreatedBy,
                    value.CreatedAtUtc,
                    value.LastChangedBy,
                    value.LastChangedAtUtc),
            (OrganizationScopeExportStore.EnrollmentLinks,
                OrganizationScopeEnrollmentLinkExportRecord value) =>
                value.EnrollmentLinkId != Guid.Empty &&
                value.OrganizationId == organizationId &&
                IsText(value.CreatorSubjectId) &&
                value.TokenVersion > 0 &&
                value.ExpiresAtUtc != default &&
                value.MaximumClaims > 0 &&
                value.ReservedClaims >= 0 &&
                value.ReservedClaims <= value.MaximumClaims &&
                IsDefined(value.ApprovalMode) &&
                IsDefined(value.Status) &&
                IsCommon(
                    value.Version,
                    value.CreatedBy,
                    value.CreatedAtUtc,
                    value.LastChangedBy,
                    value.LastChangedAtUtc),
            (OrganizationScopeExportStore.EnrollmentClaims,
                OrganizationScopeEnrollmentClaimExportRecord value) =>
                value.EnrollmentClaimId != Guid.Empty &&
                value.OrganizationId == organizationId &&
                value.EnrollmentLinkId != Guid.Empty &&
                IsText(value.SubjectId) &&
                IsDefined(value.Status) &&
                (!value.MembershipId.HasValue ||
                 value.MembershipId.Value != Guid.Empty) &&
                (!value.DecisionExpiresAtUtc.HasValue ||
                 value.DecisionExpiresAtUtc.Value != default) &&
                value.Version > 0 &&
                value.CreatedAtUtc != default &&
                IsText(value.LastChangedBy) &&
                value.LastChangedAtUtc >= value.CreatedAtUtc,
            _ => false
        };

    private static bool HasConsistentAcceptance(
        OrganizationScopeInvitationExportRecord value)
    {
        bool hasSubject = value.AcceptedSubjectId is not null;
        bool hasMembership = value.AcceptedMembershipId.HasValue;
        bool hasTime = value.AcceptedAtUtc.HasValue;
        return hasSubject == hasMembership &&
            hasMembership == hasTime &&
            (!hasSubject ||
             (IsText(value.AcceptedSubjectId) &&
              value.AcceptedMembershipId!.Value != Guid.Empty &&
              value.AcceptedAtUtc!.Value != default));
    }

    private static bool IsCommon(
        long version,
        string createdBy,
        DateTimeOffset createdAtUtc,
        string lastChangedBy,
        DateTimeOffset lastChangedAtUtc) =>
        version > 0 &&
        IsText(createdBy) &&
        createdAtUtc != default &&
        IsText(lastChangedBy) &&
        lastChangedAtUtc >= createdAtUtc;

    private static bool IsOptionalText(string? value, int maximumLength) =>
        value is null || IsText(value, maximumLength);

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
