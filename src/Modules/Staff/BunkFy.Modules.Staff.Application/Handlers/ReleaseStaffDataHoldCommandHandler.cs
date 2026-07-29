namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class ReleaseStaffDataHoldCommandHandler(
    IStaffMemberRepository members,
    IStaffDataHoldRepository holds,
    IStaffOperationLock operationLock,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ReleaseStaffDataHoldCommand, StaffDataHoldReceiptDto>
{
    public async Task<Result<StaffDataHoldReceiptDto>> HandleAsync(
        ReleaseStaffDataHoldCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.TenantRequired);
        }

        string? actorId = NormalizeActor(command.ActorId);
        if (command.IdempotencyKey == Guid.Empty ||
            command.StaffMemberId == Guid.Empty ||
            command.HoldId == Guid.Empty ||
            command.ExpectedStaffVersion < 1 ||
            command.ExpectedHoldVersion < 1 ||
            actorId is null)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.DataHoldRequestInvalid);
        }

        StaffDataHoldReceipt? existing =
            await holds.FindReceiptByIdempotencyKeyAsync(
                command.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, command, actorId);
        }

        if (!await operationLock.TryAcquireStaffMemberAsync(
                scopeContext.ScopeId,
                command.StaffMemberId,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        existing = await holds.FindReceiptByIdempotencyKeyAsync(
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, command, actorId);
        }

        StaffMember? member = await members.GetForDataRightsAsync(
            command.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        if (member.Version != command.ExpectedStaffVersion)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.DataHoldStaffVersionConflict);
        }

        StaffDataHold? hold = await holds.GetAsync(
            command.StaffMemberId,
            command.HoldId,
            cancellationToken).ConfigureAwait(false);
        if (hold is null)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.DataHoldNotFound);
        }

        DateTimeOffset nowUtc =
            ToPersistencePrecision(clock.UtcNow);
        Result released = hold.Release(
            command.ExpectedHoldVersion,
            actorId,
            nowUtc);
        if (released.IsFailure)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                released.Error);
        }

        Result<StaffDataHoldReceipt> receipt =
            StaffDataHoldReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                command.IdempotencyKey,
                hold,
                StaffDataHoldAction.Release,
                command.ExpectedStaffVersion,
                actorId,
                nowUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                receipt.Error);
        }

        await holds.AddReceiptAsync(
            receipt.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static Result<StaffDataHoldReceiptDto> Replay(
        StaffDataHoldReceipt receipt,
        ReleaseStaffDataHoldCommand command,
        string actorId)
    {
        if (receipt.Action != StaffDataHoldAction.Release ||
            receipt.HoldId != command.HoldId ||
            receipt.StaffMemberId != command.StaffMemberId ||
            receipt.SelectedStaffVersion !=
                command.ExpectedStaffVersion ||
            receipt.ResultingHoldVersion - 1 !=
                command.ExpectedHoldVersion ||
            !string.Equals(
                receipt.ActorId,
                actorId,
                StringComparison.Ordinal))
        {
            return Result.Failure<StaffDataHoldReceiptDto>(
                StaffApplicationErrors.DataHoldIdempotencyConflict);
        }

        return Result.Success(receipt.ToDto());
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= StaffMember.ActorIdMaxLength &&
            !normalized.Any(char.IsControl)
                ? normalized
                : null;
    }

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
