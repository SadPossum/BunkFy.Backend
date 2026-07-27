namespace BunkFy.Modules.Inventory.Domain.Aggregates;

using BunkFy.Modules.Inventory.Domain.Errors;
using Gma.Framework.Results;

public sealed partial class InventoryAllocation
{
    public Result<InventoryAllocationAnonymisationOutcome> Anonymise(
        long expectedVersion,
        Guid reservationPseudonym,
        DateTimeOffset completedAtUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors.VersionConflict);
        }

        if (this.IsAnonymised)
        {
            return Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors.AllocationAlreadyAnonymised);
        }

        if (this.Status is not (
                InventoryAllocationState.Released or
                InventoryAllocationState.Rejected))
        {
            return Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors
                    .AllocationNotEligibleForAnonymisation);
        }

        DateTimeOffset completed = completedAtUtc.ToUniversalTime();
        if (reservationPseudonym == Guid.Empty ||
            reservationPseudonym == this.ReservationId ||
            completedAtUtc == default)
        {
            return Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors
                    .AllocationAnonymisationProvenanceInvalid);
        }

        long selectedVersion = this.Version;
        this.ReservationId = reservationPseudonym;
        this.IsAnonymised = true;
        this.AnonymisedAtUtc = completed;
        this.Version++;
        return Result.Success(new InventoryAllocationAnonymisationOutcome(
            selectedVersion,
            this.Version,
            reservationPseudonym,
            completed));
    }

    public Result<InventoryAllocationAnonymisationOutcome>
        RestoreAnonymisation(
            long expectedResultingVersion,
            Guid reservationPseudonym,
            DateTimeOffset originallyCompletedAtUtc)
    {
        if (this.IsAnonymised ||
            expectedResultingVersion <= 1 ||
            this.Version != expectedResultingVersion - 1)
        {
            return Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors
                    .AllocationAnonymisationRestoreStateInvalid);
        }

        Result<InventoryAllocationAnonymisationOutcome> restored =
            this.Anonymise(
                this.Version,
                reservationPseudonym,
                originallyCompletedAtUtc);
        return restored.IsSuccess &&
            restored.Value.ResultingVersion == expectedResultingVersion
            ? restored
            : Result.Failure<InventoryAllocationAnonymisationOutcome>(
                InventoryDomainErrors
                    .AllocationAnonymisationRestoreStateInvalid);
    }

    public bool MatchesAnonymisedState(
        long expectedVersion,
        Guid reservationPseudonym,
        DateTimeOffset completedAtUtc) =>
        this.IsAnonymised &&
        this.Version == expectedVersion &&
        this.ReservationId == reservationPseudonym &&
        this.AnonymisedAtUtc == completedAtUtc.ToUniversalTime();
}

public sealed record InventoryAllocationAnonymisationOutcome(
    long SelectedVersion,
    long ResultingVersion,
    Guid ResultingReservationPseudonym,
    DateTimeOffset CompletedAtUtc);
