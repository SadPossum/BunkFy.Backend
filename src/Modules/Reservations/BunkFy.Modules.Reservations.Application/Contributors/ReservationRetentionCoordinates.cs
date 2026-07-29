namespace BunkFy.Modules.Reservations.Application.Contributors;

using BunkFy.Modules.Reservations.Domain.Retention;

internal static class ReservationRetentionCoordinates
{
    public const string OwnerKey = "reservations";
    public const string DataClassKey = "reservation-operational";
    public const string Trigger = "reservation-ended";
    public const string Purpose = "reservation-retention";
    public const string SourceProvenance = "retention-worker";
    public const string SystemActor =
        ReservationRetentionAnonymisationReceipt.SystemActorId;
    public const int ExecutionPolicyVersion = 1;

    public const string CompletedOutcome =
        "reservations.reservation-operational.completed";
    public const string BacklogOutcome =
        "reservations.reservation-operational.backlog";
    public const string LegalHoldOutcome =
        "reservations.reservation-operational.legal-hold";
    public const string PolicyUnavailableOutcome =
        "reservations.reservation-operational.policy-unavailable";
    public const string ProjectionUnavailableOutcome =
        "reservations.reservation-operational.projection-unavailable";
    public const string MutationFailedOutcome =
        "reservations.reservation-operational.mutation-failed";
    public const string CoordinateInvalidOutcome =
        "reservations.reservation-operational.coordinate-invalid";
}
