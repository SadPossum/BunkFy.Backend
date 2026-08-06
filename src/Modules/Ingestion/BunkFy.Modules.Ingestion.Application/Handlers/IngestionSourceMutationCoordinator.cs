namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using Gma.Framework.Scoping;

internal sealed record IngestionSourceMutationLease(
    IngestionSourceGraphCoordinate Coordinate,
    AdapterConnection Connection);

internal sealed class IngestionSourceMutationCoordinator(
    IIngestionSourceGraphLocator locator,
    IIngestionSourceOperationLock sourceLock,
    IngestionExecutionMutationCoordinator execution,
    IScopeContext scopeContext)
{
    public async Task<IngestionSourceMutationLease?> AcquireReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindReceiptAsync(receiptId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionSourceMutationLease?> AcquireProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindProposalAsync(proposalId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionSourceMutationLease?> AcquireDispatchAsync(
        Guid dispatchId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindDispatchAsync(dispatchId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionSourceMutationLease?> AcquireReprocessingAttemptAsync(
        Guid attemptId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindReprocessingAttemptAsync(
                    attemptId,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionSourceMutationLease?> AcquireSourceLinkAsync(
        Guid sourceLinkId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindSourceLinkAsync(sourceLinkId, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public async Task<IngestionSourceMutationLease?> AcquireAcceptedCancellationAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        await this.AcquireAsync(
            await locator.FindAcceptedCancellationAsync(
                    reservationId,
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    public Task<IngestionSourceMutationLease> AcquireIdentityAsync(
        Guid connectionId,
        string sourceReference,
        CancellationToken cancellationToken)
    {
        string scopeId = this.RequireScope();
        string source = sourceReference?.Trim() ?? string.Empty;
        if (connectionId == Guid.Empty || source.Length == 0)
        {
            throw new InvalidOperationException(
                "An Ingestion source mutation requires valid identity coordinates.");
        }

        Guid sourceLinkId = ReservationOperationIdentity.CreateSourceLinkId(
            scopeId,
            connectionId,
            source);
        return this.AcquireRequiredAsync(
            new(sourceLinkId, connectionId, sourceLinkId),
            cancellationToken);
    }

    public async Task<IngestionSourceMutationLease> AcquireIdentityAsync(
        AdapterConnection connection,
        string sourceReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        string scopeId = this.RequireScope();
        string source = sourceReference?.Trim() ?? string.Empty;
        if (connection.Id == Guid.Empty ||
            !string.Equals(
                connection.ScopeId,
                scopeId,
                StringComparison.Ordinal) ||
            source.Length == 0)
        {
            throw new InvalidOperationException(
                "An Ingestion source mutation requires valid identity coordinates.");
        }

        Guid sourceLinkId = ReservationOperationIdentity.CreateSourceLinkId(
            scopeId,
            connection.Id,
            source);
        await sourceLock.AcquireAsync(
                scopeId,
                sourceLinkId,
                cancellationToken)
            .ConfigureAwait(false);
        return new(
            new(sourceLinkId, connection.Id, sourceLinkId),
            connection);
    }

    public async Task AcquireAllAsync(
        IReadOnlyCollection<IngestionSourceGraphCoordinate> coordinates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (coordinates.Count == 0)
        {
            return;
        }

        string scopeId = this.RequireScope();
        if (coordinates.Any(coordinate =>
                coordinate.RecordId == Guid.Empty ||
                coordinate.ConnectionId == Guid.Empty ||
                coordinate.SourceLinkId == Guid.Empty))
        {
            throw new InvalidOperationException(
                "An Ingestion source mutation batch contains invalid coordinates.");
        }

        foreach (Guid connectionId in coordinates
                     .Select(coordinate => coordinate.ConnectionId)
                     .Distinct()
                     .Order())
        {
            _ = await execution.AcquireConnectionReadAsync(
                    connectionId,
                    cancellationToken)
                .ConfigureAwait(false) ??
                throw new InvalidOperationException(
                    "An Ingestion source graph references a missing connection.");
        }

        foreach (Guid sourceLinkId in coordinates
                     .Select(coordinate => coordinate.SourceLinkId)
                     .Distinct()
                     .Order())
        {
            await sourceLock.AcquireAsync(
                    scopeId,
                    sourceLinkId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<IngestionSourceMutationLease?> AcquireAsync(
        IngestionSourceGraphCoordinate? coordinate,
        CancellationToken cancellationToken) =>
        coordinate is null
            ? null
            : await this.AcquireRequiredAsync(coordinate, cancellationToken)
                .ConfigureAwait(false);

    private async Task<IngestionSourceMutationLease> AcquireRequiredAsync(
        IngestionSourceGraphCoordinate coordinate,
        CancellationToken cancellationToken)
    {
        string scopeId = this.RequireScope();
        if (coordinate.RecordId == Guid.Empty ||
            coordinate.ConnectionId == Guid.Empty ||
            coordinate.SourceLinkId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An Ingestion source mutation contains invalid coordinates.");
        }

        AdapterConnection connection =
            await execution.AcquireConnectionReadAsync(
                    coordinate.ConnectionId,
                    cancellationToken)
                .ConfigureAwait(false) ??
            throw new InvalidOperationException(
                "An Ingestion source graph references a missing connection.");
        await sourceLock.AcquireAsync(
                scopeId,
                coordinate.SourceLinkId,
                cancellationToken)
            .ConfigureAwait(false);
        return new(coordinate, connection);
    }

    private string RequireScope()
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            throw new InvalidOperationException(
                "An Ingestion source mutation requires a tenant scope.");
        }

        return scopeContext.ScopeId.Trim();
    }
}
