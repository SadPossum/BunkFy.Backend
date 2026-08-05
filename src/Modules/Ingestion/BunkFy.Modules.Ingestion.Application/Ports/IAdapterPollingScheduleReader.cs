namespace BunkFy.Modules.Ingestion.Application.Ports;

using System.Runtime.CompilerServices;

public interface IAdapterPollingScheduleReader
{
    Task<IReadOnlyList<AdapterPollingScheduleDefinition>> ListActiveAsync(CancellationToken cancellationToken);

    async IAsyncEnumerable<AdapterPollingScheduleDefinition> StreamActiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<AdapterPollingScheduleDefinition> schedules =
            await this.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        foreach (AdapterPollingScheduleDefinition schedule in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return schedule;
        }
    }
}

public sealed record AdapterPollingScheduleDefinition(
    string ScopeId,
    Guid ConnectionId,
    int IntervalSeconds,
    int MaxAttempts);
