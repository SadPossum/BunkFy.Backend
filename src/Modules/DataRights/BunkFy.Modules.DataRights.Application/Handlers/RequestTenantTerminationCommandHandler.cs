namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class RequestTenantTerminationCommandHandler(
    ITenantTerminationCaseRepository cases,
    IDataRightsOperationLock operationLock,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<RequestTenantTerminationCommand,
        TenantTerminationCaseDto>
{
    public async Task<Result<TenantTerminationCaseDto>> HandleAsync(
        RequestTenantTerminationCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<TenantTerminationCaseDto>(
                DataRightsApplicationErrors.TenantRequired);
        }

        await operationLock.AcquireTenantControlAsync(cancellationToken)
            .ConfigureAwait(false);
        string actor = command.ActorId?.Trim() ?? string.Empty;
        DataRightsCase? existing = await cases.GetAsync(
            command.RequestId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesTenantTerminationRequest(
                    command.ExportRequested,
                    (DataRightsRequesterRelation)
                        command.RequesterRelationship) &&
                string.Equals(
                    existing.CreatedBy,
                    actor,
                    StringComparison.Ordinal)
                    ? Result.Success(existing.ToTenantTerminationDto())
                    : Result.Failure<TenantTerminationCaseDto>(
                        DataRightsApplicationErrors
                            .TenantTerminationRequestConflict);
        }

        if (await cases.GetActiveAsync(cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return Result.Failure<TenantTerminationCaseDto>(
                DataRightsApplicationErrors
                    .TenantTerminationActiveCaseExists);
        }

        Result<DataRightsCaseRequest> request = DataRightsCaseRequest.Create(
            propertyId: null,
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseOperation.Anonymisation,
            (DataRightsRequesterRelation)command.RequesterRelationship,
            DataRightsRestrictionAction.None);
        if (request.IsFailure)
        {
            return Result.Failure<TenantTerminationCaseDto>(request.Error);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<DataRightsCase> created = DataRightsCase.Create(
            command.RequestId,
            scopeContext.ScopeId,
            request.Value,
            actor,
            nowUtc);
        if (created.IsFailure)
        {
            return Result.Failure<TenantTerminationCaseDto>(created.Error);
        }

        Result prepared = created.Value.PrepareTenantTerminationReview(
            command.ExportRequested,
            created.Value.Version,
            actor,
            nowUtc);
        if (prepared.IsFailure)
        {
            return Result.Failure<TenantTerminationCaseDto>(prepared.Error);
        }

        await cases.AddAsync(created.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(created.Value.ToTenantTerminationDto());
    }
}
