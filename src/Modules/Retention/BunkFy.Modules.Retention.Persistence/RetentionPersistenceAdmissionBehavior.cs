namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.Retention.Application.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RetentionPersistenceAdmissionBehavior<
    TCommand,
    TResponse>(RetentionDbContext dbContext)
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
        catch (RetentionOperationalAdmissionException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Result.Failure<TResponse>(
                exception.Failure ==
                    RetentionOperationalAdmissionFailure.Restricted
                    ? RetentionApplicationErrors
                        .WorkspaceProcessingRestricted
                    : RetentionApplicationErrors
                        .WorkspaceProcessingAdmissionUnavailable);
        }
    }
}
