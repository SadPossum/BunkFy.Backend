namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Reservations;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;

[IntegrationEventHandler(IngestionModuleMetadata.ReceiptAcceptedHandlerName)]
internal sealed class ObservationReceiptAcceptedHandler(
    IObservationReceiptRepository receipts,
    IAdapterConnectionRepository connections,
    ReservationObservationPayloadLoader payloadLoader,
    ICommandHandler<DispatchNormalizedReservationObservationCommand, ReservationObservationDispatchResult> dispatcher,
    ISystemClock clock)
    : IIntegrationEventHandler<ObservationReceiptAcceptedIntegrationEvent>
{
    public async Task HandleAsync(
        ObservationReceiptAcceptedIntegrationEvent accepted,
        CancellationToken cancellationToken)
    {
        ObservationReceipt? receipt = await receipts.GetAsync(accepted.ReceiptId, cancellationToken).ConfigureAwait(false);
        if (receipt is null || receipt.State != ObservationReceiptState.Pending)
        {
            return;
        }

        AdapterConnection? connection = await connections.GetAsync(accepted.ConnectionId, cancellationToken).ConfigureAwait(false);
        if (connection is null || receipt.ConnectionId != connection.Id || receipt.PropertyId != accepted.PropertyId ||
            !string.Equals(receipt.SourceRecordType, ReservationObservationJsonNormalizer.RecordType, StringComparison.Ordinal))
        {
            _ = receipt.Reject("The observation record type or connection is not supported.", clock.UtcNow);
            return;
        }

        Result<NormalizedReservationObservation> normalized = await payloadLoader.LoadAsync(receipt, cancellationToken)
            .ConfigureAwait(false);
        if (normalized.IsFailure)
        {
            _ = receipt.Reject(normalized.Error.Code, clock.UtcNow);
            return;
        }

        Result<ReservationObservationDispatchResult> result = await dispatcher.HandleAsync(
            new DispatchNormalizedReservationObservationCommand(receipt.Id, normalized.Value),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            _ = receipt.Reject(result.Error.Code, clock.UtcNow);
        }
    }
}
