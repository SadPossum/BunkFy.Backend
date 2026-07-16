namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RetireRoomCommandHandler : ICommandHandler<RetireRoomCommand, Unit>
{
    public Task<Result<Unit>> HandleAsync(RetireRoomCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<Unit>(PropertiesDomainErrors.RoomRetirementRequiresInventory));
}
