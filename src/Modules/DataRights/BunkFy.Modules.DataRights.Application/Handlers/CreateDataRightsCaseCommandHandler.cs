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
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CreateDataRightsCaseCommandHandler(
    IDataRightsCaseRepository cases,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateDataRightsCaseCommand, DataRightsCaseDto>
{
    public async Task<Result<DataRightsCaseDto>> HandleAsync(
        CreateDataRightsCaseCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<DataRightsCaseDto>(DataRightsApplicationErrors.TenantRequired);
        }

        Result<DataRightsCaseRequest> request = DataRightsCaseRequest.Create(
            command.PropertyId,
            DataRightsCaseKind.GuestRights,
            (DataRightsCaseOperation)command.RequestedOperations,
            (DataRightsRequesterRelation)command.RequesterRelationship,
            (DataRightsRestrictionAction)command.RestrictionDirective);
        if (request.IsFailure)
        {
            return Result.Failure<DataRightsCaseDto>(request.Error);
        }

        Result<DataRightsCase> created = DataRightsCase.Create(
            ids.NewId(),
            scopeContext.ScopeId,
            request.Value,
            command.ActorId,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<DataRightsCaseDto>(created.Error);
        }

        await cases.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}
