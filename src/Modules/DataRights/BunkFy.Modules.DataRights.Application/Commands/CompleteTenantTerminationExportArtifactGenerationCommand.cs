namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record CompleteTenantTerminationExportArtifactGenerationCommand(
    Guid ProcessId,
    long OperationRevision,
    Guid ArtifactId,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedProcessVersion,
    long ExpectedArtifactVersion,
    TenantTerminationProtectedExportArtifact ProtectedArtifact)
    : ITransactionalCommand<TenantTerminationExportArtifactGenerationCompleted>;

internal sealed record TenantTerminationExportArtifactGenerationCompleted(
    Guid ProcessId,
    long ProcessVersion,
    Guid ArtifactId,
    long ArtifactVersion);
