namespace Reservations.Application;

using Gma.Framework.Results;
using Reservations.Domain.Errors;

public static class ReservationsApplicationErrors
{
    public static readonly Error ReservationNotFound = new("Reservations.ReservationNotFound", "The reservation was not found.");
    public static readonly Error ExternalSourceAlreadyExists = new("Reservations.ExternalSourceAlreadyExists", "A reservation already exists for this external source reference.");
    public static readonly Error TenantRequired = new("Reservations.TenantRequired", "A tenant context is required.");
    public static readonly Error InventoryUnitNotFound = new("Reservations.InventoryUnitNotFound", "One or more inventory units are not present in the local Inventory projection.");
    public static readonly Error InventoryUnitPropertyMismatch = new("Reservations.InventoryUnitPropertyMismatch", "One or more inventory units do not belong to the requested property.");
    public static Error VersionConflict => ReservationsDomainErrors.VersionConflict;
    public static Error InvalidTransition => ReservationsDomainErrors.InvalidTransition;
    public static Error StayRangeInvalid => ReservationsDomainErrors.StayRangeInvalid;
    public static Error RequestedUnitsInvalid => ReservationsDomainErrors.RequestedUnitsInvalid;
}
