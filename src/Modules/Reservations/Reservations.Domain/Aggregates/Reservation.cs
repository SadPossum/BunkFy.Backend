namespace Reservations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Reservations.Domain.Entities;
using Reservations.Domain.Errors;
using Reservations.Domain.Events;

public sealed class Reservation : ScopedAggregateRoot<Guid>
{
    public const int PrimaryGuestNameMaxLength = 200;
    public const int EmailMaxLength = 320;
    public const int PhoneMaxLength = 50;
    public const int SourceSystemMaxLength = 100;
    public const int SourceReferenceMaxLength = 200;
    public const int NotesMaxLength = 2000;
    public const int MaximumRequestedUnits = 100;

    private readonly List<RequestedInventoryUnit> requestedUnits = [];

    private Reservation() { }

    private Reservation(
        Guid reservationId,
        string scopeId,
        Guid propertyId,
        Guid allocationRequestId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        ReservationSource source,
        string? sourceSystem,
        string? sourceReference,
        string? notes,
        DateTimeOffset createdAtUtc)
        : base(reservationId, scopeId)
    {
        this.PropertyId = propertyId;
        this.AllocationRequestId = allocationRequestId;
        this.Arrival = arrival;
        this.Departure = departure;
        this.PrimaryGuestName = primaryGuestName;
        this.Email = email;
        this.Phone = phone;
        this.GuestCount = guestCount;
        this.Source = source;
        this.SourceSystem = sourceSystem;
        this.SourceReference = sourceReference;
        this.Notes = notes;
        this.CreatedAtUtc = createdAtUtc;
        this.requestedUnits.AddRange(inventoryUnitIds.Select(unitId => new RequestedInventoryUnit(unitId, this.ScopeId, this.Id)));
    }

    public Guid PropertyId { get; private set; }
    public Guid AllocationRequestId { get; private set; }
    public Guid? AllocationId { get; private set; }
    public long? AllocationVersion { get; private set; }
    public ReservationAllocationRejection? AllocationRejection { get; private set; }
    public Guid? ReleaseRequestId { get; private set; }
    public int? LastReleaseRejectionCode { get; private set; }
    public DateOnly Arrival { get; private set; }
    public DateOnly Departure { get; private set; }
    public string PrimaryGuestName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public int GuestCount { get; private set; }
    public ReservationSource Source { get; private set; }
    public string? SourceSystem { get; private set; }
    public string? SourceReference { get; private set; }
    public string? Notes { get; private set; }
    public ReservationState Status { get; private set; } = ReservationState.PendingAllocation;
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<RequestedInventoryUnit> RequestedUnits => this.requestedUnits.AsReadOnly();

    public static Result<Reservation> Create(
        Guid reservationId,
        string scopeId,
        Guid propertyId,
        Guid allocationRequestId,
        DateOnly arrival,
        DateOnly departure,
        IReadOnlyCollection<Guid> inventoryUnitIds,
        string primaryGuestName,
        string? email,
        string? phone,
        int guestCount,
        ReservationSource source,
        string? sourceSystem,
        string? sourceReference,
        string? notes,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (reservationId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.ReservationIdRequired);
        }

