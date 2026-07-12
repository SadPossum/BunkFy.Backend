namespace BunkFy.Modules.Ingestion.Application.Parsing;

using BunkFy.ObservationParsing;

public interface IObservationParserDescriptorRegistry
{
    IReadOnlyCollection<ObservationParserDescriptor> GetAll();

    bool TryGet(string parserType, int? parserVersion, out ObservationParserDescriptor? descriptor);
}

public interface IObservationParserRegistry
{
    bool TryGet(string parserType, int parserVersion, out IObservationParser? parser);
}
