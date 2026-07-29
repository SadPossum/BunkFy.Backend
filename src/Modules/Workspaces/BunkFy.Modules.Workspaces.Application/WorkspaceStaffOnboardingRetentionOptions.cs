namespace BunkFy.Modules.Workspaces.Application;

public sealed class WorkspaceStaffOnboardingRetentionOptions
{
    public const string SectionName = "Workspaces:StaffOnboardingRetention";

    public int GracePeriodHours { get; set; } = 2;
    public int AuthorityWindowHours { get; set; } = 20;
    public int IntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 50;

    internal TimeSpan GracePeriod => TimeSpan.FromHours(this.GracePeriodHours);
    internal TimeSpan AuthorityWindow => TimeSpan.FromHours(this.AuthorityWindowHours);
    internal TimeSpan Interval => TimeSpan.FromMinutes(this.IntervalMinutes);
}
