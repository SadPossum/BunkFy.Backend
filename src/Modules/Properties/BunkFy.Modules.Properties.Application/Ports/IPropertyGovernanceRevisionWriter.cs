namespace BunkFy.Modules.Properties.Application.Ports;

public interface IPropertyGovernanceRevisionWriter
{
    Task AppendAsync(PropertyGovernanceRevisionWriteModel revision, CancellationToken cancellationToken);
}

public sealed record PropertyGovernanceRevisionWriteModel(
    Guid RevisionId,
    string ScopeId,
    Guid PropertyId,
    long PropertyVersion,
    PropertyGovernanceRevisionAction Action,
    string DecisionReasonCode,
    PropertyGovernanceRevisionCoordinates? Previous,
    PropertyGovernanceRevisionCoordinates? Current,
    string ActorId,
    DateTimeOffset OccurredAtUtc);

public sealed record PropertyGovernanceRevisionCoordinates(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string AcknowledgementSetSha256);

public enum PropertyGovernanceRevisionAction
{
    Unknown = 0,
    Activated = 1,
    Rebound = 2,
    Reactivated = 3,
    Suspended = 4
}
