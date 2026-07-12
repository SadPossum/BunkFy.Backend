namespace BunkFy.ObservationParsing;

public interface IObservationParserDescriptorProvider
{
    ObservationParserDescriptor Descriptor { get; }
}

public interface IObservationParser : IObservationParserDescriptorProvider
{
    Task<ObservationParserResult> ParseAsync(
        ObservationParserInput input,
        CancellationToken cancellationToken);
}
