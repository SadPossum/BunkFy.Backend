namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Application.Ports;

public sealed class PropertyGovernanceRevision
{
    private PropertyGovernanceRevision() { }

    internal PropertyGovernanceRevision(PropertyGovernanceRevisionWriteModel revision)
    {
        this.Id = revision.RevisionId;
        this.ScopeId = revision.ScopeId;
        this.PropertyId = revision.PropertyId;
        this.PropertyVersion = revision.PropertyVersion;
        this.Action = revision.Action;
        this.DecisionReasonCode = revision.DecisionReasonCode;
        this.Previous = PropertyGovernanceRevisionCoordinatesRecord.From(revision.Previous);
        this.Current = PropertyGovernanceRevisionCoordinatesRecord.From(revision.Current);
        this.ActorId = revision.ActorId;
        this.OccurredAtUtc = revision.OccurredAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public long PropertyVersion { get; private set; }
    public PropertyGovernanceRevisionAction Action { get; private set; }
    public string DecisionReasonCode { get; private set; } = string.Empty;
    public PropertyGovernanceRevisionCoordinatesRecord? Previous { get; private set; }
    public PropertyGovernanceRevisionCoordinatesRecord? Current { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}

public sealed class PropertyGovernanceRevisionCoordinatesRecord
{
    private PropertyGovernanceRevisionCoordinatesRecord() { }

    private PropertyGovernanceRevisionCoordinatesRecord(PropertyGovernanceRevisionCoordinates coordinates)
    {
        this.OperatingCountryCode = coordinates.OperatingCountryCode;
        this.PolicyId = coordinates.PolicyId;
        this.PolicyVersion = coordinates.PolicyVersion;
        this.DataRegionId = coordinates.DataRegionId;
        this.TransferProfileId = coordinates.TransferProfileId;
        this.RetentionPolicyId = coordinates.RetentionPolicyId;
        this.RetentionPolicyVersion = coordinates.RetentionPolicyVersion;
        this.ContentSha256 = coordinates.ContentSha256;
        this.AcknowledgementSetSha256 = coordinates.AcknowledgementSetSha256;
    }

    public string OperatingCountryCode { get; private set; } = string.Empty;
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string DataRegionId { get; private set; } = string.Empty;
    public string TransferProfileId { get; private set; } = string.Empty;
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string ContentSha256 { get; private set; } = string.Empty;
    public string AcknowledgementSetSha256 { get; private set; } = string.Empty;

    internal static PropertyGovernanceRevisionCoordinatesRecord? From(
        PropertyGovernanceRevisionCoordinates? coordinates) =>
        coordinates is null ? null : new(coordinates);
}
