namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationGuestDto(Guid GuestId, ReservationGuestRoleKind Role);
