namespace BunkFy.Adapter.Abstractions;

public interface IAdapterPushObservationSink
{
    Task<AdapterIngressSubmissionResponse> SubmitAsync(
        IReadOnlyCollection<AdapterObservedRecord> records,
        CancellationToken cancellationToken);
}
