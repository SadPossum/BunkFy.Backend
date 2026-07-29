namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;

internal static class DataRightsExecutionScopeMappings
{
    public static DataRightsExecutionScope ToExecutionScope(
        this DataRightsCaseScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        DataRightsCaseScopeKind scopeKind = scope.PropertyId.HasValue
            ? DataRightsCaseScopeKind.Property
            : DataRightsCaseScopeKind.Tenant;
        return DataRightsExecutionScope.Create(
            (DataRightsCaseKind)scope.CaseType,
            scopeKind,
            scope.PropertyId).Value;
    }
}
