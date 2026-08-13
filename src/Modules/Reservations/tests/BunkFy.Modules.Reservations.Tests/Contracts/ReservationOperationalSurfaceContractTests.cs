namespace BunkFy.Modules.Reservations.Tests.Contracts;

using BunkFy.Modules.Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationOperationalSurfaceContractTests
{
    [Fact]
    public void Mutation_receipt_contains_only_identity_state_and_concurrency_fields()
    {
        AssertProperties<ReservationMutationReceiptDto>(
            "DetailsRevision",
            "PropertyId",
            "ReservationId",
            "Status",
            "Version");
    }

    [Fact]
    public void Directory_item_contains_only_operational_summary_fields()
    {
        AssertProperties<ReservationListItemDto>(
            "Arrival",
            "Departure",
            "ExpectedArrivalTime",
            "ExpectedDepartureTime",
            "GuestCount",
            "InventoryUnitCount",
            "PrimaryGuestName",
            "PropertyId",
            "ReservationId",
            "SourceKind",
            "Status");
    }

    [Fact]
    public void List_and_history_envelopes_publish_lookahead_pagination()
    {
        AssertProperties<ReservationListResponse>("HasMore", "Page", "PageSize", "Reservations");
        AssertProperties<ReservationDetailsHistoryListResponse>("HasMore", "Items", "Page", "PageSize");
    }

    [Fact]
    public void Operations_snapshot_contract_is_explicit_bounded_and_property_local()
    {
        AssertProperties<ReservationOperationsCountDto>("GuestCount", "ReservationCount");
        AssertProperties<ReservationOperationsCohortCountsDto>(
            "ConfirmedArrivalsOnLocalDate",
            "CurrentlyInHouse",
            "ScheduledDeparturesOnLocalDate");
        AssertProperties<ReservationOperationsAttentionCountsDto>(
            "AllocationRejected",
            "ArrivalBeforeLocalDateStillConfirmed",
            "CancellationPending",
            "CheckoutPending",
            "DepartureBeforeLocalDateStillInHouse",
            "NoShowPending",
            "PendingAllocation",
            "Total");
        AssertProperties<ReservationOperationsSnapshotDto>(
            "Attention",
            "Cohorts",
            "DateSource",
            "HasMoreUpcoming",
            "LocalDate",
            "ObservedAtUtc",
            "PropertyId",
            "TimeZoneId",
            "Upcoming",
            "UpcomingLimit");

        Assert.Equal(25, ReservationsContractLimits.DefaultOperationsSnapshotUpcomingLimit);
        Assert.Equal(50, ReservationsContractLimits.MaximumOperationsSnapshotUpcomingLimit);
        Assert.False(
            typeof(ReservationOperationsSnapshotDto)
                .GetProperty(nameof(ReservationOperationsSnapshotDto.TimeZoneId))!
                .PropertyType.IsGenericType);
    }

    private static void AssertProperties<T>(params string[] expected)
    {
        string[] actual = typeof(T)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
