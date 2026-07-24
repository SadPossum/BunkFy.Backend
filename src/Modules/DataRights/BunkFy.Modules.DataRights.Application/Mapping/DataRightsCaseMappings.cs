namespace BunkFy.Modules.DataRights.Application.Mapping;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;

public static class DataRightsCaseMappings
{
    public static DataRightsCaseDto ToDto(this DataRightsCase dataRightsCase) => new(
        dataRightsCase.Id,
        dataRightsCase.PropertyId,
        (DataRightsCaseType)dataRightsCase.Kind,
        (DataRightsOperation)dataRightsCase.RequestedOperations,
        (DataRightsRequesterRelationship)dataRightsCase.RequesterRelationship,
        (DataRightsVerificationStatus)dataRightsCase.VerificationStatus,
        (DataRightsRoutingStatus)dataRightsCase.RoutingStatus,
        (DataRightsCaseStatus)dataRightsCase.Status,
        dataRightsCase.SelectedSubjects.Count,
        dataRightsCase.DueAtUtc,
        dataRightsCase.Version,
        dataRightsCase.CreatedAtUtc,
        dataRightsCase.LastChangedAtUtc);
}
