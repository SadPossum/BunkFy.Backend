namespace BunkFy.Modules.Reservations.Application.Mapping;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Models;
using ContractDataHoldAction = Contracts.ReservationDataHoldAction;
using ContractDataHoldStatus = Contracts.ReservationDataHoldStatus;
using DomainDataHoldAction = Domain.Models.ReservationDataHoldAction;

internal static class ReservationDataHoldMappings
{
    public static ReservationDataHoldDto ToDto(this ReservationDataHold hold) =>
        new(
            hold.Id,
            hold.PropertyId,
            hold.ReservationId,
            hold.ReasonCode,
            hold.State == ReservationDataHoldState.Active
                ? ContractDataHoldStatus.Active
                : ContractDataHoldStatus.Released,
            hold.PlacedBy,
            hold.PlacedAtUtc,
            hold.ReleasedBy,
            hold.ReleasedAtUtc,
            hold.Version);

    public static ReservationDataHoldReceiptDto ToDto(
        this ReservationDataHoldReceipt receipt) =>
        new(
            receipt.Id,
            receipt.HoldId,
            receipt.Action == DomainDataHoldAction.Place
                ? ContractDataHoldAction.Place
                : ContractDataHoldAction.Release,
            receipt.PropertyId,
            receipt.ReservationId,
            receipt.ReasonCode,
            receipt.SelectedReservationVersion,
            receipt.SelectedDetailsRevision,
            receipt.ResultingHoldVersion,
            receipt.CompletedAtUtc);
}
