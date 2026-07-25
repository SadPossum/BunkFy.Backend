namespace BunkFy.Modules.Reservations.Domain.DataRights;

using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Errors;
using BunkFy.Modules.Reservations.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class ReservationProcessingRestriction : ScopedAggregateRoot<Guid>
{
    private ReservationProcessingRestriction() { }

    private ReservationProcessingRestriction(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid PropertyId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid ApplyCaseId { get; private set; }
    public long ApplyApprovalRevision { get; private set; }
    public long ApplySelectedReservationVersion { get; private set; }
    public ReservationProcessingRestrictionStatus Status { get; private set; }
    public long Version { get; private set; } = 1;
    public string AppliedBy { get; private set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public Guid? ReleaseCaseId { get; private set; }
    public long? ReleaseApprovalRevision { get; private set; }
    public long? ReleaseSelectedReservationVersion { get; private set; }
    public string? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public static Result<ReservationProcessingRestriction> Create(
        Guid restrictionId,
        string tenantId,
        Guid propertyId,
        Guid reservationId,
        Guid applyCaseId,
        long applyApprovalRevision,
        long applySelectedReservationVersion,
        string actorId,
        DateTimeOffset appliedAtUtc)
    {
        if (restrictionId == Guid.Empty ||
            propertyId == Guid.Empty ||
            reservationId == Guid.Empty ||
            applyCaseId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<ReservationProcessingRestriction>(
                ReservationsDomainErrors.ProcessingRestrictionIdentityInvalid);
        }

        if (applyApprovalRevision < 1 || applySelectedReservationVersion < 1)
        {
            return Result.Failure<ReservationProcessingRestriction>(
                ReservationsDomainErrors.ProcessingRestrictionApprovalInvalid);
        }

        string? normalizedActorId = NormalizeActor(actorId);
        if (normalizedActorId is null || appliedAtUtc == default)
        {
            return Result.Failure<ReservationProcessingRestriction>(
                ReservationsDomainErrors.ProcessingRestrictionTransitionInvalid);
        }

        return Result.Success(new ReservationProcessingRestriction(restrictionId, scopeId)
        {
            PropertyId = propertyId,
            ReservationId = reservationId,
            ApplyCaseId = applyCaseId,
            ApplyApprovalRevision = applyApprovalRevision,
            ApplySelectedReservationVersion = applySelectedReservationVersion,
            Status = ReservationProcessingRestrictionStatus.Active,
            AppliedBy = normalizedActorId,
            AppliedAtUtc = appliedAtUtc
        });
    }

    public Result Release(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedReservationVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        Result validation = this.ValidateRelease(
            releaseCaseId,
            releaseApprovalRevision,
            releaseSelectedReservationVersion,
            expectedVersion,
            actorId,
            releasedAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        this.ReleaseCaseId = releaseCaseId;
        this.ReleaseApprovalRevision = releaseApprovalRevision;
        this.ReleaseSelectedReservationVersion = releaseSelectedReservationVersion;
        this.ReleasedBy = NormalizeActor(actorId)!;
        this.ReleasedAtUtc = releasedAtUtc;
        this.Status = ReservationProcessingRestrictionStatus.Released;
        this.Version++;
        return Result.Success();
    }

    public Result ValidateRelease(
        Guid releaseCaseId,
        long releaseApprovalRevision,
        long releaseSelectedReservationVersion,
        long expectedVersion,
        string actorId,
        DateTimeOffset releasedAtUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionVersionConflict);
        }

        if (this.Status != ReservationProcessingRestrictionStatus.Active)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionAlreadyReleased);
        }

        if (releaseCaseId == Guid.Empty ||
            releaseApprovalRevision < 1 ||
            releaseSelectedReservationVersion < 1)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionApprovalInvalid);
        }

        string? normalizedActorId = NormalizeActor(actorId);
        if (normalizedActorId is null ||
            releasedAtUtc == default ||
            releasedAtUtc < this.AppliedAtUtc)
        {
            return Result.Failure(
                ReservationsDomainErrors.ProcessingRestrictionTransitionInvalid);
        }

        return Result.Success();
    }

    private static string? NormalizeActor(string? actorId)
    {
        string? normalized = actorId?.Trim();
        return normalized is { Length: > 0 } &&
            normalized.Length <= Reservation.ActorIdMaxLength
            ? normalized
            : null;
    }
}
