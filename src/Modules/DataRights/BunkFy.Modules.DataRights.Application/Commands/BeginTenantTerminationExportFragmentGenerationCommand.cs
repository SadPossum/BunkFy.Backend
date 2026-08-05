namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record BeginTenantTerminationExportFragmentGenerationCommand(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    string OwnerKey,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedWorkItemVersion)
    : ITransactionalCommand<TenantTerminationExportFragmentGenerationStart>;

internal sealed record TenantTerminationExportFragmentGenerationStart(
    long FragmentVersion,
    TenantTerminationExportFragmentGenerationRequest Request);
