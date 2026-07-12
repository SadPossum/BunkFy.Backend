namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Messaging;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;

[IntegrationEventHandler(StaffModuleMetadata.PropertyUpdatedHandlerName)]
internal sealed class StaffPropertyUpdatedHandler(IStaffPropertyProjectionRepository properties)
    : IIntegrationEventHandler<PropertyUpdatedIntegrationEvent>
{
    public Task HandleAsync(PropertyUpdatedIntegrationEvent e, CancellationToken cancellationToken) =>
        properties.ApplyAsync(new(e.ScopeId, e.PropertyId, e.Name, e.Status, e.PropertyVersion), cancellationToken);
}
