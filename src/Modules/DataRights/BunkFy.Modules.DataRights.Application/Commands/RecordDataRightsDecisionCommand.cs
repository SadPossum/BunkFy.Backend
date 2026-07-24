namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RecordDataRightsDecisionCommand(
    Guid PropertyId,
    Guid CaseId,
    DataRightsDecisionOutcome Decision,
    DataRightsDecisionReason Reason,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
