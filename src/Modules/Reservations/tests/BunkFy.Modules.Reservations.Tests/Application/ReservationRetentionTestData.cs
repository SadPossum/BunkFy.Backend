namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Xunit;

internal static class ReservationRetentionTestData
{
    public static readonly DateTimeOffset Now =
        new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    public static ReservationRetentionCandidateSnapshot Snapshot(
        PropertyGovernancePolicyBinding binding,
        ReservationState status =
            ReservationState.AllocationRejected,
        DateTimeOffset? terminalAtUtc = null,
        long projectionOrdinal = 8,
        long reservationVersion = 4,
        long detailsRevision = 2,
        Guid? reservationId = null,
        Guid? propertyId = null)
    {
        Guid resolvedPropertyId = propertyId ?? Guid.NewGuid();
        return new(
            reservationId ?? Guid.NewGuid(),
            resolvedPropertyId,
            reservationVersion,
            detailsRevision,
            projectionOrdinal,
            status,
            HasPendingAllocationAmendment: false,
            IsAnonymised: false,
            terminalAtUtc ?? Now.AddDays(-400),
            ActiveHoldCount: 0,
            EarliestHoldPlacedAtUtc: null,
            ReservationProcessingRestrictionContract.CurrentVersion,
            new(
                IsKnown: true,
                IsActive: true,
                PropertyProcessingStatus.Enabled,
                TopologySourceVersion: 3,
                PolicySourceVersion: 4,
                binding));
    }

    public static ReservationRetentionPolicyFixture CreatePolicy()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "gb-hostel",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = Now.AddYears(-2),
            ExpiresAtUtc = Now.AddYears(2),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["ordinary-guest"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "guest.primary-name",
                    GuestCategory = "ordinary-guest",
                    Requirement =
                        CountryPolicyFieldRequirement.Required,
                    PurposeCodes = ["reservation-retention"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "reservation-retention",
                    LegalRuleReferenceKeys = ["storage-limitation"],
                    AllowedSurfaces = [CountryPolicySurface.Retention],
                    AllowedSourceProvenance = ["retention-worker"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "operational-records",
                    RetentionPolicyVersion = 1,
                    DataClass = "reservation-operational",
                    Trigger = "reservation-ended",
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
                Minors = "not-assessed",
                Documents = "document-images-prohibited",
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
                        Uri = "https://example.test/policy"
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
        PropertyGovernancePolicyBinding binding = new(
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
            Now.AddDays(-1),
            []);
        return new(registry, binding);
    }
}

internal sealed record ReservationRetentionPolicyFixture(
    CountryPolicyRegistry Registry,
    PropertyGovernancePolicyBinding Binding);
