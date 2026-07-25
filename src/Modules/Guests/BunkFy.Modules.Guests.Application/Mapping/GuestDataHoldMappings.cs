namespace BunkFy.Modules.Guests.Application.Mapping;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;

internal static class GuestDataHoldMappings
{
    public static GuestDataHoldDto ToDto(this GuestDataHold hold) => new(
        hold.Id,
        hold.PropertyId,
        hold.GuestId,
        hold.ReasonCode,
        (GuestDataHoldStatus)hold.State,
        hold.PlacedBy,
        hold.PlacedAtUtc,
        hold.ReleasedBy,
        hold.ReleasedAtUtc,
        hold.Version);

    public static GuestDataHoldReceiptDto ToDto(this GuestDataHoldReceipt receipt) => new(
        receipt.Id,
        receipt.IdempotencyKey,
        receipt.HoldId,
        (GuestDataHoldAction)receipt.Action,
        receipt.PropertyId,
        receipt.GuestId,
        receipt.ReasonCode,
        receipt.SelectedGuestVersion,
        receipt.ResultingHoldVersion,
        receipt.ActorId,
        receipt.CompletedAtUtc);
}
