namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;

internal static class DataRightsCaseValidation
{
    public static IEnumerable<string> Mutation(
        DataRightsCaseScope? scope,
        Guid caseId,
        long expectedVersion,
        string actorId)
    {
        if (scope is null || caseId == Guid.Empty)
        {
            yield return "Scope and CaseId are required.";
        }

        foreach (string error in VersionAndActor(expectedVersion, actorId))
        {
            yield return error;
        }
    }

    public static IEnumerable<string> Mutation(
        Guid propertyId,
        Guid caseId,
        long expectedVersion,
        string actorId)
    {
        if (propertyId == Guid.Empty || caseId == Guid.Empty)
        {
            yield return "PropertyId and CaseId are required.";
        }

        foreach (string error in VersionAndActor(expectedVersion, actorId))
        {
            yield return error;
        }
    }

    private static IEnumerable<string> VersionAndActor(long expectedVersion, string actorId)
    {
        if (expectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }

        string actor = actorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}
