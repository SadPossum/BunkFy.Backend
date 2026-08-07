namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Gma.Framework.Results;

internal sealed class PropertyMutationOperationJournal(
    IPropertyMutationOperationRepository operations)
{
    public async Task<PropertyMutationReplayDecision> InspectAsync(
        Property property,
        Guid operationId,
        PropertyMutationKind kind,
        long expectedVersion,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(property);
        PropertyMutationOperationRecord? existing =
            await operations.GetAsync(
                property.Id,
                operationId,
                cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return PropertyMutationReplayDecision.Missing;
        }

        return existing.Matches(
            kind,
            property.Id,
            expectedVersion,
            fingerprint)
            ? PropertyMutationReplayDecision.Exact(existing.ToReceipt())
            : PropertyMutationReplayDecision.Conflict;
    }

    public async Task<PropertyMutationReceiptDto> RecordAsync(
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
            new(
                operationId,
                property.ScopeId,
                property.Id,
                kind,
                expectedVersion,
                fingerprint,
                receipt.Status,
                receipt.ProcessingStatus,
                receipt.Version,
                completedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return receipt;
    }
}

internal readonly record struct PropertyMutationReplayDecision(
    PropertyMutationReplayOutcome Outcome,
    PropertyMutationReceiptDto? Receipt)
{
    public static PropertyMutationReplayDecision Missing => new(
        PropertyMutationReplayOutcome.Missing,
        null);

    public static PropertyMutationReplayDecision Conflict => new(
        PropertyMutationReplayOutcome.Conflict,
        null);

    public static PropertyMutationReplayDecision Exact(
        PropertyMutationReceiptDto receipt) => new(
        PropertyMutationReplayOutcome.Exact,
        receipt);

    public bool Exists => this.Outcome != PropertyMutationReplayOutcome.Missing;

    public Result<PropertyMutationReceiptDto> ToResult() =>
        this.Outcome switch
        {
            PropertyMutationReplayOutcome.Exact when this.Receipt is not null =>
                Result.Success(this.Receipt),
            PropertyMutationReplayOutcome.Conflict =>
                Result.Failure<PropertyMutationReceiptDto>(
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
