namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

public sealed record ExecuteDataRightsRestrictionCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    Guid IdempotencyKey,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsRestrictionExecutionDto>;
