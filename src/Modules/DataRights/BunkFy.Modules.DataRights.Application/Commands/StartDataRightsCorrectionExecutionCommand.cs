namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

public sealed record StartDataRightsCorrectionExecutionCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    Guid ExecutionId,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCorrectionExecutionDto>;
