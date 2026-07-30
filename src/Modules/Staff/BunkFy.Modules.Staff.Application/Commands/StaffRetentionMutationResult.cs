namespace BunkFy.Modules.Staff.Application.Commands;

internal sealed record StaffRetentionMutationResult(
    StaffRetentionMutationStatus Status,
    DateTimeOffset? HoldReviewDueAtUtc = null,
    StaffRetentionMutationFailure Failure =
        StaffRetentionMutationFailure.None);
