namespace BunkFy.Adapter.Runtime;

public interface IAdapterCheckpointStore
{
    ValueTask<IAdapterCheckpointLease> AcquireAsync(
        Guid connectionId,
        CancellationToken cancellationToken);
}

public interface IAdapterCheckpointLease : IAsyncDisposable
{
    Guid ConnectionId { get; }
    string? Checkpoint { get; }
    long Generation { get; }

    Task SaveAsync(
        string checkpoint,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);
}
