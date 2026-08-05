namespace BunkFy.DataGovernance;

public static class CountryPolicyCalendarDeadlineCalculator
{
    private const int MaximumInvalidTimeAdjustmentMinutes = 180;

    public static bool TryCalculate(
        DateTimeOffset receivedAtUtc,
        string timeZoneId,
        CountryPolicyCalendarPeriod period,
        out DateTimeOffset dueAtUtc)
    {
        dueAtUtc = default;
        if (receivedAtUtc == default ||
            string.IsNullOrWhiteSpace(timeZoneId) ||
            !IsValid(period))
        {
            return false;
        }

        try
        {
            TimeZoneInfo timeZone =
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime receivedLocal = DateTime.SpecifyKind(
                TimeZoneInfo.ConvertTime(receivedAtUtc, timeZone).DateTime,
                DateTimeKind.Unspecified);
            DateTime dueLocal = receivedLocal
                .AddYears(period.Years)
                .AddMonths(period.Months)
                .AddDays(period.Days);

            for (int minute = 0;
                 minute <= MaximumInvalidTimeAdjustmentMinutes &&
                 timeZone.IsInvalidTime(dueLocal);
                 minute++)
            {
                dueLocal = dueLocal.AddMinutes(1);
            }

            if (timeZone.IsInvalidTime(dueLocal))
            {
                return false;
            }

            DateTime dueUtc = timeZone.IsAmbiguousTime(dueLocal)
                ? ResolveLatestAmbiguousInstant(dueLocal, timeZone)
                : TimeZoneInfo.ConvertTimeToUtc(dueLocal, timeZone);
            dueAtUtc = new DateTimeOffset(dueUtc, TimeSpan.Zero);
            return dueAtUtc > receivedAtUtc;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    internal static bool IsValid(CountryPolicyCalendarPeriod? period) =>
        period is not null &&
        period.Years is >= 0 and <= 10 &&
        period.Months is >= 0 and <= 11 &&
        period.Days is >= 0 and <= 366 &&
        (period.Years > 0 || period.Months > 0 || period.Days > 0);

    private static DateTime ResolveLatestAmbiguousInstant(
        DateTime local,
        TimeZoneInfo timeZone) =>
        timeZone.GetAmbiguousTimeOffsets(local)
            .Max(offset => new DateTimeOffset(local, offset).UtcDateTime);
}
