namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.Inventory.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class InventoryPersistenceAdmissionBehavior<
    TCommand,
    TResponse>(InventoryDbContext dbContext)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (InventoryOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    InventoryOperationalAdmissionFailure.Restricted
                    ? InventoryApplicationErrors
                        .WorkspaceProcessingRestricted
                    : InventoryApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
