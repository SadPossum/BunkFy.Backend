namespace BunkFy.Modules.Guests.Application.Commands;

internal sealed record GuestRetentionMutationResult(
    GuestRetentionMutationStatus Status,
    DateTimeOffset? HoldReviewDueAtUtc = null,
    GuestRetentionMutationFailure Failure =
        GuestRetentionMutationFailure.None);
