namespace BunkFy.Modules.Ingestion.Persistence;

using System.Globalization;
using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.Extensions.Configuration;

internal sealed class ConfiguredIngestionRetentionPolicy : IIngestionRetentionPolicy
{
    public const string RawPayloadConfigurationKey = "Ingestion:Retention:RawPayloadRetention";
    public const string SensitiveHistoryConfigurationKey = "Ingestion:Retention:SensitiveHistoryRetention";
    public static readonly TimeSpan DefaultRawPayloadRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan DefaultSensitiveHistoryRetention = TimeSpan.FromDays(90);
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumRetention = TimeSpan.FromDays(3650);

    private readonly TimeSpan rawPayloadRetention;
    private readonly TimeSpan sensitiveHistoryRetention;

    private ConfiguredIngestionRetentionPolicy(
        TimeSpan rawPayloadRetention,
        TimeSpan sensitiveHistoryRetention)
    {
        this.rawPayloadRetention = rawPayloadRetention;
        this.sensitiveHistoryRetention = sensitiveHistoryRetention;
    }

    public static ConfiguredIngestionRetentionPolicy FromConfiguration(IConfiguration configuration)
    {
        return new ConfiguredIngestionRetentionPolicy(
            Parse(configuration, RawPayloadConfigurationKey, DefaultRawPayloadRetention),
            Parse(configuration, SensitiveHistoryConfigurationKey, DefaultSensitiveHistoryRetention));
    }

    public DateTimeOffset GetRawPayloadRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset receivedAtUtc)
    {
        EnsureIdentity(propertyId, connectionId);
        return receivedAtUtc.Add(this.rawPayloadRetention);
    }

    public DateTimeOffset GetSensitiveHistoryRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset terminalAtUtc)
    {
        EnsureIdentity(propertyId, connectionId);
        return terminalAtUtc.Add(this.sensitiveHistoryRetention);
    }

    private static TimeSpan Parse(IConfiguration configuration, string key, TimeSpan defaultValue)
    {
        string? configured = configuration[key];
        TimeSpan retention = string.IsNullOrWhiteSpace(configured)
            ? defaultValue
            : TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out TimeSpan parsed)
                ? parsed
                : throw new InvalidOperationException($"{key} must be a valid invariant TimeSpan.");

        if (retention < MinimumRetention || retention > MaximumRetention)
        {
            throw new InvalidOperationException($"{key} must be between {MinimumRetention} and {MaximumRetention}.");
        }

        return retention;
    }

    private static void EnsureIdentity(Guid propertyId, Guid connectionId)
    {
        if (propertyId == Guid.Empty || connectionId == Guid.Empty)
        {
            throw new ArgumentException("A complete ingestion identity is required to resolve retention.");
        }
    }
}
