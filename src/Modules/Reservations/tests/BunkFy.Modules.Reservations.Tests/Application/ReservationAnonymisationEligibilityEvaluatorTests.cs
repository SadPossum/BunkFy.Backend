namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationAnonymisationEligibilityEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Terminal_direct_reservation_with_exact_evidence_is_eligible()
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(
                policy.Registry,
                Snapshot(
                    policy.Binding,
                    ReservationState.CheckedOut,
                    ReservationSource.Direct));

        ReservationAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)),
                CancellationToken.None);

        Assert.Equal(
            ReservationAnonymisationEligibilityStatus.Eligible,
            result.Status);
        Assert.Equal(
            ReservationAnonymisationBlockerCode.None,
            result.BlockerCode);
        Assert.Equal(64, result.PolicyEvidenceSha256?.Length);
    }

    [Fact]
    public async Task Active_hold_blocks_after_current_restriction_state_is_proven()
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilitySnapshot snapshot =
            Snapshot(
                policy.Binding,
                ReservationState.CheckedOut,
                ReservationSource.Direct) with
            {
                ActiveHoldCount = 1
            };
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(policy.Registry, snapshot);

        ReservationAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)),
                CancellationToken.None);

        Assert.Equal(
            ReservationAnonymisationBlockerCode.ActiveDataHold,
            result.BlockerCode);
        Assert.Equal(1, result.ActiveHoldCount);
        Assert.Null(result.PolicyEvidenceSha256);
    }

    [Theory]
    [InlineData(
        ReservationState.PendingAllocation,
        ReservationAnonymisationBlockerCode.AllocationOrReleasePending)]
    [InlineData(
        ReservationState.CheckoutPending,
        ReservationAnonymisationBlockerCode.AllocationOrReleasePending)]
    [InlineData(
        ReservationState.Confirmed,
        ReservationAnonymisationBlockerCode.ActiveOrFutureStay)]
    [InlineData(
        ReservationState.CheckedIn,
        ReservationAnonymisationBlockerCode.ActiveOrFutureStay)]
    public async Task Operational_reservations_fail_closed(
        ReservationState state,
        ReservationAnonymisationBlockerCode expected)
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(
                policy.Registry,
                Snapshot(policy.Binding, state, ReservationSource.Direct));

        ReservationAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)),
                CancellationToken.None);

        Assert.Equal(expected, result.BlockerCode);
    }

    [Fact]
    public async Task External_direct_reference_requires_provider_reconciliation()
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(
                policy.Registry,
                Snapshot(
                    policy.Binding,
                    ReservationState.Cancelled,
                    ReservationSource.External) with
                {
                    HasDirectSourceReference = true
                });

        ReservationAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)),
                CancellationToken.None);

        Assert.Equal(
            ReservationAnonymisationBlockerCode.ProviderReferenceRequired,
            result.BlockerCode);
        Assert.Null(result.PolicyEvidenceSha256);
    }

    [Fact]
    public async Task Already_anonymised_reservation_fails_closed()
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(
                policy.Registry,
                Snapshot(
                    policy.Binding,
                    ReservationState.CheckedOut,
                    ReservationSource.Direct) with
                {
                    IsAnonymised = true
                });

        ReservationAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)),
                CancellationToken.None);

        Assert.Equal(
            ReservationAnonymisationEligibilityStatus.Blocked,
            result.Status);
        Assert.Equal(
            ReservationAnonymisationBlockerCode.AlreadyRedacted,
            result.BlockerCode);
        Assert.Null(result.PolicyEvidenceSha256);
    }

    [Fact]
    public async Task Stale_details_and_routing_policy_evidence_fail_closed()
    {
        PolicyFixture policy = CreatePolicy();
        Guid propertyId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        ReservationAnonymisationEligibilityEvaluator evaluator =
            CreateEvaluator(
                policy.Registry,
                Snapshot(
                    policy.Binding,
                    ReservationState.CheckedOut,
                    ReservationSource.Direct));

        ReservationAnonymisationEligibilityResult staleDetails =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding)) with
                {
                    SelectedDetailsRevision = 3
                },
                CancellationToken.None);
        ReservationAnonymisationEligibilityResult stalePolicy =
            await evaluator.EvaluateAsync(
                Request(
                    propertyId,
                    reservationId,
                    RoutePolicy(policy.Binding) with
                    {
                        ContentSha256 = new string('b', 64)
                    }),
                CancellationToken.None);

        Assert.Equal(
            ReservationAnonymisationBlockerCode.DetailsRevisionChanged,
            staleDetails.BlockerCode);
        Assert.Equal(
            ReservationAnonymisationBlockerCode.RoutingPolicyDigestChanged,
            stalePolicy.BlockerCode);
    }

    private static ReservationAnonymisationEligibilityEvaluator CreateEvaluator(
        CountryPolicyRegistry registry,
        ReservationAnonymisationEligibilitySnapshot snapshot) =>
        new(
            new StubEligibilityRepository(snapshot),
            registry,
            new TestScopeContext(),
            new TestClock());

    private static ReservationAnonymisationEligibilitySnapshot Snapshot(
        PropertyGovernancePolicyBinding policy,
        ReservationState state,
        ReservationSource source) =>
        new(
            ReservationVersion: 4,
            DetailsRevision: 2,
            state,
            HasPendingAllocationAmendment: false,
            source,
            HasDirectSourceReference: false,
            ActiveHoldCount: 0,
            ReservationProcessingRestrictionContract.CurrentVersion,
            new(
                IsKnown: true,
                IsActive: false,
                PropertyProcessingStatus.Enabled,
                TopologySourceVersion: 7,
                PolicySourceVersion: 5,
                policy));

    private static ReservationAnonymisationEligibilityRequest Request(
        Guid propertyId,
        Guid reservationId,
        ReservationAnonymisationRoutingPolicyEvidence routingPolicy) =>
        new(
            ReservationAnonymisationEligibilityContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            ApprovalRevision: 4,
            OperationRevision: 5,
            propertyId,
            reservationId,
            SelectedReservationVersion: 4,
            SelectedDetailsRevision: 2,
            routingPolicy);

    private static ReservationAnonymisationRoutingPolicyEvidence RoutePolicy(
        PropertyGovernancePolicyBinding policy) =>
        new(
            PropertyPolicySourceVersion: 5,
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
                    FieldPolicyKey = "reservation.primary-guest-name",
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
                    AllowedSourceProvenance =
                        ["authorized-workspace-operator"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "reservation-operational",
                    RetentionPolicyVersion = 1,
                    DataClass = "reservation-operational",
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
        ReservationAnonymisationEligibilitySnapshot snapshot)
        : IReservationAnonymisationEligibilityRepository
    {
        public Task<ReservationAnonymisationEligibilitySnapshot?> LoadAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReservationAnonymisationEligibilitySnapshot?>(
                snapshot);
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
