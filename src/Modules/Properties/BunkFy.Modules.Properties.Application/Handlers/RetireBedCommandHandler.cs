namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RetireBedCommandHandler : ICommandHandler<RetireBedCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(RetireBedCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Unit>(PropertiesDomainErrors.BedRetirementRequiresInventory));
}
