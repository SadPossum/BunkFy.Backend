namespace BunkFy.Modules.Reservations.Persistence;

using BunkFy.Modules.Reservations.Application.Commands;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationsPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ReservationsDbContext dbContext;
    private readonly Func<DbUpdateException, bool> isUniqueConstraintViolation;

    public ReservationsPersistenceRetryBehavior(ReservationsDbContext dbContext)
        : this(dbContext, EfDatabaseExceptionClassifier.IsUniqueConstraintViolation)
    {
    }

    internal ReservationsPersistenceRetryBehavior(
        ReservationsDbContext dbContext,
        Func<DbUpdateException, bool> isUniqueConstraintViolation)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.isUniqueConstraintViolation = isUniqueConstraintViolation ??
            throw new ArgumentNullException(nameof(isUniqueConstraintViolation));
    }

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        if (command is not ApplyReservationDataRightsCorrectionCommand and
            not ApplyReservationProcessingRestrictionCommand and
            not ReleaseReservationProcessingRestrictionCommand)
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
        catch (DbUpdateException exception) when (
            this.isUniqueConstraintViolation(exception))
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
    }
}
