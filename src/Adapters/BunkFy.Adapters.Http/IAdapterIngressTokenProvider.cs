namespace BunkFy.Adapters.Http;

public interface IAdapterIngressTokenProvider
{
    ValueTask<string> GetTokenAsync(CancellationToken cancellationToken);
}
