namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Domain.Aggregates;

internal static class DataRightsCaseValidation
{
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
