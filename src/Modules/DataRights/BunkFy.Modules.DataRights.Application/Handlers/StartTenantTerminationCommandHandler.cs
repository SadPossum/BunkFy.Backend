namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StartTenantTerminationCommandHandler(
    TenantTerminationStartCoordinator coordinator)
    : ICommandHandler<StartTenantTerminationCommand,
        TenantTerminationStartDto>
{
    public Task<Result<TenantTerminationStartDto>> HandleAsync(
        StartTenantTerminationCommand command,
        CancellationToken cancellationToken) =>
        coordinator.StartAsync(command, cancellationToken);
}
