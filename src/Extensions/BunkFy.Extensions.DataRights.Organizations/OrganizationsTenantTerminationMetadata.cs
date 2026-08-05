namespace BunkFy.Extensions.DataRights.Organizations;

using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Organizations.Contracts;

public static class OrganizationsTenantTerminationMetadata
{
    public const string OwnerKey = OrganizationsModuleMetadata.Name;
    public const string OperationsNotificationsDependencyOwnerKey =
        "operations-notifications";
    public const string RetentionDependencyOwnerKey = "retention";
    public const int CatalogVersion = 1;
    public const int PersonalDataCatalogVersion = 1;
    public const string ExportCatalogId =
        "organizations.tenant-portability";
    public const int ExportCatalogSchemaVersion = 1;
    public const string ExportSchemaId =
        "organizations.tenant-termination-export";
    public const int ExportSchemaVersion = 1;

    public const string OrganizationRecordType = "organization";
    public const string MembershipRecordType = "organization-membership";
    public const string InvitationRecordType = "organization-invitation";
    public const string EnrollmentLinkRecordType =
        "organization-enrollment-link";
    public const string EnrollmentClaimRecordType =
        "organization-enrollment-claim";

    public static IReadOnlyList<string> DependencyOwnerKeys { get; } =
        Array.AsReadOnly(
        [
            OperationsNotificationsDependencyOwnerKey,
            RetentionDependencyOwnerKey
        ]);

    public static IReadOnlyList<string> RecordTypes { get; } =
        Array.AsReadOnly(
        [
            OrganizationRecordType,
            MembershipRecordType,
            InvitationRecordType,
            EnrollmentLinkRecordType,
            EnrollmentClaimRecordType
        ]);

    public static IReadOnlyList<string> ExportFieldIds { get; } =
        Array.AsReadOnly(
        [
            "organizations.accepted-at",
            "organizations.accepted-membership-id",
            "organizations.accepted-subject-id",
            "organizations.active-owner-count",
            "organizations.approval-mode",
            "organizations.created-at",
            "organizations.created-by",
            "organizations.creator-subject-id",
            "organizations.decision-expires-at",
            "organizations.enrollment-claim-id",
            "organizations.enrollment-claim-status",
            "organizations.enrollment-link-expires-at",
            "organizations.enrollment-link-id",
            "organizations.enrollment-link-status",
            "organizations.enrollment-link-token-version",
            "organizations.invitation-expires-at",
            "organizations.invitation-id",
            "organizations.invitation-status",
            "organizations.invitation-token-version",
            "organizations.inviter-subject-id",
            "organizations.joined-at",
            "organizations.last-changed-at",
            "organizations.last-changed-by",
            "organizations.maximum-claims",
            "organizations.membership-id",
            "organizations.membership-role",
            "organizations.membership-status",
            "organizations.organization-id",
            "organizations.organization-name",
            "organizations.organization-slug",
            "organizations.organization-status",
            "organizations.recipient-email",
            "organizations.record-version",
            "organizations.reserved-claims",
            "organizations.subject-id"
        ]);

    public static string CatalogManifest { get; } = string.Join(
        '|',
        OwnerKey,
        "contract=1",
        "phases=export,destroy",
        "dependencies=" + string.Join(',', DependencyOwnerKeys),
        "mandatory=true",
        $"catalog={CatalogVersion}",
        $"personal-data-catalog={PersonalDataCatalogVersion}",
        $"export-schema={ExportSchemaId}:{ExportSchemaVersion}",
        "scope-lifecycle=organizations:v1",
        "records=" + string.Join(',', RecordTypes),
        "fields=" + string.Join(',', ExportFieldIds));

    public static string CatalogSha256 { get; } =
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(CatalogManifest)));
}
