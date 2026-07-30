namespace BunkFy.Modules.Staff.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Xunit;

internal static class StaffRetentionTestData
{
    public static readonly DateTimeOffset Now =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    public static StaffRetentionCandidateSnapshot Snapshot(
        StaffRetentionGovernanceSnapshot governance,
        StaffMemberState status = StaffMemberState.Departed,
        DateTimeOffset? departedAtUtc = null,
        long projectionOrdinal = 8,
        long staffVersion = 4,
        Guid? staffMemberId = null) =>
        new(
            staffMemberId ?? Guid.NewGuid(),
            staffVersion,
            projectionOrdinal,
            status,
            departedAtUtc ?? Now.AddDays(-400),
            DateOnly.FromDateTime(
                (departedAtUtc ?? Now.AddDays(-400)).UtcDateTime),
            HasCurrentAssignments: false,
            ActiveHoldCount: 0,
            EarliestHoldPlacedAtUtc: null,
            StaffProcessingRestrictionContract.CurrentVersion,
            ProcessingRestrictionRevision: 2,
            OperationLockRevision: 9,
            governance with
            {
                SelectedStaffVersion = staffVersion
            });

    public static StaffRetentionPolicyFixture CreatePolicy()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "gb-staff",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = Now.AddYears(-2),
            ExpiresAtUtc = Now.AddYears(2),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["staff"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "staff.profile",
                    GuestCategory = "staff",
                    Requirement =
                        CountryPolicyFieldRequirement.Required,
                    PurposeCodes = ["staff-profile-retention"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "staff-profile-retention",
                    LegalRuleReferenceKeys = ["storage-limitation"],
                    AllowedSurfaces =
                        [CountryPolicySurface.Retention],
                    AllowedSourceProvenance = ["retention-worker"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "staff-employment",
                    RetentionPolicyVersion = 1,
                    DataClass = "staff-employment",
                    Trigger = "employment-ended",
                    Period = "365.00:00:00"
                }
            ],
            RightsRule = new()
            {
                Registration = "standard-registration",
                Export = "standard-export",
                Correction = "standard-correction",
                Restriction = "standard-restriction",
                Erasure = "review-before-erasure"
            },
            Restrictions = new()
            {
                Minors = "not-applicable",
                Documents = "prohibited",
                SpecialCategoryData = "prohibited"
            },
            PermittedDataRegions = ["eu-west-2"],
            PermittedTransferProfiles = ["uk-no-transfer"],
            RequiredAcknowledgements = [],
            Approval = new()
            {
                OwnerReference = "private-owner",
                ReviewerReference = "private-reviewer",
                ReviewedAtUtc = Now.AddYears(-2),
                Sources =
                [
                    new()
                    {
                        ReferenceId = "source-1",
                        Uri = "https://example.test/staff-policy"
                    }
                ],
                DetachedSignatureReference = "signature-1"
            }
        };
        CountryPolicyPackValidator.ValidateAndThrow(document);
        string digest = new('a', 64);
        CountryPolicyRegistry registry = CountryPolicyRegistry.Create(
            [new(document, digest)],
            [
                new(
                    document.OperatingCountryCode,
                    document.PolicyId,
                    document.PolicyVersion,
                    digest,
                    CountryLaunchStatus.Approved)
            ],
            CountryPolicyRuntimeMode.Production);
        CountryPolicyRetentionRule rule =
            Assert.Single(document.RetentionRules);
        StaffRetentionGovernanceSnapshot governance = new(
            GovernanceVersion: 2,
            SelectedStaffVersion: 4,
            document.OperatingCountryCode,
            document.PolicyId,
            document.PolicyVersion,
            document.PermittedDataRegions.Single(),
            document.PermittedTransferProfiles.Single(),
            rule.RetentionPolicyId,
            rule.RetentionPolicyVersion,
            digest,
            document.EffectiveAtUtc,
            document.ExpiresAtUtc,
            EvaluatedAtUtc: Now.AddDays(-2),
            ConfiguredAtUtc: Now.AddDays(-1),
            Acknowledgements: []);
        return new(registry, governance);
    }
}

internal sealed record StaffRetentionPolicyFixture(
    CountryPolicyRegistry Registry,
    StaffRetentionGovernanceSnapshot Governance);
