namespace BunkFy.Modules.Reservations.Application.Ports;

using Gma.Framework.ProjectionRebuild;

public interface IReservationProcessingRestrictionProjectionRebuildSource
    : IProjectionRebuildSource<ReservationProcessingRestrictionProjectionSnapshot>;

public sealed record ReservationProcessingRestrictionProjectionSnapshot(
    string TenantId,
    Guid PropertyId,
    Guid ReservationId,
    int ContractVersion,
    long Revision,
    int ActiveRestrictionCount,
    DateTimeOffset LastTransitionAtUtc);
