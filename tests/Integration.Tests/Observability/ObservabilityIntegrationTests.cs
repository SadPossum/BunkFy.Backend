namespace Integration.Tests;

using System.Net;
using Integration.Tests.Support;
using Xunit;

public sealed class ObservabilityIntegrationTests
{
    private const string UnusedSqlServerConnection =
        "Server=localhost,1433;Database=unused;User Id=sa;Password=Pass@word1;TrustServerCertificate=True";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Prometheus_endpoint_is_mapped_only_when_enabled()
    {
        await using AuthTestApplication disabledApplication = new(
            "SqlServer",
            UnusedSqlServerConnection,
            "nats://localhost:4222");
        using HttpClient disabledClient = disabledApplication.CreateClient();

        using HttpResponseMessage disabledResponse = await disabledClient.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, disabledResponse.StatusCode);

        await using AuthTestApplication enabledApplication = new(
            "SqlServer",
            UnusedSqlServerConnection,
            "nats://localhost:4222",
            enablePrometheus: true);
        using HttpClient enabledClient = enabledApplication.CreateClient();

        using HttpResponseMessage enabledResponse = await enabledClient.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);
        Assert.Contains("text/plain", enabledResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
    }
}
