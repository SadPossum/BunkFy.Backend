namespace BunkFy.Modules.Guests.Application.Commands;

internal enum GuestRetentionMutationFailure
{
    None = 0,
    ProjectionUnavailable = 1,
    PolicyUnavailable = 2,
    MutationFailed = 3
}
