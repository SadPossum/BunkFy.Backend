namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GuestsPersistenceAdmissionBehavior<TCommand, TResponse>(
    GuestsDbContext dbContext)
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
        catch (GuestsOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    GuestsOperationalAdmissionFailure.Restricted
                    ? GuestsApplicationErrors.WorkspaceProcessingRestricted
                    : GuestsApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
