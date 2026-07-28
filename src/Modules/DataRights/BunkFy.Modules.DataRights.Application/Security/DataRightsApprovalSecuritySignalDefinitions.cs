namespace BunkFy.Modules.DataRights.Application.Security;

using Gma.Framework.Observability;

internal sealed class DataRightsApprovalSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition OperationApprovalDenied = new(
        "data-rights.operation-approval-denied",
        SecuritySignalCategory.Privacy,
        SecuritySignalSeverity.Warning);

    private static readonly SecuritySignalDefinition[] All =
    [
        OperationApprovalDenied
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;
}
