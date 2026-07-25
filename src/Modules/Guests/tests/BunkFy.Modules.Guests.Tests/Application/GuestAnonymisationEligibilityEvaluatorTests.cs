namespace BunkFy.Modules.Guests.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application.Policies;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestAnonymisationEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Completed_stays_and_exact_policy_evidence_are_eligible()
    {
        PolicyFixture policy = CreatePolicy();
        Guid guestId = Guid.NewGuid();
        Guid routingPropertyId = Guid.NewGuid();
        Guid historicalPropertyId = Guid.NewGuid();
        GuestAnonymisationEligibilitySnapshot snapshot = new(
            GuestVersion: 4,
            GuestProfileState.Active,
            routingPropertyId,
            [
                new(
                    historicalPropertyId,
                    Guid.NewGuid(),
                    GuestStayRole.Primary,
                    GuestStayStatus.CheckedOut,
                    IsCurrentParticipant: false,
                    ReservationVersion: 7,
                    GuestsModuleMetadata.StayHistoryProjectionVersion)
            ],
            ActiveHoldPropertyIds: [],
            [
                Property(routingPropertyId, policy.Binding, policySourceVersion: 5),
                Property(historicalPropertyId, policy.Binding, policySourceVersion: 8)
            ]);
        GuestAnonymisationEligibilityEvaluator evaluator = CreateEvaluator(
            policy.Registry,
            snapshot);

        GuestAnonymisationEligibilityResult result = await evaluator.EvaluateAsync(
            Request(
                guestId,
                routingPropertyId,
                guestVersion: 4,
                RoutePolicy(policy.Binding, policySourceVersion: 5)),
            CancellationToken.None);

        Assert.Equal(GuestAnonymisationEligibilityStatus.Eligible, result.Status);
        Assert.Equal(GuestAnonymisationBlockerCode.None, result.BlockerCode);
        Assert.Equal(2, result.AffectedPropertyCount);
        Assert.Equal(64, result.AffectedPropertySetSha256?.Length);
        Assert.Equal(64, result.PolicySetSha256?.Length);
    }

    [Fact]
    public async Task Active_hold_blocks_before_destructive_eligibility()
    {
        PolicyFixture policy = CreatePolicy();
        Guid guestId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        GuestAnonymisationEligibilityEvaluator evaluator = CreateEvaluator(
            policy.Registry,
            new(
                GuestVersion: 2,
                GuestProfileState.Active,
                propertyId,
                Stays: [],
                ActiveHoldPropertyIds: [propertyId],
                Properties: [Property(propertyId, policy.Binding, 3)]));

        GuestAnonymisationEligibilityResult result = await evaluator.EvaluateAsync(
            Request(guestId, propertyId, 2, RoutePolicy(policy.Binding, 3)),
            CancellationToken.None);

        Assert.Equal(GuestAnonymisationBlockerCode.ActiveDataHold, result.BlockerCode);
        Assert.Null(result.AffectedPropertySetSha256);
        Assert.Null(result.PolicySetSha256);
    }

    [Theory]
    [InlineData(GuestStayStatus.Confirmed)]
    [InlineData(GuestStayStatus.CheckedIn)]
    [InlineData(GuestStayStatus.CheckoutPending)]
    public async Task Current_operational_stay_blocks(
        GuestStayStatus stayStatus)
    {
        PolicyFixture policy = CreatePolicy();
        Guid guestId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        GuestAnonymisationEligibilityEvaluator evaluator = CreateEvaluator(
            policy.Registry,
            new(
                GuestVersion: 2,
                GuestProfileState.Active,
                propertyId,
                [
                    new(
                        propertyId,
                        Guid.NewGuid(),
                        GuestStayRole.Primary,
                        stayStatus,
                        IsCurrentParticipant: true,
                        ReservationVersion: 3,
                        GuestsModuleMetadata.StayHistoryProjectionVersion)
                ],
                ActiveHoldPropertyIds: [],
                Properties: [Property(propertyId, policy.Binding, 3)]));

        GuestAnonymisationEligibilityResult result = await evaluator.EvaluateAsync(
            Request(guestId, propertyId, 2, RoutePolicy(policy.Binding, 3)),
            CancellationToken.None);

        Assert.Equal(GuestAnonymisationBlockerCode.ActiveOrFutureStay, result.BlockerCode);
    }

    [Fact]
    public async Task Stale_route_policy_and_unsupported_projection_fail_closed()
    {
        PolicyFixture policy = CreatePolicy();
        Guid guestId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        GuestAnonymisationEligibilitySnapshot unsupported = new(
            GuestVersion: 2,
            GuestProfileState.Active,
            propertyId,
            [
                new(
                    propertyId,
                    Guid.NewGuid(),
                    GuestStayRole.Primary,
                    GuestStayStatus.CheckedOut,
                    IsCurrentParticipant: false,
                    ReservationVersion: 3,
                    ProjectionContractVersion:
                        GuestsModuleMetadata.StayHistoryProjectionVersion + 1)
            ],
            ActiveHoldPropertyIds: [],
            Properties: [Property(propertyId, policy.Binding, 3)]);
        GuestAnonymisationEligibilityEvaluator evaluator = CreateEvaluator(
            policy.Registry,
            unsupported);

        GuestAnonymisationEligibilityResult projectionResult =
            await evaluator.EvaluateAsync(
                Request(guestId, propertyId, 2, RoutePolicy(policy.Binding, 3)),
                CancellationToken.None);
        Assert.Equal(
            GuestAnonymisationBlockerCode.StayProjectionUnsupported,
            projectionResult.BlockerCode);

        GuestAnonymisationEligibilitySnapshot supported =
            unsupported with { Stays = [] };
        evaluator = CreateEvaluator(policy.Registry, supported);
        GuestAnonymisationRoutingPolicyEvidence changedDigest =
            RoutePolicy(policy.Binding, 3) with { ContentSha256 = new string('b', 64) };
        GuestAnonymisationEligibilityResult policyResult =
            await evaluator.EvaluateAsync(
                Request(guestId, propertyId, 2, changedDigest),
                CancellationToken.None);
        Assert.Equal(
            GuestAnonymisationBlockerCode.RoutingPolicyDigestChanged,
            policyResult.BlockerCode);

        GuestAnonymisationRoutingPolicyEvidence changedPurpose =
            RoutePolicy(policy.Binding, 3) with { PurposeCode = "guest-profile-management" };
        GuestAnonymisationEligibilityResult purposeResult =
            await evaluator.EvaluateAsync(
                Request(guestId, propertyId, 2, changedPurpose),
                CancellationToken.None);
        Assert.Equal(
            GuestAnonymisationBlockerCode.RoutingPurposeChanged,
            purposeResult.BlockerCode);
    }

    private static GuestAnonymisationEligibilityEvaluator CreateEvaluator(
        CountryPolicyRegistry registry,
        GuestAnonymisationEligibilitySnapshot snapshot) =>
        new(
            new StubEligibilityRepository(snapshot),
            registry,
            new TestScopeContext(),
            new TestClock());

    private static GuestAnonymisationEligibilityRequest Request(
        Guid guestId,
        Guid routingPropertyId,
        long guestVersion,
        GuestAnonymisationRoutingPolicyEvidence routingPolicy) =>
        new(
            GuestAnonymisationEligibilityContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            ApprovalRevision: 4,
            OperationRevision: 5,
            routingPropertyId,
            guestId,
            guestVersion,
            routingPolicy);

    private static GuestAnonymisationPropertySnapshot Property(
        Guid propertyId,
        PropertyGovernancePolicyBinding policy,
        long policySourceVersion) =>
        new(
            propertyId,
            IsKnown: true,
            IsActive: true,
            PropertyProcessingStatus.Enabled,
            TopologySourceVersion: 2,
            policySourceVersion,
            policy);

    private static GuestAnonymisationRoutingPolicyEvidence RoutePolicy(
        PropertyGovernancePolicyBinding policy,
        long policySourceVersion) =>
        new(
            policySourceVersion,
            policy.OperatingCountryCode,
            policy.PolicyId,
            policy.PolicyVersion,
            policy.RetentionPolicyId,
            policy.RetentionPolicyVersion,
            policy.ContentSha256,
            "data-rights-anonymisation",
            "erasure",
            "authorized-workspace-operator",
            Now.AddMinutes(-1));

    private static PolicyFixture CreatePolicy()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "gb-hostel",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = Now.AddDays(-1),
            ExpiresAtUtc = Now.AddDays(30),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["ordinary-guest"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "guest.primary-name",
                    GuestCategory = "ordinary-guest",
                    Requirement = CountryPolicyFieldRequirement.Required,
                    PurposeCodes = ["data-rights-anonymisation"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "data-rights-anonymisation",
                    LegalRuleReferenceKeys = ["approved-erasure"],
                    AllowedSurfaces = [CountryPolicySurface.Erasure],
                    AllowedSourceProvenance = ["authorized-workspace-operator"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "guest-operational",
                    RetentionPolicyVersion = 1,
                    DataClass = "guest-operational",
                    Trigger = "stay-ended",
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
                ReviewedAtUtc = Now.AddDays(-2),
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
        PropertyGovernancePolicyBinding binding = new(
            document.OperatingCountryCode,
            document.PolicyId,
            document.PolicyVersion,
            document.PermittedDataRegions.Single(),
            document.PermittedTransferProfiles.Single(),
            document.RetentionRules.Single().RetentionPolicyId,
            document.RetentionRules.Single().RetentionPolicyVersion,
            digest,
            document.EffectiveAtUtc,
            document.ExpiresAtUtc,
            Now.AddHours(-1),
            []);
        return new(registry, binding);
    }

    private sealed record PolicyFixture(
        CountryPolicyRegistry Registry,
        PropertyGovernancePolicyBinding Binding);

    private sealed class StubEligibilityRepository(
        GuestAnonymisationEligibilitySnapshot snapshot)
        : IGuestAnonymisationEligibilityRepository
    {
        public Task<GuestAnonymisationEligibilitySnapshot?> LoadAsync(
            Guid guestId,
            CancellationToken cancellationToken) =>
            Task.FromResult<GuestAnonymisationEligibilitySnapshot?>(snapshot);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
