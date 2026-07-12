namespace BunkFy.Adapter.Abstractions;

public interface IAdapterObservationSink
{
    Task<AdapterObservationAcknowledgement> SubmitAsync(
        AdapterObservationSubmission submission,
        CancellationToken cancellationToken);
}
