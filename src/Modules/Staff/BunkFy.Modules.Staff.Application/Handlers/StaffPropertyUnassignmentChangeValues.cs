namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

internal sealed record StaffPropertyUnassignmentChangeValues(
    StaffChangeReason Reason,
    StaffActorId Actor)
{
    public static Result<StaffPropertyUnassignmentChangeValues> Create(
        string? reason,
        string? actorId)
    {
        Result<StaffChangeReason> changeReason = StaffChangeReason.Create(reason);
        if (changeReason.IsFailure)
        {
            return Result.Failure<StaffPropertyUnassignmentChangeValues>(
                changeReason.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? Result.Success(new StaffPropertyUnassignmentChangeValues(
                changeReason.Value,
                actor.Value))
            : Result.Failure<StaffPropertyUnassignmentChangeValues>(actor.Error);
    }
}
