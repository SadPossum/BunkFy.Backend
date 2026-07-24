namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class SelectDataRightsSubjectCommandHandler(
    IDataRightsCaseRepository cases,
    IEnumerable<IDataRightsSubjectDiscoveryContributor> contributors,
    IScopeContext scopeContext,
    ISystemClock clock) : ICommandHandler<SelectDataRightsSubjectCommand, DataRightsCaseDto>
{
    public async Task<Result<DataRightsCaseDto>> HandleAsync(
        SelectDataRightsSubjectCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<DataRightsCaseDto>(DataRightsApplicationErrors.TenantRequired);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.PropertyId,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsCaseDto>(DataRightsApplicationErrors.CaseNotFound);
        }

        if (dataRightsCase.Status != DataRightsCaseState.Discovery)
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.TransitionInvalid);
        }

        DataRightsSubjectCoordinate? requestedCoordinate = command.Coordinate;
        if (requestedCoordinate is null)
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.SubjectCoordinateInvalid);
        }

        Result<IDataRightsSubjectDiscoveryContributor> contributor =
            DataRightsSubjectContributorSet.Find(contributors, requestedCoordinate.OwnerKey);
        if (contributor.IsFailure)
        {
            return Result.Failure<DataRightsCaseDto>(contributor.Error);
        }

        DataRightsSubjectSelectionValidation validation =
            await contributor.Value.ValidateSelectionAsync(
                new DataRightsSubjectSelectionRequest(
                    scopeContext.ScopeId,
                    command.PropertyId,
                    requestedCoordinate),
                cancellationToken).ConfigureAwait(false);
        if (validation is null)
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.SubjectCoordinateInvalid);
        }

        if (validation.Status != DataRightsSubjectSelectionValidationStatus.Valid)
        {
            return Result.Failure<DataRightsCaseDto>(MapValidationError(validation.Status));
        }

        DataRightsSubjectCoordinate? coordinate = validation.Coordinate;
        if (coordinate is null || !IsSameSelection(requestedCoordinate, coordinate))
        {
            return Result.Failure<DataRightsCaseDto>(
                DataRightsApplicationErrors.SubjectCoordinateInvalid);
        }

        Result selected = dataRightsCase.SelectSubject(
            coordinate.OwnerKey,
            coordinate.RecordType,
            coordinate.RecordId,
            coordinate.RecordVersion,
            command.ExpectedVersion,
            command.ActorId,
            clock.UtcNow);
        return selected.IsSuccess
            ? Result.Success(dataRightsCase.ToDto())
            : Result.Failure<DataRightsCaseDto>(selected.Error);
    }

    private static Error MapValidationError(DataRightsSubjectSelectionValidationStatus status) =>
        status switch
        {
            DataRightsSubjectSelectionValidationStatus.Stale =>
                DataRightsApplicationErrors.SubjectStale,
            DataRightsSubjectSelectionValidationStatus.ScopeUnavailable =>
                DataRightsApplicationErrors.DiscoveryScopeUnavailable,
            DataRightsSubjectSelectionValidationStatus.NotFound =>
                DataRightsApplicationErrors.SubjectNotFound,
            _ => DataRightsApplicationErrors.SubjectCoordinateInvalid
        };

    private static bool IsSameSelection(
        DataRightsSubjectCoordinate requested,
        DataRightsSubjectCoordinate validated) =>
        string.Equals(
            requested.OwnerKey?.Trim(),
            validated.OwnerKey?.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            requested.RecordType?.Trim(),
            validated.RecordType?.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        requested.RecordId == validated.RecordId &&
        requested.RecordVersion == validated.RecordVersion;
}
