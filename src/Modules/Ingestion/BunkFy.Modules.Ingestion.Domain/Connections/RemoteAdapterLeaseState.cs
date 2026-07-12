namespace BunkFy.Modules.Ingestion.Domain.Connections;

public sealed record RemoteAdapterLeaseState(
    Guid RunId,
    Guid LeaseId,
    Guid ClaimId,
    long LeaseEpoch,
    Guid CredentialId,
    Guid WorkerId,
    DateTimeOffset ExpiresAtUtc);
