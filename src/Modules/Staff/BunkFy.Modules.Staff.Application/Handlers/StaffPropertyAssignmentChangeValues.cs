namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

internal sealed record StaffPropertyAssignmentChangeValues(
    string? PropertyJobTitle,
    StaffActorId Actor)
{
    public static Result<StaffPropertyAssignmentChangeValues> Create(
        string? propertyJobTitle,
        string? actorId)
    {
        string? title = string.IsNullOrWhiteSpace(propertyJobTitle)
            ? null
            : propertyJobTitle.Trim();
        if (title?.Length > StaffPropertyAssignment.JobTitleMaxLength)
        {
            return Result.Failure<StaffPropertyAssignmentChangeValues>(
                StaffDomainErrors.JobTitleInvalid);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? Result.Success(new StaffPropertyAssignmentChangeValues(
                title,
                actor.Value))
            : Result.Failure<StaffPropertyAssignmentChangeValues>(actor.Error);
    }
}
