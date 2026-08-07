namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

internal sealed record StaffLifecycleChangeValues(
    StaffChangeReason Reason,
    StaffActorId Actor)
{
    public static Result<StaffLifecycleChangeValues> Create(
        string? reason,
        string? actorId)
    {
        Result<StaffChangeReason> changeReason =
            StaffChangeReason.Create(reason);
        if (changeReason.IsFailure)
        {
            return Result.Failure<StaffLifecycleChangeValues>(
                changeReason.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? Result.Success(new StaffLifecycleChangeValues(
                changeReason.Value,
                actor.Value))
            : Result.Failure<StaffLifecycleChangeValues>(actor.Error);
    }
}
