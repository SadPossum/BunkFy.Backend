namespace BunkFy.Modules.Reservations.Application;

using BunkFy.DataGovernance;
using Gma.Framework.Results;
using BunkFy.Modules.Reservations.Domain.Errors;

public static class ReservationsApplicationErrors
{
    public static Error CountryPolicyDenied(CountryPolicyDecisionReason reason) => new(
        $"Reservations.CountryPolicyDenied.{reason}",
        "The property is not enabled for this data-processing operation.");
    public static IReadOnlyList<Error> CountryPolicyDenials { get; } =
        Enum.GetValues<CountryPolicyDecisionReason>()
            .Where(reason => reason is not CountryPolicyDecisionReason.Unknown and not CountryPolicyDecisionReason.Allowed)
            .Select(CountryPolicyDenied)
            .ToArray();
    public static readonly Error ReservationNotFound = new("Reservations.ReservationNotFound", "The reservation was not found.");
    public static readonly Error ExternalSourceAlreadyExists = new("Reservations.ExternalSourceAlreadyExists", "A reservation already exists for this external source reference.");
    public static readonly Error TenantRequired = new("Reservations.TenantRequired", "A tenant context is required.");
    public static readonly Error InventoryUnitNotFound = new("Reservations.InventoryUnitNotFound", "One or more inventory units are not present in the local Inventory projection.");
    public static readonly Error InventoryUnitPropertyMismatch = new("Reservations.InventoryUnitPropertyMismatch", "One or more inventory units do not belong to the requested property.");
    public static Error VersionConflict => ReservationsDomainErrors.VersionConflict;
    public static Error InvalidTransition => ReservationsDomainErrors.InvalidTransition;
    public static Error StayRangeInvalid => ReservationsDomainErrors.StayRangeInvalid;
    public static Error ExpectedStayTimeInvalid => ReservationsDomainErrors.ExpectedStayTimeInvalid;
    public static Error RequestedUnitsInvalid => ReservationsDomainErrors.RequestedUnitsInvalid;
    public static Error DetailsRevisionConflict => ReservationsDomainErrors.DetailsRevisionConflict;
    public static Error DetailsChangeProvenanceInvalid => ReservationsDomainErrors.DetailsChangeProvenanceInvalid;
    public static Error AllocationAmendmentInProgress => ReservationsDomainErrors.AllocationAmendmentInProgress;
    public static Error AllocationAmendmentInvalid => ReservationsDomainErrors.AllocationAmendmentInvalid;
    public static Error StayBusinessDateInvalid => ReservationsDomainErrors.StayBusinessDateInvalid;
    public static Error StayProvenanceInvalid => ReservationsDomainErrors.StayProvenanceInvalid;
    public static readonly Error GuestNotLinkable = new("Reservations.GuestNotLinkable", "The guest is not active and visible at this property.");
    public static readonly Error ReminderTaskOptionsInvalid = new("Reservations.ReminderTaskOptionsInvalid", "Reservation reminder task options are invalid.");
    public static readonly Error CorrectionRequestInvalid = new(
        "Reservations.CorrectionRequestInvalid",
        "The reservation correction request is invalid.");
    public static readonly Error CorrectionIdempotencyConflict = new(
        "Reservations.CorrectionIdempotencyConflict",
        "The correction idempotency key is already bound to a different request or result.");
    public static readonly Error CorrectionNoChanges = new(
        "Reservations.CorrectionNoChanges",
        "The correction does not change the selected reservation details.");
    public static readonly Error DataRightsApprovalRequired = new(
        "Reservations.DataRightsApprovalRequired",
        "The reservation operation requires a current approved data-rights case.");
    public static readonly Error ProcessingRestrictionRequestInvalid = new(
        "Reservations.ProcessingRestrictionRequestInvalid",
        "The reservation processing-restriction request is invalid.");
    public static readonly Error ProcessingRestrictionIdempotencyConflict = new(
        "Reservations.ProcessingRestrictionIdempotencyConflict",
        "The processing-restriction idempotency key is bound to a different request.");
    public static readonly Error ProcessingRestrictionReservationVersionConflict = new(
        "Reservations.ProcessingRestrictionReservationVersionConflict",
        "The selected reservation version has changed.");
    public static readonly Error ProcessingRestrictionApprovalAlreadyUsed = new(
        "Reservations.ProcessingRestrictionApprovalAlreadyUsed",
        "The data-rights approval coordinate has already been used.");
    public static readonly Error ProcessingRestrictionNotFound = new(
        "Reservations.ProcessingRestrictionNotFound",
        "The reservation processing restriction was not found.");
    public static readonly Error ProcessingRestrictionProjectionUnavailable = new(
        "Reservations.ProcessingRestrictionProjectionUnavailable",
        "The reservation processing-restriction state is unavailable or unsupported.");
    public static Error ReservationGuestLinkInvalid => ReservationsDomainErrors.ReservationGuestLinkInvalid;
    public static Error ReservationGuestRoleOccupied => ReservationsDomainErrors.ReservationGuestRoleOccupied;
}
