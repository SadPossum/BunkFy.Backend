namespace BunkFy.Modules.Workspaces.Domain;

public sealed class WorkspaceStaffAccessProfileSnapshot
{
    public const int AssignmentScopeMaxLength = 1024;

    private WorkspaceStaffAccessProfileSnapshot() { }

    internal WorkspaceStaffAccessProfileSnapshot(Guid profileId, string assignmentScope)
    {
        string normalizedScope = assignmentScope?.Trim() ?? string.Empty;
        if (profileId == Guid.Empty ||
            normalizedScope.Length is 0 or > AssignmentScopeMaxLength)
        {
            throw new ArgumentException("A profile id and assignment scope are required.");
        }

        this.ProfileId = profileId;
        this.AssignmentScope = normalizedScope;
    }

    public Guid ProfileId { get; private set; }
    public string AssignmentScope { get; private set; } = string.Empty;
}
