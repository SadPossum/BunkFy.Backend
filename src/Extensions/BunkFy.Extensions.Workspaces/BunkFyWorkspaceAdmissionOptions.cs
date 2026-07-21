namespace BunkFy.Extensions.Workspaces;

public sealed class BunkFyWorkspaceAdmissionOptions
{
    public const string SectionName = "BunkFy:WorkspaceAdmission";

    public BunkFyAccountRegistrationMode AccountRegistration { get; set; }

    public BunkFyWorkspaceCreationMode WorkspaceCreation { get; set; }

    public bool RequireVerifiedEmailForWorkspaceCreation { get; set; } = true;
}

public enum BunkFyAccountRegistrationMode
{
    Unspecified = 0,
    Public = 1,
    Disabled = 2
}

public enum BunkFyWorkspaceCreationMode
{
    Unspecified = 0,
    SelfService = 1,
    Disabled = 2
}
