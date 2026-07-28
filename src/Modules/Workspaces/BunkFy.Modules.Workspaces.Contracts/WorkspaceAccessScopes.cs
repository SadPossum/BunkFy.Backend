namespace BunkFy.Modules.Workspaces.Contracts;

using Gma.Framework.AccessControl;

public static class WorkspaceAccessScopes
{
    public const string SegmentName = "tenant";
    public const string PropertySegmentName = "property";

    public static AccessScope Create(string workspaceId) =>
        AccessScope.Create(AccessScopeSegment.Create(SegmentName, workspaceId));

    public static AccessScope CreateProperty(string workspaceId, Guid propertyId) =>
        AccessScope.Create(
            AccessScopeSegment.Create(SegmentName, workspaceId),
            AccessScopeSegment.Create(PropertySegmentName, propertyId.ToString("D")));

    public static bool IsWorkspaceScope(AccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Segments.Count == 1 &&
               string.Equals(scope.Segments[0].Name, SegmentName, StringComparison.Ordinal);
    }

    public static bool IsWorkspaceOrPropertyScope(AccessScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (IsWorkspaceScope(scope))
        {
            return true;
        }

        return scope.Segments.Count == 2 &&
            string.Equals(scope.Segments[0].Name, SegmentName, StringComparison.Ordinal) &&
            IsPropertySegment(scope.Segments[1]);
    }

    public static bool IsWorkspaceOrPropertyScope(AccessScope ownerScope, AccessScope assignmentScope)
    {
        ArgumentNullException.ThrowIfNull(ownerScope);
        ArgumentNullException.ThrowIfNull(assignmentScope);
        if (!IsWorkspaceScope(ownerScope) || assignmentScope.Segments.Count is < 1 or > 2 ||
            !string.Equals(
                assignmentScope.Segments[0].Name,
                SegmentName,
                StringComparison.Ordinal) ||
            !string.Equals(
                assignmentScope.Segments[0].Value,
                ownerScope.Segments[0].Value,
                StringComparison.Ordinal))
        {
            return false;
        }

        return assignmentScope.Segments.Count == 1 ||
            IsPropertySegment(assignmentScope.Segments[1]);
    }

    private static bool IsPropertySegment(AccessScopeSegment segment) =>
        string.Equals(segment.Name, PropertySegmentName, StringComparison.Ordinal) &&
        Guid.TryParseExact(segment.Value, "D", out Guid propertyId) &&
        propertyId != Guid.Empty;
}
