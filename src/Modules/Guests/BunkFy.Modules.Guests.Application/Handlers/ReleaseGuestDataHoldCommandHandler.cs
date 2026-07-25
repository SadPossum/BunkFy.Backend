namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using DomainGuestDataHoldAction = Domain.Models.GuestDataHoldAction;

internal sealed class ReleaseGuestDataHoldCommandHandler(
    IGuestProfileRepository profiles,
    IGuestDataHoldRepository holds,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ReleaseGuestDataHoldCommand, GuestDataHoldReceiptDto>
{
    public async Task<Result<GuestDataHoldReceiptDto>> HandleAsync(
        ReleaseGuestDataHoldCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.TenantRequired);
        }

        string? actorId = NormalizeActor(command.ActorId);
        if (command.IdempotencyKey == Guid.Empty ||
            command.PropertyId == Guid.Empty ||
            command.GuestId == Guid.Empty ||
            command.HoldId == Guid.Empty ||
            command.ExpectedGuestVersion < 1 ||
            command.ExpectedHoldVersion < 1 ||
            actorId is null)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.DataHoldRequestInvalid);
        }

        GuestDataHoldReceipt? existing = await holds.FindReceiptByIdempotencyKeyAsync(
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, command, actorId);
        }

        GuestProfile? profile = await profiles.GetForDataRightsAsync(
            command.PropertyId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.GuestNotFound);
        }

        if (profile.Version != command.ExpectedGuestVersion)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.DataHoldGuestVersionConflict);
        }

        GuestDataHold? hold = await holds.GetAsync(
            command.PropertyId,
            command.GuestId,
            command.HoldId,
            cancellationToken).ConfigureAwait(false);
        if (hold is null)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.DataHoldNotFound);
        }

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Result released = hold.Release(command.ExpectedHoldVersion, actorId, nowUtc);
        if (released.IsFailure)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(released.Error);
        }

        Result<GuestDataHoldReceipt> receipt = GuestDataHoldReceipt.Create(
            ids.NewId(),
            scopeContext.ScopeId,
            command.IdempotencyKey,
            hold,
            DomainGuestDataHoldAction.Release,
            command.ExpectedGuestVersion,
            actorId,
            nowUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(receipt.Error);
        }

        await holds.AddReceiptAsync(receipt.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static Result<GuestDataHoldReceiptDto> Replay(
        GuestDataHoldReceipt receipt,
        ReleaseGuestDataHoldCommand command,
        string actorId)
    {
        if (receipt.Action != DomainGuestDataHoldAction.Release ||
            receipt.HoldId != command.HoldId ||
            receipt.PropertyId != command.PropertyId ||
            receipt.GuestId != command.GuestId ||
            receipt.SelectedGuestVersion != command.ExpectedGuestVersion ||
            receipt.ResultingHoldVersion - 1 != command.ExpectedHoldVersion ||
            !string.Equals(receipt.ActorId, actorId, StringComparison.Ordinal))
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.DataHoldIdempotencyConflict);
        }

        return Result.Success(receipt.ToDto());
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= GuestProfile.ActorIdMaxLength
            ? normalized
            : null;
    }

    private static DateTimeOffset ToPersistencePrecision(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new(value.Ticks - (value.Ticks % ticksPerMicrosecond), value.Offset);
    }
}
