namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Results;

internal sealed record StaffProfileUpdateValues(
    StaffProfile Profile,
    StaffActorId Actor)
{
    public static Result<StaffProfileUpdateValues> Create(
        string displayName,
        string? legalName,
        string? workEmail,
        string? workPhone,
        string? employeeNumber,
        string? jobTitle,
        string? department,
        string actorId)
    {
        Result<StaffProfile> profile = StaffProfile.Create(
            displayName,
            legalName,
            workEmail,
            workPhone,
            employeeNumber,
            jobTitle,
            department,
            authSubjectId: null);
        if (profile.IsFailure)
        {
            return Result.Failure<StaffProfileUpdateValues>(profile.Error);
        }

        Result<StaffActorId> actor = StaffActorId.Create(actorId);
        return actor.IsSuccess
            ? Result.Success(new StaffProfileUpdateValues(
                profile.Value,
                actor.Value))
            : Result.Failure<StaffProfileUpdateValues>(actor.Error);
    }

    public Result<StaffProfile> ForAuthSubject(string? authSubjectId) =>
        StaffProfile.Create(
            this.Profile.DisplayName,
            this.Profile.LegalName,
            this.Profile.WorkEmail,
            this.Profile.WorkPhone,
            this.Profile.EmployeeNumber,
            this.Profile.JobTitle,
            this.Profile.Department,
            authSubjectId);
}
