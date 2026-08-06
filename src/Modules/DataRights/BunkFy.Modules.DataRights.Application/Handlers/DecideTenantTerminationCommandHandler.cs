namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Production;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class DecideTenantTerminationCommandHandler(
    DataRightsCaseMutationCoordinator mutations,
    ITenantTerminationProductionCatalog productionCatalog,
    ITenantTerminationRequiredOwnerCatalog requiredOwners,
    IScopeContext scopeContext,
    ISystemClock clock)
    : ICommandHandler<DecideTenantTerminationCommand,
        TenantTerminationCaseDto>
{
    public async Task<Result<TenantTerminationCaseDto>> HandleAsync(
        DecideTenantTerminationCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<TenantTerminationCaseDto>(
                DataRightsApplicationErrors.TenantRequired);
        }

        DataRightsCase? dataRightsCase =
            await mutations.AcquireTenantTerminationCaseAsync(
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<TenantTerminationCaseDto>(
                DataRightsApplicationErrors.TenantTerminationCaseNotFound);
        }

        Result<DecisionEvidence> evidence = NormalizeEvidence(command);
        if (evidence.IsFailure)
        {
            return Result.Failure<TenantTerminationCaseDto>(evidence.Error);
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (dataRightsCase.Decision != DataRightsCaseDecision.Unknown)
        {
            return MatchesRecordedDecision(
                    dataRightsCase,
                    command,
                    evidence.Value.PolicyEvidenceSha256,
                    actor)
                    ? Result.Success(
                        dataRightsCase.ToTenantTerminationDto())
                    : Result.Failure<TenantTerminationCaseDto>(
                        DataRightsApplicationErrors
                            .TenantTerminationDecisionConflict);
        }

        if (evidence.Value.ApprovalEvidence is not null)
        {
            Result<TenantTerminationProductionCatalogEvidence> catalog =
                productionCatalog.Validate(requiredOwners.RequiredOwnerKeys);
            if (catalog.IsFailure)
            {
                return Result.Failure<TenantTerminationCaseDto>(
                    catalog.Error);
            }

            if (!TenantTerminationApprovalEvidenceContract
                    .FixedTimeSha256Equals(
                        evidence.Value.ApprovalEvidence.OwnerCatalogSha256,
                        catalog.Value.CatalogSha256))
            {
                return Result.Failure<TenantTerminationCaseDto>(
                    DataRightsApplicationErrors
                        .TenantTerminationApprovalEvidenceInvalid);
            }
        }

        Result recorded = dataRightsCase.RecordTenantTerminationDecision(
            (DataRightsCaseDecision)command.Decision,
            (DataRightsCaseDecisionReason)command.Reason,
            evidence.Value.PolicyEvidenceSha256,
            command.ExpectedVersion,
            actor,
            clock.UtcNow);
        return recorded.IsSuccess
            ? Result.Success(dataRightsCase.ToTenantTerminationDto())
            : Result.Failure<TenantTerminationCaseDto>(recorded.Error);
    }

    private static Result<DecisionEvidence> NormalizeEvidence(
        DecideTenantTerminationCommand command)
    {
        if (command.Decision == DataRightsDecisionOutcome.Denied)
        {
            return command.ApprovalEvidence is null
                ? Result.Success(new DecisionEvidence(null, null))
                : Result.Failure<DecisionEvidence>(
                    DataRightsApplicationErrors
                        .TenantTerminationApprovalEvidenceInvalid);
        }

        TenantTerminationApprovalEvidence? supplied =
            command.ApprovalEvidence;
        if (command.Decision != DataRightsDecisionOutcome.Approved ||
            supplied is null ||
            !TenantTerminationApprovalEvidenceContract.TryCreate(
                supplied.ApprovalReference,
                supplied.OwnerCatalogSha256,
                supplied.BackupEvidenceReference,
                supplied.RestoreDrillEvidenceReference,
                supplied.OperatorAssuranceReference,
                out TenantTerminationApprovalEvidence? normalized))
        {
            return Result.Failure<DecisionEvidence>(
                DataRightsApplicationErrors
                    .TenantTerminationApprovalEvidenceInvalid);
        }

        return Result.Success(new DecisionEvidence(
            normalized,
            normalized!.ComputeSha256()));
    }

    private static bool MatchesRecordedDecision(
        DataRightsCase dataRightsCase,
        DecideTenantTerminationCommand command,
        string? policyEvidenceSha256,
        string actor) =>
        dataRightsCase.Decision ==
            (DataRightsCaseDecision)command.Decision &&
        dataRightsCase.DecisionReason ==
            (DataRightsCaseDecisionReason)command.Reason &&
        string.Equals(
            dataRightsCase.DecidedBy,
            actor,
            StringComparison.Ordinal) &&
        (command.Decision == DataRightsDecisionOutcome.Denied
            ? dataRightsCase.TenantTerminationPolicyEvidenceSha256 is null &&
                policyEvidenceSha256 is null
            : TenantTerminationApprovalEvidenceContract
                .FixedTimeSha256Equals(
                    dataRightsCase
                        .TenantTerminationPolicyEvidenceSha256,
                    policyEvidenceSha256));

    private sealed record DecisionEvidence(
        TenantTerminationApprovalEvidence? ApprovalEvidence,
        string? PolicyEvidenceSha256);
}
