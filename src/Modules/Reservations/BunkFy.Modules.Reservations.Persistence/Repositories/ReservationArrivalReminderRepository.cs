namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Runtime.Identity;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationArrivalReminderRepository(
    ReservationsDbContext dbContext,
    IIdGenerator idGenerator)
    : IReservationArrivalReminderRepository
{
    public async Task ApplyPropertyAsync(
        ReservationReminderPropertyWriteModel property,
        CancellationToken cancellationToken)
    {
        ReservationPropertyProjection? projection = dbContext.PropertyProjections.Local.FirstOrDefault(
            item => item.Id == property.PropertyId && item.ScopeId == property.ScopeId) ??
            await dbContext.PropertyProjections.FirstOrDefaultAsync(
                item => item.Id == property.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            projection = ReservationPropertyProjection.Create(property.PropertyId, property.ScopeId);
            dbContext.PropertyProjections.Add(projection);
        }

        if (!projection.Apply(property.TimeZoneId, property.IsActive, property.SourceVersion))
        {
            return;
        }

        ReservationArrivalReminder[] existing = await dbContext.ArrivalReminders
            .Where(reminder => reminder.PropertyId == property.PropertyId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

        if (!projection.IsActive || string.IsNullOrWhiteSpace(projection.TimeZoneId))
        {
            Supersede(existing);
            return;
        }

        DateOnly earliestRelevantArrival = DateOnly.FromDateTime(property.OccurredAtUtc.UtcDateTime.AddDays(-1));
        Reservation[] reservations = await dbContext.Reservations
            .Where(reservation =>
                reservation.PropertyId == property.PropertyId &&
                reservation.ExpectedArrivalTime != null &&
                reservation.Arrival >= earliestRelevantArrival &&
                (reservation.Status == ReservationState.PendingAllocation ||
                 reservation.Status == ReservationState.Confirmed))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

        HashSet<Guid> relevantReservationIds = reservations.Select(reservation => reservation.Id).ToHashSet();
        Supersede(existing.Where(reminder => !relevantReservationIds.Contains(reminder.ReservationId)));
        foreach (Reservation reservation in reservations)
        {
            this.RefreshCore(
                new(
                    reservation.ScopeId,
                    reservation.Id,
                    reservation.PropertyId,
                    reservation.Arrival,
                    reservation.ExpectedArrivalTime,
                    reservation.PrimaryGuestName,
                    reservation.DetailsRevision),
                projection,
                existing.Where(reminder => reminder.ReservationId == reservation.Id).ToArray());
        }
    }

    public async Task RefreshReservationAsync(
        ReservationReminderSource reservation,
        CancellationToken cancellationToken)
    {
        ReservationArrivalReminder[] existing = await dbContext.ArrivalReminders
            .Where(reminder => reminder.ReservationId == reservation.ReservationId)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        ReservationPropertyProjection? property = dbContext.PropertyProjections.Local.FirstOrDefault(
            item => item.Id == reservation.PropertyId && item.ScopeId == reservation.ScopeId) ??
            await dbContext.PropertyProjections.AsNoTracking().FirstOrDefaultAsync(
                item => item.Id == reservation.PropertyId,
                cancellationToken).ConfigureAwait(false);

        this.RefreshCore(reservation, property, existing);
    }

    public async Task<ReservationArrivalReminderClaimResult> ClaimDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ReservationArrivalReminder[] candidates = await dbContext.ArrivalReminders
            .Where(reminder =>
                reminder.State == ReservationArrivalReminderState.Pending &&
                reminder.DueAtUtc <= nowUtc)
            .OrderBy(reminder => reminder.DueAtUtc)
            .ThenBy(reminder => reminder.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.Length == 0)
        {
            return new(0, []);
        }

        Guid[] reservationIds = candidates.Select(reminder => reminder.ReservationId).Distinct().ToArray();
        Guid[] propertyIds = candidates.Select(reminder => reminder.PropertyId).Distinct().ToArray();
        Dictionary<Guid, Reservation> reservations = await dbContext.Reservations
            .Where(reservation => reservationIds.Contains(reservation.Id))
            .ToDictionaryAsync(reservation => reservation.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, ReservationPropertyProjection> properties = await dbContext.PropertyProjections
            .AsNoTracking()
            .Where(property => propertyIds.Contains(property.Id))
            .ToDictionaryAsync(property => property.Id, cancellationToken).ConfigureAwait(false);
        List<ReservationArrivalReminderDispatch> dispatches = new(candidates.Length);

        foreach (ReservationArrivalReminder candidate in candidates)
        {
            if (!reservations.TryGetValue(candidate.ReservationId, out Reservation? reservation) ||
                reservation.Status != ReservationState.Confirmed ||
                reservation.PropertyId != candidate.PropertyId ||
                reservation.DetailsRevision != candidate.DetailsRevision ||
                reservation.Arrival != candidate.Arrival ||
                reservation.ExpectedArrivalTime != candidate.ExpectedArrivalTime ||
                !properties.TryGetValue(candidate.PropertyId, out ReservationPropertyProjection? property) ||
                !property.IsKnown ||
                !property.IsActive ||
                !string.Equals(property.TimeZoneId, candidate.TimeZoneId, StringComparison.Ordinal) ||
                candidate.ExpectedArrivalAtUtc <= nowUtc)
            {
                candidate.Supersede();
                continue;
            }

            candidate.Dispatch(nowUtc);
            dispatches.Add(new(
                candidate.Id,
                candidate.ScopeId,
                candidate.ReservationId,
                candidate.PropertyId,
                reservation.PrimaryGuestName,
                candidate.Arrival,
                candidate.ExpectedArrivalTime,
                candidate.TimeZoneId,
                candidate.DetailsRevision));
        }

        return new(candidates.Length, dispatches);
    }

    public async Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(
        CancellationToken cancellationToken) => await dbContext.Reservations
        .IgnoreQueryFilters()
        .AsNoTracking()
        .Select(reservation => reservation.ScopeId)
        .Distinct()
        .Order()
        .ToArrayAsync(cancellationToken)
        .ConfigureAwait(false);

    private void RefreshCore(
        ReservationReminderSource reservation,
        ReservationPropertyProjection? property,
        IReadOnlyCollection<ReservationArrivalReminder> existing)
    {
        if (reservation.ExpectedArrivalTime is not { } expectedArrivalTime ||
            property is null ||
            !property.IsKnown ||
            !property.IsActive ||
            string.IsNullOrWhiteSpace(property.TimeZoneId) ||
            !TryResolveExpectedArrivalUtc(
                reservation.Arrival,
                expectedArrivalTime,
                property.TimeZoneId,
                out DateTimeOffset expectedArrivalAtUtc))
        {
            Supersede(existing);
            return;
        }

        ReservationArrivalReminder? matching = existing.SingleOrDefault(reminder =>
            reminder.DetailsRevision == reservation.DetailsRevision &&
            reminder.LeadTimeMinutes == ReservationsModuleMetadata.ArrivalReminderLeadTimeMinutes &&
            string.Equals(reminder.TimeZoneId, property.TimeZoneId, StringComparison.Ordinal));
        Supersede(existing.Where(reminder => reminder != matching));
        if (matching is not null)
        {
            matching.Reactivate();
            return;
        }

        dbContext.ArrivalReminders.Add(ReservationArrivalReminder.Create(
            idGenerator.NewId(),
            reservation.ScopeId,
            reservation.ReservationId,
            reservation.PropertyId,
            reservation.DetailsRevision,
            property.TimeZoneId,
            reservation.Arrival,
            expectedArrivalTime,
            expectedArrivalAtUtc,
            expectedArrivalAtUtc.AddMinutes(-ReservationsModuleMetadata.ArrivalReminderLeadTimeMinutes),
            ReservationsModuleMetadata.ArrivalReminderLeadTimeMinutes));
    }

    private static void Supersede(IEnumerable<ReservationArrivalReminder> reminders)
    {
        foreach (ReservationArrivalReminder reminder in reminders)
        {
            reminder.Supersede();
        }
    }

    private static bool TryResolveExpectedArrivalUtc(
        DateOnly arrival,
        TimeOnly expectedArrivalTime,
        string timeZoneId,
        out DateTimeOffset expectedArrivalAtUtc)
    {
        try
        {
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime local = DateTime.SpecifyKind(
                arrival.ToDateTime(expectedArrivalTime),
                DateTimeKind.Unspecified);
            if (timeZone.IsInvalidTime(local))
            {
                expectedArrivalAtUtc = default;
                return false;
            }

            expectedArrivalAtUtc = new(
                TimeZoneInfo.ConvertTimeToUtc(local, timeZone),
                TimeSpan.Zero);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            expectedArrivalAtUtc = default;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            expectedArrivalAtUtc = default;
            return false;
        }
    }
}
