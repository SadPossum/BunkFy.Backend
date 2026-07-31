namespace BunkFy.Modules.Ingestion.Contracts;

public interface IIngestionNotificationSourceLinkResolver
{
    Task<IngestionNotificationSourceLink?> ResolveAsync(
        string scopeId,
        Guid propertyId,
        Guid connectionId,
        Guid operationId,
        Guid receiptId,
        CancellationToken cancellationToken);
}

public sealed record IngestionNotificationSourceLink(
    Guid SourceLinkId);
