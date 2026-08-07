namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Results;

internal sealed class PropertyMutationOperationJournal(
    IPropertyMutationOperationRepository operations)
{
    public Task<PropertyMutationReplayDecision<PropertyMutationReceiptDto>>
        InspectPropertyAsync(
        Property property,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        return this.InspectAsync(
            property.Id,
            PropertyMutationResourceKind.Property,
            property.Id,
            operationId,
            kind,
            expectedVersion,
            fingerprint,
            static operation => operation.ToPropertyReceipt(),
            cancellationToken);
    }

    public Task<PropertyMutationReplayDecision<RoomMutationReceiptDto>>
        InspectRoomAsync(
        Guid propertyId,
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        CancellationToken cancellationToken) => this.InspectAsync(
            propertyId,
            resourceKind,
            resourceId,
            operationId,
            kind,
            expectedVersion,
            fingerprint,
            static operation => operation.ToRoomReceipt(),
            cancellationToken);

    private async Task<PropertyMutationReplayDecision<TReceipt>> InspectAsync<TReceipt>(
        Guid propertyId,
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        Func<PropertyMutationOperationRecord, TReceipt> receiptFactory,
        CancellationToken cancellationToken)
        where TReceipt : class
    {
        PropertyMutationOperationRecord? existing =
            await operations.GetAsync(
                resourceKind,
                resourceId,
                operationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return PropertyMutationReplayDecision<TReceipt>.Missing;
        }

        return existing.Matches(
            kind,
            propertyId,
            resourceKind,
            resourceId,
            expectedVersion,
            fingerprint)
            ? PropertyMutationReplayDecision<TReceipt>.Exact(
                receiptFactory(existing))
            : PropertyMutationReplayDecision<TReceipt>.Conflict;
    }

    public async Task<PropertyMutationReceiptDto> RecordPropertyAsync(
        Property property,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        PropertyMutationReceiptDto receipt =
            PropertiesMapper.ToReceipt(property);
        await operations.AddAsync(
            PropertyMutationOperationRecord.ForProperty(
                operationId,
                property.ScopeId,
                property.Id,
                kind,
                expectedVersion,
                fingerprint,
                receipt,
                completedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }

    public async Task<RoomMutationReceiptDto> RecordRoomAsync(
        Room room,
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        long resultResourceVersion,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(room);
        RoomMutationReceiptDto receipt = PropertiesMapper.ToReceipt(room);
        await operations.AddAsync(
            PropertyMutationOperationRecord.ForRoom(
                operationId,
                room.ScopeId,
                room.PropertyId,
                resourceKind,
                resourceId,
                kind,
                expectedVersion,
                fingerprint,
                receipt,
                resultResourceVersion,
                completedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }
}

internal readonly record struct PropertyMutationReplayDecision<TReceipt>(
    PropertyMutationReplayOutcome Outcome,
    TReceipt? Receipt)
    where TReceipt : class
{
    public static PropertyMutationReplayDecision<TReceipt> Missing => new(
        PropertyMutationReplayOutcome.Missing,
        null);

    public static PropertyMutationReplayDecision<TReceipt> Conflict => new(
        PropertyMutationReplayOutcome.Conflict,
        null);

    public static PropertyMutationReplayDecision<TReceipt> Exact(
        TReceipt receipt) => new(
        PropertyMutationReplayOutcome.Exact,
        receipt);

    public bool Exists => this.Outcome != PropertyMutationReplayOutcome.Missing;

    public Result<TReceipt> ToResult() =>
        this.Outcome switch
        {
            PropertyMutationReplayOutcome.Exact when this.Receipt is not null =>
                Result.Success(this.Receipt),
            PropertyMutationReplayOutcome.Conflict =>
                Result.Failure<TReceipt>(
                    PropertiesApplicationErrors.ManagementOperationConflict),
            _ => throw new InvalidOperationException(
                "A missing property mutation operation has no replay result.")
        };
}

internal enum PropertyMutationReplayOutcome
{
    Missing = 0,
    Exact = 1,
    Conflict = 2
}
