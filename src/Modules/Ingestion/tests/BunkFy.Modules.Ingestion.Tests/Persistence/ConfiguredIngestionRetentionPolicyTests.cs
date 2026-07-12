namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.Extensions.Configuration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ConfiguredIngestionRetentionPolicyTests
{
    private static readonly Guid PropertyId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ConnectionId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_keep_raw_and_normalized_history_independent()
    {
        ConfiguredIngestionRetentionPolicy policy =
            ConfiguredIngestionRetentionPolicy.FromConfiguration(new ConfigurationManager());

        Assert.Equal(Now.AddDays(30), policy.GetRawPayloadRetainUntilUtc(PropertyId, ConnectionId, Now));
        Assert.Equal(Now.AddDays(90), policy.GetSensitiveHistoryRetainUntilUtc(PropertyId, ConnectionId, Now));
    }

    [Fact]
    public void Sensitive_history_override_does_not_rewrite_raw_payload_policy()
    {
        ConfigurationManager configuration = new();
        configuration[ConfiguredIngestionRetentionPolicy.SensitiveHistoryConfigurationKey] = "14.00:00:00";
        ConfiguredIngestionRetentionPolicy policy =
            ConfiguredIngestionRetentionPolicy.FromConfiguration(configuration);

        Assert.Equal(Now.AddDays(30), policy.GetRawPayloadRetainUntilUtc(PropertyId, ConnectionId, Now));
        Assert.Equal(Now.AddDays(14), policy.GetSensitiveHistoryRetainUntilUtc(PropertyId, ConnectionId, Now));
    }

    [Theory]
    [InlineData("00:30:00")]
    [InlineData("not-a-duration")]
    [InlineData("3651.00:00:00")]
    public void Invalid_sensitive_history_policy_fails_startup(string value)
    {
        ConfigurationManager configuration = new();
        configuration[ConfiguredIngestionRetentionPolicy.SensitiveHistoryConfigurationKey] = value;

        Assert.Throws<InvalidOperationException>(() =>
            ConfiguredIngestionRetentionPolicy.FromConfiguration(configuration));
    }
}
