namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed record BeginTenantTerminationExportArtifactGenerationCommand(
    Guid ProcessId,
    long OperationRevision,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<TenantTerminationExportArtifactGenerationStart>;

internal sealed record TenantTerminationExportArtifactGenerationStart(
    bool DispatchRequired,
    long ProcessVersion,
    long ArtifactVersion,
    TenantTerminationExportArtifact Artifact,
    IReadOnlyList<TenantTerminationExportFragment> Fragments);
