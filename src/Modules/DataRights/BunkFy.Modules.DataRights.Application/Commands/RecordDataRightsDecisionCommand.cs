namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RecordDataRightsDecisionCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    DataRightsDecisionOutcome Decision,
    DataRightsDecisionReason Reason,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
