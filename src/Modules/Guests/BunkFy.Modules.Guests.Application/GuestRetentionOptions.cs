namespace BunkFy.Modules.Guests.Application;

public sealed class GuestRetentionOptions
{
    public const string SectionName = "Guests:Retention";

    public int IntervalMinutes { get; set; } = 60;
    public int ScanSize { get; set; } = 100;
    public int MutationBatchSize { get; set; } = 25;

    internal TimeSpan Interval =>
        TimeSpan.FromMinutes(this.IntervalMinutes);
}
