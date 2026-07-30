namespace BunkFy.Modules.Workspaces.Domain;

public sealed record WorkspaceStaffOnboardingCorrectionOutcome(
    long PreviousVersion,
    long CurrentVersion,
    IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField> ChangedFields,
    Guid ApplicantEventId,
    DateTimeOffset OccurredAtUtc);
