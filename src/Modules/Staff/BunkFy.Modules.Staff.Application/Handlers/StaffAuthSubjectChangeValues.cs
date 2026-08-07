namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

internal sealed record StaffAuthSubjectChangeValues(
    StaffAuthSubject AuthSubject,
    StaffActorId Actor)
{
    public static Result<StaffAuthSubjectChangeValues> Create(
        string? authSubjectId,
        string actorId)
    {
        Result<StaffAuthSubject> subject = StaffAuthSubject.Create(
            authSubjectId);
        if (subject.IsFailure)
        {
            return Result.Failure<StaffAuthSubjectChangeValues>(
                subject.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? Result.Success(new StaffAuthSubjectChangeValues(
                subject.Value,
                actor.Value))
            : Result.Failure<StaffAuthSubjectChangeValues>(actor.Error);
    }
}
