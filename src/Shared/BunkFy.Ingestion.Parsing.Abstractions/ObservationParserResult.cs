namespace BunkFy.ObservationParsing;

public enum ObservationParserDisposition
{
    Parsed = 1,
    NoMatch = 2
}

public sealed record ObservationParserResult : IDisposable
{
    private ObservationParserResult(
        ObservationParserDisposition disposition,
        IReadOnlyCollection<ParsedObservation> outputs,
        string? reasonCode)
    {
        this.Disposition = disposition;
        this.Outputs = outputs;
        this.ReasonCode = reasonCode;
    }

    public ObservationParserDisposition Disposition { get; }
    public IReadOnlyCollection<ParsedObservation> Outputs { get; }
    public string? ReasonCode { get; }

    public static ObservationParserResult Parsed(IReadOnlyCollection<ParsedObservation> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ParsedObservation[] values = outputs.ToArray();
        if (values.Length is <= 0 or > ObservationParserLimits.MaximumOutputs ||
            values.Sum(output => (long)output.Payload.Length) > ObservationParserLimits.MaximumAggregateOutputBytes)
        {
            throw new ArgumentException("The parsed output batch is outside protocol bounds.", nameof(outputs));
        }

        return new(ObservationParserDisposition.Parsed, values, reasonCode: null);
    }

    public static ObservationParserResult NoMatch(string reasonCode) => new(
        ObservationParserDisposition.NoMatch,
        [],
        ObservationParserGuards.StableKey(
            reasonCode, ObservationParserLimits.ErrorCodeMaxLength, nameof(reasonCode)));

    public void Dispose()
    {
        foreach (ParsedObservation output in this.Outputs)
        {
            output.Dispose();
        }
    }
}
