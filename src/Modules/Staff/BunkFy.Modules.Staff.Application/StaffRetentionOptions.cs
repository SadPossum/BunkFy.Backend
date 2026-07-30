namespace BunkFy.Modules.Staff.Application;

public sealed class StaffRetentionOptions
{
    public const string SectionName = "Staff:Retention";

    public int IntervalMinutes { get; set; } = 60;
    public int ScanSize { get; set; } = 100;
    public int MutationBatchSize { get; set; } = 25;

    internal TimeSpan Interval =>
        TimeSpan.FromMinutes(this.IntervalMinutes);
}
