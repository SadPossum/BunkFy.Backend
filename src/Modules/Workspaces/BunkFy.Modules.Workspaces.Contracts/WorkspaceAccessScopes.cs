namespace BunkFy.Modules.Workspaces.Contracts;

using Gma.Framework.AccessControl;

public static class WorkspaceAccessScopes
{
    public const string SegmentName = "tenant";

    public static AccessScope Create(string workspaceId) =>
        AccessScope.Create(AccessScopeSegment.Create(SegmentName, workspaceId));

    public static bool IsWorkspaceScope(AccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Segments.Count == 1 &&
               string.Equals(scope.Segments[0].Name, SegmentName, StringComparison.Ordinal);
    }
}
