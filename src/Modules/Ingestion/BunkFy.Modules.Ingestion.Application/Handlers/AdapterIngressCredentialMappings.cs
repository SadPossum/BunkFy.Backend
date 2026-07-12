namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Credentials;

internal static class AdapterIngressCredentialMappings
{
    public static AdapterIngressCredentialDto Map(AdapterIngressCredential credential) => new(
        credential.Id,
        credential.ConnectionId,
        credential.Slot,
        credential.Label,
        (AdapterIngressCredentialStatus)(int)credential.State,
        credential.ExpiresAtUtc,
        credential.CreatedBy,
        credential.CreatedAtUtc,
        credential.RevokedBy,
        credential.RevokedAtUtc,
        credential.LastAuthenticatedAtUtc,
        credential.Version);
}
