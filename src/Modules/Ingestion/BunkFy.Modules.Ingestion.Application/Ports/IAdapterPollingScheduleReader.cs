namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IAdapterPollingScheduleReader
{
    Task<IReadOnlyList<AdapterPollingScheduleDefinition>> ListActiveAsync(CancellationToken cancellationToken);
}

public sealed record AdapterPollingScheduleDefinition(
    string ScopeId,
    Guid ConnectionId,
    int IntervalSeconds,
    int MaxAttempts);
