namespace BunkFy.Modules.Staff.Application.Handlers;

internal static class StaffMutationTime
{
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
