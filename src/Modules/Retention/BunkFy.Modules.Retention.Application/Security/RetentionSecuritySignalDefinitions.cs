namespace BunkFy.Modules.Retention.Application.Security;

using Gma.Framework.Observability;

internal sealed class RetentionSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition ScheduledExecutionFailed = new(
        "retention.scheduled-execution-failed",
        SecuritySignalCategory.Retention,
        SecuritySignalSeverity.Critical);

    public static readonly SecuritySignalDefinition ScheduledExecutionTimedOut = new(
        "retention.scheduled-execution-timed-out",
        SecuritySignalCategory.Retention,
        SecuritySignalSeverity.Critical);

    private static readonly SecuritySignalDefinition[] All =
    [
        ScheduledExecutionFailed,
        ScheduledExecutionTimedOut
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;
}
