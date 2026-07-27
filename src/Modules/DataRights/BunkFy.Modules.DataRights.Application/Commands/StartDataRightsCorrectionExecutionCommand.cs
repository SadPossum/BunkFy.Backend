namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record StartDataRightsCorrectionExecutionCommand(
    Guid PropertyId,
    Guid CaseId,
    Guid ExecutionId,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCorrectionExecutionDto>;
