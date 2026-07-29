namespace BunkFy.Modules.Reservations.Application.Commands;

internal enum ReservationRetentionMutationFailure
{
    None = 0,
    ProjectionUnavailable = 1,
    PolicyUnavailable = 2,
    MutationFailed = 3
}
