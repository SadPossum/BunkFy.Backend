namespace BunkFy.Modules.Workspaces.Domain;

public sealed class WorkspaceStaffAccessProfileSnapshot
{
    private WorkspaceStaffAccessProfileSnapshot() { }

    internal WorkspaceStaffAccessProfileSnapshot(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }

        this.ProfileId = profileId;
    }

    public Guid ProfileId { get; private set; }
}
