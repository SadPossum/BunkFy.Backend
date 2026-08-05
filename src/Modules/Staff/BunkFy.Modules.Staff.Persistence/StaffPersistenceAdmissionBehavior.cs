namespace BunkFy.Modules.Staff.Persistence;

using BunkFy.Modules.Staff.Application;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StaffPersistenceAdmissionBehavior<TCommand, TResponse>(
    StaffDbContext dbContext)
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
        catch (StaffOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    StaffOperationalAdmissionFailure.Restricted
                    ? StaffApplicationErrors.WorkspaceProcessingRestricted
                    : StaffApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
