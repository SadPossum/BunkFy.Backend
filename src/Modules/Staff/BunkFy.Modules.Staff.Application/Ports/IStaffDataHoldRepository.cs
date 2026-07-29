namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Pagination;

public interface IStaffDataHoldRepository
{
    Task<StaffDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffDataHold?> GetAsync(
        Guid staffMemberId,
        Guid holdId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StaffDataHold>> ListAsync(
        Guid staffMemberId,
        StaffDataHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    Task<long> CountAsync(
        Guid staffMemberId,
        StaffDataHoldStatus? status,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffDataHold hold,
        CancellationToken cancellationToken);

    Task AddReceiptAsync(
        StaffDataHoldReceipt receipt,
        CancellationToken cancellationToken);
}
