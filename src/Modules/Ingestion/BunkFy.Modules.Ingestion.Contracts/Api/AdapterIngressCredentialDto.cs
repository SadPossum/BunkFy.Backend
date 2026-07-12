namespace BunkFy.Modules.Ingestion.Contracts;

public enum AdapterIngressCredentialStatus
{
    Unknown = 0,
    Active = 1,
    Revoked = 2,
    Expired = 3
}

public sealed record AdapterIngressCredentialDto(
    Guid CredentialId,
    Guid ConnectionId,
    int Slot,
    string Label,
    AdapterIngressCredentialStatus Status,
    DateTimeOffset ExpiresAtUtc,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string? RevokedBy,
    DateTimeOffset? RevokedAtUtc,
    DateTimeOffset? LastAuthenticatedAtUtc,
    long Version);

public sealed record AdapterIngressCredentialListResponse(
    IReadOnlyCollection<AdapterIngressCredentialDto> Credentials,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record CreateAdapterIngressCredentialResponse(
    AdapterIngressCredentialDto Credential,
    string Token);
