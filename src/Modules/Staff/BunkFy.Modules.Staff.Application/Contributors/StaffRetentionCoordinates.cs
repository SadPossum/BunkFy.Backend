namespace BunkFy.Modules.Staff.Application.Contributors;

internal static class StaffRetentionCoordinates
{
    public const string AccommodationType = "hostel";
    public const string OwnerKey = "staff";
    public const string DataClassKey = "staff-employment";
    public const string Trigger = "employment-ended";
    public const string Purpose = "staff-profile-retention";
    public const string SourceProvenance = "retention-worker";
    public const string SystemActor = "system:retention";
    public const int ExecutionPolicyVersion = 1;

    public const string CompletedOutcome =
        "staff.staff-employment.completed";
    public const string BacklogOutcome =
        "staff.staff-employment.backlog";
    public const string LegalHoldOutcome =
        "staff.staff-employment.legal-hold";
    public const string PolicyUnavailableOutcome =
        "staff.staff-employment.policy-unavailable";
    public const string ProjectionUnavailableOutcome =
        "staff.staff-employment.projection-unavailable";
    public const string PrerequisiteBlockedOutcome =
        "staff.staff-employment.prerequisite-blocked";
    public const string PrerequisiteUnavailableOutcome =
        "staff.staff-employment.prerequisite-unavailable";
    public const string MutationFailedOutcome =
        "staff.staff-employment.mutation-failed";
    public const string CoordinateInvalidOutcome =
        "staff.staff-employment.coordinate-invalid";
}
