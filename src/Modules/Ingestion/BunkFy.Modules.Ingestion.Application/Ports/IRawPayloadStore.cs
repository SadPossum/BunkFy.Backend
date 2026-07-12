namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IRawPayloadStore
{
    Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken);
    Task<RawPayloadRead?> ReadAsync(
        Guid payloadId,
        string scopeId,
        Guid connectionId,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(
        Guid payloadId,
        string scopeId,
        Guid connectionId,
        CancellationToken cancellationToken);
}

public sealed record RawPayloadWrite(
    Guid PayloadId,
    string ScopeId,
    Guid ConnectionId,
    string ContentType,
    ReadOnlyMemory<byte> Content,
    string ContentSha256);

public sealed record RawPayloadRead(
    string ContentType,
    ReadOnlyMemory<byte> Content,
    string ContentSha256);
