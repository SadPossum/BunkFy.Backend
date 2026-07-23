namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RecordRequesterVerificationCommand(
    Guid PropertyId,
    Guid CaseId,
    bool Verified,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