        if (propertyId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PropertyIdRequired);
        }

        if (allocationRequestId == Guid.Empty)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.AllocationRequestIdRequired);
        }

        if (arrival >= departure)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.StayRangeInvalid);
        }

        ArgumentNullException.ThrowIfNull(inventoryUnitIds);
        Guid[] units = inventoryUnitIds.ToArray();
        if (units.Length is 0 or > MaximumRequestedUnits || units.Any(id => id == Guid.Empty) || units.Distinct().Count() != units.Length)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.RequestedUnitsInvalid);
        }

        string normalizedGuestName = NormalizeRequired(primaryGuestName);
        if (normalizedGuestName.Length is 0 or > PrimaryGuestNameMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PrimaryGuestNameInvalid);
        }

        string? normalizedEmail = NormalizeOptional(email);
        string? normalizedPhone = NormalizeOptional(phone);
        string? normalizedNotes = NormalizeOptional(notes);
        if (normalizedEmail?.Length > EmailMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.EmailInvalid);
        }

        if (normalizedPhone?.Length > PhoneMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.PhoneInvalid);
        }

        if (normalizedNotes?.Length > NotesMaxLength)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.NotesInvalid);
        }

        if (guestCount <= 0)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.GuestCountInvalid);
        }

        string? normalizedSourceSystem = NormalizeOptional(sourceSystem)?.ToLowerInvariant();
        string? normalizedSourceReference = NormalizeOptional(sourceReference);
        bool sourceValid = source switch
        {
            ReservationSource.Direct => normalizedSourceSystem is null && normalizedSourceReference is null,
            ReservationSource.External => normalizedSourceSystem is { Length: > 0 and <= SourceSystemMaxLength } &&
                                          normalizedSourceReference is { Length: > 0 and <= SourceReferenceMaxLength },
            _ => false
        };
        if (!sourceValid)
        {
            return Result.Failure<Reservation>(ReservationsDomainErrors.SourceInvalid);
        }

        Reservation reservation = new(
            reservationId,
            scopeId,
            propertyId,
            allocationRequestId,
            arrival,
            departure,
            units,
            normalizedGuestName,
            normalizedEmail,
            normalizedPhone,
            guestCount,
            source,
            normalizedSourceSystem,
            normalizedSourceReference,
            normalizedNotes,
            nowUtc);
        reservation.RaiseDomainEvent(new ReservationCreatedDomainEvent(
            eventId,
            nowUtc,
            reservation.ScopeId,
            reservation.Id,
            reservation.PropertyId,
            reservation.AllocationRequestId,
            reservation.Arrival,
            reservation.Departure,
            units,
            reservation.Version));
        return Result.Success(reservation);
    }

    public Result ConfirmAllocation(
        Guid allocationRequestId,
        Guid allocationId,
        long allocationVersion,
        Guid cancellationEventId,
        DateTimeOffset nowUtc)
    {
        if (allocationRequestId != this.AllocationRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        if (this.Status == ReservationState.Confirmed && this.AllocationId == allocationId)
        {
            return Result.Success();
        }

        if (this.Status == ReservationState.CancellationPending && this.AllocationId == allocationId)
        {
            return Result.Success();
        }

        bool cancellationWasRequested = this.Status == ReservationState.CancellationPending &&
                                        this.AllocationId is null &&
                                        this.ReleaseRequestId.HasValue;
        if ((this.Status != ReservationState.PendingAllocation && !cancellationWasRequested) ||
            allocationId == Guid.Empty ||
            allocationVersion <= 0)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.AllocationId = allocationId;
        this.AllocationVersion = allocationVersion;
        this.AllocationRejection = null;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        if (cancellationWasRequested)
        {
            this.RaiseDomainEvent(new ReservationCancellationRequestedDomainEvent(
                cancellationEventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                allocationId,
                this.ReleaseRequestId!.Value,
                allocationVersion));
        }
        else
        {
            this.Status = ReservationState.Confirmed;
        }

        return Result.Success();
    }

    public Result RejectAllocation(
        Guid allocationRequestId,
        ReservationAllocationRejection rejection,
        Guid cancellationEventId,
        DateTimeOffset nowUtc)
    {
        if (allocationRequestId != this.AllocationRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        if (this.Status == ReservationState.AllocationRejected && this.AllocationRejection == rejection)
        {
            return Result.Success();
        }

        bool cancellationWasRequested = this.Status == ReservationState.CancellationPending && this.AllocationId is null;
        if (this.Status != ReservationState.PendingAllocation && !cancellationWasRequested)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.AllocationRejection = rejection;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.Status = cancellationWasRequested ? ReservationState.Cancelled : ReservationState.AllocationRejected;
        if (cancellationWasRequested)
        {
            this.ReleaseRequestId = null;
            this.RaiseDomainEvent(new ReservationCancelledDomainEvent(
                cancellationEventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                this.Version));
        }

        return Result.Success();
    }

    public Result RequestCancellation(
        long expectedVersion,
        Guid releaseRequestId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(ReservationsDomainErrors.VersionConflict);
        }

        if (this.Status == ReservationState.Cancelled)
        {
            return Result.Success();
        }

        if (this.Status == ReservationState.PendingAllocation)
        {
            this.Status = ReservationState.CancellationPending;
            this.ReleaseRequestId = releaseRequestId;
            this.Version++;
            this.UpdatedAtUtc = nowUtc;
            return Result.Success();
        }

        if (this.Status == ReservationState.AllocationRejected)
        {
            this.Status = ReservationState.Cancelled;
            this.Version++;
            this.UpdatedAtUtc = nowUtc;
            this.RaiseDomainEvent(new ReservationCancelledDomainEvent(
                eventId,
                nowUtc,
                this.ScopeId,
                this.Id,
                this.PropertyId,
                this.Version));
            return Result.Success();
        }

        if (this.Status != ReservationState.Confirmed || this.AllocationId is null || this.AllocationVersion is null)
        {
            return Result.Failure(ReservationsDomainErrors.InvalidTransition);
        }

        this.Status = ReservationState.CancellationPending;
        this.ReleaseRequestId = releaseRequestId;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        this.RaiseDomainEvent(new ReservationCancellationRequestedDomainEvent(
            eventId,
            nowUtc,
            this.ScopeId,
            this.Id,
            this.PropertyId,
            this.AllocationId.Value,
            releaseRequestId,
            this.AllocationVersion.Value));
        return Result.Success();
    }

    public Result CompleteCancellation(Guid releaseRequestId, DateTimeOffset nowUtc)
    {
        if (this.Status == ReservationState.Cancelled)
        {
            return Result.Success();
        }

        if (this.Status != ReservationState.CancellationPending || this.ReleaseRequestId != releaseRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        this.Status = ReservationState.Cancelled;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result RestoreAfterReleaseRejection(Guid releaseRequestId, int rejectionCode, DateTimeOffset nowUtc)
    {
        if (this.Status != ReservationState.CancellationPending || this.ReleaseRequestId != releaseRequestId)
        {
            return Result.Failure(ReservationsDomainErrors.AllocationCorrelationMismatch);
        }

        this.Status = ReservationState.Confirmed;
        this.ReleaseRequestId = null;
        this.LastReleaseRejectionCode = rejectionCode;
        this.Version++;
        this.UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
