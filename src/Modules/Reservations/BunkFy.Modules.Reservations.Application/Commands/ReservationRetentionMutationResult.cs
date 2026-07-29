namespace BunkFy.Modules.Reservations.Application.Commands;

internal sealed record ReservationRetentionMutationResult(
    ReservationRetentionMutationStatus Status,
    DateTimeOffset? HoldReviewDueAtUtc = null,
    ReservationRetentionMutationFailure Failure =
        ReservationRetentionMutationFailure.None);
