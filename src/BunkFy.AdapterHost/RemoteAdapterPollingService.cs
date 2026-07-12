namespace BunkFy.AdapterHost;

using System.Net;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class RemoteAdapterPollingService(
    IEnumerable<IAdapterRunner> runners,
    IAdapterRemoteControlClient remoteControl,
    IAdapterRuntimeMaterialProvider materialProvider,
    AdapterHostOptions options,
    AdapterHostStatus status,
    TimeProvider timeProvider,
    ILogger<RemoteAdapterPollingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IAdapterRunner runner = this.ResolveRunner();
        this.ValidateRunner(runner);
        DateTimeOffset? firstCycle = options.RunOnStart
            ? timeProvider.GetUtcNow()
            : timeProvider.GetUtcNow().Add(options.PollInterval);
        status.SetReady(hasCheckpoint: false, firstCycle);

        try
        {
            if (!options.RunOnStart)
            {
                await Task.Delay(options.PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTimeOffset startedAtUtc = timeProvider.GetUtcNow();
                status.SetRunning(startedAtUtc);
                TimeSpan delay;
                try
                {
                    using CancellationTokenSource timeout = new(options.MaximumRunDuration, timeProvider);
                    using CancellationTokenSource cycleCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                    RemoteLeasedAdapterCycleRunner cycle = new(
                        runner,
                        remoteControl,
                        materialProvider,
                        options.CreateRuntimeIdentity(),
                        options.WorkerId!.Value,
                        options.RemoteLeaseDuration,
                        timeProvider);
                    AdapterRunCompletion completion = await cycle.RunAsync(
                        cycleCancellation.Token).ConfigureAwait(false);
                    bool failed = completion.Outcome is AdapterRunOutcome.Failed or AdapterRunOutcome.Cancelled;
                    delay = failed ? this.ResolveFailureDelay(status.Snapshot().ConsecutiveFailures + 1) : options.PollInterval;
                    DateTimeOffset completedAtUtc = timeProvider.GetUtcNow();
                    status.SetCompleted(
                        completedAtUtc,
                        completion.Outcome,
                        StandaloneAdapterPollingService.SafeErrorCode(completion.ErrorCode),
                        completion.AcceptedCheckpoint is not null,
                        failed,
                        completedAtUtc.Add(delay));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
                {
                    delay = options.PollInterval;
                    DateTimeOffset completedAtUtc = timeProvider.GetUtcNow();
                    status.SetLeaseUnavailable(completedAtUtc, completedAtUtc.Add(delay));
                    logger.LogInformation(
                        "Remote adapter lease is unavailable; another claim attempt is scheduled.");
                }
                catch (Exception exception)
                {
                    int nextFailure = status.Snapshot().ConsecutiveFailures + 1;
                    delay = this.ResolveFailureDelay(nextFailure);
                    DateTimeOffset completedAtUtc = timeProvider.GetUtcNow();
                    status.SetException(
                        completedAtUtc,
                        status.Snapshot().HasCheckpoint,
                        completedAtUtc.Add(delay));
                    logger.LogWarning(
                        "Remote adapter cycle failed with failure type {FailureType}; retry is scheduled.",
                        exception.GetType().Name);
                }

                try
                {
                    await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            status.SetStopped();
        }
    }

    private IAdapterRunner ResolveRunner()
    {
        IAdapterRunner[] matching = runners.Where(candidate => string.Equals(
            candidate.Descriptor.AdapterType,
            options.AdapterType,
            StringComparison.Ordinal)).ToArray();
        return matching.Length == 1
            ? matching[0]
            : throw new InvalidOperationException(
                "Exactly one runner must match the configured remote adapter type.");
    }

    private void ValidateRunner(IAdapterRunner runner)
    {
        if (!runner.Descriptor.ExecutionModes.Contains(AdapterExecutionMode.Polling) ||
            !runner.Descriptor.ExecutionModes.Contains(AdapterExecutionMode.RemotePolling) ||
            runner.Descriptor.Polling is null || options.PollInterval < runner.Descriptor.Polling.MinimumInterval)
        {
            throw new InvalidOperationException(
                "The selected runner does not support the remote polling configuration.");
        }
    }

    private TimeSpan ResolveFailureDelay(int failureCount)
    {
        double multiplier = Math.Pow(2, Math.Min(failureCount - 1, 20));
        double milliseconds = Math.Min(
            options.RetryBaseDelay.TotalMilliseconds * multiplier,
            options.RetryMaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
