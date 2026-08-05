namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsApplicationCompositionTests
{
    [Fact]
    public async Task Public_profile_scheduler_fails_closed_when_task_runtime_is_not_composed()
    {
        ServiceCollection services = new();
        services.AddDataRightsUnavailableTenantTerminationTaskScheduling();
        await using ServiceProvider provider = services.BuildServiceProvider();
        ITenantTerminationTaskScheduler scheduler = provider
            .GetRequiredService<ITenantTerminationTaskScheduler>();

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scheduler.EnqueueVerificationAsync(
                "tenant-a",
                Guid.NewGuid(),
                operationRevision: 1,
                CancellationToken.None));

        Assert.Equal(UnavailableTenantTerminationTaskScheduler.ErrorCode, failure.Message);
    }
}
