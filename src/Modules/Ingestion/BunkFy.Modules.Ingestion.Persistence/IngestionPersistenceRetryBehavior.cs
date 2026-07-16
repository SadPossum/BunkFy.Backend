namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Application.Commands;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal sealed class IngestionPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly IngestionDbContext dbContext;
    private readonly Func<DbUpdateException, bool> isRetryableUniqueViolation;

    public IngestionPersistenceRetryBehavior(IngestionDbContext dbContext)
        : this(dbContext, IngestionUniqueConstraintDetector.IsUniqueViolation)
    {
    }

    internal IngestionPersistenceRetryBehavior(
        IngestionDbContext dbContext,
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

        if (command is not ClaimRemoteAdapterLeaseCommand)
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
        catch (DbUpdateException exception) when (this.isRetryableUniqueViolation(exception))
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
    }
}

internal static class IngestionUniqueConstraintDetector
{
    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                IsUniqueViolation(sqlServerErrorNumber: null, postgres.SqlState))
            {
                return true;
            }

            if (current is SqlException sqlServer &&
                IsUniqueViolation(sqlServer.Number, postgreSqlState: null))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsUniqueViolation(int? sqlServerErrorNumber, string? postgreSqlState) =>
        sqlServerErrorNumber is 2601 or 2627 ||
        string.Equals(postgreSqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal);
}
