namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using Gma.Framework.Cqrs;

public sealed record FailDataRightsExportGenerationCommand(
    DataRightsCaseScope Scope,
    Guid ArtifactId,
    Guid CaseId,
    long DecisionRevision,
    Guid RunId,
    int Attempt,
    string FailureCode)
    : ITransactionalCommand<Unit>, IDataRightsPersistenceRetryableCommand;
