namespace BunkFy.Modules.Inventory.Application.Handlers;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Results;

internal sealed class InventoryManagementOperationJournal(
    IInventoryManagementOperationRepository operations)
{
    public async Task<InventoryManagementReplayDecision> InspectRoomAsync(
        Guid propertyId,
        Guid roomId,
        Guid operationId,
        long expectedVersion,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        InventoryManagementOperationRecord? existing =
            await operations.GetAsync(
                InventoryManagementResourceKind.Room,
                roomId,
                operationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return InventoryManagementReplayDecision.Missing;
        }

        return existing.Matches(
            InventoryManagementMutationKind.RoomSalesModeConfiguration,
            propertyId,
            InventoryManagementResourceKind.Room,
            roomId,
            expectedVersion,
            fingerprint)
            ? InventoryManagementReplayDecision.Exact(
                existing.ToRoomReceipt())
            : InventoryManagementReplayDecision.Conflict;
    }

    public async Task<RoomInventoryMutationReceiptDto> RecordRoomAsync(
        RoomInventoryConfiguration configuration,
        Guid operationId,
        long expectedVersion,
        string fingerprint,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        InventorySalesMode salesMode = configuration.SalesMode switch
        {
            RoomSalesMode.RoomLevel => InventorySalesMode.RoomLevel,
            RoomSalesMode.BedLevel => InventorySalesMode.BedLevel,
            _ => InventorySalesMode.Unknown
        };
        RoomInventoryMutationReceiptDto receipt = new(
            configuration.PropertyId,
            configuration.Id,
            salesMode,
            configuration.Version);
        await operations.AddAsync(
            new InventoryManagementOperationRecord(
                operationId,
                configuration.ScopeId,
                configuration.PropertyId,
                InventoryManagementResourceKind.Room,
                configuration.Id,
                InventoryManagementMutationKind.RoomSalesModeConfiguration,
                expectedVersion,
                fingerprint,
                receipt.SalesMode,
                receipt.Version,
                completedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }
}

internal readonly record struct InventoryManagementReplayDecision(
    InventoryManagementReplayOutcome Outcome,
    RoomInventoryMutationReceiptDto? Receipt)
{
    public static InventoryManagementReplayDecision Missing => new(
        InventoryManagementReplayOutcome.Missing,
        null);

    public static InventoryManagementReplayDecision Conflict => new(
        InventoryManagementReplayOutcome.Conflict,
        null);

    public static InventoryManagementReplayDecision Exact(
        RoomInventoryMutationReceiptDto receipt) => new(
        InventoryManagementReplayOutcome.Exact,
        receipt);

    public bool Exists => this.Outcome !=
        InventoryManagementReplayOutcome.Missing;

    public Result<RoomInventoryMutationReceiptDto> ToResult() =>
        this.Outcome switch
        {
            InventoryManagementReplayOutcome.Exact when
                this.Receipt is not null => Result.Success(this.Receipt),
            InventoryManagementReplayOutcome.Conflict =>
                Result.Failure<RoomInventoryMutationReceiptDto>(
                    InventoryApplicationErrors.ManagementOperationConflict),
            _ => throw new InvalidOperationException(
                "A missing Inventory management operation has no replay result.")
        };
}

internal enum InventoryManagementReplayOutcome
{
    Missing = 0,
    Exact = 1,
    Conflict = 2
}
