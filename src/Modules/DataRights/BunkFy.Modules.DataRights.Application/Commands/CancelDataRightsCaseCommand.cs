namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record CancelDataRightsCaseCommand(
    Guid PropertyId,
    Guid CaseId,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
