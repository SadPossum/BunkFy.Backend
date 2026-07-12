namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IIngestionRetentionPolicy
{
    DateTimeOffset GetRawPayloadRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset receivedAtUtc);

    DateTimeOffset GetSensitiveHistoryRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset terminalAtUtc);
}
