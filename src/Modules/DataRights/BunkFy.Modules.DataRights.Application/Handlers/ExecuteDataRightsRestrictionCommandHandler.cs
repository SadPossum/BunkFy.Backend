namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using SelectedSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class ExecuteDataRightsRestrictionCommandHandler(
    DataRightsCaseMutationCoordinator mutations,
    IDataRightsOperationApprovalGate approvalGate,
    IEnumerable<IDataRightsRestrictionContributor> contributors,
    ISystemClock clock)
    : ICommandHandler<
        ExecuteDataRightsRestrictionCommand,
        DataRightsRestrictionExecutionDto>
{
    private static readonly TimeSpan OwnerDeadline = TimeSpan.FromSeconds(30);

    public async Task<Result<DataRightsRestrictionExecutionDto>> HandleAsync(
        ExecuteDataRightsRestrictionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Scope is null)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.RestrictionExecutionDenied);
        }

        DataRightsCase? dataRightsCase = await mutations.AcquireAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        SelectedSubject? subject = dataRightsCase.SelectedSubjects.Count == 1
            ? dataRightsCase.SelectedSubjects.First()
            : null;
        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (command.IdempotencyKey == Guid.Empty ||
            actor.Length is 0 or > DataRightsCase.ActorIdMaxLength ||
            dataRightsCase.Kind != (DataRightsCaseKind)command.Scope.CaseType ||
            dataRightsCase.PropertyId != command.Scope.PropertyId ||
            dataRightsCase.RequestedOperations !=
                DataRightsCaseOperation.Restriction ||
            dataRightsCase.DecisionRevision is not long approvalRevision ||
            dataRightsCase.RestrictionAction is not
                DataRightsRestrictionAction.Apply and not
                DataRightsRestrictionAction.Release ||
            subject is null)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.RestrictionExecutionDenied);
        }

        if (dataRightsCase.RestrictionExecutionProof is { } existing)
        {
            return existing.Matches(
                command.IdempotencyKey,
                approvalRevision,
                dataRightsCase.RestrictionAction,
                subject,
                actor)
                ? Result.Success(new DataRightsRestrictionExecutionDto(
                    dataRightsCase.ToDto(),
                    existing.ToDto()))
                : Result.Failure<DataRightsRestrictionExecutionDto>(
                    DataRightsApplicationErrors.RestrictionExecutionConflict);
        }

        DataRightsRestrictionDirective directive =
            (DataRightsRestrictionDirective)dataRightsCase.RestrictionAction;
        DataRightsOperationApprovalResult approval =
            await approvalGate.EvaluateAsync(
                new DataRightsOperationApprovalRequest(
                    dataRightsCase.ScopeId,
                    command.Scope.PropertyId,
                    command.CaseId,
                    approvalRevision,
                    DataRightsOperation.Restriction,
                    subject.OwnerKey,
                    subject.RecordType,
                    subject.RecordId,
                    subject.RecordVersion,
                    directive,
                    CaseType: command.Scope.CaseType),
                cancellationToken).ConfigureAwait(false);
        if (!approval.IsApproved)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.RestrictionExecutionDenied);
        }

        IDataRightsRestrictionContributor[] matchingContributors =
            [.. contributors
                .Where(candidate =>
                    string.Equals(
                        candidate.OwnerKey,
                        subject.OwnerKey,
                        StringComparison.Ordinal) &&
                    candidate.ContractVersion ==
                        DataRightsRestrictionContract.CurrentVersion)
                .Take(2)];
        if (matchingContributors.Length != 1)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.RestrictionOwnerUnavailable);
        }
        IDataRightsRestrictionContributor contributor = matchingContributors[0];

        DateTimeOffset nowUtc = clock.UtcNow;
        DataRightsRestrictionContributionResult ownerResult;
        try
        {
            ownerResult = await contributor.ExecuteAsync(
                new DataRightsRestrictionContributionRequest(
                    DataRightsRestrictionContract.CurrentVersion,
                    dataRightsCase.ScopeId,
                    command.IdempotencyKey,
                    command.Scope.PropertyId,
                    command.CaseId,
                    approvalRevision,
                    new BunkFy.Modules.DataRights.Contracts.DataRightsSubjectCoordinate(
                        subject.OwnerKey,
                        subject.RecordType,
                        subject.RecordId,
                        subject.RecordVersion),
                    directive,
                    actor,
                    nowUtc.Add(OwnerDeadline),
                    command.Scope.CaseType),
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                DataRightsApplicationErrors.RestrictionOwnerUnavailable);
        }

        if (!TryValidate(ownerResult, directive, out DataRightsRestrictionOwnerProof ownerProof))
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(
                ownerResult.Status == DataRightsRestrictionContributionStatus.Blocked
                    ? DataRightsApplicationErrors.RestrictionExecutionBlocked
                    : DataRightsApplicationErrors.RestrictionOwnerProofInvalid);
        }

        Result<DataRightsRestrictionExecutionProof> proof =
            DataRightsRestrictionExecutionProof.Create(
                command.IdempotencyKey,
                approvalRevision,
                dataRightsCase.RestrictionAction,
                subject,
                ownerProof.ReceiptContractVersion,
                ownerProof.ReceiptId,
                ownerProof.OwnerOperationId,
                ownerProof.ResultingOwnerRevision,
                ownerProof.ResultingProjectionRevision,
                ownerProof.EffectiveRestricted,
                ownerProof.ReceiptSha256,
                actor,
                ownerProof.CompletedAtUtc);
        if (proof.IsFailure)
        {
            return Result.Failure<DataRightsRestrictionExecutionDto>(proof.Error);
        }

        Result completed = dataRightsCase.CompleteRestrictionExecution(
            command.ExpectedVersion,
            proof.Value,
            clock.UtcNow);
        return completed.IsSuccess
            ? Result.Success(new DataRightsRestrictionExecutionDto(
                dataRightsCase.ToDto(),
                proof.Value.ToDto()))
            : Result.Failure<DataRightsRestrictionExecutionDto>(completed.Error);
    }

    private static bool TryValidate(
        DataRightsRestrictionContributionResult? result,
        DataRightsRestrictionDirective directive,
        out DataRightsRestrictionOwnerProof proof)
    {
        proof = result?.OwnerProof!;
        bool expectedState = directive == DataRightsRestrictionDirective.Apply;
        return result is not null &&
            result.ContractVersion == DataRightsRestrictionContract.CurrentVersion &&
            result.Status == DataRightsRestrictionContributionStatus.Completed &&
            result.OutcomeCode is null &&
            proof is not null &&
            proof.ReceiptContractVersion > 0 &&
            proof.ReceiptId != Guid.Empty &&
            proof.OwnerOperationId != Guid.Empty &&
            proof.ResultingOwnerRevision > 0 &&
            proof.ResultingProjectionRevision > 0 &&
            proof.EffectiveRestricted == expectedState &&
            proof.ReceiptSha256.Length == DataRightsRestrictionContract.Sha256Length &&
            proof.ReceiptSha256.All(Uri.IsHexDigit) &&
            proof.CompletedAtUtc != default;
    }
}
