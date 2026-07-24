namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record StartDataRightsAnonymisationExecutionCommand(
    Guid PropertyId,
    Guid CaseId,
    Guid IdempotencyKey,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsExecutionDto>;
