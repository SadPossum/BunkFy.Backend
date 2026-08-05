namespace BunkFy.Modules.DataRights.Application.Ports;

using System.Runtime.CompilerServices;
using BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsResponseDeadlineAlertRepository
{
    Task<DataRightsResponseDeadlineAlertClaimResult> ClaimAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset dueSoonUntilUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(
        CancellationToken cancellationToken);

    async IAsyncEnumerable<string> StreamScheduleScopeIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<string> scopeIds =
            await this.ListScheduleScopeIdsAsync(cancellationToken)
                .ConfigureAwait(false);
        foreach (string scopeId in scopeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return scopeId;
        }
    }
}

public sealed record DataRightsResponseDeadlineAlertDispatch(
    Guid DispatchId,
    string ScopeId,
    Guid CaseId,
    Guid PropertyId,
    DataRightsResponseDeadlineAlertKind AlertKind,
    DateTimeOffset DueAtUtc);

public sealed record DataRightsResponseDeadlineAlertClaimResult(
    int ProcessedCount,
    IReadOnlyList<DataRightsResponseDeadlineAlertDispatch> Dispatches);
