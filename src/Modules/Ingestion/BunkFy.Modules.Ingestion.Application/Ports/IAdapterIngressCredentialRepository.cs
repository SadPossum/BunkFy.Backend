namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Credentials;

public interface IAdapterIngressCredentialRepository
{
    Task<int?> GetAvailableSlotAsync(Guid connectionId, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<AdapterIngressCredential?> GetAsync(
        Guid connectionId,
        Guid credentialId,
        CancellationToken cancellationToken);
    Task<AdapterIngressCredential?> GetForAuthenticationAsync(
        Guid connectionId,
        Guid credentialId,
        CancellationToken cancellationToken);
    Task AddAsync(AdapterIngressCredential credential, CancellationToken cancellationToken);
    Task MarkAuthenticatedAsync(Guid credentialId, DateTimeOffset authenticatedAtUtc, CancellationToken cancellationToken);
}
