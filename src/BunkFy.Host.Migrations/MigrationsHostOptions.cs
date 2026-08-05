namespace BunkFy.Host.Migrations;

using Microsoft.Extensions.Configuration;

public enum MigrationExecutionMode
{
    Plan = 0,
    Apply = 1
}

public sealed record MigrationsHostOptions
{
    public const string SectionName = "Migrations";

    private MigrationsHostOptions() { }

    public MigrationExecutionMode Mode { get; init; } = MigrationExecutionMode.Apply;
    public int LockAcquireTimeoutSeconds { get; init; } = 60;
    public int LockRetryDelayMilliseconds { get; init; } = 250;
    public int OperationTimeoutSeconds { get; init; } = 1800;
    public int CommandTimeoutSeconds { get; init; } = 300;

    public static MigrationsHostOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        IConfigurationSection section = configuration.GetSection(SectionName);

        MigrationsHostOptions options = new()
        {
            Mode = ParseMode(section["Mode"]),
            LockAcquireTimeoutSeconds = ParsePositiveInt(
                section["LockAcquireTimeoutSeconds"],
                60,
                $"{SectionName}:LockAcquireTimeoutSeconds"),
            LockRetryDelayMilliseconds = ParsePositiveInt(
                section["LockRetryDelayMilliseconds"],
                250,
                $"{SectionName}:LockRetryDelayMilliseconds"),
            OperationTimeoutSeconds = ParsePositiveInt(
                section["OperationTimeoutSeconds"],
                1800,
                $"{SectionName}:OperationTimeoutSeconds"),
            CommandTimeoutSeconds = ParsePositiveInt(
                section["CommandTimeoutSeconds"],
                300,
                $"{SectionName}:CommandTimeoutSeconds")
        };

        options.ValidateOrThrow();
        return options;
    }

    public void ValidateOrThrow()
    {
        List<string> failures = [];
        if (!Enum.IsDefined(this.Mode))
        {
            failures.Add($"{SectionName}:Mode must be Plan or Apply.");
        }

        if (this.LockAcquireTimeoutSeconds is < 1 or > 600)
        {
            failures.Add(
                $"{SectionName}:LockAcquireTimeoutSeconds must be between 1 and 600.");
        }

        if (this.LockRetryDelayMilliseconds is < 50 or > 5000)
        {
            failures.Add(
                $"{SectionName}:LockRetryDelayMilliseconds must be between 50 and 5000.");
        }

        if (this.OperationTimeoutSeconds is < 30 or > 7200)
        {
            failures.Add(
                $"{SectionName}:OperationTimeoutSeconds must be between 30 and 7200.");
        }

        if (this.CommandTimeoutSeconds is < 5 or > 1800)
        {
            failures.Add(
                $"{SectionName}:CommandTimeoutSeconds must be between 5 and 1800.");
        }

        if (this.CommandTimeoutSeconds > this.OperationTimeoutSeconds)
        {
            failures.Add(
                $"{SectionName}:CommandTimeoutSeconds cannot exceed OperationTimeoutSeconds.");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Migrations host configuration is invalid: " +
                string.Join(" ", failures));
        }
    }

    private static MigrationExecutionMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MigrationExecutionMode.Apply;
        }

        if (!Enum.TryParse(value.Trim(), ignoreCase: true,
                out MigrationExecutionMode parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Mode must be Plan or Apply.");
        }

        return parsed;
    }

    private static int ParsePositiveInt(string? value, int fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!int.TryParse(
                value.Trim(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException($"{name} must be a positive integer.");
        }

        return parsed;
    }
}
