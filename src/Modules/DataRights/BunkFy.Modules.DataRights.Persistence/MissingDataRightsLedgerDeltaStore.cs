namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;

internal sealed class MissingDataRightsLedgerDeltaStore
    : IDataRightsLedgerDeltaStore
{
    internal const string FailureCode =
        "data-rights.ledger-delta.provider-unavailable";
    private const string ProviderName = "unavailable";

    public Task<DataRightsLedgerDeltaStoreReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(new DataRightsLedgerDeltaStoreReadiness(
            ProviderName,
            IsReady: false,
            IsProductionGrade: false,
            FailureCode));

    public Task<DataRightsLedgerDeltaAppendReceipt> AppendAsync(
        DataRightsLedgerDelta delta,
        CancellationToken cancellationToken) =>
        Task.FromException<DataRightsLedgerDeltaAppendReceipt>(Unavailable());

    public Task<DataRightsLedgerDeltaCheckpoint>
        ReadTrustedCheckpointAsync(
            string scopeId,
            CancellationToken cancellationToken) =>
        Task.FromException<DataRightsLedgerDeltaCheckpoint>(Unavailable());

    public Task<DataRightsLedgerDeltaPage> ReadAfterAsync(
        string scopeId,
        DataRightsLedgerDeltaCursor cursor,
        int pageSize,
        CancellationToken cancellationToken) =>
        Task.FromException<DataRightsLedgerDeltaPage>(Unavailable());

    private static DataRightsLedgerDeltaStoreException Unavailable() =>
        new(
            FailureCode,
            "The configured data-rights ledger delta provider is unavailable.");
}
