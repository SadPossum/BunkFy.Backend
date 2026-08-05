namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class PropertiesPersistenceAdmissionBehavior<
    TCommand,
    TResponse>(PropertiesDbContext dbContext)
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
        catch (PropertiesOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    PropertiesOperationalAdmissionFailure.Restricted
                    ? PropertiesApplicationErrors
                        .WorkspaceProcessingRestricted
                    : PropertiesApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
