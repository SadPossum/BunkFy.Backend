namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterPollingCapability
{
    private static readonly TimeSpan MaximumInterval = TimeSpan.FromDays(30);

    public AdapterPollingCapability(TimeSpan minimumInterval, TimeSpan recommendedInterval)
    {
        if (minimumInterval < TimeSpan.FromSeconds(1) ||
            recommendedInterval < minimumInterval ||
            recommendedInterval > MaximumInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recommendedInterval),
                "Polling intervals must be ordered and between one second and 30 days.");
        }

        this.MinimumInterval = minimumInterval;
        this.RecommendedInterval = recommendedInterval;
    }

    public TimeSpan MinimumInterval { get; }
    public TimeSpan RecommendedInterval { get; }
}
