namespace BunkFy.ObservationParsing;

using BunkFy.Adapter.Abstractions;

public static class ObservationParserLimits
{
    public const int ParserTypeMaxLength = 100;
    public const int ErrorCodeMaxLength = AdapterProtocolLimits.ErrorCodeMaxLength;
    public const int MaximumOutputs = AdapterProtocolLimits.MaximumRecordsPerSubmission;
    public const int MaximumOutputBytes = AdapterProtocolLimits.MaximumInlinePayloadBytes;
    public const int MaximumAggregateOutputBytes = AdapterProtocolLimits.MaximumSubmissionPayloadBytes;
}
