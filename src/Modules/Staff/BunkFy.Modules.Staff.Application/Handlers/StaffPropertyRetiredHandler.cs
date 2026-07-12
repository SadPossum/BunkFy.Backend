namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

[IntegrationEventHandler(StaffModuleMetadata.PropertyRetiredHandlerName)]
internal sealed class StaffPropertyRetiredHandler(IStaffPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyRetiredIntegrationEvent>
{
    public Task HandleAsync(PropertyRetiredIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(new(e.ScopeId, e.PropertyId, string.Empty, PropertyStatus.Retired,
            e.PropertyVersion), cancellationToken);
}
