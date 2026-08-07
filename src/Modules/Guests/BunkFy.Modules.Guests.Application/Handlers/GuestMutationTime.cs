namespace BunkFy.Modules.Guests.Application.Handlers;

internal static class GuestMutationTime
{
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        DateTimeOffset utc = value.ToUniversalTime();
        return new(
            utc.Ticks - (utc.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}
