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
        NormalizeTimestamp(credential.ExpiresAtUtc),
        credential.CreatedBy,
        NormalizeTimestamp(credential.CreatedAtUtc),
        credential.RevokedBy,
        NormalizeTimestamp(credential.RevokedAtUtc),
        NormalizeTimestamp(credential.LastAuthenticatedAtUtc),
        credential.Version,
        credential.AdapterType,
        credential.AdapterProtocolVersion,
        credential.ConfigurationSchemaVersion,
        credential.SourceSystem);

    public static AdapterIngressCredentialMutationReceiptDto MapReceipt(AdapterIngressCredential credential) => new(
        credential.Id,
        credential.ConnectionId,
        (AdapterIngressCredentialStatus)(int)credential.State,
        credential.Version);

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }

    private static DateTimeOffset? NormalizeTimestamp(DateTimeOffset? value) =>
        value.HasValue ? NormalizeTimestamp(value.Value) : null;
}
