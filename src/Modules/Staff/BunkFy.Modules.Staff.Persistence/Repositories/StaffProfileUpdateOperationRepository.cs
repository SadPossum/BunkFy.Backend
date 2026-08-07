namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffProfileUpdateOperationRepository(
    StaffDbContext dbContext) : IStaffProfileUpdateOperationRepository
{
    public async Task<StaffProfileUpdateOperationRecord?> GetAsync(
        Guid staffMemberId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        StaffProfileUpdateOperation? operation = await dbContext
            .ProfileUpdateOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.StaffMemberId == staffMemberId &&
                    item.Id == operationId,
                cancellationToken).ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        StaffProfileUpdateOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.ProfileUpdateOperations.Add(
            new StaffProfileUpdateOperation(operation));
        return Task.CompletedTask;
    }

    public async Task DeleteForStaffMemberAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        _ = await dbContext.ProfileUpdateOperations
            .Where(operation =>
                operation.StaffMemberId == staffMemberId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
