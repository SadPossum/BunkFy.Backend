namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record FailTenantTerminationExportFragmentGenerationCommand(
    Guid ProcessId,
    Guid WorkItemId,
    long OperationRevision,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedFragmentVersion,
    string FailureCode)
    : ITransactionalCommand<Unit>;
