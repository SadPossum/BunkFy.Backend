namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Policies;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsAnonymisationApprovalPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Active_enabled_property_mints_frozen_policy_evidence()
    {
        Guid propertyId = Guid.NewGuid();
        (CountryPolicyRegistry registry, PropertyGovernancePolicyBinding binding) =
            CreatePolicy();
        DataRightsAnonymisationApprovalPolicy policy = new(
            new StubPropertyRepository(CreateSnapshot(binding)),
            registry,
            new TestClock());

        Result<DataRightsApprovalPolicyEvidence> result = await policy.EvaluateAsync(
            propertyId,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(propertyId, result.Value.PropertyId);
        Assert.Equal(7, result.Value.PropertyVersion);
        Assert.Equal(binding.PolicyId, result.Value.PolicyId);
        Assert.Equal(binding.ContentSha256, result.Value.ContentSha256);
        Assert.Equal(
            DataRightsAnonymisationApprovalPolicy.PurposeCode,
            result.Value.PurposeCode);
        Assert.True(result.Value.RequiresDistinctExecutor);

        static DataRightsPropertyPolicySnapshot CreateSnapshot(
            PropertyGovernancePolicyBinding governancePolicy) => new(
            true,
            true,
            PropertyProcessingStatus.Enabled,
            governancePolicy,
            7);
    }

    [Theory]
    [InlineData(false, true, PropertyProcessingStatus.Enabled)]
    [InlineData(true, false, PropertyProcessingStatus.Enabled)]
    [InlineData(true, true, PropertyProcessingStatus.Suspended)]
    public async Task Missing_inactive_or_suspended_property_fails_closed(
        bool isKnown,
        bool isActive,
        PropertyProcessingStatus processingStatus)
    {
        (CountryPolicyRegistry registry, PropertyGovernancePolicyBinding binding) =
            CreatePolicy();
        DataRightsAnonymisationApprovalPolicy policy = new(
            new StubPropertyRepository(new(
                isKnown,
                isActive,
                processingStatus,
                binding,
                7)),
            registry,
            new TestClock());

        Result<DataRightsApprovalPolicyEvidence> result = await policy.EvaluateAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied.Code,
            result.Error.Code);
    }

    [Fact]
    public async Task Policy_registry_mismatch_fails_closed()
    {
        (_, PropertyGovernancePolicyBinding binding) = CreatePolicy();
        DataRightsAnonymisationApprovalPolicy policy = new(
            new StubPropertyRepository(new(
                true,
                true,
                PropertyProcessingStatus.Enabled,
                binding,
                7)),
            CountryPolicyRegistry.Create([], [], CountryPolicyRuntimeMode.Production),
            new TestClock());

        Result<DataRightsApprovalPolicyEvidence> result = await policy.EvaluateAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.AnonymisationApprovalPolicyDenied.Code,
            result.Error.Code);
    }

    private static (
        CountryPolicyRegistry Registry,
        PropertyGovernancePolicyBinding Binding) CreatePolicy()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "CountryPolicies",
            "example-hostel-policy.v1.json"));
        CountryPolicyPackArtifact artifact = CountryPolicyPackJson.Parse(bytes);
        CountryPolicyPackDocument document = artifact.Document;
        CountryPolicyRegistry registry = CountryPolicyRegistry.Create(
            [artifact],
            [new(
                document.OperatingCountryCode,
                document.PolicyId,
                document.PolicyVersion,
                artifact.ContentSha256,
                CountryLaunchStatus.Engineering)],
            CountryPolicyRuntimeMode.Engineering);
        CountryPolicyDecision activation = registry.EvaluateActivation(new(
            document.OperatingCountryCode,
            document.PolicyId,
            document.PolicyVersion,
            document.PermittedDataRegions.Single(),
            document.PermittedTransferProfiles.Single(),
            document.RetentionRules.Single().RetentionPolicyId,
            document.RetentionRules.Single().RetentionPolicyVersion,
            document.RequiredAcknowledgements.Select(acknowledgement =>
                new CountryPolicyAcknowledgement(
                    acknowledgement.AcknowledgementId,
                    acknowledgement.AcknowledgementVersion)).ToArray(),
            DataRightsAnonymisationApprovalPolicy.AccommodationType,
            "property-activation",
            DataRightsAnonymisationApprovalPolicy.SourceProvenance,
            Now));
        CountryPolicyEvidence evidence = Assert.IsType<CountryPolicyEvidence>(
            activation.Evidence);
        return (
            registry,
            new(
                evidence.OperatingCountryCode,
                evidence.PolicyId,
                evidence.PolicyVersion,
                evidence.DataRegionId,
                evidence.TransferProfileId,
                evidence.RetentionPolicyId,
                evidence.RetentionPolicyVersion,
                evidence.ContentSha256,
                evidence.EffectiveAtUtc,
                evidence.ExpiresAtUtc,
                evidence.EvaluatedAtUtc,
                evidence.AcceptedAcknowledgements.Select(acknowledgement =>
                    new PropertyGovernanceAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion)).ToArray()));
    }

    private sealed class StubPropertyRepository(DataRightsPropertyPolicySnapshot? property)
        : IDataRightsPropertyProjectionRepository
    {
        public Task ApplyTopologyAsync(
            DataRightsPropertyTopologyWriteModel propertyWriteModel,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyPolicyAsync(
            DataRightsPropertyPolicyWriteModel propertyWriteModel,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DataRightsPropertyPolicySnapshot?> GetPolicyAsync(
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(property);
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
