namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetDataRightsSelectedSubjectsQueryHandler(
    IDataRightsCaseRepository cases)
    : IQueryHandler<GetDataRightsSelectedSubjectsQuery, DataRightsSelectedSubjectsResponse>
{
    public async Task<Result<DataRightsSelectedSubjectsResponse>> HandleAsync(
        GetDataRightsSelectedSubjectsQuery query,
        CancellationToken cancellationToken)
    {
        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsSelectedSubjectsResponse>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DataRightsSelectedSubjectDto[] subjects = dataRightsCase.SelectedSubjects
            .OrderBy(subject => subject.OwnerKey, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordType, StringComparer.Ordinal)
            .ThenBy(subject => subject.RecordId)
            .Select(subject => new DataRightsSelectedSubjectDto(
                subject.OwnerKey,
                subject.RecordType,
                subject.RecordId,
                subject.RecordVersion,
                subject.SelectedAtUtc))
            .ToArray();
        return Result.Success(new DataRightsSelectedSubjectsResponse(
            dataRightsCase.Version,
            subjects));
    }
}
