namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record StartDataRightsAnonymisationExecutionCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    Guid IdempotencyKey,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsExecutionDto>;
