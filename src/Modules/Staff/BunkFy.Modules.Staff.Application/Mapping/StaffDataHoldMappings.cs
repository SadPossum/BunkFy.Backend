namespace BunkFy.Modules.Staff.Application.Mapping;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;

internal static class StaffDataHoldMappings
{
    public static StaffDataHoldDto ToDto(this StaffDataHold hold) =>
        new(
            hold.Id,
            hold.StaffMemberId,
            hold.ReasonCode,
            hold.State switch
            {
                StaffDataHoldState.Active =>
                    StaffDataHoldStatus.Active,
                StaffDataHoldState.Released =>
                    StaffDataHoldStatus.Released,
                _ => StaffDataHoldStatus.Unknown
            },
            hold.PlacedBy,
            hold.PlacedAtUtc,
            hold.ReleasedBy,
            hold.ReleasedAtUtc,
            hold.Version);

    public static StaffDataHoldReceiptDto ToDto(
        this StaffDataHoldReceipt receipt) =>
        new(
            receipt.Id,
            receipt.IdempotencyKey,
            receipt.HoldId,
            receipt.Action switch
            {
                StaffDataHoldAction.Place =>
                    StaffDataHoldActionDto.Place,
                StaffDataHoldAction.Release =>
                    StaffDataHoldActionDto.Release,
                _ => StaffDataHoldActionDto.Unknown
            },
            receipt.StaffMemberId,
            receipt.ReasonCode,
            receipt.SelectedStaffVersion,
            receipt.ResultingHoldVersion,
            receipt.ActorId,
            receipt.CompletedAtUtc);
}
