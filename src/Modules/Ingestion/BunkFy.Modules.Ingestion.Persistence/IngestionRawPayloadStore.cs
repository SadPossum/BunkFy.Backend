namespace BunkFy.Modules.Ingestion.Persistence;

using System.Security.Cryptography;
using System.Text;
using Gma.Framework.FileManagement;
using BunkFy.Modules.Ingestion.Application.Ports;

internal sealed class IngestionRawPayloadStore(IFileStorage storage) : IRawPayloadStore
{
    private const int MaximumPayloadBytes = 4 * 1024 * 1024;

    public async Task StoreAsync(RawPayloadWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        FileStorageObjectKey key = CreateKey(write.PayloadId, write.ScopeId, write.ConnectionId);
        await using MemoryStream content = new(write.Content.ToArray(), writable: false);
        FileStorageObjectProperties stored = await storage.PutAsync(
            new FileStorageWriteRequest(
                key,
                content,
                content.Length,
                write.ContentType,
                fileName: null,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["module"] = "ingestion",
                    ["content-sha256"] = write.ContentSha256
                }),
            cancellationToken).ConfigureAwait(false);

        if (stored.ContentLength != content.Length)
        {
            throw new IOException("Stored ingestion payload length did not match the submitted payload length.");
        }
    }

    public async Task<RawPayloadRead?> ReadAsync(
        Guid payloadId,
        string scopeId,
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        FileStorageReadResult? stored = await storage.OpenReadAsync(
            CreateKey(payloadId, scopeId, connectionId),
            cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        if (stored.Properties.ContentLength is < 0 or > MaximumPayloadBytes)
        {
            throw new IOException("The ingestion payload exceeds the supported read limit.");
        }

        using MemoryStream content = new((int)stored.Properties.ContentLength);
        await stored.CopyToAsync(content, cancellationToken).ConfigureAwait(false);
        if (content.Length != stored.Properties.ContentLength)
        {
            throw new IOException("The ingestion payload length changed while reading.");
        }

        byte[] bytes = content.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (stored.Properties.Metadata.TryGetValue("content-sha256", out string? declaredHash) &&
            !string.Equals(hash, declaredHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The ingestion payload hash did not match stored metadata.");
        }

        return new RawPayloadRead(stored.Properties.ContentType, bytes, hash);
    }

    public Task<bool> DeleteAsync(
        Guid payloadId,
        string scopeId,
        Guid connectionId,
        CancellationToken cancellationToken) => storage.DeleteAsync(
        CreateKey(payloadId, scopeId, connectionId),
        cancellationToken);

    private static FileStorageObjectKey CreateKey(Guid payloadId, string scopeId, Guid connectionId)
    {
        if (payloadId == Guid.Empty || connectionId == Guid.Empty || string.IsNullOrWhiteSpace(scopeId))
        {
            throw new ArgumentException("Raw payload identity is incomplete.", nameof(payloadId));
        }

        byte[] scopeHash = SHA256.HashData(Encoding.UTF8.GetBytes(scopeId.Trim()));
        string scopeSegment = Convert.ToHexString(scopeHash).ToLowerInvariant()[..16];
        return new FileStorageObjectKey(
            $"ingestion/scope-{scopeSegment}/connection-{connectionId:N}/payload-{payloadId:N}");
    }
}
