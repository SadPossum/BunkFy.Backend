namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Domain;

public sealed class ReservationPropertyProjection : IScopedEntity
{
    private ReservationPropertyProjection() { }

    private ReservationPropertyProjection(Guid id, string scopeId)
    {
        this.Id = id;
        this.ScopeId = scopeId;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public string? TimeZoneId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsKnown { get; private set; }
    public PropertyProcessingStatus ProcessingStatus { get; private set; } = PropertyProcessingStatus.Unconfigured;
    public ReservationPropertyPolicyBinding? GovernancePolicy { get; private set; }
    public long TopologySourceVersion { get; private set; }
    public long PolicySourceVersion { get; private set; }

    internal static ReservationPropertyProjection Create(Guid propertyId, string scopeId) =>
        new(propertyId, scopeId);

    internal bool ApplyTopology(string? timeZoneId, bool isActive, long sourceVersion)
    {
        if (sourceVersion <= this.TopologySourceVersion)
        {
            return false;
        }

        string? previousTimeZoneId = this.TimeZoneId;
        bool previousIsActive = this.IsActive;
        bool wasKnown = this.IsKnown;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            this.TimeZoneId = timeZoneId.Trim();
        }

        this.IsActive = isActive;
        this.IsKnown = true;
        this.TopologySourceVersion = sourceVersion;
        return !wasKnown ||
               previousIsActive != this.IsActive ||
               !string.Equals(previousTimeZoneId, this.TimeZoneId, StringComparison.Ordinal);
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

        bool isConfigured = processingStatus is PropertyProcessingStatus.Enabled or PropertyProcessingStatus.Suspended;
        if (processingStatus == PropertyProcessingStatus.Unknown || isConfigured != (governancePolicy is not null))
        {
            throw new ArgumentException("The projected property policy is inconsistent.", nameof(governancePolicy));
        }

        this.ProcessingStatus = processingStatus;
        this.GovernancePolicy = governancePolicy is null
            ? null
            : ReservationPropertyPolicyBinding.From(governancePolicy);
        this.IsKnown = true;
        this.PolicySourceVersion = sourceVersion;
    }
}

public sealed class ReservationPropertyPolicyBinding
{
    private readonly List<ReservationPropertyPolicyAcknowledgement> acknowledgements = [];

    private ReservationPropertyPolicyBinding() { }

    private ReservationPropertyPolicyBinding(PropertyGovernancePolicyBinding policy)
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
            new ReservationPropertyPolicyAcknowledgement(
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
    public IReadOnlyCollection<ReservationPropertyPolicyAcknowledgement> Acknowledgements =>
        this.acknowledgements.AsReadOnly();

    internal static ReservationPropertyPolicyBinding From(PropertyGovernancePolicyBinding policy) => new(policy);
}

public sealed class ReservationPropertyPolicyAcknowledgement
{
    private ReservationPropertyPolicyAcknowledgement() { }

    internal ReservationPropertyPolicyAcknowledgement(string acknowledgementId, int acknowledgementVersion)
    {
        this.AcknowledgementId = acknowledgementId;
        this.AcknowledgementVersion = acknowledgementVersion;
    }

    public string AcknowledgementId { get; private set; } = string.Empty;
    public int AcknowledgementVersion { get; private set; }
}
