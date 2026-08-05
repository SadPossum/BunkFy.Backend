namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class IngestionPersistenceAdmissionBehavior<
    TCommand,
    TResponse>(IngestionDbContext dbContext)
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
        catch (IngestionOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    IngestionOperationalAdmissionFailure.Restricted
                    ? IngestionApplicationErrors.TenantLifecycleRestricted
                    : IngestionApplicationErrors
                        .TenantLifecycleAdmissionUnavailable);
        }
    }
}
