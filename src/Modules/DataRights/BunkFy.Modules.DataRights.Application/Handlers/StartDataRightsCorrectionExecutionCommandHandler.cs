namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Mapping;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using DomainSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class StartDataRightsCorrectionExecutionCommandHandler(
    IDataRightsCaseRepository cases,
    IDataRightsCorrectionExecutionRepository executions,
    IEnumerable<IDataRightsCorrectionPolicyContributor> contributors,
    ISystemClock clock)
    : ICommandHandler<
        StartDataRightsCorrectionExecutionCommand,
        DataRightsCorrectionExecutionDto>
{
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(10);

    public async Task<Result<DataRightsCorrectionExecutionDto>> HandleAsync(
        StartDataRightsCorrectionExecutionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Scope is null)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CorrectionExecutionDenied);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            command.Scope,
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        DomainSubject? subject = dataRightsCase.SelectedSubjects.Count == 1
            ? dataRightsCase.SelectedSubjects.First()
            : null;
        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (command.ExecutionId == Guid.Empty ||
            actor.Length is 0 or > DataRightsCase.ActorIdMaxLength ||
            dataRightsCase.PropertyId != command.Scope.PropertyId ||
            (DataRightsCaseType)dataRightsCase.Kind != command.Scope.CaseType ||
            dataRightsCase.RequestedOperations != DataRightsCaseOperation.Correction ||
            dataRightsCase.DecisionRevision is not long approvalRevision ||
            subject is null)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CorrectionExecutionDenied);
        }

        DataRightsCorrectionExecution? existing = await executions.GetByCaseAsync(
            command.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return this.Replay(existing, dataRightsCase, subject, command, actor);
        }

        IDataRightsCorrectionPolicyContributor[] matches =
            [.. contributors.Where(candidate =>
                    candidate.ContractVersion == DataRightsCorrectionContract.CurrentVersion &&
                    string.Equals(
                        candidate.OwnerKey,
                        subject.OwnerKey,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.RecordType,
                        subject.RecordType,
                        StringComparison.Ordinal))
                .Take(2)];
        if (matches.Length != 1)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CorrectionOwnerUnavailable);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result started = dataRightsCase.BeginCorrectionExecution(
            command.ExpectedVersion,
            actor,
            nowUtc);
        if (started.IsFailure)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(started.Error);
        }

        Result<DataRightsCorrectionExecution> created =
            DataRightsCorrectionExecution.Create(
                command.ExecutionId,
                dataRightsCase.ScopeId,
                dataRightsCase.Kind,
                command.Scope.PropertyId,
                command.CaseId,
                command.ExpectedVersion,
                dataRightsCase.Version,
                approvalRevision,
                subject,
                matches[0].FieldPolicyKey,
                actor,
                nowUtc,
                nowUtc.Add(ClaimLifetime));
        if (created.IsFailure)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(created.Error);
        }

        await executions.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Success(dataRightsCase, created.Value);
    }

    private Result<DataRightsCorrectionExecutionDto> Replay(
        DataRightsCorrectionExecution existing,
        DataRightsCase dataRightsCase,
        DomainSubject subject,
        StartDataRightsCorrectionExecutionCommand command,
        string actor)
    {
        if (dataRightsCase.DecisionRevision is not long currentApprovalRevision)
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CorrectionExecutionConflict);
        }

        bool caseMatchesClaim =
            currentApprovalRevision == existing.ApprovalRevision &&
            dataRightsCase.ExecutionRevision == existing.ExecutionRevision &&
            ((existing.State == DataRightsCorrectionExecutionState.Claimed &&
              dataRightsCase.Status == DataRightsCaseState.Executing &&
              dataRightsCase.Version == existing.ExecutionRevision) ||
             (existing.State == DataRightsCorrectionExecutionState.Completed &&
              dataRightsCase.Status == DataRightsCaseState.Completed &&
              dataRightsCase.Version == existing.ExecutionRevision + 1));
        if (!caseMatchesClaim ||
            !existing.MatchesClaim(
                command.ExecutionId,
                dataRightsCase.Kind,
                command.Scope.PropertyId,
                command.CaseId,
                command.ExpectedVersion,
                currentApprovalRevision,
                subject,
                existing.FieldPolicyKey,
                actor))
        {
            return Result.Failure<DataRightsCorrectionExecutionDto>(
                DataRightsApplicationErrors.CorrectionExecutionConflict);
        }

        if (existing.State == DataRightsCorrectionExecutionState.Claimed &&
            clock.UtcNow > existing.ExpiresAtUtc)
        {
            Result renewed = existing.Renew(
                actor,
                clock.UtcNow,
                clock.UtcNow.Add(ClaimLifetime));
            if (renewed.IsFailure)
            {
                return Result.Failure<DataRightsCorrectionExecutionDto>(renewed.Error);
            }
        }

        return Success(dataRightsCase, existing);
    }

    private static Result<DataRightsCorrectionExecutionDto> Success(
        DataRightsCase dataRightsCase,
        DataRightsCorrectionExecution execution) =>
        Result.Success(new DataRightsCorrectionExecutionDto(
            dataRightsCase.ToDto(),
            execution.ToDto()));
}
