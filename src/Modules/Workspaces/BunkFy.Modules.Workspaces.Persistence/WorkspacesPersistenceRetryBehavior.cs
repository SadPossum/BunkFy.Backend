namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Application.Ports;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspacesPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly WorkspacesDbContext dbContext;
    private readonly Func<DbUpdateException, bool>
        isRetryableUniqueViolation;

    public WorkspacesPersistenceRetryBehavior(
        WorkspacesDbContext dbContext)
        : this(
            dbContext,
            EfDatabaseExceptionClassifier.IsUniqueConstraintViolation)
    {
    }

    internal WorkspacesPersistenceRetryBehavior(
        WorkspacesDbContext dbContext,
        Func<DbUpdateException, bool> isRetryableUniqueViolation)
    {
        this.dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
        this.isRetryableUniqueViolation =
            isRetryableUniqueViolation ??
            throw new ArgumentNullException(
                nameof(isRetryableUniqueViolation));
    }

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        if (command is not IWorkspacePersistenceRetryableCommand)
        {
            return await next().ConfigureAwait(false);
        }

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (OptimisticConcurrencyException)
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (this.isRetryableUniqueViolation(exception))
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
    }
}
