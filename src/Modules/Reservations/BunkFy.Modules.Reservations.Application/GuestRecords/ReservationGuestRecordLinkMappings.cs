namespace BunkFy.Modules.Reservations.Application.GuestRecords;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using ContractReviewReason = Contracts.ReservationGuestRecordLinkReviewReason;
using DomainReviewReason = Domain.GuestRecords.ReservationGuestRecordLinkReviewReason;

internal static class ReservationGuestRecordLinkMappings
{
    public static ReservationGuestRecordLinkPreparationDto ToPreparationDto(
        this ReservationGuestRecordLinkProcess process) =>
        new(process.ToDto(), process.CreationConfirmationId);

    public static ReservationGuestRecordLinkProcessDto ToDto(
        this ReservationGuestRecordLinkProcess process) =>
        new(
            process.Id,
            process.PropertyId,
            process.ReservationId,
            process.Id,
            MapStatus(process.State),
            MapReviewReason(process.ReviewReason),
            process.Revision,
            process.DispatchRevision,
            process.CreatedAtUtc,
            process.UpdatedAtUtc);

    private static ReservationGuestRecordLinkStatus MapStatus(
        ReservationGuestRecordLinkProcessState state) => state switch
        {
            ReservationGuestRecordLinkProcessState.Prepared =>
                ReservationGuestRecordLinkStatus.Prepared,
            ReservationGuestRecordLinkProcessState.Ready =>
                ReservationGuestRecordLinkStatus.Ready,
            ReservationGuestRecordLinkProcessState.Completed =>
                ReservationGuestRecordLinkStatus.Completed,
            ReservationGuestRecordLinkProcessState.NeedsReview =>
                ReservationGuestRecordLinkStatus.NeedsReview,
            _ => ReservationGuestRecordLinkStatus.Unknown
        };

    private static ContractReviewReason MapReviewReason(
        DomainReviewReason reason) => reason switch
        {
            DomainReviewReason.None => ContractReviewReason.None,
            DomainReviewReason.ReservationUnavailable =>
                ContractReviewReason.ReservationUnavailable,
            DomainReviewReason.PrimaryGuestOccupied =>
                ContractReviewReason.PrimaryGuestOccupied,
            DomainReviewReason.GuestUnavailable => ContractReviewReason.GuestUnavailable,
            DomainReviewReason.GuestRestricted => ContractReviewReason.GuestRestricted,
            DomainReviewReason.CountryPolicyDenied => ContractReviewReason.CountryPolicyDenied,
            DomainReviewReason.RetryLimitReached => ContractReviewReason.RetryLimitReached,
            _ => ContractReviewReason.Unknown
        };
}
