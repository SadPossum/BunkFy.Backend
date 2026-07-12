namespace BunkFy.Adapters.Http;

public sealed class StaticAdapterIngressTokenProvider : IAdapterIngressTokenProvider
{
    private readonly string token;

    public StaticAdapterIngressTokenProvider(string token)
    {
        AdapterIngressTokenRules.Validate(token);
        this.token = token;
    }

    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(this.token);
    }

    public override string ToString() => nameof(StaticAdapterIngressTokenProvider);
}

internal static class AdapterIngressTokenRules
{
    public const int MaximumLength = 512;

    public static void Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumLength ||
            token.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("The adapter ingress token is invalid.", nameof(token));
        }
    }
}
