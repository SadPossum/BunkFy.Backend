namespace BunkFy.Modules.Guests.Application.Contributors;

internal static class GuestRetentionCoordinates
{
    public const string OwnerKey = "guests";
    public const string DataClassKey = "guest-operational";
    public const string Trigger = "stay-ended";
    public const string Purpose = "guest-profile-retention";
    public const string SourceProvenance = "retention-worker";
    public const string SystemActor = "system:retention";
    public const int ExecutionPolicyVersion = 1;

    public const string CompletedOutcome =
        "guests.guest-operational.completed";
    public const string BacklogOutcome =
        "guests.guest-operational.backlog";
    public const string LegalHoldOutcome =
        "guests.guest-operational.legal-hold";
    public const string PolicyUnavailableOutcome =
        "guests.guest-operational.policy-unavailable";
    public const string ProjectionUnavailableOutcome =
        "guests.guest-operational.projection-unavailable";
    public const string MutationFailedOutcome =
        "guests.guest-operational.mutation-failed";
    public const string CoordinateInvalidOutcome =
        "guests.guest-operational.coordinate-invalid";
}
