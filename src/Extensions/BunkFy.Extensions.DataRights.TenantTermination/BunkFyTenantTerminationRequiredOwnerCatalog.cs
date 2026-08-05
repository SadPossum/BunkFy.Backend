namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Application.Production;

internal sealed class BunkFyTenantTerminationRequiredOwnerCatalog
    : ITenantTerminationRequiredOwnerCatalog
{
    public IReadOnlyCollection<string> RequiredOwnerKeys =>
        TenantTerminationProductionOwnerCatalog.RequiredOwnerKeys;
}
