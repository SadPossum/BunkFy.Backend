namespace BunkFy.Parsers.ReservationMail;

using BunkFy.ObservationParsing;

public sealed class ReservationMailParserDescriptor : IObservationParserDescriptorProvider
{
    public const string ParserType = "mail.reservation-json";
    public const int ParserVersion = 1;
    public const string SourceAdapterType = "imap.reservation-json";
    public const string SourceRecordType = "mail.unparsed.v1";
    public const string OutputRecordType = "reservation.v1";

    public static ObservationParserDescriptor Value { get; } = new(
        ParserType,
        ParserVersion,
        [SourceAdapterType],
        [SourceRecordType],
        [OutputRecordType]);

    public ObservationParserDescriptor Descriptor => Value;
}
