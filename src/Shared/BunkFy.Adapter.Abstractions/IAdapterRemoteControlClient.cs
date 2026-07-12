namespace BunkFy.Adapter.Abstractions;

public interface IAdapterRemoteControlClient
{
    Task<AdapterRemoteLeaseClaimResponse> ClaimAsync(
        AdapterRemoteLeaseClaimRequest request,
        CancellationToken cancellationToken);

    Task<AdapterRemoteLeaseRenewResponse> RenewAsync(
        AdapterRemoteLeaseRenewRequest request,
        CancellationToken cancellationToken);

    Task<AdapterRemoteObservationSubmissionResponse> SubmitAsync(
        AdapterRemoteObservationSubmissionRequest request,
        CancellationToken cancellationToken);

    Task<AdapterRemoteRunCompletionResponse> CompleteAsync(
        AdapterRemoteRunCompletionRequest request,
        CancellationToken cancellationToken);
}
