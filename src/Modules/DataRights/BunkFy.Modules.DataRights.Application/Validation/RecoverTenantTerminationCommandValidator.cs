namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class RecoverTenantTerminationCommandValidator
    : ICommandValidator<RecoverTenantTerminationCommand>
{
    public IEnumerable<string> Validate(
        RecoverTenantTerminationCommand command)
    {
        if (command.CaseId == Guid.Empty)
        {
            yield return "CaseId is required.";
        }

        if (command.ProcessId == Guid.Empty)
        {
            yield return "ProcessId is required.";
        }

        TenantTerminationApprovalEvidence? evidence =
            command.ApprovalEvidence;
        if (evidence is null ||
            !TenantTerminationApprovalEvidenceContract.TryCreate(
                evidence.ApprovalReference,
                evidence.OwnerCatalogSha256,
                evidence.BackupEvidenceReference,
                evidence.RestoreDrillEvidenceReference,
                evidence.OperatorAssuranceReference,
                out _))
        {
            yield return "ApprovalEvidence is invalid.";
        }

        if (command.ExpectedCaseVersion <= 0)
        {
            yield return "ExpectedCaseVersion must be positive.";
        }

        if (command.ExpectedProcessVersion is <= 0)
        {
            yield return "ExpectedProcessVersion must be positive when supplied.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > TenantTerminationProcess.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}
