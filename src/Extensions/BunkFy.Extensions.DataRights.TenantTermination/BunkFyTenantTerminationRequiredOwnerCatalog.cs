namespace BunkFy.Extensions.DataRights.TenantTermination;

using BunkFy.Modules.DataRights.Contracts;

internal sealed class BunkFyTenantTerminationRequiredOwnerCatalog
    : ITenantTerminationRequiredOwnerCatalog
{
    public IReadOnlyCollection<string> RequiredOwnerKeys =>
        TenantTerminationProductionOwnerCatalog.RequiredOwnerKeys;
}
