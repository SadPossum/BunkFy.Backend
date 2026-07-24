namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Commands;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly DataRightsDbContext dbContext;
    private readonly Func<DbUpdateException, bool> isRetryableUniqueViolation;

    public DataRightsPersistenceRetryBehavior(DataRightsDbContext dbContext)
        : this(dbContext, EfDatabaseExceptionClassifier.IsUniqueConstraintViolation)
    {
    }

    internal DataRightsPersistenceRetryBehavior(
        DataRightsDbContext dbContext,
        Func<DbUpdateException, bool> isRetryableUniqueViolation)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.isRetryableUniqueViolation = isRetryableUniqueViolation ??
            throw new ArgumentNullException(nameof(isRetryableUniqueViolation));
    }

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        if (command is not StartDataRightsAnonymisationExecutionCommand)
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
