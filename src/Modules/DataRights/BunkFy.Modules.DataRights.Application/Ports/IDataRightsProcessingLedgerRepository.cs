namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Entities;

public interface IDataRightsProcessingLedgerRepository
{
    Task AddAsync(
        DataRightsProcessingLedgerEntry entry,
        CancellationToken cancellationToken);

    Task<DataRightsProcessingLedgerEntry?> GetByWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken);

    Task<DataRightsProcessingLedgerEntry?> GetByOwnerReceiptAsync(
        string ownerKey,
        Guid ownerReceiptId,
        CancellationToken cancellationToken);

    Task<DataRightsProcessingLedgerEntry?> GetLatestAsync(
        CancellationToken cancellationToken);
}
