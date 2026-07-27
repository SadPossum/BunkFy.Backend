namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestDataRightsExportCommand(
    DataRightsCaseScope Scope,
    Guid CaseId,
    Guid IdempotencyKey,
    long ExpectedVersion,
    string ActorId)
    : ITransactionalCommand<DataRightsExportArtifactDto>,
        IDataRightsPersistenceRetryableCommand;
