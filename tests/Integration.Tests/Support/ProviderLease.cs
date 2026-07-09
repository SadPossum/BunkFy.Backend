namespace Integration.Tests.Support;

internal sealed class ProviderLease(IAsyncDisposable container, string connectionString) : IAsyncDisposable
{
    public string ConnectionString { get; } = connectionString;

    public ValueTask DisposeAsync() => container.DisposeAsync();
}
