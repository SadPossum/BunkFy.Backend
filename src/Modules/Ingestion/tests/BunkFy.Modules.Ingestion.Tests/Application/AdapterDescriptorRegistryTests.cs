namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Queries;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterDescriptorRegistryTests
{
    [Fact]
    public async Task Registry_normalizes_lookup_and_query_exposes_safe_capabilities()
    {
        AdapterDescriptor descriptor = new(
            "booking.email",
            protocolVersion: 2,
            configurationSchemaVersion: 3,
            [AdapterExecutionMode.Polling],
            new AdapterPollingCapability(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5)));
        AdapterDescriptorRegistry registry = new([new TestProvider(descriptor)]);

        Assert.True(registry.TryGet(" BOOKING.EMAIL ", out AdapterDescriptor? found));
        Assert.Same(descriptor, found);

        var result = await new ListAdapterTypeCapabilitiesQueryHandler(registry).HandleAsync(
            new ListAdapterTypeCapabilitiesQuery(), CancellationToken.None);
        var capability = Assert.Single(result.Value.AdapterTypes);
        Assert.Equal("booking.email", capability.AdapterType);
        Assert.Equal(2, capability.ProtocolVersion);
        Assert.Equal(3, capability.ConfigurationSchemaVersion);
        Assert.Equal(60, capability.MinimumPollingIntervalSeconds);
        Assert.Equal(300, capability.RecommendedPollingIntervalSeconds);
    }

    [Fact]
    public void Registry_rejects_ambiguous_adapter_types()
    {
        AdapterDescriptor first = new("booking.email", 1, 1, [AdapterExecutionMode.Polling]);
        AdapterDescriptor second = new("booking.email", 2, 2, [AdapterExecutionMode.Push]);

        Assert.Throws<InvalidOperationException>(() => new AdapterDescriptorRegistry(
            [new TestProvider(first), new TestProvider(second)]));
    }

    private sealed record TestProvider(AdapterDescriptor Descriptor) : IAdapterDescriptorProvider;
}
