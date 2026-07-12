namespace BunkFy.Modules.Ingestion.Application.Reservations;

using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Receipts;

internal sealed class ReservationObservationPayloadLoader(IRawPayloadStore rawPayloads)
{
    public async Task<Result<NormalizedReservationObservation>> LoadAsync(
        ObservationReceipt receipt,
        CancellationToken cancellationToken)
    {
        RawPayloadRead? raw = await rawPayloads.ReadAsync(
            receipt.RawPayloadFileId,
            receipt.ScopeId,
            receipt.ConnectionId,
            cancellationToken).ConfigureAwait(false);
        if (raw is null || !string.Equals(raw.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(raw.ContentSha256, receipt.ContentHash, StringComparison.Ordinal))
        {
            return Result.Failure<NormalizedReservationObservation>(IngestionApplicationErrors.RawPayloadInvalid);
        }

        return ReservationObservationJsonNormalizer.Normalize(raw.Content.Span);
    }
}
