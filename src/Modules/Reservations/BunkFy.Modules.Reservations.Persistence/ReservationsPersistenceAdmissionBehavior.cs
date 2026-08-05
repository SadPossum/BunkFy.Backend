namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Reservations.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ReservationsPersistenceAdmissionBehavior<
    TCommand,
    TResponse>(ReservationsDbContext dbContext)
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
        catch (ReservationsOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    ReservationsOperationalAdmissionFailure.Restricted
                    ? ReservationsApplicationErrors
                        .WorkspaceProcessingRestricted
                    : ReservationsApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
