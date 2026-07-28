namespace BunkFy.Extensions.Workspaces;

using Gma.Framework.Observability;

internal sealed class SupportAccessSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition Requested = new(
        "bunkfy.support-access.requested",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Notice);

    public static readonly SecuritySignalDefinition Granted = new(
        "bunkfy.support-access.granted",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition Denied = new(
        "bunkfy.support-access.denied",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition Revoked = new(
        "bunkfy.support-access.revoked",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Notice);

    private static readonly SecuritySignalDefinition[] All =
    [
        Requested,
        Granted,
        Denied,
        Revoked
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;
}
