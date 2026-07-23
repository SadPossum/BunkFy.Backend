namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionCountryPolicyAdmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Admission_allows_an_enabled_property_with_an_exact_current_binding()
    {
        CountryPolicyPackArtifact artifact = CreateArtifact();
        IngestionCountryPolicyAdmission admission = CreateAdmission(
            CreateSnapshot(artifact, PropertyProcessingStatus.Enabled),
            artifact,
            Now);

        CountryPolicyDecision decision = await admission.EvaluateAsync(
            Guid.NewGuid(),
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            CancellationToken.None);

        Assert.True(decision.IsAllowed);
        CountryPolicyEvidence evidence = Assert.IsType<CountryPolicyEvidence>(decision.Evidence);
        Assert.Equal(artifact.ContentSha256, evidence.ContentSha256);
        Assert.Equal(CountryPolicySurface.AdapterIngress, evidence.Surface);
        Assert.Equal(Now, evidence.EvaluatedAtUtc);
    }

    [Theory]
    [InlineData(false, true, PropertyProcessingStatus.Enabled)]
    [InlineData(true, false, PropertyProcessingStatus.Enabled)]
    [InlineData(true, true, PropertyProcessingStatus.Unconfigured)]
    [InlineData(true, true, PropertyProcessingStatus.Suspended)]
    public async Task Admission_denies_missing_inactive_or_disabled_property_state(
        bool isKnown,
        bool isActive,
        PropertyProcessingStatus processingStatus)
    {
        CountryPolicyPackArtifact artifact = CreateArtifact();
        IngestionPropertyPolicySnapshot snapshot = new(
            isKnown,
            isActive,
            processingStatus,
            processingStatus is PropertyProcessingStatus.Enabled or PropertyProcessingStatus.Suspended
                ? CreateBinding(artifact)
                : null);
        IngestionCountryPolicyAdmission admission = CreateAdmission(snapshot, artifact, Now);

        CountryPolicyDecision decision = await admission.EvaluateAsync(
            Guid.NewGuid(),
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            CancellationToken.None);

        Assert.False(decision.IsAllowed);
        Assert.Equal(CountryPolicyDecisionReason.MissingBinding, decision.Reason);
        Assert.Null(decision.Evidence);
    }

    [Fact]
    public async Task Admission_rechecks_digest_and_expiry_instead_of_trusting_the_projection()
    {
        CountryPolicyPackArtifact artifact = CreateArtifact();
        IngestionPropertyPolicySnapshot drifted = CreateSnapshot(
            artifact,
            PropertyProcessingStatus.Enabled,
            contentSha256: new string('0', 64));
        IngestionCountryPolicyAdmission digestAdmission = CreateAdmission(drifted, artifact, Now);
        IngestionCountryPolicyAdmission expiredAdmission = CreateAdmission(
            CreateSnapshot(artifact, PropertyProcessingStatus.Enabled),
            artifact,
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        CountryPolicyDecision digestDecision = await digestAdmission.EvaluateAsync(
            Guid.NewGuid(),
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            CancellationToken.None);
        CountryPolicyDecision expiryDecision = await expiredAdmission.EvaluateAsync(
            Guid.NewGuid(),
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            CancellationToken.None);

        Assert.Equal(CountryPolicyDecisionReason.ContentDigestMismatch, digestDecision.Reason);
        Assert.Equal(CountryPolicyDecisionReason.PolicyExpired, expiryDecision.Reason);
    }

    private static IngestionCountryPolicyAdmission CreateAdmission(
        IngestionPropertyPolicySnapshot? snapshot,
        CountryPolicyPackArtifact artifact,
        DateTimeOffset nowUtc) =>
        new(
            new TestPropertyProjectionRepository(snapshot),
            CountryPolicyRegistry.Create(
                [artifact],
                [new("GB", "gb-hostel", 1, artifact.ContentSha256, CountryLaunchStatus.Approved)],
                CountryPolicyRuntimeMode.Production),
            new TestClock(nowUtc));

    private static IngestionPropertyPolicySnapshot CreateSnapshot(
        CountryPolicyPackArtifact artifact,
        PropertyProcessingStatus status,
        string? contentSha256 = null) =>
        new(true, true, status, CreateBinding(artifact, contentSha256));

    private static PropertyGovernancePolicyBinding CreateBinding(
        CountryPolicyPackArtifact artifact,
        string? contentSha256 = null) =>
        new(
            "GB",
            "gb-hostel",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "guest-operational",
            1,
            contentSha256 ?? artifact.ContentSha256,
            artifact.Document.EffectiveAtUtc,
            artifact.Document.ExpiresAtUtc,
            Now,
            []);

    private static CountryPolicyPackArtifact CreateArtifact()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "gb-hostel",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ExpiresAtUtc = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["ordinary-guest"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "guest.primary-name",
                    GuestCategory = "ordinary-guest",
                    Requirement = CountryPolicyFieldRequirement.Required,
                    PurposeCodes = ["reservation-ingestion"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "reservation-ingestion",
                    LegalRuleReferenceKeys = ["customer-instruction"],
                    AllowedSurfaces =
                    [
                        CountryPolicySurface.ApiWrite,
                        CountryPolicySurface.AdapterIngress,
                        CountryPolicySurface.Import
                    ],
                    AllowedSourceProvenance =
                    [
                        "authorized-workspace-operator",
                        "approved-adapter",
                        "approved-parser"
                    ]
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
                ReviewedAtUtc = new(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
                Sources = [new() { ReferenceId = "source-1", Uri = "https://example.test/policy" }],
                DetachedSignatureReference = "signature-1"
            }
        };
        CountryPolicyPackValidator.ValidateAndThrow(document);
        return new(document, new string('a', 64));
    }

    private sealed class TestPropertyProjectionRepository(IngestionPropertyPolicySnapshot? snapshot)
        : IIngestionPropertyProjectionRepository
    {
        public Task ApplyTopologyAsync(
            IngestionPropertyTopologyWriteModel property,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplyPolicyAsync(
            IngestionPropertyPolicyWriteModel property,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ApplySnapshotAsync(
            IngestionPropertyProjectionWriteModel property,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IngestionPropertyPolicySnapshot?> GetPolicyAsync(
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private sealed class TestClock(DateTimeOffset nowUtc) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = nowUtc;
    }
}
