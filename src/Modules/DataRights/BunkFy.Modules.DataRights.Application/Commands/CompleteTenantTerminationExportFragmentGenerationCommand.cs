namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record CompleteTenantTerminationExportFragmentGenerationCommand(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    string OwnerKey,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedWorkItemVersion,
    long ExpectedFragmentVersion,
    TenantTerminationProtectedExportFragment ProtectedFragment)
    : ITransactionalCommand<TenantTerminationOwnerResultRecorded>;
