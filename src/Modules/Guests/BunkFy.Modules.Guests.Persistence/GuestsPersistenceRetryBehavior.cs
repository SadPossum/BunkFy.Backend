namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Application.Commands;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal sealed class GuestsPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly GuestsDbContext dbContext;
    private readonly Func<DbUpdateException, bool> isRetryableUniqueViolation;

    public GuestsPersistenceRetryBehavior(GuestsDbContext dbContext)
        : this(dbContext, GuestsUniqueConstraintDetector.IsUniqueViolation)
    {
    }

    internal GuestsPersistenceRetryBehavior(
        GuestsDbContext dbContext,
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

        if (command is not (
            ApplyGuestDataRightsCorrectionCommand or
            ApplyGuestProcessingRestrictionCommand or
            ReleaseGuestProcessingRestrictionCommand))
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
        catch (DbUpdateException exception) when (this.isRetryableUniqueViolation(exception))
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
    }
}

internal static class GuestsUniqueConstraintDetector
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
