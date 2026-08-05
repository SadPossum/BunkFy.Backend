namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record FailTenantTerminationExportArtifactGenerationCommand(
    Guid ProcessId,
    long OperationRevision,
    Guid ArtifactId,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedArtifactVersion,
    string FailureCode)
    : ITransactionalCommand<Unit>;
