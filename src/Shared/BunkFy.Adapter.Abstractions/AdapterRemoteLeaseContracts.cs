namespace BunkFy.Adapter.Abstractions;

using System.Text.Json.Serialization;

public static class AdapterRemoteLeaseContractLimits
{
    public const int MinimumLeaseSeconds = 30;
    public const int MaximumLeaseSeconds = 15 * 60;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterRemoteLeaseClaimRequest(
    Guid ClaimId,
    Guid WorkerId,
    string AdapterType,
    int ProtocolVersion,
    int ConfigurationSchemaVersion,
    int RequestedLeaseSeconds);

public sealed record AdapterRemoteLeaseClaimResponse(
    AdapterRunAssignment Assignment,
    long LeaseEpoch,
    int RenewAfterSeconds);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterRemoteLeaseProof(
    Guid RunId,
    Guid LeaseId,
    long LeaseEpoch,
    Guid WorkerId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterRemoteLeaseRenewRequest(
    AdapterRemoteLeaseProof Lease,
    int RequestedLeaseSeconds);

public sealed record AdapterRemoteLeaseRenewResponse(
    Guid RunId,
    Guid LeaseId,
    long LeaseEpoch,
    DateTimeOffset LeaseExpiresAtUtc,
    int RenewAfterSeconds);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterRemoteObservationSubmissionRequest(
    AdapterRemoteLeaseProof Lease,
    IReadOnlyCollection<AdapterIngressObservationRequest> Records,
    string? ProposedCheckpoint);

public sealed record AdapterRemoteObservationSubmissionResponse(
    AdapterObservationAcknowledgement Acknowledgement);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdapterRemoteRunCompletionRequest(
    AdapterRemoteLeaseProof Lease,
    AdapterRunOutcome Outcome,
    int ObservedCount,
    int AcceptedCount,
    int RejectedCount,
    string? AcceptedCheckpoint,
    string? ErrorCode);

public sealed record AdapterRemoteRunCompletionResponse(
    Guid RunId,
    Guid LeaseId,
    long LeaseEpoch,
    AdapterRunOutcome Outcome,
    string? AcceptedCheckpoint,
    DateTimeOffset CompletedAtUtc);
