namespace BunkFy.Parsers.ReservationMail;

using BunkFy.Adapter.Abstractions;
using BunkFy.ObservationParsing;

internal sealed class ReservationMailObservationParser : IObservationParser
{
    private const int MaximumAttachmentBytes = 1024 * 1024;

    public ObservationParserDescriptor Descriptor => ReservationMailParserDescriptor.Value;

    public async Task<ObservationParserResult> ParseAsync(
        ObservationParserInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!this.Descriptor.Supports(input.AdapterType, input.SourceRecordType) ||
            !string.Equals(input.ContentType, "message/rfc822", StringComparison.Ordinal))
        {
            return ObservationParserResult.NoMatch("mail.reservation-json.unsupported-source");
        }

        using ReservationMailEnvelopeContent? envelope = await ReservationMailEnvelopeReader.TryReadAsync(
            input.Payload,
            requiredAttachmentFileName: null,
            MaximumAttachmentBytes,
            cancellationToken).ConfigureAwait(false);
        if (envelope is null)
        {
            return ObservationParserResult.NoMatch("mail.reservation-json.no-match");
        }

        string hash = AdapterPayloadHash.ComputeSha256(envelope.Payload);
        return ObservationParserResult.Parsed([
            new ParsedObservation(
                ReservationMailParserDescriptor.OutputRecordType,
                envelope.ExternalRecordId,
                envelope.SourceRevision,
                envelope.SourceUpdatedAtUtc,
                input.ObservedAtUtc,
                "application/json",
                envelope.Payload,
                hash)
        ]);
    }
}
