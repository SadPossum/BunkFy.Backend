namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Ports;
using Microsoft.EntityFrameworkCore;

internal sealed class PropertyMutationOperationRepository(
    PropertiesDbContext dbContext)
    : IPropertyMutationOperationRepository
{
    public async Task<PropertyMutationOperationRecord?> GetAsync(
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        PropertyMutationOperation? operation = await dbContext
            .PropertyMutationOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ResourceKind == resourceKind &&
                    item.ResourceId == resourceId &&
                    item.Id == operationId,
                cancellationToken).ConfigureAwait(false);
        return operation?.ToRecord();
    }

    public Task AddAsync(
        PropertyMutationOperationRecord operation,
        CancellationToken cancellationToken)
    {
        dbContext.PropertyMutationOperations.Add(
            new PropertyMutationOperation(operation));
        return Task.CompletedTask;
    }
}
