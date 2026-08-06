namespace BunkFy.Modules.Staff.Persistence;

using BunkFy.Modules.Staff.Application.Ports;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffPersistenceRetryBehavior<TCommand, TResponse>
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly StaffDbContext dbContext;
    private readonly Func<DbUpdateException, bool> isUniqueViolation;

    public StaffPersistenceRetryBehavior(StaffDbContext dbContext)
        : this(
            dbContext,
            EfDatabaseExceptionClassifier.IsUniqueConstraintViolation)
    {
    }

    internal StaffPersistenceRetryBehavior(
        StaffDbContext dbContext,
        Func<DbUpdateException, bool> isUniqueViolation)
    {
        this.dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
        this.isUniqueViolation = isUniqueViolation ??
            throw new ArgumentNullException(nameof(isUniqueViolation));
    }

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CommandNext<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(next);

        if (command is not IStaffPersistenceRetryableCommand)
        {
            return await next().ConfigureAwait(false);
        }

        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (DbUpdateException exception)
            when (this.isUniqueViolation(exception))
        {
            this.dbContext.ChangeTracker.Clear();
            return await next().ConfigureAwait(false);
        }
    }
}
