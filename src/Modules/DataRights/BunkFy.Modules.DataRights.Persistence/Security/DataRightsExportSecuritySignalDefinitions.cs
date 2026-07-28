namespace BunkFy.Modules.DataRights.Persistence.Security;

using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Observability;

internal sealed class DataRightsExportSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition GenerationCompleted = new(
        "data-rights.export-generation-completed",
        SecuritySignalCategory.Privacy,
        SecuritySignalSeverity.Notice);

    public static readonly SecuritySignalDefinition GenerationFailed = new(
        "data-rights.export-generation-failed",
        SecuritySignalCategory.Privacy,
        SecuritySignalSeverity.Critical);

    public static readonly SecuritySignalDefinition DownloadCompleted = new(
        "data-rights.export-download-completed",
        SecuritySignalCategory.Privacy,
        SecuritySignalSeverity.Notice);

    private static readonly SecuritySignalDefinition[] All =
    [
        GenerationCompleted,
        GenerationFailed,
        DownloadCompleted
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;

    public static SecuritySignalDefinition? ForAudit(
        DataRightsExportAuditAction action,
        string outcomeCode) =>
        action switch
        {
            DataRightsExportAuditAction.GenerationCompleted =>
                GenerationCompleted,
            DataRightsExportAuditAction.GenerationFailed =>
                GenerationFailed,
            DataRightsExportAuditAction.Download
                when string.Equals(
                    outcomeCode,
                    "succeeded",
                    StringComparison.Ordinal) =>
                DownloadCompleted,
            _ => null
        };
}
