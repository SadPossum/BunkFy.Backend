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

internal sealed class PlaceGuestDataHoldCommandHandler(
    IGuestProfileRepository profiles,
    IGuestDataHoldRepository holds,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<PlaceGuestDataHoldCommand, GuestDataHoldReceiptDto>
{
    public async Task<Result<GuestDataHoldReceiptDto>> HandleAsync(
        PlaceGuestDataHoldCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.TenantRequired);
        }

        string? actorId = NormalizeActor(command.ActorId);
        string reasonCode = command.ReasonCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (command.IdempotencyKey == Guid.Empty ||
            command.PropertyId == Guid.Empty ||
            command.GuestId == Guid.Empty ||
            command.ExpectedGuestVersion < 1 ||
            actorId is null ||
            !GuestDataHoldReasonCodes.All.Contains(reasonCode))
        {
            return Result.Failure<GuestDataHoldReceiptDto>(
                GuestsApplicationErrors.DataHoldRequestInvalid);
        }

        GuestDataHoldReceipt? existing = await holds.FindReceiptByIdempotencyKeyAsync(
            command.IdempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Replay(existing, command, actorId, reasonCode);
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

        DateTimeOffset nowUtc = ToPersistencePrecision(clock.UtcNow);
        Result<GuestDataHold> placed = GuestDataHold.Place(
            ids.NewId(),
            scopeContext.ScopeId,
            command.PropertyId,
            command.GuestId,
            reasonCode,
            actorId,
            nowUtc);
        if (placed.IsFailure)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(placed.Error);
        }

        Result<GuestDataHoldReceipt> receipt = GuestDataHoldReceipt.Create(
            ids.NewId(),
            scopeContext.ScopeId,
            command.IdempotencyKey,
            placed.Value,
            DomainGuestDataHoldAction.Place,
            command.ExpectedGuestVersion,
            actorId,
            nowUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<GuestDataHoldReceiptDto>(receipt.Error);
        }

        await holds.AddAsync(placed.Value, cancellationToken).ConfigureAwait(false);
        await holds.AddReceiptAsync(receipt.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static Result<GuestDataHoldReceiptDto> Replay(
        GuestDataHoldReceipt receipt,
        PlaceGuestDataHoldCommand command,
        string actorId,
        string reasonCode)
    {
        if (receipt.Action != DomainGuestDataHoldAction.Place ||
            receipt.PropertyId != command.PropertyId ||
            receipt.GuestId != command.GuestId ||
            receipt.SelectedGuestVersion != command.ExpectedGuestVersion ||
            receipt.ResultingHoldVersion != 1 ||
            !string.Equals(receipt.ReasonCode, reasonCode, StringComparison.Ordinal) ||
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
