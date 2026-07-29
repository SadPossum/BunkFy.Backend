namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ApplyStaffDataRightsCorrectionCommandHandler(
    IStaffMemberRepository members,
    IStaffDataRightsCorrectionReceiptRepository receipts,
    IDataRightsCorrectionExecutionGate executionGate,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ApplyStaffDataRightsCorrectionCommand,
        StaffDataRightsCorrectionReceiptDto>
{
    public async Task<Result<StaffDataRightsCorrectionReceiptDto>> HandleAsync(
        ApplyStaffDataRightsCorrectionCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                StaffApplicationErrors.TenantRequired);
        }

        if (command.ExecutionId == Guid.Empty ||
            command.CaseId == Guid.Empty ||
            command.ApprovalRevision < 1 ||
            command.StaffMemberId == Guid.Empty ||
            command.ExpectedVersion < 1)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                StaffApplicationErrors.CorrectionRequestInvalid);
        }

        Result<StaffProfileCorrection> requested = StaffProfileCorrection.Create(
            command.DisplayName,
            command.LegalName,
            command.WorkEmail,
            command.WorkPhone,
            command.EmployeeNumber,
            command.JobTitle,
            command.Department);
        if (requested.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                requested.Error);
        }

        string requestSha256 =
            StaffDataRightsCorrectionFingerprint.Compute(requested.Value);
        StaffDataRightsCorrectionReceipt? existing =
            await receipts.FindByExecutionIdAsync(
                command.ExecutionId,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesReplay(
                command.CaseId,
                command.ApprovalRevision,
                command.StaffMemberId,
                command.ExpectedVersion,
                requestSha256)
                ? Result.Success(existing.ToDto())
                : Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                    StaffApplicationErrors.CorrectionIdempotencyConflict);
        }

        DataRightsCorrectionExecutionGateResult execution =
            await executionGate.EvaluateAsync(
                new DataRightsCorrectionExecutionGateRequest(
                    scopeContext.ScopeId,
                    DataRightsCaseType.StaffRights,
                    PropertyId: null,
                    command.CaseId,
                    command.ApprovalRevision,
                    command.ExecutionId,
                    new DataRightsSubjectCoordinate(
                        StaffDataRightsCoordinates.Owner,
                        StaffDataRightsCoordinates.StaffMemberRecordType,
                        command.StaffMemberId,
                        command.ExpectedVersion),
                    StaffDataRightsCoordinates.CorrectionFieldPolicyKey,
                    command.ActorId),
                cancellationToken).ConfigureAwait(false);
        if (!execution.IsAllowed)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                StaffApplicationErrors.DataRightsApprovalRequired);
        }

        StaffMember? member = await members.GetAsync(
            command.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        Result unique = await StaffMemberUniqueness.EnsureAsync(
            members,
            requested.Value.EmployeeNumber,
            authSubjectId: null,
            member.Id,
            cancellationToken).ConfigureAwait(false);
        if (unique.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                unique.Error);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Result<StaffDataRightsCorrectionOutcome> updated =
            member.ApplyDataRightsCorrection(
                requested.Value,
                command.ExpectedVersion,
                command.ActorId,
                ids.NewId(),
                nowUtc);
        if (updated.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                updated.Error);
        }

        Result<StaffDataRightsCorrectionReceipt> created =
            StaffDataRightsCorrectionReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                command.ExecutionId,
                command.CaseId,
                command.ApprovalRevision,
                command.StaffMemberId,
                updated.Value.PreviousVersion,
                updated.Value.CurrentVersion,
                updated.Value.ChangedFields,
                requestSha256,
                updated.Value.EventId,
                ids.NewId(),
                updated.Value.OccurredAtUtc);
        if (created.IsFailure)
        {
            return Result.Failure<StaffDataRightsCorrectionReceiptDto>(
                created.Error);
        }

        await receipts.AddAsync(created.Value, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
