namespace BunkFy.Modules.Properties.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Handlers;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertyProcessingCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Activation_persists_the_exact_policy_evidence_and_append_only_revision()
    {
        Property property = CreateProperty();
        RecordingRevisionWriter revisions = new();
        CountryPolicyPackArtifact artifact = CreateArtifact();
        ActivatePropertyProcessingCommandHandler handler = new(
            new FakePropertyRepository(property),
            revisions,
            CreateRegistry(artifact),
            new TestClock(),
            new TestIdGenerator());

        Result<PropertyDto> result = await handler.HandleAsync(
            new ActivatePropertyProcessingCommand(
                property.Id,
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
                1,
                [],
                true,
                property.Version,
                " user:owner "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyProcessingState.Enabled, property.ProcessingState);
        Assert.Equal(PropertyProcessingStatus.Enabled, result.Value.ProcessingStatus);
        Assert.Equal(artifact.ContentSha256, property.GovernanceBinding!.ContentSha256);
        Assert.Equal(artifact.ContentSha256, result.Value.GovernancePolicy!.ContentSha256);
        PropertyGovernanceRevisionWriteModel revision = Assert.Single(revisions.Items);
        Assert.Equal(PropertyGovernanceRevisionAction.Activated, revision.Action);
        Assert.Null(revision.Previous);
        Assert.Equal(artifact.ContentSha256, revision.Current!.ContentSha256);
        Assert.Equal("user:owner", revision.ActorId);
        Assert.Equal(property.Version, revision.PropertyVersion);
    }

    [Fact]
    public async Task Activation_denial_does_not_mutate_or_write_a_revision()
    {
        Property property = CreateProperty();
        RecordingRevisionWriter revisions = new();
        ActivatePropertyProcessingCommandHandler handler = new(
            new FakePropertyRepository(property),
            revisions,
            CountryPolicyRegistry.Create([], [], CountryPolicyRuntimeMode.Production),
            new TestClock(),
            new TestIdGenerator());

        Result<PropertyDto> result = await handler.HandleAsync(
            new ActivatePropertyProcessingCommand(
                property.Id,
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
                1,
                [],
                true,
                property.Version,
                "user:owner"),
            CancellationToken.None);

        Assert.Equal(
            PropertiesApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.UnknownPolicy),
            result.Error);
        Assert.Equal(PropertyProcessingState.Unconfigured, property.ProcessingState);
        Assert.Null(property.GovernanceBinding);
        Assert.Empty(revisions.Items);
    }

    [Fact]
    public async Task Activation_requires_server_side_confirmation_before_policy_evaluation()
    {
        Property property = CreateProperty();
        RecordingRevisionWriter revisions = new();
        ActivatePropertyProcessingCommandHandler handler = new(
            new FakePropertyRepository(property),
            revisions,
            CountryPolicyRegistry.Create([], [], CountryPolicyRuntimeMode.Production),
            new TestClock(),
            new TestIdGenerator());

        Result<PropertyDto> result = await handler.HandleAsync(
            new ActivatePropertyProcessingCommand(
                property.Id,
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
                1,
                [],
                false,
                property.Version,
                "user:owner"),
            CancellationToken.None);

        Assert.Equal(PropertiesApplicationErrors.ConfirmationRequired, result.Error);
        Assert.Equal(PropertyProcessingState.Unconfigured, property.ProcessingState);
        Assert.Empty(revisions.Items);
    }

    [Fact]
    public async Task Effective_state_is_authoritative_for_unconfigured_enabled_expired_revoked_and_suspended_bindings()
    {
        Property property = CreateProperty();
        FakePropertyRepository repository = new(property);
        CountryPolicyPackArtifact artifact = CreateArtifact();
        CountryPolicyRegistry registry = CreateRegistry(artifact);

        PropertyProcessingStateDto unconfigured = (await new GetPropertyProcessingStateQueryHandler(
            repository,
            registry,
            new TestClock()).HandleAsync(
                new GetPropertyProcessingStateQuery(property.Id),
                CancellationToken.None)).Value;
        Assert.Equal(PropertyProcessingEffectiveStatus.Unconfigured, unconfigured.EffectiveStatus);
        Assert.Equal(GetPropertyProcessingStateQueryHandler.UnconfiguredReasonCode, unconfigured.ReasonCode);

        Assert.True((await new ActivatePropertyProcessingCommandHandler(
            repository,
            new RecordingRevisionWriter(),
            registry,
            new TestClock(),
            new TestIdGenerator()).HandleAsync(
                new ActivatePropertyProcessingCommand(
                    property.Id,
                    "GB",
                    "gb-hostel",
                    1,
                    "eu-west-2",
                    "uk-no-transfer",
                    "guest-operational",
                    1,
                    [],
                    true,
                    property.Version,
                    "user:owner"),
                CancellationToken.None)).IsSuccess);

        PropertyProcessingStateDto enabled = (await new GetPropertyProcessingStateQueryHandler(
            repository,
            registry,
            new TestClock()).HandleAsync(
                new GetPropertyProcessingStateQuery(property.Id),
                CancellationToken.None)).Value;
        Assert.Equal(PropertyProcessingEffectiveStatus.Enabled, enabled.EffectiveStatus);
        Assert.Equal(GetPropertyProcessingStateQueryHandler.AllowedReasonCode, enabled.ReasonCode);

        PropertyProcessingStateDto expired = (await new GetPropertyProcessingStateQueryHandler(
            repository,
            registry,
            new TestClock(Now.AddDays(31))).HandleAsync(
                new GetPropertyProcessingStateQuery(property.Id),
                CancellationToken.None)).Value;
        Assert.Equal(PropertyProcessingEffectiveStatus.Expired, expired.EffectiveStatus);
        Assert.Equal("Properties.CountryPolicy.PolicyExpired", expired.ReasonCode);

        PropertyProcessingStateDto revoked = (await new GetPropertyProcessingStateQueryHandler(
            repository,
            CountryPolicyRegistry.Create([], [], CountryPolicyRuntimeMode.Production),
            new TestClock()).HandleAsync(
                new GetPropertyProcessingStateQuery(property.Id),
                CancellationToken.None)).Value;
        Assert.Equal(PropertyProcessingEffectiveStatus.Revoked, revoked.EffectiveStatus);
        Assert.Equal("Properties.CountryPolicy.UnknownPolicy", revoked.ReasonCode);

        Assert.True(property.SuspendProcessing(
            property.Version,
            Guid.NewGuid(),
            Now,
            "user:owner").IsSuccess);
        PropertyProcessingStateDto suspended = (await new GetPropertyProcessingStateQueryHandler(
            repository,
            CountryPolicyRegistry.Create([], [], CountryPolicyRuntimeMode.Production),
            new TestClock()).HandleAsync(
                new GetPropertyProcessingStateQuery(property.Id),
                CancellationToken.None)).Value;
        Assert.Equal(PropertyProcessingEffectiveStatus.Suspended, suspended.EffectiveStatus);
        Assert.Equal(GetPropertyProcessingStateQueryHandler.SuspendedReasonCode, suspended.ReasonCode);
    }

    [Fact]
    public async Task Suspension_keeps_policy_coordinates_and_appends_a_revision()
    {
        Property property = CreateProperty();
        CountryPolicyPackArtifact artifact = CreateArtifact();
        RecordingRevisionWriter revisions = new();
        ActivatePropertyProcessingCommandHandler activate = new(
            new FakePropertyRepository(property),
            revisions,
            CreateRegistry(artifact),
            new TestClock(),
            new TestIdGenerator());
        Assert.True((await activate.HandleAsync(
            new ActivatePropertyProcessingCommand(
                property.Id,
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
                1,
                [],
                true,
                property.Version,
                "user:owner"),
            CancellationToken.None)).IsSuccess);
        revisions.Items.Clear();
        SuspendPropertyProcessingCommandHandler suspend = new(
            new FakePropertyRepository(property),
            revisions,
            new TestClock(),
            new TestIdGenerator());

        Result<Unit> result = await suspend.HandleAsync(
            new SuspendPropertyProcessingCommand(property.Id, property.Version, "user:owner"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyProcessingState.Suspended, property.ProcessingState);
        Assert.Equal(artifact.ContentSha256, property.GovernanceBinding!.ContentSha256);
        PropertyGovernanceRevisionWriteModel revision = Assert.Single(revisions.Items);
        Assert.Equal(PropertyGovernanceRevisionAction.Suspended, revision.Action);
        Assert.Equal(revision.Previous, revision.Current);
    }

    private static CountryPolicyRegistry CreateRegistry(CountryPolicyPackArtifact artifact) =>
        CountryPolicyRegistry.Create(
            [artifact],
            [new("GB", "gb-hostel", 1, artifact.ContentSha256, CountryLaunchStatus.Approved)],
            CountryPolicyRuntimeMode.Production);

    private static CountryPolicyPackArtifact CreateArtifact()
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
                    PurposeCodes = ["property-activation"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "property-activation",
                    LegalRuleReferenceKeys = ["operator-approval"],
                    AllowedSurfaces = [CountryPolicySurface.PropertyActivation],
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
                Sources = [new() { ReferenceId = "source-1", Uri = "https://example.test/policy" }],
                DetachedSignatureReference = "signature-1"
            }
        };
        CountryPolicyPackValidator.ValidateAndThrow(document);
        return new(document, new string('a', 64));
    }

    private static Property CreateProperty() =>
        Property.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Hostel One",
            "hostel-one",
            "UTC",
            Guid.NewGuid(),
            Now.AddDays(-10)).Value;

    private sealed class FakePropertyRepository(Property property) : IPropertyRepository
    {
        public Task AddAsync(Property value, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Property?> GetAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult<Property?>(property.Id == propertyId ? property : null);

        public Task<bool> CodeExistsAsync(
            string code,
            Guid? excludingPropertyId,
            CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class RecordingRevisionWriter : IPropertyGovernanceRevisionWriter
    {
        public List<PropertyGovernanceRevisionWriteModel> Items { get; } = [];

        public Task AppendAsync(
            PropertyGovernanceRevisionWriteModel revision,
            CancellationToken cancellationToken)
        {
            this.Items.Add(revision);
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock(DateTimeOffset? now = null) : ISystemClock
    {
        public DateTimeOffset UtcNow => now ?? Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
