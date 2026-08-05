namespace BunkFy.Host.Migrations.Tests;

using Microsoft.Extensions.Configuration;

public sealed class MigrationsHostOptionsTests
{
    [Fact]
    public void Missing_configuration_uses_bounded_development_defaults()
    {
        MigrationsHostOptions options = MigrationsHostOptions.FromConfiguration(
            BuildConfiguration([]));

        Assert.Equal(MigrationExecutionMode.Apply, options.Mode);
        Assert.Equal(60, options.LockAcquireTimeoutSeconds);
        Assert.Equal(250, options.LockRetryDelayMilliseconds);
        Assert.Equal(1800, options.OperationTimeoutSeconds);
        Assert.Equal(300, options.CommandTimeoutSeconds);
    }

    [Fact]
    public void Plan_mode_is_parsed_case_insensitively()
    {
        MigrationsHostOptions options = MigrationsHostOptions.FromConfiguration(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["Migrations:Mode"] = "plan"
            }));

        Assert.Equal(MigrationExecutionMode.Plan, options.Mode);
    }

    [Theory]
    [InlineData("Migrations:Mode", "Preview")]
    [InlineData("Migrations:LockAcquireTimeoutSeconds", "0")]
    [InlineData("Migrations:LockRetryDelayMilliseconds", "20")]
    [InlineData("Migrations:OperationTimeoutSeconds", "10")]
    [InlineData("Migrations:CommandTimeoutSeconds", "3")]
    public void Invalid_runtime_configuration_fails_closed(string key, string value)
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?> { [key] = value });

        Assert.Throws<InvalidOperationException>(
            () => MigrationsHostOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void Command_timeout_cannot_exceed_total_operation_budget()
    {
        IConfiguration configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Migrations:OperationTimeoutSeconds"] = "60",
                ["Migrations:CommandTimeoutSeconds"] = "61"
            });

        Assert.Throws<InvalidOperationException>(
            () => MigrationsHostOptions.FromConfiguration(configuration));
    }

    internal static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
