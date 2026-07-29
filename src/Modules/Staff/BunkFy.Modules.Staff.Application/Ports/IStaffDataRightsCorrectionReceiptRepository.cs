namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Domain.DataRights;

public interface IStaffDataRightsCorrectionReceiptRepository
{
    Task<StaffDataRightsCorrectionReceipt?> FindByExecutionIdAsync(
        Guid executionId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffDataRightsCorrectionReceipt receipt,
        CancellationToken cancellationToken);
}
