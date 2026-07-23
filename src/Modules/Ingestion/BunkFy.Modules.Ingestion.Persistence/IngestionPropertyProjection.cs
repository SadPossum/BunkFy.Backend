namespace BunkFy.Modules.Ingestion.Persistence;

using Gma.Framework.Domain;
using BunkFy.Modules.Properties.Contracts;

public sealed class IngestionPropertyProjection : IScopedEntity
{
    public const int NameMaxLength = 200;
    public const int CodeMaxLength = 64;

    private IngestionPropertyProjection() { }

    private IngestionPropertyProjection(Guid id, string scopeId)
    {
        this.Id = id;
        this.ScopeId = scopeId;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string? Name { get; private set; }
    public string? Code { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsKnown { get; private set; }
    public PropertyProcessingStatus ProcessingStatus { get; private set; } = PropertyProcessingStatus.Unconfigured;
    public IngestionPropertyPolicyBinding? GovernancePolicy { get; private set; }
    public long TopologySourceVersion { get; private set; }
    public long PolicySourceVersion { get; private set; }
    public long RetentionFenceVersion { get; private set; }

    internal static IngestionPropertyProjection Create(Guid propertyId, string scopeId) =>
        new(propertyId, scopeId);

    internal void ApplyTopology(string? name, string? code, bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.TopologySourceVersion)
        {
            return;
        }

        this.Name = string.IsNullOrWhiteSpace(name) ? this.Name : name.Trim();
        this.Code = string.IsNullOrWhiteSpace(code) ? this.Code : code.Trim();
        this.IsActive = isActive;
        this.IsKnown = true;
        this.TopologySourceVersion = sourceVersion;
    }

    internal void ApplyPolicy(
        PropertyProcessingStatus processingStatus,
        PropertyGovernancePolicyBinding? governancePolicy,
        long sourceVersion)
    {
        if (sourceVersion <= this.PolicySourceVersion)
        {
            return;
        }

        this.SetPolicy(processingStatus, governancePolicy);
        this.IsKnown = true;
        this.PolicySourceVersion = sourceVersion;
    }

    internal void ApplySnapshot(
        string? name,
        string? code,
        bool isActive,
        PropertyProcessingStatus processingStatus,
        PropertyGovernancePolicyBinding? governancePolicy,
        long sourceVersion)
    {
        if (sourceVersion <= this.TopologySourceVersion && sourceVersion <= this.PolicySourceVersion)
        {
            return;
        }

        if (sourceVersion > this.TopologySourceVersion)
        {
            this.Name = string.IsNullOrWhiteSpace(name) ? this.Name : name.Trim();
            this.Code = string.IsNullOrWhiteSpace(code) ? this.Code : code.Trim();
            this.IsActive = isActive;
            this.TopologySourceVersion = sourceVersion;
        }

        if (sourceVersion > this.PolicySourceVersion)
        {
            this.SetPolicy(processingStatus, governancePolicy);
            this.PolicySourceVersion = sourceVersion;
        }

        this.IsKnown = true;
    }

    private void SetPolicy(
        PropertyProcessingStatus processingStatus,
        PropertyGovernancePolicyBinding? governancePolicy)
    {
        bool isConfigured = processingStatus is PropertyProcessingStatus.Enabled or PropertyProcessingStatus.Suspended;
        if (processingStatus == PropertyProcessingStatus.Unknown || isConfigured != (governancePolicy is not null))
        {
            throw new ArgumentException("The projected property policy is inconsistent.", nameof(governancePolicy));
        }

        this.ProcessingStatus = processingStatus;
        this.GovernancePolicy = governancePolicy is null
            ? null
            : IngestionPropertyPolicyBinding.From(governancePolicy);
    }

    internal void AdvanceRetentionFence() => this.RetentionFenceVersion++;
}

public sealed class IngestionPropertyPolicyBinding
{
    private readonly List<IngestionPropertyPolicyAcknowledgement> acknowledgements = [];

    private IngestionPropertyPolicyBinding() { }

    private IngestionPropertyPolicyBinding(PropertyGovernancePolicyBinding policy)
    {
        this.OperatingCountryCode = policy.OperatingCountryCode;
        this.PolicyId = policy.PolicyId;
        this.PolicyVersion = policy.PolicyVersion;
        this.DataRegionId = policy.DataRegionId;
        this.TransferProfileId = policy.TransferProfileId;
        this.RetentionPolicyId = policy.RetentionPolicyId;
        this.RetentionPolicyVersion = policy.RetentionPolicyVersion;
        this.ContentSha256 = policy.ContentSha256;
        this.PolicyEffectiveAtUtc = policy.PolicyEffectiveAtUtc;
        this.PolicyExpiresAtUtc = policy.PolicyExpiresAtUtc;
        this.ActivatedAtUtc = policy.ActivatedAtUtc;
        this.acknowledgements.AddRange(policy.Acknowledgements.Select(acknowledgement =>
            new IngestionPropertyPolicyAcknowledgement(
                acknowledgement.AcknowledgementId,
                acknowledgement.AcknowledgementVersion)));
    }

    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string DataRegionId { get; private set; } = string.Empty;
    public string TransferProfileId { get; private set; } = string.Empty;
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public DateTimeOffset PolicyEffectiveAtUtc { get; private set; }
    public DateTimeOffset PolicyExpiresAtUtc { get; private set; }
    public DateTimeOffset ActivatedAtUtc { get; private set; }
    public IReadOnlyCollection<IngestionPropertyPolicyAcknowledgement> Acknowledgements =>
        this.acknowledgements.AsReadOnly();

    internal static IngestionPropertyPolicyBinding From(PropertyGovernancePolicyBinding policy) => new(policy);
}

public sealed class IngestionPropertyPolicyAcknowledgement
{
    private IngestionPropertyPolicyAcknowledgement() { }

    internal IngestionPropertyPolicyAcknowledgement(string acknowledgementId, int acknowledgementVersion)
    {
        this.AcknowledgementId = acknowledgementId;
        this.AcknowledgementVersion = acknowledgementVersion;
    }

    public string AcknowledgementId { get; private set; } = string.Empty;
    public int AcknowledgementVersion { get; private set; }
}
