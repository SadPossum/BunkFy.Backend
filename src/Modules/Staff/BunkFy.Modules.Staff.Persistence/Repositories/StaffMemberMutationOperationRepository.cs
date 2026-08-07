namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.Staff.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffMemberMutationOperationRepository(
    StaffDbContext dbContext) : IStaffMemberMutationOperationRepository
{
    public async Task<StaffMemberMutationOperationRecord?> GetAsync(
        Guid staffMemberId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        StaffMemberMutationOperation? operation = await dbContext
            .MemberMutationOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.StaffMemberId == staffMemberId &&
                    item.Id == operationId,
                cancellationToken).ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        StaffMemberMutationOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.MemberMutationOperations.Add(
            new StaffMemberMutationOperation(operation));
        return Task.CompletedTask;
    }

    public async Task DeleteForStaffMemberAsync(
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        _ = await dbContext.MemberMutationOperations
            .Where(operation =>
                operation.StaffMemberId == staffMemberId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
