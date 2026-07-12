namespace BunkFy.Modules.Ingestion.Application;

using System.Security.Cryptography;
using System.Text;

internal static class ObservationIdentity
{
    public static string CreateDeduplicationKey(
        string recordType,
        string externalRecordId,
        string? sourceRevision,
        string contentSha256)
    {
        string revisionIdentity = string.IsNullOrWhiteSpace(sourceRevision)
            ? $"hash:{contentSha256.Trim().ToLowerInvariant()}"
            : $"revision:{sourceRevision.Trim()}";
        string identity = $"{recordType.Trim().ToLowerInvariant()}\n{externalRecordId.Trim()}\n{revisionIdentity}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    public static Guid CreateReceiptId(string scopeId, Guid connectionId, string deduplicationKey)
    {
        string identity = $"{scopeId.Trim()}\n{connectionId:N}\n{deduplicationKey}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static Guid CreateReprocessingOperationId(
        Guid sourceReceiptId,
        string parserType,
        int parserVersion,
        int outputIndex,
        string recordType,
        string externalRecordId,
        string? sourceRevision,
        string contentSha256)
    {
        string identity = $"{sourceReceiptId:N}\n{parserType.Trim().ToLowerInvariant()}\n{parserVersion}\n" +
                          $"{outputIndex}\n{recordType.Trim().ToLowerInvariant()}\n{externalRecordId.Trim()}\n" +
                          $"{sourceRevision?.Trim()}\n{contentSha256.Trim().ToLowerInvariant()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new Guid(hash.AsSpan(0, 16));
    }
}
