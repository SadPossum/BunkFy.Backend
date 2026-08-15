namespace BunkFy.TimeZones;

using NodaTime;
using NodaTime.TimeZones;

/// <summary>
/// Performs civil-calendar calculations against the pinned embedded TZDB
/// catalog, independently of the serving operating system's time-zone data.
/// </summary>
public static class TimeZoneCalendarMath
{
    public const int MaximumSkippedLocalTimeAdjustmentMinutes = 180;
    private const int MaximumSkippedLocalTimeAdjustmentSeconds =
        MaximumSkippedLocalTimeAdjustmentMinutes * 60;

    /// <summary>
    /// Adds a Gregorian calendar period in a primary TZDB zone. Ambiguous local
    /// times resolve to their latest instant; skipped local times resolve to the
    /// first valid instant when the gap is no longer than three hours.
    /// </summary>
    public static bool TryAddCalendarPeriod(
        DateTimeOffset receivedAtUtc,
        string canonicalTimeZoneId,
        int years,
        int months,
        int days,
        out DateTimeOffset dueAtUtc)
    {
        dueAtUtc = default;
        if (receivedAtUtc == default ||
            years < 0 ||
            months < 0 ||
            days < 0 ||
            !TimeZoneCatalog.Default.TryResolve(
                canonicalTimeZoneId,
                out TimeZoneCatalogResolution? resolution) ||
            resolution.Kind != TimeZoneCatalogResolutionKind.Canonical)
        {
            return false;
        }

        try
        {
            DateTimeZone zone = DateTimeZoneProviders.Tzdb[
                resolution.CanonicalTimeZoneId];
            Instant received = Instant.FromDateTimeOffset(receivedAtUtc);
            LocalDateTime receivedLocal = received.InZone(zone).LocalDateTime;
            LocalDateTime dueLocal = receivedLocal
                .PlusYears(years)
                .PlusMonths(months)
                .PlusDays(days);
            ZoneLocalMapping mapping = zone.MapLocal(dueLocal);
            Instant due;
            if (mapping.Count == 0)
            {
                int skippedSeconds =
                    mapping.LateInterval.WallOffset.Seconds -
                    mapping.EarlyInterval.WallOffset.Seconds;
                if (skippedSeconds is <= 0 or
                    > MaximumSkippedLocalTimeAdjustmentSeconds)
                {
                    return false;
                }

                due = mapping.LateInterval.Start;
            }
            else
            {
                due = mapping.Last().ToInstant();
            }

            dueAtUtc = due.ToDateTimeOffset();
            return dueAtUtc > receivedAtUtc;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            OverflowException or
            InvalidOperationException)
        {
            dueAtUtc = default;
            return false;
        }
    }
}
