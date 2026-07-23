namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Properties.Contracts;

public sealed class GuestPropertyProjection
{
    private GuestPropertyProjection() { }

    public GuestPropertyProjection(
        string scopeId,
        Guid id,
        string? name,
        PropertyStatus status,
        long version)
    {
        this.ScopeId = scopeId;
        this.Id = id;
        this.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        this.Status = status;
        this.IsKnown = version > 0 && status != PropertyStatus.Unknown;
        this.TopologySourceVersion = version;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public PropertyStatus Status { get; private set; }
    public bool IsKnown { get; private set; }
    public PropertyProcessingStatus ProcessingStatus { get; private set; } = PropertyProcessingStatus.Unconfigured;
    public GuestPropertyPolicyBinding? GovernancePolicy { get; private set; }
    public long TopologySourceVersion { get; private set; }
    public long PolicySourceVersion { get; private set; }

    public void ApplyTopology(string? name, PropertyStatus status, long sourceVersion)
    {
        if (sourceVersion <= this.TopologySourceVersion)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            this.Name = name.Trim();
        }

        this.Status = status;
        this.IsKnown = true;
        this.TopologySourceVersion = sourceVersion;
    }

    public void ApplyPolicy(
        PropertyProcessingStatus processingStatus,
        PropertyGovernancePolicyBinding? governancePolicy,
        long sourceVersion)
    {
        if (sourceVersion <= this.PolicySourceVersion)
        {
            return;
        }

        bool isConfigured = processingStatus is PropertyProcessingStatus.Enabled or PropertyProcessingStatus.Suspended;
        if (processingStatus == PropertyProcessingStatus.Unknown || isConfigured != (governancePolicy is not null))
        {
            throw new ArgumentException("The projected property policy is inconsistent.", nameof(governancePolicy));
        }

        this.ProcessingStatus = processingStatus;
        this.GovernancePolicy = governancePolicy is null ? null : GuestPropertyPolicyBinding.From(governancePolicy);
        this.IsKnown = true;
        this.PolicySourceVersion = sourceVersion;
    }
}

public sealed class GuestPropertyPolicyBinding
{
    private readonly List<GuestPropertyPolicyAcknowledgement> acknowledgements = [];

    private GuestPropertyPolicyBinding() { }

    private GuestPropertyPolicyBinding(PropertyGovernancePolicyBinding policy)
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
            new GuestPropertyPolicyAcknowledgement(
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
    public IReadOnlyCollection<GuestPropertyPolicyAcknowledgement> Acknowledgements =>
        this.acknowledgements.AsReadOnly();

    internal static GuestPropertyPolicyBinding From(PropertyGovernancePolicyBinding policy) => new(policy);
}

public sealed class GuestPropertyPolicyAcknowledgement
{
    private GuestPropertyPolicyAcknowledgement() { }

    internal GuestPropertyPolicyAcknowledgement(string acknowledgementId, int acknowledgementVersion)
    {
        this.AcknowledgementId = acknowledgementId;
        this.AcknowledgementVersion = acknowledgementVersion;
    }

    public string AcknowledgementId { get; private set; } = string.Empty;
    public int AcknowledgementVersion { get; private set; }
}
