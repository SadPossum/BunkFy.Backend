namespace BunkFy.Extensions.Workspaces;

public sealed class BunkFySupportAccessOptions
{
    public const string SectionName = "BunkFy:SupportAccess";

    public int MaximumGrantMinutes { get; set; } = 480;
}
