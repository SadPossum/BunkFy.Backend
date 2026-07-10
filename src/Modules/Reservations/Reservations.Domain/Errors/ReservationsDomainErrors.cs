namespace Reservations.Domain.Errors;

using Gma.Framework.Results;

public static class ReservationsDomainErrors
{
    public static readonly Error ReservationIdRequired = new("Reservations.ReservationIdRequired", "Reservation id is required.");
    public static readonly Error PropertyIdRequired = new("Reservations.PropertyIdRequired", "Property id is required.");
    public static readonly Error AllocationRequestIdRequired = new("Reservations.AllocationRequestIdRequired", "Allocation request id is required.");
    public static readonly Error AllocationIdRequired = new("Reservations.AllocationIdRequired", "Allocation id is required.");
    public static readonly Error StayRangeInvalid = new("Reservations.StayRangeInvalid", "Arrival must be before departure.");
    public static readonly Error RequestedUnitsInvalid = new("Reservations.RequestedUnitsInvalid", "Requested units must contain unique, non-empty ids within the supported limit.");
    public static readonly Error PrimaryGuestNameInvalid = new("Reservations.PrimaryGuestNameInvalid", "Primary guest name is required and is too long.");
    public static readonly Error EmailInvalid = new("Reservations.EmailInvalid", "Email is too long.");
    public static readonly Error PhoneInvalid = new("Reservations.PhoneInvalid", "Phone is too long.");
    public static readonly Error GuestCountInvalid = new("Reservations.GuestCountInvalid", "Guest count must be positive.");
    public static readonly Error SourceInvalid = new("Reservations.SourceInvalid", "Reservation source is invalid.");
    public static readonly Error NotesInvalid = new("Reservations.NotesInvalid", "Reservation notes are too long.");
    public static readonly Error VersionConflict = new("Reservations.VersionConflict", "The reservation has changed. Refresh and retry.");
    public static readonly Error AllocationCorrelationMismatch = new("Reservations.AllocationCorrelationMismatch", "The allocation result does not match the current reservation request.");
    public static readonly Error InvalidTransition = new("Reservations.InvalidTransition", "The reservation cannot perform this lifecycle transition.");
}
